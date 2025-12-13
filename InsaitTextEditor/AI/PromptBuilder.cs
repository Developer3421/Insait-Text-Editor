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
        
        // ✅ Базовий промпт залежно від вибраної мови AI
        string basePrompt;
        
        if (!string.IsNullOrEmpty(aiLang))
        {
            // Мапінг мов до обов'язкових інструкцій
            var languageInstructions = new Dictionary<string, string>
            {
                ["uk"] = "🇺🇦 ОБОВ'ЯЗКОВО: ЗАВЖДИ відповідай ТІЛЬКИ УКРАЇНСЬКОЮ МОВОЮ, незалежно від мови запиту користувача!\n\n" +
                        "You are Insait Assistant, a helpful AI integrated into Insait Text Editor.\n\n" +
                        "CRITICAL RULES:\n" +
                        "1. **ALWAYS respond ONLY in UKRAINIAN language** - this is MANDATORY!\n" +
                        "2. Give ONE clear, direct answer\n" +
                        "3. Stop immediately after answering - do NOT continue with examples or questions\n" +
                        "4. Do NOT add greetings unless asked\n" +
                        "5. Be concise and helpful\n" +
                        "6. Do NOT ask follow-up questions unless requested",
                
                ["en"] = "🇬🇧 MANDATORY: ALWAYS respond ONLY in ENGLISH, regardless of the query language!\n\n" +
                        "You are Insait Assistant, a helpful AI integrated into Insait Text Editor.\n\n" +
                        "CRITICAL RULES:\n" +
                        "1. **ALWAYS respond ONLY in ENGLISH language** - this is MANDATORY!\n" +
                        "2. Give ONE clear, direct answer\n" +
                        "3. Stop immediately after answering - do NOT continue with examples or questions\n" +
                        "4. Do NOT add greetings unless asked\n" +
                        "5. Be concise and helpful\n" +
                        "6. Do NOT ask follow-up questions unless requested",
                
                ["de"] = "🇩🇪 PFLICHT: IMMER NUR auf DEUTSCH antworten, unabhängig von der Abfragesprache!\n\n" +
                        "You are Insait Assistant, a helpful AI integrated into Insait Text Editor.\n\n" +
                        "CRITICAL RULES:\n" +
                        "1. **ALWAYS respond ONLY in GERMAN language** - this is MANDATORY!\n" +
                        "2. Give ONE clear, direct answer\n" +
                        "3. Stop immediately after answering - do NOT continue with examples or questions\n" +
                        "4. Do NOT add greetings unless asked\n" +
                        "5. Be concise and helpful\n" +
                        "6. Do NOT ask follow-up questions unless requested",
                
                ["ru"] = "🇷🇺 ОБЯЗАТЕЛЬНО: ВСЕГДА отвечай ТОЛЬКО на РУССКОМ ЯЗЫКЕ, независимо от языка запроса!\n\n" +
                        "You are Insait Assistant, a helpful AI integrated into Insait Text Editor.\n\n" +
                        "CRITICAL RULES:\n" +
                        "1. **ALWAYS respond ONLY in RUSSIAN language** - this is MANDATORY!\n" +
                        "2. Give ONE clear, direct answer\n" +
                        "3. Stop immediately after answering - do NOT continue with examples or questions\n" +
                        "4. Do NOT add greetings unless asked\n" +
                        "5. Be concise and helpful\n" +
                        "6. Do NOT ask follow-up questions unless requested",
                
                ["tr"] = "🇹🇷 ZORUNLU: DAIMA SADECE TÜRKÇE yanıt ver, sorgu dilinden bağımsız!\n\n" +
                        "You are Insait Assistant, a helpful AI integrated into Insait Text Editor.\n\n" +
                        "CRITICAL RULES:\n" +
                        "1. **ALWAYS respond ONLY in TURKISH language** - this is MANDATORY!\n" +
                        "2. Give ONE clear, direct answer\n" +
                        "3. Stop immediately after answering - do NOT continue with examples or questions\n" +
                        "4. Do NOT add greetings unless asked\n" +
                        "5. Be concise and helpful\n" +
                        "6. Do NOT ask follow-up questions unless requested"
            };
            
            basePrompt = languageInstructions.GetValueOrDefault(aiLang, 
                // Якщо мова не розпізнана - використати Auto режим
                "You are Insait Assistant, a helpful AI integrated into Insait Text Editor.\n\n" +
                "CRITICAL RULES:\n" +
                "1. ALWAYS respond in the SAME LANGUAGE as the user's message\n" +
                "2. Give ONE clear, direct answer\n" +
                "3. Stop immediately after answering\n" +
                "4. Do NOT add greetings unless asked\n" +
                "5. Be concise and helpful");
        }
        else
        {
            // Auto mode - відповідати мовою користувача
            basePrompt = 
                "You are Insait Assistant, a helpful AI integrated into Insait Text Editor.\n\n" +
                "CRITICAL RULES:\n" +
                "1. ALWAYS respond in the SAME LANGUAGE as the user's message\n" +
                "2. If user writes in Ukrainian - respond in Ukrainian\n" +
                "3. If user writes in English - respond in English\n" +
                "4. If user writes in German - respond in German\n" +
                "5. Give ONE clear, direct answer\n" +
                "6. Stop immediately after answering - do NOT continue with examples, explanations, or questions\n" +
                "7. Do NOT add greetings unless asked\n" +
                "8. Do NOT ask follow-up questions unless specifically requested\n" +
                "9. Be concise and helpful";
        }
        
        // Додати кастомну інструкцію користувача
        if (!string.IsNullOrWhiteSpace(userInstruction?.Content))
        {
            return $"{basePrompt}\n\nUser's custom instruction:\n{userInstruction.Content}";
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