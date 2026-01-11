using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using LiteDB;
using InsaitTextEditor.Models;

namespace InsaitTextEditor.Services
{
    /// <summary>
    /// Responsible for saving/restoring session in embedded NoSQL database (LiteDB) with simple migration system.
    /// - Storage format: LiteDB file with 'meta' (schema version) and 'session' (session snapshot) collections.
    /// - File location: Database inside startup directory (AppContext.BaseDirectory), suitable for end user.
    /// - Auto-save: periodically saves session and writes changes to files with known paths.
    /// </summary>
    public sealed class SessionService
    {
        private const int CurrentVersion = 3;
        private const string Header = "# InsaitTextEditor Session DB";
        private const string CipherKey = "InsaitTextEditorKey1"; // simple key for XOR

        private Timer? _autoSaveTimer;
        private readonly Dictionary<string, string> _lastWrittenByPath = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Full path to Database directory in application startup directory (next to .exe).
        /// </summary>
        private static string GetProjectDatabaseDir()
        {
            var overrideRoot = Environment.GetEnvironmentVariable("INSAIT_DATA_ROOT");
            var root = string.IsNullOrWhiteSpace(overrideRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "InsaitTextEditor")
                : overrideRoot;

            var dbDir = Path.Combine(root, "Database");
            Directory.CreateDirectory(dbDir);
            return dbDir;
        }

        private static string GetDbFilePath() => Path.Combine(GetProjectDatabaseDir(), "session.litedb");

        /// <summary>
        /// Gets directory where executable file is located
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
            // Shared connection so multiple timers/threads can read/write safely within process
            var cs = new ConnectionString { Filename = path, Connection = ConnectionType.Shared };
            return new LiteDatabase(cs);
        }

        private static void EnsureMigrations(LiteDatabase db)
        {
            var meta = db.GetCollection<BsonDocument>("meta");
            var schema = meta.FindById("schema");
            if (schema is null)
            {
                schema = new BsonDocument { ["_id"] = "schema", ["version"] = 0 };
                meta.Insert(schema);
            }

            var ver = schema.ContainsKey("version") && schema["version"].IsInt32 ? schema["version"].AsInt32 : 0;
            if (ver < CurrentVersion)
            {
                // Migrations if needed: currently schema is simple, so updating version number is sufficient
                schema["version"] = CurrentVersion;
                meta.Upsert(schema);
            }
        }

        private static void CleanupLegacyDb()
        {
            try
            {
                var candidates = new List<string>();
                // Old text file in startup directory
                candidates.Add(Path.Combine(GetProjectDatabaseDir(), "session.db"));

                // Old text file in project root (logic used to go up 3 levels)
                var dir = GetExecutableDirectory();
                for (int i = 0; i < 3; i++)
                {
                    var parent = Directory.GetParent(dir)?.FullName;
                    if (string.IsNullOrEmpty(parent)) break;
                    dir = parent;
                }
                candidates.Add(Path.Combine(Path.Combine(dir, "Database"), "session.db"));

                foreach (var p in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (File.Exists(p))
                    {
                        try { File.Delete(p); } catch { /* ignore */ }
                    }
                }
            }
            catch { /* ignore */ }
        }

        // ----------------- PUBLIC API -----------------

