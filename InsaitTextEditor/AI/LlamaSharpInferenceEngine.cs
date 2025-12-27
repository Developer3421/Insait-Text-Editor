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
/// Adapter between LlamaSharp and Agent Framework for Gemma-3-1B model
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

    // Stop sequences for Gemma-3
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
    /// Async model initialization (automatically called before inference).
    /// </summary>
    public async Task InitializeAsync()
    {
        lock (_initLock)
        {
            if (_weights != null && _modelParams != null)
                return; // Already initialized

            if (_initTask != null)
                return; // Already initializing

            _initTask = Task.Run(() =>
            {
                try
                {
                    // Check availability of native libraries
                    try
                    {
                        var _ = LLama.Native.NativeApi.llama_max_devices();
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            "Failed to load llama.cpp native libraries. " +
                            "Make sure the LLamaSharp.Backend.Cpu package is installed. " +
                            "You may need to run: dotnet restore", ex);
                    }

                    var modelPath = _config.ModelPath;
                    if (!File.Exists(modelPath))
                        throw new FileNotFoundException($"Gemma model not found: {modelPath}");

                    var fi = new FileInfo(modelPath);
                    if (fi.Length == 0)
                        throw new InvalidOperationException(
                            $"Model file is 0 bytes: {modelPath}. " +
                            "This usually means the model was not copied/downloaded (e.g. an LFS placeholder) or build/publish produced an empty file. " +
                            "Specify a real path via INSAIT_MODEL_PATH or AiModel\\model-path.txt.");

                    _modelParams = new ModelParams(modelPath)
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
    /// Non-streaming response generation.
    /// </summary>
    public async Task<string> GenerateResponseAsync(string prompt, CancellationToken cancellationToken = default)
    {
        await InitializeAsync();

        if (_weights == null || _modelParams == null)
        {
            var errorMsg = _initError != null
                ? $"Failed to initialize model: {_initError.Message}"
                : "Model is not initialized.";
            throw new InvalidOperationException(errorMsg);
        }

        // Create a new context for each request (stateless)
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

        LogGeneration($"🚀 Generation started (max {_config.MaxTokens} tokens)");

        await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken))
        {
            if (tokenCount >= _config.MaxTokens)
            {
                LogGeneration($"⛔ Max tokens reached: {_config.MaxTokens}");
                break;
            }

            sb.Append(token);
            var currentText = sb.ToString();

            // Check stop sequences
            foreach (var stopSeq in _stopSequences)
            {
                if (currentText.Contains(stopSeq))
                {
                    LogGeneration($"🛑 Stop sequence detected: {stopSeq}");
                    
                    // Return only content before stop sequence
                    var stopIndex = currentText.IndexOf(stopSeq);
                    if (stopIndex > 0)
                    {
                        var cleanText = currentText.Substring(0, stopIndex).Trim();
                        LogGeneration($"✅ Generated {tokenCount} tokens");
                        return cleanText;
                    }
                    break;
                }
            }

            tokenCount++;
        }

        LogGeneration($"✅ Generated {tokenCount} tokens");
        return sb.ToString().Trim();
    }

    /// <summary>
    /// Streaming response generation (for UI).
    /// </summary>
    public async IAsyncEnumerable<string> GenerateResponseStreamAsync(
        string prompt, 
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await InitializeAsync();

        if (_weights == null || _modelParams == null)
        {
            var errorMsg = _initError != null
                ? $"Failed to initialize model: {_initError.Message}"
                : "Model is not initialized.";
            throw new InvalidOperationException(errorMsg);
        }

        // Create a new context for each request (stateless)
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

        LogGeneration($"🚀 Streaming generation started (max {_config.MaxTokens} tokens)");

        await foreach (var token in executor.InferAsync(prompt, inferenceParams, cancellationToken))
        {
            // Token limit check
            if (generatedTokens >= _config.MaxTokens)
            {
                LogGeneration($"⛔ Max tokens reached: {_config.MaxTokens}");
                break;
            }

            buffer.Append(token);
            var currentText = buffer.ToString();

            // Check stop sequences
            bool shouldStop = false;
            foreach (var stopSeq in _stopSequences)
            {
                if (currentText.Contains(stopSeq))
                {
                    LogGeneration($"🛑 Stop sequence detected: {stopSeq}");
                    
                    // Return only content before stop sequence
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
                LogGeneration($"✅ Generated {generatedTokens} tokens, stopping");
                yield break;
            }

            yield return token;
            generatedTokens++;
        }

        LogGeneration($"✅ Generated {generatedTokens} tokens");
    }

    private void LogGeneration(string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [LlamaSharp] {message}");
    }

    /// <summary>
    /// Get model information.
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
/// Information about the loaded model.
/// </summary>
public class ModelInfo
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public int ContextSize { get; set; }
    public int MaxTokens { get; set; }
    public bool IsInitialized { get; set; }
}