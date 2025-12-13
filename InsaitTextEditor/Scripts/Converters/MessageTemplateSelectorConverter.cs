using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data.Converters;
using InsaitTextEditor.Models;

namespace InsaitTextEditor.Scripts.Converters;

/// <summary>
/// Конвертер для вибору шаблону повідомлення на основі відправника
/// </summary>
public class MessageTemplateSelectorConverter : IValueConverter
{
    public IDataTemplate? UserMessageTemplate { get; set; }
    public IDataTemplate? AssistantMessageTemplate { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ChatMessage message)
            return null;

        return message.Sender == "User" 
            ? UserMessageTemplate 
            : AssistantMessageTemplate;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

