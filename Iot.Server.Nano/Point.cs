namespace Iot.Server.Nano
{

	public enum PointType
	{
		DigitalInput = 1,
		DigitalOutput = 2,
		AnalogInput = 3,
		PwmOutput = 4,
		Tm1637 = 5,
		Bmp280 = 6,
		ShiftInput = 7
	}

	public sealed class Point
	{
		public Point(int id, int deviceId, string name, PointType type, string address)
		{
			Id = id;
			DeviceId = deviceId;
			Name = name;
			Type = type;
			Address = address;
			Status = string.Empty;
			Status0 = string.Empty;
			Status1 = string.Empty;
			Units = string.Empty;
			Scale = 1;
		}

		public int Id { get; private set; }
		public int DeviceId { get; private set; }
		public string Name { get; private set; }
		public PointType Type { get; private set; }
		public string Address { get; private set; }
		public string Status { get; set; }
		public string Status0 { get; set; }
		public string Status1 { get; set; }
		public string Units { get; set; }
		public double Scale { get; set; }
	}
}
