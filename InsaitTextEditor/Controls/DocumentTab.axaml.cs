using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace InsaitTextEditor.Controls;

public partial class DocumentTab : UserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<DocumentTab, string?>(nameof(Title));

    public static readonly StyledProperty<IBrush?> TabBackgroundProperty =
        AvaloniaProperty.Register<DocumentTab, IBrush?>(nameof(TabBackground));

    public static readonly StyledProperty<IImage?> IconProperty =
        AvaloniaProperty.Register<DocumentTab, IImage?>(nameof(Icon));

    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public IBrush? TabBackground
    {
        get => GetValue(TabBackgroundProperty);
        set => SetValue(TabBackgroundProperty, value);
    }

    public IImage? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly RoutedEvent<RoutedEventArgs> CloseRequestedEvent =
        RoutedEvent.Register<DocumentTab, RoutedEventArgs>(nameof(CloseRequested), RoutingStrategies.Bubble);

    public event EventHandler<RoutedEventArgs>? CloseRequested
    {
        add => AddHandler(CloseRequestedEvent, value);
        remove => RemoveHandler(CloseRequestedEvent, value);
    }

    public DocumentTab()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(CloseRequestedEvent));
    }
}