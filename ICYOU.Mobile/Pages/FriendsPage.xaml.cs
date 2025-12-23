using System.Collections.ObjectModel;
using ICYOU.Core.Protocol;
using ICYOU.Mobile.Services;
using ICYOU.Mobile.ViewModels;
using ICYOU.SDK;

namespace ICYOU.Mobile.Pages;

public partial class FriendsPage : ContentPage
{
    private readonly ObservableCollection<FriendViewModel> _friends = new();

    public FriendsPage()
    {
        try
        {
            InitializeComponent();
            FriendsList.ItemsSource = _friends;
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[FriendsPage] Constructor error: {ex.Message}");
            DebugLog.Write($"[FriendsPage] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Не загружаем данные если пользователь не залогинен
        if (AppState.NetworkClient == null || AppState.CurrentUser == null)
            return;

        // Подписываемся на события только при появлении страницы, когда NetworkClient точно существует
        if (AppState.NetworkClient != null)
        {
            AppState.NetworkClient.PacketReceived -= OnPacketReceived; // Отписываемся на случай если уже подписаны
            AppState.NetworkClient.PacketReceived += OnPacketReceived;
        }

        await LoadFriends();
    }

    private async Task LoadFriends()
    {
        if (AppState.NetworkClient == null) return;

        try
        {
            var response = await AppState.NetworkClient.SendAndWaitAsync(new Packet(PacketType.GetFriends));

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
        catch (Exception ex)
        {
            ShowStatus($"Ошибка загрузки друзей: {ex.Message}", false);
            DebugLog.Write($"[FriendsPage] LoadFriends error: {ex}");
        }
    }

    private async void OnAddFriendClicked(object sender, EventArgs e)
    {
        var username = AddFriendBox.Text?.Trim();
        if (string.IsNullOrEmpty(username))
        {
            ShowStatus("Введите логин пользователя", false);
            return;
        }

        if (AppState.NetworkClient == null)
        {
            ShowStatus("Нет подключения к серверу", false);
            return;
        }

        try
        {
            // Ищем пользователя
            var searchResponse = await AppState.NetworkClient.SendAndWaitAsync(new Packet(PacketType.GetUserInfo, new GetUserInfoData
            {
                Username = username
            }));

            if (searchResponse?.Type == PacketType.UserInfoResponse)
            {
                var user = searchResponse.GetData<User>();
                if (user != null)
                {
                    // Отправляем запрос в друзья
                    await AppState.NetworkClient.SendAsync(new Packet(PacketType.AddFriend, new FriendActionData
                    {
                        UserId = user.Id
                    }));

                    AddFriendBox.Text = string.Empty;
                    ShowStatus($"Запрос отправлен {user.DisplayName}", true);

                    // Обновляем список
                    await LoadFriends();
                }
                else
                {
                    ShowStatus("Пользователь не найден", false);
                }
            }
            else
            {
                ShowStatus("Сервер не ответил", false);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Не удалось добавить: {ex.Message}", false);
            DebugLog.Write($"[FriendsPage] AddFriend error: {ex}");
        }
    }

    private async void OnMessageFriendClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is FriendViewModel friend)
        {
            // Создаем ChatViewModel для этого друга
            var chatViewModel = new ChatViewModel(friend.User);

            // Переходим на страницу чата
            await Shell.Current.GoToAsync("chat", new Dictionary<string, object>
            {
                ["ChatViewModel"] = chatViewModel
            });
        }
    }

    private async void OnRemoveFriendClicked(object sender, EventArgs e)
    {
        if (sender is Button button && button.CommandParameter is FriendViewModel friend)
        {
            if (AppState.NetworkClient == null)
            {
                ShowStatus("Нет подключения к серверу", false);
                return;
            }

            try
            {
                await AppState.NetworkClient.SendAsync(new Packet(PacketType.RemoveFriend, new FriendActionData
                {
                    UserId = friend.User.Id
                }));

                _friends.Remove(friend);
                ShowStatus($"{friend.DisplayName} удален", true);
            }
            catch (Exception ex)
            {
                ShowStatus($"Не удалось удалить: {ex.Message}", false);
                DebugLog.Write($"[FriendsPage] RemoveFriend error: {ex}");
            }
        }
    }

    private void OnPacketReceived(object? sender, Packet packet)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            switch (packet.Type)
            {
                case PacketType.FriendRequest:
                case PacketType.FriendRequestResponse:
                case PacketType.UserStatusChanged:
                    // Обновляем список друзей
                    await LoadFriends();
                    break;
            }
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (AppState.NetworkClient != null)
        {
            AppState.NetworkClient.PacketReceived -= OnPacketReceived;
        }
    }

    private void ShowStatus(string message, bool isSuccess)
    {
        StatusText.Text = message;
        StatusText.TextColor = isSuccess ? Colors.Green : Colors.Red;
        StatusText.IsVisible = true;

        // Скрываем через 3 секунды
        Task.Delay(3000).ContinueWith(_ =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusText.IsVisible = false;
            });
        });
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

    public Color StatusColor => User.Status == UserStatus.Online ?
        Color.FromRgb(76, 175, 80) :
        Color.FromRgb(117, 117, 117);

    public FriendViewModel(User user)
    {
        User = user;
    }
}
