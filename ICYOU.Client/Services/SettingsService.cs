using System.IO;
using System.Text.Json;

namespace ICYOU.Client.Services;

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
    
    private readonly string _settingsPath;
    private ClientSettings _settings;
    
    public ClientSettings Settings => _settings;
    
    private SettingsService()
    {
        _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        _settings = new ClientSettings();
        Load();
    }
    
    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
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
            File.WriteAllText(_settingsPath, json);
        }
        catch { }
    }
    
    public List<string> GetAvailableEmotePacks()
    {
        var packs = new List<string> { "(По умолчанию)" };
        var emotesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "emotes");
        
        if (Directory.Exists(emotesPath))
        {
            foreach (var dir in Directory.GetDirectories(emotesPath))
            {
                packs.Add(Path.GetFileName(dir));
            }
        }
        
        return packs;
    }
    
    public string? GetCurrentEmotePackPath()
    {
        if (string.IsNullOrEmpty(_settings.EmotePack) || _settings.EmotePack == "(По умолчанию)")
            return null;
            
        var packPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "emotes", _settings.EmotePack);
        return Directory.Exists(packPath) ? packPath : null;
    }
}

