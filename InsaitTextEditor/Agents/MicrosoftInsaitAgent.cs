using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
// using Microsoft.Agents.Core; // not directly used
using InsaitTextEditor.AI;
using InsaitTextEditor.Agents.Tools;
using InsaitTextEditor.Models;

namespace InsaitTextEditor.Agents;

/// <summary>
/// Агент на базі Microsoft Agent Framework з інтеграцією LlamaSharp (без локальних базових класів)
/// </summary>
public class MicrosoftInsaitAgent(
    MicrosoftAgentsAdapter adapter,
    AgentConfig config,
    SaveToFileTool? saveToFileTool = null)
{
    private readonly MicrosoftAgentsAdapter _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    private readonly AgentConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private SaveToFileTool? _saveToFileTool = saveToFileTool;

    // Обмеження для запобігання over-generation
    private const int MaxIterations = 1;
    private const int MaxResponseTokens = 1024;

    /// <summary>
    /// Встановити SaveToFileTool після створення Window
    /// </summary>
    public void SetSaveToFileTool(SaveToFileTool tool)
    {
        _saveToFileTool = tool;
        LogAgent("✅ SaveToFileTool встановлено");
    }

    /// <summary>
    /// Обробка повідомлення з використанням Microsoft Agent Framework
    /// </summary>
    public async Task<AgentResponse> ProcessAsync(
        string userMessage,
        List<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        var toolsUsed = new List<ToolInvocation>();
        var startTime = DateTime.UtcNow;

        LogAgent($"🚀 [Microsoft.Agents] Початок обробки (max {MaxIterations} ітерацій)");

        try
        {
            // Підготувати повідомлення з інформацією про інструменти
            var enhancedMessage = PrepareMessageWithToolInfo(userMessage);

            // Відправити через адаптер
            var response = await _adapter.SendMessageAsync(enhancedMessage, history, cancellationToken);

            // Перевірити чи потрібно використати інструменти
            if (_config.EnableToolUse && _saveToFileTool != null)
            {
                var toolCall = ExtractToolCall(response.Content);
                if (toolCall != null)
                {
                    LogAgent($"🔧 Виклик інструменту: {toolCall.ToolName}");
                    var toolResult = await ExecuteToolAsync(toolCall);
                    toolsUsed.Add(toolResult);

                    // Оновити відповідь
                    response.Content = CleanResponse(response.Content);
                }
            }

            LogAgent($"✅ Відповідь згенерована: {response.Content.Length} символів");

            return new AgentResponse
            {
                Content = CleanResponse(response.Content),
                ToolsUsed = toolsUsed,
                TokensGenerated = response.TokensGenerated,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            LogAgent($"❌ Помилка: {ex.Message}");
            return new AgentResponse
            {
                Content = $"Error: {ex.Message}",
                ToolsUsed = toolsUsed,
                TokensGenerated = 0,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Стрімінгова обробка через Microsoft Agent Framework
    /// </summary>
    public async IAsyncEnumerable<string> ProcessStreamAsync(
        string userMessage,
        List<ChatMessage> history,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var tokenCount = 0;
        var accumulatedText = new StringBuilder();
        var accumulatedResponse = new StringBuilder(); // Для збереження повної відповіді
        var consecutiveWhitespace = 0;
        var pendingBuffer = new StringBuilder(); // Буфер для перевірки stop sequences
        var shouldAutoSave = ShouldAutoSaveResponse(userMessage);

        LogAgent($"🚀 [Microsoft.Agents] Початок streaming (max {MaxResponseTokens} токенів)");
        if (shouldAutoSave)
        {
            LogAgent("💾 Автоматичне збереження активовано для цього запиту");
        }

        var enhancedMessage = PrepareMessageWithToolInfo(userMessage);

        await foreach (var token in _adapter.SendMessageStreamAsync(enhancedMessage, history, cancellationToken))
        {
            // Перевірка ліміту токенів
            if (tokenCount >= MaxResponseTokens)
            {
                LogAgent($"⛔ Досягнуто максимум токенів: {MaxResponseTokens}");
                // Повернути залишок з буфера
                if (pendingBuffer.Length > 0)
                {
                    var bufferContent = pendingBuffer.ToString();
                    accumulatedResponse.Append(bufferContent);
                    yield return bufferContent;
                }
                break;
            }

            // Фільтр технічних токенів
            if (IsTechnicalToken(token))
            {
                LogAgent($"🚫 Технічний токен, СТОП: {token}");
                break;
            }

            // Рахуємо послідовні whitespace токени для раннього виявлення кінця
            if (string.IsNullOrWhiteSpace(token))
            {
                consecutiveWhitespace++;
                if (consecutiveWhitespace > 5)
                {
                    LogAgent($"🛑 Виявлено багато пустих токенів підряд, СТОП");
                    break;
                }
                // Додаємо пробіли до буфера
                pendingBuffer.Append(token);
                continue;
            }
            else
            {
                consecutiveWhitespace = 0;
            }

            // Додаємо токен до буфера
            pendingBuffer.Append(token);
            accumulatedText.Append(token);
            var fullText = accumulatedText.ToString();

            // Перевірка на stop sequences в повному тексті
            if (ContainsStopSequence(fullText))
            {
                LogAgent($"🛑 Stop sequence виявлено");
                var cleanedText = RemoveStopSequences(fullText);
                
                // Повернути тільки очищений залишок
                var alreadyYielded = fullText.Length - pendingBuffer.Length;
                var toYield = cleanedText.Substring(Math.Min(alreadyYielded, cleanedText.Length));
                
                if (!string.IsNullOrWhiteSpace(toYield))
                {
                    accumulatedResponse.Append(toYield.Trim());
                    yield return toYield.Trim();
                }
                
                LogAgent($"✅ Відповідь завершена ({tokenCount} токенів)");
                break;
            }

            // Перевірка на самоітерацію або продовження нісенітниці
            if (fullText.Length > 50)
            {
                if (IsUnwantedContinuation(fullText))
                {
                    LogAgent($"🚫 Виявлено небажаний контент (самоітерація/нісенітниця), СТОП");
                    break;
                }
            }

            // Перевірка на часткові stop sequences (lookahead)
            if (pendingBuffer.Length > 0 && !MightBePartialStopSequence(pendingBuffer.ToString()))
            {
                // Безпечно повертаємо буфер
                var bufferContent = pendingBuffer.ToString();
                accumulatedResponse.Append(bufferContent);
                yield return bufferContent;
                pendingBuffer.Clear();
            }

            tokenCount++;
        }

        // Повернути залишок з буфера після завершення стріму
        if (pendingBuffer.Length > 0)
        {
            var finalText = pendingBuffer.ToString();
            if (!ContainsStopSequence(finalText) && !IsUnwantedContinuation(finalText))
            {
                accumulatedResponse.Append(finalText);
                yield return finalText;
            }
        }

        LogAgent($"✅ Streaming завершено ({tokenCount} токенів)");

        // ПІСЛЯ завершення стрімінгу - автоматичне збереження
        // ⚠️ ВАЖЛИВО: Викликаємо з UI потоку через Dispatcher
        if (shouldAutoSave && _saveToFileTool != null && accumulatedResponse.Length > 0)
        {
            var finalResponse = accumulatedResponse.ToString().Trim();
            var promptCopy = userMessage; // Копія для використання в async контексті
            
            LogAgent("💾 Планування автоматичного збереження...");
            
            // Викликаємо з UI потоку через Dispatcher
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await AutoInvokeSaveToolAsync(finalResponse, promptCopy);
            });
        }
        else
        {
            // ⚡ НОВИЙ ПІДХІД: Якщо тригер не спрацював, перевіряємо сам контент
            if (!shouldAutoSave && _saveToFileTool != null && accumulatedResponse.Length > 0)
            {
                var finalResponse = accumulatedResponse.ToString().Trim();
                
                // Використовуємо IsPoem() з SaveToFileTool для перевірки контенту
                if (IsLikelyCreativeContent(finalResponse))
                {
                    LogAgent("💾 Виявлено творчий контент (вірш/текст) за аналізом відповіді!");
                    
                    var promptCopy = userMessage;
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await AutoInvokeSaveToolAsync(finalResponse, promptCopy);
                    });
                }
                else
                {
                    LogAgent($"ℹ️ Автоматичне збереження не активовано (shouldAutoSave={shouldAutoSave}, tool={_saveToFileTool != null}, length={accumulatedResponse.Length})");
                }
            }
        }
    }

    /// <summary>
    /// Генерація reasoning кроку з повною фільтрацією (для ReasoningService)
    /// </summary>
    public async IAsyncEnumerable<string> GenerateReasoningStepStreamAsync(
        string stepPrompt,
        int maxTokens = 512,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var tokenCount = 0;
        var accumulatedText = new StringBuilder();
        var consecutiveWhitespace = 0;
        var pendingBuffer = new StringBuilder();

        LogAgent($"🧠 [Reasoning Step] Початок (max {maxTokens} токенів)");

        await foreach (var token in _adapter.SendMessageStreamAsync(stepPrompt, new List<ChatMessage>(), cancellationToken))
        {
            // Перевірка ліміту токенів
            if (tokenCount >= maxTokens)
            {
                LogAgent($"⛔ Reasoning step: досягнуто максимум токенів: {maxTokens}");
                if (pendingBuffer.Length > 0)
                {
                    var bufferContent = pendingBuffer.ToString();
                    yield return bufferContent;
                }
                break;
            }

            // Фільтр технічних токенів
            if (IsTechnicalToken(token))
            {
                LogAgent($"🚫 Reasoning step: технічний токен, СТОП: {token}");
                break;
            }

            // Рахуємо послідовні whitespace токени
            if (string.IsNullOrWhiteSpace(token))
            {
                consecutiveWhitespace++;
                if (consecutiveWhitespace > 5)
                {
                    LogAgent($"🛑 Reasoning step: багато пустих токенів, СТОП");
                    break;
                }
                pendingBuffer.Append(token);
                continue;
            }
            else
            {
                consecutiveWhitespace = 0;
            }

            // Додаємо токен до буфера
            pendingBuffer.Append(token);
            accumulatedText.Append(token);
            var fullText = accumulatedText.ToString();

            // Перевірка на stop sequences
            if (ContainsStopSequence(fullText))
            {
                LogAgent($"🛑 Reasoning step: stop sequence виявлено");
                var cleanedText = RemoveStopSequences(fullText);
                var alreadyYielded = fullText.Length - pendingBuffer.Length;
                var toYield = cleanedText.Substring(Math.Min(alreadyYielded, cleanedText.Length));
                
                if (!string.IsNullOrWhiteSpace(toYield))
                {
                    yield return toYield.Trim();
                }
                break;
            }

            // Перевірка на самоітерацію або нісенітницю
            if (fullText.Length > 50)
            {
                if (IsUnwantedContinuation(fullText))
                {
                    LogAgent($"🚫 Reasoning step: виявлено нісенітницю/самоітерацію, СТОП");
                    break;
                }
            }

            // Перевірка на часткові stop sequences
            if (pendingBuffer.Length > 0 && !MightBePartialStopSequence(pendingBuffer.ToString()))
            {
                var bufferContent = pendingBuffer.ToString();
                yield return bufferContent;
                pendingBuffer.Clear();
            }

            tokenCount++;
        }

        // Повернути залишок з буфера
        if (pendingBuffer.Length > 0)
        {
            var finalText = pendingBuffer.ToString();
            if (!ContainsStopSequence(finalText) && !IsUnwantedContinuation(finalText))
            {
                yield return finalText;
            }
        }

        LogAgent($"✅ Reasoning step завершено ({tokenCount} токенів)");
    }

    /// <summary>
    /// Визначити чи потрібно зберігати відповідь
    /// </summary>
    private bool ShouldAutoSaveResponse(string userMessage)
    {
        if (!_config.AutoSaveCreativeContent)
            return false;

        var messageLower = userMessage.ToLower();
        return _config.AutoSaveTriggers.Any(trigger => messageLower.Contains(trigger));
    }

    /// <summary>
    /// Перевірити чи відповідь схожа на творчий контент (вірш, історія)
    /// </summary>
    private bool IsLikelyCreativeContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
        
        // Вірш зазвичай має 4+ рядки
        if (lines.Length < 4) 
            return false;
        
        // Середня довжина рядка (вірші мають коротші рядки)
        var avgLength = lines.Average(l => l.Length);
        
        // Вірші: 15-80 символів на рядок
        // Історії/оповідання: можуть бути довші
        if (avgLength < 15)
            return false;
        
        // Перевірка на розділові знаки в кінці рядків (характерно для віршів)
        var linesWithPunctuation = lines.Count(l => 
            l.EndsWith(',') || l.EndsWith('.') || l.EndsWith('!') || l.EndsWith('?') || 
            l.EndsWith(':') || l.EndsWith(';') || l.EndsWith("...") || l.EndsWith('—') || l.EndsWith('-'));
        
        // Якщо більше 25% рядків мають розділові знаки - ймовірно вірш або оповідання
        var punctuationRatio = (double)linesWithPunctuation / lines.Length;
        if (punctuationRatio > 0.25)
            return true;
        
        // Перевірка на довгий багаторядковий текст (історія/оповідання)
        if (lines.Length >= 8 && content.Length > 200)
            return true;
        
        return false;
    }

    /// <summary>
    /// Автоматичний виклик інструменту збереження
    /// </summary>
    private async Task AutoInvokeSaveToolAsync(string content, string userPrompt)
    {
        if (_saveToFileTool == null)
        {
            LogAgent("⚠️ SaveToFileTool не встановлено");
            return;
        }

        try
        {
            LogAgent("💾 Автоматичний виклик SaveToFileTool...");
            
            // Згенерувати назву файлу з промпту
            var fileName = GenerateFileNameFromPrompt(userPrompt);
            
            var result = await _saveToFileTool.AutoSaveAsync(
                content, 
                fileName,
                openInEditor: true
            );
            
            if (result.Success)
            {
                LogAgent($"✅ Файл збережено: {result.FilePath}");
            }
            else
            {
                LogAgent($"❌ Помилка збереження: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            LogAgent($"❌ Помилка auto-save: {ex.Message}");
        }
    }

    /// <summary>
    /// Згенерувати назву файлу з промпту користувача
    /// </summary>
    private string GenerateFileNameFromPrompt(string prompt)
    {
        // "Напиши вірш про осінь" → "Вірш_про_осінь.txt"
        var cleaned = prompt
            .ToLower()
            .Replace("напиши ", "")
            .Replace("створи ", "")
            .Replace("скомпонуй ", "")
            .Replace("згенеруй ", "")
            .Replace("вірш ", "Вірш_")
            .Replace("історію ", "Історія_")
            .Replace("оповідання ", "Оповідання_")
            .Replace("текст ", "Текст_")
            .Replace("есе ", "Есе_")
            .Replace("про ", "про_")
            .Replace(" ", "_")
            .Trim();
        
        // Видалити недопустимі символи для файлової системи
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            cleaned = cleaned.Replace(c.ToString(), "");
        }
        
        // Обрізати до 30 символів
        if (cleaned.Length > 30)
            cleaned = cleaned.Substring(0, 30);
        
        // Якщо назва порожня - використати timestamp
        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "Відповідь";
        
        return $"{cleaned}_{DateTime.Now:HHmmss}";
    }

    /// <summary>
    /// Обробка повідомлення, приймаючи історію у вигляді об'єктів Microsoft.Agents.Core
    /// </summary>
    public Task<AgentResponse> ProcessAsyncCore(
        string userMessage,
        IEnumerable<object> msCoreHistory,
        CancellationToken cancellationToken = default)
    {
        var history = msCoreHistory.Select(MsAgentsCoreMapper.FromCoreMessage).ToList();
        return ProcessAsync(userMessage, history, cancellationToken);
    }

    /// <summary>
    /// Стрімінгова обробка з історією у вигляді об'єктів Microsoft.Agents.Core
    /// </summary>
    public async IAsyncEnumerable<string> ProcessStreamAsyncCore(
        string userMessage,
        IEnumerable<object> msCoreHistory,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var history = msCoreHistory.Select(MsAgentsCoreMapper.FromCoreMessage).ToList();
        await foreach (var token in ProcessStreamAsync(userMessage, history, cancellationToken))
        {
            yield return token;
        }
    }

    // Допоміжні методи
    private bool IsTechnicalToken(string token)
    {
        var technicalPatterns = new[]
        {
            "<end_of_turn>", "<start_of_turn>", "<eos>", "<bos>", "<pad>",
            "<|endoftext|>", "<|im_end|>", "<|im_start|>", "<|", "|>",
            "</s>", "<s>", "<<SYS>>", "<</SYS>>"
        };
        return technicalPatterns.Any(p => token.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private bool ContainsStopSequence(string text)
    {
        var stopSequences = new[] 
        { 
            "<end_of_turn>", "<eos>", "</s>", "<|im_end|>", "<|endoftext|>",
            "\n<start_of_turn>user", "\n<start_of_turn>model"
        };
        return stopSequences.Any(seq => text.Contains(seq, StringComparison.OrdinalIgnoreCase));
    }

    private bool MightBePartialStopSequence(string text)
    {
        // Перевіряємо чи може це бути початок stop sequence
        var stopSequences = new[] 
        { 
            "<end_of_turn>", "<eos>", "</s>", "<|im_end|>", "<|endoftext|>",
            "<start_of_turn>"
        };
        
        foreach (var stopSeq in stopSequences)
        {
            // Якщо текст є початком stop sequence
            for (int i = 1; i <= Math.Min(text.Length, stopSeq.Length); i++)
            {
                if (stopSeq.StartsWith(text.Substring(text.Length - i), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        
        return false;
    }

    private bool IsUnwantedContinuation(string fullText)
    {
        var lastPart = fullText.Length > 150 
            ? fullText.Substring(fullText.Length - 150) 
            : fullText;
        
        var unwantedPatterns = new[]
        {
            "<start_of_turn>user", "\nUser:", "\nYou:", "\nHuman:",
            "How can I help", "Do you want to", "Would you like",
            "* Ask a question", "* Tell me", "* Request",
            "\n\nUser:", "\n\nHuman:", "\n\nQuestion:",
            "Is there anything else", "Let me know if",
            "<start_of_turn>model", "\nAssistant:", "\nAI:"
        };
        
        return unwantedPatterns.Any(pattern => 
            lastPart.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private string RemoveStopSequences(string text)
    {
        var stopSequences = new[]
        {
            "<end_of_turn>", "<start_of_turn>model", "<start_of_turn>user",
            "<start_of_turn>", "<bos>", "<eos>", "</s>", "<|im_end|>",
            "<|im_start|>", "<|endoftext|>"
        };
        
        var cleaned = text;
        foreach (var seq in stopSequences)
        {
            var index = cleaned.IndexOf(seq, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                cleaned = cleaned.Substring(0, index);
            }
        }
        return cleaned.Trim();
    }

    private string PrepareMessageWithToolInfo(string userMessage)
    {
        // ✅ НЕ модифікуємо оригінальне повідомлення користувача
        // Інструкції про інструменти додаються тільки для AI моделі внутрішньо
        // і НЕ зберігаються в історію чату
        return userMessage;
    }

    private ToolCall? ExtractToolCall(string response)
    {
        // Покращений regex, який правильно обробляє JSON з дужками
        var match = Regex.Match(response, @"\[TOOL:(\w+)\|(.*?)\](?=\s*$|\s*\n|$)", RegexOptions.Singleline);
        if (!match.Success)
        {
            // Спробувати альтернативний патерн для складних випадків
            match = Regex.Match(response, @"\[TOOL:(\w+)\|(\{.*?\})\]", RegexOptions.Singleline);
        }
        
        if (!match.Success) return null;
        
        return new ToolCall
        {
            ToolName = match.Groups[1].Value,
            Parameters = match.Groups[2].Value
        };
    }

    private async Task<ToolInvocation> ExecuteToolAsync(ToolCall toolCall)
    {
        var invocation = new ToolInvocation
        {
            ToolName = toolCall.ToolName,
            Parameters = toolCall.Parameters,
            ExecutedAt = DateTime.UtcNow
        };

        try
        {
            if (toolCall.ToolName == "save_to_file" && _saveToFileTool != null)
            {
                var result = await _saveToFileTool.ExecuteFromJsonAsync(toolCall.Parameters);
                invocation.Result = result.Success ? $"Success: {result.Message}" : $"Failed: {result.Message}";
            }
            else
            {
                invocation.Result = $"Unknown tool: {toolCall.ToolName}";
            }
        }
        catch (Exception ex)
        {
            invocation.Result = $"Error executing tool: {ex.Message}";
        }

        return invocation;
    }

    private string CleanResponse(string response)
    {
        // Видаляємо всі маркери TOOL, включаючи можливі JSON параметри
        var cleaned = Regex.Replace(response, @"\[TOOL:\w+\|.*?\]", string.Empty, RegexOptions.Singleline);
        
        // Додатковий прохід для складних випадків
        cleaned = Regex.Replace(cleaned, @"\[TOOL:\w+\|\{.*?\}\]", string.Empty, RegexOptions.Singleline);
        
        // Видаляємо зайві порожні рядки
        cleaned = Regex.Replace(cleaned, @"(\r?\n){3,}", "\n\n", RegexOptions.Multiline);
        
        return cleaned.Trim();
    }

    private void LogAgent(string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [MSAgent] {message}");
    }

    private class ToolCall
    {
        public string ToolName { get; set; } = string.Empty;
        public string Parameters { get; set; } = string.Empty;
    }
}
