using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace InsaitTextEditor.Windows;

public partial class ComplaintWindow : Window
{
    private readonly string _initialAiAnswer;

    // Required for WindowManager.ShowSingleton<T>() which uses Activator.CreateInstance<T>().
    public ComplaintWindow() : this(aiAnswer: null)
    {
    }

    public ComplaintWindow(string? aiAnswer = null)
    {
        _initialAiAnswer = aiAnswer ?? string.Empty;

        InitializeComponent();

        // Defer control lookup until the window is opened (visual tree is ready).
        Opened += (_, _) =>
        {
            try
            {
                SetAiAnswer(_initialAiAnswer);
            }
            catch
            {
                // Best-effort; never crash the app because of a UI prefill.
            }
        };
    }

    public void SetAiAnswer(string? aiAnswer)
    {
        try
        {
            var aiTb = this.FindControl<TextBox>("AiAnswerTextBox");
            if (aiTb != null)
                aiTb.Text = aiAnswer ?? string.Empty;
        }
        catch
        {
            // ignore
        }
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Close_Click(object? sender, RoutedEventArgs e)
        => Close();

    private async void SaveToFile_Click(object? sender, RoutedEventArgs e)
    {
        var complaintTb = this.FindControl<TextBox>("ComplaintTextBox");
        var aiTb = this.FindControl<TextBox>("AiAnswerTextBox");
        var status = this.FindControl<TextBlock>("StatusText");

        var complaintText = complaintTb?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(complaintText))
        {
            if (status != null) status.Text = "Текст скарги порожній.";
            return;
        }

        var aiAnswer = aiTb?.Text ?? string.Empty;

        var payload = BuildPayload(complaintText, aiAnswer);

        try
        {
            var provider = StorageProvider;
            if (provider != null && provider.CanSave)
            {
                var suggested = $"complaint-ai-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
                var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Зберегти скаргу",
                    SuggestedFileName = suggested,
                    DefaultExtension = "txt",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("Text") { Patterns = new[] { "*.txt" } }
                    }
                });

                if (file == null)
                {
                    if (status != null) status.Text = "Збереження скасовано.";
                    return;
                }

                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(payload);

                if (status != null) status.Text = $"Збережено: {file.Name}";
                return;
            }

            await SaveFallbackAsync(payload);
            if (status != null) status.Text = "Збережено в Documents.";
        }
        catch (Exception ex)
        {
            if (status != null) status.Text = $"Помилка збереження: {ex.Message}";
        }
    }

    private static string BuildPayload(string complaintText, string aiAnswer)
    {
        var sb = new StringBuilder();
        sb.AppendLine("InsaitTextEditor - Complaint about AI answer");
        sb.AppendLine($"Timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(aiAnswer))
        {
            sb.AppendLine("=== AI ANSWER (context) ===");
            sb.AppendLine(aiAnswer.Trim());
            sb.AppendLine();
        }

        sb.AppendLine("=== COMPLAINT ===");
        sb.AppendLine(complaintText.Trim());
        sb.AppendLine();
        sb.AppendLine("Please send to: vetalebrowser01@gmail.com");

        return sb.ToString();
    }

    private static async Task SaveFallbackAsync(string text)
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(docs) || !Directory.Exists(docs))
            docs = AppContext.BaseDirectory;

        var path = Path.Combine(docs, $"complaint-ai-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
        await File.WriteAllTextAsync(path, text);
    }
}
