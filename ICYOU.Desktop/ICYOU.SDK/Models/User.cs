namespace ICYOU.SDK;

/// <summary>
/// Модель пользователя
/// </summary>
public class User
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? AvatarPath { get; set; }
    public UserStatus Status { get; set; }
    public DateTime LastSeen { get; set; }
    public DateTime CreatedAt { get; set; }
}

public enum UserStatus
{
    Offline = 0,
    Online = 1,
    Away = 2,
    DoNotDisturb = 3
}

