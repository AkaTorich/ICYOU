namespace ICYOU.SDK;

/// <summary>
/// Сервис для работы с сообщениями
/// </summary>
public interface IMessageService
{
    /// <summary>
    /// Отправить сообщение в чат
    /// </summary>
    Task SendMessageAsync(long chatId, string content);
    
    /// <summary>
    /// Отправить личное сообщение пользователю
    /// </summary>
    Task SendPrivateMessageAsync(long userId, string content);
    
    /// <summary>
    /// Получить историю сообщений чата
    /// </summary>
    Task<IEnumerable<Message>> GetChatHistoryAsync(long chatId, int count = 50, int offset = 0);
    
    /// <summary>
    /// Редактировать сообщение
    /// </summary>
    Task EditMessageAsync(long messageId, string newContent);
    
    /// <summary>
    /// Удалить сообщение
    /// </summary>
    Task DeleteMessageAsync(long messageId);
    
    /// <summary>
    /// Перехват исходящего сообщения (для модификации перед отправкой)
    /// </summary>
    void RegisterOutgoingInterceptor(Func<Message, Message?> interceptor);
    
    /// <summary>
    /// Перехват входящего сообщения (для модификации перед отображением)
    /// </summary>
    void RegisterIncomingInterceptor(Func<Message, Message?> interceptor);
}

