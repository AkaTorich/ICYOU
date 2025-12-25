using System.Collections.ObjectModel;
using ICYOU.Core.Protocol;
using ICYOU.Mobile.Services;
using ICYOU.Mobile.ViewModels;
using ICYOU.SDK;

namespace ICYOU.Mobile.Pages;

[QueryProperty(nameof(ChatViewModel), "ChatViewModel")]
public partial class ChatPage : ContentPage
{
    private readonly ObservableCollection<MessageViewModel> _messages = new();
    private ChatViewModel? _chatViewModel;
    private long _chatId;
    private MessageViewModel? _replyToMessage;

    public ChatViewModel? ChatViewModel
    {
        get => _chatViewModel;
        set
        {
            _chatViewModel = value;
            if (_chatViewModel != null)
            {
                Title = _chatViewModel.DisplayName;
                _chatId = _chatViewModel.ChatId;
                LoadMessages();
            }
        }
    }

    public ChatPage()
    {
        InitializeComponent();
        MessagesList.ItemsSource = _messages;

        // Подписка на новые сообщения
        if (AppState.NetworkClient != null)
        {
            AppState.NetworkClient.PacketReceived += OnPacketReceived;
        }
    }

    private async void LoadMessages()
    {
        // Проверяем что клиент подключен
        if (AppState.NetworkClient == null)
        {
            var localMessages = LocalDatabaseService.Instance.GetMessages(_chatId);
            _messages.Clear();
            foreach (var msg in localMessages.OrderBy(m => m.Timestamp))
            {
                // Обрабатываем сообщение через модули
                var processedMsg = ModuleManager.Instance.ProcessIncomingMessage(msg) ?? msg;
                _messages.Add(new MessageViewModel(processedMsg));
            }
            return;
        }

        if (_chatId == 0)
        {
            // Если чата еще нет (только друг), проверяем есть ли сообщения локально
            var localMessages = LocalDatabaseService.Instance.GetMessages(_chatId);
            _messages.Clear();
            foreach (var msg in localMessages.OrderBy(m => m.Timestamp))
            {
                // Обрабатываем сообщение через модули
                var processedMsg = ModuleManager.Instance.ProcessIncomingMessage(msg) ?? msg;
                _messages.Add(new MessageViewModel(processedMsg));
            }
            return;
        }

        try
        {
            // Загружаем историю с сервера
            var response = await AppState.NetworkClient.SendAndWaitAsync(new Packet(PacketType.GetChatHistory, new GetChatHistoryData
            {
                ChatId = _chatId,
                Count = 50
            }));

            if (response?.Type == PacketType.ChatHistoryResponse)
            {
                var data = response.GetData<ChatHistoryResponseData>();
                if (data != null)
                {
                    _messages.Clear();
                    foreach (var msg in data.Messages.OrderBy(m => m.Timestamp))
                    {
                        // Обрабатываем сообщение через модули
                        var processedMsg = ModuleManager.Instance.ProcessIncomingMessage(msg) ?? msg;
                        _messages.Add(new MessageViewModel(processedMsg));
                        // Сохраняем в локальную БД
                        LocalDatabaseService.Instance.SaveMessage(processedMsg);
                    }

                    // Прокручиваем вниз
                    if (_messages.Count > 0)
                    {
                        MessagesList.ScrollTo(_messages.Last(), position: ScrollToPosition.End, animate: false);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[ChatPage] LoadMessages error: {ex.Message}");

            // Загружаем из локальной БД
            var localMessages = LocalDatabaseService.Instance.GetMessages(_chatId);
            _messages.Clear();
            foreach (var msg in localMessages.OrderBy(m => m.Timestamp))
            {
                // Обрабатываем сообщение через модули
                var processedMsg = ModuleManager.Instance.ProcessIncomingMessage(msg) ?? msg;
                _messages.Add(new MessageViewModel(processedMsg));
            }
        }
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        await SendMessage();
    }

    private void OnMessageDoubleTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is MessageViewModel messageViewModel)
        {
            ShowReplyPreview(messageViewModel);
        }
    }

    private void OnCancelReplyClicked(object sender, EventArgs e)
    {
        HideReplyPreview();
    }

    private void ShowReplyPreview(MessageViewModel message)
    {
        _replyToMessage = message;
        ReplyToSenderLabel.Text = message.SenderName;
        ReplyToContentLabel.Text = message.Content;
        ReplyPreviewPanel.IsVisible = true;
        MessageInput.Focus();
    }

    private void HideReplyPreview()
    {
        _replyToMessage = null;
        ReplyPreviewPanel.IsVisible = false;
    }

    private async Task SendMessage()
    {
        var content = MessageInput.Text?.Trim();
        if (string.IsNullOrEmpty(content))
            return;

        // Проверяем что клиент подключен и пользователь залогинен
        if (AppState.NetworkClient == null || AppState.CurrentUser == null)
        {
            ShowStatus("Нет подключения к серверу", false);
            return;
        }

        // Если это цитата, форматируем
        if (_replyToMessage != null)
        {
            content = CreateQuote(
                _replyToMessage.Message.Id,
                _replyToMessage.SenderName,
                _replyToMessage.Content,
                content
            );
            HideReplyPreview();
        }

        MessageInput.Text = string.Empty;

        try
        {
            // Если чата еще нет, создаем его
            if (_chatId == 0 && _chatViewModel?.Friend != null)
            {
                var createResponse = await AppState.NetworkClient.SendAndWaitAsync(new Packet(PacketType.CreateChat, new CreateChatData
                {
                    Name = _chatViewModel.Friend.DisplayName,
                    MemberIds = new List<long> { _chatViewModel.Friend.Id }
                }));

                if (createResponse?.Type == PacketType.CreateChatResponse)
                {
                    var chat = createResponse.GetData<Chat>();
                    if (chat != null)
                    {
                        _chatId = chat.Id;
                    }
                }
            }

            if (_chatId == 0)
            {
                ShowStatus("Не удалось создать чат", false);
                return;
            }

            // Создаем сообщение
            var message = new Message
            {
                ChatId = _chatId,
                SenderId = AppState.CurrentUser.Id,
                SenderName = AppState.CurrentUser.DisplayName,
                Content = content,
                Timestamp = DateTime.Now,
                Status = MessageStatus.Sending
            };

            // Добавляем в список сразу
            var viewModel = new MessageViewModel(message);
            _messages.Add(viewModel);

            // Прокручиваем вниз
            MessagesList.ScrollTo(viewModel, position: ScrollToPosition.End, animate: true);

            // Отправляем на сервер
            var packet = new Packet(PacketType.SendMessage, new SendMessageData
            {
                ChatId = _chatId,
                Content = content
            });

            await AppState.NetworkClient.SendAsync(packet);

            // Сохраняем в локальную БД
            LocalDatabaseService.Instance.SaveMessage(message);
        }
        catch (Exception ex)
        {
            ShowStatus($"Ошибка отправки: {ex.Message}", false);
            DebugLog.Write($"[ChatPage] SendMessage error: {ex}");
        }
    }

    private async void OnAttachFileClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync();
            if (result != null)
            {
                ShowStatus("Отправка файлов пока не поддерживается", false);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Ошибка выбора файла: {ex.Message}", false);
        }
    }

