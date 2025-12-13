using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace InsaitTextEditor.Controls;

public partial class TabsPanel : UserControl
{
    private StackPanel? _itemsHost;

    public TabsPanel()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _itemsHost = this.FindControl<StackPanel>("ItemsHost");
    }

    // Висота вкладки
    public static readonly StyledProperty<double> TabHeightProperty =
        AvaloniaProperty.Register<TabsPanel, double>(nameof(TabHeight), 50d);

    public double TabHeight
    {
        get => GetValue(TabHeightProperty);
        set => SetValue(TabHeightProperty, value);
    }

    // Розмір кнопки "додати" (ширина = висота)
    public static readonly StyledProperty<double> AddButtonSizeProperty =
        AvaloniaProperty.Register<TabsPanel, double>(nameof(AddButtonSize), 40d);

    public double AddButtonSize
    {
        get => GetValue(AddButtonSizeProperty);
        set => SetValue(AddButtonSizeProperty, value);
    }

    // API для керування вкладками
    public void AddTab(DocumentTab tab)
    {
        if (_itemsHost is null) return;
        tab.Height = TabHeight;
        _itemsHost.Children.Add(tab);
    }

    public void RemoveTab(DocumentTab tab)
    {
        if (_itemsHost is null) return;
        _itemsHost.Children.Remove(tab);
    }

    // Подія: запит на додавання вкладки
    public event EventHandler? AddTabRequested;

    private void OnAddTabClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => AddTabRequested?.Invoke(this, EventArgs.Empty);

    public event EventHandler<DocumentTab>? TabDragged;

    private Point? _dragStartPoint;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Source is DocumentTab tab)
        {
            _dragStartPoint = e.GetPosition(this);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragStartPoint.HasValue && e.Source is DocumentTab tab)
        {
            var currentPoint = e.GetPosition(this);
            if (Math.Abs(currentPoint.X - _dragStartPoint.Value.X) > 10 ||
                Math.Abs(currentPoint.Y - _dragStartPoint.Value.Y) > 10)
            {
                // Open a new window with the dragged tab
                var newWindow = new MainWindow();
                newWindow.Show();

                // Optionally, transfer the tab's content to the new window
                // (Implementation depends on how tabs and their content are managed)

                TabDragged?.Invoke(this, tab);
                _dragStartPoint = null;
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragStartPoint = null;
    }
}