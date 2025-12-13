using System;
using System.Collections.Generic;

namespace InsaitTextEditor.Models.Memory;

/// <summary>
/// Represents a fact stored in global memory
/// </summary>
public class MemoryFact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = string.Empty;
    public FactType Type { get; set; }
    public string Source { get; set; } = string.Empty;  // "Chat", "User Edit", etc.
    public Guid? SourceMessageId { get; set; }
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public double Confidence { get; set; } = 1.0;  // 0.0 to 1.0
    public List<string> Tags { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public Guid? UpdatedBy { get; set; }  // If this fact was updated by another fact
}

public enum FactType
{
    Personal,      // Information about the user (name, profession, preferences)
    Project,       // Information about projects (name, technologies, goals)
    Preference,    // Preferences (code style, response language)
    Context        // Contextual information (current task, problems)
}

