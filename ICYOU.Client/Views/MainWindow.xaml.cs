using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ICYOU.Client.Services;
using ICYOU.Core.Protocol;
using ICYOU.SDK;
using Microsoft.Win32;
using WpfAnimatedGif;

namespace ICYOU.Client.Views;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ChatViewModel> _chats = new();
    private readonly ObservableCollection<MessageViewModel> _messages = new();
    private ChatViewModel? _currentChat;
    private readonly Dictionary<string, string> _emotePaths = new();
    private bool _isRefreshing;
    private readonly List<MessageViewModel> _quotedMessages = new();
    private const int MaxQuotes = 3;
    private DateTime _lastClickTime;
    private MessageViewModel? _lastClickedMessage;
    
    private List<ChatViewModel> _allItems = new();
    private bool _isUpdatingChats = false;
    
    public MainWindow()
    {
        InitializeComponent();
        
        ChatsList.ItemsSource = _chats;
        MessagesList.ItemsSource = _messages;
        
        UserDisplayName.Text = App.CurrentUser?.DisplayName ?? "Пользователь";
        
        App.NetworkClient!.PacketReceived += OnPacketReceived;
        
        // Пауза видео при скролле
        MessagesScrollViewer.ScrollChanged += OnMessagesScrollChanged;
        
        // Проверяем нужен ли пароль для БД
        CheckDatabasePassword();
        
        LoadAllData();
        LoadEmotes();
    }
    
    private void CheckDatabasePassword()
    {
        var db = LocalDatabaseService.Instance;
        if (db.NeedsPassword)
        {
            // Показываем диалог ввода пароля
            var dialog = new PasswordDialog();
            while (db.NeedsPassword)
            {
                if (dialog.ShowDialog() == true)
                {
                    if (db.VerifyPassword(dialog.Password))
                    {
                        break;
                    }
                    MessageBox.Show("Неверный пароль!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    // Пользователь отменил - отключаем шифрование для этой сессии
                    MessageBox.Show("Зашифрованные сообщения не будут расшифрованы.", "Внимание", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    break;
                }
            }
        }
    }
    
    
    private void OnMessagesScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Если изменилась позиция скролла - ставим видео на паузу
        if (e.VerticalChange != 0)
        {
            Converters.MessageContentConverter.PauseAllVideos();
        }
    }
    
    private async void LoadAllData()
    {
        await LoadChats();
        await LoadFriends();
    }
    
    private List<Chat> _privateChats = new();
    private List<Chat> _groupChats = new();
    private List<User> _friends = new();
    
    private async Task LoadChats()
    {
        var response = await App.NetworkClient!.SendAndWaitAsync(new Packet(PacketType.GetUserChats));
        if (response?.Type == PacketType.UserChatsResponse)
        {
            var data = response.GetData<UserChatsResponseData>();
            if (data != null)
            {
                _privateChats = data.Chats.Where(c => c.Type == ChatType.Private).ToList();
                _groupChats = data.Chats.Where(c => c.Type == ChatType.Group).ToList();
                Dispatcher.Invoke(() => RebuildList());
            }
        }
    }
    
    private async Task LoadFriends()
    {
        var response = await App.NetworkClient!.SendAndWaitAsync(new Packet(PacketType.GetFriends));
        if (response?.Type == PacketType.FriendsListResponse)
        {
            var data = response.GetData<FriendsListResponseData>();
            if (data != null)
            {
                _friends = data.Friends;
                Dispatcher.Invoke(() => RebuildList());
            }
        }
    }
    
    private void RebuildList()
    {
        _allItems.Clear();
        
        // Добавляем групповые чаты
        foreach (var chat in _groupChats)
        {
            _allItems.Add(new ChatViewModel(chat));
        }
        
        // Добавляем друзей с привязкой к приватным чатам
        foreach (var friend in _friends)
        {
            // Ищем приватный чат с этим другом
            var privateChat = _privateChats.FirstOrDefault(c => c.MemberIds.Contains(friend.Id));
            _allItems.Add(new ChatViewModel(friend, privateChat));
        }
        
        // Добавляем приватные чаты без друзей (если такие есть)
        foreach (var chat in _privateChats)
        {
            var otherUserId = chat.MemberIds.FirstOrDefault(id => id != App.CurrentUser!.Id);
            if (!_friends.Any(f => f.Id == otherUserId))
            {
                // Это чат с не-другом - показываем как чат
                _allItems.Add(new ChatViewModel(chat));
            }
        }
        
        RefreshList();
    }
    
    private void RefreshList()
    {
        var query = SearchBox.Text.ToLower().Trim();
        _chats.Clear();
        
        var filtered = string.IsNullOrEmpty(query) 
            ? _allItems 
            : _allItems.Where(x => x.DisplayName.ToLower().Contains(query)).ToList();
        
        // Сначала чаты с непрочитанными, потом онлайн друзья, потом остальные
        var sorted = filtered
            .OrderByDescending(x => x.UnreadCount > 0)
            .ThenByDescending(x => x.IsFriend && x.IsOnline)
            .ThenBy(x => x.DisplayName);
            
        foreach (var item in sorted)
        {
            _chats.Add(item);
        }
        
        // Показываем подсказку если список пуст
        EmptyListHint.Visibility = _chats.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        
        // Восстанавливаем выделение текущего чата
        if (_currentChat != null && _chats.Contains(_currentChat))
        {
            _isRefreshing = true;
            ChatsList.SelectedItem = _currentChat;
            _isRefreshing = false;
        }
    }
    
    private async void HandleFileAvailable(Packet packet)
    {
        var data = packet.GetData<FileNotificationData>();
        if (data == null) return;
        
        Console.WriteLine($"[File] Доступен файл: {data.FileName} от {data.SenderName}");
        
        // Скачиваем файл с сервера
        var fileService = Services.FileTransferService.Instance;
        
        Dispatcher.Invoke(() => ShowProgress());
        fileService.TransferProgress += OnTransferProgress;
        
        var (fileName, fileData) = await fileService.DownloadFileAsync(data.FileId);
        
        fileService.TransferProgress -= OnTransferProgress;
        Dispatcher.Invoke(() => HideProgress());
        
        if (fileData == null || fileName == null)
        {
            Console.WriteLine("[File] Не удалось скачать файл");
            return;
        }
        
        // Сохраняем все файлы автоматически в Downloads
        var savedPath = fileService.SaveToDownloads(fileName, fileData);
        
        // Используем ChatId напрямую (10-миллиардное число) для всех чатов
        var dbChatId = data.ChatId;
        
        // Сохраняем информацию о файле в локальную БД
        LocalDatabaseService.Instance.SaveFile(
            data.FileId, 
            0, // messageId будет позже
            dbChatId, 
            fileName, 
            data.FileType, 
            savedPath, 
            data.FileSize);
        
        bool isMediaFile = data.FileType == "image" || data.FileType == "video" || data.FileType == "audio";
        
        // Создаём сообщение с путём к файлу
        string content;
        MessageType msgType;
        var base64 = Convert.ToBase64String(fileData);
        
        // Формат: [FILE|имя|тип|путь|base64]
        content = $"[FILE|{fileName}|{data.FileType}|{savedPath}|{base64}]";
        msgType = data.FileType == "image" ? MessageType.Image : MessageType.File;
        
        var msg = new Message
        {
            Id = DateTime.UtcNow.Ticks,
            ChatId = dbChatId,
            SenderId = data.SenderId,
            SenderName = data.SenderName,
            Content = content,
            Type = msgType,
            Timestamp = DateTime.UtcNow,
            Status = MessageStatus.Sent
        };
        
        var msgVm = new MessageViewModel(msg, App.CurrentUser!.Id);
        
        // Сохраняем в локальную БД
        LocalDatabaseService.Instance.SaveMessage(msg);
        
        Dispatcher.Invoke(() =>
        {
            // Используем только ChatId (10-миллиардное число) для всех чатов
            bool isCurrentChat = _currentChat?.ChatId == data.ChatId;
            
            if (isCurrentChat)
            {
                _messages.Add(msgVm);
                ScrollToBottom();
            }
            else
            {
                // Сообщение уже в БД - увеличиваем только счётчик непрочитанных
                var chatVm = _allItems.FirstOrDefault(x => x.ChatId == data.ChatId);
                
                if (chatVm != null)
                {
                    chatVm.UnreadCount++;
                    RefreshList();
                }
            }
            
            // Обновляем превью - используем только ChatId
            UpdateMessagePreview(msg);
        });
    }
    
    private void HandleProcessedMessage(Message msg)
    {
        // Используем ChatId напрямую (10-миллиардное число) - никаких изменений не нужно
        // msg.ChatId уже содержит правильный ChatId приватного или группового чата
        
        DebugLog.Write($"[CLIENT] HandleProcessedMessage: ChatId={msg.ChatId}, SenderId={msg.SenderId}, CurrentChatId={_currentChat?.ChatId}");
        
        // Проверяем совпадает ли чат с текущим открытым
        bool isCurrentChat = _currentChat?.ChatId == msg.ChatId;
        DebugLog.Write($"[CLIENT] isCurrentChat={isCurrentChat}");
        
        var msgVm = new MessageViewModel(msg, App.CurrentUser!.Id);
        
        // Сохраняем в локальную БД
        DebugLog.Write($"[CLIENT] Сохранение в БД: ChatId={msg.ChatId}");
        LocalDatabaseService.Instance.SaveMessage(msg);
        DebugLog.Write($"[CLIENT] Сообщение сохранено в БД");
        
        if (isCurrentChat)
        {
            _messages.Add(msgVm);
            ScrollToBottom();
            DebugLog.Write($"[CLIENT] Сообщение добавлено в UI (текущий чат)");
        }
        else
        {
            DebugLog.Write($"[CLIENT] Сообщение НЕ добавлено в UI (не текущий чат), будет загружено при открытии");
        }
        // Не добавляем в pending - сообщение уже в БД, загрузится при открытии чата
        
        UpdateMessagePreview(msg);
    }
    
    private void UpdateMessagePreview(Message msg)
    {
        // Определяем от кого сообщение
        var senderId = msg.SenderId;
        var isFromMe = senderId == App.CurrentUser?.Id;
        DebugLog.Write($"[CLIENT] UpdateMessagePreview: ChatId={msg.ChatId}, SenderId={senderId}, isFromMe={isFromMe}, Всего чатов в списке={_allItems.Count}");
        
        // Ищем контакт в списке по ChatId (10-миллиардное число)
        bool found = false;
        foreach (var item in _allItems)
        {
            bool match = false;
            
            // Ищем по ChatId для всех чатов (групповых и приватных)
            if (item.ChatId.HasValue && item.ChatId.Value == msg.ChatId)
            {
                match = true;
            }
            
            if (match)
            {
                found = true;
                DebugLog.Write($"[CLIENT] Найден чат в списке: ChatId={item.ChatId}, DisplayName={item.DisplayName}");
                
                // Обновляем превью
                var preview = msg.Content;
                if (preview.Length > 30)
                    preview = preview.Substring(0, 30) + "...";
                    
                // Добавляем имя отправителя для групповых чатов или входящих
                if (!isFromMe)
                {
                    preview = $"{msg.SenderName}: {preview}";
                }
                
                item.LastMessagePreview = preview;
                item.LastMessageTime = msg.Timestamp;
                
                // Увеличиваем счётчик непрочитанных если это не текущий чат и не от меня
                if (!isFromMe && _currentChat != item)
                {
                    item.UnreadCount++;
                }
                
                break;
            }
        }
        
        if (!found && !_isUpdatingChats)
        {
            DebugLog.Write($"[CLIENT] ВНИМАНИЕ: Чат с ChatId={msg.ChatId} НЕ найден в списке! Всего чатов={_allItems.Count}");
            foreach (var item in _allItems)
            {
                DebugLog.Write($"[CLIENT]   Чат в списке: ChatId={item.ChatId}, DisplayName={item.DisplayName}");
            }
            
            // Обновляем список чатов - возможно создан новый приватный чат
            DebugLog.Write($"[CLIENT] Обновление списка чатов...");
            _isUpdatingChats = true;
            Task.Run(async () =>
            {
                try
                {
                    await LoadChats();
                    await LoadFriends();
                    
                    // После обновления пробуем еще раз обновить превью
                    Dispatcher.BeginInvoke(() =>
                    {
                        DebugLog.Write($"[CLIENT] Повторная попытка обновления превью после загрузки чатов");
                        UpdateMessagePreview(msg);
                        _isUpdatingChats = false;
                    });
                }
                catch (Exception ex)
                {
                    DebugLog.Write($"[CLIENT] Ошибка при обновлении списка чатов: {ex.Message}");
                    _isUpdatingChats = false;
                }
            });
        }
        
        RefreshList();
    }
    
    private void LoadEmotes()
    {
        // Загружаем смайлы из локальной папки с учётом настроек
        var emoteService = Services.EmoteService.Instance;
        var packName = Services.SettingsService.Instance.Settings.EmotePack;
        emoteService.LoadEmotes(packName);
        
        Dispatcher.Invoke(() =>
        {
            EmotesPanel.Children.Clear();
            
            foreach (var kvp in emoteService.Emotes)
            {
                var code = kvp.Key;
                var path = kvp.Value;
                
                var btn = new Button
                {
                    Width = 36,
                    Height = 36,
                    Margin = new Thickness(2),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = Cursors.Hand,
                    ToolTip = code,
                    Tag = code
                };
                
                try
                {
                    var img = new Image { Width = 28, Height = 28 };
                    
                    if (path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
                    {
                        // Анимированный GIF
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(path);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        
                        ImageBehavior.SetAnimatedSource(img, bitmap);
                        ImageBehavior.SetRepeatBehavior(img, RepeatBehavior.Forever);
                    }
                    else
                    {
                        // Статичное изображение
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(path);
                        bitmap.DecodePixelWidth = 32;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();
                        img.Source = bitmap;
                    }
                    
                    btn.Content = img;
                }
                catch
                {
                    btn.Content = new TextBlock { Text = code, FontSize = 14 };
                }
                
                btn.Click += (s, e) =>
                {
                    MessageInput.Text += (string)btn.Tag;
                    EmotesPopup.IsOpen = false;
                    MessageInput.Focus();
                };
                
                EmotesPanel.Children.Add(btn);
            }
            
            if (emoteService.Emotes.Count == 0)
            {
                EmotesPanel.Children.Add(new TextBlock
                {
                    Text = "Папка emotes пуста",
                    Foreground = Brushes.Gray,
                    Margin = new Thickness(10)
                });
            }
        });
    }
    
    public void RefreshEmotesPanel()
    {
        LoadEmotes();
    }
    
    private void OnPacketReceived(object? sender, Packet packet)
    {
        // Для сообщений - обрабатываем модули в фоне, чтобы не блокировать UI
        if (packet.Type == PacketType.MessageReceived)
        {
            var msg = packet.GetData<Message>();
            if (msg == null)
            {
                DebugLog.Write("[CLIENT] Получено сообщение, но msg == null");
                return;
            }
            
            DebugLog.Write($"[CLIENT] Получено сообщение: ChatId={msg.ChatId}, SenderId={msg.SenderId}, SenderName={msg.SenderName}, Content={msg.Content.Substring(0, Math.Min(50, msg.Content.Length))}...");
            
            // Обрабатываем модули в фоновом потоке
            Task.Run(() =>
            {
                var processedMsg = ModuleManager.Instance.ProcessIncomingMessage(msg);
                if (processedMsg != null)
                {
                    DebugLog.Write($"[CLIENT] Сообщение обработано модулями: ChatId={processedMsg.ChatId}");
                    Dispatcher.BeginInvoke(() => HandleProcessedMessage(processedMsg));
                }
                else
                {
                    DebugLog.Write("[CLIENT] Сообщение не обработано модулями (processedMsg == null)");
                }
            });
            return;
        }
        
        Dispatcher.Invoke(() =>
        {
            switch (packet.Type)
            {
                case PacketType.FileAvailable:
                    HandleFileAvailable(packet);
                    break;
                    
                case PacketType.MessageRead:
                    var readData = packet.GetData<MessageReadData>();
                    if (readData != null)
                    {
                        // Обновляем статус сообщений в текущем чате
                        foreach (var msgVm in _messages)
                        {
                            if (msgVm.Message.ChatId == readData.ChatId && 
                                msgVm.Message.Id <= readData.MessageId &&
                                msgVm.IsOwn)
                            {
                                msgVm.Message.Status = MessageStatus.Read;
                            }
                        }
                        // Обновляем UI
                        MessagesList.Items.Refresh();
                    }
                    break;
                    
                case PacketType.UserStatusChanged:
                    var statusData = packet.GetData<UserStatusChangedData>();
                    if (statusData != null)
                    {
                        // Обновить статус в списке чатов
                        foreach (var item in _allItems)
                        {
                            if (item.Friend?.Id == statusData.UserId)
                            {
                                item.Friend.Status = statusData.Status;
                            }
                        }
                        RefreshList();
                        
                        // Обновить заголовок если это текущий чат
                        if (_currentChat?.Friend?.Id == statusData.UserId)
                        {
                            ChatSubtitle.Text = statusData.Status == UserStatus.Online ? "В сети" : "Не в сети";
                        }
                    }
                    break;
                
                case PacketType.FriendRequest:
                    var friendUser = packet.GetData<User>();
                    if (friendUser != null)
                    {
                        var result = MessageBox.Show(
                            $"{friendUser.DisplayName} (@{friendUser.Username}) хочет добавить вас в друзья. Принять?",
                            "Запрос в друзья",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);
                        
                        if (result == MessageBoxResult.Yes)
                        {
                            // Принимаем - отправляем встречный запрос
                            App.NetworkClient!.SendAsync(new Packet(PacketType.AddFriend, new FriendActionData
                            {
                                UserId = friendUser.Id
                            }));
                            // Обновляем список друзей
                            _ = LoadFriends();
                        }
                    }
                    break;
                
                case PacketType.FriendRequestResponse:
                    MessageBox.Show("Ваш запрос в друзья принят!", "Друзья", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Обновляем список друзей
                    _ = LoadFriends();
                    break;
                    
                case PacketType.ChatInvite:
                    // Уведомление о приглашении
                    MessageBox.Show("Вас пригласили в чат!", "Приглашение");
                    LoadChats();
                    break;
                    
                case PacketType.FileTransferRequest:
                    HandleFileTransferRequest(packet);
                    break;
            }
        });
    }
    
    private void HandleFileTransferRequest(Packet packet)
    {
        var data = packet.GetData<dynamic>();
        if (data == null) return;
        
        string fileName = data.FileName;
        long fileSize = data.FileSize;
        long transferId = data.TransferId;
        
        var result = MessageBox.Show(
            $"Принять файл '{fileName}' ({fileSize / 1024} KB)?",
            "Передача файла",
            MessageBoxButton.YesNo);
            
        var accept = result == MessageBoxResult.Yes;
        
        App.NetworkClient!.SendAsync(new Packet(PacketType.FileTransferResponse, new FileTransferResponseData
        {
            TransferId = transferId,
            Accept = accept
        }));
    }
    
    private async void ChatsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshing) return;
        
        var selected = ChatsList.SelectedItem as ChatViewModel;
        if (selected == null) return;
        
        _currentChat = selected;
        
        // Сбрасываем счётчик непрочитанных
        selected.UnreadCount = 0;
        RefreshList();
        
        ChatTitle.Text = selected.DisplayName;
        
        if (selected.IsGroupChat)
        {
            ChatSubtitle.Text = $"{selected.Chat!.MemberIds.Count} участников";
        }
        else if (selected.IsFriend)
        {
            ChatSubtitle.Text = selected.IsOnline ? "В сети" : "Не в сети";
        }
        else
        {
            ChatSubtitle.Text = "Личный чат";
        }
        
        EmptyChatPanel.Visibility = Visibility.Collapsed;
        ActiveChatPanel.Visibility = Visibility.Visible;
        
        // Загружаем ТОЛЬКО из локальной БД - сервер не хранит историю
        // Используем ChatId напрямую (10-миллиардное число) для всех чатов
        if (selected.ChatId.HasValue)
        {
            DebugLog.Write($"[CLIENT] Загрузка чата: ChatId={selected.ChatId.Value}, DisplayName={selected.DisplayName}");
            var localMessages = LocalDatabaseService.Instance.GetMessages(selected.ChatId.Value, 100);
            DebugLog.Write($"[CLIENT] Загружено сообщений из БД: {localMessages.Count}");
            _messages.Clear();
            
            foreach (var msg in localMessages)
            {
                if (msg.Content.StartsWith("[FILE|"))
                {
                    msg.Content = RestoreLocalFilePath(msg.Content);
                }
                _messages.Add(new MessageViewModel(msg, App.CurrentUser!.Id));
            }
            
            DebugLog.Write($"[CLIENT] Сообщений добавлено в UI: {_messages.Count}");
            ScrollToBottom();
            return;
        }
        
        // Если нет ChatId - очищаем сообщения
        DebugLog.Write($"[CLIENT] Загрузка чата: НЕТ ChatId, DisplayName={selected.DisplayName}");
        _messages.Clear();
        
        ScrollToBottom();
    }
    
    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        await SendMessage();
    }
    
    private async void MessageInput_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            await SendMessage();
        }
    }
    
    private async Task SendMessage()
    {
        if (_currentChat == null || string.IsNullOrWhiteSpace(MessageInput.Text))
            return;
            
        var content = MessageInput.Text.Trim();
        MessageInput.Clear();
        
        // Если есть цитируемые сообщения - добавляем формат цитат
        if (_quotedMessages.Count > 0)
        {
            var quoteParts = new List<string>();
            foreach (var qm in _quotedMessages)
            {
                var quotedContent = qm.Message.Content;
                // Убираем форматирование файлов для превью
                if (quotedContent.StartsWith("[FILE|"))
                {
                    var parts = quotedContent.Split('|');
                    quotedContent = $"📎 {parts[1]}"; // Имя файла
                }
                // Убираем вложенные цитаты
                if (quotedContent.StartsWith("[QUOTE|") || quotedContent.StartsWith("[QUOTES|"))
                {
                    var endQ = quotedContent.IndexOf(']');
                    if (endQ > 0) quotedContent = quotedContent.Substring(endQ + 1);
                }
                // Заменяем разделители в контенте
                quotedContent = quotedContent.Replace("~", "-").Replace("|", "/");
                // Обрезаем длинные цитаты
                if (quotedContent.Length > 80)
                    quotedContent = quotedContent.Substring(0, 77) + "...";
                
                // Формат каждой цитаты: sender~content
                quoteParts.Add($"{qm.Message.SenderName}~{quotedContent}");
            }
            // Формат: [QUOTES|quote1|quote2|quote3]текст (разделитель между цитатами |)
            content = $"[QUOTES|{string.Join("|", quoteParts)}]{content}";
            ClearQuote();
        }
        
        var sendData = new SendMessageData
        {
            Content = content,
            Type = MessageType.Text
        };
        
        if (_currentChat.ChatId.HasValue)
        {
            // Есть существующий чат
            sendData.ChatId = _currentChat.ChatId.Value;
            DebugLog.Write($"[CLIENT] Отправка сообщения: ChatId={sendData.ChatId}, Content={content.Substring(0, Math.Min(50, content.Length))}...");
        }
        else if (_currentChat.IsFriend)
        {
            // Отправляем другу - сервер создаст приватный чат
            sendData.TargetUserId = _currentChat.Friend!.Id;
            DebugLog.Write($"[CLIENT] Отправка сообщения другу: TargetUserId={sendData.TargetUserId}, Content={content.Substring(0, Math.Min(50, content.Length))}...");
        }
        else
        {
            DebugLog.Write("[CLIENT] ОШИБКА: Нельзя отправить сообщение - нет ChatId и нет друга");
            return;
        }
        
        await App.NetworkClient!.SendAsync(new Packet(PacketType.SendMessage, sendData));
        DebugLog.Write($"[CLIENT] Сообщение отправлено на сервер");
        
        // Перезагружаем чаты чтобы получить новый приватный чат
        if (!_currentChat.ChatId.HasValue)
        {
            await Task.Delay(300);
            await LoadChats();
        }
    }
    
    private void Message_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is MessageViewModel msgVm)
        {
            var now = DateTime.Now;
            
            // Проверяем двойной клик (интервал < 300мс и тот же элемент)
            if (_lastClickedMessage == msgVm && (now - _lastClickTime).TotalMilliseconds < 300)
            {
                // Двойной клик - устанавливаем цитирование
                SetQuote(msgVm);
                _lastClickedMessage = null;
            }
            else
            {
                _lastClickedMessage = msgVm;
                _lastClickTime = now;
            }
        }
    }
    
    private void SetQuote(MessageViewModel msgVm)
    {
        // Проверяем не добавлено ли уже это сообщение
        if (_quotedMessages.Any(q => q.Message.Id == msgVm.Message.Id))
            return;
        
        // Если уже 3 цитаты - заменяем третью
        if (_quotedMessages.Count >= MaxQuotes)
        {
            _quotedMessages.RemoveAt(MaxQuotes - 1);
        }
        
        _quotedMessages.Add(msgVm);
        UpdateQuotePanel();
        MessageInput.Focus();
    }
    
    private void UpdateQuotePanel()
    {
        if (_quotedMessages.Count == 0)
        {
            QuotePanel.Visibility = Visibility.Collapsed;
            return;
        }
        
        QuotePanel.Visibility = Visibility.Visible;
        
        // Формируем текст для отображения
        var lines = new List<string>();
        foreach (var qm in _quotedMessages)
        {
            var content = GetQuotePreview(qm.Message.Content);
            lines.Add($"{qm.Message.SenderName}: {content}");
        }
        
        QuoteSenderName.Text = _quotedMessages.Count == 1 
            ? _quotedMessages[0].Message.SenderName 
            : $"Цитаты ({_quotedMessages.Count})";
        QuoteContent.Text = string.Join("\n", lines.Select(l => l.Length > 50 ? l.Substring(0, 47) + "..." : l));
    }
    
    private string GetQuotePreview(string content)
    {
        if (content.StartsWith("[FILE|"))
        {
            var parts = content.Split('|');
            return $"📎 {parts[1]}";
        }
        if (content.StartsWith("[QUOTE|"))
        {
            var endQuote = content.IndexOf(']');
            if (endQuote > 0)
                content = content.Substring(endQuote + 1);
        }
        return content.Length > 40 ? content.Substring(0, 37) + "..." : content;
    }
    
    private void ClearQuote()
    {
        _quotedMessages.Clear();
        QuotePanel.Visibility = Visibility.Collapsed;
    }
    
    private void CancelQuote_Click(object sender, RoutedEventArgs e)
    {
        ClearQuote();
    }
    
    private void ScrollToBottom()
    {
        MessagesScrollViewer.ScrollToEnd();
    }
    
    private void EmotesButton_Click(object sender, RoutedEventArgs e)
    {
        EmotesPopup.IsOpen = !EmotesPopup.IsOpen;
    }
    
    private async void AttachFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentChat == null) return;
        
        var dialog = new OpenFileDialog
        {
            Title = "Выберите файл",
            Filter = "Все файлы (*.*)|*.*|Изображения|*.jpg;*.jpeg;*.png;*.gif;*.webp;*.bmp|Видео|*.mp4;*.webm;*.avi;*.mkv;*.mov|Аудио|*.mp3;*.wav;*.ogg;*.flac"
        };
        
        if (dialog.ShowDialog() == true)
        {
            var fileInfo = new FileInfo(dialog.FileName);
            
            // Ограничение 1GB
            if (fileInfo.Length > 1024L * 1024 * 1024)
            {
                MessageBox.Show("Файл слишком большой. Максимум 1 ГБ", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var fileService = Services.FileTransferService.Instance;
            long targetUserId = 0;
            long chatId = 0;
            
            // Для личных чатов с друзьями - ВСЕГДА устанавливаем targetUserId
            if (_currentChat.IsFriend && _currentChat.Friend != null)
            {
                targetUserId = _currentChat.Friend.Id;
            }
            
            // ChatId если есть
            if (_currentChat.ChatId.HasValue)
            {
                chatId = _currentChat.ChatId.Value;
            }
            
            // Показываем прогресс
            ShowProgress();
            fileService.TransferProgress += OnTransferProgress;
            
            var success = await fileService.UploadFileAsync(dialog.FileName, targetUserId, chatId);
            
            fileService.TransferProgress -= OnTransferProgress;
            HideProgress();
            
            if (success)
            {
                // Добавляем сообщение в чат как отправленный файл
                var fileType = GetFileType(dialog.FileName);
                var base64 = Convert.ToBase64String(await File.ReadAllBytesAsync(dialog.FileName));
                // Сохраняем копию в Downloads для отправителя тоже
                var savedPath = fileService.SaveToDownloads(fileInfo.Name, await File.ReadAllBytesAsync(dialog.FileName));
                // Формат: [FILE|имя|тип|путь|base64]
                var content = $"[FILE|{fileInfo.Name}|{fileType}|{savedPath}|{base64}]";
                
                // Используем ChatId напрямую (10-миллиардное число) для всех чатов
                var dbChatId = chatId;
                
                var msg = new Message
                {
                    Id = DateTime.UtcNow.Ticks,
                    ChatId = dbChatId,
                    SenderId = App.CurrentUser!.Id,
                    SenderName = App.CurrentUser.DisplayName,
                    Content = content,
                    Type = fileType == "image" ? MessageType.Image : MessageType.File,
                    Timestamp = DateTime.UtcNow,
                    Status = MessageStatus.Sent
                };
                
                _messages.Add(new MessageViewModel(msg, App.CurrentUser.Id));
                ScrollToBottom();
                
                // Сохраняем в локальную БД
                LocalDatabaseService.Instance.SaveMessage(msg);
            }
            else
            {
                var error = fileService.LastError ?? "Неизвестная ошибка";
                MessageBox.Show($"Не удалось отправить файл:\n{error}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
    
    private void ShowProgress()
    {
        SendButton.Visibility = Visibility.Collapsed;
        ProgressGrid.Visibility = Visibility.Visible;
        UpdateProgressArc(0);
    }
    
    private void HideProgress()
    {
        SendButton.Visibility = Visibility.Visible;
        ProgressGrid.Visibility = Visibility.Collapsed;
    }
    
    private void OnTransferProgress(object? sender, double percent)
    {
        Dispatcher.Invoke(() => UpdateProgressArc(percent));
    }
    
    private void UpdateProgressArc(double percent)
    {
        ProgressText.Text = $"{(int)percent}%";
        
        // Центр круга (20, 20), радиус 18
        double angle = percent / 100.0 * 360;
        double radians = (angle - 90) * Math.PI / 180;
        
        double x = 20 + 18 * Math.Cos(radians);
        double y = 20 + 18 * Math.Sin(radians);
        
        ProgressArcSegment.IsLargeArc = angle > 180;
        ProgressArcSegment.Point = new Point(x, y);
        
        // Начальная точка сверху
        ProgressFigure.StartPoint = new Point(20, 2);
    }
    
    private string GetFileType(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" => "image",
            ".mp4" or ".webm" or ".avi" or ".mkv" or ".mov" => "video",
            ".mp3" or ".wav" or ".ogg" or ".flac" => "audio",
            _ => "file"
        };
    }
    
    /// <summary>
    /// Восстанавливает путь к локальному файлу если он существует
    /// </summary>
    private string RestoreLocalFilePath(string content)
    {
        // Формат: [FILE|имя|тип|путь|base64]
        try
        {
            var parts = content.Split('|');
            if (parts.Length >= 4)
            {
                var fileName = parts[1];
                var fileType = parts[2];
                var filePath = parts[3];
                
                // Если путь пустой или файл не существует - ищем в Downloads
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    var downloadsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
                    if (Directory.Exists(downloadsPath))
                    {
                        // Ищем файл по имени (без уникального префикса)
                        var files = Directory.GetFiles(downloadsPath, $"*_{fileName}");
                        if (files.Length > 0)
                        {
                            // Берём самый свежий
                            var latestFile = files.OrderByDescending(f => new FileInfo(f).CreationTime).First();
                            filePath = latestFile;
                            
                            // Обновляем content с правильным путём
                            if (parts.Length >= 5)
                            {
                                return $"[FILE|{fileName}|{fileType}|{filePath}|{parts[4]}";
                            }
                            return $"[FILE|{fileName}|{fileType}|{filePath}|]";
                        }
                    }
                }
            }
        }
        catch { }
        
        return content;
    }
    
    private void NewChatButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewChatWindow();
        if (dialog.ShowDialog() == true)
        {
            LoadChats();
        }
    }
    
    private async void FriendsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new FriendsWindow();
        dialog.Owner = this;
        dialog.ShowDialog();
        
        // Обновляем список после закрытия окна друзей
        await LoadFriends();
        await LoadChats();
        
        // Если выбран друг для чата - открываем чат с ним
        if (dialog.SelectedFriend != null)
        {
            var friendItem = _allItems.FirstOrDefault(x => x.IsFriend && x.Friend?.Id == dialog.SelectedFriend.Id);
            if (friendItem != null)
            {
                ChatsList.SelectedItem = friendItem;
            }
        }
    }
    
    private void ChatMembersButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentChat == null) return;
        
        var dialog = new ChatMembersWindow(_currentChat.Chat);
        dialog.ShowDialog();
    }
    
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow();
        settingsWindow.Owner = this;
        settingsWindow.ShowDialog();
    }
    
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshList();
    }
    
    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            // Закрываем локальную БД
            LocalDatabaseService.Instance.Close();
            
            await App.NetworkClient!.SendAsync(new Packet(PacketType.Logout));
            App.NetworkClient.Disconnect();
        }
        catch { }
    }
}

