using System;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;

namespace InsaitTextEditor.Services;

public enum AppLanguage
{
    En,
    De,
    Ru,
    Uk,
    Tr
}

public static class LocalizationService
{
    private static readonly Uri EnUri = new("avares://InsaitTextEditor/UI/Localization/Strings.en.axaml");
    private static readonly Uri DeUri = new("avares://InsaitTextEditor/UI/Localization/Strings.de.axaml");
    private static readonly Uri RuUri = new("avares://InsaitTextEditor/UI/Localization/Strings.ru.axaml");
    private static readonly Uri UkUri = new("avares://InsaitTextEditor/UI/Localization/Strings.uk.axaml");
    private static readonly Uri TrUri = new("avares://InsaitTextEditor/UI/Localization/Strings.tr.axaml");

    private static ResourceInclude? _currentDict;
    public static AppLanguage CurrentLanguage { get; private set; } = AppLanguage.En;

    public static void Initialize(AppLanguage? initial = null)
    {
        var lang = initial ?? CurrentLanguage;
        ApplyLanguage(lang);
    }

    public static void ChangeLanguage(AppLanguage lang)
    {
        if (lang == CurrentLanguage) return;
        ApplyLanguage(lang);
        LanguageChanged?.Invoke();
        try { SettingsService.SaveLanguage(lang); } catch { /* ignore */ }
    }

    public static string GetString(string key, string fallback)
    {
        var app = Application.Current;
        if (app != null && app.Resources.TryGetResource(key, app.ActualThemeVariant, out var value) && value is string s && !string.IsNullOrEmpty(s))
            return s;
        return fallback;
    }

    private static void ApplyLanguage(AppLanguage lang)
    {
        var app = Application.Current;
        if (app is null) return;

        var dict = new ResourceInclude(new Uri("avares://InsaitTextEditor/"))
        {
            Source = lang switch
            {
                AppLanguage.De => DeUri,
                AppLanguage.Ru => RuUri,
                AppLanguage.Uk => UkUri,
                AppLanguage.Tr => TrUri,
                _ => EnUri
            }
        };

        // Remove previous one if any
        if (_currentDict != null)
        {
            app.Resources.MergedDictionaries.Remove(_currentDict);
        }

        app.Resources.MergedDictionaries.Add(dict);
        _currentDict = dict;
        CurrentLanguage = lang;
    }

    public static event Action? LanguageChanged;
}
