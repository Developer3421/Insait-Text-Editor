using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using InsaitTextEditor.Services;

namespace InsaitTextEditor.Windows;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        ApplyWorkspaceTheme();
        SetVersionText();
    }

    private void ApplyWorkspaceTheme()
    {
        var s = SettingsService.LoadDefaults();
        // Update brushes defined in XAML resources
        if (this.Resources["WorkspaceBackgroundBrush"] is SolidColorBrush bg)
        {
            bg.Color = Color.Parse(s.PaperColorHex ?? "#FFFFFDF7");
        }
        if (this.Resources["WorkspaceTextBrush"] is SolidColorBrush fg)
        {
            fg.Color = Color.Parse(s.TextColorHex ?? "#FF000000");
        }
    }

    private void SetVersionText()
    {
        try
        {
            var ver = typeof(AboutWindow).Assembly.GetName().Version;
            var versionStr = ver is null ? "" : (ver.Build >= 0 ? ver.ToString() : $"{ver.Major}.{ver.Minor}");
            this.FindControl<TextBlock>("VersionText")!.Text = string.IsNullOrWhiteSpace(versionStr)
                ? "Version"
                : $"Version {versionStr}";
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
}
