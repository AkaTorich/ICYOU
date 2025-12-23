using ICYOU.SDK;

namespace ICYOU.Core.Database;

public class FriendRepository
{
    private readonly DatabaseContext _db;
    private readonly UserRepository _userRepo;
    
    public FriendRepository(DatabaseContext db, UserRepository userRepo)
    {
        _db = db;
        _userRepo = userRepo;
    }
    
    public List<User> GetFriends(long userId)
    {
        var friends = new List<User>();
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT FriendId FROM Friends WHERE UserId = @userId";
        cmd.Parameters.AddWithValue("@userId", userId);
        
        using var reader = cmd.ExecuteReader();
        var ids = new List<long>();
        while (reader.Read())
            ids.Add(reader.GetInt64(0));
            
        foreach (var id in ids)
        {
            var user = _userRepo.GetById(id);
            if (user != null)
                friends.Add(user);
        }
        
        return friends;
    }
    
    public bool AreFriends(long userId1, long userId2)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Friends WHERE UserId = @user1 AND FriendId = @user2";
        cmd.Parameters.AddWithValue("@user1", userId1);
        cmd.Parameters.AddWithValue("@user2", userId2);
        return (long)cmd.ExecuteScalar()! > 0;
    }
    
    public void AddFriend(long userId, long friendId)
    {
        // Добавляем в обе стороны
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT OR IGNORE INTO Friends (UserId, FriendId, CreatedAt)
            VALUES (@user1, @user2, @createdAt);
            INSERT OR IGNORE INTO Friends (UserId, FriendId, CreatedAt)
            VALUES (@user2, @user1, @createdAt);";
        cmd.Parameters.AddWithValue("@user1", userId);
        cmd.Parameters.AddWithValue("@user2", friendId);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }
    
    public void RemoveFriend(long userId, long friendId)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            DELETE FROM Friends WHERE UserId = @user1 AND FriendId = @user2;
            DELETE FROM Friends WHERE UserId = @user2 AND FriendId = @user1;";
        cmd.Parameters.AddWithValue("@user1", userId);
        cmd.Parameters.AddWithValue("@user2", friendId);
        cmd.ExecuteNonQuery();
    }
    
    // Запросы в друзья
    public void CreateFriendRequest(long fromUserId, long toUserId)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO FriendRequests (FromUserId, ToUserId, CreatedAt)
            VALUES (@from, @to, @createdAt)";
        cmd.Parameters.AddWithValue("@from", fromUserId);
        cmd.Parameters.AddWithValue("@to", toUserId);
        cmd.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }
    
    public bool HasPendingRequest(long fromUserId, long toUserId)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM FriendRequests WHERE FromUserId = @from AND ToUserId = @to";
        cmd.Parameters.AddWithValue("@from", fromUserId);
        cmd.Parameters.AddWithValue("@to", toUserId);
        return (long)cmd.ExecuteScalar()! > 0;
    }
    
    public void DeleteFriendRequest(long fromUserId, long toUserId)
    {
        var cmd = _db.CreateCommand();
        cmd.CommandText = "DELETE FROM FriendRequests WHERE FromUserId = @from AND ToUserId = @to";
        cmd.Parameters.AddWithValue("@from", fromUserId);
        cmd.Parameters.AddWithValue("@to", toUserId);
        cmd.ExecuteNonQuery();
    }
    
    public List<(long FromUserId, User FromUser)> GetPendingRequests(long userId)
    {
        var requests = new List<(long, User)>();
        var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT FromUserId FROM FriendRequests WHERE ToUserId = @userId";
        cmd.Parameters.AddWithValue("@userId", userId);
        
        using var reader = cmd.ExecuteReader();
        var ids = new List<long>();
        while (reader.Read())
            ids.Add(reader.GetInt64(0));
            
        foreach (var id in ids)
        {
            var user = _userRepo.GetById(id);
            if (user != null)
                requests.Add((id, user));
        }
        
        return requests;
    }
}

