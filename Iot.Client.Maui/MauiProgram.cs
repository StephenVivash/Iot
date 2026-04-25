using CommunityToolkit.Maui;
using Iot.Client.Maui.Logging;
using Iot.Client.Maui.Pages;
using Iot.Client.Maui.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PeerJsonSockets;

namespace Iot.Client.Maui
{
	public static class MauiProgram
	{
		public static MauiApp CreateMauiApp()
		{
			var builder = MauiApp.CreateBuilder();
			MainPageLogSink mainPageLogSink = new();
			string logFilePath = Path.Combine(
				@"C:\Src\Iot\Iot.Server.Net",
				//FileSystem.Current.AppDataDirectory,
				"logs",
				$"{DateTime.Now:yyyy-MM-dd HH-mm-ss}.log");

			builder
				.UseMauiApp<App>()
				.UseMauiCommunityToolkit()
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
					fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
					//fonts.AddFont("Courier New.ttf", "CourierNew"); 
				});

			builder.Services.AddSingleton(mainPageLogSink);
			builder.Services.AddSingleton<PeerRuntimeOptions>(_ => new(Environment.MachineName));
			builder.Services.AddSingleton<PeerConnectionRegistry>();
			builder.Services.AddSingleton(sp =>
				new PeerConnectionService(sp.GetRequiredService<ILoggerFactory>().CreateLogger("Iot.Client.Maui")));
			builder.Services.AddSingleton(sp =>
				new PeerClientService(
					sp.GetRequiredService<PeerRuntimeOptions>(),
					sp.GetRequiredService<PeerConnectionRegistry>(),
					sp.GetRequiredService<PeerConnectionService>(),
					sp.GetRequiredService<ILoggerFactory>().CreateLogger("Iot.Client.Maui"),
					sp.GetServices<IPeerClientLoopTask>()));
			builder.Services.AddSingleton<IPeerClientLoopTask, MauiClientConnectionLogTask>();
			builder.Services.AddSingleton<IotClientLoopService>();
			builder.Services.AddSingleton<AppShell>();
			builder.Services.AddSingleton<MainPage>();

			builder.Logging.ClearProviders();
			builder.Logging.SetMinimumLevel(LogLevel.Information);
			builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
			builder.Logging.AddProvider(new MainPageLoggerProvider(mainPageLogSink));
			builder.Logging.AddProvider(new TimestampedFileLoggerProvider(logFilePath));

#if DEBUG
			builder.Logging.AddDebug();
#endif

			return builder.Build();
		}
	}
}
