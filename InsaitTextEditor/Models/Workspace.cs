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

    // Режим фону сторінки для цієї вкладки (зберігаємо на рівні Workspace)
    public PageBackgroundMode BackgroundMode { get; set; }

    // Пер-сторінкові візуальні налаштування
    public string LineColorHex { get; set; }
    public string TextColorHex { get; set; }
    public double FontSize { get; set; }
    public bool Bold { get; set; }
    public bool Italic { get; set; }

    public void Dispose()
    {
        // Якщо зʼявляться керовані/некеровані ресурси — вичистити тут.
    }
}