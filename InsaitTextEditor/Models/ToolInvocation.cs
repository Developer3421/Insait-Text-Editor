using System;

namespace InsaitTextEditor.Models;

public class ToolInvocation
{
    public string ToolName { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; }
}