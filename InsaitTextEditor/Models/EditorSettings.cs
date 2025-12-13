using Avalonia.Media;

namespace InsaitTextEditor.Models;

public sealed class EditorSettings
{
    // Text area / page drawing
    public string LineColorHex { get; set; } = "#FFB0B0B0"; // default grey line
    public string TextColorHex { get; set; } = "#FF000000"; // black
    public double FontSize { get; set; } = 16d;
    public bool Bold { get; set; } = false;
    public bool Italic { get; set; } = false;

    // New settings: workspace paper background, optional alternate line shading, and tabs colors
    public string PaperColorHex { get; set; } = "#FFFFFDF7"; // matches RuledBackground default PaperBrush
    public string AltLineColorHex { get; set; } = "#00FFFFFF"; // transparent by default (no shading)

    // Tabs styling
    public string TabsBackgroundHex { get; set; } = "#FFCC5500"; // close to AppDarkOrangeBrush
    public string TabsTextColorHex { get; set; } = "#FF000000"; // black
}
