using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.Maui.Storage;
using ICYOU.SDK;

namespace ICYOU.Mobile.Services;

/// <summary>
/// Локальная база данных сообщений для каждого пользователя
/// </summary>
public class LocalDatabaseService : IDisposable
{
    private static LocalDatabaseService? _instance;
    public static LocalDatabaseService Instance => _instance ??= new LocalDatabaseService();
    
    private SqliteConnection? _connection;
    private string? _dbPath;
    private string? _userFolder;
    
    // Шифрование
    private byte[]? _encKey;
    private byte[]? _encIv;
    private bool _encryptionEnabled;
    private string? _passwordHash;
    
    public string? UserFolder => _userFolder;
    public bool EncryptionEnabled => _encryptionEnabled && _encKey != null;
    public bool NeedsPassword => !string.IsNullOrEmpty(_passwordHash) && _encKey == null;
    
    private LocalDatabaseService() { }
    
    private static string GetAppDataDirectory()
    {
        return FileSystem.AppDataDirectory;
    }
    
    /// <summary>
    /// Инициализация БД для пользователя
    /// </summary>
    public void Initialize(string username)
    {
        try
        {
            // Создаём папку пользователя
            var baseDir = GetAppDataDirectory();
            _userFolder = Path.Combine(baseDir, "userdata", SanitizeUsername(username));
            
            DebugLog.Write($"[DB] Creating user folder: {_userFolder}");
            Directory.CreateDirectory(_userFolder);
            
            // Путь к БД
            _dbPath = Path.Combine(_userFolder, "messages.db");
            
            DebugLog.Write($"[DB] Initializing database at: {_dbPath}");
            
            // Открываем соединение
            _connection = new SqliteConnection($"Data Source={_dbPath}");
            _connection.Open();
            
            // Создаём таблицы
            CreateTables();
            
            DebugLog.Write("[DB] Database initialized OK");
            
            // Проверяем наличие пароля
            _passwordHash = GetSetting("enc_password_hash");
            _encryptionEnabled = !string.IsNullOrEmpty(_passwordHash);
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[DB] ERROR initializing database: {ex.Message}");
            DebugLog.Write($"[DB] Stack trace: {ex.StackTrace}");
            throw; // Пробрасываем исключение дальше
        }
    }
    
    #region Encryption
    
