namespace ICYOU.SDK;

/// <summary>
/// Контекст выполнения модуля - предоставляет доступ к API мессенджера
/// </summary>
public interface IModuleContext
{
    /// <summary>
    /// Сервис для работы с сообщениями
    /// </summary>
    IMessageService MessageService { get; }
    
    /// <summary>
    /// Сервис для работы с пользователями
    /// </summary>
    IUserService UserService { get; }
    
    /// <summary>
    /// Сервис для работы с чатами
    /// </summary>
    IChatService ChatService { get; }
    
    /// <summary>
    /// Сервис для работы с файлами
    /// </summary>
    IFileService FileService { get; }
    
    /// <summary>
    /// Логгер модуля
    /// </summary>
    IModuleLogger Logger { get; }
    
    /// <summary>
    /// Хранилище настроек модуля
    /// </summary>
    IModuleStorage Storage { get; }
    
    /// <summary>
    /// Регистрация обработчика событий
    /// </summary>
    void RegisterEventHandler<T>(Action<T> handler) where T : ModuleEvent;
    
    /// <summary>
    /// Отмена регистрации обработчика
    /// </summary>
    void UnregisterEventHandler<T>(Action<T> handler) where T : ModuleEvent;
}

