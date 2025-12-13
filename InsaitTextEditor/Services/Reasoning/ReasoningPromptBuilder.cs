using System.Collections.Generic;
using System.Linq;
using System.Text;
using InsaitTextEditor.Models;
using InsaitTextEditor.Models.Reasoning;
using InsaitTextEditor.Services;

namespace InsaitTextEditor.Services.Reasoning;

/// <summary>
/// Генерує спеціальні промпти для Chain-of-Thought reasoning
/// </summary>
public class ReasoningPromptBuilder
{
    private readonly UserInstructionService? _instructionService;

    public ReasoningPromptBuilder(UserInstructionService? instructionService = null)
    {
        _instructionService = instructionService;
    }

    /// <summary>
    /// Отримати мовні інструкції для reasoning на основі налаштувань користувача
    /// </summary>
    private string GetLanguageInstruction()
    {
        var userInstruction = _instructionService?.GetUserInstruction();
        var aiLang = userInstruction?.AiLanguage;
        
        if (string.IsNullOrEmpty(aiLang))
        {
            // Auto mode - відповідати мовою користувача
            return "IMPORTANT: Respond in the SAME LANGUAGE as the user's query.";
        }
        
        // Мапінг мов до інструкцій
        var languageInstructions = new Dictionary<string, string>
        {
            ["uk"] = "🇺🇦 ОБОВ'ЯЗКОВО: Відповідай ТІЛЬКИ УКРАЇНСЬКОЮ МОВОЮ!",
            ["en"] = "🇬🇧 MANDATORY: Respond ONLY in ENGLISH!",
            ["de"] = "🇩🇪 PFLICHT: Antworte NUR auf DEUTSCH!",
            ["ru"] = "🇷🇺 ОБЯЗАТЕЛЬНО: Отвечай ТОЛЬКО на РУССКОМ ЯЗЫКЕ!",
            ["tr"] = "🇹🇷 ZORUNLU: SADECE TÜRKÇE yanıt ver!"
        };
        
        return languageInstructions.GetValueOrDefault(aiLang, 
            "IMPORTANT: Respond in the SAME LANGUAGE as the user's query.");
    }

    public string BuildPlanningPrompt(string userQuery)
    {
        var languageInstruction = GetLanguageInstruction();
        
        return $@"{languageInstruction}

You are a reasoning assistant. Break down this problem into logical steps.

USER QUERY: {userQuery}

Generate a step-by-step plan to solve this. Output ONLY the steps in this format:
Step 1: [Brief title]
Step 2: [Brief title]
Step 3: [Brief title]

Maximum 5 steps. Be concise.";
    }

    public List<ReasoningStep> ParsePlanIntoSteps(string planResponse)
    {
        var steps = new List<ReasoningStep>();
        var lines = planResponse.Split('\n');
        
        int stepNumber = 1;
        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            if (trimmedLine.StartsWith("Step ", System.StringComparison.OrdinalIgnoreCase))
            {
                var colonIndex = trimmedLine.IndexOf(':');
                if (colonIndex > 0 && colonIndex < trimmedLine.Length - 1)
                {
                    var title = trimmedLine.Substring(colonIndex + 1).Trim();
                    steps.Add(new ReasoningStep
                    {
                        StepNumber = stepNumber++,
                        Title = title,
                        Status = StepStatus.Pending
                    });
                }
            }
        }

        // Якщо парсинг не вдався, створити один загальний крок
        if (steps.Count == 0)
        {
            steps.Add(new ReasoningStep
            {
                StepNumber = 1,
                Title = "Solve the problem",
                Status = StepStatus.Pending
            });
        }

        return steps;
    }

    public string BuildStepPrompt(string userQuery, ReasoningStep currentStep, List<ReasoningStep> allSteps)
    {
        var languageInstruction = GetLanguageInstruction();
        var previousSteps = allSteps
            .Where(s => s.StepNumber < currentStep.StepNumber && s.Status == StepStatus.Completed)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine(languageInstruction);
        sb.AppendLine();
        sb.AppendLine($"ORIGINAL QUERY: {userQuery}");
        sb.AppendLine();
        sb.AppendLine($"CURRENT STEP ({currentStep.StepNumber}): {currentStep.Title}");
        sb.AppendLine();

        if (previousSteps.Any())
        {
            sb.AppendLine("PREVIOUS STEPS:");
            foreach (var prev in previousSteps)
            {
                sb.AppendLine($"Step {prev.StepNumber}: {prev.Title}");
                sb.AppendLine($"Result: {prev.Content}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("Now execute THIS step and provide the result. Be clear and concise.");
        
        return sb.ToString();
    }

    public string BuildFinalAnswerPrompt(string userQuery, List<ReasoningStep> completedSteps)
    {
        var languageInstruction = GetLanguageInstruction();
        var sb = new StringBuilder();
        sb.AppendLine(languageInstruction);
        sb.AppendLine();
        sb.AppendLine($"ORIGINAL QUERY: {userQuery}");
        sb.AppendLine();
        sb.AppendLine("REASONING STEPS COMPLETED:");
        
        foreach (var step in completedSteps)
        {
            sb.AppendLine($"Step {step.StepNumber}: {step.Title}");
            sb.AppendLine($"Result: {step.Content}");
            sb.AppendLine();
        }

        sb.AppendLine("Now provide a clear, final answer to the original query based on the reasoning above.");
        sb.AppendLine("Be concise but complete.");
        
        return sb.ToString();
    }
}
