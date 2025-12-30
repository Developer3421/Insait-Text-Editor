using System;
using System.IO;

namespace InsaitTextEditor.Services;

/// <summary>
/// Tiny, Store-safe file logger intended for startup diagnostics.
/// Never throws.
/// </summary>
public static class StartupDiagnostics
{
    private static readonly object Gate = new();

    private static string GetDataRoot()
    {
        var overrideRoot = Environment.GetEnvironmentVariable("INSAIT_DATA_ROOT");
        if (!string.IsNullOrWhiteSpace(overrideRoot))
            return overrideRoot;

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "InsaitTextEditor");
    }

    private static string GetLogPath()
        => Path.Combine(GetDataRoot(), "Logs", "startup.log");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Info(string message, Exception? ex) => Write("INFO", message, ex);

    public static void Warn(string message) => Write("WARN", message, null);

    public static void Warn(string message, Exception? ex) => Write("WARN", message, ex);

    public static void Error(string message, Exception? ex = null) => Write("ERROR", message, ex);

    private static void Write(string level, string message, Exception? ex)
    {
        try
        {
            var now = DateTime.UtcNow;
            var line = $"{now:O} [{level}] {message}";
            if (ex != null)
                line += $" | {ex.GetType().Name}: {ex.Message}";

            lock (Gate)
            {
                var path = GetLogPath();
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch
        {
            // never throw
        }
    }
}
