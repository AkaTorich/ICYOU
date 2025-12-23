using ICYOU.SDK;

namespace ICYOU.Mobile;

/// <summary>
/// Глобальное состояние приложения
/// </summary>
public static class AppState
{
    public static TcpClient? NetworkClient { get; set; }
    public static User? CurrentUser { get; set; }
    public static string? SessionToken { get; set; }

    // Настройки
    public static bool NotifyMessages { get; set; } = true;
    public static bool NotifySounds { get; set; } = true;
    public static bool NotifyFriends { get; set; } = true;
}
