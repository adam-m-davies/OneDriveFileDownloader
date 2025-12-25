using Avalonia;
using Avalonia.ReactiveUI;

namespace OneDriveFileDownloader.Avalonia;

internal class Program
{
	public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

	public static AppBuilder BuildAvaloniaApp()
	{
		return AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.LogToTrace()
			.UseReactiveUI();
	}
}
