using Iot.Client.Maui.Logging;
using Iot.Data;
using Iot.Client.Maui.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PeerJsonSockets;

namespace Iot.Client.Maui.Pages;

public partial class MainPage : ContentPage
{
	private readonly MainPageLogSink _logSink;
	private readonly ILoggerFactory _loggerFactory;
	private readonly IotClientLoopService _clientLoopService;
	private bool _isDisposingRuntime;

	private const string basePath = @"C:\Src\Iot\Iot.Server.Net";
	//private const string basePath = @"/home/pi/iot";

#if WINDOWS
	private Microsoft.UI.Xaml.Controls.TextBox? _configuredTextBox;
#endif

	public MainPage()
	{
		InitializeComponent();
		ConfigureEditors();
		_logSink = new MainPageLogSink();
		_loggerFactory = CreateLoggerFactory(_logSink);
		_clientLoopService = CreateClientLoopService(_loggerFactory);
		edtOutput.Text = _logSink.GetText();
		_logSink.LineAppended += OnLogLineAppended;
		_clientLoopService.Start();
		DataTest();
	}

	private static ILoggerFactory CreateLoggerFactory(MainPageLogSink logSink)
	{
		string logFilePath = Path.Combine(
			basePath,
			"logs",
			$"{DateTime.Now:yyyy-MM-dd HH-mm-ss}.log");

		return LoggerFactory.Create(logging =>
		{
			logging.SetMinimumLevel(LogLevel.Information);
			logging.AddFilter("Microsoft", LogLevel.Warning);
			logging.AddProvider(new MainPageLoggerProvider(logSink));
			logging.AddProvider(new TimestampedFileLoggerProvider(logFilePath));
#if DEBUG
			logging.AddDebug();
#endif
		});
	}

	private static IotClientLoopService CreateClientLoopService(ILoggerFactory loggerFactory)
	{
		PeerRuntimeOptions options = new(Environment.MachineName);
		PeerConnectionRegistry connectionRegistry = new();
		ILogger logger = loggerFactory.CreateLogger("Iot.Client.Maui");
		PeerConnectionService connectionService = new(logger);
		IotDatabase database = new(Path.Combine(basePath, "data", "Iot.Data.db"));
		IPeerClientLoopTask[] loopTasks =
		[
			new MauiClientConnectionLogTask(loggerFactory.CreateLogger<MauiClientConnectionLogTask>())
		];

		PeerClientService peerClientService = new(options, connectionRegistry, connectionService, logger, database, loopTasks);

		return new IotClientLoopService(options, peerClientService, loggerFactory);
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
		if (_isDisposingRuntime)
		{
			return;
		}

		MainThread.BeginInvokeOnMainThread(async () =>
		{
			try
			{
				if (_isDisposingRuntime)
				{
					return;
				}

				edtOutput.Text = string.IsNullOrEmpty(edtOutput.Text)
					? line
					: $"{edtOutput.Text}{Environment.NewLine}{line}";

				await scrOutput.ScrollToAsync(0, double.MaxValue, false);
			}
			catch (ObjectDisposedException)
			{
			}
			catch (InvalidOperationException) when (_isDisposingRuntime)
			{
			}
		});
	}

	public void DisposeRuntime()
	{
		if (_isDisposingRuntime)
		{
			return;
		}

		_isDisposingRuntime = true;
		_logSink.LineAppended -= OnLogLineAppended;
		_clientLoopService.Stop();
	}

	private void OnMainPageSelectionChanged(object? sender, SelectionChangedEventArgs e)
	{
		// Handle selection change.
	}

	private void DataTest()
	{
		ILogger logger = _loggerFactory.CreateLogger<MainPage>();
		using var dbContext = IotDataStore.CreateDbContext(
			Path.Combine(basePath, "data", "Iot.Data.db"));

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

		logger.LogInformation("Devices: {DeviceCount}", devices.Count);
		logger.LogInformation("Points: {PointCount}", dbContext.Points.Count());
		logger.LogInformation("Groups: {GroupCount}", groups.Count);
		logger.LogInformation("GroupPoints: {GroupPointCount}", dbContext.GroupPoints.Count());

		foreach (var device in devices)
		{
			logger.LogInformation("Device #{DeviceId} | Parent {ParentDeviceId} | {DeviceName} | Type {DeviceTypeId} | {DeviceStatus}",
				device.Id, device.ParentDeviceId, device.Name, device.TypeId, device.Status);
			foreach (var point in device.Points.OrderBy(point => point.Id))
				logger.LogInformation("  - {PointName} | {PointTypeId} | {PointStatus} {PointUnits}",
					point.Name,	point.TypeId, point.Status, point.Units);
		}

		foreach (var group in groups)
		{
			logger.LogInformation("Group #{GroupId} | {GroupName}", group.Id, group.Name);
			foreach (var groupPoint in group.GroupPoints.OrderBy(groupPoint => groupPoint.Id))
				logger.LogInformation("  - Point #{PointId} | {PointName}",
					groupPoint.PointId,	groupPoint.Point.Name);
		}
	}
}
