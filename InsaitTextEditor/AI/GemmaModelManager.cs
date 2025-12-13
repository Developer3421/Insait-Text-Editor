using System;
using System.Threading;
using System.Threading.Tasks;

namespace InsaitTextEditor.AI;

/// <summary>
/// Менеджер життєвого циклу моделі Gemma-3-1B
/// </summary>
public class GemmaModelManager : IDisposable
{
    private readonly GemmaConfig _config;
    private readonly PromptBuilder _promptBuilder;
    private LlamaSharpInferenceEngine? _engine;
    private readonly object _lock = new();
    private bool _disposed;

    public GemmaModelManager(GemmaConfig config, PromptBuilder promptBuilder)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _promptBuilder = promptBuilder ?? throw new ArgumentNullException(nameof(promptBuilder));
    }

    /// <summary>
    /// Отримати або створити inference engine
    /// </summary>
    private LlamaSharpInferenceEngine GetOrCreateEngine()
    {
        lock (_lock)
        {
            _engine ??= new LlamaSharpInferenceEngine(_config);
            return _engine;
        }
    }

    /// <summary>
    /// Ініціалізація моделі (можна викликати заздалегідь для warm-up)
    /// </summary>
    public async Task InitializeAsync()
    {
        var engine = GetOrCreateEngine();
        await engine.InitializeAsync();
    }

    /// <summary>
    /// Генерація відповіді з форматованим промптом
    /// </summary>
    public async Task<string> GenerateResponseAsync(
        string userMessage, 
        System.Collections.Generic.List<Models.ChatMessage>? history = null,
        CancellationToken cancellationToken = default)
    {
        var engine = GetOrCreateEngine();
        var prompt = _promptBuilder.BuildPromptWithHistory(userMessage, history);
        return await engine.GenerateResponseAsync(prompt, cancellationToken);
    }

    /// <summary>
    /// Стрімінгова генерація відповіді
    /// </summary>
    public async System.Collections.Generic.IAsyncEnumerable<string> GenerateResponseStreamAsync(
        string userMessage,
        System.Collections.Generic.List<Models.ChatMessage>? history = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var engine = GetOrCreateEngine();
        var prompt = _promptBuilder.BuildPromptWithHistory(userMessage, history);
        
        await foreach (var token in engine.GenerateResponseStreamAsync(prompt, cancellationToken))
        {
            yield return token;
        }
    }

    /// <summary>
    /// Отримати інформацію про модель
    /// </summary>
    public async Task<ModelInfo> GetModelInfoAsync()
    {
        var engine = GetOrCreateEngine();
        return await engine.GetModelInfoAsync();
    }

    /// <summary>
    /// Перезавантажити модель (якщо змінились налаштування)
    /// </summary>
    public void ReloadModel()
    {
        lock (_lock)
        {
            _engine?.Dispose();
            _engine = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            _engine?.Dispose();
            _engine = null;
        }

        _disposed = true;
    }
}