using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InsaitTextEditor.AI;
using InsaitTextEditor.Agents;
using InsaitTextEditor.Models;

namespace InsaitTextEditor.Services;

/// <summary>
/// Service for working with AI agent - Microsoft Agent Framework only
/// </summary>
public class AgentService : IDisposable
{
    private readonly MicrosoftInsaitAgent _microsoftAgent;
    private readonly GemmaModelManager _modelManager;
    private readonly ConversationStateService _conversationState;
    private readonly ChatHistoryService _historyService;
    private bool _disposed;

    public AgentService(
        MicrosoftInsaitAgent microsoftAgent,
        GemmaModelManager modelManager,
        ConversationStateService conversationState,
        ChatHistoryService historyService)
    {
        _microsoftAgent = microsoftAgent ?? throw new ArgumentNullException(nameof(microsoftAgent));
        _modelManager = modelManager ?? throw new ArgumentNullException(nameof(modelManager));
        _conversationState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
        
        Console.WriteLine("[AgentService] ✅ Initialized with Microsoft Agent Framework (new agent only)");
    }

    /// <summary>
    /// Initialize the model
    /// </summary>
    public async Task InitializeAsync()
    {
        Console.WriteLine("[AgentService] 🔄 Initializing model via Microsoft.Agents...");
        await _modelManager.InitializeAsync();
        
        // Get model info from manager
        var info = await _modelManager.GetModelInfoAsync();
        Console.WriteLine($"[AgentService] ✅ Model: {info.Name}");
        Console.WriteLine($"[AgentService] ✅ Max tokens: {info.MaxTokens}");
        Console.WriteLine($"[AgentService] ✅ Context size: {info.ContextSize}");
    }

    /// <summary>
    /// Get response from agent via Microsoft Agent Framework
    /// </summary>
    public async Task<AgentResponse> GetResponseAsync(
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var history = _conversationState.GetRecentMessages(10);
        
        try
        {
            return await _microsoftAgent.ProcessAsync(userMessage, history, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Response generation error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Streaming response via Microsoft Agent Framework
    /// </summary>
    public async IAsyncEnumerable<string> GetResponseStreamAsync(
        string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var history = _conversationState.GetRecentMessages(10);
        
        await foreach (var token in _microsoftAgent.ProcessStreamAsync(userMessage, history, cancellationToken))
        {
            yield return token;
        }
    }

    /// <summary>
    /// Add user message to history
    /// </summary>
    public void AddUserMessage(string content)
    {
        var message = new ChatMessage
        {
            Sender = "User",
            Content = content,
            Timestamp = DateTime.UtcNow
        };
        
        _conversationState.AddMessage(message);
        _historyService.AddMessage(message);
    }

    /// <summary>
    /// Add assistant response to history (with AgentMessage support)
    /// </summary>
    public void AddAssistantMessage(string content, List<ToolInvocation>? toolsUsed = null)
    {
        if (toolsUsed is { Count: > 0 })
        {
            // Use AgentMessage to save tool information
            var agentMessage = new AgentMessage
            {
                Sender = AssistantConfig.Name,
                Content = content,
                Timestamp = DateTime.UtcNow,
                ToolCalls = toolsUsed,
                ModelUsed = "Gemma-3-1B (Microsoft.Agents)",
                TokensUsed = EstimateTokens(content)
            };
            
            _conversationState.AddMessage(agentMessage);
            _historyService.AddAgentMessage(agentMessage);
        }
        else
        {
            var message = new ChatMessage
            {
                Sender = AssistantConfig.Name,
                Content = content,
                Timestamp = DateTime.UtcNow
            };
            
            _conversationState.AddMessage(message);
            _historyService.AddMessage(message);
        }
    }

    /// <summary>
    /// Clear conversation history
    /// </summary>
    public void ClearHistory()
    {
        _conversationState.ClearHistory();
        _historyService.Clear();
    }

    /// <summary>
    /// Get model information
    /// </summary>
    public async Task<ModelInfo> GetModelInfoAsync()
    {
        return await _modelManager.GetModelInfoAsync();
    }

    /// <summary>
    /// Reload model (if settings have changed)
    /// </summary>
    public void ReloadModel()
    {
        _modelManager.ReloadModel();
        _conversationState.ClearHistory(); // Clear context on reload
    }


    /// <summary>
    /// Approximate token count estimation (1 token ≈ 4 characters)
    /// </summary>
    private int EstimateTokens(string text)
    {
        return text.Length / 4;
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _modelManager.Dispose();
        _disposed = true;
    }
}