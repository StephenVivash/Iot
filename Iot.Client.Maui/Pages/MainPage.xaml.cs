using Iot.Client.Maui.Logging;
using Iot.Data;
using Microsoft.EntityFrameworkCore;

namespace Iot.Client.Maui.Pages;

public partial class MainPage : ContentPage
{
	private readonly MainPageLogSink _logSink;

	private const string basePath = @"C:\Src\Iot\Iot.Server.Net";
	//private const string basePath = @"/home/pi/iot";

#if WINDOWS
	private Microsoft.UI.Xaml.Controls.TextBox? _configuredTextBox;
#endif

	public MainPage(MainPageLogSink logSink)
	{
		InitializeComponent();
		ConfigureEditors();
		_logSink = logSink;
		edtOutput.Text = _logSink.GetText();
		_logSink.LineAppended += OnLogLineAppended;
		DataTest();
	}


	private void ConfigureEditors()
	{
#if WINDOWS
		edtSource.HandlerChanged += (_, _) => ConfigureWindowsEditors();
		ConfigureWindowsEditors();
#endif
	}

#if WINDOWS
	private void ConfigureWindowsEditors()
	{
		if (edtSource.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.TextBox sourceTextBox)
			return;
		if (edtOutput.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.TextBox outputTextBox)
			return;

		if (!ReferenceEquals(_configuredTextBox, sourceTextBox))
		{
			if (_configuredTextBox is not null)
				_configuredTextBox.KeyDown -= SourceTextBox_KeyDown;

			_configuredTextBox = sourceTextBox;
			sourceTextBox.KeyDown += SourceTextBox_KeyDown;
		}

		sourceTextBox.TextWrapping = Microsoft.UI.Xaml.TextWrapping.NoWrap;
		sourceTextBox.IsSpellCheckEnabled = false;
		sourceTextBox.IsTextPredictionEnabled = false;

		outputTextBox.TextWrapping = Microsoft.UI.Xaml.TextWrapping.NoWrap;
		outputTextBox.IsSpellCheckEnabled = false;
		outputTextBox.IsTextPredictionEnabled = false;
	}

	private void SourceTextBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
	{
		if (e.Key != Windows.System.VirtualKey.Tab ||
			sender is not Microsoft.UI.Xaml.Controls.TextBox textBox)
			return;

		int selectionStart = textBox.SelectionStart;
		string text = textBox.Text ?? string.Empty;
		string updatedText = text.Remove(selectionStart, textBox.SelectionLength).Insert(selectionStart, "\t");
		textBox.Text = updatedText;
		edtSource.Text = updatedText;
		textBox.SelectionStart = selectionStart + 1;
		textBox.SelectionLength = 0;
		e.Handled = true;
	}
#endif

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

	private void DataTest()
	{
		DatabasePaths.Set(Path.Combine(basePath, "data", "Iot.Data.db"));
		using var dbContext = IotDataStore.CreateMigratedDbContext();

		var devices = dbContext.Devices
			.AsNoTracking()
			.Include(device => device.Points)
			.OrderBy(device => device.Id)
			.ToList();
		var groups = dbContext.Groups
			.AsNoTracking()
			.Include(group => group.GroupPoints)
			.ThenInclude(groupPoint => groupPoint.Point)
			.OrderBy(group => group.Id)
			.ToList();

		OnLogLineAppended($"Database: {DatabasePaths.GetConnectionString()}");
		OnLogLineAppended($"Devices: {devices.Count}");
		OnLogLineAppended($"Points: {dbContext.Points.Count()}");
		OnLogLineAppended($"Groups: {groups.Count}");
		OnLogLineAppended($"GroupPoints: {dbContext.GroupPoints.Count()}");

		foreach (var device in devices)
		{
			OnLogLineAppended($"Device #{device.Id} | Parent {device.ParentDeviceId} | {device.Name} | Type {device.TypeId} | {device.Status}");

			foreach (var point in device.Points.OrderBy(point => point.Id))
			{
				OnLogLineAppended($"  - {point.Name} | {point.TypeId} | {point.Status} {point.Units}".TrimEnd());
			}
		}

		foreach (var group in groups)
		{
			OnLogLineAppended($"Group #{group.Id} | {group.Name}");

			foreach (var groupPoint in group.GroupPoints.OrderBy(groupPoint => groupPoint.Id))
			{
				OnLogLineAppended($"  - Point #{groupPoint.PointId} | {groupPoint.Point.Name}");
			}
		}
	}





}
