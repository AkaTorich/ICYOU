using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ICYOU.Client.Services;

public class LinkPreviewService
{
    private static LinkPreviewService? _instance;
    public static LinkPreviewService Instance => _instance ??= new LinkPreviewService();
    
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, LinkPreviewData?> _cache = new();
    private static readonly Regex UrlRegex = new(
        @"(https?://[^\s<>\[\]""']+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private LinkPreviewService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ICYOU/1.0");
    }
    
    public List<(int Start, int Length, string Url)> FindUrlsInText(string text)
    {
        var result = new List<(int, int, string)>();
        var matches = UrlRegex.Matches(text);
        
        foreach (Match match in matches)
        {
            result.Add((match.Index, match.Length, match.Value));
        }
        
        return result;
    }
    
    public bool HasUrls(string text) => UrlRegex.IsMatch(text);
    
    public async Task<LinkPreviewData?> GetPreviewAsync(string url)
    {
        if (_cache.TryGetValue(url, out var cached))
            return cached;
        
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;
            
            var html = await response.Content.ReadAsStringAsync();
            var preview = ParseHtml(html, url);
            
            _cache[url] = preview;
            return preview;
        }
        catch
        {
            _cache[url] = null;
            return null;
        }
    }
    
    private LinkPreviewData? ParseHtml(string html, string url)
    {
        var preview = new LinkPreviewData { Url = url };
        
        // Получаем title
        var titleMatch = Regex.Match(html, @"<title[^>]*>([^<]+)</title>", RegexOptions.IgnoreCase);
        if (titleMatch.Success)
            preview.Title = System.Net.WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim());
        
        // og:title
        var ogTitleMatch = Regex.Match(html, @"<meta[^>]+property=[""']og:title[""'][^>]+content=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        if (!ogTitleMatch.Success)
            ogTitleMatch = Regex.Match(html, @"<meta[^>]+content=[""']([^""']+)[""'][^>]+property=[""']og:title[""']", RegexOptions.IgnoreCase);
        if (ogTitleMatch.Success)
            preview.Title = System.Net.WebUtility.HtmlDecode(ogTitleMatch.Groups[1].Value.Trim());
        
        // description
        var descMatch = Regex.Match(html, @"<meta[^>]+name=[""']description[""'][^>]+content=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        if (!descMatch.Success)
            descMatch = Regex.Match(html, @"<meta[^>]+content=[""']([^""']+)[""'][^>]+name=[""']description[""']", RegexOptions.IgnoreCase);
        if (descMatch.Success)
            preview.Description = System.Net.WebUtility.HtmlDecode(descMatch.Groups[1].Value.Trim());
        
        // og:description
        var ogDescMatch = Regex.Match(html, @"<meta[^>]+property=[""']og:description[""'][^>]+content=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        if (!ogDescMatch.Success)
            ogDescMatch = Regex.Match(html, @"<meta[^>]+content=[""']([^""']+)[""'][^>]+property=[""']og:description[""']", RegexOptions.IgnoreCase);
        if (ogDescMatch.Success)
            preview.Description = System.Net.WebUtility.HtmlDecode(ogDescMatch.Groups[1].Value.Trim());
        
        // og:site_name
        var siteMatch = Regex.Match(html, @"<meta[^>]+property=[""']og:site_name[""'][^>]+content=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
        if (!siteMatch.Success)
            siteMatch = Regex.Match(html, @"<meta[^>]+content=[""']([^""']+)[""'][^>]+property=[""']og:site_name[""']", RegexOptions.IgnoreCase);
        if (siteMatch.Success)
            preview.SiteName = System.Net.WebUtility.HtmlDecode(siteMatch.Groups[1].Value.Trim());
        
        if (string.IsNullOrEmpty(preview.SiteName))
        {
            try
            {
                preview.SiteName = new Uri(url).Host;
            }
            catch { }
        }
        
        return string.IsNullOrEmpty(preview.Title) ? null : preview;
    }
    
    public FrameworkElement CreatePreviewElement(LinkPreviewData preview, Brush textBrush)
    {
        var accentBrush = Application.Current.Resources["AccentBrush"] as Brush ?? Brushes.Green;
        var cardBrush = Application.Current.Resources["CardBrush"] as Brush ?? Brushes.DarkGray;
        var secondaryBrush = Application.Current.Resources["TextSecondaryBrush"] as Brush ?? Brushes.Gray;
        
        var border = new Border
        {
            Background = cardBrush,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Margin = new Thickness(0, 8, 0, 0),
            MaxWidth = 350,
            Cursor = System.Windows.Input.Cursors.Hand
        };
        
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        
        // Вертикальная линия
        var line = new Border
        {
            Background = accentBrush,
            CornerRadius = new CornerRadius(2)
        };
        Grid.SetColumn(line, 0);
        grid.Children.Add(line);
        
        // Контент
        var content = new StackPanel();
        Grid.SetColumn(content, 2);
        
        content.Children.Add(new TextBlock
        {
            Text = $"🔗 {preview.SiteName}",
            FontSize = 10,
            Foreground = secondaryBrush
        });
        
        content.Children.Add(new TextBlock
        {
            Text = preview.Title,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = textBrush,
            TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxHeight = 36
        });
        
        if (!string.IsNullOrEmpty(preview.Description))
        {
            var desc = preview.Description;
            if (desc.Length > 120)
                desc = desc.Substring(0, 117) + "...";
            
            content.Children.Add(new TextBlock
            {
                Text = desc,
                FontSize = 11,
                Foreground = secondaryBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }
        
        grid.Children.Add(content);
        border.Child = grid;
        
        // Клик открывает ссылку
        border.MouseLeftButtonUp += (s, e) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = preview.Url,
                    UseShellExecute = true
                });
            }
            catch { }
        };
        
        return border;
    }
}

public class LinkPreviewData
{
    public string Url { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string SiteName { get; set; } = "";
}

