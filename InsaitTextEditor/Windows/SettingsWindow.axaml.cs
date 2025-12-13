using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using InsaitTextEditor.Models;
using InsaitTextEditor.Services;

namespace InsaitTextEditor.Windows;

public partial class SettingsWindow : Window
{
    // Public parameterless constructor for runtime XAML loader
    public SettingsWindow()
    {
        InitializeComponent();
        LoadIntoForm(SettingsService.LoadDefaults());
    }

    private static double ParseFont(string? s)
    {
        if (double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
            return Math.Clamp(v, 8d, 96d);
        return 16d;
    }

    private void LoadIntoForm(EditorSettings s)
    {
        this.FindControl<TextBox>("PaperColorBox")!.Text = s.PaperColorHex;
        this.FindControl<TextBox>("AltLineColorBox")!.Text = s.AltLineColorHex;
        this.FindControl<TextBox>("FontSizeBox")!.Text = (s.FontSize <= 0 ? 16d : s.FontSize).ToString(System.Globalization.CultureInfo.InvariantCulture);
        this.FindControl<TextBox>("TabsBackgroundBox")!.Text = s.TabsBackgroundHex;
        this.FindControl<TextBox>("TabsTextColorBox")!.Text = s.TabsTextColorHex;
    }

    private EditorSettings Collect()
    {
        var paper = this.FindControl<TextBox>("PaperColorBox")!.Text?.Trim();
        var alt = this.FindControl<TextBox>("AltLineColorBox")!.Text?.Trim();
        var font = ParseFont(this.FindControl<TextBox>("FontSizeBox")!.Text);
        var tabsBg = this.FindControl<TextBox>("TabsBackgroundBox")!.Text?.Trim();
        var tabsText = this.FindControl<TextBox>("TabsTextColorBox")!.Text?.Trim();
        var currentDefaults = SettingsService.LoadDefaults();
        return new EditorSettings
        {
            // keep existing defaults for unrelated fields
            LineColorHex = currentDefaults.LineColorHex,
            TextColorHex = currentDefaults.TextColorHex,
            FontSize = font,
            Bold = currentDefaults.Bold,
            Italic = currentDefaults.Italic,
            PaperColorHex = string.IsNullOrWhiteSpace(paper) ? "#FFFFFDF7" : paper!,
            AltLineColorHex = string.IsNullOrWhiteSpace(alt) ? "#00FFFFFF" : alt!,
            TabsBackgroundHex = string.IsNullOrWhiteSpace(tabsBg) ? "#FFCC5500" : tabsBg!,
            TabsTextColorHex = string.IsNullOrWhiteSpace(tabsText) ? "#FF000000" : tabsText!
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

    private void SaveDefaults_Click(object? sender, RoutedEventArgs e)
    {
        var s = Collect();
        SettingsService.SaveDefaults(s);
        Close();
    }

    private void ResetToDefaults_Click(object? sender, RoutedEventArgs e)
    {
        LoadIntoForm(SettingsService.LoadDefaults());
    }
}