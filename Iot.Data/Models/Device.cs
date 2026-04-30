namespace Iot.Data.Models;

public enum eDeviceType
{
	eNetServer,
	eNanoServer,
	eLoRaServer,
	eClient
}

public sealed class Device
{
    public Device()
    {
    }

    public Device(int id, int parentDeviceId, string name, string description, eDeviceType typeId, string status)
    {
        Id = id;
        ParentDeviceId = parentDeviceId;
        Name = name;
        Description = description;
        TypeId = (int)typeId;
        Status = status;
    }

    public int Id { get; set; }

    public int ParentDeviceId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int TypeId { get; set; }

    public string Status { get; set; } = string.Empty;

    public List<Point> Points { get; set; } = [];
}
