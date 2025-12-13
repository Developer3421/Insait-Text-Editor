using System;

namespace InsaitTextEditor.Models;

public class UserInstruction
{
    // Інструкція АНГЛІЙСЬКОЮ для кращого розуміння моделлю
    public string Content { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    
    // Налаштування продуктивності
    public int ContextSize { get; set; } = 4096;  // Вхідні токени (2048-8192)
    public int MaxTokens { get; set; } = 1024;    // Вихідні токени (256-2048)
    
    // Мова AI моделі (null = Auto, визначається з UI)
    public string? AiLanguage { get; set; } = null;
    
    // Режими роботи AI
    public bool ReasoningEnabled { get; set; } = false;      // Режим міркування (Chain-of-Thought)
    public bool GlobalMemoryEnabled { get; set; } = false;   // Глобальна пам'ять між сесіями
}
