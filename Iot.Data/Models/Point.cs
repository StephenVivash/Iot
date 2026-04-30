namespace Iot.Data.Models;

public enum ePointType
{
	eDigitalInput,
	eDigitalOutput,
	eAnalogInput,
	eAnalogOutput,
	ePwmOutput,
	eTm1637,
	eBmp280,
	eShiftInput,
	eShifOutput,
	eSequencer
}

public sealed class Point
{
    public Point()
    {
    }

    public Point(int id, int deviceId, string name, string description, ePointType typeId, string address)
    {
        Id = id;
        DeviceId = deviceId;
        Name = name;
        Description = description;
        TypeId = typeId;
        Address = address;
        Status = string.Empty;
        RawStatus = 0;
        Status0 = string.Empty;
        Status1 = string.Empty;
        Scale = 1;
        Units = string.Empty;
        TimeStamp = DateTime.UtcNow;
    }

    public int Id { get; set; }

    public int DeviceId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ePointType TypeId { get; set; }

    public string Address { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public double RawStatus { get; set; }

    public string Status0 { get; set; } = string.Empty;

    public string Status1 { get; set; } = string.Empty;

    public double Scale { get; set; }

    public string Units { get; set; } = string.Empty;

    public DateTime TimeStamp { get; set; }

    public Device Device { get; set; } = null!;

    public List<GroupPoint> GroupPoints { get; set; } = [];
}
