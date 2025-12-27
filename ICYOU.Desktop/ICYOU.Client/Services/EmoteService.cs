using System.IO;
using System.Windows.Media.Imaging;

namespace ICYOU.Client.Services;

public class EmoteService
{
    private static EmoteService? _instance;
    public static EmoteService Instance => _instance ??= new EmoteService();
    
    private readonly Dictionary<string, string> _emotes = new(); // code -> file path
    private readonly Dictionary<string, BitmapImage> _cachedImages = new();
    private string _emotesPath = "emotes";
    private string? _currentPack;
    
    public IReadOnlyDictionary<string, string> Emotes => _emotes;
    public string? CurrentPack => _currentPack;
    
    public void LoadEmotes(string? packName = null)
    {
        _emotes.Clear();
        _cachedImages.Clear();
        
        var baseEmotesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "emotes");
        
        // Если указан конкретный пак - загружаем только его
        if (!string.IsNullOrEmpty(packName) && packName != "(По умолчанию)")
        {
            var packPath = Path.Combine(baseEmotesPath, packName);
            if (Directory.Exists(packPath))
            {
                _emotesPath = packPath;
                _currentPack = packName;
                LoadFromDirectory(packPath);
                return;
            }
        }
        
        // Иначе загружаем все смайлы из корневой папки
        _emotesPath = baseEmotesPath;
        _currentPack = null;
        
        if (!Directory.Exists(_emotesPath))
        {
            Directory.CreateDirectory(_emotesPath);
            return;
        }
        
        LoadFromDirectory(_emotesPath);
    }
    
    private void LoadFromDirectory(string path)
    {
        var extensions = new[] { "*.gif", "*.png", "*.jpg", "*.jpeg", "*.webp" };
        
        foreach (var ext in extensions)
        {
            foreach (var file in Directory.GetFiles(path, ext))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var code = $":{fileName}:";
                
                if (!_emotes.ContainsKey(code))
                {
                    _emotes[code] = file;
                }
            }
        }
    }
    
    public BitmapImage? GetEmoteImage(string code)
    {
        if (!_emotes.TryGetValue(code, out var path))
            return null;
            
        if (_cachedImages.TryGetValue(code, out var cached))
            return cached;
            
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            
            _cachedImages[code] = bitmap;
            return bitmap;
        }
        catch
        {
            return null;
        }
    }
    
    public string? GetEmotePath(string code)
    {
        return _emotes.TryGetValue(code, out var path) ? path : null;
    }
    
    public List<string> GetAllCodes()
    {
        return _emotes.Keys.ToList();
    }
    
    // Находит все смайлы в тексте
    public List<(int Start, int Length, string Code)> FindEmotesInText(string text)
    {
        var results = new List<(int Start, int Length, string Code)>();
        
        foreach (var code in _emotes.Keys)
        {
            var index = 0;
            while ((index = text.IndexOf(code, index, StringComparison.Ordinal)) != -1)
            {
                results.Add((index, code.Length, code));
                index += code.Length;
            }
        }
        
        // Сортируем по позиции
        results.Sort((a, b) => a.Start.CompareTo(b.Start));
        return results;
    }
}

