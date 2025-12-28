// Controls/LinedTextInput.axaml.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using InsaitTextEditor.Scripts.SkiaSharp;
using InsaitTextEditor.Models;
using InsaitTextEditor.Utils;
using Avalonia.Threading;

namespace InsaitTextEditor.Controls;

public partial class LinedTextInput : UserControl
{
    // Property definitions
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<LinedTextInput, string?>(nameof(Text), string.Empty);

    public static readonly StyledProperty<double> EditorFontSizeProperty =
        AvaloniaProperty.Register<LinedTextInput, double>(nameof(EditorFontSize), 18d);

    public static readonly StyledProperty<int> SelectionStartProperty =
        AvaloniaProperty.Register<LinedTextInput, int>(nameof(SelectionStart), 0);

    public static readonly StyledProperty<int> SelectionEndProperty =
        AvaloniaProperty.Register<LinedTextInput, int>(nameof(SelectionEnd), 0);

    public static readonly StyledProperty<IBrush> TextBrushProperty =
        AvaloniaProperty.Register<LinedTextInput, IBrush>(nameof(TextBrush), Brushes.Black);

    public static readonly StyledProperty<IBrush> PaperBrushProperty =
        AvaloniaProperty.Register<LinedTextInput, IBrush>(nameof(PaperBrush), Brushes.White);

    public static readonly StyledProperty<IBrush> LineBrushProperty =
        AvaloniaProperty.Register<LinedTextInput, IBrush>(nameof(LineBrush), new SolidColorBrush(Color.FromRgb(0, 0, 0)));

    public static readonly StyledProperty<double> LineSpacingProperty =
        AvaloniaProperty.Register<LinedTextInput, double>(nameof(LineSpacing), 22d);

    public static readonly StyledProperty<double> LineThicknessProperty =
        AvaloniaProperty.Register<LinedTextInput, double>(nameof(LineThickness), 2.5d);

    public static readonly StyledProperty<double> LeftMarginProperty =
        AvaloniaProperty.Register<LinedTextInput, double>(nameof(LeftMargin), 60d);

    public static readonly StyledProperty<double> RightMarginProperty =
        AvaloniaProperty.Register<LinedTextInput, double>(nameof(RightMargin), 20d);

    public static readonly StyledProperty<double> TopMarginProperty =
        AvaloniaProperty.Register<LinedTextInput, double>(nameof(TopMargin), 20d);

    public static readonly StyledProperty<double> BottomMarginProperty =
        AvaloniaProperty.Register<LinedTextInput, double>(nameof(BottomMargin), 20d);

    public static readonly StyledProperty<double> FirstLineOffsetProperty =
        AvaloniaProperty.Register<LinedTextInput, double>(nameof(FirstLineOffset), 16d);

    public static readonly StyledProperty<Thickness> TextPaddingProperty =
        AvaloniaProperty.Register<LinedTextInput, Thickness>(nameof(TextPadding), new Thickness(61, 0, 20, 0));

    public static readonly StyledProperty<bool> BoldDefaultProperty =
        AvaloniaProperty.Register<LinedTextInput, bool>(nameof(BoldDefault), false);

    public static readonly StyledProperty<bool> ItalicDefaultProperty =
        AvaloniaProperty.Register<LinedTextInput, bool>(nameof(ItalicDefault), false);

    public static readonly StyledProperty<bool> DrawVerticalMarginLineProperty =
        AvaloniaProperty.Register<LinedTextInput, bool>(nameof(DrawVerticalMarginLine), true);

    public static readonly StyledProperty<IBrush> VerticalMarginLineBrushProperty =
        AvaloniaProperty.Register<LinedTextInput, IBrush>(nameof(VerticalMarginLineBrush), Brushes.Red);

    public static readonly StyledProperty<PageBackgroundMode> BackgroundModeProperty =
        AvaloniaProperty.Register<LinedTextInput, PageBackgroundMode>(nameof(BackgroundMode), PageBackgroundMode.Lined);

