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
    /// Відповідає за збереження/відновлення сесії у вбудованій NoSQL-базі даних (LiteDB) з простою системою міграцій.
    /// - Формат зберігання: LiteDB файл із колекціями 'meta' (версія схеми) та 'session' (знімок сесії).
    /// - Розташування файлу: Database всередині каталогу запуску (AppContext.BaseDirectory), придатно для кінцевого користувача.
    /// - Автозбереження: періодично зберігає сесію та записує зміни у файли з відомими шляхами.
    /// </summary>
    public sealed class SessionService
    {
        private const int CurrentVersion = 3;
        private const string Header = "# InsaitTextEditor Session DB";
        private const string CipherKey = "InsaitTextEditorKey1"; // простий ключ для XOR

        private Timer? _autoSaveTimer;
        private readonly Dictionary<string, string> _lastWrittenByPath = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Повний шлях до каталогу Database у каталозі запуску застосунку (поруч із .exe).
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
                // Міграції за потреби: наразі схема проста, тож достатньо оновити номер версії
                schema["version"] = CurrentVersion;
                meta.Upsert(schema);
            }
        }

        private static void CleanupLegacyDb()
        {
            try
            {
                var candidates = new List<string>();
                // Старий текстовий файл у каталозі запуску
                candidates.Add(Path.Combine(GetProjectDatabaseDir(), "session.db"));

                // Старий текстовий файл у корені проєкту (логіка раніше піднімалася на 3 рівні)
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

        // ----------------- ПУБЛІЧНИЙ API -----------------

        /// <summary>
        /// Спроба відновити сесію. Якщо даних немає або сталася помилка — створює одну порожню вкладку.
        /// </summary>
        public async Task RestoreOrInitAsync(TabManager tm)
        {
            SessionSnapshot? snap = null;
            try { snap = await TryLoadAsync().ConfigureAwait(false); }
            catch { /* ігноруємо, ініціалізуємо порожню сесію */ }

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                // Закриваємо наявні вкладки, щоби уникнути дублювання
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
        /// Безпечно зберігає поточну сесію (використовується на закритті додатку).
        /// </summary>
        public async Task SaveSafeAsync(TabManager tm)
        {
            try
            {
                var snap = await CaptureOnUiAsync(tm).ConfigureAwait(false);
                await SaveAsync(snap).ConfigureAwait(false);
            }
            catch { /* ковтаємо помилки збереження на виході */ }
        }

        /// <summary>
        /// Запускає автозбереження з указаним інтервалом.
        /// </summary>
        public void StartAutoSave(TabManager tm, TimeSpan interval)
        {
            StopAutoSave();
            _autoSaveTimer = new Timer(async void (_) => await AutoSaveTick(tm).ConfigureAwait(false), null,
                dueTime: interval, period: interval);
        }

        /// <summary>
        /// Зупиняє автозбереження.
        /// </summary>
        public void StopAutoSave()
        {
            _autoSaveTimer?.Dispose();
            _autoSaveTimer = null;
        }

        // ----------------- ОСНОВНА ЛОГІКА -----------------

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
                // Навмисно ігноруємо помилки автозбереження, щоб не падати під час набору тексту
            }
        }

        /// <summary>
        /// Формує знімок сесії на UI-потоці (безпечно читає ObservableCollection Tabs).
        /// </summary>
        private static async Task<SessionSnapshot> CaptureOnUiAsync(TabManager tm)
        {
            return await Dispatcher.UIThread.InvokeAsync(() => Capture(tm));
        }

        /// <summary>
        /// Зняти знімок сесії: порядок вкладок, активний індекс, заголовки, шлях і вміст для «безіменних».
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
        /// Відновити вкладки з наданого знімка.
        /// </summary>
        public async Task RestoreAsync(TabManager tm, SessionSnapshot ss)
        {
            // Відновлюємо у тому ж порядку
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
        /// Зберігає знімок сесії у файл Database/session.db (у проєктній папці).
        /// Формат — простий текст для зручного перегляду у редакторі; значення маскуються XOR+Base64.
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
        /// Пробує завантажити знімок сесії з LiteDB. Повертає null, якщо БД або документ відсутні/некоректні.
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
        /// Записує у файли вкладки з відомим шляхом лише якщо вміст відрізняється від останнього записаного.
        /// </summary>
        private async Task SaveTabsToFilesIfNeededAsync(TabManager tm)
        {
            // 1) Зняти знімок потрібних даних на UI-потоці (список шляхів і текстів)
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

            // 2) Писати у файли вже поза UI-потоком
            foreach (var item in items)
            {
                var path = item.path;
                var text = item.text;

                if (_lastWrittenByPath.TryGetValue(path, out var cached) && string.Equals(cached, text, StringComparison.Ordinal))
                    continue; // немає змін

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllTextAsync(path, text, Encoding.UTF8).ConfigureAwait(false);
                    _lastWrittenByPath[path] = text;
                }
                catch
                {
                    // ігноруємо помилки файлової системи під час автозбереження
                }
            }
        }
    }

    /// <summary>
    /// Знімок усієї сесії (версія, активний індекс, список вкладок).
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
