namespace ICYOU.SDK;

/// <summary>
/// Хранилище данных модуля
/// </summary>
public interface IModuleStorage
{
    /// <summary>
    /// Сохранить значение (синхронно)
    /// </summary>
    void Set<T>(string key, T value);
    
    /// <summary>
    /// Получить значение (синхронно)
    /// </summary>
    T Get<T>(string key, T defaultValue);
    
    /// <summary>
    /// Сохранить значение (асинхронно)
    /// </summary>
    Task SetAsync<T>(string key, T value);
    
    /// <summary>
    /// Получить значение (асинхронно)
    /// </summary>
    Task<T?> GetAsync<T>(string key);
    
    /// <summary>
    /// Удалить значение
    /// </summary>
    Task RemoveAsync(string key);
    
    /// <summary>
    /// Проверить наличие ключа
    /// </summary>
    Task<bool> ContainsKeyAsync(string key);
    
    /// <summary>
    /// Получить все ключи
    /// </summary>
    Task<IEnumerable<string>> GetKeysAsync();
}

