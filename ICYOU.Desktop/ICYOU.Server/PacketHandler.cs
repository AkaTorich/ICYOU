using System.Net;
using ICYOU.Core.Database;
using ICYOU.Core.Emotes;
using ICYOU.Core.Protocol;
using ICYOU.SDK;

namespace ICYOU.Server;

public class PacketHandler
{
    private readonly TcpServer _server;
    private readonly DatabaseContext _db;
    private readonly UserRepository _userRepo;
    private readonly ChatRepository _chatRepo;
    private readonly MessageRepository _messageRepo;
    private readonly FriendRepository _friendRepo;
    private readonly EmoteManager _emoteManager;
    private readonly FileTransferManager _fileManager;
    private readonly FileServer _fileServer;
    
    public PacketHandler(TcpServer server, DatabaseContext db, EmoteManager emoteManager, FileTransferManager fileManager, FileServer fileServer)
    {
        _server = server;
        _db = db;
        _userRepo = new UserRepository(db);
        _chatRepo = new ChatRepository(db);
        _messageRepo = new MessageRepository(db);
        _friendRepo = new FriendRepository(db, _userRepo);
        _emoteManager = emoteManager;
        _fileManager = fileManager;
        _fileServer = fileServer;
        
        _server.PacketReceived += OnPacketReceived;
        _fileServer.FileUploaded += OnFileUploaded;
    }
    
    private async void OnFileUploaded(object? sender, FileUploadedEventArgs e)
    {
        // Уведомляем получателя о новом файле
        var senderUser = _userRepo.GetById(e.SenderId);
        var fileType = GetFileType(e.FileName);
        
        var notification = new FileNotificationData
        {
            FileId = e.FileId,
            SenderId = e.SenderId,
            SenderName = senderUser?.DisplayName ?? "Unknown",
            FileName = e.FileName,
            FileSize = e.FileSize,
            FileType = fileType,
            ChatId = e.ChatId,
            TargetUserId = e.TargetUserId
        };
        
        if (e.TargetUserId > 0)
        {
            // Личный чат
            await _server.SendToUserAsync(new Packet(PacketType.FileAvailable, notification), e.TargetUserId);
            Console.WriteLine($"[File] Уведомление отправлено пользователю {e.TargetUserId}");
        }
        else if (e.ChatId > 0)
        {
            // Групповой чат - отправляем всем участникам кроме отправителя
            var members = _chatRepo.GetChatMemberIds(e.ChatId).Where(id => id != e.SenderId);
            foreach (var memberId in members)
            {
                await _server.SendToUserAsync(new Packet(PacketType.FileAvailable, notification), memberId);
            }
            Console.WriteLine($"[File] Уведомление отправлено в групповой чат {e.ChatId}");
        }
    }
    