public class ChatViewModel
{
    public Chat? Chat { get; }
    public User? Friend { get; }
    public Chat? PrivateChat { get; }
    
    public bool IsGroupChat => Chat != null && Chat.Type == ChatType.Group;
    public bool IsFriend => Friend != null;
    public bool IsOnline => Friend?.Status == UserStatus.Online;
    
    // Для получения ID чата при отправке сообщений
    public long? ChatId => Chat?.Id ?? PrivateChat?.Id;
    
    public string DisplayName
    {
        get
        {
            if (IsFriend) return Friend!.DisplayName;
            if (IsGroupChat) return Chat!.Name;
            // Приватный чат без друга
            return "Чат";
        }
    }
    
    public string AvatarLetter => DisplayName.Length > 0 ? DisplayName[0].ToString().ToUpper() : "?";
    
    private string? _lastMessagePreview;
    public string LastMessagePreview
    {
        get
        {
            if (_lastMessagePreview != null) return _lastMessagePreview;
            
            // Для друга - показываем последнее сообщение из приватного чата или статус
            if (IsFriend)
            {
                if (PrivateChat?.LastMessage != null)
                {
                    var content = PrivateChat.LastMessage.Content;
                    if (content.Length > 30) content = content.Substring(0, 30) + "...";
                    return content;
                }
                return IsOnline ? "В сети" : "Не в сети";
            }
            // Для группового чата
            var chatContent = Chat?.LastMessage?.Content ?? "";
            if (chatContent.Length > 30) chatContent = chatContent.Substring(0, 30) + "...";
            return chatContent;
        }
        set => _lastMessagePreview = value;
    }
    
