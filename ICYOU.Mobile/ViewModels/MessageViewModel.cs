using System.ComponentModel;
using System.Runtime.CompilerServices;
using ICYOU.SDK;
using ICYOU.Mobile.Services;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ICYOU.Mobile.ViewModels;

// Класс для элемента контента (текст или смайл)
public class ContentPart
{
    public bool IsEmote { get; set; }
    public string Text { get; set; } = "";
    public string EmoteCode { get; set; } = "";
    public ImageSource? EmoteImage { get; set; }
}

public class MessageViewModel : INotifyPropertyChanged
{
    public Message Message { get; }

    public bool IsOwnMessage => Message.SenderId == AppState.CurrentUser?.Id;

    public string SenderName => Message.SenderName;

    public string Content => Message.Content;

    // Свойства для смайлов
    public bool HasEmotes { get; }
    public List<ContentPart> ContentParts { get; } = new();
    public List<ContentPart> ReplyTextParts { get; } = new();
    public List<ContentPart> QuoteContentParts { get; } = new();

    // Свойства для цитат
    public bool HasQuote { get; }
    public string QuoteSender { get; } = "";
    public string QuoteContent { get; } = "";
    public string ReplyText { get; } = "";
    public bool HasQuoteLinkPreview { get; } = false;
    public string QuoteLinkPreviewTitle { get; } = "";
    public string QuoteLinkPreviewDescription { get; } = "";

    // Свойства для превью ссылок
    public bool HasLinkPreview { get; }
    public string LinkPreviewUrl { get; } = "";
    public string LinkPreviewTitle { get; } = "";
    public string LinkPreviewDescription { get; } = "";
    public string LinkPreviewImageUrl { get; } = "";
    public string LinkPreviewSiteName { get; } = "";
    public string TextBeforePreview { get; } = "";

    // Свойства для файлов
    public bool HasFile { get; }
    public string FileName { get; } = "";
    public string FileType { get; } = "";
    public byte[]? FileData { get; }
    public string FilePath { get; } = "";
    public bool IsImage => FileType == "image";
    public bool IsVideo => FileType == "video";
    public bool IsAudio => FileType == "audio";
    public bool IsOtherFile => !IsImage && !IsVideo && !IsAudio && HasFile;
    public ImageSource? FileImageSource { get; }

    // Вспомогательные свойства
    public bool ShowFullContent => !HasQuote && !HasLinkPreview && !HasFile;
    public bool HasLinkPreviewImage => !string.IsNullOrEmpty(LinkPreviewImageUrl);
    public bool HasLinkPreviewDescription => !string.IsNullOrEmpty(LinkPreviewDescription);
    public bool HasTextBeforePreview => !string.IsNullOrEmpty(TextBeforePreview);

    public string TimeText => Message.Timestamp.ToString("HH:mm");

    public LayoutOptions Alignment => IsOwnMessage ? LayoutOptions.End : LayoutOptions.Start;

    public Color BackgroundColor => IsOwnMessage ?
        Color.FromRgb(220, 248, 198) :
        Color.FromRgb(255, 255, 255);

    public string StatusIcon
    {
        get
        {
            if (!IsOwnMessage)
                return "";

            return Message.Status switch
            {
                MessageStatus.Sending => "⏳",
                MessageStatus.Sent => "✓",
                MessageStatus.Delivered => "✓✓",
                MessageStatus.Read => "✓✓",
                _ => ""
            };
        }
    }

    public Color StatusIconColor => Message.Status == MessageStatus.Read ?
        Color.FromRgb(66, 133, 244) :
        Colors.Gray;

