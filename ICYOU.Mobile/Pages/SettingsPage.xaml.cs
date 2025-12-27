using ICYOU.Mobile.Services;
using ICYOU.SDK;
using ICYOU.Core.Protocol;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ICYOU.Mobile.Pages;

public partial class SettingsPage : ContentPage
{
    private ObservableCollection<ModuleViewModel> _moduleViewModels = new();

    public SettingsPage()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[SettingsPage] Constructor error: {ex.Message}");
            DebugLog.Write($"[SettingsPage] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Не загружаем данные если пользователь не залогинен
        if (AppState.CurrentUser == null)
            return;

        LoadSettings();
    }

    private void LoadSettings()
    {
        // User Info
        if (AppState.CurrentUser != null)
        {
            UsernameLabel.Text = $"Логин: {AppState.CurrentUser.Username}";
            DisplayNameLabel.Text = $"Имя: {AppState.CurrentUser.DisplayName}";
            UserIdLabel.Text = $"ID: {AppState.CurrentUser.Id}";
        }

        // Notifications
        NotifyMessagesSwitch.IsToggled = AppState.NotifyMessages;
        NotifySoundsSwitch.IsToggled = AppState.NotifySounds;
        NotifyFriendsSwitch.IsToggled = AppState.NotifyFriends;

        // Modules
        LoadModules();
    }

    private void LoadModules()
    {
        try
        {
            _moduleViewModels.Clear();

            var moduleInfos = ModuleManager.Instance.GetModuleInfos();

            if (moduleInfos.Count == 0)
            {
                NoModulesLabel.IsVisible = true;
                ModulesCollectionView.IsVisible = false;
                return;
            }

            NoModulesLabel.IsVisible = false;
            ModulesCollectionView.IsVisible = true;

            foreach (var moduleInfo in moduleInfos)
            {
                if (!moduleInfo.HasSettings)
                    continue;

                var moduleSettings = ModuleManager.Instance.GetModuleSettings(moduleInfo.Id);
                if (moduleSettings == null)
                    continue;

                var settings = moduleSettings.GetSettings().ToList();
                var viewModel = new ModuleViewModel
                {
                    Id = moduleInfo.Id,
                    Name = moduleInfo.Name,
                    Description = moduleInfo.Description,
                    Settings = new ObservableCollection<ModuleSettingViewModel>(
                        settings.Select(s => new ModuleSettingViewModel(moduleInfo.Id, s))
                    )
                };

                _moduleViewModels.Add(viewModel);
            }

            ModulesCollectionView.ItemsSource = _moduleViewModels;
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[SettingsPage] LoadModules error: {ex}");
            ShowStatus($"Ошибка загрузки модулей: {ex.Message}", false);
        }
    }

    private void OnNotifyMessagesToggled(object sender, ToggledEventArgs e)
    {
        AppState.NotifyMessages = e.Value;
    }

    private void OnNotifySoundsToggled(object sender, ToggledEventArgs e)
    {
        AppState.NotifySounds = e.Value;
    }

    private void OnNotifyFriendsToggled(object sender, ToggledEventArgs e)
    {
        AppState.NotifyFriends = e.Value;
    }

    private void OnClearCacheClicked(object sender, EventArgs e)
    {
        try
        {
            LocalDatabaseService.Instance.ClearAllData();
            ShowStatus("Кэш сообщений очищен", true);
        }
        catch (Exception ex)
        {
            ShowStatus($"Ошибка очистки кэша: {ex.Message}", false);
            DebugLog.Write($"[SettingsPage] ClearCache error: {ex}");
        }
    }

    private void OnExportMessagesClicked(object sender, EventArgs e)
    {
        ShowStatus("Экспорт пока не реализован", false);
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        try
        {
            // Отправляем Logout пакет на сервер
            if (AppState.NetworkClient != null)
            {
                try
                {
                    var logoutPacket = new Packet(PacketType.Logout);
                    await AppState.NetworkClient.SendAsync(logoutPacket);
                    await Task.Delay(200); // Даём время на отправку
                }
                catch (Exception ex)
                {
                    DebugLog.Write($"[SettingsPage] Error sending logout packet: {ex.Message}");
                }

                // Отключаемся от сервера
                AppState.NetworkClient.Disconnect();
                AppState.NetworkClient = null;
            }

            AppState.CurrentUser = null;
            AppState.SessionToken = null;

            // Возвращаемся на страницу входа
            await Shell.Current.GoToAsync("../..");
        }
        catch (Exception ex)
        {
            ShowStatus($"Ошибка выхода: {ex.Message}", false);
            DebugLog.Write($"[SettingsPage] Logout error: {ex}");
        }
    }

