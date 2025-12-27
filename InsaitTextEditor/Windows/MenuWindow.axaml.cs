using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using InsaitTextEditor.Scripts.WindowsControl;

namespace InsaitTextEditor.Windows;

public partial class MenuWindow : Window
{
    public MenuWindow()
    {
        InitializeComponent();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    // Allow dragging on the purple area but ignore when clicking buttons
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

    private async void Settings_Click(object? sender, RoutedEventArgs e)
    {
        var owner = this.IsVisible ? this : ((this.Owner as Window) ?? this);
        await WindowManager.ShowDialogSingletonAsync<SettingsWindow>(owner);
        Close();
    }

    private async void About_Click(object? sender, RoutedEventArgs e)
    {
        var owner = this.IsVisible ? this : ((this.Owner as Window) ?? this);
        await WindowManager.ShowDialogSingletonAsync<AboutWindow>(owner);
        Close();
    }

    private async void Language_Click(object? sender, RoutedEventArgs e)
    {
        var owner = this.IsVisible ? this : ((this.Owner as Window) ?? this);
        await WindowManager.ShowDialogSingletonAsync<LanguageWindow>(owner);
        Close();
    }

    private async void UserAgreement_Click(object? sender, RoutedEventArgs e)
    {
        var owner = this.IsVisible ? this : ((this.Owner as Window) ?? this);
        await WindowManager.ShowDialogSingletonAsync<UserAgreementWindow>(owner);
        Close();
    }
}