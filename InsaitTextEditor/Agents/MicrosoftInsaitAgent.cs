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
/// Agent based on Microsoft Agent Framework with LlamaSharp integration (without local base classes).
/// </summary>
public class MicrosoftInsaitAgent(
    MicrosoftAgentsAdapter adapter,
    AgentConfig config,
    SaveToFileTool? saveToFileTool = null)
{
    private readonly MicrosoftAgentsAdapter _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
    private readonly AgentConfig _config = config ?? throw new ArgumentNullException(nameof(config));
    private SaveToFileTool? _saveToFileTool = saveToFileTool;

    // Limits to prevent over-generation
    private const int MaxIterations = 1;
    private const int MaxResponseTokens = 1024;

    /// <summary>
    /// Set SaveToFileTool after the Window is created.
    /// </summary>
    public void SetSaveToFileTool(SaveToFileTool tool)
    {
        _saveToFileTool = tool;
        LogAgent("✅ SaveToFileTool set");
    }

    /// <summary>
    /// Message processing using Microsoft Agent Framework.
    /// </summary>
    public async Task<AgentResponse> ProcessAsync(
        string userMessage,
        List<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        var toolsUsed = new List<ToolInvocation>();
        var startTime = DateTime.UtcNow;

        LogAgent($"🚀 [Microsoft.Agents] Start processing (max {MaxIterations} iterations)");

        try
        {
            // Prepare a message with tools information
            var enhancedMessage = PrepareMessageWithToolInfo(userMessage);

            // Send via adapter
            var response = await _adapter.SendMessageAsync(enhancedMessage, history, cancellationToken);

            // Check whether we need to use tools
            if (_config.EnableToolUse && _saveToFileTool != null)
            {
                var toolCall = ExtractToolCall(response.Content);
                if (toolCall != null)
                {
                    LogAgent($"🔧 Tool call: {toolCall.ToolName}");
                    var toolResult = await ExecuteToolAsync(toolCall);
                    toolsUsed.Add(toolResult);

                    // Update response
                    response.Content = CleanResponse(response.Content);
                }
            }

            LogAgent($"✅ Response generated: {response.Content.Length} chars");

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
            LogAgent($"❌ Error: {ex.Message}");
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
    /// Streaming processing via Microsoft Agent Framework.
    /// </summary>
    public async IAsyncEnumerable<string> ProcessStreamAsync(
        string userMessage,
        List<ChatMessage> history,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var tokenCount = 0;
        var accumulatedText = new StringBuilder();
        var accumulatedResponse = new StringBuilder(); // For keeping the full response
        var consecutiveWhitespace = 0;
        var pendingBuffer = new StringBuilder(); // Buffer for stop-sequence checks
        var shouldAutoSave = ShouldAutoSaveResponse(userMessage);

        LogAgent($"🚀 [Microsoft.Agents] Start streaming (max {MaxResponseTokens} tokens)");
        if (shouldAutoSave)
        {
            LogAgent("💾 Auto-save enabled for this prompt");
        }

        var enhancedMessage = PrepareMessageWithToolInfo(userMessage);

        await foreach (var token in _adapter.SendMessageStreamAsync(enhancedMessage, history, cancellationToken))
        {
            // Token limit check
            if (tokenCount >= MaxResponseTokens)
            {
                LogAgent($"⛔ Max tokens reached: {MaxResponseTokens}");
                // Return the remaining buffer
                if (pendingBuffer.Length > 0)
                {
                    var bufferContent = pendingBuffer.ToString();
                    accumulatedResponse.Append(bufferContent);
                    yield return bufferContent;
                }
                break;
            }

            // Technical token filter
            if (IsTechnicalToken(token))
            {
                LogAgent($"🚫 Technical token, STOP: {token}");
                break;
            }

            // Count consecutive whitespace tokens for early end detection
            if (string.IsNullOrWhiteSpace(token))
            {
                consecutiveWhitespace++;
                if (consecutiveWhitespace > 5)
                {
                    LogAgent($"🛑 Too many empty tokens in a row, STOP");
                    break;
                }
                // Add whitespace to buffer
                pendingBuffer.Append(token);
                continue;
            }
            else
            {
                consecutiveWhitespace = 0;
            }

            // Add token to buffer
            pendingBuffer.Append(token);
            accumulatedText.Append(token);
            var fullText = accumulatedText.ToString();

            // Stop sequence check in the full text
            if (ContainsStopSequence(fullText))
            {
                LogAgent($"🛑 Stop sequence detected");
                var cleanedText = RemoveStopSequences(fullText);
                
                // Return only the cleaned remainder
                var alreadyYielded = fullText.Length - pendingBuffer.Length;
                var toYield = cleanedText.Substring(Math.Min(alreadyYielded, cleanedText.Length));
                
                if (!string.IsNullOrWhiteSpace(toYield))
                {
                    accumulatedResponse.Append(toYield.Trim());
                    yield return toYield.Trim();
                }
                
                LogAgent($"✅ Response completed ({tokenCount} tokens)");
                break;
            }

            // Check for self-iteration or unwanted continuation
            if (fullText.Length > 50)
            {
                if (IsUnwantedContinuation(fullText))
                {
                    LogAgent($"🚫 Unwanted content detected (self-iteration/gibberish), STOP");
                    break;
                }
            }

            // Partial stop sequence check (lookahead)
            if (pendingBuffer.Length > 0 && !MightBePartialStopSequence(pendingBuffer.ToString()))
            {
                // Safe to flush buffer
                var bufferContent = pendingBuffer.ToString();
                accumulatedResponse.Append(bufferContent);
                yield return bufferContent;
                pendingBuffer.Clear();
            }

            tokenCount++;
        }

        // Return remaining buffer after stream ends
        if (pendingBuffer.Length > 0)
        {
            var finalText = pendingBuffer.ToString();
            if (!ContainsStopSequence(finalText) && !IsUnwantedContinuation(finalText))
            {
                accumulatedResponse.Append(finalText);
                yield return finalText;
            }
        }

        LogAgent($"✅ Streaming completed ({tokenCount} tokens)");

        // AFTER streaming ends - auto-save
        // ⚠️ IMPORTANT: invoke from the UI thread via Dispatcher
        if (shouldAutoSave && _saveToFileTool != null && accumulatedResponse.Length > 0)
        {
            var finalResponse = accumulatedResponse.ToString().Trim();
            var promptCopy = userMessage; // Copy for use in async context
            
            LogAgent("💾 Scheduling auto-save...");
            
            // Invoke from the UI thread via Dispatcher
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await AutoInvokeSaveToolAsync(finalResponse, promptCopy);
            });
        }
        else
        {
            // ⚡ NEW APPROACH: if the trigger didn't fire, analyze the content itself
            if (!shouldAutoSave && _saveToFileTool != null && accumulatedResponse.Length > 0)
            {
                var finalResponse = accumulatedResponse.ToString().Trim();
                
                // Use IsPoem() from SaveToFileTool to analyze content
                if (IsLikelyCreativeContent(finalResponse))
                {
                    LogAgent("💾 Creative content detected (poem/text) by response analysis!");
                    
                    var promptCopy = userMessage;
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        await AutoInvokeSaveToolAsync(finalResponse, promptCopy);
                    });
                }
                else
                {
                    LogAgent($"ℹ️ Auto-save not enabled (shouldAutoSave={shouldAutoSave}, tool={_saveToFileTool != null}, length={accumulatedResponse.Length})");
                }
            }
        }
    }

    /// <summary>
    /// Reasoning-step generation with full filtering (for ReasoningService).
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

        LogAgent($"🧠 [Reasoning Step] Start (max {maxTokens} tokens)");

        await foreach (var token in _adapter.SendMessageStreamAsync(stepPrompt, new List<ChatMessage>(), cancellationToken))
        {
            // Token limit check
            if (tokenCount >= maxTokens)
            {
                LogAgent($"⛔ Reasoning step: max tokens reached: {maxTokens}");
                if (pendingBuffer.Length > 0)
                {
                    var bufferContent = pendingBuffer.ToString();
                    yield return bufferContent;
                }
                break;
            }

            // Technical token filter
            if (IsTechnicalToken(token))
            {
                LogAgent($"🚫 Reasoning step: technical token, STOP: {token}");
                break;
            }

            // Count consecutive whitespace tokens
            if (string.IsNullOrWhiteSpace(token))
            {
                consecutiveWhitespace++;
                if (consecutiveWhitespace > 5)
                {
                    LogAgent($"🛑 Reasoning step: too many empty tokens, STOP");
                    break;
                }
                pendingBuffer.Append(token);
                continue;
            }
            else
            {
                consecutiveWhitespace = 0;
            }

            // Add token to buffer
            pendingBuffer.Append(token);
            accumulatedText.Append(token);
            var fullText = accumulatedText.ToString();

            // Stop sequence check
            if (ContainsStopSequence(fullText))
            {
                LogAgent($"🛑 Reasoning step: stop sequence detected");
                var cleanedText = RemoveStopSequences(fullText);
                var alreadyYielded = fullText.Length - pendingBuffer.Length;
                var toYield = cleanedText.Substring(Math.Min(alreadyYielded, cleanedText.Length));
                
                if (!string.IsNullOrWhiteSpace(toYield))
                {
                    yield return toYield.Trim();
                }
                break;
            }

            // Check for self-iteration or gibberish
            if (fullText.Length > 50)
            {
                if (IsUnwantedContinuation(fullText))
                {
                    LogAgent($"🚫 Reasoning step: gibberish/self-iteration detected, STOP");
                    break;
                }
            }

            // Partial stop sequence check
            if (pendingBuffer.Length > 0 && !MightBePartialStopSequence(pendingBuffer.ToString()))
            {
                var bufferContent = pendingBuffer.ToString();
                yield return bufferContent;
                pendingBuffer.Clear();
            }

            tokenCount++;
        }

        // Return remaining buffer
        if (pendingBuffer.Length > 0)
        {
            var finalText = pendingBuffer.ToString();
            if (!ContainsStopSequence(finalText) && !IsUnwantedContinuation(finalText))
            {
                yield return finalText;
            }
        }

        LogAgent($"✅ Reasoning step completed ({tokenCount} tokens)");
    }

    /// <summary>
    /// Determine whether the response should be auto-saved.
    /// </summary>
    private bool ShouldAutoSaveResponse(string userMessage)
    {
        if (!_config.AutoSaveCreativeContent)
            return false;

        var messageLower = userMessage.ToLower();
        return _config.AutoSaveTriggers.Any(trigger => messageLower.Contains(trigger));
    }

    /// <summary>
    /// Check whether the response looks like creative content (poem, story).
    /// </summary>
    private bool IsLikelyCreativeContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
        
        // A poem usually has 4+ lines
        if (lines.Length < 4) 
            return false;
        
        // Average line length (poems have shorter lines)
        var avgLength = lines.Average(l => l.Length);
        
        // Poems: 15-80 chars per line
        // Stories: can be longer
        if (avgLength < 15)
            return false;
        
        // Check punctuation at line endings (common for poems)
        var linesWithPunctuation = lines.Count(l => 
            l.EndsWith(',') || l.EndsWith('.') || l.EndsWith('!') || l.EndsWith('?') || 
            l.EndsWith(':') || l.EndsWith(';') || l.EndsWith("...") || l.EndsWith('—') || l.EndsWith('-'));
        
        // If >25% of lines end with punctuation, it's likely a poem or a story
        var punctuationRatio = (double)linesWithPunctuation / lines.Length;
        if (punctuationRatio > 0.25)
            return true;
        
        // Long multiline text (story)
        if (lines.Length >= 8 && content.Length > 200)
            return true;
        
        return false;
    }

    /// <summary>
    /// Automatically invoke the save tool.
    /// </summary>
    private async Task AutoInvokeSaveToolAsync(string content, string userPrompt)
    {
        if (_saveToFileTool == null)
        {
            LogAgent("⚠️ SaveToFileTool is not set");
            return;
        }

        try
        {
            LogAgent("💾 Auto-invoking SaveToFileTool...");
            
            // Generate file name from prompt
            var fileName = GenerateFileNameFromPrompt(userPrompt);
            
            var result = await _saveToFileTool.AutoSaveAsync(
                content, 
                fileName,
                openInEditor: true
            );
            
            if (result.Success)
            {
                LogAgent($"✅ File saved: {result.FilePath}");
            }
            else
            {
                LogAgent($"❌ Save error: {result.Message}");
            }
        }
        catch (Exception ex)
        {
            LogAgent($"❌ Auto-save error: {ex.Message}");
        }
    }

    /// <summary>
    /// Generate a file name from the user's prompt.
    /// </summary>
    private string GenerateFileNameFromPrompt(string prompt)
    {
        // "Write a poem about autumn" → "Poem_about_autumn.txt"
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
        
        // Remove invalid file-system characters
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
            cleaned = cleaned.Replace(c.ToString(), "");
        }
        
        // Trim to 30 characters
        if (cleaned.Length > 30)
            cleaned = cleaned.Substring(0, 30);
        
        // If empty, use a timestamp
        if (string.IsNullOrWhiteSpace(cleaned))
            cleaned = "Response";
        
        return $"{cleaned}_{DateTime.Now:HHmmss}";
    }

    /// <summary>
    /// Process a message, accepting history as Microsoft.Agents.Core objects.
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
    /// Stream processing with history as Microsoft.Agents.Core objects.
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

    // Helper methods
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
        // Check whether this could be the beginning of a stop sequence
        var stopSequences = new[] 
        { 
            "<end_of_turn>", "<eos>", "</s>", "<|im_end|>", "<|endoftext|>",
            "<start_of_turn>"
        };
        
        foreach (var stopSeq in stopSequences)
        {
            // If the text is a prefix of a stop sequence
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
        // ✅ Do NOT modify the original user message
        // Tool instructions are added only internally for the AI model,
        // and are NOT saved into the chat history
        return userMessage;
    }

    private ToolCall? ExtractToolCall(string response)
    {
        // Improved regex that correctly handles JSON with braces
        var match = Regex.Match(response, @"\[TOOL:(\w+)\|(.*?)\](?=\s*$|\s*\n|$)", RegexOptions.Singleline);
        if (!match.Success)
        {
            // Try an alternative pattern for complex cases
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
        // Remove all TOOL markers, including possible JSON parameters
        var cleaned = Regex.Replace(response, @"\[TOOL:\w+\|.*?\]", string.Empty, RegexOptions.Singleline);
        
        // Extra pass for complex cases
        cleaned = Regex.Replace(cleaned, @"\[TOOL:\w+\|\{.*?\}\]", string.Empty, RegexOptions.Singleline);
        
        // Remove extra blank lines
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
