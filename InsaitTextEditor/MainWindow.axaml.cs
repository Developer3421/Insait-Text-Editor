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
        private bool _isClosingSafely; // flag for safe closing (to avoid recursion)

        // ✅ Public access to TabManager for App and other components
        public TabManager TabManager => _tabManager;
        
        // ✅ File to open after initialization (from command line arguments)
        private string? _startupFilePath;

        public MainWindow() : this(false, null)
        {
        }

        public MainWindow(bool suppressInit) : this(suppressInit, null)
        {
        }

        /// <summary>
        /// Constructor with parameter to open a file on startup
        /// </summary>
        /// <param name="suppressInit">Do not create initial tab</param>
        /// <param name="startupFilePath">Path to file to open (from command line)</param>
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
                            editor.FocusEditor();
                        }, Avalonia.Threading.DispatcherPriority.Background);
                    }
                }
            };

            // Restore session after window opens and start auto-save
            this.Opened += async (_, _) =>
            {
                // If there is a file to open from command line - do not restore session
                bool hasStartupFile = !string.IsNullOrWhiteSpace(_startupFilePath) && 
                                      System.IO.File.Exists(_startupFilePath);
                
                if (!suppressInit1 && !hasStartupFile)
                {
                    await _sessionService.RestoreOrInitAsync(_tabManager);
                }
                
                // ✅ Open file from command line (if present)
                if (hasStartupFile)
                {
                    System.Console.WriteLine($"[MainWindow] ✅ Відкриваю файл з командного рядка: {_startupFilePath}");
                    await _tabManager.OpenFileAsync(_startupFilePath!);
                }

                // For specially created window (Alt+T) do not restore session and do not create extra tabs
                _sessionService.StartAutoSave(_tabManager, TimeSpan.FromSeconds(10));
                
                // Focus editor on startup (without moving caret)
                var editor = this.FindControl<LinedTextInput>("LinedEditorHost");
                if (editor != null)
                {
                    editor.FocusEditor();
                }
            };

            // Save session during closing (wait for write before exit)
            this.Closing += async (_, e) =>
            {
                if (_isClosingSafely)
                    return; // already in final phase — allow closing

                // Cancel first closing to have time for await
                e.Cancel = true;
                try
                {
                    _isClosingSafely = true;
                    _sessionService.StopAutoSave();
                    await _sessionService.SaveSafeAsync(_tabManager);
                }
                finally
                {
                    // After saving is complete — close again (now without cancellation)
                    Close();
                }
            };

            // Whenever this window becomes active again (e.g., after closing a menu/chat/settings window),
            // restore focus so typing works immediately.
            this.Activated += (_, _) => EnsureEditorFocus();
        }

        private void EnsureEditorFocus()
        {
            var editor = this.FindControl<LinedTextInput>("LinedEditorHost");
            if (editor == null)
                return;

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // If focus got lost to some other control/window, bring it back.
                editor.FocusEditor();
            }, Avalonia.Threading.DispatcherPriority.Background);
        }

        // Window management
        private void Minimize_Click(object? sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void MaxRestore_Click(object? sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        private async void Close_Click(object? sender, RoutedEventArgs e)
        {
            // Explicit save before closing from button: stop auto-save and wait for final write
            _sessionService.StopAutoSave();
            await _sessionService.SaveSafeAsync(_tabManager);

            // Mark this as "safe" closing so the Closing handler doesn't cancel it
            _isClosingSafely = true;
            Close();
        }

        // Drag by header + double-click Max/Restore
        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            var point = e.GetCurrentPoint(this);
            if (!point.Properties.IsLeftButtonPressed)
                return;

            // Ignore clicks on buttons panel
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

        // Resize without changing cursors — just start system drag on the needed edge/corner
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

            // Mode switching can trigger layout changes and focus churn. Restore focus after the UI has settled.
            EnsureEditorFocus();
            Avalonia.Threading.Dispatcher.UIThread.Post(EnsureEditorFocus, Avalonia.Threading.DispatcherPriority.Loaded);
            Avalonia.Threading.Dispatcher.UIThread.Post(EnsureEditorFocus, Avalonia.Threading.DispatcherPriority.Background);
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
        /// Public method to access TabManager (for ChatWindow)
        /// </summary>
        public TabManager GetTabManager() => _tabManager;
    }
}

