namespace ICYOU.SDK;

/// <summary>
/// Модель чата
/// </summary>
public class Chat
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ChatType Type { get; set; }
    public long OwnerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<long> MemberIds { get; set; } = new();
    
    /// <summary>
    /// Последнее сообщение (для превью)
    /// </summary>
    public Message? LastMessage { get; set; }
    
    /// <summary>
    /// Количество непрочитанных
    /// </summary>
    public int UnreadCount { get; set; }
}

public enum ChatType
{
    /// <summary>
    /// Личный чат между двумя пользователями
    /// </summary>
    Private = 0,
    
    /// <summary>
    /// Групповой чат
    /// </summary>
    Group = 1
}

