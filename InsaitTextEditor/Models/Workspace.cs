// Models/Workspace.cs
namespace InsaitTextEditor.Models;

public sealed class Workspace : System.IDisposable
{
    public Workspace(System.Guid id, string? text = null)
    {
        Id = id;
        Text = text ?? string.Empty;
        BackgroundMode = PageBackgroundMode.Lined;
        // Editor defaults
        LineColorHex = "#FFB0B0B0";
        TextColorHex = "#FF000000";
        FontSize = 16d;
        Bold = false;
        Italic = false;
    }

    public System.Guid Id { get; }
    public string Text { get; set; }

    // Page background mode for this tab (stored per Workspace)
    public PageBackgroundMode BackgroundMode { get; set; }

    // Per-page visual settings
    public string LineColorHex { get; set; }
    public string TextColorHex { get; set; }
    public double FontSize { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }

    public void Dispose()
    {
        // If managed/unmanaged resources appear later - clean up here.
    }
}