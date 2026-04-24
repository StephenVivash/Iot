using Iot.Client.Maui.Logging;

namespace Iot.Client.Maui.Pages;

public partial class MainPage : ContentPage
{
	private readonly MainPageLogSink _logSink;

	public MainPage(MainPageLogSink logSink)
	{
		InitializeComponent();
		_logSink = logSink;
		edtOutput.Text = _logSink.GetText();
		_logSink.LineAppended += OnLogLineAppended;
	}

	private void OnLogLineAppended(string line)
	{
		MainThread.BeginInvokeOnMainThread(async () =>
		{
			edtOutput.Text = string.IsNullOrEmpty(edtOutput.Text)
				? line
				: $"{edtOutput.Text}{Environment.NewLine}{line}";

			await scrOutput.ScrollToAsync(0, double.MaxValue, false);
		});
	}

	private void OnMainPageSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		// Handle selection change.
	}
}