        /// <summary>
        /// Attempt to restore session. If no data or error occurred — creates one empty tab.
        /// </summary>
        public async Task RestoreOrInitAsync(TabManager tm)
        {
            SessionSnapshot? snap = null;
            try { snap = await TryLoadAsync().ConfigureAwait(false); }
            catch { /* ignore, initialize empty session */ }

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                // Close existing tabs to avoid duplication
                var existing = tm.Tabs.ToList();
                foreach (var vm in existing)
                    tm.CloseTab(vm.Id);

                if (snap is null || snap.Tabs.Count == 0)
                {
                    tm.CreateNewTab();
                    return;
                }

                await RestoreAsync(tm, snap);
            });
        }

        /// <summary>
        /// Safely saves current session (used on application close).
        /// </summary>
        public async Task SaveSafeAsync(TabManager tm)
        {
            try
            {
                var snap = await CaptureOnUiAsync(tm).ConfigureAwait(false);
                await SaveAsync(snap).ConfigureAwait(false);
            }
            catch { /* swallow save errors on exit */ }
        }

        /// <summary>
        /// Starts auto-save with specified interval.
        /// </summary>
        public void StartAutoSave(TabManager tm, TimeSpan interval)
        {
            StopAutoSave();
            _autoSaveTimer = new Timer(async void (_) => await AutoSaveTick(tm).ConfigureAwait(false), null,
                dueTime: interval, period: interval);
        }

        /// <summary>
        /// Stops auto-save.
        /// </summary>
        public void StopAutoSave()
        {
            _autoSaveTimer?.Dispose();
            _autoSaveTimer = null;
        }

        // ----------------- CORE LOGIC -----------------

        private async Task AutoSaveTick(TabManager tm)
        {
            try
            {
                var snap = await CaptureOnUiAsync(tm).ConfigureAwait(false);
                await SaveAsync(snap).ConfigureAwait(false);
                await SaveTabsToFilesIfNeededAsync(tm).ConfigureAwait(false);
            }
            catch
            {
                // Intentionally ignore auto-save errors to avoid crashing during typing
            }
        }

        /// <summary>
        /// Forms session snapshot on UI thread (safely reads ObservableCollection Tabs).
        /// </summary>
        private static async Task<SessionSnapshot> CaptureOnUiAsync(TabManager tm)
        {
            return await Dispatcher.UIThread.InvokeAsync(() => Capture(tm));
        }

        /// <summary>
        /// Take session snapshot: tab order, active index, titles, path and content for "unnamed".
        /// </summary>
        public static SessionSnapshot Capture(TabManager tm)
        {
            var tabs = new List<TabSnapshot>(tm.Tabs.Count);
            var activeIndex = 0;

            for (int i = 0; i < tm.Tabs.Count; i++)
            {
                var vm = tm.Tabs[i];
                if (vm.IsActive) activeIndex = i;

                var id = vm.Id;
                var path = tm.GetFilePath(id);
                var text = tm.GetText(id);

                tabs.Add(new TabSnapshot
                {
                    Title = vm.Title,
                    FilePath = string.IsNullOrWhiteSpace(path) ? null : path,
                    InMemoryText = string.IsNullOrWhiteSpace(path) ? text : null,
                    BackgroundMode = tm.GetBackgroundMode(id)
                });
            }

            return new SessionSnapshot
            {
                Version = CurrentVersion,
                ActiveIndex = activeIndex,
                Tabs = tabs
            };
        }

        /// <summary>
        /// Restore tabs from provided snapshot.
        /// </summary>
        public async Task RestoreAsync(TabManager tm, SessionSnapshot ss)
        {
            // Restore in same order
            for (int i = 0; i < ss.Tabs.Count; i++)
            {
                var t = ss.Tabs[i];
                string? textToUse = null;
                string titleToUse = string.IsNullOrWhiteSpace(t.Title) ? LocalizationService.GetString("Key.NewPage", "New page") : t.Title!;

                if (!string.IsNullOrWhiteSpace(t.FilePath) && File.Exists(t.FilePath))
                {
                    try
                    {
                        textToUse = await File.ReadAllTextAsync(t.FilePath, Encoding.UTF8);
                        titleToUse = Path.GetFileName(t.FilePath);
                    }
                    catch
                    {
                        textToUse = t.InMemoryText ?? string.Empty;
                    }
                }
                else
                {
                    textToUse = t.InMemoryText ?? string.Empty;
                }

                var vm = tm.CreateNewTab(titleToUse, textToUse);
                if (!string.IsNullOrWhiteSpace(t.FilePath))
                    tm.SetFilePath(vm.Id, t.FilePath);
                tm.SetBackgroundMode(vm.Id, t.BackgroundMode);
            }

            var active = Math.Clamp(ss.ActiveIndex, 0, Math.Max(0, tm.Tabs.Count - 1));
            if (tm.Tabs.Count > 0)
                tm.SetActiveTab(tm.Tabs[active].Id);
        }

        /// <summary>
        /// Saves session snapshot to Database/session.db file (in project folder).
        /// Format — simple text for easy viewing in editor; values are masked with XOR+Base64.
        /// </summary>
        public Task SaveAsync(SessionSnapshot ss)
        {
            CleanupLegacyDb();

            using var db = OpenDb();
            EnsureMigrations(db);

            var col = db.GetCollection<BsonDocument>("session");

            var tabs = new BsonArray();
            for (int i = 0; i < ss.Tabs.Count; i++)
            {
                var t = ss.Tabs[i];
                var doc = new BsonDocument
                {
                    ["title"] = t.Title ?? string.Empty,
                    ["filePath"] = t.FilePath ?? string.Empty,
                    ["inMemoryText"] = t.InMemoryText ?? string.Empty,
                    ["backgroundMode"] = (int)t.BackgroundMode
                };
                tabs.Add(doc);
            }

            var sessionDoc = new BsonDocument
            {
                ["_id"] = 1,
                ["version"] = CurrentVersion,
                ["activeIndex"] = ss.ActiveIndex,
                ["tabs"] = tabs
            };

            col.Upsert(1, sessionDoc);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Tries to load session snapshot from LiteDB. Returns null if DB or document is missing/invalid.
        /// </summary>
        public Task<SessionSnapshot?> TryLoadAsync()
        {
            var path = GetDbFilePath();
            if (!File.Exists(path)) return Task.FromResult<SessionSnapshot?>(null);

            try
            {
                using var db = OpenDb();
                EnsureMigrations(db);
                var col = db.GetCollection<BsonDocument>("session");
                var doc = col.FindById(1);
                if (doc is null) return Task.FromResult<SessionSnapshot?>(null);

                int version = doc.ContainsKey("version") && doc["version"].IsInt32 ? doc["version"].AsInt32 : 1;
                int activeIndex = doc.ContainsKey("activeIndex") && doc["activeIndex"].IsInt32 ? doc["activeIndex"].AsInt32 : 0;

                var tabs = new List<TabSnapshot>();
                if (doc.ContainsKey("tabs") && doc["tabs"].IsArray)
                {
                    foreach (var item in doc["tabs"].AsArray)
                    {
                        var d = item.IsDocument ? item.AsDocument : new BsonDocument();
                        var t = new TabSnapshot
                        {
                            Title = d.ContainsKey("title") && d["title"].IsString ? d["title"].AsString : string.Empty,
                            FilePath = ToNullIfEmpty(d.ContainsKey("filePath") && d["filePath"].IsString ? d["filePath"].AsString : string.Empty),
                            InMemoryText = ToNullIfEmpty(d.ContainsKey("inMemoryText") && d["inMemoryText"].IsString ? d["inMemoryText"].AsString : string.Empty),
                            BackgroundMode = d.ContainsKey("backgroundMode") && d["backgroundMode"].IsInt32 ? (PageBackgroundMode)Math.Clamp(d["backgroundMode"].AsInt32, 0, 1) : PageBackgroundMode.Lined
                        };
                        tabs.Add(t);
                    }
                }

                return Task.FromResult<SessionSnapshot?>(new SessionSnapshot
                {
                    Version = version,
                    ActiveIndex = activeIndex,
                    Tabs = tabs
                });
            }
            catch
            {
                return Task.FromResult<SessionSnapshot?>(null);
            }
        }

        private static int TryParseInt(Dictionary<string, string> dict, string key, int def)
            => int.TryParse(dict.TryGetValue(key, out var v) ? v : null, out var i) ? i : def;

        private static string? ToNullIfEmpty(string s) => string.IsNullOrEmpty(s) ? null : s;

        private static string Encode(string plain)
        {
            var bytes = Encoding.UTF8.GetBytes(plain);
            var key = Encoding.UTF8.GetBytes(CipherKey);
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)(bytes[i] ^ key[i % key.Length]);
            return Convert.ToBase64String(bytes);
        }

        private static string Decode(string cipher)
        {
            if (string.IsNullOrEmpty(cipher)) return string.Empty;
            byte[] bytes;
            try { bytes = Convert.FromBase64String(cipher); }
            catch { return string.Empty; }
            var key = Encoding.UTF8.GetBytes(CipherKey);
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = (byte)(bytes[i] ^ key[i % key.Length]);
            return Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// Writes to files tabs with known path only if content differs from last written.
        /// </summary>
        private async Task SaveTabsToFilesIfNeededAsync(TabManager tm)
        {
            // 1) Take snapshot of needed data on UI thread (list of paths and texts)
            var items = await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var list = new List<(string path, string text)>();
                foreach (var vm in tm.Tabs)
                {
                    var path = tm.GetFilePath(vm.Id);
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    var text = tm.GetText(vm.Id) ?? string.Empty;
                    list.Add((path!, text));
                }
                return list;
            });

            // 2) Write to files already outside UI thread
            foreach (var item in items)
            {
                var path = item.path;
                var text = item.text;

                if (_lastWrittenByPath.TryGetValue(path, out var cached) && string.Equals(cached, text, StringComparison.Ordinal))
                    continue; // no changes

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllTextAsync(path, text, Encoding.UTF8).ConfigureAwait(false);
                    _lastWrittenByPath[path] = text;
                }
                catch
                {
                    // ignore file system errors during auto-save
                }
            }
        }
    }

    /// <summary>
    /// Session snapshot (version, active index, list of tabs).
    /// </summary>
    public sealed class SessionSnapshot
    {
        public int Version { get; set; }
        public int ActiveIndex { get; set; }
        public List<TabSnapshot> Tabs { get; set; } = new();
    }

    /// <summary>
    /// Snapshot of a single tab.
    /// </summary>
    public sealed class TabSnapshot
    {
        public string? Title { get; set; }
        public string? FilePath { get; set; }
        public string? InMemoryText { get; set; }
        public PageBackgroundMode BackgroundMode { get; set; } = PageBackgroundMode.Lined;
    }
}
