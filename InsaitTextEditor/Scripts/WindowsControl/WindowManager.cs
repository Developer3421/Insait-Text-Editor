using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace InsaitTextEditor.Scripts.WindowsControl;

/// <summary>
/// Manages single-instance windows across the application. Ensures only one instance
/// of a specific Window type is shown at any time. If an instance exists, it is focused/activated.
/// </summary>
public static class WindowManager
{
    private static readonly Dictionary<Type, Window> OpenWindows = new();
    private static readonly Dictionary<Window, Task> DialogTasks = new();

    /// <summary>
    /// Show a modeless window ensuring there is only one instance per window type.
    /// If an instance already exists, it will be activated and returned.
    /// </summary>
    public static TWindow ShowSingleton<TWindow>(Window? owner = null, Func<TWindow>? factory = null)
        where TWindow : Window
    {
        var t = typeof(TWindow);
        if (OpenWindows.TryGetValue(t, out var existing) && existing is TWindow existingTyped)
        {
            BringToFront(existingTyped);
            return existingTyped;
        }

        var window = factory != null ? factory() : Activator.CreateInstance<TWindow>();

        OpenWindows[t] = window;
        window.Closed += (_, _) =>
        {
            OpenWindows.Remove(t);
            DialogTasks.Remove(window);
        };

        if (owner is not null)
            window.Show(owner);
        else
            window.Show();

        return window;
    }

    /// <summary>
    /// Show a modal dialog ensuring there is only one instance per window type.
    /// If an instance already exists, it is activated and the original ShowDialog Task is returned.
    /// </summary>
    public static Task ShowDialogSingletonAsync<TWindow>(Window owner, Func<TWindow>? factory = null)
        where TWindow : Window
    {
        var t = typeof(TWindow);
        if (OpenWindows.TryGetValue(t, out var existing))
        {
            BringToFront(existing);
            if (DialogTasks.TryGetValue(existing, out var existingDialogTask))
                return existingDialogTask;
            return Task.CompletedTask;
        }

        var window = factory != null ? factory() : Activator.CreateInstance<TWindow>();

        OpenWindows[t] = window;
        var dialogTask = window.ShowDialog(owner);
        DialogTasks[window] = dialogTask;

        window.Closed += (_, _) =>
        {
            OpenWindows.Remove(t);
            DialogTasks.Remove(window);
        };

        return dialogTask;
    }

    private static void BringToFront(Window window)
    {
        try
        {
            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;
            // Activate should focus the window cross-platform.
            window.Activate();
        }
        catch
        {
            // No-op: best-effort focus.
        }
    }
}
