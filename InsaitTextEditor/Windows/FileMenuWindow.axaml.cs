using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree; // додано

namespace InsaitTextEditor.Windows;

public partial class FileMenuWindow : Window
{
    private readonly Func<Task> _openAsync;
    private readonly Func<Task> _saveAsync;
    private readonly Func<Task> _saveAsAsync;

    // Public parameterless constructor for runtime XAML loader
    public FileMenuWindow()
    {
        InitializeComponent();
        _openAsync = static () => Task.CompletedTask;
        _saveAsync = static () => Task.CompletedTask;
        _saveAsAsync = static () => Task.CompletedTask;
    }

    public FileMenuWindow(Func<Task> openAsync, Func<Task> saveAsync, Func<Task> saveAsAsync)
    {
        InitializeComponent();
        _openAsync = openAsync ?? throw new ArgumentNullException(nameof(openAsync));
        _saveAsync = saveAsync ?? throw new ArgumentNullException(nameof(saveAsync));
        _saveAsAsync = saveAsAsync ?? throw new ArgumentNullException(nameof(saveAsAsync));
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    // Перетягування за фіолетові області; ігноруємо кліки по кнопках
    private void DragSurface_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (e.Source is Visual v)
        {
            // Якщо натиснули на кнопку або всередині неї — не перетягуємо
            if (v is Button || v.FindAncestorOfType<Button>() is not null)
                return;
        }

        BeginMoveDrag(e);
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Close_Click(object? sender, RoutedEventArgs e)
        => Close();

    private async void Open_Click(object? sender, RoutedEventArgs e)
    {
        await _openAsync();
        Close();
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        await _saveAsync();
        Close();
    }

    private async void SaveAs_Click(object? sender, RoutedEventArgs e)
    {
        await _saveAsAsync();
        Close();
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
        => Close();
}