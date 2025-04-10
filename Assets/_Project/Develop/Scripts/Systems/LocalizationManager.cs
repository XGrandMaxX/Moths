using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization.Settings;

public class LocalizationManager
{
    private static readonly Lazy<LocalizationManager> instance = new(() => new LocalizationManager());
    public static LocalizationManager Instance => instance.Value;

    public event Action<string> OnLanguageChanged;

    private LocalizationManager()
    {
        string savedLanguage = PlayerPrefs.GetString("SelectedLanguage", GetSystemLanguageCode());
        SetLanguageAsync(savedLanguage).Forget();
    }

    /// <summary>
    /// Установить язык по коду (например, "en", "ru").
    /// </summary>
    public async UniTask SetLanguageAsync(string languageCode)
    {
        await SetLocaleAsync(languageCode);

        PlayerPrefs.SetString("SelectedLanguage", languageCode);
        PlayerPrefs.Save();

        OnLanguageChanged?.Invoke(languageCode);
    }

    /// <summary>
    /// Получить текущий код языка (например, "en", "ru").
    /// </summary>
    /// 
    public string GetCurrentLanguage() => LocalizationSettings.SelectedLocale.Identifier.Code;

    /// <summary>
    /// Получить список доступных языков.
    /// </summary>
    public List<string> GetAvailableLanguages()
    {
        List<string> languages = new();
        foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
        {
            languages.Add(locale.Identifier.Code);
        }
        return languages;
    }

    /// <summary>
    /// Переключить язык на следующий по списку.
    /// </summary>
    public void SwitchToNextLanguage()
    {
        var languages = GetAvailableLanguages();
        string currentLanguage = GetCurrentLanguage();
        int index = languages.IndexOf(currentLanguage);

        if (index >= 0)
        {
            int nextIndex = (index + 1) % languages.Count;
            SetLanguageAsync(languages[nextIndex]).Forget();
        }
    }

    /// <summary>
    /// Получить код системного языка устройства.
    /// </summary>
    public string GetSystemLanguageCode()
    {
        SystemLanguage systemLanguage = Application.systemLanguage;
        return systemLanguage switch
        {
            SystemLanguage.Russian => "ru",
            SystemLanguage.English => "en",
            SystemLanguage.Spanish => "es",
            SystemLanguage.French => "fr",
            _ => "en"
        };
    }

    private async UniTask SetLocaleAsync(string localeCode)
    {
        await LocalizationSettings.InitializationOperation.Task;

        var locale = LocalizationSettings.AvailableLocales.Locales.Find(l => l.Identifier.Code == localeCode);

        if (locale != null) LocalizationSettings.SelectedLocale = locale;
    }
}
