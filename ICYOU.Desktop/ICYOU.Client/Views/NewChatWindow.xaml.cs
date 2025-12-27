using System.Collections.ObjectModel;
using System.Windows;
using ICYOU.Core.Protocol;
using ICYOU.SDK;

namespace ICYOU.Client.Views;

public partial class NewChatWindow : Window
{
    private readonly ObservableCollection<UserSelectViewModel> _users = new();
    
    public NewChatWindow()
    {
        InitializeComponent();
        UsersList.ItemsSource = _users;
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
                _users.Clear();
                foreach (var friend in data.Friends)
                {
                    _users.Add(new UserSelectViewModel(friend));
                }
                
                if (_users.Count == 0)
                {
                    MessageBox.Show("У вас пока нет друзей. Добавьте друзей через кнопку 'Друзья' в главном окне.", 
                        "Нет друзей", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }
    
    private async void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        var name = ChatName.Text.Trim();
        var selectedUsers = _users.Where(u => u.IsSelected).Select(u => u.User.Id).ToList();
        
        if (string.IsNullOrEmpty(name))
        {
            MessageBox.Show("Введите название чата", "Ошибка");
            return;
        }
        
        if (selectedUsers.Count == 0)
        {
            MessageBox.Show("Выберите хотя бы одного участника", "Ошибка");
            return;
        }
        
        var response = await App.NetworkClient!.SendAndWaitAsync(new Packet(PacketType.CreateChat, new CreateChatData
        {
            Name = name,
            MemberIds = selectedUsers
        }));
        
        if (response?.Type == PacketType.CreateChatResponse)
        {
            DialogResult = true;
            Close();
        }
        else
        {
            MessageBox.Show("Ошибка создания чата", "Ошибка");
        }
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public class UserSelectViewModel
{
    public User User { get; }
    public string Username => User.Username;
    public string DisplayName => User.DisplayName;
    public string AvatarLetter => DisplayName.Length > 0 ? DisplayName[0].ToString().ToUpper() : "?";
    public string StatusText => User.Status == UserStatus.Online ? "В сети" : "Не в сети";
    public bool IsSelected { get; set; }
    
    public UserSelectViewModel(User user)
    {
        User = user;
    }
}

