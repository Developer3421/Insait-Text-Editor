using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace InsaitTextEditor.Scripts.WindowsControl;

// Хелпер для перетягування та ресайзу безрамкового вікна
public static class WindowDragResizeHelper
{
    // Обробка натискання на "тайтлбар": перетягування або Max/Restore по дабл-кліку
    public static void OnTitleBarPointerPressed(Window window, StackPanel ignoreDescendantsOf, PointerPressedEventArgs e)
    {
        // Ігноруємо кліки по панелі з кнопками (перевірка попадання в її прямокутник)
        if (ignoreDescendantsOf is not null)
        {
            var pos = e.GetPosition(ignoreDescendantsOf);
            if (pos.X >= 0 && pos.X <= ignoreDescendantsOf.Bounds.Width &&
                pos.Y >= 0 && pos.Y <= ignoreDescendantsOf.Bounds.Height)
            {
                return;
            }
        }

        var point = e.GetCurrentPoint(window);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        // Подвійний клік: перемикання Max/Restore
        if (e.ClickCount == 2)
        {
            ToggleMaxRestore(window);
            return;
        }

        if (window.WindowState != WindowState.Maximized)
        {
            window.BeginMoveDrag(e);
        }
    }

    // Ресайз за вказаним ребром/кутом
    public static void BeginResize(Window window, WindowEdge edge, PointerPressedEventArgs e)
    {
        if (window.WindowState == WindowState.Maximized)
            return;

        window.BeginResizeDrag(edge, e);
    }

    public static void ToggleMaxRestore(Window window)
    {
        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
}