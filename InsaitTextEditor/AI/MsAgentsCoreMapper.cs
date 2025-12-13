// filepath: InsaitTextEditor/AI/MsAgentsCoreMapper.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using InsaitTextEditor.Models;
using InsaitTextEditor.Services; // for AssistantConfig.Name

namespace InsaitTextEditor.AI;

/// <summary>
/// Mapper між внутрішніми моделями та типами з Microsoft.Agents.Core (через рефлексію).
/// Без compile-time залежності від конкретних класів у пакеті.
/// </summary>
public static class MsAgentsCoreMapper
{
    private static readonly string[] MessageTypeHints =
    {
        // Імовірні назви типів повідомлень у Microsoft.Agents.Core
        "Message", "ChatMessage", "AgentMessage", "Activity"
    };

    private static readonly string[] ContentPropertyHints = { "Content", "Text", "Value", "Body" };
    private static readonly string[] SenderPropertyHints = { "Role", "Author", "From", "Sender", "Name" };
    private static readonly string[] TimestampPropertyHints = { "Timestamp", "CreatedAt", "Time" };

    public static object? TryCreateCoreMessage(ChatMessage message)
    {
        var asm = LoadMsAgentsCoreAssembly();
        if (asm == null) return null;

        var msgType = FindMessageType(asm);
        if (msgType == null) return null;

        // Спроба створення екземпляра
        object? instance = null;
        var ctors = msgType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        // Перевагу надаємо безпараметровому
        var defaultCtor = ctors.FirstOrDefault(c => c.GetParameters().Length == 0);
        if (defaultCtor != null)
        {
            instance = Activator.CreateInstance(msgType);
        }
        else
        {
            // Спробувати найпростіші конструктори зі строковими параметрами
            var ctor = ctors.OrderBy(c => c.GetParameters().Length).FirstOrDefault();
            if (ctor != null)
            {
                var prms = ctor.GetParameters();
                var args = new object?[prms.Length];
                for (int i = 0; i < prms.Length; i++)
                {
                    var p = prms[i];
                    if (p.ParameterType == typeof(string))
                    {
                        // Першу строку віддамо як контент, інші — як роль/автор
                        args[i] = i == 0 ? message.Content : NormalizeRole(message.Sender);
                    }
                    else if (p.ParameterType == typeof(DateTime) || p.ParameterType == typeof(DateTime?))
                    {
                        args[i] = message.Timestamp;
                    }
                    else
                    {
                        args[i] = p.HasDefaultValue ? p.DefaultValue : GetDefault(p.ParameterType);
                    }
                }
                instance = ctor.Invoke(args);
            }
        }

        if (instance == null) return null;

        // Встановити основні властивості якщо існують
        SetFirstStringProperty(instance, ContentPropertyHints, message.Content);
        SetFirstStringProperty(instance, SenderPropertyHints, NormalizeRole(message.Sender));
        SetFirstDateTimeProperty(instance, TimestampPropertyHints, message.Timestamp);

        return instance;
    }

    public static List<object> ToCoreMessages(IEnumerable<ChatMessage> history)
    {
        var list = new List<object>();
        foreach (var m in history)
        {
            var obj = TryCreateCoreMessage(m);
            if (obj != null)
                list.Add(obj);
        }
        return list;
    }

    public static ChatMessage FromCoreMessage(object msMessage)
    {
        var content = GetFirstStringProperty(msMessage, ContentPropertyHints) ?? string.Empty;
        var sender = GetFirstStringProperty(msMessage, SenderPropertyHints) ?? "Assistant";
        var ts = GetFirstDateTimeProperty(msMessage, TimestampPropertyHints) ?? DateTime.UtcNow;

        return new ChatMessage
        {
            Sender = DenormalizeRole(sender),
            Content = content,
            Timestamp = ts
        };
    }

    private static Assembly? LoadMsAgentsCoreAssembly()
    {
        try
        {
            // Спробувати знайти серед завантажених збірок
            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "Microsoft.Agents.Core", StringComparison.OrdinalIgnoreCase));
            if (loaded != null) return loaded;

            // Спробувати завантажити за ім'ям (має бути у probing paths .NET)
            return Assembly.Load("Microsoft.Agents.Core");
        }
        catch
        {
            return null;
        }
    }

    private static Type? FindMessageType(Assembly asm)
    {
        try
        {
            var types = asm.GetTypes();
            // Шукаємо клас з підходящою назвою та публічним конструктором
            return types.FirstOrDefault(t =>
                t.IsClass && !t.IsAbstract &&
                t.IsPublic &&
                t.Namespace != null && t.Namespace.StartsWith("Microsoft.Agents", StringComparison.OrdinalIgnoreCase) &&
                MessageTypeHints.Any(h => t.Name.Contains(h, StringComparison.OrdinalIgnoreCase)));
        }
        catch
        {
            return null;
        }
    }

    private static void SetFirstStringProperty(object instance, string[] propertyNames, string value)
    {
        var type = instance.GetType();
        foreach (var name in propertyNames)
        {
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(string))
            {
                prop.SetValue(instance, value);
                return;
            }
        }
    }

    private static void SetFirstDateTimeProperty(object instance, string[] propertyNames, DateTime value)
    {
        var type = instance.GetType();
        foreach (var name in propertyNames)
        {
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null && prop.CanWrite)
            {
                if (prop.PropertyType == typeof(DateTime))
                {
                    prop.SetValue(instance, value);
                    return;
                }
                if (prop.PropertyType == typeof(DateTime?))
                {
                    prop.SetValue(instance, (DateTime?)value);
                    return;
                }
            }
        }
    }

    private static string? GetFirstStringProperty(object instance, string[] propertyNames)
    {
        var type = instance.GetType();
        foreach (var name in propertyNames)
        {
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null && prop.CanRead && prop.PropertyType == typeof(string))
            {
                var val = (string?)prop.GetValue(instance);
                if (!string.IsNullOrEmpty(val))
                    return val;
            }
        }
        return null;
    }

    private static DateTime? GetFirstDateTimeProperty(object instance, string[] propertyNames)
    {
        var type = instance.GetType();
        foreach (var name in propertyNames)
        {
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop != null && prop.CanRead)
            {
                if (prop.PropertyType == typeof(DateTime))
                {
                    return (DateTime?)prop.GetValue(instance);
                }
                if (prop.PropertyType == typeof(DateTime?))
                {
                    return (DateTime?)prop.GetValue(instance);
                }
            }
        }
        return null;
    }

    private static string NormalizeRole(string sender)
    {
        return sender.Equals("User", StringComparison.OrdinalIgnoreCase) ? "user" : "assistant";
    }

    private static string DenormalizeRole(string role)
    {
        return role.Equals("user", StringComparison.OrdinalIgnoreCase) ? "User" : AssistantConfig.Name;
    }

    private static object? GetDefault(Type t) => t.IsValueType ? Activator.CreateInstance(t) : null;
}
