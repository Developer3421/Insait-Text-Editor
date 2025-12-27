using System;
using System.Threading;
using System.Threading.Tasks;

namespace InsaitTextEditor.AI;

/// <summary>
/// Lifecycle manager for Gemma-3-1B model
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
    /// Get or create inference engine
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
    /// Initialize model (can be called ahead of time for warm-up)
    /// </summary>
    public async Task InitializeAsync()
    {
        var engine = GetOrCreateEngine();
        await engine.InitializeAsync();
    }

    /// <summary>
    /// Generate response with formatted prompt
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
    /// Streaming response generation
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
    /// Get model information
    /// </summary>
    public async Task<ModelInfo> GetModelInfoAsync()
    {
        var engine = GetOrCreateEngine();
        return await engine.GetModelInfoAsync();
    }

    /// <summary>
    /// Reload model (if settings have changed)
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