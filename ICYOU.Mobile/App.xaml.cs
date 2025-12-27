using Microsoft.Extensions.DependencyInjection;
using ICYOU.Core.Protocol;

namespace ICYOU.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// Загружаем модули
		try
		{
			Services.ModuleManager.Instance.LoadModules();
			System.Diagnostics.Debug.WriteLine("[APP] Modules loaded successfully");
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[APP] Error loading modules: {ex.Message}");
		}

		// Глобальный обработчик необработанных исключений
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
	}

	private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		var ex = e.ExceptionObject as Exception;
		System.Diagnostics.Debug.WriteLine($"[APP] Unhandled Exception: {ex?.Message}");
		System.Diagnostics.Debug.WriteLine($"[APP] Stack trace: {ex?.StackTrace}");

		// Показываем Toast на Android
		MainThread.BeginInvokeOnMainThread(() =>
		{
			try
			{
				#if ANDROID
				var context = Android.App.Application.Context;
				Android.Widget.Toast.MakeText(context, $"Ошибка: {ex?.Message}", Android.Widget.ToastLength.Long)?.Show();
				#endif
			}
			catch { }
		});
	}

	private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		System.Diagnostics.Debug.WriteLine($"[APP] Unobserved Task Exception: {e.Exception?.Message}");
		e.SetObserved();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		try
		{
			System.Diagnostics.Debug.WriteLine("[APP] Creating window...");
			var shell = new AppShell();
			System.Diagnostics.Debug.WriteLine("[APP] AppShell created");
			var window = new Window(shell);
			System.Diagnostics.Debug.WriteLine("[APP] Window created successfully");

			// Обрабатываем закрытие окна для отправки Logout
			window.Destroying += OnWindowDestroying;

			// Показываем LoginPage модально, если пользователь не залогинен
			MainThread.BeginInvokeOnMainThread(async () =>
			{
				try
				{
					if (AppState.CurrentUser == null)
					{
						System.Diagnostics.Debug.WriteLine("[APP] User not logged in, showing LoginPage modally");
						await shell.Navigation.PushModalAsync(new Pages.LoginPage());
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[APP] Error showing LoginPage: {ex.Message}");
				}
			});

			return window;
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[APP] FATAL Error creating window: {ex.Message}");
			System.Diagnostics.Debug.WriteLine($"[APP] Stack trace: {ex.StackTrace}");

			// Пытаемся показать Toast
			try
			{
				#if ANDROID
				var context = Android.App.Application.Context;
				Android.Widget.Toast.MakeText(context, $"Window Error: {ex.Message}", Android.Widget.ToastLength.Long)?.Show();
				#endif
			}
			catch { }

			throw;
		}
	}

	private void OnWindowDestroying(object? sender, EventArgs e)
	{
		try
		{
			System.Diagnostics.Debug.WriteLine("[APP] Window destroying, sending logout...");

			// Отправляем Logout пакет на сервер при закрытии приложения
			if (AppState.NetworkClient != null && AppState.CurrentUser != null)
			{
				try
				{
					var logoutPacket = new Packet(PacketType.Logout);
					AppState.NetworkClient.SendAsync(logoutPacket).Wait(1000); // Ждём максимум 1 секунду
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine($"[APP] Error sending logout on destroy: {ex.Message}");
				}

				AppState.NetworkClient.Disconnect();
				AppState.NetworkClient = null;
			}
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"[APP] Error in OnWindowDestroying: {ex.Message}");
		}
	}
}