    /// <summary>
    /// Устанавливает пароль шифрования
    /// </summary>
    public void SetEncryptionPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            _encKey = null;
            _encIv = null;
            _encryptionEnabled = false;
            _passwordHash = null;
            SetSetting("enc_password_hash", "");
            return;
        }
        
        // Генерируем ключ из пароля
        using var deriveBytes = new Rfc2898DeriveBytes(
            password, 
            Encoding.UTF8.GetBytes("ICYOU_LOCAL_DB_SALT"), 
            100000, 
            HashAlgorithmName.SHA256);
        
        _encKey = deriveBytes.GetBytes(32); // AES-256
        _encIv = deriveBytes.GetBytes(16);  // IV
        
        // Сохраняем хеш пароля
        _passwordHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
        SetSetting("enc_password_hash", _passwordHash);
        _encryptionEnabled = true;
    }
    
    /// <summary>
    /// Проверяет пароль
    /// </summary>
    public bool VerifyPassword(string password)
    {
        if (string.IsNullOrEmpty(_passwordHash))
            return true;
            
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
        if (hash == _passwordHash)
        {
            // Пароль верный - генерируем ключ
            using var deriveBytes = new Rfc2898DeriveBytes(
                password, 
                Encoding.UTF8.GetBytes("ICYOU_LOCAL_DB_SALT"), 
                100000, 
                HashAlgorithmName.SHA256);
            
            _encKey = deriveBytes.GetBytes(32);
            _encIv = deriveBytes.GetBytes(16);
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Отключает шифрование
    /// </summary>
    public void DisableEncryption()
    {
        _encKey = null;
        _encIv = null;
        _encryptionEnabled = false;
        _passwordHash = null;
        SetSetting("enc_password_hash", "");
    }
    
    private string EncryptContent(string content)
    {
        if (!EncryptionEnabled || string.IsNullOrEmpty(content))
            return content;
            
        try
        {
            using var aes = Aes.Create();
            aes.Key = _encKey!;
            aes.IV = _encIv!;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            
            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(content);
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            
            return "ENC:" + Convert.ToBase64String(encryptedBytes);
        }
        catch
        {
            return content;
        }
    }
    
    private string DecryptContent(string content)
    {
        if (string.IsNullOrEmpty(content) || !content.StartsWith("ENC:"))
            return content;
            
        if (_encKey == null)
            return "[🔒 Требуется пароль]";
            
        try
        {
            var encryptedData = Convert.FromBase64String(content.Substring(4));
            
            using var aes = Aes.Create();
            aes.Key = _encKey;
            aes.IV = _encIv!;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            
            using var decryptor = aes.CreateDecryptor();
            var decryptedBytes = decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
            
            return Encoding.UTF8.GetString(decryptedBytes);
        }
        catch
        {
            return "[🔒 Ошибка дешифрования]";
        }
    }
    
    #endregion
    
    private string SanitizeUsername(string username)
    {
        // Убираем недопустимые символы из имени папки
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", username.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }
    
    private void CreateTables()
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS messages (
                id INTEGER PRIMARY KEY,
                chat_id INTEGER NOT NULL,
                sender_id INTEGER NOT NULL,
                sender_name TEXT NOT NULL,
                content TEXT NOT NULL,
                type INTEGER NOT NULL,
                timestamp TEXT NOT NULL,
                status INTEGER DEFAULT 1,
                is_edited INTEGER DEFAULT 0
            );
            
            CREATE INDEX IF NOT EXISTS idx_messages_chat ON messages(chat_id);
            CREATE INDEX IF NOT EXISTS idx_messages_timestamp ON messages(timestamp);
            
            CREATE TABLE IF NOT EXISTS chats (
                id INTEGER PRIMARY KEY,
                name TEXT,
                type INTEGER NOT NULL,
                last_message TEXT,
                last_message_time TEXT,
                unread_count INTEGER DEFAULT 0
            );
            
            CREATE TABLE IF NOT EXISTS friends (
                user_id INTEGER PRIMARY KEY,
                username TEXT NOT NULL,
                display_name TEXT NOT NULL,
                status INTEGER DEFAULT 0,
                private_chat_id INTEGER
            );
            
            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT
            );
            
            CREATE TABLE IF NOT EXISTS files (
                id TEXT PRIMARY KEY,
                message_id INTEGER,
                chat_id INTEGER,
                file_name TEXT NOT NULL,
                file_type TEXT,
                file_path TEXT,
                file_size INTEGER,
                timestamp TEXT NOT NULL
            );
            
            CREATE INDEX IF NOT EXISTS idx_files_chat ON files(chat_id);
            CREATE INDEX IF NOT EXISTS idx_files_message ON files(message_id);
        ";
        cmd.ExecuteNonQuery();
    }
    
    #region Messages
    
    private static long _lastId = 0;
    private static readonly object _idLock = new object();
    
    private long GenerateUniqueId()
    {
        lock (_idLock)
        {
            var newId = DateTime.UtcNow.Ticks;
            if (newId <= _lastId)
                newId = _lastId + 1;
            _lastId = newId;
            return newId;
        }
    }
    
    public void SaveMessage(Message message)
    {
        if (_connection == null)
        {
            DebugLog.Write("[DB] ERROR: Connection is null!");
            return;
        }
        
        // Для файловых сообщений убираем base64 - сохраняем только путь
        var contentToSave = message.Content;
        if (contentToSave.StartsWith("[FILE|"))
        {
            contentToSave = StripBase64FromFileMessage(contentToSave);
        }
        
        // Генерируем уникальный локальный ID если нужно
        var localId = message.Id > 0 ? message.Id : GenerateUniqueId();
        
        try
        {
            // Проверяем нет ли уже такого сообщения (по содержимому и времени)
            using var checkCmd = _connection.CreateCommand();
            checkCmd.CommandText = @"
                SELECT COUNT(*) FROM messages 
                WHERE chat_id = $chatId AND sender_id = $senderId 
                AND timestamp = $timestamp AND content = $content
            ";
            checkCmd.Parameters.AddWithValue("$chatId", message.ChatId);
            checkCmd.Parameters.AddWithValue("$senderId", message.SenderId);
            checkCmd.Parameters.AddWithValue("$timestamp", message.Timestamp.ToString("O"));
            checkCmd.Parameters.AddWithValue("$content", EncryptContent(contentToSave));
            
            var exists = Convert.ToInt64(checkCmd.ExecuteScalar()) > 0;
            if (exists)
            {
                DebugLog.Write($"[DB] Message already exists, skipping");
                return;
            }
            
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO messages (id, chat_id, sender_id, sender_name, content, type, timestamp, status, is_edited)
                VALUES ($id, $chatId, $senderId, $senderName, $content, $type, $timestamp, $status, $isEdited)
            ";
            cmd.Parameters.AddWithValue("$id", localId);
            cmd.Parameters.AddWithValue("$chatId", message.ChatId);
            cmd.Parameters.AddWithValue("$senderId", message.SenderId);
            cmd.Parameters.AddWithValue("$senderName", message.SenderName);
            cmd.Parameters.AddWithValue("$content", EncryptContent(contentToSave));
            cmd.Parameters.AddWithValue("$type", (int)message.Type);
            cmd.Parameters.AddWithValue("$timestamp", message.Timestamp.ToString("O"));
            cmd.Parameters.AddWithValue("$status", (int)message.Status);
            cmd.Parameters.AddWithValue("$isEdited", message.IsEdited ? 1 : 0);
            cmd.ExecuteNonQuery();
            
            DebugLog.Write($"[DB] Saved message ID={localId} ChatId={message.ChatId} Type={message.Type}");
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[DB] ERROR saving message: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Убирает base64 из файлового сообщения, оставляя только путь
    /// Формат: [FILE|имя|тип|путь|base64] -> [FILE|имя|тип|путь|]
    /// </summary>
    private string StripBase64FromFileMessage(string content)
    {
        try
        {
            // [FILE|имя|тип|путь|base64]
            var lastPipe = content.LastIndexOf('|');
            if (lastPipe > 0)
            {
                // Оставляем всё до последнего | и добавляем ]
                var withoutBase64 = content.Substring(0, lastPipe + 1);
                if (!withoutBase64.EndsWith("]"))
                    withoutBase64 += "]";
                return withoutBase64;
            }
        }
        catch { }
        return content;
    }
    
    public void SaveMessages(IEnumerable<Message> messages)
    {
        if (_connection == null) return;
        
        using var transaction = _connection.BeginTransaction();
        try
        {
            foreach (var message in messages)
            {
                SaveMessage(message);
            }
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
        }
    }
    
    public List<Message> GetMessages(long chatId, int limit = 100, int offset = 0)
    {
        var messages = new List<Message>();
        if (_connection == null)
        {
            DebugLog.Write("[DB] GetMessages: Connection is null!");
            return messages;
        }
        
        DebugLog.Write($"[DB] GetMessages for ChatId={chatId}");
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, chat_id, sender_id, sender_name, content, type, timestamp, status, is_edited
            FROM messages
            WHERE chat_id = $chatId
            ORDER BY timestamp DESC
            LIMIT $limit OFFSET $offset
        ";
        cmd.Parameters.AddWithValue("$chatId", chatId);
        cmd.Parameters.AddWithValue("$limit", limit);
        cmd.Parameters.AddWithValue("$offset", offset);
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            messages.Add(new Message
            {
                Id = reader.GetInt64(0),
                ChatId = reader.GetInt64(1),
                SenderId = reader.GetInt64(2),
                SenderName = reader.GetString(3),
                Content = DecryptContent(reader.GetString(4)), // Дешифруем
                Type = (MessageType)reader.GetInt32(5),
                Timestamp = DateTime.Parse(reader.GetString(6)),
                Status = (MessageStatus)reader.GetInt32(7),
                IsEdited = reader.GetInt32(8) == 1
            });
        }
        
        messages.Reverse(); // Возвращаем в хронологическом порядке
        DebugLog.Write($"[DB] Found {messages.Count} messages for ChatId={chatId}");
        return messages;
    }
    
    public List<Message> GetMessagesByFriend(long friendId, int limit = 100)
    {
        var messages = new List<Message>();
        if (_connection == null) return messages;
        
        // Ищем ТОЛЬКО сообщения в личном чате с этим другом
        // chat_id = friendId (положительный ID друга)
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT id, chat_id, sender_id, sender_name, content, type, timestamp, status, is_edited
            FROM messages
            WHERE chat_id = $friendId
            ORDER BY timestamp DESC
            LIMIT $limit
        ";
        cmd.Parameters.AddWithValue("$friendId", friendId);
        cmd.Parameters.AddWithValue("$limit", limit);
        
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            messages.Add(new Message
            {
                Id = reader.GetInt64(0),
                ChatId = reader.GetInt64(1),
                SenderId = reader.GetInt64(2),
                SenderName = reader.GetString(3),
                Content = DecryptContent(reader.GetString(4)), // Дешифруем
                Type = (MessageType)reader.GetInt32(5),
                Timestamp = DateTime.Parse(reader.GetString(6)),
                Status = (MessageStatus)reader.GetInt32(7),
                IsEdited = reader.GetInt32(8) == 1
            });
        }
        
        messages.Reverse();
        return messages;
    }
    
    #endregion
    
    #region Chats
    
    public void SaveChat(long id, string? name, int type)
    {
        if (_connection == null) return;
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO chats (id, name, type)
            VALUES ($id, $name, $type)
        ";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.Parameters.AddWithValue("$name", name ?? "");
        cmd.Parameters.AddWithValue("$type", type);
        cmd.ExecuteNonQuery();
    }
    
    public void UpdateChatLastMessage(long chatId, string lastMessage, DateTime time)
    {
        if (_connection == null) return;
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE chats SET last_message = $msg, last_message_time = $time WHERE id = $id
        ";
        cmd.Parameters.AddWithValue("$id", chatId);
        cmd.Parameters.AddWithValue("$msg", lastMessage);
        cmd.Parameters.AddWithValue("$time", time.ToString("O"));
        cmd.ExecuteNonQuery();
    }
    
    #endregion
    
    #region Friends
    
    public void SaveFriend(long userId, string username, string displayName, int status, long? privateChatId = null)
    {
        if (_connection == null) return;
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO friends (user_id, username, display_name, status, private_chat_id)
            VALUES ($userId, $username, $displayName, $status, $privateChatId)
        ";
        cmd.Parameters.AddWithValue("$userId", userId);
        cmd.Parameters.AddWithValue("$username", username);
        cmd.Parameters.AddWithValue("$displayName", displayName);
        cmd.Parameters.AddWithValue("$status", status);
        cmd.Parameters.AddWithValue("$privateChatId", privateChatId.HasValue ? privateChatId.Value : DBNull.Value);
        cmd.ExecuteNonQuery();
    }
    
    public long? GetFriendPrivateChatId(long friendId)
    {
        if (_connection == null) return null;
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT private_chat_id FROM friends WHERE user_id = $id";
        cmd.Parameters.AddWithValue("$id", friendId);
        
        var result = cmd.ExecuteScalar();
        return result != DBNull.Value && result != null ? (long?)Convert.ToInt64(result) : null;
    }
    
    public void SetFriendPrivateChatId(long friendId, long chatId)
    {
        if (_connection == null) return;
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "UPDATE friends SET private_chat_id = $chatId WHERE user_id = $id";
        cmd.Parameters.AddWithValue("$id", friendId);
        cmd.Parameters.AddWithValue("$chatId", chatId);
        cmd.ExecuteNonQuery();
    }
    
    #endregion
    
    #region Settings
    
    public void SetSetting(string key, string value)
    {
        if (_connection == null) return;
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO settings (key, value) VALUES ($key, $value)";
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        cmd.ExecuteNonQuery();
    }
    
    public string? GetSetting(string key)
    {
        if (_connection == null) return null;
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = $key";
        cmd.Parameters.AddWithValue("$key", key);
        
        return cmd.ExecuteScalar() as string;
    }
    
    #endregion
    
    #region Files
    
    public void SaveFile(string fileId, long messageId, long chatId, string fileName, string fileType, string filePath, long fileSize)
    {
        if (_connection == null) return;
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO files (id, message_id, chat_id, file_name, file_type, file_path, file_size, timestamp)
            VALUES ($id, $messageId, $chatId, $fileName, $fileType, $filePath, $fileSize, $timestamp)
        ";
        cmd.Parameters.AddWithValue("$id", fileId);
        cmd.Parameters.AddWithValue("$messageId", messageId);
        cmd.Parameters.AddWithValue("$chatId", chatId);
        cmd.Parameters.AddWithValue("$fileName", fileName);
        cmd.Parameters.AddWithValue("$fileType", fileType);
        cmd.Parameters.AddWithValue("$filePath", filePath);
        cmd.Parameters.AddWithValue("$fileSize", fileSize);
        cmd.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }
    
    public string? GetFilePath(string fileId)
    {
        if (_connection == null) return null;
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT file_path FROM files WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", fileId);
        return cmd.ExecuteScalar() as string;
    }
    
    public long GetCacheSize()
    {
        if (_connection == null) return 0;
        
        long totalSize = 0;
        
        // Размер файлов в Downloads
        var downloadsPath = Path.Combine(GetAppDataDirectory(), "Downloads");
        if (Directory.Exists(downloadsPath))
        {
            var files = Directory.GetFiles(downloadsPath, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try { totalSize += new FileInfo(file).Length; } catch { }
            }
        }
        
        // Размер БД
        if (!string.IsNullOrEmpty(_dbPath) && File.Exists(_dbPath))
        {
            try { totalSize += new FileInfo(_dbPath).Length; } catch { }
        }
        
        return totalSize;
    }
    
    public int GetFilesCount()
    {
        if (_connection == null) return 0;
        
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM files";
        var result = cmd.ExecuteScalar();
        return result != null ? Convert.ToInt32(result) : 0;
    }
    
    public void ClearFileCache()
    {
        // Удаляем файлы из Downloads
        var downloadsPath = Path.Combine(GetAppDataDirectory(), "Downloads");
        if (Directory.Exists(downloadsPath))
        {
            try
            {
                var files = Directory.GetFiles(downloadsPath);
                foreach (var file in files)
                {
                    try { File.Delete(file); } catch { }
                }
            }
            catch { }
        }
        
        // Очищаем таблицу файлов
        if (_connection != null)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM files";
            cmd.ExecuteNonQuery();
        }
    }
    
    public void ClearAllData()
    {
        ClearFileCache();
        
        if (_connection != null)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM messages;
                DELETE FROM chats;
                DELETE FROM friends;
            ";
            cmd.ExecuteNonQuery();
            
            // Сжимаем БД
            using var vacuum = _connection.CreateCommand();
            vacuum.CommandText = "VACUUM";
            vacuum.ExecuteNonQuery();
        }
    }
    
    #endregion
    
    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
        _connection = null;
    }
    
    public void Close()
    {
        Dispose();
        _instance = null;
    }
}

