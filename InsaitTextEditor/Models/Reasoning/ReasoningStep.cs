using System;

namespace InsaitTextEditor.Models.Reasoning;

/// <summary>
/// Represents a single step in a reasoning chain
/// </summary>
public class ReasoningStep
{
    public int StepNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum StepStatus
{
    Pending,   // Waiting to be executed
    Running,   // Currently executing
    Completed, // Successfully completed
    Failed     // Error occurred
}

