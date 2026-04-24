using Iot.Client.Maui.Services;

namespace Iot.Client.Maui;

public partial class App : Application
{
	private readonly AppShell _appShell;

	public App(AppShell appShell, IotClientLoopService clientLoopService)
	{
		InitializeComponent();
		_appShell = appShell;
		clientLoopService.Start();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(_appShell);
	}
}
