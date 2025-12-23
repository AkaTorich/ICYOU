namespace ICYOU.SDK;

/// <summary>
/// Интерфейс для модулей с настройками
/// </summary>
public interface IModuleSettings
{
    /// <summary>
    /// Получить список настроек модуля
    /// </summary>
    IEnumerable<ModuleSetting> GetSettings();
    
    /// <summary>
    /// Применить настройку
    /// </summary>
    void ApplySetting(string key, object value);
}

/// <summary>
/// Описание одной настройки модуля
/// </summary>
public class ModuleSetting
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ModuleSettingType Type { get; set; }
    public object? CurrentValue { get; set; }
    public object? DefaultValue { get; set; }
    public object[]? Options { get; set; } // Для выпадающих списков
}

public enum ModuleSettingType
{
    Boolean,
    String,
    Integer,
    Choice,
    Password
}

