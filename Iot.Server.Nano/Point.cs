namespace Iot.Server.Nano
{
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
