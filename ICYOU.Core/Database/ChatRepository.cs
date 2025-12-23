using ICYOU.SDK;

namespace ICYOU.Core.Database;

public class ChatRepository
{
    private readonly DatabaseContext _db;
    
    public ChatRepository(DatabaseContext db)
    {
        _db = db;
    }
    
    public Chat? GetById(long id)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM Chats WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var chat = ReadChat(reader);
            chat.MemberIds = GetChatMemberIds(id);
            return chat;
        }
        return null;
    }
    
    public Chat Create(string name, ChatType type, long ownerId, List<long> memberIds)
    {
        // Генерируем уникальный ID на основе времени
        var chatId = DateTime.UtcNow.Ticks;
        
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Chats (Id, Name, Type, OwnerId, CreatedAt)
            VALUES (@id, @name, @type, @ownerId, @createdAt)";
        cmd.Parameters.AddWithValue("@id", chatId);
        cmd.Parameters.AddWithValue("@name", name);
        cmd.Parameters.AddWithValue("@type", (int)type);
        cmd.Parameters.AddWithValue("@ownerId", ownerId);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
        
        cmd.ExecuteNonQuery();
        
        // Добавляем владельца
        AddMember(chatId, ownerId);
        
        // Добавляем остальных участников
        foreach (var memberId in memberIds.Where(id => id != ownerId))
        {
            AddMember(chatId, memberId);
        }
        
        return GetById(chatId)!;
    }
    
    public Chat? GetOrCreatePrivateChat(long userId1, long userId2)
    {
        // Ищем существующий приватный чат
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT c.* FROM Chats c
            INNER JOIN ChatMembers cm1 ON c.Id = cm1.ChatId AND cm1.UserId = @user1
            INNER JOIN ChatMembers cm2 ON c.Id = cm2.ChatId AND cm2.UserId = @user2
            WHERE c.Type = 0";
        cmd.Parameters.AddWithValue("@user1", userId1);
        cmd.Parameters.AddWithValue("@user2", userId2);
        
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var chat = ReadChat(reader);
            chat.MemberIds = GetChatMemberIds(chat.Id);
            return chat;
        }
        reader.Close();
        
        // Создаем новый
        return Create("Private", ChatType.Private, userId1, new List<long> { userId1, userId2 });
    }
    
    public List<Chat> GetUserChats(long userId)
    {
        var chats = new List<Chat>();
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            SELECT c.* FROM Chats c
            INNER JOIN ChatMembers cm ON c.Id = cm.ChatId
            WHERE cm.UserId = @userId";
        cmd.Parameters.AddWithValue("@userId", userId);
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var chat = ReadChat(reader);
            chats.Add(chat);
        }
        
        foreach (var chat in chats)
        {
            chat.MemberIds = GetChatMemberIds(chat.Id);
            chat.LastMessage = GetLastMessage(chat.Id);
        }
        
        return chats;
    }
    
    public void AddMember(long chatId, long userId)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO ChatMembers (ChatId, UserId, JoinedAt)
            VALUES (@chatId, @userId, @joinedAt)";
        cmd.Parameters.AddWithValue("@chatId", chatId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@joinedAt", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }
    
    public void RemoveMember(long chatId, long userId)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM ChatMembers WHERE ChatId = @chatId AND UserId = @userId";
        cmd.Parameters.AddWithValue("@chatId", chatId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.ExecuteNonQuery();
    }
    
    public bool IsMember(long chatId, long userId)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM ChatMembers WHERE ChatId = @chatId AND UserId = @userId";
        cmd.Parameters.AddWithValue("@chatId", chatId);
        cmd.Parameters.AddWithValue("@userId", userId);
        return (long)cmd.ExecuteScalar()! > 0;
    }
    
    public List<long> GetChatMemberIds(long chatId)
    {
        var ids = new List<long>();
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT UserId FROM ChatMembers WHERE ChatId = @chatId";
        cmd.Parameters.AddWithValue("@chatId", chatId);
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            ids.Add(reader.GetInt64(0));
        return ids;
    }
    
    public void Delete(long chatId)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM Messages WHERE ChatId = @chatId;
            DELETE FROM ChatMembers WHERE ChatId = @chatId;
            DELETE FROM ChatInvites WHERE ChatId = @chatId;
            DELETE FROM Chats WHERE Id = @chatId;";
        cmd.Parameters.AddWithValue("@chatId", chatId);
        cmd.ExecuteNonQuery();
    }
    
    // Приглашения
    public void CreateInvite(long chatId, long userId, long invitedBy)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO ChatInvites (ChatId, UserId, InvitedBy, CreatedAt)
            VALUES (@chatId, @userId, @invitedBy, @createdAt)";
        cmd.Parameters.AddWithValue("@chatId", chatId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@invitedBy", invitedBy);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }
    
    public bool HasPendingInvite(long chatId, long userId)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM ChatInvites WHERE ChatId = @chatId AND UserId = @userId";
        cmd.Parameters.AddWithValue("@chatId", chatId);
        cmd.Parameters.AddWithValue("@userId", userId);
        return (long)cmd.ExecuteScalar()! > 0;
    }
    
    public void DeleteInvite(long chatId, long userId)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM ChatInvites WHERE ChatId = @chatId AND UserId = @userId";
        cmd.Parameters.AddWithValue("@chatId", chatId);
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.ExecuteNonQuery();
    }
    
    private Message? GetLastMessage(long chatId)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM Messages WHERE ChatId = @chatId ORDER BY Id DESC LIMIT 1";
        cmd.Parameters.AddWithValue("@chatId", chatId);
        
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
            return ReadMessage(reader);
        return null;
    }
    
    private Chat ReadChat(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new Chat
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            Type = (ChatType)reader.GetInt32(2),
            OwnerId = reader.GetInt64(3),
            CreatedAt = DateTime.Parse(reader.GetString(4))
        };
    }
    
    private Message ReadMessage(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new Message
        {
            Id = reader.GetInt64(0),
            ChatId = reader.GetInt64(1),
            SenderId = reader.GetInt64(2),
            Content = reader.GetString(3),
            Type = (MessageType)reader.GetInt32(4),
            Timestamp = DateTime.Parse(reader.GetString(5)),
            IsEdited = reader.GetInt32(6) == 1,
            EditedAt = reader.IsDBNull(7) ? null : DateTime.Parse(reader.GetString(7))
        };
    }
}

