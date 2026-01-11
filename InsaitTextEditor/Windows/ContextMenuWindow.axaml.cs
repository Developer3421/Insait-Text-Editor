using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Markup.Xaml;
using InsaitTextEditor.Utils;

namespace InsaitTextEditor.Windows
{
    public partial class ContextMenuWindow : Window
    {
        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        private readonly Window _owner;
        private readonly TextBox? _editor;

        public ContextMenuWindow(Window owner, TextBox? editor)
        {
            InitializeComponent();
            _owner = owner;
            _editor = editor;

            // Close on focus lost like a real context menu
            Deactivated += (_, __) => Close();
        }

        public ContextMenuWindow(Window owner)
        {
            InitializeComponent();
            _owner = owner;
            Deactivated += (_, __) => Close();
        }

        private TextBox? ResolveTargetEditor()
        {
            if (_editor is not null)
                return _editor;

            var top = TopLevel.GetTopLevel(this) ?? _owner;
            var focused = top?.FocusManager?.GetFocusedElement();

            if (focused is TextBox tb)
                return tb;

            if (focused is Control c)
            {
                // Try to find TextBox in ancestors or descendants of the focused control
                var fromAncestor = c.FindAncestorOfType<TextBox>();
                if (fromAncestor is not null)
                    return fromAncestor;

                var fromDesc = c.FindDescendantOfType<TextBox>();
                if (fromDesc is not null)
                    return fromDesc;
            }

            if (_owner is Control oc)
            {
                var inOwner = oc.FindDescendantOfType<TextBox>();
                if (inOwner is not null)
                    return inOwner;
            }

            return null;
        }

        private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }

        private void Minimize_Click(object? sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void Close_Click(object? sender, RoutedEventArgs e)
            => Close();

        private async void Cut_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var tb = ResolveTargetEditor();
                if (tb is null) return;

                var sel = tb.SelectedText ?? string.Empty;
                var clipboard = _owner?.Clipboard ?? TopLevel.GetTopLevel(tb)?.Clipboard;
                if (!string.IsNullOrEmpty(sel) && clipboard is not null)
                {
                    await clipboard.SetTextAsync(sel);
                }
                // Remove selection
                tb.SelectedText = string.Empty;
                tb.Focus();
            }
            finally
            {
                Close();
            }
        }

        private async void Copy_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var tb = ResolveTargetEditor();
                if (tb is null) return;

                var sel = tb.SelectedText ?? string.Empty;
                var clipboard = _owner?.Clipboard ?? TopLevel.GetTopLevel(tb)?.Clipboard;
                if (!string.IsNullOrEmpty(sel) && clipboard is not null)
                {
                    await clipboard.SetTextAsync(sel);
                }
            }
            finally
            {
                Close();
            }
        }

        private async void Paste_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var tb = ResolveTargetEditor();
                if (tb is null) return;

                var clipboard = _owner?.Clipboard ?? TopLevel.GetTopLevel(tb)?.Clipboard;
                if (clipboard is not null)
                {
                    var text = await ClipboardCompat.TryGetTextAsync((Avalonia.Input.Platform.IClipboard)clipboard);
                    if (!string.IsNullOrEmpty(text))
                    {
                        // Replace selection with paste
                        tb.SelectedText = text;
                        tb.Focus();
                    }
                }
            }
            finally
            {
                Close();
            }
        }

        private void SelectAll_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var tb = ResolveTargetEditor();
                if (tb is null) return;
                tb.SelectAll();
                tb.Focus();
            }
            finally
            {
                Close();
            }
        }

        private void Undo_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var tb = ResolveTargetEditor();
                if (tb is null) return;
                tb.Undo();
                tb.Focus();
            }
            finally
            {
                Close();
            }
        }

        private void Redo_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var tb = ResolveTargetEditor();
                if (tb is null) return;
                tb.Redo();
                tb.Focus();
            }
            finally
            {
                Close();
            }
        }

        private void Rewrite_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var tb = ResolveTargetEditor();
                if (tb is null) return;

                var sel = tb.SelectedText;
                if (string.IsNullOrWhiteSpace(sel))
                    sel = tb.Text ?? string.Empty;

                var prompt = "Перепиши цей текст, зберігаючи зміст і стиль, але зроби його плавнішим і читабельнішим:\n\n" + sel;

                OpenChatAndSend(prompt);
            }
            finally
            {
                Close();
            }
        }

        private void Summarize_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                var tb = ResolveTargetEditor();
                if (tb is null) return;

                var sel = tb.SelectedText;
                if (string.IsNullOrWhiteSpace(sel))
                    sel = tb.Text ?? string.Empty;

                var prompt = "Summarise this text briefly:\n\n" + sel;

                OpenChatAndSend(prompt);
            }
            finally
            {
                Close();
            }
        }

        private void OpenChatAndSend(string prompt)
        {
            var chat = new ChatWindow();
            // After opening — paste text and press Send
            chat.Opened += (_, __) =>
            {
                var input = chat.FindControl<TextBox>("InputTextBox");
                var send = chat.FindControl<Button>("SendButton");
                if (input is not null)
                    input.Text = prompt;
                if (send is not null)
                {
                    // Trigger "Send" click
                    send.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            };
            chat.Show(_owner);
        }
    }
}
