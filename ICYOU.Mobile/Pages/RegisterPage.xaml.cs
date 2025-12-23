using System.Security.Cryptography;
using System.Text;
using ICYOU.Core.Protocol;
using ICYOU.Mobile.Services;

namespace ICYOU.Mobile.Pages;

[QueryProperty(nameof(ServerHost), "ServerHost")]
[QueryProperty(nameof(ServerPort), "ServerPort")]
public partial class RegisterPage : ContentPage
{
    public string ServerHost { get; set; } = "127.0.0.1";
    public string ServerPort { get; set; } = "5000";

    public RegisterPage()
    {
        InitializeComponent();
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Username.Text) ||
            string.IsNullOrWhiteSpace(DisplayName.Text) ||
            string.IsNullOrWhiteSpace(Password.Text))
        {
            ShowError("Заполните все поля");
            return;
        }

        if (Password.Text != PasswordConfirm.Text)
        {
            ShowError("Пароли не совпадают");
            return;
        }

        if (Password.Text.Length < 4)
        {
            ShowError("Пароль должен быть не менее 4 символов");
            return;
        }

        if (!int.TryParse(ServerPort, out var port))
        {
            ShowError("Неверный порт сервера");
            return;
        }

        RegisterButton.IsEnabled = false;
        ErrorText.IsVisible = false;

        try
        {
            var client = new TcpClient(ServerHost, port);
            client.Connect();

            var passwordHash = HashPassword(Password.Text);

            var response = await client.SendAndWaitAsync(new Packet(PacketType.Register, new RegisterData
            {
                Username = Username.Text,
                DisplayName = DisplayName.Text,
                PasswordHash = passwordHash
            }));

            client.Disconnect();

            if (response == null)
            {
                ShowError("Сервер не отвечает");
                return;
            }

            var data = response.GetData<RegisterResponseData>();
            if (data == null || !data.Success)
            {
                ShowError(data?.Error ?? "Ошибка регистрации");
                return;
            }

            ShowError("Регистрация успешна! Теперь войдите");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка: {ex.Message}");
            DebugLog.Write($"[REGISTER] Exception: {ex}");
        }
        finally
        {
            RegisterButton.IsEnabled = true;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
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
