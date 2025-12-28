using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICYOU.SDK;

namespace ICYOU.Mobile.Services;

public class EmoteService
{
    private static EmoteService? _instance;
    public static EmoteService Instance => _instance ??= new EmoteService();

    private readonly Dictionary<string, string> _emotes = new(); // code -> file path
    private readonly Dictionary<string, ImageSource> _cachedImages = new();
    private string _emotesPath = "emotes";
    private string? _currentPack;

    public IReadOnlyDictionary<string, string> Emotes => _emotes;
    public string? CurrentPack => _currentPack;

    public void LoadEmotes(string? packName = null)
    {
        _emotes.Clear();
        _cachedImages.Clear();

        // В MAUI файлы из Resources/Raw доступны через AppContext.BaseDirectory
        var baseEmotesPath = Path.Combine(AppContext.BaseDirectory, "emotes");
        DebugLog.Write($"[EmoteService] Base emotes path: {baseEmotesPath}");
        DebugLog.Write($"[EmoteService] Directory exists: {Directory.Exists(baseEmotesPath)}");

        // Если указан конкретный пак - загружаем только его
        if (!string.IsNullOrEmpty(packName) && packName != "(По умолчанию)")
        {
            var packPath = Path.Combine(baseEmotesPath, packName);
            DebugLog.Write($"[EmoteService] Loading pack '{packName}' from: {packPath}");

            if (Directory.Exists(packPath))
            {
                _emotesPath = packPath;
                _currentPack = packName;
                LoadFromDirectory(packPath);
                DebugLog.Write($"[EmoteService] Loaded {_emotes.Count} emotes from pack '{packName}'");
                return;
            }
            else
            {
                DebugLog.Write($"[EmoteService] Pack directory '{packName}' not found");
            }
        }

        // Иначе загружаем смайлы из пака "default"
        var defaultPackPath = Path.Combine(baseEmotesPath, "default");
        DebugLog.Write($"[EmoteService] Trying default pack at: {defaultPackPath}");

        if (Directory.Exists(defaultPackPath))
        {
            _emotesPath = defaultPackPath;
            _currentPack = "default";
            LoadFromDirectory(defaultPackPath);
            DebugLog.Write($"[EmoteService] Loaded {_emotes.Count} emotes from default pack");
        }
        else
        {
            DebugLog.Write($"[EmoteService] Default pack not found, trying base directory");
            // Если нет default, загружаем из корневой папки emotes
            _emotesPath = baseEmotesPath;
            _currentPack = null;
            if (Directory.Exists(baseEmotesPath))
            {
                LoadFromDirectory(baseEmotesPath);
                DebugLog.Write($"[EmoteService] Loaded {_emotes.Count} emotes from base directory");
            }
            else
            {
                DebugLog.Write($"[EmoteService] No emotes found - base directory doesn't exist");
            }
        }
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

    public ImageSource? GetEmoteImage(string code)
    {
        if (!_emotes.TryGetValue(code, out var path))
            return null;

        if (_cachedImages.TryGetValue(code, out var cached))
            return cached;

        try
        {
            // MAUI автоматически анимирует GIF в Image контроле
            var imageSource = ImageSource.FromFile(path);
            _cachedImages[code] = imageSource;
            return imageSource;
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
