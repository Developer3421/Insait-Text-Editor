using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using InsaitTextEditor.Controls;
using InsaitTextEditor.Models;
using InsaitTextEditor.ViewModels;
using InsaitTextEditor.Services;

namespace InsaitTextEditor.Services;

public class TabManager : INotifyPropertyChanged
{
    private readonly TabsPanel _tabsPanel;
    private readonly DatabaseService _databaseService;

    private readonly ObservableCollection<DocumentTabViewModel> _tabs = new();
    public ReadOnlyObservableCollection<DocumentTabViewModel> Tabs { get; }

    private readonly Dictionary<Guid, Workspace> _workspaces = new();
    private readonly Dictionary<Guid, DocumentTabViewModel> _tabIndex = new();
    private readonly Dictionary<Guid, DocumentTab> _tabViews = new();

    private readonly Dictionary<Guid, EventHandler<RoutedEventArgs>> _closeHandlers = new();
    private readonly Dictionary<Guid, EventHandler<PointerPressedEventArgs>> _activateHandlers = new();

    // Шлях файлу, прив’язаний до вкладки (null, якщо документ ще не збережено)
    private readonly Dictionary<Guid, string?> _filePaths = new();

    private Guid? _activeTabId;

    public Guid? ActiveTabId
    {
        get => _activeTabId;
        private set
        {
            if (_activeTabId != value)
            {
                _activeTabId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentText));
                OnPropertyChanged(nameof(CurrentBackgroundMode));
                OnPropertyChanged(nameof(CurrentLineBrush));
                OnPropertyChanged(nameof(CurrentTextBrush));
                OnPropertyChanged(nameof(CurrentFontSize));
                OnPropertyChanged(nameof(CurrentBold));
                OnPropertyChanged(nameof(CurrentItalic));
            }
        }
    }

    public string CurrentText
    {
        get
        {
            if (_activeTabId.HasValue && _workspaces.TryGetValue(_activeTabId.Value, out var ws))
                return ws.Text;
            return string.Empty;
        }
        set
        {
            if (!_activeTabId.HasValue) return;
            if (_workspaces.TryGetValue(_activeTabId.Value, out var ws))
            {
                if (ws.Text != value)
                {
                    ws.Text = value;
                    if (_tabIndex.TryGetValue(_activeTabId.Value, out var vm))
                        vm.DocumentText = value;
                    OnPropertyChanged();
                }
            }
        }
    }

    // Поточний режим фону сторінки для активної вкладки
    public PageBackgroundMode CurrentBackgroundMode
    {
        get
        {
            if (_activeTabId.HasValue && _workspaces.TryGetValue(_activeTabId.Value, out var ws))
                return ws.BackgroundMode;
            return PageBackgroundMode.Lined;
        }
        set
        {
            if (!_activeTabId.HasValue) return;
            if (_workspaces.TryGetValue(_activeTabId.Value, out var ws))
            {
                if (ws.BackgroundMode != value)
                {
                    ws.BackgroundMode = value;
                    OnPropertyChanged();
                }
            }
        }
    }

    // ===== Current style properties (per active tab) =====
    private static IBrush BrushFromHex(string? hex, string fallback)
    {
        try
        {
            var use = string.IsNullOrWhiteSpace(hex) ? fallback : hex!;
            return new SolidColorBrush(Color.Parse(use));
        }
        catch
        {
            return new SolidColorBrush(Color.Parse(fallback));
        }
    }

    public IBrush CurrentLineBrush
    {
        get
        {
            if (_activeTabId.HasValue && _workspaces.TryGetValue(_activeTabId.Value, out var ws))
                return BrushFromHex(ws.LineColorHex, "#FFB0B0B0");
            return BrushFromHex(null, "#FFB0B0B0");
        }
    }

    public IBrush CurrentTextBrush
    {
        get
        {
            if (_activeTabId.HasValue && _workspaces.TryGetValue(_activeTabId.Value, out var ws))
                return BrushFromHex(ws.TextColorHex, "#FF000000");
            return BrushFromHex(null, "#FF000000");
        }
    }

    public double CurrentFontSize
    {
        get
        {
            if (_activeTabId.HasValue && _workspaces.TryGetValue(_activeTabId.Value, out var ws))
                return ws.FontSize;
            return 16d;
        }
        set
        {
            if (!_activeTabId.HasValue) return;
            if (_workspaces.TryGetValue(_activeTabId.Value, out var ws))
            {
                var v = Math.Clamp(value, 8d, 96d);
                if (Math.Abs(ws.FontSize - v) > 0.0001)
                {
                    ws.FontSize = v;
                    OnPropertyChanged();
                }
            }
        }
    }

    public bool CurrentBold
    {
        get
        {
            if (_activeTabId.HasValue && _workspaces.TryGetValue(_activeTabId.Value, out var ws))
                return ws.Bold;
            return false;
        }
        set
        {
            if (!_activeTabId.HasValue) return;
            if (_workspaces.TryGetValue(_activeTabId.Value, out var ws))
            {
                if (ws.Bold != value)
                {
                    ws.Bold = value;
                    OnPropertyChanged();
                }
            }
        }
    }

    public bool CurrentItalic
    {
        get
        {
            if (_activeTabId.HasValue && _workspaces.TryGetValue(_activeTabId.Value, out var ws))
                return ws.Italic;
            return false;
        }
        set
        {
            if (!_activeTabId.HasValue) return;
            if (_workspaces.TryGetValue(_activeTabId.Value, out var ws))
            {
                if (ws.Italic != value)
                {
                    ws.Italic = value;
                    OnPropertyChanged();
                }
            }
        }
    }

    public EditorSettings GetActiveSettings()
    {
        if (_activeTabId.HasValue && _workspaces.TryGetValue(_activeTabId.Value, out var ws))
        {
            return new EditorSettings
            {
                LineColorHex = ws.LineColorHex,
                TextColorHex = ws.TextColorHex,
                FontSize = ws.FontSize,
                Bold = ws.Bold,
                Italic = ws.Italic
            };
        }
        return SettingsService.LoadDefaults();
    }

    public void ApplySettingsToActive(EditorSettings s)
    {
        if (!_activeTabId.HasValue) return;
        if (_workspaces.TryGetValue(_activeTabId.Value, out var ws))
        {
            ws.LineColorHex = s.LineColorHex ?? ws.LineColorHex;
            ws.TextColorHex = s.TextColorHex ?? ws.TextColorHex;
            ws.FontSize = Math.Clamp(s.FontSize, 8d, 96d);
            ws.Bold = s.Bold;
            ws.Italic = s.Italic;

            OnPropertyChanged(nameof(CurrentLineBrush));
            OnPropertyChanged(nameof(CurrentTextBrush));
            OnPropertyChanged(nameof(CurrentFontSize));
            OnPropertyChanged(nameof(CurrentBold));
            OnPropertyChanged(nameof(CurrentItalic));
        }
    }

    public void ApplySettingsToAll(EditorSettings s)
    {
        foreach (var ws in _workspaces.Values)
        {
            ws.LineColorHex = s.LineColorHex ?? ws.LineColorHex;
            ws.TextColorHex = s.TextColorHex ?? ws.TextColorHex;
            ws.FontSize = Math.Clamp(s.FontSize, 8d, 96d);
            ws.Bold = s.Bold;
            ws.Italic = s.Italic;
        }

        // Оновити активні прив’язки
        OnPropertyChanged(nameof(CurrentLineBrush));
        OnPropertyChanged(nameof(CurrentTextBrush));
        OnPropertyChanged(nameof(CurrentFontSize));
        OnPropertyChanged(nameof(CurrentBold));
        OnPropertyChanged(nameof(CurrentItalic));
    }

    public PageBackgroundMode GetBackgroundMode(Guid id)
    {
        return _workspaces.TryGetValue(id, out var ws) ? ws.BackgroundMode : PageBackgroundMode.Lined;
    }

    public void SetBackgroundMode(Guid id, PageBackgroundMode mode)
    {
        if (_workspaces.TryGetValue(id, out var ws))
        {
            if (ws.BackgroundMode != mode)
            {
                ws.BackgroundMode = mode;
                if (_activeTabId.HasValue && _activeTabId.Value == id)
                    OnPropertyChanged(nameof(CurrentBackgroundMode));
            }
        }
    }

    public event EventHandler<DocumentTabViewModel>? TabAdded;
    public event EventHandler<DocumentTabViewModel>? TabRemoved;

    public TabManager(TabsPanel tabsPanel)
    {
        _tabsPanel = tabsPanel ?? throw new ArgumentNullException(nameof(tabsPanel));
        _databaseService = new DatabaseService();
        Tabs = new ReadOnlyObservableCollection<DocumentTabViewModel>(_tabs);

        _tabsPanel.AddTabRequested += (_, __) => CreateNewTab();
    }

    public DocumentTabViewModel CreateNewTab(string? title = null, string? initialText = null)
    {
        var id = Guid.NewGuid();
        var ws = new Workspace(id, initialText ?? string.Empty);

        // Apply persisted defaults for editor styling
        try
        {
            var def = SettingsService.LoadDefaults();
            ws.LineColorHex = def.LineColorHex;
            ws.TextColorHex = def.TextColorHex;
            ws.FontSize = def.FontSize;
            ws.Bold = def.Bold;
            ws.Italic = def.Italic;
        }
        catch { /* ignore */ }

        _workspaces.Add(id, ws);

        var vm = new DocumentTabViewModel
        {
            Id = id,
            Title = title ?? LocalizationService.GetString("Key.NewPage", "New page"),
            Icon = TryLoadAppIcon(),
            TabBackground = BuildTabBackground(),
            DocumentText = ws.Text
        };

        var view = new DocumentTab
        {
            Height = _tabsPanel.TabHeight,
            DataContext = vm
        };

        EventHandler<RoutedEventArgs> closeHandler = (_, __) => CloseTab(id);
        view.CloseRequested += closeHandler;
        _closeHandlers[id] = closeHandler;

        EventHandler<PointerPressedEventArgs> activateHandler = (_, __) => SetActiveTab(id);
        view.PointerPressed += activateHandler;
        _activateHandlers[id] = activateHandler;

        _tabIndex[id] = vm;
        _tabViews[id] = view;

        _tabs.Add(vm);
        _tabsPanel.AddTab(view);

        // новий документ ще без шляху
        _filePaths[id] = null;

        SetActiveTab(id);
        TabAdded?.Invoke(this, vm);
        
        // Save initial state to DB
        _databaseService.SaveDocumentAsync(id, ws.Text);
        
        return vm;
    }

    public void RemoveTab(DocumentTab view)
    {
        if (view?.DataContext is DocumentTabViewModel vm)
            CloseTab(vm.Id);
    }

    public bool CloseTab(Guid id)
    {
        if (_tabViews.TryGetValue(id, out var view))
        {
            if (_closeHandlers.TryGetValue(id, out var ch))
            {
                view.CloseRequested -= ch;
                _closeHandlers.Remove(id);
            }
            if (_activateHandlers.TryGetValue(id, out var ah))
            {
                view.PointerPressed -= ah;
                _activateHandlers.Remove(id);
            }

            _tabsPanel.RemoveTab(view);
            view.DataContext = null;
            _tabViews.Remove(id);
        }

        if (_tabIndex.TryGetValue(id, out var vm))
        {
            _tabs.Remove(vm);
            _tabIndex.Remove(id);
            TabRemoved?.Invoke(this, vm);
        }

        if (_workspaces.TryGetValue(id, out var ws))
        {
            ws.Dispose();
            _workspaces.Remove(id);
        }

        _filePaths.Remove(id);

        if (ActiveTabId == id)
        {
            if (_tabs.Count > 0)
                SetActiveTab(_tabs[^1].Id);
            else
                ActiveTabId = null;
        }

        OnPropertyChanged(nameof(CurrentText));
        return true;
    }

    // Активувати вкладку
    public void SetActiveTab(Guid id)
    {
        if (!_tabIndex.ContainsKey(id))
            return;

        ActiveTabId = id;

        foreach (var vm in _tabs)
            vm.IsActive = vm.Id == id;

        if (_workspaces.TryGetValue(id, out var ws) && _tabIndex.TryGetValue(id, out var activeVm))
        {
            // Load from DB if needed, or use current state
            _databaseService.LoadDocumentAsync(id).ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully && task.Result is not null)
                {
                    ws.Text = task.Result;
                    activeVm.DocumentText = task.Result;
                    OnPropertyChanged(nameof(CurrentText));
                }
            }, TaskScheduler.FromCurrentSynchronizationContext());
        }

        OnPropertyChanged(nameof(CurrentText));
    }

    public string GetCurrentText() => CurrentText;

    // --------------------------
    // ФАЙЛОВІ ОПЕРАЦІЇ: OPEN / SAVE / SAVE AS
    // --------------------------

    /// <summary>
    /// Відкрити файл за шляхом (для SaveToFileTool)
    /// </summary>
    public async Task OpenFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        try
        {
            var text = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            
            // Створити нову вкладку
            CreateNewTab();

            if (ActiveTabId is null)
                return;

            var id = ActiveTabId.Value;

            if (_workspaces.TryGetValue(id, out var ws))
            {
                ws.Text = text;
                await _databaseService.SaveDocumentAsync(id, text);
            }

            var fileName = Path.GetFileName(filePath);
            if (_tabIndex.TryGetValue(id, out var vm))
            {
                vm.DocumentText = text;
                vm.Title = fileName;
            }

            _filePaths[id] = filePath;
            OnPropertyChanged(nameof(CurrentText));
        }
        catch
        {
            // TODO: опційно показати повідомлення користувачу
        }
    }

    public async Task OpenTextFileAsync(Window owner)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("Text files") { Patterns = new[] { "*.txt", "*.log", "*.md" } },
                new FilePickerFileType("All files") { Patterns = new[] { "*.*" } }
            }
        });

        var file = files is { Count: > 0 } ? files[0] : null;
        if (file is null)
            return;

        try
        {
            using var s = await file.OpenReadAsync();
            using var sr = new StreamReader(s, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = await sr.ReadToEndAsync();

            if (ActiveTabId is null)
                CreateNewTab();

            if (ActiveTabId is null)
                return;

            var id = ActiveTabId.Value;

            if (_workspaces.TryGetValue(id, out var ws))
            {
                ws.Text = text;
                _databaseService.SaveDocumentAsync(id, text); // Save to DB
            }

            var displayName = file.Name;
            if (_tabIndex.TryGetValue(id, out var vm))
            {
                vm.DocumentText = text;
                vm.Title = string.IsNullOrWhiteSpace(displayName) ? vm.Title : displayName;
            }

            var localPath = file.TryGetLocalPath();
            _filePaths[id] = string.IsNullOrWhiteSpace(localPath) ? null : localPath;
            OnPropertyChanged(nameof(CurrentText));
        }
        catch
        {
            // TODO: опційно показати повідомлення користувачу
        }
    }

    public async Task SaveCurrentToFileAsync(Window owner, bool saveAs)
    {
        if (ActiveTabId is null)
            return;

        var id = ActiveTabId.Value;
        var text = GetCurrentText();

        var hasPath = _filePaths.TryGetValue(id, out var existingPath) && !string.IsNullOrWhiteSpace(existingPath);
        IStorageFile? fileToSave = null;

        if (saveAs || !hasPath)
        {
            var suggestedName = "Document.txt";
            if (_tabIndex.TryGetValue(id, out var vm) && !string.IsNullOrWhiteSpace(vm.Title))
            {
                var baseName = vm.Title.Trim();
                if (!baseName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                    baseName += ".txt";
                suggestedName = baseName;
            }

            fileToSave = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = suggestedName,
                DefaultExtension = "txt",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new FilePickerFileType("Text files") { Patterns = new[] { "*.txt" } },
                    new FilePickerFileType("All files") { Patterns = new[] { "*.*" } }
                }
            });

            if (fileToSave is null)
                return;
        }

        try
        {
            if (fileToSave is not null)
            {
                await using var s = await fileToSave.OpenWriteAsync();
                await using var sw = new StreamWriter(s, new UTF8Encoding(false));
                await sw.WriteAsync(text);
                await sw.FlushAsync();

                var localPath = fileToSave.TryGetLocalPath();
                _filePaths[id] = string.IsNullOrWhiteSpace(localPath) ? null : localPath;
                if (_tabIndex.TryGetValue(id, out var vm))
                    vm.Title = fileToSave.Name;
            }
            else
            {
                // Save to existing path
                await File.WriteAllTextAsync(existingPath!, text, Encoding.UTF8);
                _filePaths[id] = existingPath!;
                if (_tabIndex.TryGetValue(id, out var vm))
                    vm.Title = Path.GetFileName(existingPath!);
            }
            
            // Also save to DB
            await _databaseService.SaveDocumentAsync(id, text);
        }
        catch
        {
            // TODO: опційно показати повідомлення користувачу
        }
    }

    // --------------------------

    private static IBrush BuildTabBackground()
    {
        IBrush baseBrush;
        if (Application.Current?.Resources.TryGetResource("AppDarkOrangeBrush",
                Application.Current.ActualThemeVariant, out var res) == true && res is IBrush b)
        {
            baseBrush = b;
        }
        else
        {
            baseBrush = new SolidColorBrush(Color.Parse("#CC5500"));
        }

        return Lighten(baseBrush, 0.15);
    }

    private static IImage? TryLoadAppIcon()
    {
        try
        {
            var uri = new Uri("avares://InsaitTextEditor/Icons/AppIcon.png");
            using var s = AssetLoader.Open(uri);
            return new Bitmap(s);
        }
        catch
        {
            return null;
        }
    }

    private static IBrush Lighten(IBrush brush, double amount)
    {
        if (brush is SolidColorBrush sb)
        {
            var c = sb.Color;
            byte L(byte v) => (byte)(v + (255 - v) * amount);
            return new SolidColorBrush(Color.FromArgb(c.A, L(c.R), L(c.G), L(c.B)));
        }
        return brush;
    }

    // --------------------------
    // ПУБЛІЧНІ СЛУЖБОВІ МЕТОДИ ДЛЯ СЕСІЙ/ЗОВНІШНІХ СЕРВІСІВ
    //   Мінімальні гетери/сетери без зміни внутрішньої логіки менеджера вкладок.
    // --------------------------

    /// <summary>
    /// Отримати шлях файлу, прив’язаний до вкладки (або null, якщо документ ще не збережено).
    /// </summary>
    public string? GetFilePath(Guid id)
        => _filePaths.TryGetValue(id, out var p) ? p : null;

    /// <summary>
    /// Встановити/очистити шлях файлу, прив’язаний до вкладки. Заголовок вкладки тут свідомо не змінюємо —
    /// цим займаються інші методи (Open/Save). Цей метод призначено для відновлення сесії.
    /// </summary>
    public void SetFilePath(Guid id, string? path)
        => _filePaths[id] = path;

    /// <summary>
    /// Отримати поточний текст документа для вкладки за її ідентифікатором.
    /// Повертає порожній рядок, якщо ідентифікатор невідомий.
    /// </summary>
    public string GetText(Guid id)
        => _workspaces.TryGetValue(id, out var ws) ? ws.Text : string.Empty;

    public string GetTitle(Guid id)
        => _tabIndex.TryGetValue(id, out var vm) ? vm.Title : LocalizationService.GetString("Key.NewPage", "New page");

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

