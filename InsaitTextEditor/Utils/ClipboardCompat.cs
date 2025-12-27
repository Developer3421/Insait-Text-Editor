using System;
using System.Reflection;
using System.Threading.Tasks;

namespace InsaitTextEditor.Utils;

/// <summary>
/// Compatibility helpers for clipboard APIs across Avalonia versions.
/// Newer Avalonia versions prefer ClipboardExtensions.TryGetTextAsync.
/// </summary>
internal static class ClipboardCompat
{
    private static readonly Func<Avalonia.Input.Platform.IClipboard, Task<string?>>? TryGetTextAsyncDelegate =
        CreateTryGetTextAsyncDelegate();

    public static Task<string?> TryGetTextAsync(Avalonia.Input.Platform.IClipboard clipboard)
    {
        if (clipboard is null) throw new ArgumentNullException(nameof(clipboard));

        // Prefer new API when available.
        if (TryGetTextAsyncDelegate is not null)
            return TryGetTextAsyncDelegate(clipboard);

#pragma warning disable CS0618
        // Fallback for older Avalonia.
        return clipboard.GetTextAsync();
#pragma warning restore CS0618
    }

    private static Func<Avalonia.Input.Platform.IClipboard, Task<string?>>? CreateTryGetTextAsyncDelegate()
    {
        try
        {
            // Avalonia.Input.ClipboardExtensions.TryGetTextAsync(Avalonia.Input.Platform.IClipboard)
            var t = Type.GetType("Avalonia.Input.ClipboardExtensions, Avalonia.Base");
            var m = t?.GetMethod(
                "TryGetTextAsync",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(Avalonia.Input.Platform.IClipboard) },
                modifiers: null);

            if (m is null)
                return null;

            return (Func<Avalonia.Input.Platform.IClipboard, Task<string?>>)
                Delegate.CreateDelegate(typeof(Func<Avalonia.Input.Platform.IClipboard, Task<string?>>), m);
        }
        catch
        {
            return null;
        }
    }
}
