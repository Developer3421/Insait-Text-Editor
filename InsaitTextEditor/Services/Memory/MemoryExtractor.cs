using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using InsaitTextEditor.Models.Memory;
using InsaitTextEditor.AI;

namespace InsaitTextEditor.Services.Memory;

/// <summary>
/// Extracts facts from user messages using AI
/// </summary>
public class MemoryExtractor
{
    private readonly LlamaSharpInferenceEngine _inferenceEngine;

    public MemoryExtractor(LlamaSharpInferenceEngine inferenceEngine)
    {
        _inferenceEngine = inferenceEngine;
    }

    public async Task<List<MemoryFact>> ExtractFactsAsync(string message)
    {
        var prompt = BuildExtractionPrompt(message);
        var response = await _inferenceEngine.GenerateResponseAsync(prompt, default);
        
        return ParseFactsFromResponse(response);
    }

    private string BuildExtractionPrompt(string message)
    {
        return $@"Analyze this message and extract important facts about the user.
Only extract facts that are worth remembering long-term.

MESSAGE: {message}

Extract facts in this JSON format:
[
  {{""type"": ""Personal"", ""content"": ""User is a software developer"", ""tags"": [""profession"", ""developer""]}},
  {{""type"": ""Project"", ""content"": ""Working on Insait Text Editor"", ""tags"": [""project"", ""insait""]}}
]

Types: Personal, Project, Preference, Context
Output ONLY valid JSON array, or empty array [] if no facts found.";
    }

    private List<MemoryFact> ParseFactsFromResponse(string response)
    {
        try
        {
            // Try to parse JSON
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var factDtos = JsonSerializer.Deserialize<List<FactDto>>(jsonStr, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                return factDtos?.Select(dto => new MemoryFact
                {
                    Content = dto.Content ?? string.Empty,
                    Type = ParseFactType(dto.Type ?? "Context"),
                    Tags = dto.Tags ?? new List<string>(),
                    Confidence = 0.8 // AI-extracted facts have lower confidence
                }).ToList() ?? new List<MemoryFact>();
            }
        }
        catch
        {
            // If parsing fails, return an empty list
        }

        return new List<MemoryFact>();
    }

    private FactType ParseFactType(string type)
    {
        return type switch
        {
            "Personal" => FactType.Personal,
            "Project" => FactType.Project,
            "Preference" => FactType.Preference,
            "Context" => FactType.Context,
            _ => FactType.Context
        };
    }

    private class FactDto
    {
        public string? Type { get; set; }
        public string? Content { get; set; }
        public List<string>? Tags { get; set; }
    }
}
