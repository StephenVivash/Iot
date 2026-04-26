namespace Iot.Data.Models;

public sealed class Device
{
    public int Id { get; set; }

    public int ParentDeviceId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int TypeId { get; set; }

    public string Status { get; set; } = string.Empty;

    public List<Point> Points { get; set; } = [];
}
