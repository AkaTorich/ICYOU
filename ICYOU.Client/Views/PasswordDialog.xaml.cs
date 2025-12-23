using System.Windows;

namespace ICYOU.Client.Views;

public partial class PasswordDialog : Window
{
    public string Password => PasswordBox.Password;
    
    public PasswordDialog()
    {
        InitializeComponent();
        PasswordBox.Focus();
    }
    
    private void OK_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(Password))
        {
            MessageBox.Show("Введите пароль", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
        Close();
    }
    
    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