    private string GetFileType(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" => "image",
            ".mp4" or ".webm" or ".avi" or ".mkv" or ".mov" => "video",
            ".mp3" or ".wav" or ".ogg" or ".flac" => "audio",
            _ => "file"
        };
    }
    
    private async void OnPacketReceived(object? sender, (Packet Packet, ClientConnection Connection) args)
    {
        var (packet, connection) = args;
        
        // Проверка сессии для всех пакетов кроме Login/Register
        if (packet.Type != PacketType.Login && packet.Type != PacketType.Register)
        {
            if (string.IsNullOrEmpty(packet.SessionToken))
            {
                await SendError(connection, "Session required");
                return;
            }
            
            var userId = _userRepo.ValidateSession(packet.SessionToken);
            if (userId == null)
            {
                await SendError(connection, "Invalid session");
                return;
            }
            packet.UserId = userId.Value;
        }
        
        try
        {
            switch (packet.Type)
            {
                case PacketType.Login:
                    await HandleLogin(packet, connection);
                    break;
                case PacketType.Register:
                    await HandleRegister(packet, connection);
                    break;
                case PacketType.Logout:
                    await HandleLogout(packet, connection);
                    break;
                case PacketType.SendMessage:
                    await HandleSendMessage(packet, connection);
                    break;
                case PacketType.EditMessage:
                    await HandleEditMessage(packet, connection);
                    break;
                case PacketType.DeleteMessage:
                    await HandleDeleteMessage(packet, connection);
                    break;
                case PacketType.GetChatHistory:
                    await HandleGetChatHistory(packet, connection);
                    break;
                case PacketType.MarkAsRead:
                    await HandleMarkAsRead(packet, connection);
                    break;
                case PacketType.GetUserInfo:
                    await HandleGetUserInfo(packet, connection);
                    break;
                case PacketType.SearchUsers:
                    await HandleSearchUsers(packet, connection);
                    break;
                case PacketType.GetOnlineUsers:
                    await HandleGetOnlineUsers(packet, connection);
                    break;
                case PacketType.AddFriend:
                    await HandleAddFriend(packet, connection);
                    break;
                case PacketType.RemoveFriend:
                    await HandleRemoveFriend(packet, connection);
                    break;
                case PacketType.GetFriends:
                    await HandleGetFriends(packet, connection);
                    break;
                case PacketType.CreateChat:
                    await HandleCreateChat(packet, connection);
                    break;
                case PacketType.GetUserChats:
                    await HandleGetUserChats(packet, connection);
                    break;
                case PacketType.InviteToChat:
                    await HandleInviteToChat(packet, connection);
                    break;
                case PacketType.ChatInviteResponse:
                    await HandleChatInviteResponse(packet, connection);
                    break;
                case PacketType.LeaveChat:
                    await HandleLeaveChat(packet, connection);
                    break;
                case PacketType.KickFromChat:
                    await HandleKickFromChat(packet, connection);
                    break;
                case PacketType.DeleteChat:
                    await HandleDeleteChat(packet, connection);
                    break;
                case PacketType.GetChatMembers:
                    await HandleGetChatMembers(packet, connection);
                    break;
                case PacketType.GetEmotePacks:
                    await HandleGetEmotePacks(packet, connection);
                    break;
                case PacketType.FileChunk:
                    await HandleFileChunk(packet, connection);
                    break;
                case PacketType.FileChunkAck:
                    await HandleFileChunkAck(packet, connection);
                    break;
                case PacketType.FileTransferCancel:
                    await HandleFileTransferCancel(packet, connection);
                    break;
                case PacketType.Ping:
                    await HandlePing(packet, connection);
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling packet {packet.Type}: {ex.Message}");
            await SendError(connection, "Internal server error");
        }
    }
    
    private async Task HandleLogin(Packet packet, ClientConnection connection)
    {
        Console.WriteLine($"[SERVER] HandleLogin: получен пакет Login");
        var data = packet.GetData<LoginData>()!;
        Console.WriteLine($"[SERVER] HandleLogin: Username={data.Username}");
        
        if (!_userRepo.ValidatePassword(data.Username, data.PasswordHash))
        {
            Console.WriteLine($"[SERVER] HandleLogin: Неверный пароль для {data.Username}");
            await _server.SendAsync(packet.CreateResponse(PacketType.LoginResponse, new LoginResponseData
            {
                Success = false,
                Error = "Invalid username or password"
            }), connection);
            return;
        }
        
        var user = _userRepo.GetByUsername(data.Username)!;
        var token = _userRepo.CreateSession(user.Id);
        
        _userRepo.UpdateStatus(user.Id, UserStatus.Online);
        _server.RegisterClient(user.Id, connection, token);
        
        user.Status = UserStatus.Online;
        
        Console.WriteLine($"[SERVER] HandleLogin: Пользователь {data.Username} (UserId={user.Id}) успешно залогинился");
        await _server.SendAsync(packet.CreateResponse(PacketType.LoginResponse, new LoginResponseData
        {
            Success = true,
            SessionToken = token,
            User = user
        }), connection);
        Console.WriteLine($"[SERVER] HandleLogin: Ответ отправлен");
        
        // Уведомляем друзей о входе
        await NotifyFriendsAboutStatus(user.Id, UserStatus.Online);
    }
    
    private async Task HandleRegister(Packet packet, ClientConnection connection)
    {
        Console.WriteLine($"[SERVER] HandleRegister: получен пакет Register");
        var data = packet.GetData<RegisterData>()!;
        Console.WriteLine($"[SERVER] HandleRegister: Username={data.Username}, DisplayName={data.DisplayName}");
        
        if (_userRepo.GetByUsername(data.Username) != null)
        {
            Console.WriteLine($"[SERVER] HandleRegister: Пользователь {data.Username} уже существует");
            await _server.SendAsync(packet.CreateResponse(PacketType.RegisterResponse, new RegisterResponseData
            {
                Success = false,
                Error = "Username already exists"
            }), connection);
            return;
        }
        
        var user = _userRepo.Create(data.Username, data.DisplayName, data.PasswordHash);
        Console.WriteLine($"[SERVER] HandleRegister: Пользователь создан, UserId={user.Id}");
        
        await _server.SendAsync(packet.CreateResponse(PacketType.RegisterResponse, new RegisterResponseData
        {
            Success = true,
            User = user
        }), connection);
        Console.WriteLine($"[SERVER] HandleRegister: Ответ отправлен");
    }
    
    private async Task HandleLogout(Packet packet, ClientConnection connection)
    {
        _userRepo.UpdateStatus(packet.UserId, UserStatus.Offline);
        _userRepo.DeleteSession(packet.SessionToken!);
        _server.UnregisterClient(packet.UserId);
        
        await NotifyFriendsAboutStatus(packet.UserId, UserStatus.Offline);
    }
    
    private async Task HandleSendMessage(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<SendMessageData>()!;
        var sender = _userRepo.GetById(packet.UserId)!;
        
        Console.WriteLine($"[SERVER] HandleSendMessage: SenderId={packet.UserId}, SenderName={sender.DisplayName}, ChatId={data.ChatId}, TargetUserId={data.TargetUserId}");
        
        long chatId = data.ChatId;
        
        // Если указан целевой пользователь - создаем/получаем приватный чат
        if (data.TargetUserId.HasValue)
        {
            Console.WriteLine($"[SERVER] Создание/получение приватного чата: UserId1={packet.UserId}, UserId2={data.TargetUserId.Value}");
            var chat = _chatRepo.GetOrCreatePrivateChat(packet.UserId, data.TargetUserId.Value);
            chatId = chat!.Id;
            Console.WriteLine($"[SERVER] Приватный чат создан/найден: ChatId={chatId}");
        }
        
        // Проверяем что пользователь участник чата
        if (!_chatRepo.IsMember(chatId, packet.UserId))
        {
            Console.WriteLine($"[SERVER] ОШИБКА: Пользователь {packet.UserId} не является участником чата {chatId}");
            await SendError(connection, "Not a member of this chat");
            return;
        }
        
        // Создаём сообщение БЕЗ сохранения в БД сервера - только для пересылки
        var message = new Message
        {
            Id = DateTime.UtcNow.Ticks,
            ChatId = chatId,
            SenderId = packet.UserId,
            SenderName = sender.DisplayName,
            Content = data.Content,
            Type = data.Type,
            Timestamp = DateTime.UtcNow,
            Status = MessageStatus.Sent
        };
        
        // Отправляем всем участникам чата
        var memberIds = _chatRepo.GetChatMemberIds(chatId);
        var contentPreview = data.Content.Length > 50 ? data.Content.Substring(0, 50) + "..." : data.Content;
        Console.WriteLine($"[SERVER] Отправка сообщения: ChatId={chatId}, SenderId={packet.UserId}, SenderName={sender.DisplayName}, Content={contentPreview}, Участников={memberIds.Count}");
        foreach (var memberId in memberIds)
        {
            Console.WriteLine($"[SERVER]   -> Отправка участнику: UserId={memberId}");
        }
        var msgPacket = new Packet(PacketType.MessageReceived, message);
        await _server.BroadcastAsync(msgPacket, memberIds);
        Console.WriteLine($"[SERVER] Сообщение отправлено всем {memberIds.Count} участникам чата {chatId}");
    }
    
    private async Task HandleEditMessage(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<EditMessageData>()!;
        var message = _messageRepo.GetById(data.MessageId);
        
        if (message == null || message.SenderId != packet.UserId)
        {
            await SendError(connection, "Cannot edit this message");
            return;
        }
        
        _messageRepo.Edit(data.MessageId, data.NewContent);
        message = _messageRepo.GetById(data.MessageId)!;
        
        var memberIds = _chatRepo.GetChatMemberIds(message.ChatId);
        await _server.BroadcastAsync(new Packet(PacketType.EditMessage, message), memberIds);
    }
    
    private async Task HandleDeleteMessage(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<DeleteMessageData>()!;
        var message = _messageRepo.GetById(data.MessageId);
        
        if (message == null || message.SenderId != packet.UserId)
        {
            await SendError(connection, "Cannot delete this message");
            return;
        }
        
        var chatId = message.ChatId;
        _messageRepo.Delete(data.MessageId);
        
        var memberIds = _chatRepo.GetChatMemberIds(chatId);
        await _server.BroadcastAsync(new Packet(PacketType.DeleteMessage, data), memberIds);
    }
    
    private async Task HandleGetChatHistory(Packet packet, ClientConnection connection)
    {
        // Сервер не хранит историю - возвращаем пустой список
        // Вся история хранится только на клиентах
        var data = packet.GetData<GetChatHistoryData>()!;
        
        await _server.SendAsync(packet.CreateResponse(PacketType.ChatHistoryResponse, new ChatHistoryResponseData
        {
            ChatId = data.ChatId,
            Messages = new List<Message>()
        }), connection);
    }
    
    private async Task HandleMarkAsRead(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<MarkAsReadData>()!;
        
        // Проверяем членство в чате
        if (!_chatRepo.IsMember(data.ChatId, packet.UserId))
            return;
        
        // Получаем сообщения которые нужно отметить как прочитанные
        var messages = _messageRepo.GetChatHistory(data.ChatId, 100, 0);
        
        // Уведомляем отправителей о прочтении
        foreach (var msg in messages.Where(m => m.Id <= data.LastReadMessageId && m.SenderId != packet.UserId))
        {
            // Отправляем уведомление автору сообщения
            await _server.SendToUserAsync(new Packet(PacketType.MessageRead, new MessageReadData
            {
                ChatId = data.ChatId,
                MessageId = msg.Id,
                ReadByUserId = packet.UserId
            }), msg.SenderId);
        }
    }

    private async Task HandleGetUserInfo(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<GetUserInfoData>()!;
        
        User? user = null;
        if (data.UserId > 0)
            user = _userRepo.GetById(data.UserId);
        else if (!string.IsNullOrEmpty(data.Username))
            user = _userRepo.GetByUsername(data.Username);
            
        await _server.SendAsync(packet.CreateResponse(PacketType.UserInfoResponse, user), connection);
    }
    
    private async Task HandleSearchUsers(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<SearchUsersData>()!;
        var users = _userRepo.Search(data.Query);
        
        await _server.SendAsync(packet.CreateResponse(PacketType.SearchUsersResponse, new SearchUsersResponseData
        {
            Users = users
        }), connection);
    }
    
    private async Task HandleGetOnlineUsers(Packet packet, ClientConnection connection)
    {
        var users = _userRepo.GetOnlineUsers();
        
        await _server.SendAsync(packet.CreateResponse(PacketType.OnlineUsersResponse, new SearchUsersResponseData
        {
            Users = users
        }), connection);
    }
    
    private async Task HandleAddFriend(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<FriendActionData>()!;
        
        if (_friendRepo.AreFriends(packet.UserId, data.UserId))
            return;
            
        // Если есть встречный запрос - добавляем в друзья
        if (_friendRepo.HasPendingRequest(data.UserId, packet.UserId))
        {
            _friendRepo.DeleteFriendRequest(data.UserId, packet.UserId);
            _friendRepo.AddFriend(packet.UserId, data.UserId);
            
            // Уведомляем обоих
            var user = _userRepo.GetById(packet.UserId);
            await _server.SendToUserAsync(new Packet(PacketType.FriendRequestResponse, new { Accepted = true, Friend = user }), data.UserId);
        }
        else
        {
            // Создаем запрос
            if (!_friendRepo.HasPendingRequest(packet.UserId, data.UserId))
            {
                _friendRepo.CreateFriendRequest(packet.UserId, data.UserId);
                var user = _userRepo.GetById(packet.UserId);
                await _server.SendToUserAsync(new Packet(PacketType.FriendRequest, user), data.UserId);
            }
        }
    }
    
    private async Task HandleRemoveFriend(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<FriendActionData>()!;
        _friendRepo.RemoveFriend(packet.UserId, data.UserId);
    }
    
    private async Task HandleGetFriends(Packet packet, ClientConnection connection)
    {
        var friends = _friendRepo.GetFriends(packet.UserId);
        
        await _server.SendAsync(packet.CreateResponse(PacketType.FriendsListResponse, new FriendsListResponseData
        {
            Friends = friends
        }), connection);
    }
    
    private async Task HandleCreateChat(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<CreateChatData>()!;
        
        var chat = _chatRepo.Create(data.Name, ChatType.Group, packet.UserId, data.MemberIds);
        
        await _server.SendAsync(packet.CreateResponse(PacketType.CreateChatResponse, chat), connection);
        
        // Уведомляем участников
        foreach (var memberId in data.MemberIds.Where(id => id != packet.UserId))
        {
            await _server.SendToUserAsync(new Packet(PacketType.ChatInvite, new { Chat = chat, InvitedBy = _userRepo.GetById(packet.UserId) }), memberId);
        }
    }
    
    private async Task HandleGetUserChats(Packet packet, ClientConnection connection)
    {
        var chats = _chatRepo.GetUserChats(packet.UserId);
        
        await _server.SendAsync(packet.CreateResponse(PacketType.UserChatsResponse, new UserChatsResponseData
        {
            Chats = chats
        }), connection);
    }
    
    private async Task HandleInviteToChat(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<InviteToChatData>()!;
        var chat = _chatRepo.GetById(data.ChatId);
        
        if (chat == null || !_chatRepo.IsMember(data.ChatId, packet.UserId))
        {
            await SendError(connection, "Cannot invite to this chat");
            return;
        }
        
        if (_chatRepo.IsMember(data.ChatId, data.UserId))
            return;
            
        _chatRepo.CreateInvite(data.ChatId, data.UserId, packet.UserId);
        
        var inviter = _userRepo.GetById(packet.UserId);
        await _server.SendToUserAsync(new Packet(PacketType.ChatInvite, new { Chat = chat, InvitedBy = inviter }), data.UserId);
    }
    
    private async Task HandleChatInviteResponse(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<ChatInviteResponseData>()!;
        
        if (!_chatRepo.HasPendingInvite(data.ChatId, packet.UserId))
            return;
            
        _chatRepo.DeleteInvite(data.ChatId, packet.UserId);
        
        if (data.Accept)
        {
            _chatRepo.AddMember(data.ChatId, packet.UserId);
            var user = _userRepo.GetById(packet.UserId);
            var memberIds = _chatRepo.GetChatMemberIds(data.ChatId);
            await _server.BroadcastAsync(new Packet(PacketType.UserJoinedChat, new { ChatId = data.ChatId, User = user }), memberIds);
        }
    }
    
    private async Task HandleLeaveChat(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<ChatActionData>()!;
        var chat = _chatRepo.GetById(data.ChatId);
        
        if (chat == null || !_chatRepo.IsMember(data.ChatId, packet.UserId))
            return;
            
        _chatRepo.RemoveMember(data.ChatId, packet.UserId);
        
        var user = _userRepo.GetById(packet.UserId);
        var memberIds = _chatRepo.GetChatMemberIds(data.ChatId);
        await _server.BroadcastAsync(new Packet(PacketType.UserLeftChat, new { ChatId = data.ChatId, User = user }), memberIds);
    }
    
    private async Task HandleKickFromChat(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<InviteToChatData>()!;
        var chat = _chatRepo.GetById(data.ChatId);
        
        if (chat == null || chat.OwnerId != packet.UserId)
        {
            await SendError(connection, "Only owner can kick users");
            return;
        }
        
        _chatRepo.RemoveMember(data.ChatId, data.UserId);
        
        var user = _userRepo.GetById(data.UserId);
        var memberIds = _chatRepo.GetChatMemberIds(data.ChatId);
        memberIds.Add(data.UserId);
        await _server.BroadcastAsync(new Packet(PacketType.UserLeftChat, new { ChatId = data.ChatId, User = user, Kicked = true }), memberIds);
    }
    
    private async Task HandleDeleteChat(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<ChatActionData>()!;
        var chat = _chatRepo.GetById(data.ChatId);
        
        if (chat == null || chat.OwnerId != packet.UserId)
        {
            await SendError(connection, "Only owner can delete chat");
            return;
        }
        
        var memberIds = _chatRepo.GetChatMemberIds(data.ChatId);
        _chatRepo.Delete(data.ChatId);
        
        await _server.BroadcastAsync(new Packet(PacketType.DeleteChat, data), memberIds);
    }
    
    private async Task HandleGetChatMembers(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<ChatActionData>()!;
        
        if (!_chatRepo.IsMember(data.ChatId, packet.UserId))
        {
            await SendError(connection, "Not a member");
            return;
        }
        
        var memberIds = _chatRepo.GetChatMemberIds(data.ChatId);
        var members = memberIds.Select(id => _userRepo.GetById(id)).Where(u => u != null).ToList();
        
        await _server.SendAsync(packet.CreateResponse(PacketType.ChatMembersResponse, new ChatMembersResponseData
        {
            ChatId = data.ChatId,
            Members = members!
        }), connection);
    }
    
    private async Task HandleGetEmotePacks(Packet packet, ClientConnection connection)
    {
        var packs = _emoteManager.GetAllPacks();
        
        await _server.SendAsync(packet.CreateResponse(PacketType.EmotePacksResponse, new EmotePacksResponseData
        {
            Packs = packs
        }), connection);
    }
    
    private async Task HandleFileTransferResponse(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<FileTransferResponseData>()!;
        var transfer = _fileManager.GetTransfer(data.TransferId);
        
        if (transfer == null)
            return;
            
        if (data.Accept)
        {
            _fileManager.AcceptTransfer(data.TransferId, packet.UserId);
            await _server.SendToUserAsync(new Packet(PacketType.FileTransferResponse, new { TransferId = data.TransferId, Accepted = true, ReceiverId = packet.UserId }), transfer.SenderId);
        }
        else
        {
            _fileManager.RejectTransfer(data.TransferId);
            await _server.SendToUserAsync(new Packet(PacketType.FileTransferResponse, new { TransferId = data.TransferId, Accepted = false }), transfer.SenderId);
        }
    }
    
    private async Task HandleFileChunk(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<FileChunkData>()!;
        var transfer = _fileManager.GetTransfer(data.TransferId);
        
        if (transfer == null || transfer.SenderId != packet.UserId)
            return;
            
        _fileManager.AddChunk(data.TransferId, data.ChunkIndex, data.Data);
        
        // Пересылаем получателю
        await _server.SendToUserAsync(new Packet(PacketType.FileChunk, data), transfer.ReceiverId);
        
        if (data.IsLast)
        {
            _fileManager.CompleteTransfer(data.TransferId);
            await _server.SendToUserAsync(new Packet(PacketType.FileTransferComplete, new { TransferId = data.TransferId }), transfer.ReceiverId);
            await _server.SendToUserAsync(new Packet(PacketType.FileTransferComplete, new { TransferId = data.TransferId }), transfer.SenderId);
        }
    }
    
    private async Task HandleFileChunkAck(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<FileChunkAckData>()!;
        var transfer = _fileManager.GetTransfer(data.TransferId);
        
        if (transfer == null)
            return;
            
        await _server.SendToUserAsync(new Packet(PacketType.FileChunkAck, data), transfer.SenderId);
    }
    
    private async Task HandleFileTransferCancel(Packet packet, ClientConnection connection)
    {
        var data = packet.GetData<FileTransferResponseData>()!;
        var transfer = _fileManager.GetTransfer(data.TransferId);
        
        if (transfer == null)
            return;
            
        _fileManager.CancelTransfer(data.TransferId);
        
        // Уведомляем обе стороны
        await _server.SendToUserAsync(new Packet(PacketType.FileTransferCancel, new { TransferId = data.TransferId }), transfer.SenderId);
        await _server.SendToUserAsync(new Packet(PacketType.FileTransferCancel, new { TransferId = data.TransferId }), transfer.ReceiverId);
    }
    
    private async Task HandlePing(Packet packet, ClientConnection connection)
    {
        await _server.SendAsync(new Packet(PacketType.Pong), connection);
    }
    
    private async Task NotifyFriendsAboutStatus(long userId, UserStatus status)
    {
        var friends = _friendRepo.GetFriends(userId);
        var packet = new Packet(PacketType.UserStatusChanged, new UserStatusChangedData
        {
            UserId = userId,
            Status = status
        });
        
        foreach (var friend in friends)
        {
            await _server.SendToUserAsync(packet, friend.Id);
        }
    }
    
    private async Task SendError(ClientConnection connection, string message)
    {
        await _server.SendAsync(new Packet(PacketType.Error, new ErrorData { Message = message }), connection);
    }
}

