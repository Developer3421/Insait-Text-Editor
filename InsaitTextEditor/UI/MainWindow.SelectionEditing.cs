using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using InsaitTextEditor.Controls;

namespace InsaitTextEditor;

public partial class MainWindow 
{
    // Editor cache for faster access to TextBox from LinedTextInput
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

    // ===== Universal helpers for working with TextBox selection =====

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

    // ===== Toolbar button handlers =====

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
            // Delete character to the left of cursor (Backspace)
            host.Text = text.Remove(start - 1, 1);
            SetSelection(host, start - 1, 0);
        }

        host.Focus();
    }

    // Add these handlers inside MainWindow class
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