namespace ICYOU.SDK;

/// <summary>
/// Основной интерфейс модуля мессенджера
/// </summary>
public interface IModule
{
    /// <summary>
    /// Уникальный идентификатор модуля
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// Название модуля
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Версия модуля
    /// </summary>
    string Version { get; }
    
    /// <summary>
    /// Автор модуля
    /// </summary>
    string Author { get; }
    
    /// <summary>
    /// Описание модуля
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Инициализация модуля
    /// </summary>
    void Initialize(IModuleContext context);
    
    /// <summary>
    /// Выгрузка модуля
    /// </summary>
    void Shutdown();
}

