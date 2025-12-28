using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Controls.Primitives;

namespace InsaitTextEditor.Scripts.SkiaSharp;

public sealed class RichTextOverlay : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<RichTextOverlay, string?>(
            nameof(Text), 
            string.Empty);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<RichTextOverlay, double>(nameof(FontSize), 16d);

    public static readonly StyledProperty<IBrush> TextBrushProperty =
        AvaloniaProperty.Register<RichTextOverlay, IBrush>(nameof(TextBrush), Brushes.Black);

    public static readonly StyledProperty<bool> DefaultBoldProperty =
        AvaloniaProperty.Register<RichTextOverlay, bool>(nameof(DefaultBold), false);

    public static readonly StyledProperty<bool> DefaultItalicProperty =
        AvaloniaProperty.Register<RichTextOverlay, bool>(nameof(DefaultItalic), false);

    public static readonly StyledProperty<int> SelectionStartProperty =
        AvaloniaProperty.Register<RichTextOverlay, int>(nameof(SelectionStart), 0);

    public static readonly StyledProperty<int> SelectionEndProperty =
        AvaloniaProperty.Register<RichTextOverlay, int>(nameof(SelectionEnd), 0);

    // Caret visual properties
    public static readonly StyledProperty<IBrush> CaretBrushProperty =
        AvaloniaProperty.Register<RichTextOverlay, IBrush>(nameof(CaretBrush), new SolidColorBrush(Color.FromRgb(0xB4, 0x00, 0xFF))); // bright purple
    public static readonly StyledProperty<double> CaretWidthProperty =
        AvaloniaProperty.Register<RichTextOverlay, double>(nameof(CaretWidth), 2.0);
    public static readonly StyledProperty<bool> ShowCaretProperty =
        AvaloniaProperty.Register<RichTextOverlay, bool>(nameof(ShowCaret), true);

    // Alignment properties
    public static readonly StyledProperty<double> LineSpacingProperty =
        AvaloniaProperty.Register<RichTextOverlay, double>(nameof(LineSpacing), 0d);
    public static readonly StyledProperty<double> FirstLineOffsetProperty =
        AvaloniaProperty.Register<RichTextOverlay, double>(nameof(FirstLineOffset), 0d);
    public static readonly StyledProperty<double> TopMarginProperty =
        AvaloniaProperty.Register<RichTextOverlay, double>(nameof(TopMargin), 0d);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public IBrush TextBrush { get => GetValue(TextBrushProperty); set => SetValue(TextBrushProperty, value); }
    public bool DefaultBold { get => GetValue(DefaultBoldProperty); set => SetValue(DefaultBoldProperty, value); }
    public bool DefaultItalic { get => GetValue(DefaultItalicProperty); set => SetValue(DefaultItalicProperty, value); }
    public int SelectionStart { get => GetValue(SelectionStartProperty); set => SetValue(SelectionStartProperty, value); }
    public int SelectionEnd { get => GetValue(SelectionEndProperty); set => SetValue(SelectionEndProperty, value); }

    public IBrush CaretBrush { get => GetValue(CaretBrushProperty); set => SetValue(CaretBrushProperty, value); }
    public double CaretWidth { get => GetValue(CaretWidthProperty); set => SetValue(CaretWidthProperty, value); }
    public bool ShowCaret { get => GetValue(ShowCaretProperty); set => SetValue(ShowCaretProperty, value); }

    public double LineSpacing { get => GetValue(LineSpacingProperty); set => SetValue(LineSpacingProperty, value); }
    public double FirstLineOffset { get => GetValue(FirstLineOffsetProperty); set => SetValue(FirstLineOffsetProperty, value); }
    public double TopMargin { get => GetValue(TopMarginProperty); set => SetValue(TopMarginProperty, value); }

    private Size _measured = new Size(1, 1);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        
        if (change.Property == TextProperty)
        {
            // Text changed - trigger layout update
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    // --- Measure / Arrange for ScrollViewer (no IScrollable needed) ---
    protected override Size MeasureOverride(Size availableSize)
    {
        var raw = (Text ?? string.Empty).Replace("\r\n", "\n");

        var w = double.IsInfinity(availableSize.Width) ? Math.Max(1, Bounds.Width) : Math.Max(1, availableSize.Width);

        double lineHeight = LineSpacing > 0 ? LineSpacing : Math.Max(10, Math.Round(FontSize * 1.3));

        double visualTextHeight = Math.Max(8, Math.Round(FontSize * 1.0));
        double bottomGap = Math.Max(1, Math.Round(FontSize * 0.12));
        double y0 = TopMargin + FirstLineOffset - (visualTextHeight + bottomGap);
        if (double.IsNaN(y0) || double.IsInfinity(y0)) y0 = 0;

        // Estimate number of visual lines, including wrapping.
        // This gives ScrollViewer a correct Extent so the scrollbar thumb has a real size.
        int visualLines = 1;
        if (raw.Length > 0)
        {
            visualLines = 0;
            var brush = TextBrush;
            var typeface = new Typeface(Typeface.Default.FontFamily);
            double textWidth = Math.Max(1, w);

            int start = 0;
            while (start <= raw.Length)
            {
                int nl = raw.IndexOf('\n', start);
                int end = nl < 0 ? raw.Length : nl;
                var line = raw.Substring(start, end - start);

                if (line.Length == 0)
                {
                    visualLines += 1;
                }
                else
                {
                    int pos = 0;
                    while (pos < line.Length)
                    {
                        int count = FitCount(line, pos, typeface, FontSize, brush, textWidth);
                        if (count <= 0)
                        {
                            // safeguard: at least advance 1 char
                            count = 1;
                        }
                        pos += count;
                        visualLines += 1;
                    }
                }

                if (nl < 0)
                    break;
                start = nl + 1;
            }
        }

        // extra padding lines
        double h = Math.Max(1, y0 + (visualLines + 2) * lineHeight);

        _measured = new Size(w, h);
        return _measured;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        // IMPORTANT: return desired size so ScrollViewer gets correct Extent.
        // Width is constrained to viewport; height can exceed viewport.
        var arranged = new Size(finalSize.Width, Math.Max(finalSize.Height, _measured.Height));
        return arranged;
    }

    private double MeasureTextWidth(string text, Typeface typeface, double fontSize, IBrush brush)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        
        // Avalonia TextLayout might trim trailing spaces in width calculation.
        // We manually account for them to ensure caret moves correctly.
        if (text.EndsWith(" "))
        {
            int spaces = 0;
            while (spaces < text.Length && text[text.Length - 1 - spaces] == ' ')
                spaces++;
            
            if (spaces > 0)
            {
                var trimmed = text.Substring(0, text.Length - spaces);
                double wTrimmed = 0;
                if (trimmed.Length > 0)
                {
                    var l = new TextLayout(trimmed, typeface, fontSize, brush, TextAlignment.Left);
                    wTrimmed = l.Width;
                }
                
                // Measure space width reliably
                var probe = new TextLayout("A A", typeface, fontSize, brush, TextAlignment.Left);
                var probe2 = new TextLayout("AA", typeface, fontSize, brush, TextAlignment.Left);
                double singleSpaceW = probe.Width - probe2.Width;
                
                // Fallback if something is weird
                if (singleSpaceW <= 0.01) 
                {
                    var spaceOnly = new TextLayout(" ", typeface, fontSize, brush, TextAlignment.Left);
                    singleSpaceW = spaceOnly.Width;
                }
                
                return wTrimmed + (spaces * singleSpaceW);
            }
        }
        
        var layout = new TextLayout(text, typeface, fontSize, brush, TextAlignment.Left);
        return layout.Width;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var raw = (Text ?? string.Empty).Replace("\r\n", "\n");

        double maxWidth = Math.Max(0, Bounds.Width);
        double maxHeight = Math.Max(0, Bounds.Height);
        if (maxWidth < 2 || maxHeight < 2) return;

        var brush = TextBrush ?? Brushes.Black;

        // Line height aligned to ruled background
        double lineHeight = LineSpacing > 0 ? LineSpacing : Math.Max(10, Math.Round(FontSize * 1.3));

        // Vertical positioning for text
        double visualTextHeight = Math.Max(8, Math.Round(FontSize * 1.0));
        double bottomGap = Math.Max(1, Math.Round(FontSize * 0.12));
        double y = TopMargin + FirstLineOffset - (visualTextHeight + bottomGap);
        if (double.IsNaN(y) || double.IsInfinity(y)) y = 0;

        // If no text: still draw caret if requested
        if (raw.Length == 0)
        {
            if (ShowCaret && SelectionStart == 0 && SelectionEnd == 0)
            {
                var cBrush = CaretBrush ?? new SolidColorBrush(Color.FromRgb(0xB4, 0x00, 0xFF));
                double cWidth = Math.Max(1, CaretWidth);
                context.FillRectangle(cBrush, new Rect(0, y, cWidth, lineHeight));
            }
            return;
        }

        var (lines, rawToDisplay) = ParseRunsWithMap(raw);

        // Map selection to display domain - with safety checks
        int selRawStart = Math.Min(SelectionStart, SelectionEnd);
        int selRawEnd = Math.Max(SelectionStart, SelectionEnd);
        selRawStart = Math.Clamp(selRawStart, 0, raw.Length);
        selRawEnd = Math.Clamp(selRawEnd, 0, raw.Length);
        
        int selStart = selRawStart < rawToDisplay.Length ? rawToDisplay[selRawStart] : 0;
        int selEnd = selRawEnd < rawToDisplay.Length ? rawToDisplay[selRawEnd] : (rawToDisplay.Length > 0 ? rawToDisplay[rawToDisplay.Length - 1] : 0);

        // Caret mapping (collapsed selection)
        int caretDisplay = -1;
        bool drawCaret = ShowCaret && SelectionStart == SelectionEnd;
        if (drawCaret)
        {
            int caretRaw = Math.Clamp(SelectionEnd, 0, raw.Length);
            caretDisplay = caretRaw < rawToDisplay.Length ? rawToDisplay[caretRaw] : 0;
        }

        int globalDisplayIndex = 0;

        foreach (var line in lines)
        {
            double x = 0d;
            int lineDisplayStart = globalDisplayIndex;
            bool caretDrawnForThisLine = false;
            bool isStartOfLogicalLine = true; // Track if we're at the start of the actual line

            foreach (var run in line)
            {
                var typeface = new Typeface(
                    Typeface.Default.FontFamily,
                    run.Italic ? FontStyle.Italic : FontStyle.Normal,
                    run.Bold ? FontWeight.Bold : FontWeight.Normal
                );

                string text = run.Text;
                int pos = 0;

                while (pos < text.Length)
                {
                    // Skip leading whitespace ONLY at the start of a wrapped line (not logical line start)
                    if (x == 0 && !isStartOfLogicalLine && char.IsWhiteSpace(text[pos]))
                    {
                        pos++;
                        globalDisplayIndex++;
                        continue;
                    }
                    
                    if (x >= maxWidth - 0.5)
                    {
                        y += lineHeight;
                        x = 0d;
                        isStartOfLogicalLine = false; // After wrapping, we're no longer at logical line start
                        if (y > maxHeight + lineHeight) return;
                    }

                    int count = FitCount(text, pos, typeface, FontSize, brush, maxWidth - x);
                    if (count == 0)
                    {
                        y += lineHeight;
                        x = 0d;
                        isStartOfLogicalLine = false; // After wrapping, we're no longer at logical line start
                        if (y > maxHeight + lineHeight) return;
                        continue;
                    }

                    string frag = text.Substring(pos, count);

                    // Measure fragment
                    var layout = new TextLayout(frag, typeface, FontSize, brush, TextAlignment.Left);
                    double w = MeasureTextWidth(frag, typeface, FontSize, brush); // Use robust measure

                    // Selection fill inside fragment
                    int fragStart = globalDisplayIndex;
                    int fragEnd = globalDisplayIndex + count;
                    if (selStart != selEnd && fragEnd > selStart && fragStart < selEnd)
                    {
                        int ovStart = Math.Max(selStart, fragStart) - fragStart;
                        int ovEnd = Math.Min(selEnd, fragEnd) - fragStart;

                        double x1 = 0;
                        if (ovStart > 0)
                        {
                            x1 = MeasureTextWidth(frag[..ovStart], typeface, FontSize, brush);
                        }

                        double wSel = 0;
                        if (ovEnd > ovStart)
                        {
                            wSel = MeasureTextWidth(frag.Substring(ovStart, ovEnd - ovStart), typeface, FontSize, brush);
                        }

                        var selRect = new Rect(x + x1, y + Math.Round(lineHeight * 0.15), wSel, Math.Round(lineHeight * 0.7));
                        var selBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x33, 0x99, 0xFF));
                        context.FillRectangle(selBrush, selRect);
                    }

                    // Draw text
                    layout.Draw(context, new Point(x, y));

                    // Underline for run if needed
                    if (run.Underline)
                    {
                        double thickness = Math.Max(1d, FontSize * 0.06d);
                        double underlineY = y + Math.Min(visualTextHeight - bottomGap - thickness - 1, visualTextHeight * 0.9);
                        var pen = new Pen(brush, thickness);
                        context.DrawLine(pen, new Point(x, underlineY), new Point(x + w, underlineY));
                    }

                    // Caret inside this fragment
                    if (drawCaret && !caretDrawnForThisLine && caretDisplay >= fragStart && caretDisplay <= fragEnd)
                    {
                        int offset = Math.Clamp(caretDisplay - fragStart, 0, count);
                        double xCaret = x;
                        if (offset > 0)
                        {
                            xCaret += MeasureTextWidth(frag[..offset], typeface, FontSize, brush);
                        }
                        var cBrush = CaretBrush ?? new SolidColorBrush(Color.FromRgb(0xB4, 0x00, 0xFF));
                        double cWidth = Math.Max(1, CaretWidth);
                        var caretRect = new Rect(xCaret, y, cWidth, lineHeight);
                        context.FillRectangle(cBrush, caretRect);
                        caretDrawnForThisLine = true;
                    }

                    x += w;
                    globalDisplayIndex += count;
                    pos += count;
                    isStartOfLogicalLine = false; // We've rendered something, no longer at start
                }
            }

            // If caret is exactly at end of this line (before newline), draw at end position
            if (drawCaret && !caretDrawnForThisLine && caretDisplay == globalDisplayIndex)
            {
                var cBrush = CaretBrush ?? new SolidColorBrush(Color.FromRgb(0xB4, 0x00, 0xFF));
                double cWidth = Math.Max(1, CaretWidth);
                context.FillRectangle(cBrush, new Rect(x, y, cWidth, lineHeight));
                caretDrawnForThisLine = true;
            }

            // Move to next ruled line
            globalDisplayIndex += 1; // '\n'
            y += lineHeight;
            if (y > maxHeight + lineHeight) return;
        }
        
        // If caret is at the very end of text after last line
        if (drawCaret && caretDisplay >= globalDisplayIndex)
        {
            var cBrush = CaretBrush ?? new SolidColorBrush(Color.FromRgb(0xB4, 0x00, 0xFF));
            double cWidth = Math.Max(1, CaretWidth);
            context.FillRectangle(cBrush, new Rect(0, y, cWidth, lineHeight));
        }
    }

    public Point GetCaretLocation(int caretRawIndex)
    {
        // Returns caret position (top-left) in Overlay coordinates.
        var raw = (Text ?? string.Empty).Replace("\r\n", "\n");
        caretRawIndex = Math.Clamp(caretRawIndex, 0, raw.Length);

        double maxWidth = Math.Max(0, Bounds.Width);
        var brush = TextBrush;
        double lineHeight = LineSpacing > 0 ? LineSpacing : Math.Max(10, Math.Round(FontSize * 1.3));

        double visualTextHeight = Math.Max(8, Math.Round(FontSize * 1.0));
        double bottomGap = Math.Max(1, Math.Round(FontSize * 0.12));
        double y = TopMargin + FirstLineOffset - (visualTextHeight + bottomGap);
        if (double.IsNaN(y) || double.IsInfinity(y)) y = 0;

        if (maxWidth < 2 || raw.Length == 0)
            return new Point(0, y);

        var (lines, rawToDisplay) = ParseRunsWithMap(raw);
        int caretDisplay = caretRawIndex < rawToDisplay.Length ? rawToDisplay[caretRawIndex] : 0;

        int globalDisplayIndex = 0;
        foreach (var line in lines)
        {
            double x = 0d;
            bool isStartOfLogicalLine = true;
            foreach (var run in line)
            {
                var typeface = new Typeface(
                    Typeface.Default.FontFamily,
                    run.Italic ? FontStyle.Italic : FontStyle.Normal,
                    run.Bold ? FontWeight.Bold : FontWeight.Normal
                );

                string text = run.Text;
                int pos = 0;
                while (pos < text.Length)
                {
                    // Skip leading whitespace ONLY at the start of a wrapped line (not logical line start)
                    if (x == 0 && !isStartOfLogicalLine && char.IsWhiteSpace(text[pos]))
                    {
                        pos++;
                        globalDisplayIndex++;
                        continue;
                    }
                    
                    if (x >= maxWidth - 0.5)
                    {
                        y += lineHeight;
                        x = 0d;
                        isStartOfLogicalLine = false;
                    }

                    int count = FitCount(text, pos, typeface, FontSize, brush, maxWidth - x);
                    if (count == 0)
                    {
                        y += lineHeight;
                        x = 0d;
                        isStartOfLogicalLine = false;
                        continue;
                    }

                    int fragStart = globalDisplayIndex;
                    int fragEnd = globalDisplayIndex + count;
                    if (caretDisplay >= fragStart && caretDisplay <= fragEnd)
                    {
                        int offset = Math.Clamp(caretDisplay - fragStart, 0, count);
                        double xCaret = x;
                        if (offset > 0)
                        {
                            xCaret += MeasureTextWidth(text.Substring(pos, offset), typeface, FontSize, brush);
                        }
                        return new Point(xCaret, y);
                    }

                    double w = MeasureTextWidth(text.Substring(pos, count), typeface, FontSize, brush);
                    x += w;
                    globalDisplayIndex += count;
                    pos += count;
                    isStartOfLogicalLine = false;
                }
            }

            globalDisplayIndex += 1; // '\n'
            y += lineHeight;
        }

        return new Point(0, y);
    }



    public int GetCaretPositionFromPoint(Point point)
    {
        var raw = (Text ?? string.Empty).Replace("\r\n", "\n");
        if (raw.Length == 0) return 0;

        // clamp
        if (point.X < 0) point = new Point(0, point.Y);

        double maxWidth = Math.Max(0, Bounds.Width);
        if (maxWidth < 2) return 0;

        var brush = TextBrush;
        double lineHeight = LineSpacing > 0 ? LineSpacing : Math.Max(10, Math.Round(FontSize * 1.3));

        double visualTextHeight = Math.Max(8, Math.Round(FontSize * 1.0));
        double bottomGap = Math.Max(1, Math.Round(FontSize * 0.12));
        double y0 = TopMargin + FirstLineOffset - (visualTextHeight + bottomGap);
        if (double.IsNaN(y0) || double.IsInfinity(y0)) y0 = 0;

        // If click is above first line, go to start
        if (point.Y <= y0) return 0;

        // Use the same parsing logic as rendering to maintain consistency
        var (lines, rawToDisplay) = ParseRunsWithMap(raw);

        double y = y0;
        int globalDisplayIndex = 0;

        foreach (var line in lines)
        {
            double x = 0d;
            bool isStartOfLogicalLine = true;

            foreach (var run in line)
            {
                var typeface = new Typeface(
                    Typeface.Default.FontFamily,
                    run.Italic ? FontStyle.Italic : FontStyle.Normal,
                    run.Bold ? FontWeight.Bold : FontWeight.Normal
                );

                string text = run.Text;
                int pos = 0;
                while (pos < text.Length)
                {
                    // Skip leading whitespace ONLY at the start of a wrapped line (not logical line start)
                    if (x == 0 && !isStartOfLogicalLine && char.IsWhiteSpace(text[pos]))
                    {
                        pos++;
                        globalDisplayIndex++;
                        continue;
                    }
                    
                    // Handle wrapping
                    if (x >= maxWidth - 0.5)
                    {
                        y += lineHeight;
                        x = 0d;
                        isStartOfLogicalLine = false;
                    }

                    int count = FitCount(text, pos, typeface, FontSize, brush, maxWidth - x);
                    if (count == 0)
                    {
                        y += lineHeight;
                        x = 0d;
                        isStartOfLogicalLine = false;
                        continue;
                    }

                    string frag = text.Substring(pos, count);
                    double w = MeasureTextWidth(frag, typeface, FontSize, brush);

                    // Check if click is in this fragment
                    if (point.Y >= y && point.Y < y + lineHeight)
                    {
                        if (point.X <= x)
                        {
                            // Before this fragment - return start of fragment
                            int displayPos = globalDisplayIndex;
                            // Find raw index for this display position
                            for (int i = 0; i < rawToDisplay.Length; i++)
                            {
                                if (rawToDisplay[i] == displayPos)
                                    return i;
                            }
                            return Math.Clamp(globalDisplayIndex, 0, raw.Length);
                        }
                        
                        if (point.X < x + w)
                        {
                            // Inside this fragment - find exact character
                            double prevW = 0;
                            for (int i = 0; i < frag.Length; i++)
                            {
                                double charW = MeasureTextWidth(frag.Substring(0, i + 1), typeface, FontSize, brush);
                                double charMid = x + prevW + (charW - prevW) / 2;
                                
                                if (point.X <= charMid)
                                {
                                    int displayPos = globalDisplayIndex + i;
                                    // Find raw index for this display position
                                    for (int j = 0; j < rawToDisplay.Length; j++)
                                    {
                                        if (rawToDisplay[j] == displayPos)
                                            return j;
                                    }
                                    return Math.Clamp(displayPos, 0, raw.Length);
                                }
                                prevW = charW;
                            }
                            
                            // After last char in fragment
                            int endDisplayPos = globalDisplayIndex + frag.Length;
                            for (int j = 0; j < rawToDisplay.Length; j++)
                            {
                                if (rawToDisplay[j] == endDisplayPos)
                                    return j;
                            }
                            return Math.Clamp(endDisplayPos, 0, raw.Length);
                        }
                    }

                    x += w;
                    globalDisplayIndex += count;
                    pos += count;
                    isStartOfLogicalLine = false;
                }
            }

            // Check if click is at end of line (after all text)
            if (point.Y >= y && point.Y < y + lineHeight && point.X >= x)
            {
                // End of this visual line
                int displayPos = globalDisplayIndex;
                for (int i = 0; i < rawToDisplay.Length; i++)
                {
                    if (rawToDisplay[i] == displayPos)
                        return i;
                }
                return Math.Clamp(displayPos, 0, raw.Length);
            }

            globalDisplayIndex += 1; // '\n'
            y += lineHeight;
        }

        // Click is below all text
        return raw.Length;
    }

    private static int FitCount(string text, int start, Typeface typeface, double fontSize, IBrush brush, double remainWidth)
    {
        if (remainWidth <= 0) return 0;
        remainWidth -= 0.5;
        if (remainWidth <= 0) return 0;

        int lo = 1;
        int hi = text.Length - start;
        int best = 0;

        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            var layout = new TextLayout(text.Substring(start, mid), typeface, fontSize, brush, TextAlignment.Left);
            double w = layout.Width;

            if (w <= remainWidth)
            {
                best = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }

        if (best == 0) return 0;

        // Try to wrap on whitespace - find last space before the break point
        int end = start + best;
        
        // If we're not at the end and we're breaking in the middle of a word
        if (end < text.Length)
        {
            // Check if we're breaking in the middle of a word (no space at break point)
            bool breakingWord = !char.IsWhiteSpace(text[end]) && 
                               (end > start && !char.IsWhiteSpace(text[end - 1]));
            
            if (breakingWord)
            {
                // Search backwards for the last whitespace
                for (int i = end - 1; i > start; i--)
                {
                    if (char.IsWhiteSpace(text[i]))
                    {
                        // Found a space - break here (don't include the space)
                        return i - start;
                    }
                }
                
                // No space found - we have to break the word
                // But make sure we at least take 1 character to avoid infinite loop
                return Math.Max(1, best);
            }
            
            // If we're at a space, don't include it in the line
            if (char.IsWhiteSpace(text[end]))
            {
                return best;
            }
        }

        return best;
    }

    private (List<List<TextRun>> Lines, int[] RawToDisplay) ParseRunsWithMap(string raw)
    {
        var lines = new List<List<TextRun>>();
        var current = new List<TextRun>();
        lines.Add(current);

        var buf = new StringBuilder();
        bool bold = DefaultBold, italic = DefaultItalic, underline = false;

        int displayIndex = 0;
        var map = new int[raw.Length + 1];
        map[0] = 0;

        void Flush()
        {
            if (buf.Length == 0) return;
            current.Add(new TextRun(buf.ToString(), bold, italic, underline));
            buf.Clear();
        }

        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];

            if (c == '\n')
            {
                Flush();
                displayIndex += 1;
                map[i + 1] = displayIndex;

                current = new List<TextRun>();
                lines.Add(current);
                continue;
            }

            if (c == '<')
            {
                int close = raw.IndexOf('>', i + 1);
                if (close > i)
                {
                    var tag = raw.Substring(i, close - i + 1).ToLowerInvariant();
                    if (tag == "<u>")
                    {
                        Flush();
                        underline = true;
                        i = close;
                        map[i + 1] = displayIndex;
                        continue;
                    }
                    if (tag == "</u>")
                    {
                        Flush();
                        underline = false;
                        i = close;
                        map[i + 1] = displayIndex;
                        continue;
                    }
                }
            }

            if (c == '*' && i + 1 < raw.Length && raw[i + 1] == '*')
            {
                Flush();
                bold = !bold;
                i++;
                map[i + 1] = displayIndex;
                continue;
            }

            if (c == '*')
            {
                Flush();
                italic = !italic;
                map[i + 1] = displayIndex;
                continue;
            }

            buf.Append(c);
            displayIndex += 1;
            map[i + 1] = displayIndex;
        }

        Flush();

        if (lines.Count > 0 && lines[^1].Count == 0)
            lines.RemoveAt(lines.Count - 1);

        return (lines, map);
    }

    private readonly record struct TextRun(string Text, bool Bold, bool Italic, bool Underline);
}
