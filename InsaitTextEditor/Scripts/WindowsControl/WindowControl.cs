using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace InsaitTextEditor.Scripts.WindowsControl;

// Helper for dragging and resizing a borderless window
public static class WindowDragResizeHelper
{
    // Handle pointer press on "title bar": dragging or Max/Restore on double-click
    public static void OnTitleBarPointerPressed(Window window, StackPanel ignoreDescendantsOf, PointerPressedEventArgs e)
    {
        // Ignore clicks on the panel with buttons (check if position falls inside its rectangle)
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

        // Double click: toggle Max/Restore
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

    // Resize by the specified edge/corner
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