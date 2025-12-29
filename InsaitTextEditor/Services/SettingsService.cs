using System;
using System.IO;
using LiteDB;
using InsaitTextEditor.Models;

namespace InsaitTextEditor.Services;

public static class SettingsService
{
    private const string Header = "# InsaitTextEditor Settings DB";

    // GDPR / User agreement
    private const int AgreementCurrentVersion = 1;

    private static string GetProjectDatabaseDir()
    {
        // Prefer exe-adjacent storage (portable) but fall back to LocalAppData for Store/sandbox.
        var baseDir = GetExecutableDirectory();
        var primary = Path.Combine(baseDir, "Database");

        try
        {
            Directory.CreateDirectory(primary);
            return primary;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
        {
            // Store apps often can't write next to the executable.
            try
            {
                var fallbackRoot = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var fallback = Path.Combine(fallbackRoot, "InsaitTextEditor", "Database");
                Directory.CreateDirectory(fallback);
                return fallback;
            }
            catch
            {
                // If even LocalAppData isn't available, keep things alive with BaseDirectory (may still fail later).
                Console.WriteLine($"[SettingsService] Failed to create settings DB directory: {ex.Message}");
                return primary;
            }
        }
    }

    private static string GetDbFilePath() => Path.Combine(GetProjectDatabaseDir(), "settings_v2.litedb");

    /// <summary>
    /// Best-effort DB path logging for diagnostics (never throws).
    /// If we are running in-memory fallback, this still returns the intended file path.
    /// </summary>
    public static string GetSettingsDbPathForDiagnostics()
    {
        try
        {
            return GetDbFilePath();
        }
        catch
        {
            return "<unavailable>";
        }
    }

    /// <summary>
    /// Отримує директорію, де знаходиться виконуваний файл
    /// </summary>
    private static string GetExecutableDirectory()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(processPath))
        {
            return Path.GetDirectoryName(processPath) ?? AppContext.BaseDirectory;
        }
        return AppContext.BaseDirectory;
    }

    private static LiteDatabase OpenDb()
    {
        // Contract: never throw. If file DB isn't possible, fall back to in-memory DB.
        LiteDatabase TryOpen(string? path)
        {
            var cs = new ConnectionString { Filename = path, Connection = ConnectionType.Shared, Upgrade = true };
            return new LiteDatabase(cs);
        }

        try
        {
            var path = GetDbFilePath();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            return TryOpen(path);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SettingsService] Failed to open settings DB file, using in-memory: {ex.GetType().Name}: {ex.Message}");
            return TryOpen(null);
        }
    }

    public static EditorSettings LoadDefaults()
    {
        try
        {
            using var db = OpenDb();
            var meta = db.GetCollection<BsonDocument>("meta");
            var hdr = meta.FindById("header");
            if (hdr is null)
            {
                hdr = new BsonDocument { ["_id"] = "header", ["text"] = Header };
                meta.Upsert(hdr);
            }

            var col = db.GetCollection<BsonDocument>("settings");
            var doc = col.FindById(1);
            if (doc is null)
            {
                return new EditorSettings();
            }

            return new EditorSettings
            {
                LineColorHex = doc.TryGetValue("lineColor", out var lc) && lc.IsString ? lc.AsString : "#FFB0B0B0",
                TextColorHex = doc.TryGetValue("textColor", out var tc) && tc.IsString ? tc.AsString : "#FF000000",
                FontSize = doc.TryGetValue("fontSize", out var fs) && fs.IsDouble ? fs.AsDouble : 16d,
                Bold = doc.TryGetValue("bold", out var b) && b.IsBoolean && b.AsBoolean,
                Italic = doc.TryGetValue("italic", out var it) && it.IsBoolean && it.AsBoolean,
                PaperColorHex = doc.TryGetValue("paperColor", out var pc) && pc.IsString ? pc.AsString : "#FFFFFDF7",
                AltLineColorHex = doc.TryGetValue("altLineColor", out var ac) && ac.IsString ? ac.AsString : "#00FFFFFF",
                TabsBackgroundHex = doc.TryGetValue("tabsBackground", out var tb) && tb.IsString ? tb.AsString : "#FFCC5500",
                TabsTextColorHex = doc.TryGetValue("tabsTextColor", out var ttc) && ttc.IsString ? ttc.AsString : "#FF000000"
            };
        }
        catch
        {
            return new EditorSettings();
        }
    }

    public static void SaveDefaults(EditorSettings settings)
    {
        try
        {
            using var db = OpenDb();
            var col = db.GetCollection<BsonDocument>("settings");
            // Preserve existing language value if present
            var existing = col.FindById(1);
            var language = existing != null && existing.TryGetValue("language", out var l) && l.IsInt32 ? l.AsInt32 : (int)AppLanguage.En;

            var doc = new BsonDocument
            {
                ["_id"] = 1,
                ["lineColor"] = settings.LineColorHex ?? "#FFB0B0B0",
                ["textColor"] = settings.TextColorHex ?? "#FF000000",
                ["fontSize"] = settings.FontSize,
                ["bold"] = settings.Bold,
                ["italic"] = settings.Italic,
                ["paperColor"] = settings.PaperColorHex ?? "#FFFFFDF7",
                ["altLineColor"] = settings.AltLineColorHex ?? "#00FFFFFF",
                ["tabsBackground"] = settings.TabsBackgroundHex ?? "#FFCC5500",
                ["tabsTextColor"] = settings.TabsTextColorHex ?? "#FF000000",
                ["language"] = language
            };
            col.Upsert(doc);
        }
        catch
        {
            // ignore persistence errors silently
        }
    }

    public static AppLanguage LoadLanguage()
    {
        try
        {
            using var db = OpenDb();
            var col = db.GetCollection<BsonDocument>("settings");
            var doc = col.FindById(1);
            if (doc != null && doc.TryGetValue("language", out var l) && l.IsInt32)
            {
                var val = l.AsInt32;
                return Enum.IsDefined(typeof(AppLanguage), val) ? (AppLanguage)val : AppLanguage.En;
            }
        }
        catch { /* ignore */ }
        return AppLanguage.En;
    }

    /// <summary>
    /// Writes a one-line startup diagnostics entry: DB path + resolved UI language.
    /// Safe to call in Store/sandbox; never throws.
    /// </summary>
    public static void LogStartupLanguageDiagnostics()
    {
        try
        {
            var path = GetSettingsDbPathForDiagnostics();
            var lang = LoadLanguage();
            Console.WriteLine($"[SettingsService] Settings DB path: {path}; default UI language: {lang}");
        }
        catch
        {
            // never crash on logging
        }
    }

    public static void SaveLanguage(AppLanguage lang)
    {
        try
        {
            using var db = OpenDb();
            var col = db.GetCollection<BsonDocument>("settings");
            var doc = col.FindById(1) ?? new BsonDocument { ["_id"] = 1 };
            doc["language"] = (int)lang;
            col.Upsert(doc);
        }
        catch { /* ignore */ }
    }

    public static bool IsUserAgreementAccepted(int requiredVersion = AgreementCurrentVersion)
    {
        try
        {
            using var db = OpenDb();
            var col = db.GetCollection<BsonDocument>("settings");
            var doc = col.FindById(1);
            if (doc is null)
                return false;

            var accepted = doc.TryGetValue("agreementAccepted", out var a) && a.IsBoolean && a.AsBoolean;
            if (!accepted)
                return false;

            var version = doc.TryGetValue("agreementVersion", out var v) && v.IsInt32 ? v.AsInt32 : 0;
            return version >= requiredVersion;
        }
        catch
        {
            return false;
        }
    }

    public static void SetUserAgreementAccepted(int version = AgreementCurrentVersion)
    {
        try
        {
            using var db = OpenDb();
            var col = db.GetCollection<BsonDocument>("settings");
            var doc = col.FindById(1) ?? new BsonDocument { ["_id"] = 1 };

            doc["agreementAccepted"] = true;
            doc["agreementVersion"] = version;
            doc["agreementAcceptedUtc"] = DateTime.UtcNow;

            col.Upsert(doc);
        }
        catch
        {
            // ignore
        }
    }
}
