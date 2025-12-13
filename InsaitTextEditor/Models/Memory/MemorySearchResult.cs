using System.Collections.Generic;

namespace InsaitTextEditor.Models.Memory;

/// <summary>
/// Represents a search result from memory
/// </summary>
public class MemorySearchResult
{
    public MemoryFact Fact { get; set; } = null!;
    public double RelevanceScore { get; set; }
    public List<string> MatchedKeywords { get; set; } = new();
}

