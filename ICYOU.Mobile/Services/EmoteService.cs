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

    public async void LoadEmotes(string? packName = null)
    {
        _emotes.Clear();
        _cachedImages.Clear();

        // В MAUI смайлы копируются из Resources/Raw в AppDataDirectory
        var targetEmotesPath = Path.Combine(FileSystem.AppDataDirectory, "emotes");
        DebugLog.Write($"[EmoteService] Target emotes path: {targetEmotesPath}");

        // Определяем какой пак загружать
        var selectedPack = string.IsNullOrEmpty(packName) || packName == "(По умолчанию)" ? "default" : packName;
        DebugLog.Write($"[EmoteService] Selected pack: {selectedPack}");

        // Копируем смайлы из Resources если их еще нет
        await EnsureEmotesCopiedAsync(selectedPack);

        // Загружаем смайлы из AppDataDirectory
        var packPath = Path.Combine(targetEmotesPath, selectedPack);
        if (Directory.Exists(packPath))
        {
            _emotesPath = packPath;
            _currentPack = selectedPack;
            LoadFromDirectory(packPath);
            DebugLog.Write($"[EmoteService] Loaded {_emotes.Count} emotes from pack '{selectedPack}'");
        }
        else
        {
            DebugLog.Write($"[EmoteService] Pack directory not found: {packPath}");
        }
    }

    private async Task EnsureEmotesCopiedAsync(string packName)
    {
        try
        {
            var targetPackPath = Path.Combine(FileSystem.AppDataDirectory, "emotes", packName);

            // Если уже скопированы - пропускаем
            if (Directory.Exists(targetPackPath) && Directory.GetFiles(targetPackPath, "*.gif").Length > 0)
            {
                DebugLog.Write($"[EmoteService] Pack '{packName}' already copied");
                return;
            }

            DebugLog.Write($"[EmoteService] Copying pack '{packName}' from Resources...");
            Directory.CreateDirectory(targetPackPath);

            // Читаем список файлов из emotes.txt
            var manifestPath = $"emotes/{packName}/emotes.txt";
            var emoteNames = new List<string>();

            try
            {
                using var stream = await FileSystem.OpenAppPackageFileAsync(manifestPath);
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        emoteNames.Add(line.Trim());
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog.Write($"[EmoteService] Error reading manifest for pack '{packName}': {ex.Message}");
                return;
            }

            DebugLog.Write($"[EmoteService] Found {emoteNames.Count} emotes in manifest");

            // Копируем каждый файл
            int copied = 0;
            foreach (var emoteName in emoteNames)
            {
                try
                {
                    var sourcePath = $"emotes/{packName}/{emoteName}.gif";
                    var targetPath = Path.Combine(targetPackPath, $"{emoteName}.gif");

                    using var sourceStream = await FileSystem.OpenAppPackageFileAsync(sourcePath);
                    using var fileStream = File.Create(targetPath);
                    await sourceStream.CopyToAsync(fileStream);
                    copied++;
                }
                catch (Exception ex)
                {
                    DebugLog.Write($"[EmoteService] Error copying {emoteName}: {ex.Message}");
                }
            }

            DebugLog.Write($"[EmoteService] Copied {copied}/{emoteNames.Count} emotes for pack '{packName}'");
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[EmoteService] Error in EnsureEmotesCopiedAsync: {ex.Message}");
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