    // Property accessors
    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double EditorFontSize { get => GetValue(EditorFontSizeProperty); set => SetValue(EditorFontSizeProperty, value); }
    public int SelectionStart { get => GetValue(SelectionStartProperty); set => SetValue(SelectionStartProperty, value); }
    public int SelectionEnd { get => GetValue(SelectionEndProperty); set => SetValue(SelectionEndProperty, value); }
    public IBrush TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    public IBrush PaperBrush { get => GetValue(PaperBrushProperty); set => SetValue(PaperBrushProperty, value); }
    public IBrush LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public double LineSpacing { get => GetValue(LineSpacingProperty); set => SetValue(LineSpacingProperty, value); }
    public double LineThickness { get => GetValue(LineThicknessProperty); set => SetValue(LineThicknessProperty, value); }
    public double LeftMargin { get => GetValue(LeftMarginProperty); set => SetValue(LeftMarginProperty, value); }
    public double RightMargin { get => GetValue(RightMarginProperty); set => SetValue(RightMarginProperty, value); }
    public double TopMargin { get => GetValue(TopMarginProperty); set => SetValue(TopMarginProperty, value); }
    public double BottomMargin { get => GetValue(BottomMarginProperty); set => SetValue(BottomMarginProperty, value); }
    public double FirstLineOffset { get => GetValue(FirstLineOffsetProperty); set => SetValue(FirstLineOffsetProperty, value); }
    public Thickness TextPadding { get => GetValue(TextPaddingProperty); set => SetValue(TextPaddingProperty, value); }
    public bool BoldDefault { get => GetValue(BoldDefaultProperty); set => SetValue(BoldDefaultProperty, value); }
    public bool ItalicDefault { get => GetValue(ItalicDefaultProperty); set => SetValue(ItalicDefaultProperty, value); }
    public bool DrawVerticalMarginLine { get => GetValue(DrawVerticalMarginLineProperty); set => SetValue(DrawVerticalMarginLineProperty, value); }
    public IBrush VerticalMarginLineBrush { get => GetValue(VerticalMarginLineBrushProperty); set => SetValue(VerticalMarginLineBrushProperty, value); }
    public PageBackgroundMode BackgroundMode { get => GetValue(BackgroundModeProperty); set => SetValue(BackgroundModeProperty, value); }

    private RichTextOverlay? _overlay;
    private ScrollViewer? _scroll;
    private bool _isPointerPressed;
    private bool _hasLoadedFile; // Flag to track if file was loaded

    // Undo/Redo stacks
    private Stack<string> _undoStack = new();
    private Stack<string> _redoStack = new();
    private string _lastSavedText = string.Empty;

    private const string InternalNewLine = "\n";

    // "Free indent" settings (soft tabs)
    private const int IndentSize = 4;
    private static readonly string IndentString = new(' ', IndentSize);

    public LinedTextInput()
    {
        InitializeComponent();

        // Initial alignment
        SyncMetricsFromFont(EditorFontSize);
        SyncPaddingToMargin();

        AttachedToVisualTree += (_, _) =>
        {
            SyncMetricsFromFont(EditorFontSize);
            SyncPaddingToMargin();
            _overlay?.Focus();
        };
    }

    // Public method to focus the editor
    public void FocusEditor()
    {
        _overlay?.Focus();
    }
    
    // Public method to reset caret to the beginning
    public void ResetCaret()
    {
        UpdateSelection(0, 0);
        
        // Scroll to top
        if (_scroll != null)
        {
            _scroll.Offset = new Vector(0, 0);
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _overlay = this.FindControl<RichTextOverlay>("Overlay");
        _scroll = this.FindControl<ScrollViewer>("EditorScroll");

        if (_overlay is null)
            return;

        // Handle keyboard input
        _overlay.KeyDown += OnOverlayKeyDown;
        _overlay.TextInput += OnOverlayTextInput;
        _overlay.GotFocus += OnOverlayGotFocus;
        _overlay.LostFocus += OnOverlayLostFocus;
        _overlay.PointerPressed += OnOverlayPointerPressed;
        _overlay.PointerMoved += OnOverlayPointerMoved;
        _overlay.PointerReleased += OnOverlayPointerReleased;

        // Handle clicks on the scrollviewer background (below text)
        if (_scroll != null)
        {
            _scroll.PointerPressed += OnScrollPointerPressed;
        }

        // Initial caret position
        UpdateSelection(0, 0);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_overlay is not null)
        {
            _overlay.KeyDown -= OnOverlayKeyDown;
            _overlay.TextInput -= OnOverlayTextInput;
            _overlay.GotFocus -= OnOverlayGotFocus;
            _overlay.LostFocus -= OnOverlayLostFocus;
            _overlay.PointerPressed -= OnOverlayPointerPressed;
            _overlay.PointerMoved -= OnOverlayPointerMoved;
            _overlay.PointerReleased -= OnOverlayPointerReleased;
        }
        
        if (_scroll != null)
        {
            _scroll.PointerPressed -= OnScrollPointerPressed;
        }
    }

