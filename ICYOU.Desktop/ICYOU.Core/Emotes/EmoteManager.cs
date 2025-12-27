using ICYOU.Core.Protocol;
using System.Text.RegularExpressions;

namespace ICYOU.Core.Emotes;

public class EmoteManager
{
    private readonly Dictionary<string, EmotePack> _packs = new();
    private readonly Dictionary<string, (EmotePack Pack, Emote Emote)> _emotesByCode = new();
    private readonly string _emotesPath;
    
    public IReadOnlyDictionary<string, EmotePack> Packs => _packs;
    
    public EmoteManager(string emotesPath)
    {
        _emotesPath = emotesPath;
        if (!Directory.Exists(_emotesPath))
            Directory.CreateDirectory(_emotesPath);
    }
    
    public void LoadEmotePacks()
    {
        _packs.Clear();
        _emotesByCode.Clear();
        
        var packDirs = Directory.GetDirectories(_emotesPath);
        foreach (var packDir in packDirs)
        {
            LoadPack(packDir);
        }
        
        // Загружаем также смайлы из корня (default pack)
        LoadPack(_emotesPath, "default");
    }
    
    private void LoadPack(string path, string? name = null)
    {
        var packName = name ?? Path.GetFileName(path);
        var pack = new EmotePack
        {
            Name = packName,
            Path = path
        };
        
        var imageFiles = Directory.GetFiles(path)
            .Where(f => IsImageFile(f))
            .ToList();
            
        foreach (var file in imageFiles)
        {
            var fileName = Path.GetFileName(file);
            var code = GetEmoteCode(fileName);
            
            var emote = new Emote
            {
                Code = code,
                FileName = fileName
            };
            
            pack.Emotes.Add(emote);
            
            // Регистрируем код смайла
            if (!_emotesByCode.ContainsKey(code))
            {
                _emotesByCode[code] = (pack, emote);
            }
        }
        
        if (pack.Emotes.Count > 0)
        {
            _packs[packName] = pack;
            Console.WriteLine($"Loaded emote pack '{packName}' with {pack.Emotes.Count} emotes");
        }
    }
    
    private string GetEmoteCode(string fileName)
    {
        // Имя файла без расширения становится кодом
        // Например: ":).gif" -> ":)"
        return Path.GetFileNameWithoutExtension(fileName);
    }
    
    private bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLower();
        return ext is ".gif" or ".png" or ".jpg" or ".jpeg" or ".webp";
    }
    
    public string? GetEmotePath(string code)
    {
        if (_emotesByCode.TryGetValue(code, out var emote))
        {
            return Path.Combine(emote.Pack.Path, emote.Emote.FileName);
        }
        return null;
    }
    
    public List<string> FindEmotesInText(string text)
    {
        var found = new List<string>();
        foreach (var code in _emotesByCode.Keys)
        {
            if (text.Contains(code))
            {
                found.Add(code);
            }
        }
        return found;
    }
    
    public List<EmotePack> GetAllPacks()
    {
        return _packs.Values.ToList();
    }
    
    public byte[]? GetEmoteData(string code)
    {
        var path = GetEmotePath(code);
        if (path != null && File.Exists(path))
        {
            return File.ReadAllBytes(path);
        }
        return null;
    }
}

