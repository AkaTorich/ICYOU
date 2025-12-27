using ICYOU.SDK;

namespace ICYOU.Core.Protocol;

#region Auth

public class LoginData
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}

public class LoginResponseData
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? SessionToken { get; set; }
    public User? User { get; set; }
}

public class RegisterData
{
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}

public class RegisterResponseData
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public User? User { get; set; }
}

#endregion

#region Messages

public class SendMessageData
{
    public long ChatId { get; set; }
    public long? TargetUserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public MessageType Type { get; set; }
}

public class MarkAsReadData
{
    public long ChatId { get; set; }
    public long LastReadMessageId { get; set; }
}

public class MessageReadData
{
    public long ChatId { get; set; }
    public long MessageId { get; set; }
    public long ReadByUserId { get; set; }
}

public class EditMessageData
{
    public long MessageId { get; set; }
    public string NewContent { get; set; } = string.Empty;
}

public class DeleteMessageData
{
    public long MessageId { get; set; }
}

public class GetChatHistoryData
{
    public long ChatId { get; set; }
    public int Count { get; set; } = 50;
    public int Offset { get; set; } = 0;
    public long AfterId { get; set; } = 0; // Загрузить сообщения после этого ID
}

public class ChatHistoryResponseData
{
    public long ChatId { get; set; }
    public List<Message> Messages { get; set; } = new();
}

#endregion

#region Users

public class GetUserInfoData
{
    public long UserId { get; set; }
    public string? Username { get; set; }
}

public class UserStatusChangedData
{
    public long UserId { get; set; }
    public UserStatus Status { get; set; }
}

public class SearchUsersData
{
    public string Query { get; set; } = string.Empty;
}

public class SearchUsersResponseData
{
    public List<User> Users { get; set; } = new();
}

#endregion

#region Friends

public class FriendActionData
{
    public long UserId { get; set; }
}

public class FriendsListResponseData
{
    public List<User> Friends { get; set; } = new();
}

#endregion

#region Chats

public class CreateChatData
{
    public string Name { get; set; } = string.Empty;
    public List<long> MemberIds { get; set; } = new();
}

public class ChatActionData
{
    public long ChatId { get; set; }
}

public class InviteToChatData
{
    public long ChatId { get; set; }
    public long UserId { get; set; }
}

public class ChatInviteResponseData
{
    public long ChatId { get; set; }
    public bool Accept { get; set; }
}

public class UserChatsResponseData
{
    public List<Chat> Chats { get; set; } = new();
}

public class ChatMembersResponseData
{
    public long ChatId { get; set; }
    public List<User> Members { get; set; } = new();
}

#endregion

#region Files

public class FileTransferRequestData
{
    public long? ChatId { get; set; }
    public long? TargetUserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
}

public class FileTransferResponseData
{
    public long TransferId { get; set; }
    public bool Accept { get; set; }
}

public class FileChunkData
{
    public long TransferId { get; set; }
    public int ChunkIndex { get; set; }
    public byte[] Data { get; set; } = Array.Empty<byte>();
    public bool IsLast { get; set; }
}

public class FileChunkAckData
{
    public long TransferId { get; set; }
    public int ChunkIndex { get; set; }
}

public class FileNotificationData
{
    public string FileId { get; set; } = "";
    public long SenderId { get; set; }
    public string SenderName { get; set; } = "";
    public string FileName { get; set; } = "";
    public long FileSize { get; set; }
    public string FileType { get; set; } = "";
    public long ChatId { get; set; }  // 0 для личных чатов, >0 для групповых
    public long TargetUserId { get; set; }  // ID получателя для личных чатов
}

#endregion

#region Emotes

public class EmotePack
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public List<Emote> Emotes { get; set; } = new();
}

public class Emote
{
    public string Code { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
}

public class EmotePacksResponseData
{
    public List<EmotePack> Packs { get; set; } = new();
}

#endregion

#region Error

public class ErrorData
{
    public string Message { get; set; } = string.Empty;
    public int Code { get; set; }
}

#endregion

