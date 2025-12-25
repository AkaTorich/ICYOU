using ICYOU.SDK;

namespace ICYOU.Modules.Quote;

/// <summary>
/// Модуль цитирования сообщений
/// Формат: [QUOTE|MessageId|SenderName|Content]ваш ответ
/// </summary>
public class QuoteModule : IModule, IModuleSettings
{
    public string Id => "icyou.quote";
    public string Name => "Цитирование";
    public string Version => "1.0.0";
    public string Author => "ICYOU Team";
    public string Description => "Позволяет цитировать сообщения при ответе";
    
    private IModuleContext? _context;
    private bool _enabled = true;
    private string _quoteStyle = "line"; // line, box, minimal
    
    public void Initialize(IModuleContext context)
    {
        _context = context;
        
        // Загружаем настройки
        _enabled = context.Storage.Get("enabled", true);
        _quoteStyle = context.Storage.Get("style", "line");
        
        // Регистрируем перехватчик для форматирования цитат
        context.MessageService.RegisterIncomingInterceptor(FormatQuote);
        
        context.Logger.Info("Модуль цитирования инициализирован");
    }
    
    public void Shutdown()
    {
        _context?.Logger.Info("Модуль цитирования выгружен");
    }
    
    /// <summary>
    /// Форматирует цитату в сообщении для отображения
    /// </summary>
    private Message? FormatQuote(Message message)
    {
        _context?.Logger.Debug($"FormatQuote вызван для: {message.Content.Substring(0, Math.Min(50, message.Content.Length))}");

        if (!_enabled)
        {
            _context?.Logger.Debug("Quote модуль отключен");
            return message;
        }

        // Поддерживаем оба формата: [QUOTE|...] и [QUOTES|...]
        bool isQuote = message.Content.StartsWith("[QUOTE|");
        bool isQuotes = message.Content.StartsWith("[QUOTES|");

        if (!isQuote && !isQuotes)
        {
            _context?.Logger.Debug("Сообщение не является цитатой");
            return message;
        }

        _context?.Logger.Debug($"Обрабатываем цитату (формат: {(isQuote ? "QUOTE" : "QUOTES")})...");

        try
        {
            var endQuote = message.Content.IndexOf(']');
            if (endQuote < 0)
            {
                _context?.Logger.Debug("Не найдена закрывающая скобка");
                return message;
            }

            var replyContent = message.Content.Substring(endQuote + 1).TrimStart();
            string quotedSender;
            string quotedContent;

            if (isQuote)
            {
                // Формат: [QUOTE|MessageId|SenderName|Content]reply
                var quoteData = message.Content.Substring(7, endQuote - 7);
                var parts = quoteData.Split('|', 3);

                if (parts.Length < 3)
                {
                    _context?.Logger.Debug($"QUOTE: Недостаточно частей: {parts.Length}");
                    return message;
                }

                quotedSender = parts[1];
                quotedContent = parts[2];
            }
            else
            {
                // Формат: [QUOTES|MessageId~SenderName]reply
                var quoteData = message.Content.Substring(8, endQuote - 8);
                var parts = quoteData.Split('~', 2);

                if (parts.Length < 2)
                {
                    _context?.Logger.Debug($"QUOTES: Недостаточно частей: {parts.Length}");
                    return message;
                }

                quotedSender = parts[1];
                quotedContent = "цитата"; // В формате QUOTES нет контента
            }

            _context?.Logger.Debug($"Parsed: sender={quotedSender}, reply={replyContent}");

            // Форматируем в зависимости от стиля
            message.Content = _quoteStyle switch
            {
                "box" => $"┌─ {quotedSender}:\n│ {quotedContent}\n└─────────\n{replyContent}",
                "minimal" => $"↩ {quotedSender}: {TruncateText(quotedContent, 50)}\n{replyContent}",
                _ => $"▎ {quotedSender}: {quotedContent}\n\n{replyContent}" // line style
            };

            _context?.Logger.Debug($"Отформатировано: {message.Content.Substring(0, Math.Min(50, message.Content.Length))}");
        }
        catch (Exception ex)
        {
            _context?.Logger.Error($"Ошибка парсинга цитаты: {ex.Message}");
        }

        return message;
    }
    
    private string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength - 3) + "...";
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
                Description = "Включить форматирование цитат",
                Type = ModuleSettingType.Boolean,
                CurrentValue = _enabled,
                DefaultValue = true
            },
            new ModuleSetting
            {
                Key = "style",
                DisplayName = "Стиль цитаты",
                Description = "Выберите стиль отображения цитат",
                Type = ModuleSettingType.Choice,
                CurrentValue = _quoteStyle,
                DefaultValue = "line",
                Options = new object[] { "line", "box", "minimal" }
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
            case "style":
                _quoteStyle = value.ToString() ?? "line";
                _context?.Storage.Set("style", _quoteStyle);
                break;
        }
    }
    
    #endregion
}

/// <summary>
/// Хелпер для создания цитат
/// </summary>
public static class QuoteHelper
{
    /// <summary>
    /// Создаёт строку цитаты для отправки
    /// </summary>
    public static string CreateQuote(long messageId, string senderName, string content, string reply)
    {
        // Убираем переносы строк из цитируемого контента
        var sanitizedContent = content.Replace("\n", " ").Replace("\r", "");
        return $"[QUOTE|{messageId}|{senderName}|{sanitizedContent}]{reply}";
    }
}

