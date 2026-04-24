using Iot.Client.Maui.Pages;

namespace Iot.Client.Maui;

public partial class AppShell : Shell
{
	public AppShell(MainPage mainPage)
	{
		InitializeComponent();
		Items.Add(new ShellContent
		{
			Route = nameof(MainPage),
			ContentTemplate = new DataTemplate(() => mainPage)
		});
	}
}
