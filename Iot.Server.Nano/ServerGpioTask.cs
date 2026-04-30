using System;
using System.Collections;
using System.Device.Adc;
using System.Device.Gpio;
using System.Device.Pwm;
using PeerJsonSockets.Nano;

namespace Iot.Server.Nano
{
	public sealed class ServerGpioTask : IPeerServerLoopTask, IPeerPointControlHandler
	{
		private readonly NanoLog _log;
		private readonly int _deviceId;
		private readonly PointStore _pointStore;
		private readonly Hashtable _points = new Hashtable();
		private readonly GpioController _gpioController;
		private AdcController _adcController;
		private bool _initialiseGpioPointsAttempted;

		public ServerGpioTask(NanoLog log, int deviceId, PointStore pointStore)
		{
			_log = log;
			_deviceId = deviceId;
			_pointStore = pointStore;
			_gpioController = new GpioController();
		}

		public string Name
		{
			get { return "server.gpio"; }
		}

		public int IntervalMilliseconds
		{
			get { return 10 * 1000; }
		}

		public void Execute(PeerServerLoopContext context)
		{
			InitialiseGpioPoints();
			if (_points.Count == 0)
			{
				_log.Info("Server GPIO has no initialised points to poll.");
				return;
			}

			ArrayList changedStatuses = new ArrayList();
			foreach (GpioPoint point in _points.Values)
			{
				string status = PollPoint(point);
				if (point.CurrentStatus == status)
				{
					continue;
				}

				_log.Info("Server GPIO point changed. " + point.Name + " (" + point.Id.ToString() + "): " + point.CurrentStatus + " -> " + status + ".");
				point.CurrentStatus = status;
				point.Definition.Status = status;
				changedStatuses.Add(PeerMessages.CreatePointStatus(point.Id, status));
			}

			for (int i = 0; i < changedStatuses.Count; i++)
			{
				PointStatus pointStatus = (PointStatus)changedStatuses[i];
				context.SendToConnectedPeers(PeerMessages.PointStatusType, pointStatus);
			}
		}

		public PointStatus TryHandlePointControl(PointControl pointControl)
		{
			InitialiseGpioPoints();
			Point point = _pointStore.Find(pointControl.id);
			if (point == null || point.DeviceId != _deviceId)
			{
				return null;
			}

			if (point.Type != PointType.DigitalOutput && point.Type != PointType.PwmOutput)
			{
				_log.Warn("Server GPIO rejected point control for non-output point " + point.Id.ToString() + ".");
				return PeerMessages.CreatePointStatus(point.Id, point.Status);
			}

			GpioPoint gpioPoint = (GpioPoint)_points[point.Id];
			if (gpioPoint == null)
			{
				_log.Warn("Server GPIO point control could not find initialised point " + point.Id.ToString() + ".");
				return PeerMessages.CreatePointStatus(point.Id, point.Status);
			}

			string status = NormaliseControlStatus(pointControl.status, point.Status0, point.Status1);
			bool active = IsActiveStatus(status, point.Status1);

			if (gpioPoint.Pin != null)
			{
				gpioPoint.Pin.Write(active ? PinValue.High : PinValue.Low);
			}

			if (gpioPoint.PwmChannel != null)
			{
				gpioPoint.PwmChannel.DutyCycle = active ? 1.0 : 0.0;
				if (active)
				{
					gpioPoint.PwmChannel.Start();
				}
				else
				{
					gpioPoint.PwmChannel.Stop();
				}
			}

			gpioPoint.CurrentStatus = status;
			point.Status = status;
			_log.Info("Server GPIO controlled output point " + point.Id.ToString() + ": " + status + ".");
			return PeerMessages.CreatePointStatus(point.Id, status);
		}

