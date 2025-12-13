using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InsaitTextEditor.Models;

namespace InsaitTextEditor.AI;

/// <summary>
/// Адаптер між LlamaSharp та Microsoft Agent Framework (через локальний інференс)
/// </summary>
public class MicrosoftAgentsAdapter
{
    private readonly LlamaSharpInferenceEngine _inferenceEngine;
    private readonly PromptBuilder _promptBuilder;

    public MicrosoftAgentsAdapter(
        LlamaSharpInferenceEngine inferenceEngine,
        PromptBuilder promptBuilder)
    {
        _inferenceEngine = inferenceEngine ?? throw new ArgumentNullException(nameof(inferenceEngine));
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
    }

    /// <summary>
    /// Відправити повідомлення агенту та отримати відповідь
    /// </summary>
    public async Task<AgentResponse> SendMessageAsync(
        string message,
        List<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Побудувати промпт з історією
            var prompt = _promptBuilder.BuildPromptWithHistory(message, history);
            
            // Отримати відповідь від LlamaSharp
            var response = await _inferenceEngine.GenerateResponseAsync(prompt, cancellationToken);
            
            return new AgentResponse
            {
                Content = response,
                ToolsUsed = new List<ToolInvocation>(),
                TokensGenerated = EstimateTokens(response),
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            return new AgentResponse
            {
                Content = $"Error: {ex.Message}",
                ToolsUsed = new List<ToolInvocation>(),
                TokensGenerated = 0,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Стрімінгова відповідь від агента
    /// </summary>
    public async IAsyncEnumerable<string> SendMessageStreamAsync(
        string message,
        List<ChatMessage> history,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Побудувати промпт з історією
        var prompt = _promptBuilder.BuildPromptWithHistory(message, history);
        
        // Стримити відповідь від LlamaSharp
        await foreach (var token in _inferenceEngine.GenerateResponseStreamAsync(prompt, cancellationToken))
        {
            yield return token;
        }
    }

    private int EstimateTokens(string text)
    {
        return text.Length / 4;
    }
}
