using System.Collections;

namespace Iot.Server.Nano
{
	public sealed class PointStore
	{
		private readonly Point[] _points;

		public PointStore(Point[] points)
		{
			_points = points;
		}

		public Point Find(int id)
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

		public Point[] GetForDevice(int deviceId)
		{
			ArrayList matches = new ArrayList();
			for (int i = 0; i < _points.Length; i++)
			{
				if (_points[i].DeviceId == deviceId)
				{
					matches.Add(_points[i]);
				}
			}

			Point[] result = new Point[matches.Count];
			for (int i = 0; i < matches.Count; i++)
			{
				result[i] = (Point)matches[i];
			}

			return result;
		}

		public static PointStore CreateDefault()
		{
			Point[] points = new Point[]
			{
				WithStatuses(new Point(1, 1, "Pi51 Led1", PointType.DigitalOutput, "18"), "Off", "On"),
				new Point(2, 2, "Segment", PointType.Tm1637, "DIO=23,CLK=22"),
				WithStatuses(new Point(3, 7, "Radar", PointType.DigitalInput, "27"), "Ready", "Alert"),
				WithStatuses(new Point(4, 7, "6502", PointType.DigitalInput, "13"), "Low", "High"),
				WithStatuses(new Point(5, 10, "Pwm1", PointType.PwmOutput, "4"), "Off", "On"),
				WithScale(new Point(6, 7, "Supply", PointType.AnalogInput, "PIN=35"), 0.000805, "Volts"),
				WithScale(new Point(7, 7, "Cpu Temp", PointType.AnalogInput, "PIN=34"), 0.02444, "C"),
				WithStatuses(new Point(8, 11, "Lora1 Led1", PointType.DigitalOutput, "4"), "Off", "On"),
				WithStatuses(new Point(9, 12, "Lora2 Led1", PointType.DigitalOutput, "4"), "Off", "On"),
				new Point(10, 2, "Bmp280", PointType.Bmp280, "SDA=3,SCL=2"),
				WithStatuses(new Point(11, 10, "Pwm2", PointType.PwmOutput, "5"), "Off", "On"),
				WithStatuses(new Point(12, 7, "6501", PointType.DigitalOutput, "26"), "Low", "High"),
				WithStatuses(new Point(13, 9, "6401", PointType.DigitalOutput, "4"), "Low", "High"),
				WithStatuses(new Point(14, 9, "6402", PointType.DigitalInput, "5"), "Low", "High"),
				WithScale(new Point(15, 2, "Temperature", PointType.AnalogInput, "PID=10,TYP=TEM"),  1, "°C"),
				WithScale(new Point(16, 2, "Pressure", PointType.AnalogInput, "PID=10,TYP=PRE"), 1, "mBars"),
				WithScale(new Point(17, 2, "Altitude", PointType.AnalogInput, "PID=10,TYP=ALT"), 1, "Meters"),
				new Point(18, 7, "Bmp280", PointType.Bmp280, "SDA=21,SCL=22"),
				WithScale(new Point(19, 7, "Temperature", PointType.AnalogInput, "PID=18,TYP=TEM"), 1, "C"),
				WithScale(new Point(20, 7, "Pressure", PointType.AnalogInput, "PID=18,TYP=PRE"), 1, "mBars"),
				WithScale(new Point(21, 7, "Altitude", PointType.AnalogInput, "PID=18,TYP=ALT"), 1, "Meters"),
				new Point(22, 7, "ShiftInput", PointType.ShiftInput, "LAT=12,CLK=25,DAT=33"),
				WithStatuses(new Point(23, 7, "Shift Bit0", PointType.DigitalInput, "PID=22,BIT=0"), "Off", "On"),
				WithStatuses(new Point(24, 7, "Shift Bit5", PointType.DigitalInput, "PID=22,BIT=5"), "Off", "On"),
				WithStatuses(new Point(25, 2, "Pi31 Led1", PointType.DigitalOutput, "18"), "Off", "On"),
				WithStatuses(new Point(26, 3, "Piz21 Led1", PointType.DigitalOutput, "18"), "Off", "On"),
				WithStatuses(new Point(27, 1, "Pi51 Light1S", PointType.DigitalInput, "19"), "Off", "On"),
				WithStatuses(new Point(28, 1, "Pi51 Light1C", PointType.DigitalOutput, "20"), "Off", "On"),
				WithStatuses(new Point(29, 3, "Piz21 Light1S", PointType.DigitalInput, "19"), "Off", "On"),
				WithStatuses(new Point(30, 3, "Piz21 Light1C", PointType.DigitalOutput, "20"), "Off", "On"),
				WithStatuses(new Point(31, 8, "Light1", PointType.DigitalInput, "4"), "Off", "On"),
				WithStatuses(new Point(32, 8, "Led1", PointType.DigitalOutput, "5"), "Off", "On"),
			};

			return new PointStore(points);
		}

		private static Point WithStatuses(Point point, string status0, string status1)
		{
			point.Status0 = status0;
			point.Status1 = status1;
			return point;
		}

		private static Point WithScale(Point point, double scale, string units)
		{
			point.Scale = scale;
			point.Units = units;
			return point;
		}
	}
}
