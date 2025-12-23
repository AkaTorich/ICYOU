using System.Security.Cryptography;
using System.Text;
using ICYOU.Core.Protocol;
using ICYOU.Mobile.Services;

namespace ICYOU.Mobile.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await DoLogin();
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("register", new Dictionary<string, object>
        {
            ["ServerHost"] = ServerHost.Text,
            ["ServerPort"] = ServerPort.Text
        });
    }

    private async Task DoLogin()
    {
        if (string.IsNullOrWhiteSpace(Username.Text) || string.IsNullOrWhiteSpace(Password.Text))
        {
            ShowError("Заполните все поля");
            return;
        }

        if (!int.TryParse(ServerPort.Text, out var port))
        {
            ShowError("Неверный порт");
            return;
        }

        LoginButton.IsEnabled = false;
        ErrorText.IsVisible = false;

        try
        {
            var client = new TcpClient(ServerHost.Text, port);

            try
            {
                client.Connect();
            }
            catch (Exception connectEx)
            {
                ShowError($"Не удалось подключиться: {connectEx.Message}");
                LoginButton.IsEnabled = true;
                return;
            }

            var passwordHash = HashPassword(Password.Text);

            var response = await client.SendAndWaitAsync(new Packet(PacketType.Login, new LoginData
            {
                Username = Username.Text,
                PasswordHash = passwordHash
            }));

            if (response == null)
            {
                ShowError("Сервер не отвечает");
                client.Disconnect();
                return;
            }

            var data = response.GetData<LoginResponseData>();
            if (data == null || !data.Success)
            {
                ShowError(data?.Error ?? "Ошибка входа");
                client.Disconnect();
                return;
            }

            if (data.User == null)
            {
                ShowError("Ошибка получения данных пользователя");
                client.Disconnect();
                return;
            }

            // Сохраняем состояние
            AppState.NetworkClient = client;
            AppState.CurrentUser = data.User;
            AppState.SessionToken = data.SessionToken;

            if (!string.IsNullOrEmpty(data.SessionToken))
            {
                client.SessionToken = data.SessionToken;
            }
            client.UserId = data.User.Id;

            // Настройка файлового сервиса - игнорируем ошибки
            try
            {
                FileTransferService.Instance.SetServer(ServerHost.Text, port + 1);
            }
            catch (Exception ex)
            {
                Services.DebugLog.Write($"[LoginPage] FileTransferService error: {ex.Message}");
            }

            // Инициализация БД - логируем ошибки, но не прерываем вход
            try
            {
                LocalDatabaseService.Instance.Initialize(data.User.Username);
                Services.DebugLog.Write($"[LoginPage] Database initialized successfully for user: {data.User.Username}");
            }
            catch (Exception dbEx)
            {
                // Не прерываем вход, если БД не инициализировалась, но логируем ошибку
                Services.DebugLog.Write($"[LoginPage] Database initialization failed: {dbEx.Message}");
                Services.DebugLog.Write($"[LoginPage] DB Stack trace: {dbEx.StackTrace}");
            }

            // Закрываем модальную страницу логина, пользователь попадет на главную страницу с TabBar
            Services.DebugLog.Write("[LoginPage] Closing modal login page...");
            await Navigation.PopModalAsync();
            Services.DebugLog.Write("[LoginPage] Login successful, modal closed");
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка: {ex.Message}");
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.IsVisible = true;
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}
