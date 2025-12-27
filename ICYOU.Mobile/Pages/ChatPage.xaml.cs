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
    private readonly List<MessageViewModel> _quotedMessages = new();
    private const int MaxQuotes = 3;

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
            DebugLog.Write($"[ChatPage] Загрузка {localMessages.Count} сообщений из локальной БД (нет сети)");
            foreach (var msg in localMessages.OrderBy(m => m.Timestamp))
            {
                // БД содержит уже обработанные сообщения - не обрабатываем повторно
                _messages.Add(new MessageViewModel(msg));
            }
            return;
        }

        if (_chatId == 0)
        {
            // Если чата еще нет (только друг), проверяем есть ли сообщения локально
            var localMessages = LocalDatabaseService.Instance.GetMessages(_chatId);
            _messages.Clear();
            DebugLog.Write($"[ChatPage] Загрузка {localMessages.Count} сообщений из локальной БД (chatId=0)");
            foreach (var msg in localMessages.OrderBy(m => m.Timestamp))
            {
                // БД содержит уже обработанные сообщения - не обрабатываем повторно
                _messages.Add(new MessageViewModel(msg));
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
                    DebugLog.Write($"[ChatPage] Получено {data.Messages.Count} сообщений с сервера (обычно 0 - сервер не хранит историю)");
                    foreach (var msg in data.Messages.OrderBy(m => m.Timestamp))
                    {
                        // Обрабатываем через модули для отображения
                        var processedMsg = ModuleManager.Instance.ProcessIncomingMessage(msg) ?? msg;
                        // Сохраняем ОБРАБОТАННОЕ сообщение в БД
                        LocalDatabaseService.Instance.SaveMessage(processedMsg);
                        _messages.Add(new MessageViewModel(processedMsg));
                    }

                    // Если сервер вернул пустую историю - загружаем из локальной БД
                    if (data.Messages.Count == 0)
                    {
                        DebugLog.Write($"[ChatPage] Сервер вернул пустую историю, загружаем из локальной БД");
                        var localMessages = LocalDatabaseService.Instance.GetMessages(_chatId);
                        foreach (var msg in localMessages.OrderBy(m => m.Timestamp))
                        {
                            // БД содержит уже обработанные сообщения - не обрабатываем повторно
                            _messages.Add(new MessageViewModel(msg));
                        }
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
            DebugLog.Write($"[ChatPage] Ошибка загрузки с сервера, загружаем {localMessages.Count} сообщений из локальной БД");
            foreach (var msg in localMessages.OrderBy(m => m.Timestamp))
            {
                // БД содержит уже обработанные сообщения - не обрабатываем повторно
                _messages.Add(new MessageViewModel(msg));
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

    private async void OnLinkPreviewTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is MessageViewModel messageViewModel && !string.IsNullOrEmpty(messageViewModel.LinkPreviewUrl))
        {
            try
            {
                await Launcher.OpenAsync(new Uri(messageViewModel.LinkPreviewUrl));
            }
            catch (Exception ex)
            {
                ShowStatus($"Ошибка открытия ссылки: {ex.Message}", false);
            }
        }
    }

    private void ShowReplyPreview(MessageViewModel message)
    {
        // Проверяем не добавлено ли уже это сообщение
        if (_quotedMessages.Any(q => q.Message.Id == message.Message.Id))
            return;

        // Если уже 3 цитаты - заменяем последнюю
        if (_quotedMessages.Count >= MaxQuotes)
        {
            _quotedMessages.RemoveAt(MaxQuotes - 1);
        }

        _quotedMessages.Add(message);
        UpdateQuotePanel();
        MessageInput.Focus();
    }

    private void HideReplyPreview()
    {
        _quotedMessages.Clear();
        ReplyPreviewPanel.IsVisible = false;
    }

    private void UpdateQuotePanel()
    {
        if (_quotedMessages.Count == 0)
        {
            ReplyPreviewPanel.IsVisible = false;
            return;
        }

        ReplyPreviewPanel.IsVisible = true;

        // Формируем текст для отображения
        if (_quotedMessages.Count == 1)
        {
            ReplyToSenderLabel.Text = _quotedMessages[0].SenderName;
            ReplyToContentLabel.Text = GetQuotePreview(_quotedMessages[0].Content);
        }
        else
        {
            ReplyToSenderLabel.Text = $"Цитаты ({_quotedMessages.Count})";
            var lines = new List<string>();
            foreach (var qm in _quotedMessages)
            {
                var content = GetQuotePreview(qm.Content);
                var preview = $"{qm.SenderName}: {content}";
                if (preview.Length > 50)
                    preview = preview.Substring(0, 47) + "...";
                lines.Add(preview);
            }
            ReplyToContentLabel.Text = string.Join("\n", lines);
        }
    }

    private string GetQuotePreview(string content)
    {
        // Убираем теги для превью
        if (content.StartsWith("[QUOTE|") || content.StartsWith("[QUOTES|"))
        {
            var endQuote = content.IndexOf(']');
            if (endQuote > 0)
                content = content.Substring(endQuote + 1);
        }
        if (content.Length > 40)
            return content.Substring(0, 37) + "...";
        return content;
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

        // Если есть цитируемые сообщения - добавляем формат цитат
        if (_quotedMessages.Count > 0)
        {
            var quoteParts = new List<string>();
            foreach (var qm in _quotedMessages)
            {
                var quotedContent = qm.Content;
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
                quoteParts.Add($"{qm.SenderName}~{quotedContent}");
            }
            // Формат: [QUOTES|quote1|quote2|quote3]текст (разделитель между цитатами |)
            content = $"[QUOTES|{string.Join("|", quoteParts)}]{content}";
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

            // Обрабатываем исходящее сообщение через модули ТОЛЬКО для локального отображения
            var processedMessage = ModuleManager.Instance.ProcessOutgoingMessage(message) ?? message;

            // Добавляем в UI обработанное сообщение (ViewModel сам распарсит RAW формат [QUOTE|...] и [LINKPREVIEW|...])
            var viewModel = new MessageViewModel(processedMessage);
            _messages.Add(viewModel);

            // Прокручиваем вниз
            MessagesList.ScrollTo(viewModel, position: ScrollToPosition.End, animate: true);

            // Отправляем на сервер ОРИГИНАЛЬНОЕ сообщение (без превью - другие клиенты обработают локально)
            var packet = new Packet(PacketType.SendMessage, new SendMessageData
            {
                ChatId = _chatId,
                Content = content
            });

            await AppState.NetworkClient.SendAsync(packet);

            // Сохраняем в локальную БД ОБРАБОТАННОЕ сообщение (как на Desktop)
            LocalDatabaseService.Instance.SaveMessage(processedMessage);
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
                        // Пропускаем свои собственные сообщения (они уже добавлены локально)
                        if (AppState.CurrentUser != null && message.SenderId == AppState.CurrentUser.Id)
                        {
                            DebugLog.Write($"[ChatPage] Пропускаем своё сообщение: {message.Content.Substring(0, Math.Min(50, message.Content.Length))}");
                            return;
                        }

                        var originalContent = message.Content;
                        DebugLog.Write($"[ChatPage] Получено новое сообщение: {originalContent.Substring(0, Math.Min(50, originalContent.Length))}");

                        // Обрабатываем сообщение через модули для отображения
                        var processedMessage = ModuleManager.Instance.ProcessIncomingMessage(message) ?? message;

                        if (processedMessage.Content != originalContent)
                        {
                            DebugLog.Write($"[ChatPage] Новое сообщение изменено модулями: '{processedMessage.Content.Substring(0, Math.Min(50, processedMessage.Content.Length))}'");
                        }
                        else
                        {
                            DebugLog.Write($"[ChatPage] Новое сообщение НЕ изменено модулями");
                        }

                        // Сохраняем ОБРАБОТАННОЕ сообщение в БД (как на Desktop)
                        // При загрузке из БД модули не нужно применять повторно
                        LocalDatabaseService.Instance.SaveMessage(processedMessage);

                        // Проверяем, нет ли уже этого сообщения
                        if (!_messages.Any(m => m.Message.Id == processedMessage.Id))
                        {
                            _messages.Add(new MessageViewModel(processedMessage));

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

}
