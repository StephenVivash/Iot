namespace Iot.Data.Models;

public sealed class GroupPoint
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public int PointId { get; set; }

    public Group Group { get; set; } = null!;

    public Point Point { get; set; } = null!;
}
