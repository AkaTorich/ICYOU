namespace ICYOU.SDK;

/// <summary>
/// Базовый класс для модулей - упрощает создание модулей
/// </summary>
public abstract class ModuleBase : IModule
{
    protected IModuleContext Context { get; private set; } = null!;
    protected IModuleLogger Logger => Context.Logger;
    protected IMessageService Messages => Context.MessageService;
    protected IUserService Users => Context.UserService;
    protected IChatService Chats => Context.ChatService;
    protected IFileService Files => Context.FileService;
    protected IModuleStorage Storage => Context.Storage;
    
    public virtual string Id => GetModuleInfo()?.Id ?? GetType().Name;
    public virtual string Name => GetModuleInfo()?.Name ?? GetType().Name;
    public virtual string Version => GetModuleInfo()?.Version ?? "1.0.0";
    public virtual string Author => GetModuleInfo()?.Author ?? "Unknown";
    public virtual string Description => GetModuleInfo()?.Description ?? string.Empty;
    
    private ModuleInfoAttribute? GetModuleInfo()
    {
        return GetType().GetCustomAttributes(typeof(ModuleInfoAttribute), false)
            .FirstOrDefault() as ModuleInfoAttribute;
    }
    
    public void Initialize(IModuleContext context)
    {
        Context = context;
        OnInitialize();
    }
    
    public void Shutdown()
    {
        OnShutdown();
    }
    
    /// <summary>
    /// Вызывается при инициализации модуля
    /// </summary>
    protected abstract void OnInitialize();
    
    /// <summary>
    /// Вызывается при выгрузке модуля
    /// </summary>
    protected virtual void OnShutdown() { }
    
    /// <summary>
    /// Подписаться на событие
    /// </summary>
    protected void Subscribe<T>(Action<T> handler) where T : ModuleEvent
    {
        Context.RegisterEventHandler(handler);
    }
    
    /// <summary>
    /// Отписаться от события
    /// </summary>
    protected void Unsubscribe<T>(Action<T> handler) where T : ModuleEvent
    {
        Context.UnregisterEventHandler(handler);
    }
}

