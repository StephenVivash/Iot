namespace Iot.Data.Models;

public sealed class Group
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<GroupPoint> GroupPoints { get; set; } = [];
}
