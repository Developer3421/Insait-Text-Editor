using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using InsaitTextEditor.Controls;

namespace InsaitTextEditor;

public partial class MainWindow 
{
    // Кеш редактора, щоб швидше діставати TextBox з LinedTextInput
    private LinedTextInput? _editorHostCache;

    private LinedTextInput? GetEditorHost()
    {
        if (_editorHostCache != null && _editorHostCache.GetVisualRoot() != null)
            return _editorHostCache;

        var host = this.FindControl<LinedTextInput>("LinedEditorHost");
        if (host is null) return null;

        _editorHostCache = host;
        return _editorHostCache;
    }

    // ===== Універсальні помічники для роботи з виділенням у TextBox =====

    // Helpers operating on LinedTextInput (the actual editor control)
    private static (int start, int length) GetSelection(LinedTextInput editor)
    {
        var s = Math.Min(editor.SelectionStart, editor.SelectionEnd);
        var e = Math.Max(editor.SelectionStart, editor.SelectionEnd);
        return (s, Math.Max(0, e - s));
    }

    private static void SetSelection(LinedTextInput editor, int start, int length)
    {
        editor.SelectionStart = start;
        editor.SelectionEnd = start + Math.Max(0, length);
    }

    // Note: use LinedTextInput.ToggleWrapSelection to perform wrap operations (it records undo state)

    // ===== Обробники кнопок тулбару =====

    private void BoldButton_Click(object? sender, RoutedEventArgs e)
    {
        var host = GetEditorHost();
        if (host is null) return;
        host.ToggleWrapSelection("**", "**");
    }

    private void ItalicButton_Click(object? sender, RoutedEventArgs e)
    {
        var host = GetEditorHost();
        if (host is null) return;
        host.ToggleWrapSelection("*", "*");
    }

    private void UnderlineButton_Click(object? sender, RoutedEventArgs e)
    {
        var host = GetEditorHost();
        if (host is null) return;
        host.ToggleWrapSelection("<u>", "</u>");
    }

    private void StrikeButton_Click(object? sender, RoutedEventArgs e)
    {
        var host = GetEditorHost();
        if (host is null) return;
        // Use Markdown-style strikethrough markers
        host.ToggleWrapSelection("~~", "~~");
    }

    private void DeleteSelection_Click(object? sender, RoutedEventArgs e)
    {
        var host = GetEditorHost();
        if (host is null) return;

        var (start, length) = GetSelection(host);
        var text = host.Text ?? string.Empty;

        if (length > 0)
        {
            host.Text = text.Remove(start, length);
            SetSelection(host, start, 0);
        }
        else if (start > 0)
        {
            // Видалити символ ліворуч від курсора (Backspace)
            host.Text = text.Remove(start - 1, 1);
            SetSelection(host, start - 1, 0);
        }

        host.Focus();
    }

    // Додайте ці обробники всередині класу MainWindow
    private void IncreaseFont_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is InsaitTextEditor.Services.TabManager tm)
        {
            tm.CurrentFontSize = Math.Min(96d, Math.Max(8d, tm.CurrentFontSize + 1d));
        }

        var host = GetEditorHost();
        host?.Focus();
    }

    private void DecreaseFont_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is InsaitTextEditor.Services.TabManager tm)
        {
            tm.CurrentFontSize = Math.Max(8d, tm.CurrentFontSize - 1d);
        }
        var host = GetEditorHost();
        host?.Focus();
    }

    private void ClearSelection_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
    var host = GetEditorHost();
    if (host is null) return;

    var text = host.Text ?? string.Empty;
    var (s, length) = GetSelection(host);

    if (length > 0)
    {
        host.Text = text.Remove(s, length);
        host.SelectionStart = s;
        host.SelectionEnd = s;
    }

    host.Focus();
    }
}