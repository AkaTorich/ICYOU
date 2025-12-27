using Microsoft.Data.Sqlite;
using ICYOU.SDK;

namespace ICYOU.Core.Database;

public class DatabaseContext : IDisposable
{
    private readonly SqliteConnection _connection;
    
    public DatabaseContext(string dbPath)
    {
        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        InitializeDatabase();
    }
    
    private void InitializeDatabase()
    {
        var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Username TEXT NOT NULL UNIQUE,
                DisplayName TEXT NOT NULL,
                PasswordHash TEXT NOT NULL,
                AvatarPath TEXT,
                Status INTEGER DEFAULT 0,
                LastSeen TEXT,
                CreatedAt TEXT NOT NULL
            );
            
            CREATE TABLE IF NOT EXISTS Sessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                Token TEXT NOT NULL UNIQUE,
                CreatedAt TEXT NOT NULL,
                ExpiresAt TEXT NOT NULL,
                FOREIGN KEY (UserId) REFERENCES Users(Id)
            );
            
            CREATE TABLE IF NOT EXISTS Friends (
                UserId INTEGER NOT NULL,
                FriendId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                PRIMARY KEY (UserId, FriendId),
                FOREIGN KEY (UserId) REFERENCES Users(Id),
                FOREIGN KEY (FriendId) REFERENCES Users(Id)
            );
            
            CREATE TABLE IF NOT EXISTS FriendRequests (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                FromUserId INTEGER NOT NULL,
                ToUserId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (FromUserId) REFERENCES Users(Id),
                FOREIGN KEY (ToUserId) REFERENCES Users(Id)
            );
            
            CREATE TABLE IF NOT EXISTS Chats (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Type INTEGER NOT NULL,
                OwnerId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (OwnerId) REFERENCES Users(Id)
            );
            
            CREATE TABLE IF NOT EXISTS ChatMembers (
                ChatId INTEGER NOT NULL,
                UserId INTEGER NOT NULL,
                JoinedAt TEXT NOT NULL,
                PRIMARY KEY (ChatId, UserId),
                FOREIGN KEY (ChatId) REFERENCES Chats(Id),
                FOREIGN KEY (UserId) REFERENCES Users(Id)
            );
            
            CREATE TABLE IF NOT EXISTS ChatInvites (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL,
                UserId INTEGER NOT NULL,
                InvitedBy INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (ChatId) REFERENCES Chats(Id),
                FOREIGN KEY (UserId) REFERENCES Users(Id),
                FOREIGN KEY (InvitedBy) REFERENCES Users(Id)
            );
            
            CREATE TABLE IF NOT EXISTS Messages (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ChatId INTEGER NOT NULL,
                SenderId INTEGER NOT NULL,
                Content TEXT NOT NULL,
                Type INTEGER NOT NULL,
                Timestamp TEXT NOT NULL,
                IsEdited INTEGER DEFAULT 0,
                EditedAt TEXT,
                FOREIGN KEY (ChatId) REFERENCES Chats(Id),
                FOREIGN KEY (SenderId) REFERENCES Users(Id)
            );
            
            CREATE TABLE IF NOT EXISTS FileTransfers (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                SenderId INTEGER NOT NULL,
                ReceiverId INTEGER NOT NULL,
                ChatId INTEGER,
                FileName TEXT NOT NULL,
                FileSize INTEGER NOT NULL,
                Status INTEGER NOT NULL,
                StartedAt TEXT NOT NULL,
                CompletedAt TEXT,
                FOREIGN KEY (SenderId) REFERENCES Users(Id),
                FOREIGN KEY (ReceiverId) REFERENCES Users(Id),
                FOREIGN KEY (ChatId) REFERENCES Chats(Id)
            );
            
            CREATE TABLE IF NOT EXISTS ModuleStorage (
                ModuleId TEXT NOT NULL,
                Key TEXT NOT NULL,
                Value TEXT NOT NULL,
                PRIMARY KEY (ModuleId, Key)
            );
            
            CREATE INDEX IF NOT EXISTS idx_messages_chat ON Messages(ChatId);
            CREATE INDEX IF NOT EXISTS idx_messages_sender ON Messages(SenderId);
            CREATE INDEX IF NOT EXISTS idx_chatmembers_user ON ChatMembers(UserId);
        ";
        cmd.ExecuteNonQuery();
    }
    
    public SqliteCommand CreateCommand() => _connection.CreateCommand();
    
    public void Dispose()
    {
        _connection.Dispose();
    }
}

