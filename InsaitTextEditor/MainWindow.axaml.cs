using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using InsaitTextEditor.Controls;
using InsaitTextEditor.Services;
using InsaitTextEditor.Models;
using InsaitTextEditor.Windows;
using InsaitTextEditor.Scripts.WindowsControl;


namespace InsaitTextEditor
{
    public partial class MainWindow
    {
        private readonly TabManager _tabManager;
        private readonly SessionService _sessionService = new();
        private bool _isClosingSafely; // прапорець для безпечного закриття (щоб уникнути рекурсії)

        // ✅ Публічний доступ до TabManager для App та інших компонентів
        public TabManager TabManager => _tabManager;
        
        // ✅ Файл для відкриття після ініціалізації (з аргументів командного рядка)
        private string? _startupFilePath;

        public MainWindow() : this(false, null)
        {
        }

        public MainWindow(bool suppressInit) : this(suppressInit, null)
        {
        }

        /// <summary>
        /// Конструктор з параметром для відкриття файлу при запуску
        /// </summary>
        /// <param name="suppressInit">Не створювати початкову вкладку</param>
        /// <param name="startupFilePath">Шлях до файлу для відкриття (з командного рядка)</param>
public MainWindow(bool suppressInit, string? startupFilePath)
        {
            var suppressInit1 = suppressInit;
            _startupFilePath = startupFilePath;
            InitializeComponent();

            var tabs = this.FindControl<TabsPanel>("TopTabs")
                       ?? throw new InvalidOperationException("TabsPanel not found.");

            _tabManager = new TabManager(tabs);
            DataContext = _tabManager;

            // Removed previous triggers for new-window creation (drag/double-click)

            if (!suppressInit1)
                _tabManager.CreateNewTab();

            // Hotkey: Alt+T opens current tab in a new MainWindow
            this.KeyDown += OnMainWindowKeyDown;
            
            // Subscribe to ResetCaretRequested event
            _tabManager.ResetCaretRequested += (_, _) =>
            {
                var editor = this.FindControl<LinedTextInput>("LinedEditorHost");
                if (editor != null)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        editor.ResetCaret();
                    }, Avalonia.Threading.DispatcherPriority.Background);
                }
            };
            
            // Listen to tab changes to focus editor (without resetting caret position)
            _tabManager.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(TabManager.CurrentText) || 
                    args.PropertyName == nameof(TabManager.ActiveTabId))
                {
                    // Focus the editor when tab changes (without moving caret)
                    var editor = this.FindControl<LinedTextInput>("LinedEditorHost");
                    if (editor != null)
                    {
                        // Focus the editor after a short delay to ensure it's ready
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            editor.Focus();
                        }, Avalonia.Threading.DispatcherPriority.Background);
                    }
                }
            };

            // Відновлення сесії після відкриття вікна та запуск автозбереження
            this.Opened += async (_, _) =>
            {
                // Якщо є файл для відкриття з командного рядка - не відновлюємо сесію
                bool hasStartupFile = !string.IsNullOrWhiteSpace(_startupFilePath) && 
                                      System.IO.File.Exists(_startupFilePath);
                
                if (!suppressInit1 && !hasStartupFile)
                {
                    await _sessionService.RestoreOrInitAsync(_tabManager);
                }
                
                // ✅ Відкрити файл з командного рядка (якщо є)
                if (hasStartupFile)
                {
                    System.Console.WriteLine($"[MainWindow] ✅ Відкриваю файл з командного рядка: {_startupFilePath}");
                    await _tabManager.OpenFileAsync(_startupFilePath!);
                }

                // Для спеціально створеного вікна (Alt+T) не відновлюємо сесію і не створюємо зайві вкладки
                _sessionService.StartAutoSave(_tabManager, TimeSpan.FromSeconds(10));
                
                // Focus editor on startup (without moving caret)
                var editor = this.FindControl<LinedTextInput>("LinedEditorHost");
                if (editor != null)
                {
                    editor.Focus();
                }
            };

            // Збереження сесії під час закриття (очікуємо запис перед виходом)
            this.Closing += async (_, e) =>
            {
                if (_isClosingSafely)
                    return; // вже на фінальній фазі — дозволяємо закриття

                // Скасовуємо перше закриття, щоб мати час на await
                e.Cancel = true;
                try
                {
                    _isClosingSafely = true;
                    _sessionService.StopAutoSave();
                    await _sessionService.SaveSafeAsync(_tabManager);
                }
                finally
                {
                    // Після завершення збереження — закриваємо ще раз (тепер без відміни)
                    Close();
                }
            };
        }

        // Керування вікном
        private void Minimize_Click(object? sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void MaxRestore_Click(object? sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        private async void Close_Click(object? sender, RoutedEventArgs e)
        {
            // Явне збереження перед закриттям з кнопки: зупиняємо автозбереження і чекаємо фінальний запис
            _sessionService.StopAutoSave();
            await _sessionService.SaveSafeAsync(_tabManager);

            // Позначаємо, що це «безпечне» закриття, аби обробник Closing не скасовував його
            _isClosingSafely = true;
            Close();
        }

        // Перетягування за шапку + дабл-клік Max/Restore
        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed)
                return;

            // Ігноруємо кліки по панелі з кнопками
            var buttonsPanel = this.FindControl<StackPanel>("TitleButtonsPanel");
            if (buttonsPanel is not null)
            {
                var pos = e.GetPosition(buttonsPanel);
                if (pos.X >= 0 && pos.X <= buttonsPanel.Bounds.Width &&
                    pos.Y >= 0 && pos.Y <= buttonsPanel.Bounds.Height)
                {
                    return;
                }
            }

            if (e.ClickCount == 2)
            {
                MaxRestore_Click(null, new RoutedEventArgs());
                return;
            }

            if (WindowState != WindowState.Maximized)
                BeginMoveDrag(e);
        }

        // Ресайз без зміни курсорів — просто запускаємо системний drag по потрібному ребру/куту
        private void Resize_Top_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (WindowState != WindowState.Maximized)
                BeginResizeDrag(WindowEdge.North, e);
        }

        private void Resize_Bottom_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (WindowState != WindowState.Maximized)
                BeginResizeDrag(WindowEdge.South, e);
        }

        private void Resize_Left_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (WindowState != WindowState.Maximized)
                BeginResizeDrag(WindowEdge.West, e);
        }

        private void Resize_Right_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (WindowState != WindowState.Maximized)
                BeginResizeDrag(WindowEdge.East, e);
        }

        private void Resize_TopLeft_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (WindowState != WindowState.Maximized)
                BeginResizeDrag(WindowEdge.NorthWest, e);
        }

        private void Resize_TopRight_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (WindowState != WindowState.Maximized)
                BeginResizeDrag(WindowEdge.NorthEast, e);
        }

        private void Resize_BottomLeft_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (WindowState != WindowState.Maximized)
                BeginResizeDrag(WindowEdge.SouthWest, e);
        }

        private void Resize_BottomRight_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (WindowState != WindowState.Maximized)
                BeginResizeDrag(WindowEdge.SouthEast, e);
        }

        private void ModeToggle_Click(object? sender, RoutedEventArgs e)
        {
            var current = _tabManager.CurrentBackgroundMode;
            _tabManager.CurrentBackgroundMode = current == PageBackgroundMode.Lined
                ? PageBackgroundMode.Grid
                : PageBackgroundMode.Lined;
        }

        private void Menu_Click(object? sender, RoutedEventArgs e)
        {
            WindowManager.ShowSingleton<MenuWindow>(this);
        }

        private void Ai_Click(object? sender, RoutedEventArgs e)
        {
            WindowManager.ShowSingleton<ChatWindow>(this);
        }

        private void Complaint_Click(object? sender, RoutedEventArgs e)
        {
            WindowManager.ShowSingleton<ComplaintWindow>(this);
        }

        private void OnMainWindowKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.T || (e.KeyModifiers & KeyModifiers.Alt) != KeyModifiers.Alt) return;
            var id = _tabManager.ActiveTabId;
            if (id is null)
                return;

            var title = _tabManager.GetTitle(id.Value);
            var text = _tabManager.GetText(id.Value);
            var oldPath = _tabManager.GetFilePath(id.Value);
            var oldMode = _tabManager.GetBackgroundMode(id.Value);

            var newWindow = new MainWindow(true);
            var newVm = newWindow._tabManager.CreateNewTab(title, text);
            newWindow._tabManager.SetFilePath(newVm.Id, oldPath);
            newWindow._tabManager.SetBackgroundMode(newVm.Id, oldMode);

            newWindow.Show();

            // Remove the original tab from this window after creating the new one
            _tabManager.CloseTab(id.Value);

            e.Handled = true;
        }

        // Ribbon undo/redo handlers
        public void UndoRibbon_Click(object sender, RoutedEventArgs e)
        {
            // Try to find the main editor host and invoke its UndoAction
            var editor = this.FindControl<InsaitTextEditor.Controls.LinedTextInput>("LinedEditorHost");
            if (editor != null)
            {
                editor.UndoAction();
            }
        }

        public void RedoRibbon_Click(object sender, RoutedEventArgs e)
        {
            var editor = this.FindControl<InsaitTextEditor.Controls.LinedTextInput>("LinedEditorHost");
            if (editor != null)
            {
                editor.RedoAction();
            }
        }

        /// <summary>
        /// Публічний метод для доступу до TabManager (для ChatWindow)
        /// </summary>
        public TabManager GetTabManager() => _tabManager;
    }
}

