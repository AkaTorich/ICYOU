namespace ICYOU.SDK;

/// <summary>
/// Модель сообщения
/// </summary>
public class Message
{
    public long Id { get; set; }
    public long ChatId { get; set; }
    public long SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public MessageType Type { get; set; }
    public DateTime Timestamp { get; set; }
    public bool IsEdited { get; set; }
    public DateTime? EditedAt { get; set; }
    
    /// <summary>
    /// Статус доставки/прочтения
    /// </summary>
    public MessageStatus Status { get; set; } = MessageStatus.Sent;
    
    /// <summary>
    /// Прикрепленные файлы (ID передач)
    /// </summary>
    public List<long> AttachmentIds { get; set; } = new();
}

public enum MessageType
{
    Text = 0,
    File = 1,
    Image = 2,
    Emote = 3,
    System = 4
}

public enum MessageStatus
{
    Sending = 0,   // Отправляется
    Sent = 1,      // Отправлено (одна галочка)
    Delivered = 2, // Доставлено
    Read = 3       // Прочитано (две галочки)
}

