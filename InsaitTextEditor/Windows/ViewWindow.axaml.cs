using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using InsaitTextEditor.Models;
using InsaitTextEditor.Services;

namespace InsaitTextEditor.Windows;

public partial class ViewWindow : Window
{
    private readonly Action<EditorSettings>? _applyPage;
    private readonly Action<EditorSettings>? _applyAll;
    private readonly Action<EditorSettings>? _saveDefaults;

    // Public parameterless constructor for runtime XAML loader
    public ViewWindow()
    {
        InitializeComponent();
        _applyPage = null;
        _applyAll = null;
        _saveDefaults = null;
        var defaults = Services.SettingsService.LoadDefaults();
        LoadIntoForm(defaults);
    }

    public ViewWindow(EditorSettings initial,
                      Action<EditorSettings> applyPage,
                      Action<EditorSettings> applyAll,
                      Action<EditorSettings> saveDefaults)
    {
        InitializeComponent();
        _applyPage = applyPage;
        _applyAll = applyAll;
        _saveDefaults = saveDefaults;
        LoadIntoForm(initial);
    }

    private void LoadIntoForm(EditorSettings s)
    {
        this.FindControl<TextBox>("LineColorBox")!.Text = s.LineColorHex;
        this.FindControl<TextBox>("TextColorBox")!.Text = s.TextColorHex;
        this.FindControl<TextBox>("FontSizeBox")!.Text = (s.FontSize <= 0 ? 16d : s.FontSize).ToString(System.Globalization.CultureInfo.InvariantCulture);
        this.FindControl<CheckBox>("BoldCheck")!.IsChecked = s.Bold;
        this.FindControl<CheckBox>("ItalicCheck")!.IsChecked = s.Italic;
    }

    private static double ParseFont(string? s)
    {
        if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
            return Math.Clamp(v, 8d, 96d);
        return 16d;
    }

    private EditorSettings Collect()
    {
        var lineHex = this.FindControl<TextBox>("LineColorBox")!.Text?.Trim();
        var textHex = this.FindControl<TextBox>("TextColorBox")!.Text?.Trim();
        var font = ParseFont(this.FindControl<TextBox>("FontSizeBox")!.Text);
        var bold = this.FindControl<CheckBox>("BoldCheck")!.IsChecked == true;
        var italic = this.FindControl<CheckBox>("ItalicCheck")!.IsChecked == true;
        return new EditorSettings
        {
            LineColorHex = string.IsNullOrWhiteSpace(lineHex) ? "#FFB0B0B0" : lineHex!,
            TextColorHex = string.IsNullOrWhiteSpace(textHex) ? "#FF000000" : textHex!,
            FontSize = font,
            Bold = bold,
            Italic = italic
        };
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void DragSurface_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (e.Source is Visual v)
        {
            if (v is Button || v.FindAncestorOfType<Button>() is not null)
                return;
        }
        BeginMoveDrag(e);
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Close_Click(object? sender, RoutedEventArgs e)
        => Close();

    private void ApplyPage_Click(object? sender, RoutedEventArgs e)
    {
        var s = Collect();
        _applyPage?.Invoke(s);
    }

    private void ApplyAll_Click(object? sender, RoutedEventArgs e)
    {
        var s = Collect();
        _applyAll?.Invoke(s);
    }

    private void SaveDefaults_Click(object? sender, RoutedEventArgs e)
    {
        var s = Collect();
        _saveDefaults?.Invoke(s);
    }

    private void ResetToDefaults_Click(object? sender, RoutedEventArgs e)
    {
        var s = SettingsService.LoadDefaults();
        LoadIntoForm(s);
    }
}