using System.Collections.Generic;

namespace InsaitTextEditor.Models;

public class AgentMessage : ChatMessage
{
    public List<ToolInvocation> ToolCalls { get; set; } = new();
    public string ModelUsed { get; set; } = "Gemma-3-1B";
    public int TokensUsed { get; set; }
}