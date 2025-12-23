using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using ICYOU.Core.Protocol;
using ICYOU.SDK;

namespace ICYOU.Client.Views;

public partial class ChatMembersWindow : Window
{
    private readonly Chat _chat;
    private readonly ObservableCollection<MemberViewModel> _members = new();
    
    public ChatMembersWindow(Chat chat)
    {
        InitializeComponent();
        _chat = chat;
        MembersList.ItemsSource = _members;
        TitleText.Text = $"Участники - {chat.Name}";
        
        // Скрыть панель приглашения для приватных чатов
        if (chat.Type == ChatType.Private)
        {
            InvitePanel.Visibility = Visibility.Collapsed;
            LeaveButton.Visibility = Visibility.Collapsed;
        }
        
        LoadMembers();
    }
    
    private async void LoadMembers()
    {
        var response = await App.NetworkClient!.SendAndWaitAsync(new Packet(PacketType.GetChatMembers, new ChatActionData
        {
            ChatId = _chat.Id
        }));
        
        if (response?.Type == PacketType.ChatMembersResponse)
        {
            var data = response.GetData<ChatMembersResponseData>();
            if (data != null)
            {
                _members.Clear();
                foreach (var member in data.Members)
                {
                    _members.Add(new MemberViewModel(member, _chat.OwnerId, App.CurrentUser!.Id == _chat.OwnerId));
                }
            }
        }
    }
    
    private async void InviteButton_Click(object sender, RoutedEventArgs e)
    {
        var username = InviteBox.Text.Trim();
        if (string.IsNullOrEmpty(username)) return;
        
        // Ищем пользователя
        var searchResponse = await App.NetworkClient!.SendAndWaitAsync(new Packet(PacketType.GetUserInfo, new GetUserInfoData
        {
            Username = username
        }));
        
        if (searchResponse?.Type == PacketType.UserInfoResponse)
        {
            var user = searchResponse.GetData<User>();
            if (user != null)
            {
                await App.NetworkClient!.SendAsync(new Packet(PacketType.InviteToChat, new InviteToChatData
                {
                    ChatId = _chat.Id,
                    UserId = user.Id
                }));
                
                InviteBox.Clear();
                MessageBox.Show("Приглашение отправлено!", "Успех");
            }
            else
            {
                MessageBox.Show("Пользователь не найден", "Ошибка");
            }
        }
    }
    
    private async void KickButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        var member = btn?.Tag as MemberViewModel;
        if (member == null) return;
        
        var result = MessageBox.Show(
            $"Исключить {member.DisplayName}?",
            "Подтверждение",
            MessageBoxButton.YesNo);
            
        if (result == MessageBoxResult.Yes)
        {
            await App.NetworkClient!.SendAsync(new Packet(PacketType.KickFromChat, new InviteToChatData
            {
                ChatId = _chat.Id,
                UserId = member.User.Id
            }));
            
            _members.Remove(member);
        }
    }
    
    private async void LeaveButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Вы уверены, что хотите покинуть чат?",
            "Подтверждение",
            MessageBoxButton.YesNo);
            
        if (result == MessageBoxResult.Yes)
        {
            await App.NetworkClient!.SendAsync(new Packet(PacketType.LeaveChat, new ChatActionData
            {
                ChatId = _chat.Id
            }));
            
            Close();
        }
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

public class MemberViewModel
{
    public User User { get; }
    private readonly long _ownerId;
    private readonly bool _currentUserIsOwner;
    
    public string DisplayName => User.DisplayName;
    public string Username => User.Username;
    public string AvatarLetter => DisplayName.Length > 0 ? DisplayName[0].ToString().ToUpper() : "?";
    public bool IsOwner => User.Id == _ownerId;
    public Visibility IsOwnerVisibility => IsOwner ? Visibility.Visible : Visibility.Collapsed;
    public Visibility CanKickVisibility => _currentUserIsOwner && !IsOwner ? Visibility.Visible : Visibility.Collapsed;
    
    public MemberViewModel(User user, long ownerId, bool currentUserIsOwner)
    {
        User = user;
        _ownerId = ownerId;
        _currentUserIsOwner = currentUserIsOwner;
    }
}

