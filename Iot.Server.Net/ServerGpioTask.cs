using Iot.Data;
using Iot.Data.Models;
using Iot.Device.Tm16xx;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PeerJsonSockets;
using System.Device.Gpio;
using System.Device.I2c;
using System.Device.Pwm;

namespace Iot.Server.Net;

internal sealed class ServerGpioTask : IPeerServerLoopTask, IPeerPointControlHandler
{
	private readonly ILogger _logger;
	private readonly int _deviceId;
	private readonly IotDatabase _database;
	private readonly Dictionary<int, GpioPoint> _points = [];
	private GpioController? _gpioController;
	private bool _initialiseGpioPointsAttempted;

	public ServerGpioTask(ILogger logger, int deviceId, IotDatabase database)
	{
		_logger = logger;
		_deviceId = deviceId;
		_database = database;
		InitialiseGpioController();
	}

	public string Name => "server.gpio";

	public TimeSpan Interval => TimeSpan.FromSeconds(10);

	private void InitialiseGpioController()
	{
		try
		{
			_gpioController = new GpioController();
			_logger.LogInformation("Server initialised GPIO controller.");
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Server GPIO controller is not available on this device.");
			_gpioController = null;
		}
	}

	private void InitialiseGpioPoints()
	{
		if (_initialiseGpioPointsAttempted)
		{
			return;
		}

		_initialiseGpioPointsAttempted = true;
		using AppDbContext dbContext = _database.CreateDbContext();
		var points = dbContext.Points
			.AsNoTracking()
			.Where(point => point.DeviceId == _deviceId)
			.OrderBy(point => point.Id)
			.Select(point => new
			{
				point.Id,
				point.Name,
				point.TypeId,
				point.Address,
				point.Status,
				point.Status0,
				point.Status1
			})
			.ToArray();

		foreach (var point in points)
		{
			GpioPoint gpioPoint = new(
				point.Id,
				point.Name,
				point.TypeId,
				point.Address,
				point.Status,
				point.Status0,
				point.Status1);

			try
			{
				InitialisePoint(gpioPoint);
				_points[point.Id] = gpioPoint;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Server point {PointId} ({PointName}) could not be initialised from address '{PointAddress}'.",
					point.Id, point.Name, point.Address);
			}
		}