    public MessageViewModel(Message message)
    {
        Message = message;

        // Парсим контент
        var content = message.Content;

        // Логируем входящее сообщение
        DebugLog.Write($"[MessageViewModel] Original content: {content}");

        // Сначала проверяем формат [QUOTE|...] или [QUOTES|...]
        if (content.StartsWith("[QUOTE|") || content.StartsWith("[QUOTES|"))
        {
            try
            {
                int endQuote;

                if (content.StartsWith("[QUOTE|"))
                {
                    // Для формата [QUOTE|...] нужно учитывать вложенные скобки
                    // Подсчитываем открывающие [ и закрывающие ]
                    int bracketCount = 1; // [QUOTE уже открыта
                    endQuote = 7; // начинаем после [QUOTE|
                    while (endQuote < content.Length && bracketCount > 0)
                    {
                        if (content[endQuote] == '[') bracketCount++;
                        else if (content[endQuote] == ']') bracketCount--;
                        if (bracketCount > 0) endQuote++;
                    }
                }
                else
                {
                    // Для формата [QUOTES|...] просто ищем первый ]
                    endQuote = content.IndexOf(']');
                }

                if (endQuote > 0 && endQuote < content.Length)
                {
                    HasQuote = true;
                    ReplyText = content.Substring(endQuote + 1).TrimStart();

                    if (content.StartsWith("[QUOTE|"))
                    {
                        // Формат: [QUOTE|MessageId|SenderName|Content]reply
                        var quoteData = content.Substring(7, endQuote - 7);
                        var parts = quoteData.Split('|', 3);
                        if (parts.Length >= 3)
                        {
                            QuoteSender = parts[1];
                            QuoteContent = parts[2];
                        }
                    }
                    else
                    {
                        // Формат: [QUOTES|sender~content|sender~content|...]reply
                        var quoteData = content.Substring(8, endQuote - 8);
                        // Разбиваем по | на отдельные цитаты
                        var quoteParts = quoteData.Split('|');
                        if (quoteParts.Length >= 1)
                        {
                            // Берём первую цитату
                            var firstQuote = quoteParts[0].Split('~', 2);
                            if (firstQuote.Length >= 2)
                            {
                                QuoteSender = firstQuote[0];
                                QuoteContent = firstQuote[1];
                            }

                            // Если цитат больше одной - показываем счётчик
                            if (quoteParts.Length > 1)
                            {
                                QuoteSender = $"Цитаты ({quoteParts.Length})";
                                // Формируем список всех цитат для превью
                                var lines = new List<string>();
                                foreach (var quotePart in quoteParts)
                                {
                                    var parts = quotePart.Split('~', 2);
                                    if (parts.Length >= 2)
                                    {
                                        var preview = $"{parts[0]}: {parts[1]}";
                                        if (preview.Length > 40)
                                            preview = preview.Substring(0, 37) + "...";
                                        lines.Add(preview);
                                    }
                                }
                                QuoteContent = string.Join("\n", lines);
                            }
                        }
                    }

                    // Проверяем, есть ли превью в ReplyText
                    if (ReplyText.Contains("[LINKPREVIEW|"))
                    {
                        var linkStart = ReplyText.IndexOf("[LINKPREVIEW|");
                        var linkEnd = ReplyText.IndexOf("]", linkStart);
                        if (linkEnd > linkStart)
                        {
                            var before = linkStart > 0 ? ReplyText.Substring(0, linkStart).Trim() : "";
                            var after = linkEnd + 1 < ReplyText.Length ? ReplyText.Substring(linkEnd + 1).TrimStart() : "";

                            // Парсим данные превью из цитаты
                            var previewData = ReplyText.Substring(linkStart + 13, linkEnd - linkStart - 13);
                            var parts = previewData.Split('|');

                            if (parts.Length >= 3)
                            {
                                HasQuoteLinkPreview = true;
                                QuoteLinkPreviewTitle = parts[1].Replace("{{PIPE}}", "|");
                                QuoteLinkPreviewDescription = parts[2].Replace("{{PIPE}}", "|");
                            }

                            // Убираем тег из ReplyText
                            ReplyText = string.IsNullOrEmpty(before) ? after : (string.IsNullOrEmpty(after) ? before : $"{before}\n{after}");
                        }
                    }

                    DebugLog.Write($"[MessageViewModel] After quote parse: HasQuote={HasQuote}, QuoteSender='{QuoteSender}', QuoteContent='{QuoteContent}', ReplyText='{ReplyText}'");
                }
            }
            catch
            {
                HasQuote = false;
            }
        }

        // Проверяем формат [FILE|...] (только если нет цитаты)
        if (!HasQuote && (content.StartsWith("[FILE|") || content.StartsWith("[FILE:")))
        {
            try
            {
                var fileService = FileTransferService.Instance;
                var (fileType, fileName, fileData, savedPath) = fileService.ParseFileMessage(content);

                if (!string.IsNullOrEmpty(fileType) && !string.IsNullOrEmpty(fileName))
                {
                    HasFile = true;
                    FileName = fileName;
                    FileType = fileType;
                    FileData = fileData;
                    FilePath = savedPath ?? "";

                    // Если это изображение, создаем ImageSource
                    if (IsImage && fileData != null)
                    {
                        FileImageSource = ImageSource.FromStream(() => new MemoryStream(fileData));
                    }

                    DebugLog.Write($"[MessageViewModel] File parsed: {FileName}, Type: {FileType}, Size: {fileData?.Length ?? 0} bytes");
                }
            }
            catch (Exception ex)
            {
                DebugLog.Write($"[MessageViewModel] Error parsing file: {ex.Message}");
                HasFile = false;
            }
        }

        // Проверяем формат [LINKPREVIEW|...] (только если нет цитаты и файла, т.к. превью в цитате уже обработано выше)
        if (!HasQuote && !HasFile && content.Contains("[LINKPREVIEW|"))
        {
            try
            {
                var previewStart = content.IndexOf("[LINKPREVIEW|");
                var previewEnd = content.IndexOf("]", previewStart);

                if (previewEnd > previewStart)
                {
                    HasLinkPreview = true;
                    TextBeforePreview = previewStart > 0 ? content.Substring(0, previewStart).Trim() : "";

                    var previewData = content.Substring(previewStart + 13, previewEnd - previewStart - 13);
                    var parts = previewData.Split('|');

                    if (parts.Length >= 5)
                    {
                        LinkPreviewUrl = parts[0].Replace("{{PIPE}}", "|");
                        LinkPreviewTitle = parts[1].Replace("{{PIPE}}", "|");
                        LinkPreviewDescription = parts[2].Replace("{{PIPE}}", "|");
                        LinkPreviewImageUrl = parts[3].Replace("{{PIPE}}", "|");
                        LinkPreviewSiteName = parts[4].Replace("{{PIPE}}", "|");

                        DebugLog.Write($"[MessageViewModel] After preview parse: HasLinkPreview={HasLinkPreview}, URL='{LinkPreviewUrl}', Title='{LinkPreviewTitle}', TextBefore='{TextBeforePreview}'");
                    }
                }
            }
            catch
            {
                HasLinkPreview = false;
            }
        }

        DebugLog.Write($"[MessageViewModel] Final result: HasQuote={HasQuote}, HasLinkPreview={HasLinkPreview}, ReplyText='{ReplyText}'");

        // Парсим смайлы в основном контенте
        if (ShowFullContent)
        {
            ContentParts.AddRange(ParseContentWithEmotes(Content));
            HasEmotes = ContentParts.Any(p => p.IsEmote);
        }

        // Парсим смайлы в ReplyText (если есть цитата)
        if (HasQuote && !string.IsNullOrEmpty(ReplyText))
        {
            ReplyTextParts.AddRange(ParseContentWithEmotes(ReplyText));
        }

        // Парсим смайлы в QuoteContent
        if (HasQuote && !string.IsNullOrEmpty(QuoteContent))
        {
            QuoteContentParts.AddRange(ParseContentWithEmotes(QuoteContent));
        }
    }

