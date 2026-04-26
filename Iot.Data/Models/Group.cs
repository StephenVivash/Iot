namespace Iot.Data.Models;

public sealed class Group
{
    public Group()
    {
    }

    public Group(int id, string name, string description)
    {
        Id = id;
        Name = name;
        Description = description;
    }

    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<GroupPoint> GroupPoints { get; set; } = [];
}
