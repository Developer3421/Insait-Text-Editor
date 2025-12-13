using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using InsaitTextEditor.Services;
using Avalonia.Markup.Xaml;

namespace InsaitTextEditor.Windows;

public partial class LanguageWindow : Window
{
    public LanguageWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
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

    private void German_Click(object? sender, RoutedEventArgs e)
    {
        LocalizationService.ChangeLanguage(AppLanguage.De);
        Close();
    }

    private void Russian_Click(object? sender, RoutedEventArgs e)
    {
        LocalizationService.ChangeLanguage(AppLanguage.Ru);
        Close();
    }

    private void Ukrainian_Click(object? sender, RoutedEventArgs e)
    {
        LocalizationService.ChangeLanguage(AppLanguage.Uk);
        Close();
    }

    private void English_Click(object? sender, RoutedEventArgs e)
    {
        LocalizationService.ChangeLanguage(AppLanguage.En);
        Close();
    }

    private void Turkish_Click(object? sender, RoutedEventArgs e)
    {
        LocalizationService.ChangeLanguage(AppLanguage.Tr);
        Close();
    }
}
