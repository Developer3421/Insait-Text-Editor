using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LLama;
using LLama.Common;
using LLama.Sampling;

namespace InsaitTextEditor.AI;

/// <summary>
/// Адаптер між LlamaSharp та Agent Framework для моделі Gemma-3-1B
/// </summary>
public class LlamaSharpInferenceEngine : IDisposable
{
    private readonly GemmaConfig _config;
    private LLamaWeights? _weights;
    private ModelParams? _modelParams;
    private readonly object _initLock = new();
    private Task? _initTask;
    private Exception? _initError;
    private bool _disposed;

    // Stop sequences для Gemma-3
    private readonly HashSet<string> _stopSequences = new()
    {
        "<end_of_turn>",
        "<eos>",
        "<start_of_turn>",
        "\n<end_of_turn>",
        "<|endoftext|>",
        "<|im_end|>",
        "<bos>",
        "<pad>"
    };

    public LlamaSharpInferenceEngine(GemmaConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        
        // Suppress llama.cpp warnings
        try
        {
            Environment.SetEnvironmentVariable("LLAMA_LOG_LEVEL", "3", EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("GGML_LOG_LEVEL", "3", EnvironmentVariableTarget.Process);
        }
        catch
        {
            // Ignore if setting environment variables fails
        }
    }

    /// <summary>
    /// Асинхронна ініціалізація моделі (викликається автоматично перед інференсом)
    /// </summary>
    public async Task InitializeAsync()
    {
        lock (_initLock)
        {
            if (_weights != null && _modelParams != null)
                return; // Вже ініціалізовано

            if (_initTask != null)
                return; // Вже в процесі ініціалізації

            _initTask = Task.Run(() =>
            {
                try
                {
                    // Перевірка наявності нативних бібліотек
                    try
                    {
                        var _ = LLama.Native.NativeApi.llama_max_devices();
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            "Не вдалося завантажити нативні бібліотеки llama.cpp. " +
                            "Переконайтеся, що встановлено пакет LLamaSharp.Backend.Cpu. " +
                            "Можливо потрібно виконати: dotnet restore", ex);
                    }

                    if (!File.Exists(_config.ModelPath))
                        throw new FileNotFoundException($"Модель Gemma-3-1B не знайдено: {_config.ModelPath}");

                    _modelParams = new ModelParams(_config.ModelPath)
                    {
                        ContextSize = (uint)_config.ContextSize,
                        GpuLayerCount = _config.GpuLayerCount,
                        BatchSize = (uint)_config.BatchSize,
                        Threads = _config.Threads
                    };

                    _weights = LLamaWeights.LoadFromFile(_modelParams);
                    _initError = null;
                }
                catch (Exception ex)
                {
                    _initError = ex;
                    throw;
                }
            });
        }

        await _initTask;
    }

    /// <summary>
    /// Синхронна генерація відповіді (для Agent Framework)
    /// </summary>
    public async Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
    {
        await InitializeAsync();

        if (_weights == null || _modelParams == null)
        {
            var errorMsg = _initError != null
                ? $"Не вдалося ініціалізувати модель: {_initError.Message}"
                : "Модель не ініціалізована.";
            throw new InvalidOperationException(errorMsg);
        }

        // Створюємо новий контекст для кожного запиту (stateless)
        using var context = _weights.CreateContext(_modelParams);
        var executor = new InteractiveExecutor(context);

        var inferenceParams = new InferenceParams
        {
            MaxTokens = _config.MaxTokens,
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = _config.Temperature,
                TopP = _config.TopP,
                TopK = _config.TopK,
                RepeatPenalty = _config.RepeatPenalty
            }
        };

        var sb = new StringBuilder();
        var tokenCount = 0;

        LogGeneration($"🚀 Початок генерації (max {_config.MaxTokens} токенів)");

