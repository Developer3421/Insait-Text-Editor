using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InsaitTextEditor.Models;

namespace InsaitTextEditor.AI;

/// <summary>
/// Adapter between LlamaSharp and Microsoft Agent Framework (via local inference)
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
    /// Send message to agent and get response
    /// </summary>
    public async Task<AgentResponse> SendMessageAsync(
        string message,
        List<ChatMessage> history,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Build prompt with history
            var prompt = _promptBuilder.BuildPromptWithHistory(message, history);
            
            // Get response from LlamaSharp
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
    /// Streaming response from agent
    /// </summary>
    public async IAsyncEnumerable<string> SendMessageStreamAsync(
        string message,
        List<ChatMessage> history,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Build prompt with history
        var prompt = _promptBuilder.BuildPromptWithHistory(message, history);
        
        // Stream response from LlamaSharp
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
