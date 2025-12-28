using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        // В MAUI используем FileSystem.AppDataDirectory для хранения смайлов
        var baseEmotesPath = Path.Combine(FileSystem.AppDataDirectory, "emotes");

        // Проверяем, скопированы ли смайлы из bundled resources
        EnsureEmotesCopied(baseEmotesPath);

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

        // Иначе загружаем смайлы из пака "default"
        var defaultPackPath = Path.Combine(baseEmotesPath, "default");
        if (Directory.Exists(defaultPackPath))
        {
            _emotesPath = defaultPackPath;
            _currentPack = "default";
            LoadFromDirectory(defaultPackPath);
        }
        else
        {
            _emotesPath = baseEmotesPath;
            _currentPack = null;
        }
    }

    private void EnsureEmotesCopied(string targetPath)
    {
        // Если смайлы уже скопированы, пропускаем
        if (Directory.Exists(targetPath) && Directory.GetFiles(targetPath, "*.*", SearchOption.AllDirectories).Length > 0)
            return;

        try
        {
            // Копируем смайлы из bundled resources
            var bundledEmotesPath = Path.Combine(FileSystem.Current.AppPackageDirectory, "emotes");

            if (!Directory.Exists(bundledEmotesPath))
            {
                // Пробуем альтернативный путь
                bundledEmotesPath = Path.Combine(AppContext.BaseDirectory, "emotes");
            }

            if (Directory.Exists(bundledEmotesPath))
            {
                CopyDirectory(bundledEmotesPath, targetPath);
            }
            else
            {
                // Создаём пустую директорию если смайлы не найдены
                Directory.CreateDirectory(targetPath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EmoteService] Error copying emotes: {ex.Message}");
            // Создаём пустую директорию в случае ошибки
            if (!Directory.Exists(targetPath))
                Directory.CreateDirectory(targetPath);
        }
    }

    private void CopyDirectory(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);

        // Копируем все файлы
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(targetDir, fileName);
            File.Copy(file, destFile, overwrite: true);
        }

        // Рекурсивно копируем подпапки
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);
            var destDir = Path.Combine(targetDir, dirName);
            CopyDirectory(subDir, destDir);
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
