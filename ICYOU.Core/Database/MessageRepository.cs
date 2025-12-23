using ICYOU.SDK;

namespace ICYOU.Core.Database;

public class MessageRepository
{
    private readonly DatabaseContext _db;
    
    public MessageRepository(DatabaseContext db)
    {
        _db = db;
    }
    
    public Message? GetById(long id)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM Messages WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
            return ReadMessage(reader);
        return null;
    }
    
    public Message Create(long chatId, long senderId, string content, MessageType type)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Messages (ChatId, SenderId, Content, Type, Timestamp, IsEdited)
            VALUES (@chatId, @senderId, @content, @type, @timestamp, 0);
            SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("@chatId", chatId);
        cmd.Parameters.AddWithValue("@senderId", senderId);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.Parameters.AddWithValue("@type", (int)type);
        cmd.Parameters.AddWithValue("@timestamp", DateTime.UtcNow.ToString("O"));
        
        var id = (long)cmd.ExecuteScalar()!;
        return GetById(id)!;
    }
    
    public List<Message> GetChatHistory(long chatId, int count = 50, int offset = 0, long afterId = 0)
    {
        var messages = new List<Message>();
        var cmd = _db.CreateCommand();
        
        if (afterId > 0)
        {
            // Только сообщения после указанного ID
            cmd.CommandText = @"
                SELECT * FROM Messages 
                WHERE ChatId = @chatId AND Id > @afterId
                ORDER BY Id ASC 
                LIMIT @count";
            cmd.Parameters.AddWithValue("@afterId", afterId);
        }
        else
        {
            cmd.CommandText = @"
                SELECT * FROM Messages 
                WHERE ChatId = @chatId 
                ORDER BY Id DESC 
                LIMIT @count OFFSET @offset";
            cmd.Parameters.AddWithValue("@offset", offset);
        }
        
        cmd.Parameters.AddWithValue("@chatId", chatId);
        cmd.Parameters.AddWithValue("@count", count);
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            messages.Add(ReadMessage(reader));
        
        // Для afterId уже отсортировано по возрастанию, для остальных - разворачиваем
        if (afterId == 0)
            messages.Reverse();
            
        return messages;
    }
    
    public void Edit(long messageId, string newContent)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            UPDATE Messages 
            SET Content = @content, IsEdited = 1, EditedAt = @editedAt 
            WHERE Id = @id";
        cmd.Parameters.AddWithValue("@content", newContent);
        cmd.Parameters.AddWithValue("@editedAt", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", messageId);
        cmd.ExecuteNonQuery();
    }
    
    public void Delete(long messageId)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM Messages WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", messageId);
        cmd.ExecuteNonQuery();
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