		private void InitialiseGpioPoints()
		{
			if (_initialiseGpioPointsAttempted)
			{
				return;
			}

			_initialiseGpioPointsAttempted = true;
			Point[] points = _pointStore.GetForDevice(_deviceId);
			for (int i = 0; i < points.Length; i++)
			{
				GpioPoint gpioPoint = new GpioPoint(points[i]);
				try
				{
					InitialisePoint(gpioPoint);
					_points[points[i].Id] = gpioPoint;
				}
				catch (Exception ex)
				{
					_log.Error("Server GPIO point " + points[i].Id.ToString() + " (" + points[i].Name + ") could not be initialised from address '" + points[i].Address + "'.", ex);
				}
			}

			_log.Info("Server initialised " + _points.Count.ToString() + " GPIO points for device " + _deviceId.ToString() + ".");
		}

		private void InitialisePoint(GpioPoint point)
		{
			if (point.Type == PointType.DigitalInput)
			{
				OpenGpioPin(point, PinMode.Input);
			}
			else if (point.Type == PointType.DigitalOutput)
			{
				OpenGpioPin(point, PinMode.Output);
			}
			else if (point.Type == PointType.PwmOutput)
			{
				InitialisePwmOutput(point);
			}
			else if (point.Type == PointType.AnalogInput)
			{
				InitialiseAnalogInput(point);
			}
		}

		private void OpenGpioPin(GpioPoint point, PinMode mode)
		{
			int pinNumber = ReadPin(point.Address);
			if (pinNumber < 0)
			{
				_log.Warn("Server GPIO point " + point.Id.ToString() + " has invalid GPIO pin address '" + point.Address + "'.");
				return;
			}

			if (_gpioController.IsPinOpen(pinNumber))
			{
				_log.Warn("Server GPIO point " + point.Id.ToString() + " pin " + pinNumber.ToString() + " is already open.");
				return;
			}

			point.Pin = _gpioController.OpenPin(pinNumber, mode);
		}

		private void InitialisePwmOutput(GpioPoint point)
		{
			int channel = ReadPin(point.Address);
			if (channel < 0)
			{
				_log.Warn("Server PWM point " + point.Id.ToString() + " has invalid address '" + point.Address + "'.");
				return;
			}

			point.PwmChannel = PwmChannel.CreateFromPin(channel, 400, 0);
		}

		private void InitialiseAnalogInput(GpioPoint point)
		{
			if (_adcController == null)
			{
				_adcController = new AdcController();
			}

			int channel = ReadAnalogChannel(point.Address);
			if (channel < 0)
			{
				_log.Warn("Server ADC point " + point.Id.ToString() + " has invalid address '" + point.Address + "'.");
				return;
			}

			if (channel >= _adcController.ChannelCount)
			{
				_log.Warn("Server ADC point " + point.Id.ToString() + " channel " + channel.ToString() + " is outside available channel count " + _adcController.ChannelCount.ToString() + ".");
				return;
			}

			point.AdcController = _adcController;
			point.AdcChannel = _adcController.OpenChannel(channel);
		}

		private string PollPoint(GpioPoint point)
		{
			try
			{
				if (point.Type == PointType.DigitalInput || point.Type == PointType.DigitalOutput)
				{
					return PollDigitalPoint(point);
				}

				if (point.Type == PointType.PwmOutput)
				{
					return point.PwmChannel == null ? point.CurrentStatus : "Ready";
				}

				if (point.Type == PointType.AnalogInput && point.AdcChannel != null)
				{
					double value = point.AdcChannel.ReadValue() * point.Definition.Scale;
					return value.ToString("N2");
				}

				return point.CurrentStatus;
			}
			catch (Exception ex)
			{
				_log.Error("Server GPIO point " + point.Id.ToString() + " poll failed.", ex);
				return point.CurrentStatus;
			}
		}

		private static string PollDigitalPoint(GpioPoint point)
		{
			if (point.Pin == null)
			{
				return point.CurrentStatus;
			}

			return point.Pin.Read() == PinValue.High
				? GetFallbackStatus(point.Status1, "On")
				: GetFallbackStatus(point.Status0, "Off");
		}

