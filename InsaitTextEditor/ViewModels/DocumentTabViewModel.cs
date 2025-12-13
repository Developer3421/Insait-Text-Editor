using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using InsaitTextEditor.Services;

namespace InsaitTextEditor.ViewModels;

public class DocumentTabViewModel : INotifyPropertyChanged
{
    private string _title = LocalizationService.GetString("Key.NewPage", "New page");
    private IImage? _icon;
    private IBrush _tabBackground = Brushes.Transparent;
    private string _documentText = string.Empty;
    private bool _isActive;

    public Guid Id { get; init; } = Guid.NewGuid();

    public string Title
    {
        get => _title;
        set => SetField(ref _title, value);
    }

    public IImage? Icon
    {
        get => _icon;
        set => SetField(ref _icon, value);
    }

    public IBrush TabBackground
    {
        get => _tabBackground;
        set => SetField(ref _tabBackground, value);
    }

    public string DocumentText
    {
        get => _documentText;
        set => SetField(ref _documentText, value);
    }

    // Стан активності вкладки
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (SetField(ref _isActive, value))
            {
                // Повідомляємо про зміну похідних властивостей для XAML-прив’язок
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TabBorderBrush)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TabBorderThickness)));
            }
        }
    }

    // Похідні властивості для стилізації (без тригерів)
    public IBrush TabBorderBrush => IsActive ? new SolidColorBrush(Color.Parse("#FF8C00")) : Brushes.Transparent;
    public Thickness TabBorderThickness => IsActive ? new Thickness(2) : new Thickness(0);

    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        return true;
    }
}