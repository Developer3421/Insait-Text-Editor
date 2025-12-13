using System;
using System.Collections.Generic;

namespace InsaitTextEditor.Models;

public class AgentResponse
{
    public string Content { get; set; } = string.Empty;
    public List<ToolInvocation> ToolsUsed { get; set; } = new();
    public int TokensGenerated { get; set; }
    public TimeSpan Duration { get; set; }
}