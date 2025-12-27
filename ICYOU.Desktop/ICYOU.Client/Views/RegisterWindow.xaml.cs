using System.Security.Cryptography;
using System.Text;
using System.Windows;
using ICYOU.Core.Protocol;

namespace ICYOU.Client.Views;

public partial class RegisterWindow : Window
{
    private readonly string _serverHost;
    private readonly int _serverPort;
    
    public string? RegisteredUsername { get; private set; }
    
    public RegisterWindow(string serverHost, int serverPort)
    {
        InitializeComponent();
        _serverHost = serverHost;
        _serverPort = serverPort;
        Username.Focus();
    }
    
    private async void RegisterButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Username.Text) || 
            string.IsNullOrWhiteSpace(DisplayName.Text) ||
            string.IsNullOrWhiteSpace(Password.Password))
        {
            ShowError("Заполните все поля");
            return;
        }
        
        if (Password.Password != PasswordConfirm.Password)
        {
            ShowError("Пароли не совпадают");
            return;
        }
        
        if (Password.Password.Length < 4)
        {
            ShowError("Пароль должен быть не менее 4 символов");
            return;
        }
        
        RegisterButton.IsEnabled = false;
        ErrorText.Visibility = Visibility.Collapsed;
        
        try
        {
            var client = new TcpClient(_serverHost, _serverPort);
            client.Connect();
            
            var passwordHash = HashPassword(Password.Password);
            
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
            
            RegisteredUsername = Username.Text;
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка: {ex.Message}");
        }
        finally
        {
            RegisterButton.IsEnabled = true;
        }
    }
    
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
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

