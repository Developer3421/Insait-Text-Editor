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
    
    public string ModelPath => Path.Combine(AppContext.BaseDirectory, "AiModel", "gemma-3-1b-it-UD-Q2_K_XL.gguf");
    
    // ========== ОБМЕЖЕННЯ ГЕНЕРАЦІЇ ==========
    public int ContextSize 
    {
        get
        {
            var instruction = _instructionService.GetUserInstruction();
            return instruction?.ContextSize ?? 4096;  // Дефолт: 4K токенів
        }
    }
    
    public int MaxTokens
    {
        get
        {
            var instruction = _instructionService.GetUserInstruction();
            return instruction?.MaxTokens ?? 1024;  // Дефолт: 1K токенів
        }
    }
    
    // ========== STOP TOKENS ДЛЯ GEMMA-3 ==========
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
    public float RepeatPenalty => 1.2f;     // Збільшити для запобігання повторенням (було 1.15)
    public int RepeatLastN => 256;          // Збільшити кількість токенів для перевірки (було 128)
    
    // ========== ТЕМПЕРАТУРА ТА SAMPLING ==========
    public float Temperature => 0.6f;       // Знизити для більш точних відповідей (було 0.7)
    public float TopP => 0.85f;             // Знизити для менш креативних відповідей (було 0.9)
    public int TopK => 30;                  // Знизити для більш фокусованих відповідей (було 40)
    
    // ========== TIMEOUT ==========
    public TimeSpan MaxResponseTime => TimeSpan.FromSeconds(60);  // Hard timeout
    
    // ========== GEMMA-3 FORMAT TOKENS ==========
    public string UserTurnStart => "<start_of_turn>user\n";
    public string UserTurnEnd => "<end_of_turn>\n";
    public string ModelTurnStart => "<start_of_turn>model\n";
    public string ModelTurnEnd => "<end_of_turn>\n";
    public string EndOfText => "<end_of_turn>";
}