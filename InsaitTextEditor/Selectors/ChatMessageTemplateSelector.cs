using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Metadata;
using InsaitTextEditor.Models;

namespace InsaitTextEditor.Selectors;

public class ChatMessageTemplateSelector : IDataTemplate
{
    [Content]
    public Dictionary<string, IDataTemplate> Templates { get; } = new();

    public Control? Build(object? data)
    {
        if (data is ChatMessage message)
        {
            var key = message.Sender == "User" ? "User" : "Assistant";
            if (Templates.TryGetValue(key, out var template))
            {
                return template.Build(data);
            }
        }
        return null;
    }

    public bool Match(object? data)
    {
        return data is ChatMessage;
    }
}

