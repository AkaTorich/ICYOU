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

        // Загружаем смайлы из текущего пака
        LoadEmotes();

        // Подписка на новые сообщения
        if (AppState.NetworkClient != null)
        {
            AppState.NetworkClient.PacketReceived += OnPacketReceived;
        }
    }

    private void LoadEmotes()
    {
        try
        {
            var settings = SettingsService.Instance.Settings;
            var packName = settings.EmotePack;
            EmoteService.Instance.LoadEmotes(packName);
            DebugLog.Write($"[ChatPage] Emotes loaded from pack: {packName ?? "(По умолчанию)"}");
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[ChatPage] Error loading emotes: {ex.Message}");
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
        // Обработка превью ссылок
        if (content.Contains("[LINKPREVIEW|"))
        {
            var previewStart = content.IndexOf("[LINKPREVIEW|");
            var previewEnd = content.IndexOf("]", previewStart);
            if (previewEnd > previewStart)
            {
                var before = previewStart > 0 ? content.Substring(0, previewStart).Trim() : "";
                var after = previewEnd + 1 < content.Length ? content.Substring(previewEnd + 1).TrimStart() : "";

                // Если есть текст до превью - используем его
                if (!string.IsNullOrEmpty(before))
                {
                    content = before;
                }
                // Если есть текст после - используем его
                else if (!string.IsNullOrEmpty(after))
                {
                    content = after;
                }
                // Иначе берем title из превью
                else
                {
                    var previewData = content.Substring(previewStart + 13, previewEnd - previewStart - 13);
                    var parts = previewData.Split('|');
                    if (parts.Length >= 2)
                        content = "🔗 " + parts[1].Replace("{{PIPE}}", "|"); // 🔗 title
                }
            }
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
                // Убираем превью ссылок
                if (quotedContent.Contains("[LINKPREVIEW|"))
                {
                    var previewStart = quotedContent.IndexOf("[LINKPREVIEW|");
                    var previewEnd = quotedContent.IndexOf("]", previewStart);
                    if (previewEnd > previewStart)
                    {
                        var before = previewStart > 0 ? quotedContent.Substring(0, previewStart).Trim() : "";
                        var after = previewEnd + 1 < quotedContent.Length ? quotedContent.Substring(previewEnd + 1).TrimStart() : "";

                        if (!string.IsNullOrEmpty(before))
                        {
                            quotedContent = before;
                        }
                        else if (!string.IsNullOrEmpty(after))
                        {
                            quotedContent = after;
                        }
                        else
                        {
                            var previewData = quotedContent.Substring(previewStart + 13, previewEnd - previewStart - 13);
                            var parts = previewData.Split('|');
                            if (parts.Length >= 2)
                                quotedContent = "🔗 " + parts[1].Replace("{{PIPE}}", "|");
                        }
                    }
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
            // Проверяем, что есть чат или друг
            if (_chatId == 0 && _chatViewModel?.Friend == null)
            {
                ShowStatus("Сначала выберите чат или друга", false);
                return;
            }

            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Выберите файл"
            });

            if (result == null)
                return;

            // Копируем файл во временную папку для получения размера
            var cacheDir = FileSystem.CacheDirectory;
            var tempFilePath = Path.Combine(cacheDir, result.FileName);

            using (var stream = await result.OpenReadAsync())
            using (var fileStream = File.Create(tempFilePath))
            {
                await stream.CopyToAsync(fileStream);
            }

            var fileInfo = new FileInfo(tempFilePath);

            // Ограничение 1GB
            if (fileInfo.Length > 1024L * 1024 * 1024)
            {
                ShowStatus("Файл слишком большой. Максимум 1 ГБ", false);
                File.Delete(tempFilePath);
                return;
            }

            DebugLog.Write($"[ChatPage] Отправка файла: {fileInfo.Name} ({fileInfo.Length} байт)");

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
                File.Delete(tempFilePath);
                return;
            }

            long targetUserId = 0;
            if (_chatViewModel?.Friend != null)
            {
                targetUserId = _chatViewModel.Friend.Id;
            }

            // Показываем прогресс
            ShowStatus("Отправка файла...", true);

            var fileService = FileTransferService.Instance;
            var success = await fileService.UploadFileAsync(tempFilePath, targetUserId, _chatId);

            if (success)
            {
                // Создаем сообщение с файлом
                var fileType = fileService.GetFileType(fileInfo.Name);
                var fileData = await File.ReadAllBytesAsync(tempFilePath);
                var base64 = Convert.ToBase64String(fileData);

                // Сохраняем копию в AppData для отправителя
                var savedPath = fileService.SaveToAppData(fileInfo.Name, fileData);

                // Формат: [FILE|имя|тип|путь|base64]
                var content = $"[FILE|{fileInfo.Name}|{fileType}|{savedPath}|{base64}]";

                var message = new Message
                {
                    Id = DateTime.UtcNow.Ticks,
                    ChatId = _chatId,
                    SenderId = AppState.CurrentUser.Id,
                    SenderName = AppState.CurrentUser.DisplayName,
                    Content = content,
                    Type = fileType == "image" ? MessageType.Image : MessageType.File,
                    Timestamp = DateTime.UtcNow,
                    Status = MessageStatus.Sent
                };

                // Добавляем в UI
                var viewModel = new MessageViewModel(message);
                _messages.Add(viewModel);
                MessagesList.ScrollTo(viewModel, position: ScrollToPosition.End, animate: true);

                // Сохраняем в БД
                LocalDatabaseService.Instance.SaveMessage(message);

                ShowStatus("Файл отправлен успешно", true);
                DebugLog.Write($"[ChatPage] Файл {fileInfo.Name} отправлен успешно");
            }
            else
            {
                var error = fileService.LastError ?? "Неизвестная ошибка";
                ShowStatus($"Не удалось отправить файл: {error}", false);
                DebugLog.Write($"[ChatPage] Ошибка отправки файла: {error}");
            }

            // Удаляем временный файл
            File.Delete(tempFilePath);
        }
        catch (Exception ex)
        {
            ShowStatus($"Ошибка отправки файла: {ex.Message}", false);
            DebugLog.Write($"[ChatPage] OnAttachFileClicked error: {ex}");
        }
    }

    private void OnEmotesButtonClicked(object sender, EventArgs e)
    {
        try
        {
            if (EmotesPanel.IsVisible)
            {
                EmotesPanel.IsVisible = false;
            }
            else
            {
                LoadEmotesToPanel();
                EmotesPanel.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[ChatPage] Error toggling emotes panel: {ex.Message}");
            ShowStatus($"Ошибка открытия смайлов: {ex.Message}", false);
        }
    }

    private void OnEmotesPanelBackgroundTapped(object sender, EventArgs e)
    {
        EmotesPanel.IsVisible = false;
    }

    private void LoadEmotesToPanel()
    {
        try
        {
            EmotesFlexLayout.Children.Clear();

            var emoteService = EmoteService.Instance;
            var emotes = emoteService.Emotes;

            if (emotes.Count == 0)
            {
                var noEmotesLabel = new Label
                {
                    Text = "Смайлы не найдены",
                    FontSize = 14,
                    TextColor = Colors.Gray,
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 20, 0, 0)
                };
                EmotesFlexLayout.Children.Add(noEmotesLabel);
                return;
            }

            foreach (var kvp in emotes)
            {
                var code = kvp.Key;
                var path = kvp.Value;

                try
                {
                    var emoteImage = emoteService.GetEmoteImage(code);
                    if (emoteImage != null)
                    {
                        // Используем Image с анимацией внутри Frame
                        var image = new Image
                        {
                            Source = emoteImage,
                            WidthRequest = 40,
                            HeightRequest = 40,
                            Aspect = Aspect.AspectFit,
                            IsAnimationPlaying = true
                        };

                        var frame = new Frame
                        {
                            Content = image,
                            WidthRequest = 50,
                            HeightRequest = 50,
                            Padding = 5,
                            Margin = new Thickness(5),
                            BackgroundColor = Colors.Transparent,
                            BorderColor = Colors.LightGray,
                            CornerRadius = 8,
                            HasShadow = false
                        };

                        var tapGesture = new TapGestureRecognizer();
                        tapGesture.Tapped += (s, e) =>
                        {
                            MessageInput.Text += code;
                            EmotesPanel.IsVisible = false;
                            MessageInput.Focus();
                        };
                        frame.GestureRecognizers.Add(tapGesture);

                        EmotesFlexLayout.Children.Add(frame);
                    }
                    else
                    {
                        // Используем обычный Button для текстовых кодов (если изображение не загрузилось)
                        var button = new Button
                        {
                            Text = code,
                            FontSize = 10,
                            WidthRequest = 50,
                            HeightRequest = 50,
                            Padding = 0,
                            Margin = new Thickness(5),
                            BackgroundColor = Colors.Transparent,
                            BorderColor = Colors.LightGray,
                            BorderWidth = 1,
                            CornerRadius = 8
                        };

                        button.Clicked += (s, e) =>
                        {
                            MessageInput.Text += code;
                            EmotesPanel.IsVisible = false;
                            MessageInput.Focus();
                        };

                        EmotesFlexLayout.Children.Add(button);
                    }
                }
                catch
                {
                    // В случае ошибки используем текстовый Button
                    var button = new Button
                    {
                        Text = code,
                        FontSize = 10,
                        WidthRequest = 50,
                        HeightRequest = 50,
                        Padding = 0,
                        Margin = new Thickness(5),
                        BackgroundColor = Colors.Transparent,
                        BorderColor = Colors.LightGray,
                        BorderWidth = 1,
                        CornerRadius = 8
                    };

                    button.Clicked += (s, e) =>
                    {
                        MessageInput.Text += code;
                        EmotesPanel.IsVisible = false;
                        MessageInput.Focus();
                    };

                    EmotesFlexLayout.Children.Add(button);
                }
            }

            DebugLog.Write($"[ChatPage] Loaded {emotes.Count} emotes to panel");
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[ChatPage] Error loading emotes to panel: {ex.Message}");
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

                case PacketType.FileAvailable:
                    HandleFileAvailable(packet);
                    break;
            }
        });
    }

    private async void HandleFileAvailable(Packet packet)
    {
        var data = packet.GetData<FileNotificationData>();
        if (data == null)
        {
            DebugLog.Write("[ChatPage] FileAvailable: data is null");
            return;
        }

        // Проверяем, что файл для текущего чата
        if (data.ChatId != _chatId)
        {
            DebugLog.Write($"[ChatPage] FileAvailable: не наш чат (data.ChatId={data.ChatId}, _chatId={_chatId})");
            return;
        }

        DebugLog.Write($"[ChatPage] Получен файл: {data.FileName} от {data.SenderName}");

        try
        {
            ShowStatus("Скачивание файла...", true);

            // Скачиваем файл с сервера
            var fileService = FileTransferService.Instance;
            var (fileName, fileData) = await fileService.DownloadFileAsync(data.FileId);

            if (fileData == null || fileName == null)
            {
                ShowStatus("Не удалось скачать файл", false);
                DebugLog.Write("[ChatPage] Не удалось скачать файл");
                return;
            }

            // Сохраняем файл в AppData
            var savedPath = fileService.SaveToAppData(fileName, fileData);

            // Сохраняем информацию о файле в локальную БД
            LocalDatabaseService.Instance.SaveFile(
                data.FileId,
                0, // messageId будет позже
                data.ChatId,
                fileName,
                data.FileType,
                savedPath,
                data.FileSize);

            // Создаем сообщение с файлом
            var base64 = Convert.ToBase64String(fileData);
            var content = $"[FILE|{fileName}|{data.FileType}|{savedPath}|{base64}]";
            var msgType = data.FileType == "image" ? MessageType.Image : MessageType.File;

            var message = new Message
            {
                Id = DateTime.UtcNow.Ticks,
                ChatId = data.ChatId,
                SenderId = data.SenderId,
                SenderName = data.SenderName,
                Content = content,
                Type = msgType,
                Timestamp = DateTime.UtcNow,
                Status = MessageStatus.Sent
            };

            // Сохраняем в БД
            LocalDatabaseService.Instance.SaveMessage(message);

            // Добавляем в UI
            var viewModel = new MessageViewModel(message);
            _messages.Add(viewModel);
            MessagesList.ScrollTo(viewModel, position: ScrollToPosition.End, animate: true);

            ShowStatus($"Файл {fileName} получен", true);
            DebugLog.Write($"[ChatPage] Файл {fileName} получен и сохранен успешно");
        }
        catch (Exception ex)
        {
            ShowStatus($"Ошибка получения файла: {ex.Message}", false);
            DebugLog.Write($"[ChatPage] HandleFileAvailable error: {ex}");
        }
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

    private async void OnFileImageTapped(object sender, EventArgs e)
    {
        try
        {
            if (sender is Image image && image.BindingContext is MessageViewModel viewModel)
            {
                await OpenFile(viewModel);
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[ChatPage] OnFileImageTapped error: {ex}");
            ShowStatus($"Ошибка открытия изображения: {ex.Message}", false);
        }
    }

    private async void OnFileFrameTapped(object sender, EventArgs e)
    {
        try
        {
            if (sender is Frame frame && frame.BindingContext is MessageViewModel viewModel)
            {
                await OpenFile(viewModel);
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[ChatPage] OnFileFrameTapped error: {ex}");
            ShowStatus($"Ошибка открытия файла: {ex.Message}", false);
        }
    }

    private async Task OpenFile(MessageViewModel viewModel)
    {
        try
        {
            if (!viewModel.HasFile || string.IsNullOrEmpty(viewModel.FilePath))
            {
                ShowStatus("Файл не найден", false);
                return;
            }

            if (!File.Exists(viewModel.FilePath))
            {
                ShowStatus("Файл не найден на устройстве", false);
                DebugLog.Write($"[ChatPage] File not found: {viewModel.FilePath}");
                return;
            }

            DebugLog.Write($"[ChatPage] Opening file: {viewModel.FilePath}");

            // Используем Launcher для открытия файла
            await Launcher.OpenAsync(new OpenFileRequest
            {
                File = new ReadOnlyFile(viewModel.FilePath)
            });
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[ChatPage] OpenFile error: {ex}");
            ShowStatus($"Не удалось открыть файл: {ex.Message}", false);
        }
    }

    private async void OnSaveFileTapped(object sender, EventArgs e)
    {
        try
        {
            if (sender is Button button && button.CommandParameter is MessageViewModel viewModel)
            {
                await SaveFile(viewModel);
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[ChatPage] OnSaveFileTapped error: {ex}");
            ShowStatus($"Ошибка сохранения файла: {ex.Message}", false);
        }
    }

    private async Task SaveFile(MessageViewModel viewModel)
    {
        try
        {
            if (!viewModel.HasFile || viewModel.FileData == null || viewModel.FileData.Length == 0)
            {
                ShowStatus("Нет данных файла для сохранения", false);
                return;
            }

            DebugLog.Write($"[ChatPage] Saving file: {viewModel.FileName}");

#if ANDROID
            // Android - сохраняем напрямую в Downloads или Pictures
            await SaveFileAndroid(viewModel);
#elif IOS
            // iOS - сохраняем в Photos или Files
            await SaveFileIOS(viewModel);
#else
            // Fallback для других платформ
            var tempPath = Path.Combine(FileSystem.CacheDirectory, viewModel.FileName);
            await File.WriteAllBytesAsync(tempPath, viewModel.FileData);
            await Share.RequestAsync(new ShareFileRequest
            {
                Title = "Сохранить файл",
                File = new ShareFile(tempPath)
            });
#endif
        }
        catch (PermissionException pex)
        {
            DebugLog.Write($"[ChatPage] Permission error: {pex.Message}");
            ShowStatus("Нет разрешения на сохранение файлов. Проверьте настройки приложения", false);
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[ChatPage] SaveFile error: {ex}");
            ShowStatus($"Не удалось сохранить файл: {ex.Message}", false);
        }
    }

#if ANDROID
    private async Task SaveFileAndroid(MessageViewModel viewModel)
    {
        var context = Android.App.Application.Context;

        if (viewModel.IsImage)
        {
            // Сохраняем изображение в галерею
            var contentValues = new Android.Content.ContentValues();
            contentValues.Put(Android.Provider.MediaStore.IMediaColumns.DisplayName, viewModel.FileName);
            contentValues.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, GetMimeType(viewModel.FileName));
            contentValues.Put(Android.Provider.MediaStore.IMediaColumns.RelativePath, Android.OS.Environment.DirectoryPictures + "/ICYOU");

            var resolver = context.ContentResolver;
            var imageUri = resolver?.Insert(Android.Provider.MediaStore.Images.Media.ExternalContentUri!, contentValues);

            if (imageUri != null && resolver != null)
            {
                using var outputStream = resolver.OpenOutputStream(imageUri);
                if (outputStream != null)
                {
                    await outputStream.WriteAsync(viewModel.FileData, 0, viewModel.FileData.Length);
                    ShowStatus($"Изображение сохранено в галерею", true);
                    DebugLog.Write($"[ChatPage] Image saved to gallery: {viewModel.FileName}");
                }
            }
        }
        else
        {
            // Сохраняем файл в Downloads
            var contentValues = new Android.Content.ContentValues();
            contentValues.Put(Android.Provider.MediaStore.IMediaColumns.DisplayName, viewModel.FileName);
            contentValues.Put(Android.Provider.MediaStore.IMediaColumns.MimeType, GetMimeType(viewModel.FileName));
            contentValues.Put(Android.Provider.MediaStore.IMediaColumns.RelativePath, Android.OS.Environment.DirectoryDownloads + "/ICYOU");

            var resolver = context.ContentResolver;
            var fileUri = resolver?.Insert(Android.Provider.MediaStore.Downloads.ExternalContentUri!, contentValues);

            if (fileUri != null && resolver != null)
            {
                using var outputStream = resolver.OpenOutputStream(fileUri);
                if (outputStream != null)
                {
                    await outputStream.WriteAsync(viewModel.FileData, 0, viewModel.FileData.Length);
                    ShowStatus($"Файл сохранён в Downloads/ICYOU", true);
                    DebugLog.Write($"[ChatPage] File saved to downloads: {viewModel.FileName}");
                }
            }
        }
    }

    private string GetMimeType(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            ".mp4" => "video/mp4",
            ".webm" => "video/webm",
            ".avi" => "video/avi",
            ".mkv" => "video/x-matroska",
            ".mov" => "video/quicktime",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".flac" => "audio/flac",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }
#endif

#if IOS
    private async Task SaveFileIOS(MessageViewModel viewModel)
    {
        if (viewModel.IsImage)
        {
            // Сохраняем изображение в Photos
            var image = UIKit.UIImage.LoadFromData(Foundation.NSData.FromArray(viewModel.FileData));
            if (image != null)
            {
                image.SaveToPhotosAlbum((img, error) =>
                {
                    if (error == null)
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            ShowStatus("Изображение сохранено в галерею", true);
                            DebugLog.Write($"[ChatPage] Image saved to photos: {viewModel.FileName}");
                        });
                    }
                    else
                    {
                        MainThread.BeginInvokeOnMainThread(() =>
                        {
                            ShowStatus($"Ошибка сохранения: {error.LocalizedDescription}", false);
                        });
                    }
                });
            }
        }
        else
        {
            // Для других файлов используем UIDocumentPickerViewController
            var tempPath = Path.Combine(FileSystem.CacheDirectory, viewModel.FileName);
            await File.WriteAllBytesAsync(tempPath, viewModel.FileData);

            var url = Foundation.NSUrl.FromFilename(tempPath);
            var documentPicker = new UIKit.UIDocumentPickerViewController(new[] { url }, UIKit.UIDocumentPickerMode.ExportToService);

            var viewController = Platform.GetCurrentUIViewController();
            if (viewController != null)
            {
                await viewController.PresentViewControllerAsync(documentPicker, true);
                ShowStatus("Выберите место для сохранения файла", true);
            }
        }
    }
#endif

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