    private List<ContentPart> ParseContentWithEmotes(string text)
    {
        var parts = new List<ContentPart>();

        try
        {
            var emoteService = EmoteService.Instance;
            var emotes = emoteService.FindEmotesInText(text);

            if (emotes.Count == 0)
            {
                // Нет смайлов - возвращаем просто текст
                parts.Add(new ContentPart { IsEmote = false, Text = text });
                return parts;
            }

            int lastIndex = 0;

            foreach (var (start, length, code) in emotes)
            {
                // Добавляем текст до смайла
                if (start > lastIndex)
                {
                    var textBefore = text.Substring(lastIndex, start - lastIndex);
                    parts.Add(new ContentPart { IsEmote = false, Text = textBefore });
                }

                // Добавляем смайл
                var emoteImage = emoteService.GetEmoteImage(code);
                if (emoteImage != null)
                {
                    parts.Add(new ContentPart
                    {
                        IsEmote = true,
                        EmoteCode = code,
                        EmoteImage = emoteImage
                    });
                }
                else
                {
                    // Если изображение не загрузилось - показываем код
                    parts.Add(new ContentPart { IsEmote = false, Text = code });
                }

                lastIndex = start + length;
            }

            // Добавляем оставшийся текст после последнего смайла
            if (lastIndex < text.Length)
            {
                var textAfter = text.Substring(lastIndex);
                parts.Add(new ContentPart { IsEmote = false, Text = textAfter });
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[MessageViewModel] Error parsing emotes: {ex.Message}");
            // В случае ошибки возвращаем весь текст как есть
            parts.Clear();
            parts.Add(new ContentPart { IsEmote = false, Text = text });
        }

        return parts;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
