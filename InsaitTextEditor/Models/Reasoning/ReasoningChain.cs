using System;
using System.Collections.Generic;

namespace InsaitTextEditor.Models.Reasoning;

/// <summary>
/// Represents a complete reasoning chain (Chain-of-Thought)
/// </summary>
public class ReasoningChain
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public string UserQuery { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public ChainStatus Status { get; set; } = ChainStatus.Pending;
    public List<ReasoningStep> Steps { get; set; } = new();
    public string FinalAnswer { get; set; } = string.Empty;
    public int TotalTokensUsed { get; set; }
}

public enum ChainStatus
{
    Pending,      // Not started yet
    InProgress,   // Currently executing
    Completed,    // Successfully completed
    Failed        // Error occurred
}

