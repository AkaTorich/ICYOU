using System.IO;
using System.Reflection;
using Microsoft.Maui.Storage;
using ICYOU.SDK;

namespace ICYOU.Mobile.Services;

public class ModuleManager
{
    private static ModuleManager? _instance;
    public static ModuleManager Instance => _instance ??= new ModuleManager();

    private readonly List<IModule> _modules = new();
    private readonly List<Func<Message, Message?>> _incomingInterceptors = new();
    private readonly List<Func<Message, Message?>> _outgoingInterceptors = new();
    
    public IReadOnlyList<IModule> Modules => _modules;
    
    private ModuleManager()
    {
    }
    
    /// <summary>
    /// Обработка входящего сообщения через все модули
    /// </summary>
    public Message? ProcessIncomingMessage(Message message)
    {
        Message? result = message;
        foreach (var interceptor in _incomingInterceptors)
        {
            if (result == null) break;
            try
            {
                result = interceptor(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в интерсепторе: {ex.Message}");
            }
        }
        return result;
    }
    
    /// <summary>
    /// Обработка исходящего сообщения через все модули
    /// </summary>
    public Message? ProcessOutgoingMessage(Message message)
    {
        Message? result = message;
        foreach (var interceptor in _outgoingInterceptors)
        {
            if (result == null) break;
            try
            {
                result = interceptor(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка в интерсепторе: {ex.Message}");
            }
        }
        return result;
    }
    
    internal void RegisterIncomingInterceptor(Func<Message, Message?> interceptor)
    {
        _incomingInterceptors.Add(interceptor);
    }
    
    internal void RegisterOutgoingInterceptor(Func<Message, Message?> interceptor)
    {
        _outgoingInterceptors.Add(interceptor);
    }
    
    public void LoadModules()
    {
        _modules.Clear();

        var moduleNames = new[] { "ICYOU.Modules.E2E.dll", "ICYOU.Modules.Quote.dll", "ICYOU.Modules.LinkPreview.dll" };

        foreach (var moduleName in moduleNames)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ModuleManager] Загрузка модуля {moduleName}...");

                // Загружаем модуль напрямую из assets
                using var stream = FileSystem.OpenAppPackageFileAsync($"modules/{moduleName}").GetAwaiter().GetResult();
                if (stream == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ModuleManager] Не удалось открыть {moduleName}");
                    continue;
                }

                // Читаем DLL в память
                using var memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                var assemblyBytes = memoryStream.ToArray();

                // Загружаем assembly из байтов
                var assembly = Assembly.Load(assemblyBytes);
                System.Diagnostics.Debug.WriteLine($"[ModuleManager] Assembly {moduleName} загружен");

                // Ищем и создаем экземпляры модулей
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IModule).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                    {
                        var module = (IModule?)Activator.CreateInstance(type);
                        if (module != null)
                        {
                            _modules.Add(module);
                            System.Diagnostics.Debug.WriteLine($"[ModuleManager] Создан экземпляр модуля: {module.Name}");

                            // Инициализируем модуль
                            try
                            {
                                var context = new ClientModuleContext(module.Id);
                                module.Initialize(context);
                                System.Diagnostics.Debug.WriteLine($"[ModuleManager] Модуль {module.Name} инициализирован");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[ModuleManager] Ошибка инициализации модуля {module.Name}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModuleManager] Ошибка загрузки {moduleName}: {ex.Message}\n{ex.StackTrace}");
            }
        }

        System.Diagnostics.Debug.WriteLine($"[ModuleManager] Всего загружено модулей: {_modules.Count}");
    }
    
    public List<ModuleInfo> GetModuleInfos()
    {
        return _modules.Select(m => new ModuleInfo
        {
            Id = m.Id,
            Name = m.Name,
            Version = m.Version,
            Author = m.Author,
            Description = m.Description,
            HasSettings = m is IModuleSettings
        }).ToList();
    }
    
    public IModuleSettings? GetModuleSettings(string moduleId)
    {
        var module = _modules.FirstOrDefault(m => m.Id == moduleId);
        return module as IModuleSettings;
    }
    
    public void UnloadModules()
    {
        foreach (var module in _modules)
        {
            try
            {
                module.Shutdown();
            }
            catch { }
        }
        _modules.Clear();
    }
}

public class ModuleInfo
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string Description { get; set; } = "";
    public bool HasSettings { get; set; }
}

// Простой контекст для клиентских модулей
internal class ClientModuleContext : IModuleContext
{
    private readonly Dictionary<Type, List<Delegate>> _eventHandlers = new();
    
    public IMessageService MessageService { get; }
    public IUserService UserService { get; }
    public IChatService ChatService { get; }
    public IFileService FileService { get; }
    public IModuleLogger Logger { get; }
    public IModuleStorage Storage { get; }
    
    public ClientModuleContext(string moduleId)
    {
        Logger = new ClientModuleLogger(moduleId);
        Storage = new ClientModuleStorage(moduleId);
        MessageService = new DummyMessageService();
        UserService = new DummyUserService();
        ChatService = new DummyChatService();
        FileService = new DummyFileService();
    }
    
    public void RegisterEventHandler<T>(Action<T> handler) where T : ModuleEvent
    {
        var type = typeof(T);
        if (!_eventHandlers.ContainsKey(type))
            _eventHandlers[type] = new List<Delegate>();
        _eventHandlers[type].Add(handler);
    }
    
