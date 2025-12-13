using System.Collections.Generic;

namespace InsaitTextEditor.Models.Memory;

/// <summary>
/// Represents a query to search memory facts
/// </summary>
public class MemoryQuery
{
    public string QueryText { get; set; } = string.Empty;
    public List<FactType>? FactTypes { get; set; }  // Filter by fact types
    public List<string>? Tags { get; set; }  // Filter by tags
    public int MaxResults { get; set; } = 5;
    public double MinConfidence { get; set; } = 0.0;  // Minimum confidence threshold
    public bool ActiveOnly { get; set; } = true;  // Only search active facts
}

