using System;
using System.Collections.Generic;
using System.IO;
using InsaitTextEditor.Services;

namespace InsaitTextEditor.AI;

public class GemmaConfig
{
    private readonly UserInstructionService _instructionService;

    public GemmaConfig(UserInstructionService instructionService)
    {
        _instructionService = instructionService;
    }

    // Default model location in the repo/project folder (next to InsaitTextEditor.csproj).
    // This matches: InsaitTextEditor\AiModel\gemma-3-1b-it-UD-Q2_K_XL.gguf
    private static string DefaultRepoModelPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "AiModel", "gemma-3-1b-it-UD-Q2_K_XL.gguf"));

    // Keep packaged model location (when published) as a fallback.
    private static string DefaultPackagedModelPath =>
        Path.Combine(AppContext.BaseDirectory, "AiModel", "gemma-3-1b-it-UD-Q2_K_XL.gguf");

    /// <summary>
    /// Resolved model path.
    /// Priority:
    /// 1) INSAIT_MODEL_PATH env var
    /// 2) AiModel/model-path.txt (absolute or relative)
    /// 3) repo/project default (useful for Debug/running from IDE)
    /// 4) packaged default under AppContext.BaseDirectory (useful for published app)
    /// </summary>
    public string ModelPath
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("INSAIT_MODEL_PATH");
            if (!string.IsNullOrWhiteSpace(env))
                return env.Trim();

            try
            {
                var overrideFile = Path.Combine(AppContext.BaseDirectory, "AiModel", "model-path.txt");
                if (File.Exists(overrideFile))
                {
                    var raw = File.ReadAllText(overrideFile).Trim();
                    if (!string.IsNullOrWhiteSpace(raw))
                    {
                        return Path.IsPathRooted(raw)
                            ? raw
                            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, raw));
                    }
                }
            }
            catch
            {
                // Ignore override read errors and fall back.
            }

            // Prefer project/repo path first (dev), then packaged path (publish)
            return File.Exists(DefaultRepoModelPath) ? DefaultRepoModelPath : DefaultPackagedModelPath;
        }
    }
    
    // ========== GENERATION LIMITS ==========
    public int ContextSize 
    {
        get
        {
            var instruction = _instructionService.GetUserInstruction();
            return instruction?.ContextSize ?? 4096;  // Default: 4K tokens
        }
    }
    
    public int MaxTokens
    {
        get
        {
            var instruction = _instructionService.GetUserInstruction();
            return instruction?.MaxTokens ?? 1024;  // Default: 1K tokens
        }
    }
    
    // ========== STOP TOKENS FOR GEMMA-3 ==========
    public List<string> StopSequences => new()
    {
        "<end_of_turn>",
        "<eos>",
        "\n<end_of_turn>",
        "<start_of_turn>user",
        "<start_of_turn>model",
        "<|endoftext|>",
        "<|im_end|>"
    };
    
    // ========== HARDWARE PARAMETERS ==========
    public int GpuLayerCount => 0;  // 0 = CPU only, 33 = full GPU
    public int BatchSize => 512;
    public int Threads => Environment.ProcessorCount / 2;
    
    // ========== ANTI-REPETITION PARAMETERS ==========
    public float RepeatPenalty => 1.2f;     // Increase to prevent repetitions (was 1.15)
    public int RepeatLastN => 256;          // Increase number of tokens to check (was 128)
    
    // ========== TEMPERATURE AND SAMPLING ==========
    public float Temperature => 0.6f;       // Lower for more accurate responses (was 0.7)
    public float TopP => 0.85f;             // Lower for less creative responses (was 0.9)
    public int TopK => 30;                  // Lower for more focused responses (was 40)
    
    // ========== TIMEOUT ==========
    public TimeSpan MaxResponseTime => TimeSpan.FromSeconds(60);  // Hard timeout
    
    // ========== GEMMA-3 FORMAT TOKENS ==========
    public string UserTurnStart => "<start_of_turn>user\n";
    public string UserTurnEnd => "<end_of_turn>\n";
    public string ModelTurnStart => "<start_of_turn>model\n";
    public string ModelTurnEnd => "<end_of_turn>\n";
    public string EndOfText => "<end_of_turn>";
}