    private void OnPacketReceived(object? sender, Packet packet)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            switch (packet.Type)
            {
                case PacketType.MessageReceived:
                    var message = packet.GetData<Message>();
                    if (message != null && message.ChatId == _chatId)
                    {
                        // Обрабатываем сообщение через модули
                        var processedMessage = ModuleManager.Instance.ProcessIncomingMessage(message) ?? message;

                        // Проверяем, нет ли уже этого сообщения
                        if (!_messages.Any(m => m.Message.Id == processedMessage.Id))
                        {
                            _messages.Add(new MessageViewModel(processedMessage));
                            LocalDatabaseService.Instance.SaveMessage(processedMessage);

                            // Прокручиваем вниз
                            if (_messages.Count > 0)
                            {
                                MessagesList.ScrollTo(_messages.Last(), position: ScrollToPosition.End, animate: true);
                            }
                        }
                    }
                    break;
            }
        });
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // Отписываемся от событий
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

    /// <summary>
    /// Создаёт строку цитаты для отправки
    /// Формат: [QUOTE|MessageId|SenderName|Content]ваш ответ
    /// </summary>
    private string CreateQuote(long messageId, string senderName, string content, string reply)
    {
        // Убираем переносы строк из цитируемого контента
        var sanitizedContent = content.Replace("\n", " ").Replace("\r", "");
        return $"[QUOTE|{messageId}|{senderName}|{sanitizedContent}]{reply}";
    }
}
