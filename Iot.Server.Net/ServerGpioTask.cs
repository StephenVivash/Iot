using Iot.Device.Tm16xx;
using Microsoft.Extensions.Logging;
using PeerJsonSockets;
using System.Device.Gpio;
using System.Device.I2c;
using System.Device.Pwm;

namespace Iot.Server.Net;

internal sealed class	ServerGpioTask : IPeerServerLoopTask
{
	private readonly ILogger _logger;
	private GpioController? gpioController = null;
	private GpioPin? pin = null;
	private Tm1637? tm1637 = null;
	private I2cConnectionSettings? settings = null;
	private I2cDevice? i2cdevice = null;
	//private Bmp280? bmp280 = null;
	private PwmChannel? pwmPin = null;

	public ServerGpioTask(ILogger logger)
	{
		_logger = logger;
		InitialiseGpio();
	}

	public string Name => "server.gpio";

	public TimeSpan Interval => TimeSpan.FromSeconds(10);

	public Task ExecuteAsync(PeerServerLoopContext context, CancellationToken cancellationToken)
	{

		if (gpioController != null) {

			PinMode mode = pin!.GetPinMode();
			PinValue value = pin.Read();
			bool screen = tm1637!.IsScreenOn;
			var info1 = i2cdevice!.QueryComponentInformation();
			var info2 = pwmPin.QueryComponentInformation();
			_logger.LogInformation("Server GPIO task Pin: 10 {mode} {value}, I2C {info1} , PWM {info2}",
				mode, value, info1, info2);
		}
		return Task.CompletedTask;
	}

	private void InitialiseGpio()
	{
		_logger.LogInformation("Initialising GpioController");
		gpioController = new(); // PinNumberingScheme.Logical
		pin = gpioController.OpenPin(10, PinMode.Input);

		tm1637 = new(11, 12);
		settings = new I2cConnectionSettings(1, 0);
		i2cdevice = I2cDevice.Create(settings);
		//bmp280 = new(i2cdevice);
		pwmPin = PwmChannel.Create(0, 0, 400, 0.5);
	}
}
