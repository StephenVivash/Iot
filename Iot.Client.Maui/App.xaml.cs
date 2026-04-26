using Iot.Client.Maui.Pages;

namespace Iot.Client.Maui;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		MainPage mainPage = new();
		Window window = new(new AppShell(mainPage));
		window.Destroying += (_, _) => mainPage.DisposeRuntime();

		return window;
	}
}
