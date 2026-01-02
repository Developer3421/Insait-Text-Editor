using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using InsaitTextEditor.Services;

namespace InsaitTextEditor.Windows;

public partial class UserAgreementWindow : Window
{
    public const int CurrentAgreementVersion = 1;

    public bool IsAccepted { get; private set; }

    // Public parameterless constructor for runtime XAML loader
    public UserAgreementWindow()
    {
        InitializeComponent();
        ApplyWorkspaceTheme();
        ApplyAcceptedUiStateIfNeeded();
    }

    private void ApplyWorkspaceTheme()
    {
        var s = SettingsService.LoadDefaults();

        if (Resources["WorkspaceBackgroundBrush"] is SolidColorBrush bg)
            bg.Color = Color.Parse(s.PaperColorHex);

        if (Resources["WorkspaceTextBrush"] is SolidColorBrush fg)
            fg.Color = Color.Parse(s.TextColorHex);
    }

    private void ApplyAcceptedUiStateIfNeeded()
    {
        // If agreement is already accepted, this window should be informational only.
        if (!SettingsService.IsUserAgreementAccepted(CurrentAgreementVersion))
            return;

        // Hide consent checkbox area.
        var consentPanel = this.FindControl<StackPanel>("ConsentPanel");
        if (consentPanel is not null)
            consentPanel.IsVisible = false;

        // Hide action buttons + footer hint.
        var actionsPanel = this.FindControl<StackPanel>("ActionsPanel");
        if (actionsPanel is not null)
            actionsPanel.IsVisible = false;

        var mustAcceptText = this.FindControl<TextBlock>("MustAcceptText");
        if (mustAcceptText is not null)
            mustAcceptText.IsVisible = false;

        // Optionally collapse the whole footer row to remove extra space.
        var footerPanel = this.FindControl<Border>("FooterPanel");
        if (footerPanel is not null)
            footerPanel.IsVisible = false;
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
        => Close(false);

    private void Accept_Click(object? sender, RoutedEventArgs e)
    {
        var consent = this.FindControl<CheckBox>("ConsentCheckBox");
        if (consent?.IsChecked != true)
            return;

        SettingsService.SetUserAgreementAccepted(CurrentAgreementVersion);
        IsAccepted = true;
        Close(true);
    }

    private void Decline_Click(object? sender, RoutedEventArgs e)
        => Close(false);
}