        await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken))
        {
            if (tokenCount >= _config.MaxTokens)
            {
                LogGeneration($"⛔ Досягнуто максимум токенів: {_config.MaxTokens}");
                break;
            }

            sb.Append(token);
            var currentText = sb.ToString();

            // Перевірити на stop sequences
            foreach (var stopSeq in _stopSequences)
            {
                if (currentText.Contains(stopSeq))
                {
                    LogGeneration($"🛑 Stop sequence виявлено: {stopSeq}");
                    
                    // Повернути тільки текст до stop sequence
                    var stopIndex = currentText.IndexOf(stopSeq);
                    if (stopIndex > 0)
                    {
                        var cleanText = currentText.Substring(0, stopIndex).Trim();
                        LogGeneration($"✅ Згенеровано {tokenCount} токенів");
                        return cleanText;
                    }
                    break;
                }
            }

            tokenCount++;
        }

        LogGeneration($"✅ Згенеровано {tokenCount} токенів");
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Стрімінгова генерація відповіді (для UI)
    /// </summary>
    public async IAsyncEnumerable<string> GenerateResponseStreamAsync(
        string prompt, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await InitializeAsync();

        if (_weights == null || _modelParams == null)
        {
            var errorMsg = _initError != null
                ? $"Не вдалося ініціалізувати модель: {_initError.Message}"
                : "Модель не ініціалізована.";
            throw new InvalidOperationException(errorMsg);
        }

        // Створюємо новий контекст для кожного запиту (stateless)
        using var context = _weights.CreateContext(_modelParams);
        var executor = new InteractiveExecutor(context);

        var inferenceParams = new InferenceParams
        {
            MaxTokens = _config.MaxTokens,
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = _config.Temperature,
                TopP = _config.TopP,
                TopK = _config.TopK,
                RepeatPenalty = _config.RepeatPenalty
            }
        };

        var buffer = new StringBuilder();
        var generatedTokens = 0;

        LogGeneration($"🚀 Початок streaming генерації (max {_config.MaxTokens} токенів)");

        await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken))
        {
            // Перевірка ліміту токенів
            if (generatedTokens >= _config.MaxTokens)
            {
                LogGeneration($"⛔ Досягнуто максимум токенів: {_config.MaxTokens}");
                break;
            }

            buffer.Append(token);
            var currentText = buffer.ToString();

            // Перевірити на stop sequences
            bool shouldStop = false;
            foreach (var stopSeq in _stopSequences)
            {
                if (currentText.Contains(stopSeq))
                {
                    LogGeneration($"🛑 Stop sequence виявлено: {stopSeq}");
                    
                    // Повернути тільки текст до stop sequence
                    var stopIndex = currentText.IndexOf(stopSeq);
                    if (stopIndex > 0)
                    {
                        var finalText = currentText.Substring(0, stopIndex);
                        var lastPart = finalText.Substring(Math.Max(0, finalText.Length - token.Length));
                        if (!string.IsNullOrEmpty(lastPart))
                        {
                            yield return lastPart;
                        }
                    }
                    shouldStop = true;
                    break;
                }
            }

            if (shouldStop)
            {
                LogGeneration($"✅ Згенеровано {generatedTokens} токенів, зупинка");
                yield break;
            }

            yield return token;
            generatedTokens++;
        }

        LogGeneration($"✅ Згенеровано {generatedTokens} токенів");
    }

    private void LogGeneration(string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [LlamaSharp] {message}");
    }

    /// <summary>
    /// Отримати інформацію про модель
    /// </summary>
    public async Task<ModelInfo> GetModelInfoAsync()
    {
        await InitializeAsync();

        return new ModelInfo
        {
            Name = "Gemma-3-1B",
            Path = _config.ModelPath,
            ContextSize = _config.ContextSize,
            MaxTokens = _config.MaxTokens,
            IsInitialized = _weights != null
        };
    }

    public void Dispose()
    {
        if (_disposed) return;

        _weights?.Dispose();
        _weights = null;
        _modelParams = null;
        _disposed = true;
    }
}

/// <summary>
/// Інформація про завантажену модель
/// </summary>
public class ModelInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int ContextSize { get; set; }
    public int MaxTokens { get; set; }
    public bool IsInitialized { get; set; }
}