    // Public method to focus the editor (kept for existing call sites)
    public new void Focus()
    {
        _overlay?.Focus();
    }

    private void OnOverlayGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (_overlay is not null)
        {
            _overlay.ShowCaret = true;
            _overlay.InvalidateVisual();
        }
    }

    private void OnOverlayLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_overlay is not null)
        {
            _overlay.ShowCaret = false;
            _overlay.InvalidateVisual();
        }
    }

    private void OnOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _overlay?.Focus();
        if (_overlay is null) return;

        var currentPoint = e.GetCurrentPoint(_overlay);
        var properties = currentPoint.Properties;

        // Always evaluate caret from overlay-local coordinates.
        var point = e.GetPosition(_overlay);

        // Allow positioning with both left and right mouse buttons
        if (properties.IsLeftButtonPressed || properties.IsRightButtonPressed)
        {
            int charPos = _overlay.GetCaretPositionFromPoint(point);
            charPos = Math.Clamp(charPos, 0, (Text ?? string.Empty).Length);

            if (properties.IsLeftButtonPressed)
            {
                _isPointerPressed = true;
                e.Pointer.Capture(_overlay);

                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    UpdateSelection(SelectionStart, charPos);
                }
                else
                {
                    UpdateSelection(charPos, charPos);
                }
            }
            else if (properties.IsRightButtonPressed)
            {
                UpdateSelection(charPos, charPos);
            }
            e.Handled = true;
        }
    }

    private void OnScrollPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // If click wasn't handled by overlay (e.g. clicked on background below text), handle it here.
        if (e.Handled || _overlay is null) return;

        var point = e.GetPosition(_overlay);
        var properties = e.GetCurrentPoint(_scroll).Properties;

        if (properties.IsLeftButtonPressed)
        {
            _overlay.Focus();
            
            int charPos = _overlay.GetCaretPositionFromPoint(point);
            charPos = Math.Clamp(charPos, 0, (Text ?? string.Empty).Length);

            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                UpdateSelection(SelectionStart, charPos);
            }
            else
            {
                UpdateSelection(charPos, charPos);
            }
            
            e.Handled = true;
        }
    }

    private void OnOverlayPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_overlay is null || !_isPointerPressed) return;

        // Always evaluate caret from overlay-local coordinates.
        var point = e.GetPosition(_overlay);
        int charPos = _overlay.GetCaretPositionFromPoint(point);
        charPos = Math.Clamp(charPos, 0, (Text ?? string.Empty).Length);

        UpdateSelection(SelectionStart, charPos);
        e.Handled = true;
    }

    private void OnOverlayPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            _isPointerPressed = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
        else if (e.InitialPressMouseButton == MouseButton.Right)
        {
            ShowContextMenu();
            e.Handled = true;
        }
    }

    private int CaretIndex => SelectionStart == SelectionEnd ? SelectionEnd : Math.Min(SelectionStart, SelectionEnd);

    private int SelectionAnchor => Math.Min(SelectionStart, SelectionEnd);
    private int SelectionCaret => Math.Max(SelectionStart, SelectionEnd);

    private void OnOverlayTextInput(object? sender, TextInputEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Text)) return;

        // Prevent double-insert of new lines: Enter is handled explicitly in KeyDown.
        var input = e.Text;
        if (input == "\r" || input == "\n" || input == "\r\n")
        {
            e.Handled = true;
            return;
        }

        // Keep internal storage normalized to LF (this control's caret/line math expects '\n').
        var normalizedInput = input.Replace("\r\n", "\n").Replace("\r", "\n");

        string text = (Text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");

        // Clamp selection to current text length (fixes edge cases when caret is at the very end)
        int len = text.Length;
        int selStart = Math.Clamp(SelectionStart, 0, len);
        int selEnd = Math.Clamp(SelectionEnd, 0, len);
        int start = Math.Min(selStart, selEnd);
        int end = Math.Max(selStart, selEnd);

        SaveUndoState();

        if (end > start)
            text = text.Remove(start, end - start);

        int caretPos = start;
        caretPos = Math.Clamp(caretPos, 0, text.Length);

        text = text.Insert(caretPos, normalizedInput);
        int newCaretPos = caretPos + normalizedInput.Length;

        Text = text;
        UpdateSelection(newCaretPos, newCaretPos);
        e.Handled = true;
    }

    private void OnOverlayKeyDown(object? sender, KeyEventArgs e)
    {
        // Keep internal storage normalized to LF
        string text = (Text ?? string.Empty).Replace("\r\n", "\n").Replace("\r", "\n");

        // Clamp selection/caret to current text length
        int len = text.Length;
        int selStart = Math.Clamp(SelectionStart, 0, len);
        int selEnd = Math.Clamp(SelectionEnd, 0, len);

        int start = Math.Min(selStart, selEnd);
        int end = Math.Max(selStart, selEnd);
        bool hasSelection = end > start;

        int caretPos = CaretIndex;
        caretPos = Math.Clamp(caretPos, 0, len);

        // --- Indentation (Tab / Shift+Tab) ---
        // Implement soft tabs with proper selection+caret sync.
        if (e.Key == Key.Tab)
        {
            SaveUndoState();

            if (!hasSelection)
            {
                // Insert spaces up to next tab stop at the caret column.
                int lineStart = GetLineStart(text, caretPos);
                int column = caretPos - lineStart;
                int toInsert = IndentSize - (column % IndentSize);
                if (toInsert <= 0) toInsert = IndentSize;

                text = text.Insert(caretPos, new string(' ', toInsert));
                Text = text;
                UpdateSelection(caretPos + toInsert, caretPos + toInsert);
                e.Handled = true;
                return;
            }
            else
            {
                // If selection ends exactly at a line start, don't include that last line (editor-like).
                int effectiveEnd = end;
                if (effectiveEnd > 0 && effectiveEnd <= text.Length && effectiveEnd == GetLineStart(text, effectiveEnd))
                    effectiveEnd = Math.Max(start, effectiveEnd - 1);

                bool unindent = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

                // Determine affected line starts
                int firstLineStart = GetLineStart(text, start);
                var lineStarts = new List<int>();
                lineStarts.Add(firstLineStart);

                for (int i = firstLineStart; i < text.Length && i <= effectiveEnd; i++)
                {
                    if (text[i] == '\n')
                    {
                        int next = i + 1;
                        if (next <= effectiveEnd)
                            lineStarts.Add(next);
                    }
                }

                // Build new text with edits from bottom to top so indexes stay valid.
                int deltaBeforeStart = 0;
                int deltaBeforeEnd = 0;

                // We need per-line delta accounting for both start/end positions.
                // We'll compute deltas by applying operations in ascending order and tracking shifts.
                int runningDelta = 0;
                int adjustedStart = start;
                int adjustedEnd = end;

                // Apply indentation line-by-line using a StringBuilder approach of in-place edits.
                // Since count of lines is limited, simple string splicing bottom-to-top is OK.
                // But we also need accurate selection shifts; we can compute removed/added per line.

                // Process line starts in reverse for safe splicing.
                foreach (var rawLineStart in lineStarts.OrderByDescending(x => x))
                {
                    int lineStartIdx = rawLineStart;
                    if (lineStartIdx < 0 || lineStartIdx > text.Length)
                        continue;

                    int change = 0;

                    if (!unindent)
                    {
                        text = text.Insert(lineStartIdx, IndentString);
                        change = IndentSize;
                    }
                    else
                    {
                        // Remove up to IndentSize leading spaces.
                        int removeCount = 0;
                        while (removeCount < IndentSize && lineStartIdx + removeCount < text.Length && text[lineStartIdx + removeCount] == ' ')
                            removeCount++;

                        if (removeCount > 0)
                        {
                            text = text.Remove(lineStartIdx, removeCount);
                            change = -removeCount;
                        }
                    }

                    // Update selection endpoints if this lineStart is before them.
                    if (lineStartIdx < start)
                        deltaBeforeStart += change;
                    if (lineStartIdx < end)
                        deltaBeforeEnd += change;
                }

                int newStart = Math.Clamp(start + deltaBeforeStart, 0, text.Length);
                int newEnd = Math.Clamp(end + deltaBeforeEnd, 0, text.Length);

                // Preserve selection direction.
                if (selStart <= selEnd)
                    UpdateSelection(newStart, newEnd);
                else
                    UpdateSelection(newEnd, newStart);

                Text = text;
                _overlay?.Focus();
                e.Handled = true;
                return;
            }
        }

        // Alt+K for context menu
        if (e.Key == Key.K && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
        {
            if (TopLevel.GetTopLevel(this) is Window owner)
            {
                var menu = new Windows.ContextMenuWindow(owner, null);
                menu.Show(owner);
                e.Handled = true;
            }
            return;
        }

        // Standard keyboard shortcuts
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.B:
                    // Toggle bold around selection
                    SaveUndoState();
                    ToggleWrapSelection("**", "**");
                    e.Handled = true;
                    return;

                case Key.I:
                    // Toggle italic around selection
                    SaveUndoState();
                    ToggleWrapSelection("*", "*");
                    e.Handled = true;
                    return;

                 case Key.C:
                     _ = CopyAsync();
                     e.Handled = true;
                     return;
                    
                case Key.V:
                    _ = PasteAsync();
                    e.Handled = true;
                    return;
                    
                case Key.X:
                    _ = CutAsync();
                    e.Handled = true;
                    return;
                    
                case Key.A:
                    SelectAll();
                    e.Handled = true;
                    return;
                    
                case Key.Z:
                    Undo();
                    e.Handled = true;
                    return;
                    
                case Key.Y:
                    Redo();
                    e.Handled = true;
                    return;
            }
        }

        // Navigation
        switch (e.Key)
        {
            case Key.Left:
                if (hasSelection && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    UpdateSelection(start, start);
                }
                else if (caretPos > 0)
                {
                    int newPos = e.KeyModifiers.HasFlag(KeyModifiers.Control)
                        ? GetPreviousWordPosition(text, caretPos)
                        : caretPos - 1;

                    if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                        UpdateSelection(newPos, newPos);
                    else
                        UpdateSelection(SelectionStart, newPos);
                }
                e.Handled = true;
                return;

            case Key.Right:
                if (hasSelection && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    UpdateSelection(end, end);
                }
                else if (caretPos < text.Length)
                {
                    int newPos = e.KeyModifiers.HasFlag(KeyModifiers.Control)
                        ? GetNextWordPosition(text, caretPos)
                        : caretPos + 1;

                    if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                        UpdateSelection(newPos, newPos);
                    else
                        UpdateSelection(SelectionStart, newPos);
                }
                e.Handled = true;
                return;

            case Key.Up:
            {
                int newPos = GetCaretUp(text, caretPos);
                if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    UpdateSelection(newPos, newPos);
                else
                    UpdateSelection(SelectionStart, newPos);
                e.Handled = true;
                return;
            }

            case Key.Down:
            {
                int newPos = GetCaretDown(text, caretPos);
                if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    UpdateSelection(newPos, newPos);
                else
                    UpdateSelection(SelectionStart, newPos);
                e.Handled = true;
                return;
            }

            case Key.Home:
            {
                int lineStart = e.KeyModifiers.HasFlag(KeyModifiers.Control) 
                    ? 0 
                    : GetLineStart(text, caretPos);
                if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    UpdateSelection(lineStart, lineStart);
                else
                    UpdateSelection(start, lineStart);
                e.Handled = true;
                return;
            }

            case Key.End:
            {
                int lineEnd = e.KeyModifiers.HasFlag(KeyModifiers.Control)
                    ? text.Length
                    : GetLineEnd(text, caretPos);
                if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    UpdateSelection(lineEnd, lineEnd);
                else
                    UpdateSelection(start, lineEnd);
                e.Handled = true;
                return;
            }
        }

        // Editing keys
        switch (e.Key)
        {
            case Key.Back:
                SaveUndoState();
                if (hasSelection)
                {
                    text = text.Remove(start, end - start);
                    Text = text;
                    UpdateSelection(start, start);
                }
                else if (caretPos > 0)
                {
                    int deleteCount = 1;
                    int deletePos = caretPos - 1;

                    // Handle CRLF as a single newline (Notepad-like)
                    if (deletePos > 0 && text[deletePos] == '\n' && text[deletePos - 1] == '\r')
                    {
                        deleteCount = 2;
                        deletePos -= 1;
                    }

                    if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    {
                        int wordStart = GetPreviousWordPosition(text, caretPos);
                        deleteCount = caretPos - wordStart;
                        deletePos = wordStart;
                    }

                    text = text.Remove(deletePos, deleteCount);
                    Text = text;
                    UpdateSelection(deletePos, deletePos);
                }
                e.Handled = true;
                return;

            case Key.Delete:
                SaveUndoState();
                if (hasSelection)
                {
                    text = text.Remove(start, end - start);
                    Text = text;
                    UpdateSelection(start, start);
                }
                else if (caretPos < text.Length)
                {
                    int deleteCount = 1;

                    // Handle CRLF as a single newline (Notepad-like)
                    if (text[caretPos] == '\r' && caretPos + 1 < text.Length && text[caretPos + 1] == '\n')
                        deleteCount = 2;

                    if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                    {
                        int wordEnd = GetNextWordPosition(text, caretPos);
                        deleteCount = wordEnd - caretPos;
                    }

                    text = text.Remove(caretPos, deleteCount);
                    Text = text;
                    UpdateSelection(caretPos, caretPos);
                }
                e.Handled = true;
                return;

            case Key.Enter:
                // Handle Enter only here; TextInput may also fire with \r/\n depending on platform/IME.
                SaveUndoState();
                if (hasSelection)
                {
                    text = text.Remove(start, end - start);
                    caretPos = start;
                }
                text = text.Insert(caretPos, "\n");
                Text = text;
                UpdateSelection(caretPos + 1, caretPos + 1);
                e.Handled = true;
                return;

            // IMPORTANT: don't handle Space here.
            // Let Avalonia TextInput insert spaces so caret movement is consistent (Notepad-like).
        }
    }

    private void SaveUndoState()
    {
        string currentText = Text ?? string.Empty;
        if (currentText != _lastSavedText)
        {
            _undoStack.Push(_lastSavedText);
            _lastSavedText = currentText;
            _redoStack.Clear();
            
            if (_undoStack.Count > 100)
            {
                var temp = new Stack<string>(_undoStack.Take(100).Reverse());
                _undoStack = temp;
            }
        }
    }

    private void Undo()
    {
        if (_undoStack.Count > 0)
        {
            _redoStack.Push(Text ?? string.Empty);
            string previousText = _undoStack.Pop();
            Text = previousText;
            _lastSavedText = previousText;
            
            int newPos = previousText.Length;
            UpdateSelection(newPos, newPos);
        }
    }

    private void Redo()
    {
        if (_redoStack.Count > 0)
        {
            _undoStack.Push(Text ?? string.Empty);
            string nextText = _redoStack.Pop();
            Text = nextText;
            _lastSavedText = nextText;
            
            int newPos = nextText.Length;
            UpdateSelection(newPos, newPos);
        }
    }

    // make Undo/Redo accessible publicly
    public void UndoAction()
    {
        Undo();
    }

    public void RedoAction()
    {
        Redo();
    }

    private static int GetPreviousWordPosition(string text, int position)
    {
        if (position <= 0) return 0;
        
        int pos = position - 1;
        
        while (pos > 0 && char.IsWhiteSpace(text[pos]))
            pos--;
        
        while (pos > 0 && !char.IsWhiteSpace(text[pos - 1]))
            pos--;
            
        return pos;
    }

    private static int GetNextWordPosition(string text, int position)
    {
        if (position >= text.Length) return text.Length;
        
        int pos = position;
        
        while (pos < text.Length && !char.IsWhiteSpace(text[pos]))
            pos++;
        
        while (pos < text.Length && char.IsWhiteSpace(text[pos]))
            pos++;
            
        return pos;
    }

    private static int GetLineStart(string text, int position)
    {
        int pos = Math.Clamp(position, 0, text.Length);
        while (pos > 0 && text[pos - 1] != '\n')
            pos--;
        return pos;
    }

    private static int GetLineEnd(string text, int position)
    {
        int pos = Math.Clamp(position, 0, text.Length);
        while (pos < text.Length && text[pos] != '\n' && text[pos] != '\r')
            pos++;
        return pos;
    }

    private int GetCaretUp(string text, int caretPos)
    {
        if (_overlay is null) return caretPos;
        
        int lineStart = GetLineStart(text, caretPos);
        if (lineStart == 0) return 0;
        
        int prevLineEnd = lineStart - 1;
        if (prevLineEnd > 0 && text[prevLineEnd] == '\n' && text[prevLineEnd - 1] == '\r')
            prevLineEnd--;
        
        int prevLineStart = GetLineStart(text, prevLineEnd);
        int offsetInCurrentLine = caretPos - lineStart;
        int prevLineLength = prevLineEnd - prevLineStart;
        
        return prevLineStart + Math.Min(offsetInCurrentLine, prevLineLength);
    }

    private int GetCaretDown(string text, int caretPos)
    {
        if (_overlay is null) return caretPos;
        
        int lineStart = GetLineStart(text, caretPos);
        int lineEnd = GetLineEnd(text, caretPos);
        
        if (lineEnd >= text.Length) return text.Length;
        
        int nextLineStart = lineEnd;
        if (nextLineStart < text.Length && text[nextLineStart] == '\r')
            nextLineStart++;
        if (nextLineStart < text.Length && text[nextLineStart] == '\n')
            nextLineStart++;
        
        int offsetInCurrentLine = caretPos - lineStart;
        int nextLineEnd = GetLineEnd(text, nextLineStart);
        int nextLineLength = nextLineEnd - nextLineStart;
        
        return nextLineStart + Math.Min(offsetInCurrentLine, nextLineLength);
    }

    private async Task CopyAsync()
    {
        int start = Math.Min(SelectionStart, SelectionEnd);
        int end = Math.Max(SelectionStart, SelectionEnd);
        
        if (end > start && TopLevel.GetTopLevel(this) is { Clipboard: not null } topLevel)
        {
            string selectedText = (Text ?? string.Empty).Substring(start, end - start);
            await topLevel.Clipboard.SetTextAsync(selectedText);
        }
    }

    private async Task CutAsync()
    {
        int start = Math.Min(SelectionStart, SelectionEnd);
        int end = Math.Max(SelectionStart, SelectionEnd);
        
        if (end > start && TopLevel.GetTopLevel(this) is { Clipboard: not null } topLevel)
        {
            SaveUndoState();
            string selectedText = (Text ?? string.Empty).Substring(start, end - start);
            await topLevel.Clipboard.SetTextAsync(selectedText);
            
            string text = Text ?? string.Empty;
            text = text.Remove(start, end - start);
            Text = text;
            UpdateSelection(start, start);
        }
    }

    private async Task PasteAsync()
    {
        if (TopLevel.GetTopLevel(this) is not { Clipboard: not null } topLevel)
            return;

        string? clipboardText = await ClipboardCompat.TryGetTextAsync((Avalonia.Input.Platform.IClipboard)topLevel.Clipboard);
        if (string.IsNullOrEmpty(clipboardText))
            return;

        SaveUndoState();

        string text = Text ?? string.Empty;
        int start = Math.Min(SelectionStart, SelectionEnd);
        int end = Math.Max(SelectionStart, SelectionEnd);
        
        if (end > start)
        {
            text = text.Remove(start, end - start);
        }
        
        text = text.Insert(start, clipboardText);
        Text = text;
        UpdateSelection(start + clipboardText.Length, start + clipboardText.Length);
    }

    private void SelectAll()
    {
        string text = Text ?? string.Empty;
        UpdateSelection(0, text.Length);
    }

    private void UpdateSelection(int start, int end)
    {
        SelectionStart = start;
        SelectionEnd = end;

        if (_overlay is not null)
        {
            _overlay.SelectionStart = start;
            _overlay.SelectionEnd = end;
            _overlay.InvalidateVisual();
        }

        ScrollCaretIntoView();
    }

    private void ScrollCaretIntoView()
    {
        if (_overlay is null || _scroll is null)
            return;

        if (SelectionStart != SelectionEnd)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            if (_overlay is null || _scroll is null)
                return;

            // caret point in overlay coordinates
            var caretPt = _overlay.GetCaretLocation(SelectionEnd);

            // Translate to scrollviewer viewport coordinates
            var tx = _overlay.TransformToVisual(_scroll);
            if (tx is null)
                return;

            var caretInScroll = caretPt.Transform(tx.Value);

            // Current viewport and offset
            var viewportH = _scroll.Viewport.Height;
            if (viewportH <= 0) return;

            var top = _scroll.Offset.Y;
            var bottom = top + viewportH;

            // Add small padding
            const double pad = 12;
            var caretTop = caretInScroll.Y + _scroll.Offset.Y;
            var caretBottom = caretTop + Math.Max(1, LineSpacing);

            double targetY = _scroll.Offset.Y;
            if (caretTop < top + pad)
                targetY = Math.Max(0, caretTop - pad);
            else if (caretBottom > bottom - pad)
                targetY = Math.Max(0, caretBottom - viewportH + pad);

            if (Math.Abs(targetY - _scroll.Offset.Y) > 0.5)
                _scroll.Offset = new Vector(_scroll.Offset.X, targetY);
        }, DispatcherPriority.Background);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == EditorFontSizeProperty)
            SyncMetricsFromFont(EditorFontSize);
        else if (change.Property == LeftMarginProperty || change.Property == RightMarginProperty)
            SyncPaddingToMargin();
        else if (change.Property == TextProperty)
        {
            // When text changes from external source (e.g., file load), reset caret to start
            // But only do this once per file load
            var oldText = change.OldValue as string ?? string.Empty;
            var newText = change.NewValue as string ?? string.Empty;
            
            // Detect file load: old text was empty or small, new text is substantial
            // AND we haven't already loaded a file in this editor instance
            bool isFileLoad = !_hasLoadedFile && oldText.Length < 50 && newText.Length > 100;
            
            if (isFileLoad)
            {
                _hasLoadedFile = true; // Set flag so this only happens once
                
                // Force complete layout update for scrollviewer
                if (_overlay != null)
                {
                    _overlay.InvalidateMeasure();
                    _overlay.InvalidateArrange();
                    _overlay.InvalidateVisual();
                }
                
                if (_scroll != null)
                {
                    _scroll.InvalidateMeasure();
                    _scroll.InvalidateArrange();
                }
                
                // Text loaded from file - set caret to start and focus after layout is updated
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    // Wait for layout to complete
                    _overlay?.UpdateLayout();
                    _scroll?.UpdateLayout();
                    
                    UpdateSelection(0, 0);
                    _overlay?.Focus();
                }, Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }
    }

    private void SyncMetricsFromFont(double fontSize)
    {
        if (fontSize <= 0) return;

        var spacing = Math.Max(10, Math.Round(fontSize * 1.1));
        LineSpacing = spacing;
        FirstLineOffset = Math.Round(fontSize * 0.9);
    }

    private void SyncPaddingToMargin()
    {
        var left = Math.Max(0, LeftMargin) + 1;
        var right = Math.Max(0, RightMargin);
        TextPadding = new Thickness(left, 0, right, 0);
    }

    // Public helper: toggle wrapping markers around current selection
    public void ToggleWrapSelection(string open, string close)
    {
        // Record undo state for toolbar-invoked changes
        SaveUndoState();

        int start = Math.Min(SelectionStart, SelectionEnd);
        int end = Math.Max(SelectionStart, SelectionEnd);
        int length = Math.Max(0, end - start);
        var text = Text ?? string.Empty;

        if (length <= 0)
        {
            // nothing selected: focus the overlay so user can select
            _overlay?.Focus();
            return;
        }

        var selected = text.Substring(start, length);

        // If selection already includes markers on both sides -> remove them
        if (selected.StartsWith(open) && selected.EndsWith(close) &&
            selected.Length >= open.Length + close.Length)
        {
            var inner = selected.Substring(open.Length, selected.Length - open.Length - close.Length);
            Text = text.Remove(start, length).Insert(start, inner);
            UpdateSelection(start, inner.Length);
            _overlay?.Focus();
            return;
        }

        // If markers wrap the selection externally -> remove them
        var beforeOk = start - open.Length >= 0 &&
                       text.AsSpan(start - open.Length, open.Length).SequenceEqual(open);
        var afterOk = start + length + close.Length <= text.Length &&
                      text.AsSpan(start + length, close.Length).SequenceEqual(close);
        if (beforeOk && afterOk)
        {
            var newStart = start - open.Length;
            var newLen = length + open.Length + close.Length;
            Text = text.Remove(newStart, newLen).Insert(newStart, text.Substring(start, length));
            UpdateSelection(newStart, length);
            _overlay?.Focus();
            return;
        }

        // Otherwise add markers
        var replacement = $"{open}{selected}{close}";
        Text = text.Remove(start, length).Insert(start, replacement);
        UpdateSelection(start, replacement.Length);
        _overlay?.Focus();
    }

    private void ShowContextMenu()
    {
        var menu = new ContextMenu();
        var cut = new MenuItem { Header = "Cut" };
        cut.Click += async (_, _) => await CutAsync();

        var copy = new MenuItem { Header = "Copy" };
        copy.Click += async (_, _) => await CopyAsync();

        var paste = new MenuItem { Header = "Paste" };
        paste.Click += async (_, _) => await PasteAsync();

        var selectAll = new MenuItem { Header = "Select All" };
        selectAll.Click += (_, _) => SelectAll();

        menu.Items.Add(cut);
        menu.Items.Add(copy);
        menu.Items.Add(paste);
        menu.Items.Add(new Separator());
        menu.Items.Add(selectAll);

        menu.Open(_overlay);
    }
}
