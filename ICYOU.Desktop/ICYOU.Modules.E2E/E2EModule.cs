using System.Security.Cryptography;
using System.Text;
using ICYOU.SDK;

namespace ICYOU.Modules.E2E;

/// <summary>
/// Модуль сквозного шифрования E2E
/// Шифрует сообщения AES-256 с обменом ключами через RSA
/// </summary>
public class E2EModule : IModule, IModuleSettings
{
    public string Id => "icyou.e2e";
    public string Name => "Шифрование E2E";
    public string Version => "1.0.0";
    public string Author => "ICYOU Team";
    public string Description => "Сквозное шифрование сообщений AES-256";
    
    private IModuleContext? _context;
    private bool _enabled = true;
    private bool _encryptFiles = false;
    
    // Ключи шифрования
    private RSA? _rsa;
    private readonly Dictionary<long, byte[]> _sessionKeys = new(); // userId -> AES key
    private readonly Dictionary<long, string> _publicKeys = new();   // userId -> RSA public key
    
    private const string EncryptedPrefix = "[E2E|";
    private const string KeyExchangePrefix = "[E2EKEY|";
    
    public void Initialize(IModuleContext context)
    {
        _context = context;
        
        // Загружаем настройки
        _enabled = context.Storage.Get("enabled", true);
        _encryptFiles = context.Storage.Get("encryptFiles", false);
        
        // Генерируем или загружаем RSA ключи
        InitializeKeys(context);
        
        // Регистрируем перехватчики
        context.MessageService.RegisterOutgoingInterceptor(EncryptMessage);
        context.MessageService.RegisterIncomingInterceptor(DecryptMessage);
        
        // Обработка обмена ключами
        context.RegisterEventHandler<MessageReceivedEvent>(HandleKeyExchange);
        
        context.Logger.Info("Модуль E2E шифрования инициализирован");
    }
    
    public void Shutdown()
    {
        _rsa?.Dispose();
        _sessionKeys.Clear();
        _publicKeys.Clear();
        _context?.Logger.Info("Модуль E2E шифрования выгружен");
    }
    
    private void InitializeKeys(IModuleContext context)
    {
        var privateKeyXml = context.Storage.Get<string?>("privateKey", null);
        
        _rsa = RSA.Create(2048);
        
        if (!string.IsNullOrEmpty(privateKeyXml))
        {
            try
            {
                _rsa.FromXmlString(privateKeyXml);
            }
            catch
            {
                // Если не удалось загрузить - генерируем новый
                context.Storage.Set("privateKey", _rsa.ToXmlString(true));
                context.Storage.Set("publicKey", _rsa.ToXmlString(false));
            }
        }
        else
        {
            // Сохраняем новые ключи
            context.Storage.Set("privateKey", _rsa.ToXmlString(true));
            context.Storage.Set("publicKey", _rsa.ToXmlString(false));
        }
    }
    
    /// <summary>
    /// Шифрует исходящее сообщение
    /// </summary>
    private Message? EncryptMessage(Message message)
    {
        if (!_enabled) return message;
        if (message.Type != MessageType.Text) return message;
        if (message.Content.StartsWith(KeyExchangePrefix)) return message; // Не шифруем обмен ключами
        
        // Получаем или генерируем сессионный ключ для собеседника
        var targetUserId = GetTargetUserId(message);
        if (targetUserId == 0) return message;
        
        if (!_sessionKeys.TryGetValue(targetUserId, out var sessionKey))
        {
            // Нет ключа - отправляем запрос на обмен
            RequestKeyExchange(targetUserId);
            _context?.Logger.Debug($"Нет ключа для {targetUserId}, запрошен обмен");
            return message; // Пока отправляем без шифрования
        }
        
        try
        {
            var encrypted = EncryptAES(message.Content, sessionKey);
            message.Content = $"{EncryptedPrefix}{Convert.ToBase64String(encrypted)}]";
            _context?.Logger.Debug($"Сообщение зашифровано для {targetUserId}");
        }
        catch (Exception ex)
        {
            _context?.Logger.Error($"Ошибка шифрования: {ex.Message}");
        }
        
        return message;
    }
    
    /// <summary>
    /// Расшифровывает входящее сообщение
    /// </summary>
    private Message? DecryptMessage(Message message)
    {
        if (!_enabled) return message;
        if (!message.Content.StartsWith(EncryptedPrefix)) return message;
        
        var senderId = message.SenderId;
        
        if (!_sessionKeys.TryGetValue(senderId, out var sessionKey))
        {
            // Нет ключа - не можем расшифровать
            message.Content = "🔒 [Зашифрованное сообщение - ключ недоступен]";
            return message;
        }
        
        try
        {
            var endIndex = message.Content.LastIndexOf(']');
            var encryptedBase64 = message.Content.Substring(EncryptedPrefix.Length, endIndex - EncryptedPrefix.Length);
            var encrypted = Convert.FromBase64String(encryptedBase64);
            
            message.Content = DecryptAES(encrypted, sessionKey);
            message.Content = "🔐 " + message.Content; // Индикатор что сообщение было зашифровано
        }
        catch (Exception ex)
        {
            _context?.Logger.Error($"Ошибка расшифровки: {ex.Message}");
            message.Content = "🔒 [Ошибка расшифровки]";
        }
        
        return message;
    }
    
