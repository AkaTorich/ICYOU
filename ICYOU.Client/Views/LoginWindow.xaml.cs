using System.Security.Cryptography;
using System.Text;
using System.Windows;
using ICYOU.Core.Protocol;

namespace ICYOU.Client.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
        Username.Focus();
    }
    
    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        await DoLogin();
    }
    
    private async void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        var registerWindow = new RegisterWindow(ServerHost.Text, int.Parse(ServerPort.Text));
        if (registerWindow.ShowDialog() == true)
        {
            Username.Text = registerWindow.RegisteredUsername;
            MessageBox.Show("Регистрация успешна! Теперь войдите.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    
    private async Task DoLogin()
    {
        if (string.IsNullOrWhiteSpace(Username.Text) || string.IsNullOrWhiteSpace(Password.Password))
        {
            ShowError("Заполните все поля");
            return;
        }
        
        LoginButton.IsEnabled = false;
        ErrorText.Visibility = Visibility.Collapsed;
        
        try
        {
            Services.DebugLog.Write($"[LOGIN] Попытка подключения к {ServerHost.Text}:{ServerPort.Text}");
            var client = new TcpClient(ServerHost.Text, int.Parse(ServerPort.Text));
            try
            {
                client.Connect();
                Services.DebugLog.Write("[LOGIN] Подключение успешно, отправка Login пакета...");
            }
            catch (Exception connectEx)
            {
                Services.DebugLog.Write($"[LOGIN] ОШИБКА подключения: {connectEx.Message}");
                ShowError($"Не удалось подключиться к серверу: {connectEx.Message}");
                return;
            }
            
            var passwordHash = HashPassword(Password.Password);
            
            Services.DebugLog.Write($"[LOGIN] Отправка Login пакета для пользователя {Username.Text}");
            var response = await client.SendAndWaitAsync(new Packet(PacketType.Login, new LoginData
            {
                Username = Username.Text,
                PasswordHash = passwordHash
            }));
            
            if (response == null)
            {
                Services.DebugLog.Write("[LOGIN] Сервер не ответил на Login запрос");
                ShowError("Сервер не отвечает");
                client.Disconnect();
                return;
            }
            
            Services.DebugLog.Write($"[LOGIN] Получен ответ: Type={response.Type}");
            var data = response.GetData<LoginResponseData>();
            if (data == null || !data.Success)
            {
                Services.DebugLog.Write($"[LOGIN] Ошибка входа: {data?.Error ?? "Unknown"}");
                ShowError(data?.Error ?? "Ошибка входа");
                client.Disconnect();
                return;
            }
            
            Services.DebugLog.Write($"[LOGIN] Вход успешен, UserId={data.User?.Id}");
            
            // Успешный вход
            App.NetworkClient = client;
            App.CurrentUser = data.User;
            App.SessionToken = data.SessionToken;
            client.SessionToken = data.SessionToken;
            client.UserId = data.User!.Id;
            
            // Настройка файлового сервиса
            var port = int.Parse(ServerPort.Text);
            Services.FileTransferService.Instance.SetServer(ServerHost.Text, port + 1);
            
            // Инициализация локальной БД для пользователя
            Services.LocalDatabaseService.Instance.Initialize(App.CurrentUser!.Username);
            
            var mainWindow = new MainWindow();
            mainWindow.Show();
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка подключения: {ex.Message}");
        }
        finally
        {
            LoginButton.IsEnabled = true;
        }
    }
    
    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorText.Visibility = Visibility.Visible;
    }
    
    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }
}

