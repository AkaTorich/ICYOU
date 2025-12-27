using System.ComponentModel;
using System.Runtime.CompilerServices;
using ICYOU.SDK;

namespace ICYOU.Mobile.ViewModels;

public class ChatViewModel : INotifyPropertyChanged
{
    private int _unreadCount;
    private Message? _lastMessage;

    public Chat? Chat { get; }
    public User? Friend { get; }
    public bool IsFriend => Friend != null;

    public string DisplayName =>
        IsFriend ? Friend!.DisplayName :
        Chat != null ? Chat.Name : "Unknown";

    public string LastMessageText
    {
        get
        {
            if (LastMessage == null)
            {
                // Если нет сообщений и это друг - показываем статус
                if (IsFriend)
                    return StatusText;
                return "Нет сообщений";
            }

            var content = LastMessage.Content;

            // Обработка файлов
            if (content.StartsWith("[FILE|"))
                return "📎 Файл";

            // Удаление служебных тегов из превью
            if (content.Contains("[LINKPREVIEW|"))
            {
                var previewStart = content.IndexOf("[LINKPREVIEW|");
                var previewEnd = content.IndexOf("]", previewStart);
                if (previewEnd > previewStart)
                {
                    var textBefore = previewStart > 0 ? content.Substring(0, previewStart).Trim() : "";
                    var textAfter = previewEnd + 1 < content.Length ? content.Substring(previewEnd + 1).TrimStart() : "";

                    if (!string.IsNullOrEmpty(textBefore))
                        content = textBefore;
                    else if (!string.IsNullOrEmpty(textAfter))
                        content = textAfter;
                    else
                    {
                        // Показываем 🔗 если только превью без текста
                        var previewData = content.Substring(previewStart + 13, previewEnd - previewStart - 13);
                        var parts = previewData.Split('|');
                        if (parts.Length >= 2)
                            content = "🔗 " + parts[1].Replace("{{PIPE}}", "|");
                        else
                            content = "🔗 Ссылка";
                    }
                }
            }

            // Удаление цитат из превью
            if (content.StartsWith("[QUOTE|") || content.StartsWith("[QUOTES|"))
            {
                var endQuote = content.IndexOf(']');
                if (endQuote > 0 && endQuote < content.Length - 1)
                    content = content.Substring(endQuote + 1).TrimStart();
            }

            // Обрезаем до 30 символов как на Desktop
            if (content.Length > 30)
                content = content.Substring(0, 30) + "...";

            // Для групповых чатов добавляем имя отправителя
            if (!IsFriend && Chat?.Type == ChatType.Group)
            {
                var isFromMe = LastMessage.SenderId == Services.AppState.CurrentUser?.Id;
                if (!isFromMe)
                    content = $"{LastMessage.SenderName}: {content}";
            }

            return content;
        }
    }

    public string LastMessageTime
    {
        get
        {
            if (LastMessage == null)
                return "";

            var ts = LastMessage.Timestamp;
            if (ts.Date == DateTime.Today)
                return ts.ToString("HH:mm");
            if (ts.Date == DateTime.Today.AddDays(-1))
                return "Вчера";
            return ts.ToString("dd.MM.yy");
        }
    }

    public int UnreadCount
    {
        get => _unreadCount;
        set
        {
            if (_unreadCount != value)
            {
                _unreadCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasUnread));
            }
        }
    }

    public bool HasUnread => UnreadCount > 0;

    public bool IsOnline => Friend?.Status == UserStatus.Online;

    public string StatusText
    {
        get
        {
            if (Friend == null)
                return "";

            return Friend.Status switch
            {
                UserStatus.Online => "В сети",
                UserStatus.Away => "Отошел",
                UserStatus.DoNotDisturb => "Не беспокоить",
                _ => "Не в сети"
            };
        }
    }

    public Color StatusColor
    {
        get
        {
            if (Friend == null)
                return Colors.Gray;

            return Friend.Status == UserStatus.Online ?
                Color.FromRgb(76, 175, 80) :
                Color.FromRgb(117, 117, 117);
        }
    }

    public string AvatarLetter
    {
        get
        {
            var name = DisplayName;
            return name.Length > 0 ? name[0].ToString().ToUpper() : "?";
        }
    }

    public Message? LastMessage
    {
        get => _lastMessage;
        set
        {
            if (_lastMessage != value)
            {
                _lastMessage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastMessageText));
                OnPropertyChanged(nameof(LastMessageTime));
            }
        }
    }

    public long ChatId => Chat?.Id ?? 0;

    // Конструктор для группового чата
    public ChatViewModel(Chat chat)
    {
        Chat = chat;
        Friend = null;
        _lastMessage = chat.LastMessage;
        _unreadCount = chat.UnreadCount;
    }

    // Конструктор для друга (с или без чата)
    public ChatViewModel(User friend, Chat? chat = null)
    {
        Friend = friend;
        Chat = chat;
        _lastMessage = chat?.LastMessage;
        _unreadCount = chat?.UnreadCount ?? 0;
    }

    public void UpdateStatus()
    {
        OnPropertyChanged(nameof(IsOnline));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(LastMessageText)); // Обновляем т.к. может показывать статус
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
