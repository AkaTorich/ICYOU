using ICYOU.SDK;
using System.Security.Cryptography;
using System.Text;

namespace ICYOU.Core.Database;

public class UserRepository
{
    private readonly DatabaseContext _db;
    
    public UserRepository(DatabaseContext db)
    {
        _db = db;
    }
    
    public User? GetById(long id)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
            return ReadUser(reader);
        return null;
    }
    
    public User? GetByUsername(string username)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users WHERE Username = @username";
        cmd.Parameters.AddWithValue("@username", username);
        
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
            return ReadUser(reader);
        return null;
    }
    
    public User? Create(string username, string displayName, string passwordHash)
    {
        // Генерируем уникальный ID на основе времени
        var id = DateTime.UtcNow.Ticks;
        
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Users (Id, Username, DisplayName, PasswordHash, Status, CreatedAt)
            VALUES (@id, @username, @displayName, @passwordHash, 0, @createdAt)";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@username", username);
        cmd.Parameters.AddWithValue("@displayName", displayName);
        cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
        
        cmd.ExecuteNonQuery();
        return GetById(id);
    }
    
    public bool ValidatePassword(string username, string passwordHash)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT PasswordHash FROM Users WHERE Username = @username";
        cmd.Parameters.AddWithValue("@username", username);
        
        var storedHash = cmd.ExecuteScalar() as string;
        return storedHash == passwordHash;
    }
    
    public void UpdateStatus(long userId, UserStatus status)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "UPDATE Users SET Status = @status, LastSeen = @lastSeen WHERE Id = @id";
        cmd.Parameters.AddWithValue("@status", (int)status);
        cmd.Parameters.AddWithValue("@lastSeen", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@id", userId);
        cmd.ExecuteNonQuery();
    }
    
    public List<User> Search(string query)
    {
        var users = new List<User>();
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users WHERE Username LIKE @query OR DisplayName LIKE @query LIMIT 50";
        cmd.Parameters.AddWithValue("@query", $"%{query}%");
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            users.Add(ReadUser(reader));
        return users;
    }
    
    public List<User> GetOnlineUsers()
    {
        var users = new List<User>();
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM Users WHERE Status > 0";
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            users.Add(ReadUser(reader));
        return users;
    }
    
    private User ReadUser(Microsoft.Data.Sqlite.SqliteDataReader reader)
    {
        return new User
        {
            Id = reader.GetInt64(0),
            Username = reader.GetString(1),
            DisplayName = reader.GetString(2),
            AvatarPath = reader.IsDBNull(4) ? null : reader.GetString(4),
            Status = (UserStatus)reader.GetInt32(5),
            LastSeen = reader.IsDBNull(6) ? DateTime.MinValue : DateTime.Parse(reader.GetString(6)),
            CreatedAt = DateTime.Parse(reader.GetString(7))
        };
    }
    
    // Sessions
    public string CreateSession(long userId)
    {
        var token = GenerateToken();
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Sessions (UserId, Token, CreatedAt, ExpiresAt)
            VALUES (@userId, @token, @createdAt, @expiresAt)";
        cmd.Parameters.AddWithValue("@userId", userId);
        cmd.Parameters.AddWithValue("@token", token);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("@expiresAt", DateTime.UtcNow.AddDays(30).ToString("O"));
        cmd.ExecuteNonQuery();
        return token;
    }
    
    public long? ValidateSession(string token)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT UserId FROM Sessions WHERE Token = @token AND ExpiresAt > @now";
        cmd.Parameters.AddWithValue("@token", token);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
        
        var result = cmd.ExecuteScalar();
        return result as long?;
    }
    
    public void DeleteSession(string token)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM Sessions WHERE Token = @token";
        cmd.Parameters.AddWithValue("@token", token);
        cmd.ExecuteNonQuery();
    }
    
    private string GenerateToken()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }
}

