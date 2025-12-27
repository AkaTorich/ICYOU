using ICYOU.Core.Database;
using ICYOU.Core.Emotes;
using ICYOU.Core.Modules;

namespace ICYOU.Server;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("ICYOU Messenger Server");
        Console.WriteLine("======================");
        
        var port = 7777;
        var filePort = 7778;
        var dbPath = "icyou.db";
        var emotesPath = "emotes";
        var modulesPath = "modules";
        
        // Парсинг аргументов
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--port" or "-p":
                    if (i + 1 < args.Length)
                        port = int.Parse(args[++i]);
                    break;
                case "--file-port":
                    if (i + 1 < args.Length)
                        filePort = int.Parse(args[++i]);
                    break;
                case "--db":
                    if (i + 1 < args.Length)
                        dbPath = args[++i];
                    break;
                case "--emotes":
                    if (i + 1 < args.Length)
                        emotesPath = args[++i];
                    break;
                case "--modules":
                    if (i + 1 < args.Length)
                        modulesPath = args[++i];
                    break;
            }
        }
        
        // Инициализация
        Console.WriteLine($"Database: {dbPath}");
        Console.WriteLine($"Emotes: {emotesPath}");
        Console.WriteLine($"Modules: {modulesPath}");
        Console.WriteLine($"TCP Port: {port}");
        Console.WriteLine($"TCP File Port: {filePort}");
        Console.WriteLine();
        
        using var db = new DatabaseContext(dbPath);
        var emoteManager = new EmoteManager(emotesPath);
        emoteManager.LoadEmotePacks();
        
        var server = new TcpServer(port);
        var fileServer = new FileServer(filePort);
        var fileManager = new FileTransferManager();
        var handler = new PacketHandler(server, db, emoteManager, fileManager, fileServer);
        
        // Модули
        var moduleContext = CreateModuleContext();
        var moduleLoader = new ModuleLoader(moduleContext, modulesPath);
        moduleLoader.LoadAllModules();
        
        // Запуск файлового сервера
        fileServer.Start();
        
        // Периодическая очистка
        var cleanupTimer = new Timer(_ =>
        {
            fileManager.CleanupOldTransfers(TimeSpan.FromHours(1));
            fileServer.Cleanup(TimeSpan.FromHours(1));
        }, null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));
        
        var cts = new CancellationTokenSource();
        
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        
        Console.WriteLine("Server started. Press Ctrl+C to stop.");
        Console.WriteLine();
        
        try
        {
            await server.StartAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        
        Console.WriteLine("Shutting down...");
        moduleLoader.UnloadAllModules();
        server.Stop();
        fileServer.Stop();
        cleanupTimer.Dispose();
        
        Console.WriteLine("Server stopped.");
    }
    
    static ServerModuleContext CreateModuleContext()
    {
        // Заглушки для серверного контекста - реальная реализация будет связана с сервером
        return new ServerModuleContext(
            null!, null!, null!, null!,
            new ServerModuleLogger("Server"),
            null!
        );
    }
}

