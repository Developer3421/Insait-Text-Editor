using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using InsaitTextEditor.Models;

namespace InsaitTextEditor.Scripts.SkiaSharp;

public class RuledBackground : Control
{
    static RuledBackground()
    {
        AffectsRender<RuledBackground>(
            LineSpacingProperty,
            LineThicknessProperty,
            LineBrushProperty,
            LeftMarginProperty,
            RightMarginProperty,
            TopMarginProperty,
            BottomMarginProperty,
            FirstLineOffsetProperty,
            DrawVerticalMarginLineProperty,
            VerticalMarginLineBrushProperty,
            PaperBrushProperty,
            BackgroundModeProperty
        );
    }

    public static readonly StyledProperty<double> LineSpacingProperty =
        AvaloniaProperty.Register<RuledBackground, double>(nameof(LineSpacing), 28d);

    public static readonly StyledProperty<double> LineThicknessProperty =
        AvaloniaProperty.Register<RuledBackground, double>(nameof(LineThickness), 2.5d);

    public static readonly StyledProperty<IBrush> LineBrushProperty =
        AvaloniaProperty.Register<RuledBackground, IBrush>(nameof(LineBrush),
            new SolidColorBrush(Color.FromRgb(0x6A, 0x6A, 0x6A)));

    // Лінія майже біля лівого краю
    public static readonly StyledProperty<double> LeftMarginProperty =
        AvaloniaProperty.Register<RuledBackground, double>(nameof(LeftMargin), 2d);

    public static readonly StyledProperty<double> RightMarginProperty =
        AvaloniaProperty.Register<RuledBackground, double>(nameof(RightMargin), 16d);

    // Без верхнього/нижнього полів — лінії на всю висоту
    public static readonly StyledProperty<double> TopMarginProperty =
        AvaloniaProperty.Register<RuledBackground, double>(nameof(TopMargin), 0d);

    public static readonly StyledProperty<double> BottomMarginProperty =
        AvaloniaProperty.Register<RuledBackground, double>(nameof(BottomMargin), 0d);

    public static readonly StyledProperty<double> FirstLineOffsetProperty =
        AvaloniaProperty.Register<RuledBackground, double>(nameof(FirstLineOffset), 0d);

    public static readonly StyledProperty<bool> DrawVerticalMarginLineProperty =
        AvaloniaProperty.Register<RuledBackground, bool>(nameof(DrawVerticalMarginLine), true);

    public static readonly StyledProperty<IBrush> VerticalMarginLineBrushProperty =
        AvaloniaProperty.Register<RuledBackground, IBrush>(nameof(VerticalMarginLineBrush),
            new SolidColorBrush(Color.FromRgb(0x9A, 0x9A, 0x9A)));

    public static readonly StyledProperty<IBrush> PaperBrushProperty =
        AvaloniaProperty.Register<RuledBackground, IBrush>(nameof(PaperBrush),
            new SolidColorBrush(Color.FromRgb(0xFF, 0xFD, 0xF7)));

    public static readonly StyledProperty<PageBackgroundMode> BackgroundModeProperty =
        AvaloniaProperty.Register<RuledBackground, PageBackgroundMode>(nameof(BackgroundMode), PageBackgroundMode.Lined);

    public double LineSpacing { get => GetValue(LineSpacingProperty); set => SetValue(LineSpacingProperty, value); }
    public double LineThickness { get => GetValue(LineThicknessProperty); set => SetValue(LineThicknessProperty, value); }
    public IBrush LineBrush { get => GetValue(LineBrushProperty); set => SetValue(LineBrushProperty, value); }
    public double LeftMargin { get => GetValue(LeftMarginProperty); set => SetValue(LeftMarginProperty, value); }
    public double RightMargin { get => GetValue(RightMarginProperty); set => SetValue(RightMarginProperty, value); }
    public double TopMargin { get => GetValue(TopMarginProperty); set => SetValue(TopMarginProperty, value); }
    public double BottomMargin { get => GetValue(BottomMarginProperty); set => SetValue(BottomMarginProperty, value); }
    public double FirstLineOffset { get => GetValue(FirstLineOffsetProperty); set => SetValue(FirstLineOffsetProperty, value); }
    public bool DrawVerticalMarginLine { get => GetValue(DrawVerticalMarginLineProperty); set => SetValue(DrawVerticalMarginLineProperty, value); }
    public IBrush VerticalMarginLineBrush { get => GetValue(VerticalMarginLineBrushProperty); set => SetValue(VerticalMarginLineBrushProperty, value); }
    public IBrush PaperBrush { get => GetValue(PaperBrushProperty); set => SetValue(PaperBrushProperty, value); }
    public PageBackgroundMode BackgroundMode { get => GetValue(BackgroundModeProperty); set => SetValue(BackgroundModeProperty, value); }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var rect = Bounds;
        if (rect.Width <= 0 || rect.Height <= 0) return;

        // Фон "паперу"
        context.DrawRectangle(PaperBrush, pen: null, rect);

        var pen = new Pen(LineBrush, LineThickness);
        var vPen = new Pen(VerticalMarginLineBrush, LineThickness);

        double left = LeftMargin;
        double right = rect.Width - RightMargin;
        double yStart = TopMargin + FirstLineOffset;
        double yEnd = rect.Height - BottomMargin;

        // Горизонтальні лінії
        for (double y = yStart; y <= yEnd; y += LineSpacing)
        {
            context.DrawLine(pen, new Point(left, y), new Point(right, y));
        }
        // Закриваємо нижню межу, якщо крок не співпав рівно з yEnd
        if (LineSpacing > 0)
        {
            double steps = Math.Floor((yEnd - yStart) / LineSpacing);
            double lastY = yStart + steps * LineSpacing;
            if (Math.Abs(lastY - yEnd) > 0.1)
            {
                context.DrawLine(pen, new Point(left, yEnd), new Point(right, yEnd));
            }
        }

        if (BackgroundMode == PageBackgroundMode.Grid)
        {
            // Вертикальні лінії сітки (клітинки квадратні: крок = LineSpacing)
            for (double x = left; x <= right; x += LineSpacing)
            {
                context.DrawLine(pen, new Point(x, TopMargin), new Point(x, rect.Height - BottomMargin));
            }
            // Гарантуємо праву межу, якщо не співпала по кроку
            if (LineSpacing > 0)
            {
                double vSteps = Math.Floor((right - left) / LineSpacing);
                double lastX = left + vSteps * LineSpacing;
                if (Math.Abs(lastX - right) > 0.1)
                {
                    context.DrawLine(pen, new Point(right, TopMargin), new Point(right, rect.Height - BottomMargin));
                }
            }
        }

        // Вертикальна лінія поля — дозволяємо на X=0
        if (DrawVerticalMarginLine && left >= 0 && left < rect.Width)
        {
            context.DrawLine(vPen, new Point(left, TopMargin), new Point(left, rect.Height - BottomMargin));
        }
    }
}