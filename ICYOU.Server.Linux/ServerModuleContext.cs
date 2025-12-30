using ICYOU.SDK;

namespace ICYOU.Server;

public class ServerModuleContext : IModuleContext
{
    public IMessageService MessageService { get; }
    public IUserService UserService { get; }
    public IChatService ChatService { get; }
    public IFileService FileService { get; }
    public IModuleLogger Logger { get; }
    public IModuleStorage Storage { get; }
    
    private readonly Dictionary<Type, List<Delegate>> _eventHandlers = new();
    private readonly object _handlersLock = new();
    
    public ServerModuleContext(
        IMessageService messageService,
        IUserService userService,
        IChatService chatService,
        IFileService fileService,
        IModuleLogger logger,
        IModuleStorage storage)
    {
        MessageService = messageService;
        UserService = userService;
        ChatService = chatService;
        FileService = fileService;
        Logger = logger;
        Storage = storage;
    }
    
    public void RegisterEventHandler<T>(Action<T> handler) where T : ModuleEvent
    {
        lock (_handlersLock)
        {
            var type = typeof(T);
            if (!_eventHandlers.ContainsKey(type))
                _eventHandlers[type] = new List<Delegate>();
            _eventHandlers[type].Add(handler);
        }
    }
    
    public void UnregisterEventHandler<T>(Action<T> handler) where T : ModuleEvent
    {
        lock (_handlersLock)
        {
            var type = typeof(T);
            if (_eventHandlers.ContainsKey(type))
                _eventHandlers[type].Remove(handler);
        }
    }
    
    public void RaiseEvent<T>(T evt) where T : ModuleEvent
    {
        lock (_handlersLock)
        {
            var type = typeof(T);
            if (_eventHandlers.TryGetValue(type, out var handlers))
            {
                foreach (var handler in handlers.ToList())
                {
                    try
                    {
                        ((Action<T>)handler)(evt);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error in event handler: {ex.Message}", ex);
                    }
                }
            }
        }
    }
}

public class ServerModuleLogger : IModuleLogger
{
    private readonly string _moduleName;
    
    public ServerModuleLogger(string moduleName)
    {
        _moduleName = moduleName;
    }
    
    public void Debug(string message) => Log("DEBUG", message);
    public void Info(string message) => Log("INFO", message);
    public void Warning(string message) => Log("WARN", message);
    public void Error(string message, Exception? ex = null)
    {
        Log("ERROR", message);
        if (ex != null)
            Console.WriteLine($"  Exception: {ex}");
    }
    
    private void Log(string level, string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{level}] [{_moduleName}] {message}");
    }
}

