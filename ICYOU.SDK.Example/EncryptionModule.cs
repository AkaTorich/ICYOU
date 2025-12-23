using ICYOU.SDK;
using System.Security.Cryptography;
using System.Text;

namespace ICYOU.SDK.Example;

/// <summary>
/// Модуль шифрования сообщений - базовая реализация
/// Это шаблон для будущего модуля шифрования
/// </summary>
[ModuleInfo("security.encryption", "Encryption", "1.0.0", 
    Author = "ICYOU Team", 
    Description = "Шифрование сообщений AES")]
public class EncryptionModule : ModuleBase, IModuleSettings
{
    private byte[]? _key;
    private bool _enabled = false;
    private string _password = "";
    
    public IEnumerable<ModuleSetting> GetSettings()
    {
        yield return new ModuleSetting
        {
            Key = "enabled",
            DisplayName = "Включить шифрование",
            Description = "Шифровать все исходящие сообщения",
            Type = ModuleSettingType.Boolean,
            CurrentValue = _enabled,
            DefaultValue = false
        };
        
        yield return new ModuleSetting
        {
            Key = "password",
            DisplayName = "Пароль шифрования",
            Description = "Пароль для генерации ключа (одинаковый у обоих собеседников)",
            Type = ModuleSettingType.Password,
            CurrentValue = _password,
            DefaultValue = ""
        };
    }
    
    public void ApplySetting(string key, object value)
    {
        switch (key)
        {
            case "enabled":
                _enabled = (bool)value;
                Logger.Info(_enabled ? "Шифрование включено" : "Шифрование отключено");
                break;
            case "password":
                var pwd = value?.ToString() ?? "";
                if (!string.IsNullOrEmpty(pwd))
                {
                    _password = pwd;
                    GenerateKeyFromPassword(pwd);
                }
                break;
        }
    }
    
    protected override void OnInitialize()
    {
        Logger.Info("Модуль шифрования инициализирован");
        
        // Загружаем ключ из хранилища
        LoadKeyAsync();
        
        // Регистрируем перехватчики
        Messages.RegisterOutgoingInterceptor(EncryptMessage);
        Messages.RegisterIncomingInterceptor(DecryptMessage);
        
        Subscribe<MessageReceivedEvent>(OnMessageReceived);
    }
    
    private async void LoadKeyAsync()
    {
        var keyBase64 = await Storage.GetAsync<string>("encryption_key");
        if (!string.IsNullOrEmpty(keyBase64))
        {
            _key = Convert.FromBase64String(keyBase64);
            _enabled = true;
            Logger.Info("Ключ шифрования загружен");
        }
    }
    
    private void OnMessageReceived(MessageReceivedEvent evt)
    {
        // Обрабатываем команды
        var content = evt.Message.Content;
        
        if (content == "/encrypt on")
        {
            EnableEncryption();
        }
        else if (content == "/encrypt off")
        {
            _enabled = false;
            Logger.Info("Шифрование отключено");
        }
        else if (content.StartsWith("/encrypt key "))
        {
            var password = content.Substring(13);
            GenerateKeyFromPassword(password);
        }
    }
    
    private void EnableEncryption()
    {
        if (_key == null)
        {
            Logger.Warning("Ключ не установлен. Используйте /encrypt key <пароль>");
            return;
        }
        _enabled = true;
        Logger.Info("Шифрование включено");
    }
    
    private void GenerateKeyFromPassword(string password)
    {
        using var sha256 = SHA256.Create();
        _key = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        
        // Сохраняем ключ
        Storage.SetAsync("encryption_key", Convert.ToBase64String(_key));
        
        _enabled = true;
        Logger.Info("Ключ шифрования установлен");
    }
    
    private Message? EncryptMessage(Message message)
    {
        if (!_enabled || _key == null)
            return message;
            
        try
        {
            var encrypted = Encrypt(message.Content);
            message.Content = $"[ENC]{encrypted}";
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка шифрования: {ex.Message}");
        }
        
        return message;
    }
    
    private Message? DecryptMessage(Message message)
    {
        if (_key == null || !message.Content.StartsWith("[ENC]"))
            return message;
            
        try
        {
            var encrypted = message.Content.Substring(5);
            message.Content = Decrypt(encrypted);
        }
        catch (Exception ex)
        {
            Logger.Error($"Ошибка расшифровки: {ex.Message}");
            message.Content = "[Не удалось расшифровать]";
        }
        
        return message;
    }
    
    private string Encrypt(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key!;
        aes.GenerateIV();
        
        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
        
        // IV + encrypted data
        var result = new byte[aes.IV.Length + encryptedBytes.Length];
        aes.IV.CopyTo(result, 0);
        encryptedBytes.CopyTo(result, aes.IV.Length);
        
        return Convert.ToBase64String(result);
    }
    
    private string Decrypt(string cipherText)
    {
        var data = Convert.FromBase64String(cipherText);
        
        using var aes = Aes.Create();
        aes.Key = _key!;
        
        var iv = new byte[16];
        var encrypted = new byte[data.Length - 16];
        Array.Copy(data, 0, iv, 0, 16);
        Array.Copy(data, 16, encrypted, 0, encrypted.Length);
        
        aes.IV = iv;
        
        using var decryptor = aes.CreateDecryptor();
        var decryptedBytes = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
        
        return Encoding.UTF8.GetString(decryptedBytes);
    }
}

