using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace InsaitTextEditor.Scripts.SkiaSharp;

public sealed class RichTextOverlay : Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<RichTextOverlay, string?>(nameof(Text), string.Empty);

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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty ||
            change.Property == FontSizeProperty ||
            change.Property == TextBrushProperty ||
            change.Property == DefaultBoldProperty ||
            change.Property == DefaultItalicProperty ||
            change.Property == SelectionStartProperty ||
            change.Property == SelectionEndProperty ||
            change.Property == LineSpacingProperty ||
            change.Property == FirstLineOffsetProperty ||
            change.Property == TopMarginProperty ||
            change.Property == CaretBrushProperty ||
            change.Property == CaretWidthProperty ||
            change.Property == ShowCaretProperty)
        {
            InvalidateVisual();
        }
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
                    if (x >= maxWidth - 0.5)
                    {
                        y += lineHeight;
                        x = 0d;
                        if (y > maxHeight + lineHeight) return;
                    }

                    int count = FitCount(text, pos, typeface, FontSize, brush, maxWidth - x);
                    if (count == 0)
                    {
                        y += lineHeight;
                        x = 0d;
                        if (y > maxHeight + lineHeight) return;
                        continue;
                    }

                    string frag = text.Substring(pos, count);

                    // Measure fragment
                    var layout = new TextLayout(frag, typeface, FontSize, brush, TextAlignment.Left);
                    double w = layout.Width;

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
                            var pre = new TextLayout(frag[..ovStart], typeface, FontSize, brush, TextAlignment.Left);
                            x1 = pre.Width;
                        }

                        double wSel = 0;
                        if (ovEnd > ovStart)
                        {
                            var sel = new TextLayout(frag.Substring(ovStart, ovEnd - ovStart), typeface, FontSize, brush, TextAlignment.Left);
                            wSel = sel.Width;
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
                            var preCaret = new TextLayout(frag[..offset], typeface, FontSize, brush, TextAlignment.Left);
                            xCaret += preCaret.Width;
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

    public int GetCaretPositionFromPoint(Point point)
    {
        var raw = (Text ?? string.Empty).Replace("\r\n", "\n");
        if (string.IsNullOrEmpty(raw)) return 0;

        double maxWidth = Math.Max(0, Bounds.Width);
        if (maxWidth < 2) return 0;

        var brush = TextBrush ?? Brushes.Black;
        double lineHeight = LineSpacing > 0 ? LineSpacing : Math.Max(10, Math.Round(FontSize * 1.3));
        double visualTextHeight = Math.Max(8, Math.Round(FontSize * 1.0));
        double bottomGap = Math.Max(1, Math.Round(FontSize * 0.12));
        double y = TopMargin + FirstLineOffset - (visualTextHeight + bottomGap);
        if (double.IsNaN(y) || double.IsInfinity(y)) y = 0;

        var (lines, _) = ParseRunsWithMap(raw);

        // Find the line index based on Y coordinate
        int lineIndex = (int)Math.Floor((point.Y - y) / lineHeight);

        // If clicked below all text, place caret at the end of the document
        if (lineIndex >= lines.Count)
        {
            return raw.Length;
        }
        lineIndex = Math.Clamp(lineIndex, 0, lines.Count - 1);

        // Calculate the starting character index for the target line
        int rawCharIndex = 0;
        for (int i = 0; i < lineIndex; i++)
        {
            foreach (var run in lines[i])
            {
                rawCharIndex += run.Text.Length;
            }
            rawCharIndex++; // Account for newline character
        }

        var targetLine = lines[lineIndex];
        double x = 0;

        foreach (var run in targetLine)
        {
            var typeface = new Typeface(
                Typeface.Default.FontFamily,
                run.Italic ? FontStyle.Italic : FontStyle.Normal,
                run.Bold ? FontWeight.Bold : FontWeight.Normal
            );

            var layout = new TextLayout(run.Text, typeface, FontSize, brush, TextAlignment.Left);
            
            if (x <= point.X && point.X < x + layout.Width)
            {
                // Click is within this run, find the exact character
                for (int i = 0; i < run.Text.Length; i++)
                {
                    var subLayout = new TextLayout(run.Text.Substring(0, i + 1), typeface, FontSize, brush, TextAlignment.Left);
                    var charWidth = subLayout.Width - (i > 0 ? new TextLayout(run.Text.Substring(0, i), typeface, FontSize, brush, TextAlignment.Left).Width : 0);

                    if (x + subLayout.Width - (charWidth / 2) > point.X)
                    {
                        return Math.Clamp(rawCharIndex + i, 0, raw.Length);
                    }
                }
                // Click is in the second half of the last character of the run
                return Math.Clamp(rawCharIndex + run.Text.Length, 0, raw.Length);
            }
            
            x += layout.Width;
            rawCharIndex += run.Text.Length;
        }

        // Click was to the right of all text on the line, so place caret at the end of the line
        return Math.Clamp(rawCharIndex, 0, raw.Length);
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

        // Try wrap on whitespace
        int end = start + best;
        if (end < text.Length && !char.IsWhiteSpace(text[end - 1]) && !char.IsWhiteSpace(text[end]))
        {
            for (int i = end - 1; i > start; i--)
            {
                if (char.IsWhiteSpace(text[i]))
                    return i - start + 1;
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