    private void OnModuleSettingToggled(object sender, ToggledEventArgs e)
    {
        if (sender is Switch toggle && toggle.BindingContext is ModuleSettingViewModel setting)
        {
            try
            {
                setting.BoolValue = e.Value;
                ApplyModuleSetting(setting.ModuleId, setting.Key, e.Value);
            }
            catch (Exception ex)
            {
                DebugLog.Write($"[SettingsPage] Error toggling setting: {ex}");
                ShowStatus($"Ошибка изменения настройки: {ex.Message}", false);
            }
        }
    }

    private void OnModuleSettingTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is Entry entry && entry.BindingContext is ModuleSettingViewModel setting)
        {
            try
            {
                if (setting.IsInteger)
                {
                    if (int.TryParse(e.NewTextValue, out int intValue))
                    {
                        setting.IntValue = e.NewTextValue;
                        ApplyModuleSetting(setting.ModuleId, setting.Key, intValue);
                    }
                }
                else if (setting.IsString)
                {
                    setting.StringValue = e.NewTextValue;
                    ApplyModuleSetting(setting.ModuleId, setting.Key, e.NewTextValue);
                }
            }
            catch (Exception ex)
            {
                DebugLog.Write($"[SettingsPage] Error changing text setting: {ex}");
                ShowStatus($"Ошибка изменения настройки: {ex.Message}", false);
            }
        }
    }

    private void OnModuleSettingPickerChanged(object sender, EventArgs e)
    {
        if (sender is Picker picker && picker.BindingContext is ModuleSettingViewModel setting)
        {
            try
            {
                if (picker.SelectedItem != null)
                {
                    setting.SelectedOption = picker.SelectedItem.ToString();
                    ApplyModuleSetting(setting.ModuleId, setting.Key, picker.SelectedItem);
                }
            }
            catch (Exception ex)
            {
                DebugLog.Write($"[SettingsPage] Error changing picker setting: {ex}");
                ShowStatus($"Ошибка изменения настройки: {ex.Message}", false);
            }
        }
    }

    private void ApplyModuleSetting(string moduleId, string key, object value)
    {
        try
        {
            var moduleSettings = ModuleManager.Instance.GetModuleSettings(moduleId);
            if (moduleSettings != null)
            {
                moduleSettings.ApplySetting(key, value);
                DebugLog.Write($"[SettingsPage] Applied setting {key}={value} for module {moduleId}");
            }
        }
        catch (Exception ex)
        {
            DebugLog.Write($"[SettingsPage] ApplySetting error: {ex}");
            throw;
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

// View Models для привязки данных модулей
public class ModuleViewModel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ObservableCollection<ModuleSettingViewModel> Settings { get; set; } = new();
}

public class ModuleSettingViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string ModuleId { get; }
    public string Key { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public ModuleSettingType Type { get; }

    private bool _boolValue;
    private string _stringValue = "";
    private string _intValue = "";
    private string? _selectedOption;

    public ModuleSettingViewModel(string moduleId, ModuleSetting setting)
    {
        ModuleId = moduleId;
        Key = setting.Key;
        DisplayName = setting.DisplayName;
        Description = setting.Description;
        Type = setting.Type;

        // Инициализируем значения
        if (setting.CurrentValue != null)
        {
            switch (Type)
            {
                case ModuleSettingType.Boolean:
                    _boolValue = (bool)setting.CurrentValue;
                    break;
                case ModuleSettingType.String:
                case ModuleSettingType.Password:
                    _stringValue = setting.CurrentValue.ToString() ?? "";
                    break;
                case ModuleSettingType.Integer:
                    _intValue = setting.CurrentValue.ToString() ?? "0";
                    break;
                case ModuleSettingType.Choice:
                    _selectedOption = setting.CurrentValue.ToString();
                    if (setting.Options != null)
                    {
                        Options = new ObservableCollection<string>(
                            setting.Options.Select(o => o.ToString() ?? "")
                        );
                    }
                    break;
            }
        }
    }

    public bool BoolValue
    {
        get => _boolValue;
        set
        {
            if (_boolValue != value)
            {
                _boolValue = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BoolValue)));
            }
        }
    }

    public string StringValue
    {
        get => _stringValue;
        set
        {
            if (_stringValue != value)
            {
                _stringValue = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StringValue)));
            }
        }
    }

    public string IntValue
    {
        get => _intValue;
        set
        {
            if (_intValue != value)
            {
                _intValue = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IntValue)));
            }
        }
    }

    public string? SelectedOption
    {
        get => _selectedOption;
        set
        {
            if (_selectedOption != value)
            {
                _selectedOption = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedOption)));
            }
        }
    }

    public ObservableCollection<string> Options { get; } = new();

    public bool IsBoolean => Type == ModuleSettingType.Boolean;
    public bool IsString => Type == ModuleSettingType.String || Type == ModuleSettingType.Password;
    public bool IsInteger => Type == ModuleSettingType.Integer;
    public bool IsChoice => Type == ModuleSettingType.Choice;
}
