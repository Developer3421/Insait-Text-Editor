using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace InsaitTextEditor.Windows;

public partial class ComplaintWindow : Window
{
    public ComplaintWindow()
    {
        InitializeComponent();
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
        var tb = this.FindControl<TextBox>("ComplaintTextBox");
        var status = this.FindControl<TextBlock>("StatusText");

        var text = tb?.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            if (status != null) status.Text = "Текст скарги порожній.";
            return;
        }

        try
        {
            // Prefer native file picker. Falls back to Documents with timestamp.
            var provider = StorageProvider;
            if (provider != null && provider.CanSave)
            {
                var suggested = $"complaint-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt";
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
                await writer.WriteAsync(text);

                if (status != null) status.Text = $"Збережено: {file.Name}";
                return;
            }

            await SaveFallbackAsync(text);
            if (status != null) status.Text = "Збережено в Documents.";
        }
        catch (Exception ex)
        {
            if (status != null) status.Text = $"Помилка збереження: {ex.Message}";
        }
    }

    private static async Task SaveFallbackAsync(string text)
    {
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(docs) || !Directory.Exists(docs))
            docs = AppContext.BaseDirectory;

        var path = Path.Combine(docs, $"complaint-{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
        await File.WriteAllTextAsync(path, text);
    }
}

