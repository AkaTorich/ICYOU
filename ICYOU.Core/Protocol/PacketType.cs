namespace ICYOU.Core.Protocol;

public enum PacketType : byte
{
    // Аутентификация
    Login = 1,
    LoginResponse = 2,
    Register = 3,
    RegisterResponse = 4,
    Logout = 5,
    
    // Сообщения
    SendMessage = 10,
    MessageReceived = 11,
    EditMessage = 12,
    DeleteMessage = 13,
    GetChatHistory = 14,
    ChatHistoryResponse = 15,
    MarkAsRead = 16,
    MessageRead = 17,
    
    // Пользователи
    GetUserInfo = 20,
    UserInfoResponse = 21,
    UserStatusChanged = 22,
    GetOnlineUsers = 23,
    OnlineUsersResponse = 24,
    SearchUsers = 25,
    SearchUsersResponse = 26,
    
    // Друзья
    AddFriend = 30,
    RemoveFriend = 31,
    FriendRequest = 32,
    FriendRequestResponse = 33,
    GetFriends = 34,
    FriendsListResponse = 35,
    
    // Чаты
    CreateChat = 40,
    CreateChatResponse = 41,
    GetUserChats = 42,
    UserChatsResponse = 43,
    InviteToChat = 44,
    ChatInvite = 45,
    ChatInviteResponse = 46,
    LeaveChat = 47,
    KickFromChat = 48,
    DeleteChat = 49,
    GetChatMembers = 50,
    ChatMembersResponse = 51,
    UserJoinedChat = 52,
    UserLeftChat = 53,
    
    // Файлы через сервер
    FileTransferRequest = 60,
    FileTransferResponse = 61,
    FileAvailable = 62,       // Уведомление о доступном файле
    FileChunk = 64,
    FileChunkAck = 65,
    FileTransferComplete = 66,
    FileTransferCancel = 67,
    
    // Смайлы
    GetEmotePacks = 70,
    EmotePacksResponse = 71,
    
    // Служебные
    Ping = 250,
    Pong = 251,
    Error = 255
}