    /// <summary>
    /// Обрабатывает обмен ключами
    /// </summary>
    private void HandleKeyExchange(MessageReceivedEvent evt)
    {
        if (!evt.Message.Content.StartsWith(KeyExchangePrefix)) return;
        
        try
        {
            var endIndex = evt.Message.Content.LastIndexOf(']');
            var keyData = evt.Message.Content.Substring(KeyExchangePrefix.Length, endIndex - KeyExchangePrefix.Length);
            var parts = keyData.Split('|', 2);
            
            if (parts[0] == "REQUEST")
            {
                // Получили запрос - отправляем свой публичный ключ
                var myPublicKey = _rsa!.ToXmlString(false);
                _context?.MessageService.SendPrivateMessageAsync(evt.Message.SenderId, 
                    $"{KeyExchangePrefix}PUBKEY|{myPublicKey}]");
                
                // Сохраняем публичный ключ отправителя
                if (parts.Length > 1)
                {
                    _publicKeys[evt.Message.SenderId] = parts[1];
                }
            }
            else if (parts[0] == "PUBKEY" && parts.Length > 1)
            {
                // Получили публичный ключ - генерируем сессионный ключ и отправляем
                _publicKeys[evt.Message.SenderId] = parts[1];
                
                var sessionKey = GenerateSessionKey();
                _sessionKeys[evt.Message.SenderId] = sessionKey;
                
                // Шифруем сессионный ключ RSA публичным ключом получателя
                using var theirRsa = RSA.Create();
                theirRsa.FromXmlString(parts[1]);
                var encryptedSessionKey = theirRsa.Encrypt(sessionKey, RSAEncryptionPadding.OaepSHA256);
                
                _context?.MessageService.SendPrivateMessageAsync(evt.Message.SenderId,
                    $"{KeyExchangePrefix}SESSION|{Convert.ToBase64String(encryptedSessionKey)}]");
                
                _context?.Logger.Info($"Установлен защищённый канал с пользователем {evt.Message.SenderId}");
            }
            else if (parts[0] == "SESSION" && parts.Length > 1)
            {
                // Получили зашифрованный сессионный ключ
                var encryptedSessionKey = Convert.FromBase64String(parts[1]);
                var sessionKey = _rsa!.Decrypt(encryptedSessionKey, RSAEncryptionPadding.OaepSHA256);
                _sessionKeys[evt.Message.SenderId] = sessionKey;
                
                _context?.Logger.Info($"Установлен защищённый канал с пользователем {evt.Message.SenderId}");
            }
        }
        catch (Exception ex)
        {
            _context?.Logger.Error($"Ошибка обмена ключами: {ex.Message}");
        }
    }
    
    private void RequestKeyExchange(long userId)
    {
        var myPublicKey = _rsa!.ToXmlString(false);
        _context?.MessageService.SendPrivateMessageAsync(userId, $"{KeyExchangePrefix}REQUEST|{myPublicKey}]");
    }
    
    private long GetTargetUserId(Message message)
    {
        // Для личных чатов - возвращаем ID собеседника
        // В реальной реализации нужно получить через ChatService
        return message.ChatId; // Упрощённо
    }
    
    private byte[] GenerateSessionKey()
    {
        var key = new byte[32]; // 256 бит
        RandomNumberGenerator.Fill(key);
        return key;
    }
    
    private byte[] EncryptAES(string plainText, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        
        // Prepend IV to encrypted data
        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
        
        return result;
    }
    
    private string DecryptAES(byte[] cipherText, byte[] key)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        
        // Extract IV from beginning
        var iv = new byte[16];
        Buffer.BlockCopy(cipherText, 0, iv, 0, 16);
        aes.IV = iv;
        
        using var decryptor = aes.CreateDecryptor();
        var decrypted = decryptor.TransformFinalBlock(cipherText, 16, cipherText.Length - 16);
        
        return Encoding.UTF8.GetString(decrypted);
    }
    
    #region IModuleSettings
    
    public IEnumerable<ModuleSetting> GetSettings()
    {
        return new[]
        {
            new ModuleSetting
            {
                Key = "enabled",
                DisplayName = "Включено",
                Description = "Включить E2E шифрование",
                Type = ModuleSettingType.Boolean,
                CurrentValue = _enabled,
                DefaultValue = true
            },
            new ModuleSetting
            {
                Key = "encryptFiles",
                DisplayName = "Шифровать файлы",
                Description = "Шифровать передаваемые файлы",
                Type = ModuleSettingType.Boolean,
                CurrentValue = _encryptFiles,
                DefaultValue = false
            }
        };
    }
    
    public void ApplySetting(string key, object value)
    {
        switch (key)
        {
            case "enabled":
                _enabled = Convert.ToBoolean(value);
                _context?.Storage.Set("enabled", _enabled);
                break;
            case "encryptFiles":
                _encryptFiles = Convert.ToBoolean(value);
                _context?.Storage.Set("encryptFiles", _encryptFiles);
                break;
        }
    }
    
    #endregion
}

