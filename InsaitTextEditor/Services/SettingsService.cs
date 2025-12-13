using System;
using System.IO;
using LiteDB;
using InsaitTextEditor.Models;

namespace InsaitTextEditor.Services;

public static class SettingsService
{
    private const string Header = "# InsaitTextEditor Settings DB";

    private static string GetProjectDatabaseDir()
    {
        var baseDir = GetExecutableDirectory();
        var dbDir = Path.Combine(baseDir, "Database");
        Directory.CreateDirectory(dbDir);
        return dbDir;
    }

    private static string GetDbFilePath() => Path.Combine(GetProjectDatabaseDir(), "settings_v2.litedb");

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
        var path = GetDbFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var cs = new ConnectionString { Filename = path, Connection = ConnectionType.Shared };
        return new LiteDatabase(cs);
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
}
