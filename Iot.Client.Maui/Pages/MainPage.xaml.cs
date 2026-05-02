using Iot.Client.Maui.Logging;
using Iot.Data;
using Iot.Client.Maui.Services;
using Iot.Data.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PeerJsonSockets;
using ModelDeviceType = Iot.Data.Models.DeviceType;
using ModelPoint = Iot.Data.Models.Point;

namespace Iot.Client.Maui.Pages;

public partial class MainPage : ContentPage
{
	private readonly MainPageLogSink _logSink;
	private readonly ILoggerFactory _loggerFactory;
	private readonly IotClientLoopService _clientLoopService;
	private readonly ObservableCollection<PointItem> _points = [];
	private bool _loadingPickers;
	private bool _isDisposingRuntime;

#if WINDOWS
	private const string basePath = @"C:\Src\Iot\Iot.Server.Net";
#elif ANDROID
	private static readonly string basePath = FileSystem.AppDataDirectory;
#else
	private static readonly string basePath = "";
#endif
	private const string SelectedServerPreferenceKey = "MainPage.SelectedServer";

	public MainPage()
	{
		InitializeComponent();
		ConfigureSourceWebView();
		_logSink = new MainPageLogSink();
		_logSink.WriteLine($"App data: {basePath}");
		_logSink.WriteLine($"Log file: {GetLogFilePath()}");
		_loggerFactory = CreateLoggerFactory(_logSink);
		_clientLoopService = CreateClientLoopService(_loggerFactory);
		SetOutputText(_logSink.GetText());
		_logSink.LineAppended += OnLogLineAppended;
		_clientLoopService.PointStatusReceived += OnPointStatusReceived;
		colWorkspace.ItemsSource = _points;
		LoadPickers();
		ConnectToSelectedServer();
		DataTest();
	}

