using Avalonia.Controls;
using Avalonia.Interactivity;
using InsaitTextEditor.Services;
using InsaitTextEditor.Windows;
using InsaitTextEditor.Scripts.WindowsControl;

namespace InsaitTextEditor;

public partial class MainWindow : Window
{
    // Відкриваємо меню файлів як немодальне вікно (не блокує UI)
    private void FileMenu_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TabManager manager)
            return;

        WindowManager.ShowSingleton<FileMenuWindow>(this, factory: () => new FileMenuWindow(
            openAsync: () => manager.OpenTextFileAsync(this),
            saveAsync: () => manager.SaveCurrentToFileAsync(this, saveAs: false),
            saveAsAsync: () => manager.SaveCurrentToFileAsync(this, saveAs: true)
        ));
    }
}