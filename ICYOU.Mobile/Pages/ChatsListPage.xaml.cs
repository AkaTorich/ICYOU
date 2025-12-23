using System.Collections.ObjectModel;
using ICYOU.Core.Protocol;
using ICYOU.Mobile.Services;
using ICYOU.Mobile.ViewModels;
using ICYOU.SDK;

namespace ICYOU.Mobile.Pages;

public partial class ChatsListPage : ContentPage
{
    private readonly ObservableCollection<ChatViewModel> _chats = new();
    private readonly List<ChatViewModel> _allChats = new();
    private List<Chat> _privateChats = new();
    private List<Chat> _groupChats = new();
    private List<User> _friends = new();

    public ChatsListPage()
    {
        try
        {
            InitializeComponent();
            ChatsList.ItemsSource = _chats;
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[ChatsListPage] Constructor error: {ex.Message}");
            DebugLog.Write($"[ChatsListPage] Stack trace: {ex.StackTrace}");
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

        await LoadAllData();
    }

    private async Task LoadAllData()
    {
        await LoadChats();
        await LoadFriends();
        RebuildList();
    }

    private async Task LoadChats()
    {
        if (AppState.NetworkClient == null) return;

        try
        {
            var response = await AppState.NetworkClient.SendAndWaitAsync(new Packet(PacketType.GetUserChats));
            if (response?.Type == PacketType.UserChatsResponse)
            {
                var data = response.GetData<UserChatsResponseData>();
                if (data != null)
                {
                    _privateChats = data.Chats.Where(c => c.Type == ChatType.Private).ToList();
                    _groupChats = data.Chats.Where(c => c.Type == ChatType.Group).ToList();
                }
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[ChatsListPage] LoadChats error: {ex.Message}");
        }
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
                    _friends = data.Friends;
                }
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[ChatsListPage] LoadFriends error: {ex.Message}");
        }
    }

    private void RebuildList()
    {
        _allChats.Clear();

        // Добавляем групповые чаты
        foreach (var chat in _groupChats)
        {
            _allChats.Add(new ChatViewModel(chat));
        }

        // Добавляем друзей с привязкой к приватным чатам
        foreach (var friend in _friends)
        {
            var privateChat = _privateChats.FirstOrDefault(c => c.MemberIds.Contains(friend.Id));
            _allChats.Add(new ChatViewModel(friend, privateChat));
        }

        // Добавляем приватные чаты без друзей
        foreach (var chat in _privateChats)
        {
            var otherUserId = chat.MemberIds.FirstOrDefault(id => id != AppState.CurrentUser!.Id);
            if (!_friends.Any(f => f.Id == otherUserId))
            {
                _allChats.Add(new ChatViewModel(chat));
            }
        }

        RefreshList();
    }

    private void RefreshList()
    {
        var query = SearchBox?.Text?.ToLower().Trim() ?? "";
        _chats.Clear();

        var filtered = string.IsNullOrEmpty(query)
            ? _allChats
            : _allChats.Where(x => x.DisplayName.ToLower().Contains(query)).ToList();

        // Сортировка: сначала непрочитанные, потом онлайн, потом по имени
        var sorted = filtered
            .OrderByDescending(x => x.UnreadCount > 0)
            .ThenByDescending(x => x.IsFriend && x.IsOnline)
            .ThenBy(x => x.DisplayName);

        foreach (var item in sorted)
        {
            _chats.Add(item);
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshList();
    }

    private async void OnChatSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ChatViewModel chat)
        {
            // Открываем чат
            await Shell.Current.GoToAsync("chat", new Dictionary<string, object>
            {
                ["ChatViewModel"] = chat
            });

            // Сброс выделения
            ChatsList.SelectedItem = null;
        }
    }

    private void OnPacketReceived(object? sender, Packet packet)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            switch (packet.Type)
            {
                case PacketType.MessageReceived:
                    // Обновляем список чатов при новом сообщении
                    await LoadChats();
                    RebuildList();
                    break;

                case PacketType.FriendRequest:
                case PacketType.FriendRequestResponse:
                case PacketType.UserStatusChanged:
                    // Обновляем список друзей
                    await LoadFriends();
                    RebuildList();
                    break;
            }
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Отписываемся от событий при исчезновении страницы
        if (AppState.NetworkClient != null)
        {
            AppState.NetworkClient.PacketReceived -= OnPacketReceived;
        }
    }
}
