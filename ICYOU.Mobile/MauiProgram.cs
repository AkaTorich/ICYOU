using Microsoft.Extensions.Logging;

namespace ICYOU.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		// КРИТИЧНО: Инициализация SQLite для Android
		SQLitePCL.Batteries_V2.Init();

		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
