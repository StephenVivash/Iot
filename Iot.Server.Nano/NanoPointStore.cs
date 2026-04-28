using System.Collections;

namespace Iot.Server.Nano
{
	public sealed class NanoPointStore
	{
		private readonly PointDefinition[] _points;

		public NanoPointStore(PointDefinition[] points)
		{
			_points = points;
		}

		public PointDefinition Find(int id)
		{
			for (int i = 0; i < _points.Length; i++)
			{
				if (_points[i].Id == id)
				{
					return _points[i];
				}
			}

			return null;
		}

		public PointDefinition[] GetForDevice(int deviceId)
		{
			ArrayList matches = new ArrayList();
			for (int i = 0; i < _points.Length; i++)
			{
				if (_points[i].DeviceId == deviceId)
				{
					matches.Add(_points[i]);
				}
			}

			PointDefinition[] result = new PointDefinition[matches.Count];
			for (int i = 0; i < matches.Count; i++)
			{
				result[i] = (PointDefinition)matches[i];
			}

			return result;
		}

		public static NanoPointStore CreateDefault()
		{
			PointDefinition[] points = new PointDefinition[]
			{
				WithStatuses(new PointDefinition(3, 7, "Radar", PointType.DigitalInput, "27"), "Ready", "Alert"),
				WithStatuses(new PointDefinition(4, 7, "6502", PointType.DigitalInput, "13"), "Low", "High"),
				WithScale(new PointDefinition(6, 7, "Supply", PointType.AnalogInput, "PIN=35"), 0.000805, "Volts"),
				WithScale(new PointDefinition(7, 7, "Cpu Temp", PointType.AnalogInput, "PIN=34"), 0.02444, "C"),
				WithStatuses(new PointDefinition(12, 7, "6501", PointType.DigitalOutput, "26"), "Low", "High"),
				new PointDefinition(18, 7, "Bmp280", PointType.Bmp280, "SDA=21,SCL=22"),
				WithScale(new PointDefinition(19, 7, "Temperature", PointType.AnalogInput, "PID=18,TYP=TEM"), 1, "C"),
				WithScale(new PointDefinition(20, 7, "Pressure", PointType.AnalogInput, "PID=18,TYP=PRE"), 1, "mBars"),
				WithScale(new PointDefinition(21, 7, "Altitude", PointType.AnalogInput, "PID=18,TYP=ALT"), 1, "Meters"),
				WithStatuses(new PointDefinition(13, 9, "6401", PointType.DigitalOutput, "4"), "Low", "High"),
				WithStatuses(new PointDefinition(14, 9, "6402", PointType.DigitalInput, "5"), "Low", "High"),
				WithStatuses(new PointDefinition(5, 10, "Pwm1", PointType.PwmOutput, "4"), "Off", "On"),
				WithStatuses(new PointDefinition(11, 10, "Pwm2", PointType.PwmOutput, "5"), "Off", "On"),
				WithStatuses(new PointDefinition(8, 12, "LoRa1 Led1", PointType.DigitalOutput, "4"), "Off", "On"),
				WithStatuses(new PointDefinition(9, 11, "LoRa2 Led1", PointType.DigitalOutput, "4"), "Off", "On")
			};

			return new NanoPointStore(points);
		}

		private static PointDefinition WithStatuses(PointDefinition point, string status0, string status1)
		{
			point.Status0 = status0;
			point.Status1 = status1;
			return point;
		}

		private static PointDefinition WithScale(PointDefinition point, double scale, string units)
		{
			point.Scale = scale;
			point.Units = units;
			return point;
		}
	}
}
