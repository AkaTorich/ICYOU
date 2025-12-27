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
                return "Нет сообщений";

            var content = LastMessage.Content;
            if (content.StartsWith("[FILE|"))
                return "📎 Файл";
            if (content.Length > 50)
                content = content.Substring(0, 47) + "...";
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
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