    public void UnregisterEventHandler<T>(Action<T> handler) where T : ModuleEvent
    {
        var type = typeof(T);
        if (_eventHandlers.ContainsKey(type))
            _eventHandlers[type].Remove(handler);
    }
}

internal class ClientModuleLogger : IModuleLogger
{
    private readonly string _prefix;
    public ClientModuleLogger(string prefix) => _prefix = prefix;
    
    public void Info(string message) => System.Diagnostics.Debug.WriteLine($"[{_prefix}] INFO: {message}");
    public void Warning(string message) => System.Diagnostics.Debug.WriteLine($"[{_prefix}] WARN: {message}");
    public void Error(string message, Exception? ex = null) => 
        System.Diagnostics.Debug.WriteLine($"[{_prefix}] ERROR: {message}" + (ex != null ? $"\n{ex}" : ""));
    public void Debug(string message) => System.Diagnostics.Debug.WriteLine($"[{_prefix}] DEBUG: {message}");
}

internal class ClientModuleStorage : IModuleStorage
{
    private readonly Dictionary<string, object> _cache = new();
    
    public ClientModuleStorage(string moduleId) { }
    
    // Синхронные методы
    public void Set<T>(string key, T value) => _cache[key] = value!;
    
    public T Get<T>(string key, T defaultValue)
    {
        if (_cache.TryGetValue(key, out var value))
            return (T)value;
        return defaultValue;
    }
    
    // Асинхронные методы
    public Task<T?> GetAsync<T>(string key)
    {
        if (_cache.TryGetValue(key, out var value))
            return Task.FromResult((T?)value);
        return Task.FromResult(default(T));
    }
    
    public Task SetAsync<T>(string key, T value)
    {
        _cache[key] = value!;
        return Task.CompletedTask;
    }
    
    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
    
    public Task<bool> ContainsKeyAsync(string key) => Task.FromResult(_cache.ContainsKey(key));
    
    public Task<IEnumerable<string>> GetKeysAsync() => Task.FromResult<IEnumerable<string>>(_cache.Keys);
}

// Заглушки для сервисов
internal class DummyMessageService : IMessageService
{
    public Task SendMessageAsync(long chatId, string content) => Task.CompletedTask;
    public Task SendPrivateMessageAsync(long userId, string content) => Task.CompletedTask;
    public Task<IEnumerable<Message>> GetChatHistoryAsync(long chatId, int count = 50, int offset = 0) 
        => Task.FromResult<IEnumerable<Message>>(new List<Message>());
    public Task EditMessageAsync(long messageId, string newContent) => Task.CompletedTask;
    public Task DeleteMessageAsync(long messageId) => Task.CompletedTask;
    
    public void RegisterOutgoingInterceptor(Func<Message, Message?> interceptor)
    {
        try
        {
            ModuleManager.Instance?.RegisterOutgoingInterceptor(interceptor);
        }
        catch { }
    }
    
    public void RegisterIncomingInterceptor(Func<Message, Message?> interceptor)
    {
        try
        {
            ModuleManager.Instance?.RegisterIncomingInterceptor(interceptor);
        }
        catch { }
    }
}

internal class DummyUserService : IUserService
{
    public User? CurrentUser => null;
    public Task<User?> GetUserAsync(long userId) => Task.FromResult<User?>(null);
    public Task<User?> GetUserByNameAsync(string username) => Task.FromResult<User?>(null);
    public Task<IEnumerable<User>> GetOnlineUsersAsync() => Task.FromResult<IEnumerable<User>>(new List<User>());
    public Task<IEnumerable<User>> GetFriendsAsync() => Task.FromResult<IEnumerable<User>>(new List<User>());
    public Task AddFriendAsync(long userId) => Task.CompletedTask;
    public Task RemoveFriendAsync(long userId) => Task.CompletedTask;
}

internal class DummyChatService : IChatService
{
    public Task<IEnumerable<Chat>> GetUserChatsAsync() => Task.FromResult<IEnumerable<Chat>>(new List<Chat>());
    public Task<Chat> CreateChatAsync(string name, IEnumerable<long> memberIds) => Task.FromResult(new Chat());
    public Task<Chat?> GetChatAsync(long chatId) => Task.FromResult<Chat?>(null);
    public Task InviteUserAsync(long chatId, long userId) => Task.CompletedTask;
    public Task KickUserAsync(long chatId, long userId) => Task.CompletedTask;
    public Task LeaveChatAsync(long chatId) => Task.CompletedTask;
    public Task DeleteChatAsync(long chatId) => Task.CompletedTask;
    public Task<IEnumerable<User>> GetChatMembersAsync(long chatId) => Task.FromResult<IEnumerable<User>>(new List<User>());
}

internal class DummyFileService : IFileService
{
    public event EventHandler<FileTransferProgressEventArgs>? TransferProgress;
    
    public Task<FileTransfer> SendFileAsync(long chatId, string filePath) => Task.FromResult(new FileTransfer());
    public Task<FileTransfer> SendFileToUserAsync(long userId, string filePath) => Task.FromResult(new FileTransfer());
    public Task AcceptFileAsync(long transferId, string savePath) => Task.CompletedTask;
    public Task RejectFileAsync(long transferId) => Task.CompletedTask;
    public Task CancelTransferAsync(long transferId) => Task.CompletedTask;
    public Task<IEnumerable<FileTransfer>> GetActiveTransfersAsync() 
        => Task.FromResult<IEnumerable<FileTransfer>>(new List<FileTransfer>());
}
