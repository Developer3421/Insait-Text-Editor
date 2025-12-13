using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using InsaitTextEditor.AI;
using InsaitTextEditor.Agents.Tools;
using InsaitTextEditor.Models;

namespace InsaitTextEditor.Agents;

/// <summary>
/// Головний агент на базі Gemma-3-1B з підтримкою інструментів
/// </summary>
public class InsaitAgent
{
    private readonly GemmaModelManager _modelManager;
    private readonly PromptBuilder _promptBuilder;
    private readonly AgentConfig _config;
    private readonly SaveToFileTool? _saveToFileTool;

    // Обмеження для запобігання over-generation
    private const int MAX_ITERATIONS = 1;           // ⛔ Тільки 1 ітерація
    private const int MAX_RESPONSE_TOKENS = 1024;   // ⛔ Максимум токенів

    public InsaitAgent(
        GemmaModelManager modelManager,
        PromptBuilder promptBuilder,
        AgentConfig config,
        SaveToFileTool? saveToFileTool = null)
    {
        _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _saveToFileTool = saveToFileTool;
    }

    /// <summary>
    /// Обробити повідомлення користувача з можливістю використання інструментів
    /// </summary>
    public async Task<AgentResponse> ProcessMessageAsync(
        string userMessage,
        List<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        var toolsUsed = new List<ToolInvocation>();
        var startTime = DateTime.UtcNow;
        var iteration = 0;
        var fullResponse = new StringBuilder();

        LogAgent($"🚀 Початок обробки повідомлення (max {MAX_ITERATIONS} ітерацій)");

        try
        {
            var enhancedMessage = PrepareMessageWithToolInfo(userMessage);
            var response = await _modelManager.GenerateResponseAsync(
                enhancedMessage,
                history,
                cancellationToken);

            fullResponse.Append(response);

            while (iteration < MAX_ITERATIONS && _config.EnableToolUse)
            {
                var toolCall = ExtractToolCall(response);
                
                if (toolCall == null)
                    break;

                iteration++;
                LogAgent($"🔧 Виклик інструменту: {toolCall.ToolName}");

                var toolResult = await ExecuteToolAsync(toolCall, cancellationToken);
                toolsUsed.Add(toolResult);

                if (iteration >= MAX_ITERATIONS)
                {
                    LogAgent($"⛔ Досягнуто максимум ітерацій: {MAX_ITERATIONS}");
                    break;
                }

                var followUpPrompt = $"Tool '{toolCall.ToolName}' executed. Result: {toolResult.Result}\n\nContinue your response.";
                response = await _modelManager.GenerateResponseAsync(followUpPrompt, history, cancellationToken);
                fullResponse.Append("\n").Append(response);
            }

            var cleanedResponse = CleanResponse(fullResponse.ToString());
            LogAgent($"✅ Відповідь згенерована: {cleanedResponse.Length} символів");

            return new AgentResponse
            {
                Content = cleanedResponse,
                ToolsUsed = toolsUsed,
                TokensGenerated = EstimateTokens(cleanedResponse),
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            LogAgent($"❌ Помилка: {ex.Message}");
            return new AgentResponse
            {
                Content = $"Error processing message: {ex.Message}",
                ToolsUsed = toolsUsed,
                TokensGenerated = 0,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Стрімінгова обробка повідомлення з фільтрацією stop tokens та запобіганням самоітерації
    /// </summary>
    public async IAsyncEnumerable<string> ProcessMessageStreamAsync(
        string userMessage,
        List<ChatMessage> history,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var enhancedMessage = PrepareMessageWithToolInfo(userMessage);
        var accumulatedText = new StringBuilder();
        var yieldedLength = 0;
        var tokenCount = 0;

        LogAgent($"🚀 Початок streaming (max {MAX_RESPONSE_TOKENS} токенів)");

        await foreach (var token in _modelManager.GenerateResponseStreamAsync(enhancedMessage, history, cancellationToken))
        {
            if (tokenCount >= MAX_RESPONSE_TOKENS)
            {
                LogAgent($"⛔ Досягнуто максимум токенів: {MAX_RESPONSE_TOKENS}");
                yield break;
            }

            if (IsTechnicalToken(token))
            {
                LogAgent($"🚫 Технічний токен, СТОП: {token}");
                yield break;
            }

            accumulatedText.Append(token);
            var fullText = accumulatedText.ToString();
            
            if (ShouldSkipToken(token) && yieldedLength == 0)
            {
                continue;
            }
            
            if (ContainsStopSequence(fullText))
            {
                LogAgent($"🛑 Stop sequence виявлено");
                var cleanedText = RemoveStopSequences(fullText);
                var remainingText = cleanedText.Substring(yieldedLength);
                
                if (!string.IsNullOrWhiteSpace(remainingText))
                {
                    yield return remainingText;
                }
                
                LogAgent($"✅ Відповідь завершена ({tokenCount} токенів)");
                yield break;
            }
            
            if (fullText.Contains("<start_of_turn>user", StringComparison.OrdinalIgnoreCase) ||
                fullText.Contains("\nUser:", StringComparison.OrdinalIgnoreCase) ||
                fullText.Contains("\nYou:", StringComparison.OrdinalIgnoreCase))
            {
                LogAgent($"🚫 Самоітерація виявлена, СТОП");
                var stopPosition = FindSelfIterationStart(fullText);
                if (stopPosition > 0 && stopPosition > yieldedLength)
                {
                    var remainingText = fullText.Substring(yieldedLength, stopPosition - yieldedLength);
                    if (!string.IsNullOrWhiteSpace(remainingText))
                    {
                        yield return remainingText;
                    }
                }
                yield break;
            }
            
            if (!ShouldSkipToken(token))
            {
                yield return token;
                yieldedLength = fullText.Length;
                tokenCount++;
            }
        }

        LogAgent($"✅ Streaming завершено ({tokenCount} токенів)");
    }

    /// <summary>
    /// Перевірка чи токен є технічним (не для відображення)
    /// </summary>
    private bool IsTechnicalToken(string token)
    {
        var technicalPatterns = new[]
        {
            "<end_of_turn>",
            "<start_of_turn>",
            "<eos>",
            "<bos>",
            "<pad>",
            "<|endoftext|>",
            "<|im_end|>",
            "<|im_start|>",
            "<|",
            "|>"
        };
        
        return technicalPatterns.Any(p => token.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private void LogAgent(string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [Agent] {message}");
    }

    private string PrepareMessageWithToolInfo(string userMessage)
    {
        if (!_config.EnableToolUse || _saveToFileTool == null)
            return userMessage;

        var toolInfo = new StringBuilder();
        toolInfo.AppendLine("\nAvailable tools:");
        toolInfo.AppendLine($"- save_to_file: {_saveToFileTool.Description}");
        toolInfo.AppendLine("\nTo use a tool, respond with: [TOOL:tool_name|parameters]");
        toolInfo.AppendLine("Example: [TOOL:save_to_file|{{\"content\":\"text\",\"suggestedFileName\":\"file.txt\"}}]");
        toolInfo.AppendLine();
        toolInfo.Append(userMessage);

        return toolInfo.ToString();
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
        
        if (!match.Success)
            return null;

        return new ToolCall
        {
            ToolName = match.Groups[1].Value,
            Parameters = match.Groups[2].Value
        };
    }

    private async Task<ToolInvocation> ExecuteToolAsync(ToolCall toolCall, CancellationToken cancellationToken)
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
                invocation.Result = result.Success 
                    ? $"Success: {result.Message}" 
                    : $"Failed: {result.Message}";
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

    private int EstimateTokens(string text)
    {
        return text.Length / 4;
    }

    private bool ContainsStopSequence(string text)
    {
        var stopSequences = new[]
        {
            "<end_of_turn>",
            "<eos>",
            "</s>"
        };
        
        return stopSequences.Any(seq => text.Contains(seq, StringComparison.OrdinalIgnoreCase));
    }

    private string RemoveStopSequences(string text)
    {
        var stopSequences = new[]
        {
            "<end_of_turn>",
            "<start_of_turn>model",
            "<start_of_turn>user",
            "<start_of_turn>",
            "<bos>",
            "<eos>",
            "</s>"
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

    private bool ShouldSkipToken(string token)
    {
        var techTokens = new[]
        {
            "<start_of_turn>model",
            "<bos>"
        };
        
        return techTokens.Any(tech => token.Contains(tech, StringComparison.OrdinalIgnoreCase));
    }

    private int FindSelfIterationStart(string text)
    {
        var patterns = new[]
        {
            "<start_of_turn>user",
            "\nUser:",
            "\nYou:"
        };
        
        var minPosition = int.MaxValue;
        foreach (var pattern in patterns)
        {
            var index = text.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (index >= 0 && index < minPosition)
            {
                minPosition = index;
            }
        }
        
        return minPosition == int.MaxValue ? -1 : minPosition;
    }

    private class ToolCall
    {
        public string ToolName { get; set; } = string.Empty;
        public string Parameters { get; set; } = string.Empty;
    }
}
