using System;

namespace InsaitTextEditor.Models;

public class UserInstruction
{
    // Instruction (preferably English) for better model understanding
    public string Content { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    
    // Performance settings
    public int ContextSize { get; set; } = 4096;  // Input tokens (2048-8192)
    public int MaxTokens { get; set; } = 1024;    // Output tokens (256-2048)
    
    // AI model language (null = Auto, determined from UI)
    public string? AiLanguage { get; set; } = null;
    
    // AI operation modes
    public bool ReasoningEnabled { get; set; } = false;      // Chain-of-Thought reasoning mode
    public bool GlobalMemoryEnabled { get; set; } = false;   // Global memory across sessions
}
