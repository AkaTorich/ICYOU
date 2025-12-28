using System.IO;
using System.Text.Json;
using Microsoft.Maui.Storage;
using ICYOU.SDK;

namespace ICYOU.Mobile.Services;

public class ClientSettings
{
    public string? EmotePack { get; set; }
    public string Theme { get; set; } = "Dark";
    public bool NotifyMessages { get; set; } = true;
    public bool NotifySounds { get; set; } = true;
    public bool NotifyFriends { get; set; } = true;
    public string? LastServer { get; set; }
    public int LastPort { get; set; } = 7777;
}

public class SettingsService
{
    private static SettingsService? _instance;
    public static SettingsService Instance => _instance ??= new SettingsService();
    
    private string? _settingsPath;
    private ClientSettings? _settings;
    private bool _loaded = false;
    
    public ClientSettings Settings
    {
        get
        {
            if (!_loaded)
            {
                Load();
                _loaded = true;
            }
            return _settings!;
        }
    }
    
    private SettingsService()
    {
        // Ленивая инициализация - только при первом использовании
    }
    
    private string GetSettingsPath()
    {
        if (_settingsPath == null)
        {
            _settingsPath = Path.Combine(GetAppDataDirectory(), "settings.json");
        }
        return _settingsPath;
    }
    
    private static string GetAppDataDirectory()
    {
        return FileSystem.AppDataDirectory;
    }
    
    public void Load()
    {
        try
        {
            var path = GetSettingsPath();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                _settings = JsonSerializer.Deserialize<ClientSettings>(json) ?? new ClientSettings();
            }
        }
        catch
        {
            _settings = new ClientSettings();
        }
    }
    
    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetSettingsPath(), json);
        }
        catch { }
    }
    
    public List<string> GetAvailableEmotePacks()
    {
        var packs = new List<string> { "(По умолчанию)" };

        try
        {
            // В MAUI файлы из Resources/Raw доступны через AppContext.BaseDirectory
            var emotesPath = Path.Combine(AppContext.BaseDirectory, "emotes");
            DebugLog.Write($"[SettingsService] Looking for emotes at: {emotesPath}");
            DebugLog.Write($"[SettingsService] Directory exists: {Directory.Exists(emotesPath)}");

            if (Directory.Exists(emotesPath))
            {
                var dirs = Directory.GetDirectories(emotesPath);
                DebugLog.Write($"[SettingsService] Found {dirs.Length} directories");

                foreach (var dir in dirs)
                {
                    var packName = Path.GetFileName(dir);
                    DebugLog.Write($"[SettingsService] Found pack: {packName}");
                    packs.Add(packName);
                }
            }
            else
            {
                DebugLog.Write($"[SettingsService] AppContext.BaseDirectory: {AppContext.BaseDirectory}");
                DebugLog.Write($"[SettingsService] Listing BaseDirectory contents:");

                if (Directory.Exists(AppContext.BaseDirectory))
                {
                    foreach (var item in Directory.GetFileSystemEntries(AppContext.BaseDirectory))
                    {
                        DebugLog.Write($"[SettingsService]   - {Path.GetFileName(item)}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[SettingsService] Error in GetAvailableEmotePacks: {ex.Message}");
        }

        return packs;
    }

    public string? GetCurrentEmotePackPath()
    {
        if (string.IsNullOrEmpty(_settings.EmotePack) || _settings.EmotePack == "(По умолчанию)")
            return null;

        var packPath = Path.Combine(AppContext.BaseDirectory, "emotes", _settings.EmotePack);
        return Directory.Exists(packPath) ? packPath : null;
    }
}

