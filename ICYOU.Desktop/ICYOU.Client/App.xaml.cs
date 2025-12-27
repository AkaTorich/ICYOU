using System.Windows;
using ICYOU.Client.Services;

namespace ICYOU.Client;

public partial class App : Application
{
    public static TcpClient? NetworkClient { get; set; }
    public static SDK.User? CurrentUser { get; set; }
    public static string? SessionToken { get; set; }
    
    // Настройки
    public static bool NotifyMessages { get; set; } = true;
    public static bool NotifySounds { get; set; } = true;
    public static bool NotifyFriends { get; set; } = true;
    
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Загружаем сохранённую тему
        ThemeService.Instance.LoadSavedTheme();
        
        // Загружаем модули
        ModuleManager.Instance.LoadModules();
    }
}

