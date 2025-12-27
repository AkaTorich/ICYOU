using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ICYOU.Core.Protocol;
using ICYOU.SDK;

namespace ICYOU.Client.Views;

public partial class FriendsWindow : Window
{
    private readonly ObservableCollection<FriendViewModel> _friends = new();
    
    public FriendsWindow()
    {
        InitializeComponent();
        FriendsList.ItemsSource = _friends;
        LoadFriends();
    }
    
    private async void LoadFriends()
    {
        var response = await App.NetworkClient!.SendAndWaitAsync(new Packet(PacketType.GetFriends));
        
        if (response?.Type == PacketType.FriendsListResponse)
        {
            var data = response.GetData<FriendsListResponseData>();
            if (data != null)
            {
                _friends.Clear();
                foreach (var friend in data.Friends)
                {
                    _friends.Add(new FriendViewModel(friend));
                }
            }
        }
    }
    
    private async void AddFriendButton_Click(object sender, RoutedEventArgs e)
    {
        var username = AddFriendBox.Text.Trim();
        if (string.IsNullOrEmpty(username)) return;
        
        // Сначала ищем пользователя
        var searchResponse = await App.NetworkClient!.SendAndWaitAsync(new Packet(PacketType.GetUserInfo, new GetUserInfoData
        {
            Username = username
        }));
        
        if (searchResponse?.Type == PacketType.UserInfoResponse)
        {
            var user = searchResponse.GetData<User>();
            if (user != null)
            {
                await App.NetworkClient!.SendAsync(new Packet(PacketType.AddFriend, new FriendActionData
                {
                    UserId = user.Id
                }));
                
                AddFriendBox.Clear();
                MessageBox.Show("Запрос в друзья отправлен!", "Успех");
            }
            else
            {
                MessageBox.Show("Пользователь не найден", "Ошибка");
            }
        }
    }
    
    private void MessageFriendButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        var friend = btn?.Tag as FriendViewModel;
        if (friend == null) return;
        
        // Закрываем окно - главное окно само откроет чат с этим другом
        SelectedFriend = friend.User;
        Close();
    }
    
    public User? SelectedFriend { get; private set; }
    
    private async void RemoveFriendButton_Click(object sender, RoutedEventArgs e)
    {
        var btn = sender as Button;
        var friend = btn?.Tag as FriendViewModel;
        if (friend == null) return;
        
        var result = MessageBox.Show(
            $"Удалить {friend.DisplayName} из друзей?",
            "Подтверждение",
            MessageBoxButton.YesNo);
            
        if (result == MessageBoxResult.Yes)
        {
            await App.NetworkClient!.SendAsync(new Packet(PacketType.RemoveFriend, new FriendActionData
            {
                UserId = friend.User.Id
            }));
            
            _friends.Remove(friend);
        }
    }
}

public class FriendViewModel
{
    public User User { get; }
    
    public string DisplayName => User.DisplayName;
    public string Username => User.Username;
    public string AvatarLetter => DisplayName.Length > 0 ? DisplayName[0].ToString().ToUpper() : "?";
    public string StatusText => User.Status switch
    {
        UserStatus.Online => "В сети",
        UserStatus.Away => "Отошел",
        UserStatus.DoNotDisturb => "Не беспокоить",
        _ => "Не в сети"
    };
    public Brush StatusColor => User.Status == UserStatus.Online ? 
        new SolidColorBrush(Color.FromRgb(76, 175, 80)) : 
        new SolidColorBrush(Color.FromRgb(117, 117, 117));
    
    public FriendViewModel(User user)
    {
        User = user;
    }
}