	private static ILoggerFactory CreateLoggerFactory(MainPageLogSink logSink)
	{
		string logFilePath = GetLogFilePath();

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

	private static string GetLogFilePath() =>
		Path.Combine(basePath, "logs", $"{DateTime.Now:yyyy-MM-dd HH-mm-ss}.log");

	private static IotClientLoopService CreateClientLoopService(ILoggerFactory loggerFactory)
	{
		PeerRuntimeOptions options = new(GetPeerName());
		PeerConnectionRegistry connectionRegistry = new();
		ILogger logger = loggerFactory.CreateLogger("Iot.Client.Maui");
		PeerConnectionService connectionService = new(logger);
		IotDatabase database = new(Path.Combine(basePath, "data", "Iot.Data.db"));
		IPeerClientLoopTask[] loopTasks =
		[
			new MauiClientConnectionLogTask(loggerFactory.CreateLogger<MauiClientConnectionLogTask>()),
			new MauiPointControlTestTask(loggerFactory.CreateLogger<MauiPointControlTestTask>())
		];

		PeerClientService peerClientService = new(options, connectionRegistry, connectionService, logger, database, loopTasks);

		return new IotClientLoopService(options, peerClientService, loggerFactory);
	}

	private static string GetPeerName()
	{
		string machineName = Environment.MachineName;
		if (!string.IsNullOrWhiteSpace(machineName) &&
			!string.Equals(machineName, "localhost", StringComparison.OrdinalIgnoreCase))
		{
			return machineName;
		}

		return $"maui-{DeviceInfo.Platform}-{DeviceInfo.Name}".Replace(' ', '-');
	}

	private void ConfigureSourceWebView()
	{
		webSource.Source = "https://www.google.com";
	}

	private void LoadPickers()
	{
		_loadingPickers = true;

		using var dbContext = CreateDbContext();
#if ANDROID
		dbContext.InitialiseDB();
#endif
		List<ServerItem> servers = dbContext.Devices
			.AsNoTracking()
			.Where(device => device.TypeId != (int)ModelDeviceType.Client)
			.OrderBy(device => device.Name)
			.Select(device => new ServerItem(device.Id, device.Name))
			.ToList();

		List<PointSetItem> pointSets =
		[
			PointSetItem.All()
		];

		pointSets.AddRange(dbContext.Devices
			.AsNoTracking()
			.Where(device => device.TypeId != (int)ModelDeviceType.Client)
			.OrderBy(device => device.Name)
			.Select(device => PointSetItem.Server(device.Id, $"Server: {device.Name}")));

		pointSets.AddRange(dbContext.Groups
			.AsNoTracking()
			.OrderBy(group => group.Name)
			.Select(group => PointSetItem.Group(group.Id, $"Group: {group.Name}")));

		pckServer.ItemsSource = servers;
		pckPointSet.ItemsSource = pointSets;

		string selectedServerName = Preferences.Get(SelectedServerPreferenceKey, string.Empty);
		pckServer.SelectedItem = servers.FirstOrDefault(server =>
			string.Equals(server.Name, selectedServerName, StringComparison.OrdinalIgnoreCase)) ??
			servers.FirstOrDefault();
		pckPointSet.SelectedItem = pointSets.FirstOrDefault();

		_loadingPickers = false;
		LoadSelectedPoints();
	}

	private void ConnectToSelectedServer()
	{
		if (pckServer.SelectedItem is not ServerItem server)
		{
			return;
		}

		Preferences.Set(SelectedServerPreferenceKey, server.Name);
		_clientLoopService.ConnectToServer(server.Name);
	}

	private void LoadSelectedPoints()
	{
		if (pckPointSet.SelectedItem is not PointSetItem pointSet)
		{
			return;
		}

		using var dbContext = CreateDbContext();
		IQueryable<ModelPoint> query = dbContext.Points
			.AsNoTracking()
			.Include(point => point.Device);

		query = pointSet.Kind switch
		{
			PointSetKind.Server => query.Where(point => point.DeviceId == pointSet.Id),
			PointSetKind.Group => query.Where(point => point.GroupPoints.Any(groupPoint => groupPoint.GroupId == pointSet.Id)),
			_ => query
		};

		List<PointItem> points = query
			.OrderBy(point => point.Device.Name)
			.ThenBy(point => point.Id)
			.Select(point => new PointItem(
				point.Id,
				point.Name,
				point.Status,
				point.RawStatus,
				point.Status0,
				point.Status1,
				point.Scale,
				point.Units,
				point.TypeId))
			.ToList();

		_points.Clear();
		foreach (PointItem point in points)
		{
			_points.Add(point);
		}
	}

	private static AppDbContext CreateDbContext() =>
		IotDataStore.CreateDbContext(Path.Combine(basePath, "data", "Iot.Data.db"));

	private void OnServerPickerSelectedIndexChanged(object? sender, EventArgs e)
	{
		if (_loadingPickers)
		{
			return;
		}

		ConnectToSelectedServer();
	}

	private void OnPointSetPickerSelectedIndexChanged(object? sender, EventArgs e)
	{
		if (_loadingPickers)
		{
			return;
		}

		LoadSelectedPoints();
	}

	private async void OnDigitalStatePickerSelectedIndexChanged(object? sender, EventArgs e)
	{
		if (sender is not Picker { BindingContext: PointItem point } ||
			point.IsUpdatingFromStatus ||
			string.IsNullOrWhiteSpace(point.SelectedDigitalState) ||
			string.Equals(point.Status, point.SelectedDigitalState, StringComparison.Ordinal))
		{
			return;
		}

		await SendPointControlAsync(point, point.SelectedDigitalState);
	}

	private async void OnSetPointClicked(object? sender, EventArgs e)
	{
		if (sender is not Button { BindingContext: PointItem point } ||
			string.IsNullOrWhiteSpace(point.PendingStatus))
		{
			return;
		}

		await SendPointControlAsync(point, point.PendingStatus);
	}

	private async Task SendPointControlAsync(PointItem point, string status)
	{
		try
		{
			if (await _clientLoopService.SendPointControlAsync(point.Id, status))
			{
				point.Status = status;
			}
		}
		catch (Exception ex)
		{
			_loggerFactory.CreateLogger<MainPage>().LogError(ex,
				"Failed to send point control for {PointName} ({PointId}): {Status}.",
				point.Name, point.Id, status);
		}
	}

	private void OnPointStatusReceived(PointStatus pointStatus)
	{
		if (_isDisposingRuntime)
		{
			return;
		}

		MainThread.BeginInvokeOnMainThread(() =>
		{
			PointItem? point = _points.FirstOrDefault(point => point.Id == pointStatus.Id);
			point?.ApplyReceivedStatus(pointStatus.Status);
		});
	}

	private void SetOutputText(string text)
	{
		lblOutput.Text = string.IsNullOrEmpty(text)
			? "Command output will appear here."
			: text;
		lblOutput.InvalidateMeasure();
	}

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

				SetOutputText(_logSink.GetText());
				await Task.Yield();
				lblOutput.InvalidateMeasure();
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
		_clientLoopService.PointStatusReceived -= OnPointStatusReceived;
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
					point.Name, point.TypeId, point.Status, point.Units);
		}

		foreach (var group in groups)
		{
			logger.LogInformation("Group #{GroupId} | {GroupName}", group.Id, group.Name);
			foreach (var groupPoint in group.GroupPoints.OrderBy(groupPoint => groupPoint.Id))
				logger.LogInformation("  - Point #{PointId} | {PointName}",
					groupPoint.PointId, groupPoint.Point.Name);
		}
	}

	private sealed record ServerItem(int Id, string Name);

	private enum PointSetKind
	{
		All,
		Server,
		Group
	}

	private sealed record PointSetItem(PointSetKind Kind, int Id, string Name)
	{
		public static PointSetItem All() => new(PointSetKind.All, 0, "All");

		public static PointSetItem Server(int id, string name) => new(PointSetKind.Server, id, name);

		public static PointSetItem Group(int id, string name) => new(PointSetKind.Group, id, name);
	}

	private sealed class PointItem : INotifyPropertyChanged
	{
		private string _status;
		private string? _selectedDigitalState;
		private string _pendingStatus;

		public PointItem(
			int id,
			string name,
			string status,
			double rawStatus,
			string status0,
			string status1,
			double scale,
			string units,
			PointType typeId)
		{
			Id = id;
			Name = name;
			_status = status;
			RawStatus = rawStatus;
			Status0 = string.IsNullOrWhiteSpace(status0) ? "Off" : status0;
			Status1 = string.IsNullOrWhiteSpace(status1) ? "On" : status1;
			Scale = scale;
			Units = units;
			TypeId = typeId;
			DigitalStates = [Status0, Status1];
			_selectedDigitalState = DigitalStates.FirstOrDefault(option =>
				string.Equals(option, status, StringComparison.Ordinal));
			_pendingStatus = status;
		}

		public event PropertyChangedEventHandler? PropertyChanged;

		public int Id { get; }

		public string Name { get; }

		public string Status
		{
			get => _status;
			set
			{
				if (_status == value)
				{
					return;
				}

				_status = value;
				PendingStatus = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(StatusText));
			}
		}

		public string Status0 { get; }

		public string Status1 { get; }

		public string Units { get; }

		public double RawStatus { get; }

		public double Scale { get; }

		public PointType TypeId { get; }

		public string[] DigitalStates { get; }

		private string DisplayValue
		{
			get
			{
				if (!string.IsNullOrWhiteSpace(Status))
				{
					return Status;
				}

				double value = RawStatus * Scale;
				return value == 0 ? string.Empty : value.ToString("G4");
			}
		}

		public string StatusText => string.IsNullOrWhiteSpace(Units)
			? DisplayValue
			: $"{DisplayValue} {Units} ";

		public bool IsDigitalOutput => TypeId == PointType.DigitalOutput;

		public bool IsOtherOutput =>
			TypeId is PointType.AnalogOutput or PointType.PwmOutput or PointType.ShifOutput;

		public bool IsStatusTextVisible => !IsDigitalOutput && !IsOtherOutput;

		public bool IsUpdatingFromStatus { get; private set; }

		public void ApplyReceivedStatus(string status)
		{
			IsUpdatingFromStatus = true;
			try
			{
				Status = status;
				if (IsDigitalOutput)
				{
					SelectedDigitalState = DigitalStates.FirstOrDefault(option =>
						string.Equals(option, status, StringComparison.OrdinalIgnoreCase));
				}
			}
			finally
			{
				IsUpdatingFromStatus = false;
			}
		}

		public string? SelectedDigitalState
		{
			get => _selectedDigitalState;
			set
			{
				if (_selectedDigitalState == value)
				{
					return;
				}

				_selectedDigitalState = value;
				OnPropertyChanged();
			}
		}

		public string PendingStatus
		{
			get => _pendingStatus;
			set
			{
				if (_pendingStatus == value)
				{
					return;
				}

				_pendingStatus = value;
				OnPropertyChanged();
			}
		}

		private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
	}
}
