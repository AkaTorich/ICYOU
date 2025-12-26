using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using ICYOU.SDK;

namespace ICYOU.Modules.LinkPreview;

/// <summary>
/// Модуль превью ссылок
/// Автоматически добавляет заголовок и описание страницы к ссылкам
/// </summary>
public class LinkPreviewModule : IModule, IModuleSettings
{
    public string Id => "icyou.linkpreview";
    public string Name => "Превью ссылок";
    public string Version => "1.0.0";
    public string Author => "ICYOU Team";
    public string Description => "Показывает заголовок и описание веб-страниц";
    
    private IModuleContext? _context;
    private bool _enabled = true;
    private bool _showDescription = true;
    private bool _showImage = true;
    private int _maxDescriptionLength = 150;
    
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };
    
    private static readonly Regex UrlRegex = new(
        @"https?://[^\s<>\[\]]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    public void Initialize(IModuleContext context)
    {
        _context = context;
        
        // Загружаем настройки
        _enabled = context.Storage.Get("enabled", true);
        _showDescription = context.Storage.Get("showDescription", true);
        _showImage = context.Storage.Get("showImage", true);
        _maxDescriptionLength = context.Storage.Get("maxDescLength", 150);
        
        // Перехватываем входящие сообщения для добавления превью
        context.MessageService.RegisterIncomingInterceptor(AddLinkPreview);
        
        context.Logger.Info("Модуль превью ссылок инициализирован");
    }
    
    public void Shutdown()
    {
        _context?.Logger.Info("Модуль превью ссылок выгружен");
    }
    
    private Message? AddLinkPreview(Message message)
    {
        if (!_enabled) return message;
        if (message.Type != MessageType.Text) return message;
        
        // Ищем ссылки в сообщении
        var matches = UrlRegex.Matches(message.Content);
        if (matches.Count == 0) return message;
        
        // Берём первую ссылку
        var url = matches[0].Value;
        
        try
        {
            // Вызываем напрямую без Task.Run для совместимости с Android AOT
            var task = GetLinkPreviewAsync(url);
            if (task.Wait(TimeSpan.FromSeconds(3)))
            {
                var preview = task.Result;
                if (preview != null)
                {
                    var previewText = FormatPreview(preview);
                    message.Content = $"{message.Content}\n\n{previewText}";
                }
            }
        }
        catch (Exception ex)
        {
            _context?.Logger.Debug($"Не удалось получить превью для {url}: {ex.Message}");
        }
        
        return message;
    }
    
    private async Task<LinkPreviewData?> GetLinkPreviewAsync(string url)
    {
        try
        {
            // Используем HttpWebRequest для совместимости с Android trimming
            var request = WebRequest.CreateHttp(url);
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ICYOU/1.0";
            request.Timeout = 5000;

            using var response = await request.GetResponseAsync();
            using var stream = response.GetResponseStream();
            using var reader = new StreamReader(stream);
            var html = await reader.ReadToEndAsync();

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            
            var preview = new LinkPreviewData { Url = url };
            
            // Получаем title
            var titleNode = doc.DocumentNode.SelectSingleNode("//title");
            preview.Title = titleNode?.InnerText?.Trim() ?? "";
            
            // Пробуем получить og:title
            var ogTitle = doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']");
            if (ogTitle != null)
            {
                preview.Title = ogTitle.GetAttributeValue("content", preview.Title);
            }
            
            // Получаем description
            var metaDesc = doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
            preview.Description = metaDesc?.GetAttributeValue("content", "") ?? "";
            
            // Пробуем og:description
            var ogDesc = doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']");
            if (ogDesc != null)
            {
                preview.Description = ogDesc.GetAttributeValue("content", preview.Description);
            }
            
            // Получаем изображение
            var ogImage = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
            preview.ImageUrl = ogImage?.GetAttributeValue("content", "") ?? "";
            
            // Получаем имя сайта
            var ogSiteName = doc.DocumentNode.SelectSingleNode("//meta[@property='og:site_name']");
            preview.SiteName = ogSiteName?.GetAttributeValue("content", "") ?? "";
            
            if (string.IsNullOrEmpty(preview.SiteName))
            {
                var uri = new Uri(url);
                preview.SiteName = uri.Host;
            }
            
            return string.IsNullOrEmpty(preview.Title) ? null : preview;
        }
        catch
        {
            return null;
        }
    }
    
    private string FormatPreview(LinkPreviewData preview)
    {
        // Формат: [LINKPREVIEW|url|title|description|imageUrl|siteName]
        // Экранируем | в данных
        var url = preview.Url.Replace("|", "{{PIPE}}");
        var title = preview.Title.Replace("|", "{{PIPE}}").Replace("\n", " ").Replace("\r", "");
        var desc = "";
        
        if (_showDescription && !string.IsNullOrEmpty(preview.Description))
        {
            desc = preview.Description.Replace("|", "{{PIPE}}").Replace("\n", " ").Replace("\r", "");
            if (desc.Length > _maxDescriptionLength)
            {
                desc = desc.Substring(0, _maxDescriptionLength - 3) + "...";
            }
        }
        
        var imageUrl = "";
        if (_showImage && !string.IsNullOrEmpty(preview.ImageUrl))
        {
            imageUrl = preview.ImageUrl.Replace("|", "{{PIPE}}");
        }
        
        var siteName = preview.SiteName.Replace("|", "{{PIPE}}");
        
        return $"[LINKPREVIEW|{url}|{title}|{desc}|{imageUrl}|{siteName}]";
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
                Description = "Включить превью ссылок",
                Type = ModuleSettingType.Boolean,
                CurrentValue = _enabled,
                DefaultValue = true
            },
            new ModuleSetting
            {
                Key = "showDescription",
                DisplayName = "Показывать описание",
                Description = "Показывать описание страницы",
                Type = ModuleSettingType.Boolean,
                CurrentValue = _showDescription,
                DefaultValue = true
            },
            new ModuleSetting
            {
                Key = "showImage",
                DisplayName = "Показывать картинку",
                Description = "Показывать превью-картинку",
                Type = ModuleSettingType.Boolean,
                CurrentValue = _showImage,
                DefaultValue = true
            },
            new ModuleSetting
            {
                Key = "maxDescLength",
                DisplayName = "Макс. длина описания",
                Description = "Максимальная длина описания в символах",
                Type = ModuleSettingType.Integer,
                CurrentValue = _maxDescriptionLength,
                DefaultValue = 150
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
            case "showDescription":
                _showDescription = Convert.ToBoolean(value);
                _context?.Storage.Set("showDescription", _showDescription);
                break;
            case "showImage":
                _showImage = Convert.ToBoolean(value);
                _context?.Storage.Set("showImage", _showImage);
                break;
            case "maxDescLength":
                _maxDescriptionLength = Convert.ToInt32(value);
                _context?.Storage.Set("maxDescLength", _maxDescriptionLength);
                break;
        }
    }
    
    #endregion
}

internal class LinkPreviewData
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string SiteName { get; set; } = "";
}

