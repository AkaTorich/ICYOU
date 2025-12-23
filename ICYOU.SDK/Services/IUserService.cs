namespace ICYOU.SDK;

/// <summary>
/// Сервис для работы с пользователями
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Текущий пользователь
    /// </summary>
    User? CurrentUser { get; }
    
    /// <summary>
    /// Получить пользователя по ID
    /// </summary>
    Task<User?> GetUserAsync(long userId);
    
    /// <summary>
    /// Получить пользователя по имени
    /// </summary>
    Task<User?> GetUserByNameAsync(string username);
    
    /// <summary>
    /// Получить список онлайн пользователей
    /// </summary>
    Task<IEnumerable<User>> GetOnlineUsersAsync();
    
    /// <summary>
    /// Получить список друзей
    /// </summary>
    Task<IEnumerable<User>> GetFriendsAsync();
    
    /// <summary>
    /// Добавить в друзья
    /// </summary>
    Task AddFriendAsync(long userId);
    
    /// <summary>
    /// Удалить из друзей
    /// </summary>
    Task RemoveFriendAsync(long userId);
}

