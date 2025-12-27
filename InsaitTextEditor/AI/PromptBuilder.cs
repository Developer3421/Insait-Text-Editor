using System.Collections.Generic;
using System.Text;
using InsaitTextEditor.Models;
using InsaitTextEditor.Services;

namespace InsaitTextEditor.AI;

public class PromptBuilder
{
    private readonly GemmaConfig _config;
    private readonly UserInstructionService _instructionService;
    
    public PromptBuilder(GemmaConfig config, UserInstructionService instructionService)
    {
        _config = config;
        _instructionService = instructionService;
    }
    
    public string BuildSystemPrompt()
    {
        var userInstruction = _instructionService.GetUserInstruction();
        var aiLang = userInstruction?.AiLanguage;
        
        // Keep the system prompt short and unambiguous. Avoid mentioning translation.
        // Language strategy:
        // - If AiLanguage is null/empty => auto: reply in the same language as the user's last message.
        // - If AiLanguage is set => force that language.
        
        string languageRule = string.IsNullOrWhiteSpace(aiLang)
            ? "Language: Reply in the SAME LANGUAGE as the user's last message. If the user mixes languages, reply in the language they used most recently. Never say that you are translating."
            : aiLang switch
            {
                "uk" => "Language: Reply ONLY in Ukrainian. Never say that you are translating.",
                "ru" => "Language: Reply ONLY in Russian. Never say that you are translating.",
                "en" => "Language: Reply ONLY in English. Never say that you are translating.",
                "de" => "Language: Reply ONLY in German. Never say that you are translating.",
                "tr" => "Language: Reply ONLY in Turkish. Never say that you are translating.",
                _ => "Language: Reply in the SAME LANGUAGE as the user's last message. Never say that you are translating."
            };
        
        var basePrompt =
            "You are Insait Assistant integrated into Insait Text Editor.\n" +
            languageRule + "\n\n" +
            "Rules:\n" +
            "- Give ONE clear, direct answer.\n" +
            "- Do not echo the question.\n" +
            "- Do not include role labels/prefixes (Assistant:, User:, etc.).\n" +
            "- No greetings unless explicitly asked.\n" +
            "- Do not ask follow-up questions unless requested.";
        
        // Add the user's custom instruction (if present)
        if (!string.IsNullOrWhiteSpace(userInstruction?.Content))
        {
            return $"{basePrompt}\n\nUser instruction:\n{userInstruction.Content}";
        }
        
        return basePrompt;
    }
    
    public string FormatMessage(string role, string content)
    {
        // Gemma-3 формат: <start_of_turn>user\n...<end_of_turn>\n
        if (role == "user")
            return $"{_config.UserTurnStart}{content}{_config.UserTurnEnd}";
        else
            return $"{_config.ModelTurnStart}{content}{_config.ModelTurnEnd}";
    }
    
    /// <summary>
    /// Побудова повного промпту з історією повідомлень для Gemma-3
    /// </summary>
    public string BuildPromptWithHistory(string userMessage, List<ChatMessage>? history = null)
    {
        var sb = new StringBuilder();
        
        // Системний промпт як перше повідомлення від user
        var systemPrompt = BuildSystemPrompt();
        
        // ✅ НОВИЙ ПІДХІД: Додаємо інструкції про інструменти ТУТ, в системному промпті
        // Це означає що вони НЕ будуть в історії чату, але AI їх побачить
        var enhancedSystemPrompt = AppendToolInstructionsIfAvailable(systemPrompt);
        
        sb.Append(_config.UserTurnStart);
        sb.Append(enhancedSystemPrompt);
        sb.Append(_config.UserTurnEnd);
        
        // Додаємо історію (якщо є)
        if (history != null && history.Count > 0)
        {
            foreach (var msg in history)
            {
                var role = msg.Sender == "User" ? "user" : "model";
                sb.Append(FormatMessage(role, msg.Content));
            }
        }
        
        // Додаємо поточне повідомлення користувача (БЕЗ інструкцій про інструменти)
        sb.Append(_config.UserTurnStart);
        sb.Append(userMessage);
        sb.Append(_config.UserTurnEnd);
        
        // Початок відповіді моделі
        sb.Append(_config.ModelTurnStart);
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Побудова простого промпту без історії
    /// </summary>
    public string BuildSimplePrompt(string userMessage)
    {
        return BuildPromptWithHistory(userMessage, null);
    }
    
    /// <summary>
    /// Додати інструкції про інструменти до системного промпту (НЕ до повідомлення користувача)
    /// </summary>
    private string AppendToolInstructionsIfAvailable(string systemPrompt)
    {
        // Перевіряємо чи є SaveToFileTool через App (це не ідеально, але працює)
        var saveToFileTool = App.MicrosoftInsaitAgent?.GetType()
            .GetField("_saveToFileTool", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(App.MicrosoftInsaitAgent);
        
        if (saveToFileTool == null)
        {
            return systemPrompt;
        }
        
        // Отримуємо опис інструменту
        var description = saveToFileTool.GetType()
            .GetProperty("Description")
            ?.GetValue(saveToFileTool) as string ?? "Saves content to a file";
        
        var sb = new StringBuilder(systemPrompt);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("=== AVAILABLE TOOLS (INTERNAL USE ONLY) ===");
        sb.AppendLine($"- save_to_file: {description}");
        sb.AppendLine("To use a tool, respond with: [TOOL:tool_name|parameters]");
        sb.AppendLine("Note: These instructions are ONLY for the AI model and will NOT be shown to the user.");
        sb.AppendLine("============================================");
        
        return sb.ToString();
    }
}