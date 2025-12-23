using System.Windows;
using System.Windows.Controls;
using ICYOU.Core.Protocol;
using ICYOU.SDK;
using ICYOU.Client.Services;

namespace ICYOU.Client.Views;

public partial class SettingsWindow : Window
{
    private bool _emotePackChanged = false;
    
    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettings();
    }
    
    private void LoadSettings()
    {
        // Загружаем текущие настройки
        DisplayNameBox.Text = App.CurrentUser?.DisplayName ?? "";
        
        // Устанавливаем статус
        var statusIndex = App.CurrentUser?.Status switch
        {
            UserStatus.Online => 0,
            UserStatus.Away => 1,
            UserStatus.DoNotDisturb => 2,
            _ => 0
        };
        StatusCombo.SelectedIndex = statusIndex;
        
        // Загружаем настройки из файла
        var settings = SettingsService.Instance.Settings;
        
        // Настройки уведомлений
        NotifyMessagesCheck.IsChecked = settings.NotifyMessages;
        NotifySoundsCheck.IsChecked = settings.NotifySounds;
        NotifyFriendsCheck.IsChecked = settings.NotifyFriends;
        
        // Тема
        var currentTheme = SettingsService.Instance.Settings.Theme;
        ThemeCombo.SelectedIndex = currentTheme == "Light" ? 1 : 0;
        
        // Паки смайлов
        LoadEmotePacks();
        
        // Модули
        LoadModules();
        
        // Шифрование БД
        LoadEncryptionSettings();
        
        ServerInfoText.Text = "Подключено";
    }
    
    private void LoadEncryptionSettings()
    {
        var db = LocalDatabaseService.Instance;
        DbEncryptionCheck.IsChecked = db.EncryptionEnabled || db.NeedsPassword;
        EncryptionPasswordPanel.Visibility = DbEncryptionCheck.IsChecked == true 
            ? Visibility.Visible : Visibility.Collapsed;
        
        if (db.EncryptionEnabled)
        {
            EncryptionStatusText.Text = "✓ Шифрование активно";
            EncryptionStatusText.Foreground = (System.Windows.Media.Brush)FindResource("OnlineBrush");
        }
        else if (db.NeedsPassword)
        {
            EncryptionStatusText.Text = "🔒 Требуется ввод пароля";
            EncryptionStatusText.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
        }
        else
        {
            EncryptionStatusText.Text = "";
        }
        
        // Обновляем размер кэша
        UpdateCacheSize();
    }
    
    private void UpdateCacheSize()
    {
        var db = LocalDatabaseService.Instance;
        var cacheSize = db.GetCacheSize();
        var filesCount = db.GetFilesCount();
        
        string sizeStr;
        if (cacheSize < 1024)
            sizeStr = $"{cacheSize} Б";
        else if (cacheSize < 1024 * 1024)
            sizeStr = $"{cacheSize / 1024.0:F1} КБ";
        else if (cacheSize < 1024 * 1024 * 1024)
            sizeStr = $"{cacheSize / (1024.0 * 1024):F1} МБ";
        else
            sizeStr = $"{cacheSize / (1024.0 * 1024 * 1024):F2} ГБ";
        
        CacheSizeText.Text = $"{sizeStr} ({filesCount} файлов)";
    }
    
    private void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Удалить все скачанные файлы из папки Downloads?\n\nСообщения останутся, но медиа нужно будет скачать заново.",
            "Очистка кэша",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
            
        if (result == MessageBoxResult.Yes)
        {
            LocalDatabaseService.Instance.ClearFileCache();
            UpdateCacheSize();
            MessageBox.Show("Кэш очищен!", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    
    private void ClearAllData_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "ВНИМАНИЕ!\n\nЭто удалит ВСЕ локальные данные:\n• История сообщений\n• Скачанные файлы\n• Список чатов\n\nПродолжить?",
            "Удаление всех данных",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
            
        if (result == MessageBoxResult.Yes)
        {
            var confirm = MessageBox.Show(
                "Вы уверены? Это действие нельзя отменить!",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Exclamation);
                
            if (confirm == MessageBoxResult.Yes)
            {
                LocalDatabaseService.Instance.ClearAllData();
                UpdateCacheSize();
                MessageBox.Show("Все данные удалены!", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
    
    private void DbEncryptionCheck_Changed(object sender, RoutedEventArgs e)
    {
        EncryptionPasswordPanel.Visibility = DbEncryptionCheck.IsChecked == true 
            ? Visibility.Visible : Visibility.Collapsed;
            
        if (DbEncryptionCheck.IsChecked == false)
        {
            // Отключаем шифрование
            LocalDatabaseService.Instance.DisableEncryption();
            EncryptionStatusText.Text = "Шифрование отключено";
            EncryptionStatusText.Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush");
        }
    }
    
    private void SetEncryptionPassword_Click(object sender, RoutedEventArgs e)
    {
        var password = EncryptionPasswordBox.Password;
        
        if (string.IsNullOrEmpty(password))
        {
            MessageBox.Show("Введите пароль", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        if (password.Length < 4)
        {
            MessageBox.Show("Пароль должен быть минимум 4 символа", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        
        var db = LocalDatabaseService.Instance;
        
        if (db.NeedsPassword)
        {
            // Проверяем пароль
            if (db.VerifyPassword(password))
            {
                EncryptionStatusText.Text = "✓ Пароль принят, шифрование активно";
                EncryptionStatusText.Foreground = (System.Windows.Media.Brush)FindResource("OnlineBrush");
                EncryptionPasswordBox.Clear();
            }
            else
            {
                MessageBox.Show("Неверный пароль", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        else
        {
            // Устанавливаем новый пароль
            db.SetEncryptionPassword(password);
            EncryptionStatusText.Text = "✓ Пароль установлен, шифрование активно";
            EncryptionStatusText.Foreground = (System.Windows.Media.Brush)FindResource("OnlineBrush");
            EncryptionPasswordBox.Clear();
            MessageBox.Show("Пароль шифрования установлен!\n\nНовые сообщения будут зашифрованы.", 
                "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    
    private readonly Dictionary<string, Dictionary<string, object>> _moduleSettings = new();
    
    private void LoadModules()
    {
        ModulesPanel.Children.Clear();
        
        var modules = ModuleManager.Instance.Modules;
        
        if (modules.Count == 0)
        {
            ModulesPanel.Children.Add(new TextBlock
            {
                Text = "Модули не загружены. Поместите .dll файлы в папку modules/",
                Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
                FontStyle = FontStyles.Italic,
                TextWrapping = TextWrapping.Wrap
            });
            return;
        }
        
        foreach (var module in modules)
        {
            // Заголовок модуля
            var headerPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            
            headerPanel.Children.Add(new TextBlock
            {
                Text = module.Name,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (System.Windows.Media.Brush)FindResource("TextBrush")
            });
            
            headerPanel.Children.Add(new TextBlock
            {
                Text = $"v{module.Version} • {module.Author}",
                FontSize = 11,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush")
            });
            
            if (!string.IsNullOrEmpty(module.Description))
            {
                headerPanel.Children.Add(new TextBlock
                {
                    Text = module.Description,
                    FontSize = 12,
                    Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }
            
            ModulesPanel.Children.Add(headerPanel);
            
            // Настройки модуля
            if (module is IModuleSettings settingsProvider)
            {
                _moduleSettings[module.Id] = new Dictionary<string, object>();
                
                foreach (var setting in settingsProvider.GetSettings())
                {
                    var settingPanel = new StackPanel { Margin = new Thickness(0, 5, 0, 5) };
                    
                    switch (setting.Type)
                    {
                        case ModuleSettingType.Boolean:
                            var checkBox = new CheckBox
                            {
                                Content = setting.DisplayName,
                                IsChecked = setting.CurrentValue as bool? ?? false,
                                Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                                Tag = new Tuple<string, string>(module.Id, setting.Key)
                            };
                            checkBox.Checked += ModuleSetting_Changed;
                            checkBox.Unchecked += ModuleSetting_Changed;
                            _moduleSettings[module.Id][setting.Key] = checkBox.IsChecked ?? false;
                            settingPanel.Children.Add(checkBox);
                            break;
                            
                        case ModuleSettingType.String:
                        case ModuleSettingType.Password:
                            settingPanel.Children.Add(new TextBlock
                            {
                                Text = setting.DisplayName,
                                FontSize = 12,
                                Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
                                Margin = new Thickness(0, 0, 0, 4)
                            });
                            
                            if (setting.Type == ModuleSettingType.Password)
                            {
                                var pwdBox = new PasswordBox
                                {
                                    Password = setting.CurrentValue?.ToString() ?? "",
                                    Tag = new Tuple<string, string>(module.Id, setting.Key)
                                };
                                pwdBox.PasswordChanged += ModulePasswordSetting_Changed;
                                _moduleSettings[module.Id][setting.Key] = pwdBox.Password;
                                settingPanel.Children.Add(pwdBox);
                            }
                            else
                            {
                                var textBox = new TextBox
                                {
                                    Text = setting.CurrentValue?.ToString() ?? "",
                                    Tag = new Tuple<string, string>(module.Id, setting.Key)
                                };
                                textBox.TextChanged += ModuleTextSetting_Changed;
                                _moduleSettings[module.Id][setting.Key] = textBox.Text;
                                settingPanel.Children.Add(textBox);
                            }
                            break;
                    }
                    
                    if (!string.IsNullOrEmpty(setting.Description))
                    {
                        settingPanel.Children.Add(new TextBlock
                        {
                            Text = setting.Description,
                            FontSize = 11,
                            Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(0, 2, 0, 0)
                        });
                    }
                    
                    ModulesPanel.Children.Add(settingPanel);
                }
            }
            
            // Разделитель между модулями
            ModulesPanel.Children.Add(new Separator { Margin = new Thickness(0, 10, 0, 10), Opacity = 0.3 });
        }
    }
    
    private void ModuleSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is Tuple<string, string> tag)
        {
            _moduleSettings[tag.Item1][tag.Item2] = cb.IsChecked ?? false;
        }
    }
    
    private void ModuleTextSetting_Changed(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is Tuple<string, string> tag)
        {
            _moduleSettings[tag.Item1][tag.Item2] = tb.Text;
        }
    }
    
    private void ModulePasswordSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb && pb.Tag is Tuple<string, string> tag)
        {
            _moduleSettings[tag.Item1][tag.Item2] = pb.Password;
        }
    }
    
    private void LoadEmotePacks()
    {
        var packs = SettingsService.Instance.GetAvailableEmotePacks();
        EmotePackCombo.Items.Clear();
        
        foreach (var pack in packs)
        {
            EmotePackCombo.Items.Add(pack);
        }
        
        // Выбираем текущий пак
        var currentPack = SettingsService.Instance.Settings.EmotePack ?? "(По умолчанию)";
        var index = packs.IndexOf(currentPack);
        EmotePackCombo.SelectedIndex = index >= 0 ? index : 0;
        
        UpdateEmotePackInfo();
    }
    
    private void EmotePackCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _emotePackChanged = true;
        UpdateEmotePackInfo();
    }
    
    private void UpdateEmotePackInfo()
    {
        var selectedPack = EmotePackCombo.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(selectedPack) || selectedPack == "(По умолчанию)")
        {
            EmotePackInfo.Text = "Смайлы из корневой папки emotes/";
        }
        else
        {
            var count = CountEmotesInPack(selectedPack);
            EmotePackInfo.Text = $"Пак: emotes/{selectedPack}/ ({count} смайлов)";
        }
    }
    
    private int CountEmotesInPack(string packName)
    {
        var packPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "emotes", packName);
        if (!System.IO.Directory.Exists(packPath)) return 0;
        
        var extensions = new[] { "*.gif", "*.png", "*.jpg", "*.jpeg", "*.webp" };
        int count = 0;
        foreach (var ext in extensions)
        {
            count += System.IO.Directory.GetFiles(packPath, ext).Length;
        }
        return count;
    }
    
    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var settings = SettingsService.Instance.Settings;
        
        // Сохраняем настройки уведомлений
        settings.NotifyMessages = NotifyMessagesCheck.IsChecked ?? true;
        settings.NotifySounds = NotifySoundsCheck.IsChecked ?? true;
        settings.NotifyFriends = NotifyFriendsCheck.IsChecked ?? true;
        
        // Сохраняем пак смайлов
        settings.EmotePack = EmotePackCombo.SelectedItem?.ToString();
        
        // Применяем тему
        var selectedTheme = (ThemeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var newTheme = selectedTheme == "Light" ? AppTheme.Light : AppTheme.Dark;
        ThemeService.Instance.ApplyTheme(newTheme);
        
        // Сохраняем в файл
        SettingsService.Instance.Save();
        
        // Обновляем статус
        var selectedStatus = (StatusCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var newStatus = selectedStatus switch
        {
            "Away" => UserStatus.Away,
            "DoNotDisturb" => UserStatus.DoNotDisturb,
            _ => UserStatus.Online
        };
        
        if (App.CurrentUser != null)
        {
            App.CurrentUser.Status = newStatus;
        }
        
        // Перезагружаем смайлы если пак изменился
        if (_emotePackChanged)
        {
            EmoteService.Instance.LoadEmotes(settings.EmotePack);
            
            // Уведомляем MainWindow обновить панель смайлов
            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow mainWindow)
                {
                    mainWindow.RefreshEmotesPanel();
                    break;
                }
            }
        }
        
        // Применяем настройки модулей
        foreach (var moduleId in _moduleSettings.Keys)
        {
            var moduleSettingsProvider = ModuleManager.Instance.GetModuleSettings(moduleId);
            if (moduleSettingsProvider != null)
            {
                foreach (var kvp in _moduleSettings[moduleId])
                {
                    moduleSettingsProvider.ApplySetting(kvp.Key, kvp.Value);
                }
            }
        }
        
        MessageBox.Show("Настройки сохранены", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }
    
    private async void LogoutButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Вы уверены, что хотите выйти из аккаунта?",
            "Выход",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
            
        if (result == MessageBoxResult.Yes)
        {
            try
            {
                await App.NetworkClient!.SendAsync(new Packet(PacketType.Logout));
                App.NetworkClient.Disconnect();
            }
            catch { }
            
            App.CurrentUser = null;
            App.SessionToken = null;
            
            // Открываем окно входа
            var loginWindow = new LoginWindow();
            loginWindow.Show();
            
            // Закрываем главное окно
            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow)
                {
                    window.Close();
                    break;
                }
            }
            
            Close();
        }
    }
}

