using Avalonia.Controls;
using Avalonia.Interactivity;
using InsaitTextEditor.Models;
using InsaitTextEditor.Services;
using InsaitTextEditor.Windows;
using InsaitTextEditor.Scripts.WindowsControl;

namespace InsaitTextEditor;

public partial class MainWindow : Window
{
    private void View_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TabManager manager)
            return;

        // Load current settings for the active tab as initial values in the dialog
        EditorSettings initial = manager.GetActiveSettings();

        WindowManager.ShowSingleton<ViewWindow>(this, factory: () => new ViewWindow(
            initial,
            applyPage: s => manager.ApplySettingsToActive(s),
            applyAll: s => manager.ApplySettingsToAll(s),
            saveDefaults: s => SettingsService.SaveDefaults(s)
        ));
    }
}
