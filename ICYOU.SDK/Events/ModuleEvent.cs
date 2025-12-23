namespace ICYOU.SDK;

/// <summary>
/// Базовый класс событий модуля
/// </summary>
public abstract class ModuleEvent
{
    public DateTime Timestamp { get; } = DateTime.UtcNow;
}

/// <summary>
/// Новое сообщение получено
/// </summary>
public class MessageReceivedEvent : ModuleEvent
{
    public Message Message { get; set; } = null!;
}

/// <summary>
/// Сообщение отправлено
/// </summary>
public class MessageSentEvent : ModuleEvent
{
    public Message Message { get; set; } = null!;
}

/// <summary>
/// Сообщение отредактировано
/// </summary>
public class MessageEditedEvent : ModuleEvent
{
    public Message Message { get; set; } = null!;
    public string OldContent { get; set; } = string.Empty;
}

/// <summary>
/// Сообщение удалено
/// </summary>
public class MessageDeletedEvent : ModuleEvent
{
    public long MessageId { get; set; }
    public long ChatId { get; set; }
}

/// <summary>
/// Пользователь вошел в сеть
/// </summary>
public class UserOnlineEvent : ModuleEvent
{
    public User User { get; set; } = null!;
}

/// <summary>
/// Пользователь вышел из сети
/// </summary>
public class UserOfflineEvent : ModuleEvent
{
    public User User { get; set; } = null!;
}

/// <summary>
/// Пользователь присоединился к чату
/// </summary>
public class UserJoinedChatEvent : ModuleEvent
{
    public long ChatId { get; set; }
    public User User { get; set; } = null!;
}

/// <summary>
/// Пользователь покинул чат
/// </summary>
public class UserLeftChatEvent : ModuleEvent
{
    public long ChatId { get; set; }
    public User User { get; set; } = null!;
}

/// <summary>
/// Новый чат создан
/// </summary>
public class ChatCreatedEvent : ModuleEvent
{
    public Chat Chat { get; set; } = null!;
}

/// <summary>
/// Начата передача файла
/// </summary>
public class FileTransferStartedEvent : ModuleEvent
{
    public FileTransfer Transfer { get; set; } = null!;
}

/// <summary>
/// Передача файла завершена
/// </summary>
public class FileTransferCompletedEvent : ModuleEvent
{
    public FileTransfer Transfer { get; set; } = null!;
}

/// <summary>
/// Приглашение в чат
/// </summary>
public class ChatInviteReceivedEvent : ModuleEvent
{
    public Chat Chat { get; set; } = null!;
    public User InvitedBy { get; set; } = null!;
}