    public DateTime? LastMessageTime { get; set; }
    
    private int? _unreadCount;
    public int UnreadCount
    {
        get => _unreadCount ?? PrivateChat?.UnreadCount ?? Chat?.UnreadCount ?? 0;
        set => _unreadCount = value;
    }
    public Visibility UnreadVisibility => UnreadCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    
    public Brush StatusIndicator => IsFriend 
        ? (IsOnline ? new SolidColorBrush(Color.FromRgb(76, 175, 80)) : new SolidColorBrush(Color.FromRgb(117, 117, 117)))
        : Brushes.Transparent;
    public Visibility StatusVisibility => IsFriend ? Visibility.Visible : Visibility.Collapsed;
    
    // Групповой чат
    public ChatViewModel(Chat chat)
    {
        Chat = chat;
    }
    
    // Друг с возможным приватным чатом
    public ChatViewModel(User friend, Chat? privateChat = null)
    {
        Friend = friend;
        PrivateChat = privateChat;
    }
}

public class MessageViewModel
{
    public Message Message { get; }
    private readonly long _currentUserId;
    
    public string SenderName => Message.SenderName;
    public string Content => Message.Content;
    public string TimeString => Message.Timestamp.ToLocalTime().ToString("HH:mm");
    public bool IsOwn => Message.SenderId == _currentUserId;
    public HorizontalAlignment Alignment => IsOwn ? HorizontalAlignment.Right : HorizontalAlignment.Left;
    public Brush Background
    {
        get
        {
            if (IsOwn)
            {
                // Свои сообщения
                return Application.Current.Resources["OwnMessageBrush"] as Brush ?? 
                       new SolidColorBrush(Color.FromRgb(45, 90, 39));
            }
            else
            {
                // Чужие сообщения
                return Application.Current.Resources["OtherMessageBrush"] as Brush ?? 
                       new SolidColorBrush(Color.FromRgb(61, 61, 61));
            }
        }
    }
    public Visibility SenderVisibility => IsOwn ? Visibility.Collapsed : Visibility.Visible;
    
    // Статус сообщения (галочки) - только для своих сообщений
    public string StatusIcon => IsOwn ? Message.Status switch
    {
        MessageStatus.Sending => "◌",      // Отправляется
        MessageStatus.Sent => "✓",         // Одна галочка - отправлено
        MessageStatus.Delivered => "✓",    // Доставлено
        MessageStatus.Read => "✓✓",        // Две галочки - прочитано
        _ => "✓"
    } : "";
    
    public Visibility StatusVisibility => IsOwn ? Visibility.Visible : Visibility.Collapsed;
    
    public Brush StatusColor => Message.Status == MessageStatus.Read 
        ? new SolidColorBrush(Color.FromRgb(52, 183, 241))  // Синий для прочитанных
        : (Application.Current.Resources["TextSecondaryBrush"] as Brush ?? Brushes.Gray);
    
    public MessageViewModel(Message message, long currentUserId)
    {
        Message = message;
        _currentUserId = currentUserId;
    }
}

