using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InsaitTextEditor.AI;
using InsaitTextEditor.Agents;
using InsaitTextEditor.Models;

namespace InsaitTextEditor.Services;

/// <summary>
/// Сервіс для роботи з AI агентом - ТІЛЬКИ Microsoft Agent Framework
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
        
        Console.WriteLine("[AgentService] ✅ Ініціалізовано з Microsoft Agent Framework (тільки новий агент)");
    }

    /// <summary>
    /// Ініціалізація моделі
    /// </summary>
    public async Task InitializeAsync()
    {
        Console.WriteLine("[AgentService] 🔄 Ініціалізація моделі через Microsoft.Agents...");
        await _modelManager.InitializeAsync();
        
        // Отримати інформацію про модель з менеджера
        var info = await _modelManager.GetModelInfoAsync();
        Console.WriteLine($"[AgentService] ✅ Модель: {info.Name}");
        Console.WriteLine($"[AgentService] ✅ Max tokens: {info.MaxTokens}");
        Console.WriteLine($"[AgentService] ✅ Context size: {info.ContextSize}");
    }

    /// <summary>
    /// Отримати відповідь від агента через Microsoft Agent Framework
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
            throw new InvalidOperationException($"Помилка генерації відповіді: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Стрімінгова відповідь через Microsoft Agent Framework
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
    /// Додати повідомлення користувача до історії
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
    /// Додати відповідь асистента до історії (з підтримкою AgentMessage)
    /// </summary>
    public void AddAssistantMessage(string content, List<ToolInvocation>? toolsUsed = null)
    {
        if (toolsUsed is { Count: > 0 })
        {
            // Використати AgentMessage для збереження інформації про інструменти
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
    /// Очистити історію розмови
    /// </summary>
    public void ClearHistory()
    {
        _conversationState.ClearHistory();
        _historyService.Clear();
    }

    /// <summary>
    /// Отримати інформацію про модель
    /// </summary>
    public async Task<ModelInfo> GetModelInfoAsync()
    {
        return await _modelManager.GetModelInfoAsync();
    }

    /// <summary>
    /// Перезавантажити модель (якщо змінились налаштування)
    /// </summary>
    public void ReloadModel()
    {
        _modelManager.ReloadModel();
        _conversationState.ClearHistory(); // Очистити контекст при перезавантаженні
    }


    /// <summary>
    /// Приблизна оцінка кількості токенів (1 токен ≈ 4 символи)
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