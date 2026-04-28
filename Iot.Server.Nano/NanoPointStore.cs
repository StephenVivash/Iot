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
				WithStatuses(new PointDefinition(1, 1, "Pi51 Led1", PointType.DigitalOutput, "18"), "Off", "On"),
				new PointDefinition(2, 2, "Segment", PointType.Tm1637, "DIO=23,CLK=22"),
				WithStatuses(new PointDefinition(3, 7, "Radar", PointType.DigitalInput, "27"), "Ready", "Alert"),
				WithStatuses(new PointDefinition(4, 7, "6502", PointType.DigitalInput, "13"), "Low", "High"),
				WithStatuses(new PointDefinition(5, 10, "Pwm1", PointType.PwmOutput, "4"), "Off", "On"),
				WithScale(new PointDefinition(6, 7, "Supply", PointType.AnalogInput, "PIN=35"), 0.000805, "Volts"),
				WithScale(new PointDefinition(7, 7, "Cpu Temp", PointType.AnalogInput, "PIN=34"), 0.02444, "C"),
				WithStatuses(new PointDefinition(8, 12, "LoRa1 Led1", PointType.DigitalOutput, "4"), "Off", "On"),
				WithStatuses(new PointDefinition(9, 11, "LoRa2 Led1", PointType.DigitalOutput, "4"), "Off", "On"),
				new PointDefinition(10, 2, "Bmp280", PointType.Bmp280, "SDA=3,SCL=2"),
				WithStatuses(new PointDefinition(11, 10, "Pwm2", PointType.PwmOutput, "5"), "Off", "On"),
				WithStatuses(new PointDefinition(12, 7, "6501", PointType.DigitalOutput, "26"), "Low", "High"),
				WithStatuses(new PointDefinition(13, 9, "6401", PointType.DigitalOutput, "4"), "Low", "High"),
				WithStatuses(new PointDefinition(14, 9, "6402", PointType.DigitalInput, "5"), "Low", "High"),
				WithScale(new PointDefinition(15, 2, "Temperature", PointType.AnalogInput, "PID=10,TYP=TEM"),  1, "°C"),
				WithScale(new PointDefinition(16, 2, "Pressure", PointType.AnalogInput, "PID=10,TYP=PRE"), 1, "mBars"),
				WithScale(new PointDefinition(17, 2, "Altitude", PointType.AnalogInput, "PID=10,TYP=ALT"), 1, "Meters"),
				new PointDefinition(18, 7, "Bmp280", PointType.Bmp280, "SDA=21,SCL=22"),
				WithScale(new PointDefinition(19, 7, "Temperature", PointType.AnalogInput, "PID=18,TYP=TEM"), 1, "C"),
				WithScale(new PointDefinition(20, 7, "Pressure", PointType.AnalogInput, "PID=18,TYP=PRE"), 1, "mBars"),
				WithScale(new PointDefinition(21, 7, "Altitude", PointType.AnalogInput, "PID=18,TYP=ALT"), 1, "Meters"),
				new PointDefinition(22, 7, "ShiftInput", PointType.ShiftInput, "LAT=12,CLK=25,DAT=33"),
				WithStatuses(new PointDefinition(23, 7, "Shift Bit0", PointType.DigitalInput, "PID=22,BIT=0"), "Off", "On"),
				WithStatuses(new PointDefinition(24, 7, "Shift Bit5", PointType.DigitalInput, "PID=22,BIT=5"), "Off", "On"),
				WithStatuses(new PointDefinition(25, 2, "Pi31 Led1", PointType.DigitalOutput, "18"), "Off", "On"),
				WithStatuses(new PointDefinition(26, 3, "Piz21 Led1", PointType.DigitalOutput, "18"), "Off", "On"),
				WithStatuses(new PointDefinition(27, 1, "Pi51 Light1S", PointType.DigitalInput, "19"), "Off", "On"),
				WithStatuses(new PointDefinition(28, 1, "Pi51 Light1C", PointType.DigitalOutput, "20"), "Off", "On"),
				WithStatuses(new PointDefinition(29, 3, "Piz21 Light1S", PointType.DigitalInput, "19"), "Off", "On"),
				WithStatuses(new PointDefinition(30, 3, "Piz21 Light1C", PointType.DigitalOutput, "20"), "Off", "On"),
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
