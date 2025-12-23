using System.Windows;

namespace ICYOU.Client.Services;

public enum AppTheme
{
    Dark,
    Light
}

public class ThemeService
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();
    
    public AppTheme CurrentTheme { get; private set; } = AppTheme.Dark;
    
    public void ApplyTheme(AppTheme theme)
    {
        CurrentTheme = theme;
        
        var app = Application.Current;
        var mergedDicts = app.Resources.MergedDictionaries;
        
        // Удаляем старую тему
        ResourceDictionary? oldTheme = null;
        foreach (var dict in mergedDicts)
        {
            var source = dict.Source?.ToString() ?? "";
            if (source.Contains("DarkTheme.xaml") || source.Contains("LightTheme.xaml") || source.Contains("Theme.xaml"))
            {
                oldTheme = dict;
                break;
            }
        }
        
        if (oldTheme != null)
        {
            mergedDicts.Remove(oldTheme);
        }
        
        // Добавляем новую тему
        var themeFile = theme == AppTheme.Light ? "LightTheme.xaml" : "DarkTheme.xaml";
        var newTheme = new ResourceDictionary
        {
            Source = new Uri($"Styles/{themeFile}", UriKind.Relative)
        };
        mergedDicts.Add(newTheme);
        
        // Сохраняем в настройки
        SettingsService.Instance.Settings.Theme = theme.ToString();
        SettingsService.Instance.Save();
    }
    
    public void LoadSavedTheme()
    {
        var themeName = SettingsService.Instance.Settings.Theme;
        var theme = themeName == "Light" ? AppTheme.Light : AppTheme.Dark;
        ApplyTheme(theme);
    }
}

