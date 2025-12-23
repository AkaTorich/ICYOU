using ICYOU.Mobile.Pages;

namespace ICYOU.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// Регистрируем маршруты
		Routing.RegisterRoute("register", typeof(RegisterPage));
		Routing.RegisterRoute("chat", typeof(ChatPage));
	}
}
