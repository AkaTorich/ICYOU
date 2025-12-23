using System.ComponentModel;
using System.Runtime.CompilerServices;
using ICYOU.SDK;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace ICYOU.Mobile.ViewModels;

public class MessageViewModel : INotifyPropertyChanged
{
    public Message Message { get; }

    public bool IsOwnMessage => Message.SenderId == AppState.CurrentUser?.Id;

    public string SenderName => Message.SenderName;

    public string Content => Message.Content;

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
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