		_logger.LogInformation("Server initialised {GpioPointCount} GPIO points for device {DeviceId}.",
			_points.Count, _deviceId);
	}

	public async Task ExecuteAsync(PeerServerLoopContext context, CancellationToken cancellationToken)
	{
		InitialiseGpioPoints();

		if (_points.Count == 0)
		{
			_logger.LogDebug("Server has no initialised points to poll.");
			return;
		}

		List<PointStatus> changedStatuses = [];
		foreach (GpioPoint point in _points.Values.OrderBy(point => point.Id))
		{
			cancellationToken.ThrowIfCancellationRequested();

			string status = PollPoint(point);
			if (string.Equals(point.CurrentStatus, status, StringComparison.Ordinal))
				continue;

			_logger.LogDebug("Server point changed. {PointName} ({PointId}): {PreviousStatus} -> {Status}.",
				point.Name, point.Id, point.CurrentStatus, status);

			point.CurrentStatus = status;
			changedStatuses.Add(PeerMessages.CreatePointStatus(point.Id, status));
		}

		if (changedStatuses.Count == 0)
		{
			_logger.LogDebug("Server polled {GpioPointCount} points with no status changes.",
				_points.Count);
			return;
		}

		await SaveChangedStatusesAsync(changedStatuses, cancellationToken);

		if (context.ConnectedPeerCount == 0)
		{
			_logger.LogInformation("Server updated {ChangedPointCount} changed points but has no connected peers.",
				changedStatuses.Count);
			return;
		}

		foreach (PointStatus pointStatus in changedStatuses)
		{
			cancellationToken.ThrowIfCancellationRequested();

			_logger.LogInformation("Server sending changed point status to ({ConnectedClientCount} clients, {ConnectedServerCount} servers). Point {PointId}: {Status}.",
				context.ConnectedClientCount, context.ConnectedServerCount, pointStatus.Id, pointStatus.Status);

			await context.SendToConnectedPeersAsync(PeerMessages.PointStatusType, pointStatus, cancellationToken);
		}
	}

	private async Task SaveChangedStatusesAsync(IEnumerable<PointStatus> changedStatuses, CancellationToken cancellationToken)
	{
		await using AppDbContext dbContext = _database.CreateDbContext();

		foreach (PointStatus pointStatus in changedStatuses)
		{
			Point? dbPoint = await dbContext.Points.FindAsync([pointStatus.Id], cancellationToken);
			if (dbPoint is null)
			{
				_logger.LogWarning("Server could not update unknown point {PointId}: {Status}.",
					pointStatus.Id, pointStatus.Status);
				continue;
			}

			dbPoint.Status = pointStatus.Status;
			dbPoint.TimeStamp = DateTime.UtcNow;
		}

		await dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task<PointStatus?> TryHandlePointControlAsync(PointControl pointControl, CancellationToken cancellationToken)
	{
		InitialiseGpioPoints();

		await using AppDbContext dbContext = _database.CreateDbContext();
		Point? dbPoint = await dbContext.Points.FindAsync([pointControl.Id], cancellationToken);
		if (dbPoint is null || dbPoint.DeviceId != _deviceId)
			return null;

		if (dbPoint.TypeId != ePointType.eDigitalOutput)
		{
			_logger.LogWarning("Server rejected point control for non-digital-output point {PointId} ({PointName}) on device {DeviceId}.",
				dbPoint.Id, dbPoint.Name, _deviceId);
			return PeerMessages.CreatePointStatus(dbPoint.Id, dbPoint.Status);
		}

		if (!_points.TryGetValue(dbPoint.Id, out GpioPoint? gpioPoint))
		{
			_logger.LogWarning("Server point control could not find initialised point {PointId} ({PointName}) on device {DeviceId}.",
				dbPoint.Id, dbPoint.Name, _deviceId);
			return PeerMessages.CreatePointStatus(dbPoint.Id, dbPoint.Status);
		}

		string status = NormaliseControlStatus(pointControl.Status, dbPoint.Status0, dbPoint.Status1);
		bool active = IsActiveStatus(status, dbPoint.Status1);

		if (gpioPoint.Pin is null)
		{
			_logger.LogWarning("Server GPIO point {PointId} ({PointName}) has no open GPIO pin; updating database status only.",
				dbPoint.Id, dbPoint.Name);
		}
		else
		{
			gpioPoint.Pin.Write(active ? PinValue.High : PinValue.Low);
		}

		gpioPoint.CurrentStatus = status;
		dbPoint.Status = status;
		dbPoint.TimeStamp = DateTime.UtcNow;
		await dbContext.SaveChangesAsync(cancellationToken);

		_logger.LogInformation("Server controlled output point {PointId} ({PointName}) on device {DeviceId}: {Status}.",
			dbPoint.Id, dbPoint.Name, _deviceId, status);

		return PeerMessages.CreatePointStatus(dbPoint.Id, status);
	}

	private void InitialisePoint(GpioPoint point)
	{
		switch (point.TypeId)
		{
			case ePointType.eDigitalInput:
				OpenGpioPin(point, PinMode.Input);
				break;

			case ePointType.eDigitalOutput:
				OpenGpioPin(point, PinMode.Output);
				break;

			case ePointType.ePwmOutput:
				InitialisePwmOutput(point);
				break;

			case ePointType.eTm1637:
				InitialiseTm1637(point);
				break;

			case ePointType.eBmp280:
				InitialiseI2cProbe(point);
				break;

			default:
				_logger.LogError("Server point {PointId} ({PointName}) uses unsupported point type {PointType}.",
					point.Id, point.Name, point.TypeId);
				break;
		}
	}

	private void OpenGpioPin(GpioPoint point, PinMode mode)
	{
		if (_gpioController is null)
		{
			return;
		}

		if (!TryReadPin(point.Address, out int pinNumber))
		{
			_logger.LogWarning("Server point {PointId} ({PointName}) has invalid GPIO pin address '{PointAddress}'.",
				point.Id, point.Name, point.Address);
			return;
		}

		point.Pin = _gpioController.OpenPin(pinNumber, mode);
	}

	private static bool TryReadPin(string address, out int pinNumber)
	{
		if (int.TryParse(address, out pinNumber))
		{
			return true;
		}

		Dictionary<string, string> values = ParseAddress(address);
		return values.TryGetValue("PIN", out string? pin) && int.TryParse(pin, out pinNumber);
	}

	private static Dictionary<string, string> ParseAddress(string address)
	{
		Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);
		foreach (string part in address.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
		{
			string[] pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
			if (pair.Length == 2)
			{
				values[pair[0]] = pair[1];
			}
		}

		return values;
	}

	private void InitialisePwmOutput(GpioPoint point)
	{
		Dictionary<string, string> address = ParseAddress(point.Address);
		int chip = ReadInt(address, "CHIP", 0);
		int channel = ReadInt(address, "CHANNEL", ReadInt(address, "CH", -1));
		if (channel < 0 && int.TryParse(point.Address, out int rawChannel))
		{
			channel = rawChannel;
		}

		if (channel < 0)
		{
			_logger.LogWarning("Server PWM point {PointId} ({PointName}) has invalid PWM address '{PointAddress}'.",
				point.Id, point.Name, point.Address);
			return;
		}

		try
		{
			point.PwmChannel = PwmChannel.Create(chip, channel, 400, 0);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Server PWM point {PointId} ({PointName}) could not open chip {PwmChip}, channel {PwmChannel}.",
				point.Id, point.Name, chip, channel);
		}
	}

	private void InitialiseTm1637(GpioPoint point)
	{
		Dictionary<string, string> address = ParseAddress(point.Address);
		if (!address.TryGetValue("DIO", out string? dioValue) ||
			!address.TryGetValue("CLK", out string? clkValue) ||
			!int.TryParse(dioValue, out int dioPin) ||
			!int.TryParse(clkValue, out int clkPin))
		{
			_logger.LogWarning("Server TM1637 point {PointId} ({PointName}) has invalid address '{PointAddress}'.",
				point.Id, point.Name, point.Address);
			return;
		}

		point.Tm1637 = new Tm1637(clkPin, dioPin);
	}

	private void InitialiseI2cProbe(GpioPoint point)
	{
		Dictionary<string, string> address = ParseAddress(point.Address);
		int busId = ReadInt(address, "BUS", 1);
		int deviceAddress = ReadInt(address, "ADDR", 0x76);

		try
		{
			point.I2cDevice = I2cDevice.Create(new I2cConnectionSettings(busId, deviceAddress));
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Server I2C point {PointId} ({PointName}) could not open bus {I2cBus}, address 0x{I2cAddress:X2}.",
				point.Id, point.Name, busId, deviceAddress);
		}
	}

	private static int ReadInt(IReadOnlyDictionary<string, string> values, string key, int fallback)
	{
		return values.TryGetValue(key, out string? value) && int.TryParse(value, out int result)
			? result
			: fallback;
	}

	private string PollPoint(GpioPoint point)
	{
		try
		{
			return point.TypeId switch
			{
				ePointType.eDigitalInput or ePointType.eDigitalOutput => PollDigitalPoint(point),
				ePointType.ePwmOutput => point.PwmChannel is null ? point.CurrentStatus : "Ready",
				ePointType.eTm1637 => point.Tm1637 is null ? point.CurrentStatus : (point.Tm1637.IsScreenOn ? "On" : "Off"),
				ePointType.eBmp280 => point.I2cDevice is null ? point.CurrentStatus : "Ready",
				_ => point.CurrentStatus
			};
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Server point {PointId} ({PointName}) poll failed.",
				point.Id, point.Name);
			return point.CurrentStatus;
		}
	}

	private static string PollDigitalPoint(GpioPoint point)
	{
		if (point.Pin is null)
		{
			return point.CurrentStatus;
		}

		PinValue value = point.Pin.Read();
		return value == PinValue.High
			? GetFallbackStatus(point.Status1, "On")
			: GetFallbackStatus(point.Status0, "Off");
	}

	private static string GetFallbackStatus(string status, string fallback)
	{
		return string.IsNullOrWhiteSpace(status) ? fallback : status;
	}

	private static string NormaliseControlStatus(string requestedStatus, string status0, string status1)
	{
		string offStatus = GetFallbackStatus(status0, "Off");
		string onStatus = GetFallbackStatus(status1, "On");

		if (string.Equals(requestedStatus, onStatus, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(requestedStatus, "On", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(requestedStatus, "High", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(requestedStatus, "True", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(requestedStatus, "1", StringComparison.OrdinalIgnoreCase))
		{
			return onStatus;
		}

		if (string.Equals(requestedStatus, offStatus, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(requestedStatus, "Off", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(requestedStatus, "Low", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(requestedStatus, "False", StringComparison.OrdinalIgnoreCase) ||
			string.Equals(requestedStatus, "0", StringComparison.OrdinalIgnoreCase))
		{
			return offStatus;
		}

		return requestedStatus;
	}

	private static bool IsActiveStatus(string status, string status1) =>
		string.Equals(status, GetFallbackStatus(status1, "On"), StringComparison.OrdinalIgnoreCase) ||
		string.Equals(status, "On", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(status, "High", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(status, "True", StringComparison.OrdinalIgnoreCase) ||
		string.Equals(status, "1", StringComparison.OrdinalIgnoreCase);

	private sealed class GpioPoint
	{
		public GpioPoint(int id, string name, ePointType typeId, string address, string currentStatus, string status0, string status1)
		{
			Id = id;
			Name = name;
			TypeId = typeId;
			Address = address;
			CurrentStatus = currentStatus;
			Status0 = status0;
			Status1 = status1;
		}

		public int Id { get; }

		public string Name { get; }

		public ePointType TypeId { get; }

		public string Address { get; }

		public string CurrentStatus { get; set; }

		public string Status0 { get; }

		public string Status1 { get; }

		public GpioPin? Pin { get; set; }

		public PwmChannel? PwmChannel { get; set; }

		public Tm1637? Tm1637 { get; set; }

		public I2cDevice? I2cDevice { get; set; }
	}
}
