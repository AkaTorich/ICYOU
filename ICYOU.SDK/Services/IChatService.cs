namespace ICYOU.SDK;

/// <summary>
/// Сервис для работы с чатами
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Получить список чатов пользователя
    /// </summary>
    Task<IEnumerable<Chat>> GetUserChatsAsync();
    
    /// <summary>
    /// Создать новый чат
    /// </summary>
    Task<Chat> CreateChatAsync(string name, IEnumerable<long> memberIds);
    
    /// <summary>
    /// Получить чат по ID
    /// </summary>
    Task<Chat?> GetChatAsync(long chatId);
    
    /// <summary>
    /// Пригласить пользователя в чат
    /// </summary>
    Task InviteUserAsync(long chatId, long userId);
    
    /// <summary>
    /// Исключить пользователя из чата
    /// </summary>
    Task KickUserAsync(long chatId, long userId);
    
    /// <summary>
    /// Покинуть чат
    /// </summary>
    Task LeaveChatAsync(long chatId);
    
    /// <summary>
    /// Удалить чат (только для создателя)
    /// </summary>
    Task DeleteChatAsync(long chatId);
    
    /// <summary>
    /// Получить участников чата
    /// </summary>
    Task<IEnumerable<User>> GetChatMembersAsync(long chatId);
}

