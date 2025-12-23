using ICYOU.Mobile.Services;

namespace ICYOU.Mobile.Pages;

public partial class SettingsPage : ContentPage
{
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
            // Отключаемся от сервера
            AppState.NetworkClient?.Disconnect();
            AppState.NetworkClient = null;
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