		private static int ReadPin(string address)
		{
			try
			{
				return int.Parse(address);
			}
			catch
			{
			}

			string pin = ReadAddressValue(address, "PIN");
			if (pin.Length == 0)
			{
				pin = ReadAddressValue(address, "CH");
			}

			if (pin.Length == 0)
			{
				pin = ReadAddressValue(address, "CHANNEL");
			}

			if (pin.Length == 0)
			{
				return -1;
			}

			try
			{
				return int.Parse(pin);
			}
			catch
			{
				return -1;
			}
		}

		private static int ReadAnalogChannel(string address)
		{
			string channel = ReadAddressValue(address, "CH");
			if (channel.Length == 0)
			{
				channel = ReadAddressValue(address, "CHANNEL");
			}

			if (channel.Length > 0)
			{
				return ReadInt(channel);
			}

			int pinNumber;
			string pin = ReadAddressValue(address, "PIN");
			if (pin.Length > 0)
			{
				pinNumber = ReadInt(pin);
			}
			else
			{
				pinNumber = ReadInt(address);
			}

			int esp32Channel = MapEsp32Adc1PinToChannel(pinNumber);
			return esp32Channel >= 0 ? esp32Channel : pinNumber;
		}

		private static int ReadInt(string value)
		{
			try
			{
				return int.Parse(value);
			}
			catch
			{
				return -1;
			}
		}

		private static int MapEsp32Adc1PinToChannel(int pinNumber)
		{
			switch (pinNumber)
			{
				case 36:
					return 0;
				case 37:
					return 1;
				case 38:
					return 2;
				case 39:
					return 3;
				case 32:
					return 4;
				case 33:
					return 5;
				case 34:
					return 6;
				case 35:
					return 7;
				default:
					return -1;
			}
		}

		private static string ReadAddressValue(string address, string key)
		{
			string[] parts = address.Split(',');
			for (int i = 0; i < parts.Length; i++)
			{
				int equals = parts[i].IndexOf('=');
				if (equals <= 0)
				{
					continue;
				}

				string partKey = parts[i].Substring(0, equals).Trim();
				if (partKey.ToUpper() == key)
				{
					return parts[i].Substring(equals + 1).Trim();
				}
			}

			return string.Empty;
		}

		private static string GetFallbackStatus(string status, string fallback)
		{
			return status == null || status.Length == 0 ? fallback : status;
		}

		private static string NormaliseControlStatus(string requestedStatus, string status0, string status1)
		{
			string offStatus = GetFallbackStatus(status0, "Off");
			string onStatus = GetFallbackStatus(status1, "On");
			string requested = requestedStatus == null ? string.Empty : requestedStatus.ToUpper();

			if (requested == onStatus.ToUpper() || requested == "ON" || requested == "HIGH" || requested == "TRUE" || requested == "1")
			{
				return onStatus;
			}

			if (requested == offStatus.ToUpper() || requested == "OFF" || requested == "LOW" || requested == "FALSE" || requested == "0")
			{
				return offStatus;
			}

			return requestedStatus;
		}

		private static bool IsActiveStatus(string status, string status1)
		{
			string active = GetFallbackStatus(status1, "On").ToUpper();
			string value = status == null ? string.Empty : status.ToUpper();
			return value == active || value == "ON" || value == "HIGH" || value == "TRUE" || value == "1";
		}

		private sealed class GpioPoint
		{
			public GpioPoint(Point definition)
			{
				Definition = definition;
				Id = definition.Id;
				Name = definition.Name;
				Type = definition.Type;
				Address = definition.Address;
				CurrentStatus = definition.Status;
				Status0 = definition.Status0;
				Status1 = definition.Status1;
			}

			public Point Definition;
			public int Id;
			public string Name;
			public PointType Type;
			public string Address;
			public string CurrentStatus;
			public string Status0;
			public string Status1;
			public GpioPin Pin;
			public PwmChannel PwmChannel;
			public AdcController AdcController;
			public AdcChannel AdcChannel;
		}
	}
}
