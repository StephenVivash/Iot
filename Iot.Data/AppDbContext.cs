using Iot.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Iot.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Device> Devices => Set<Device>();

    public DbSet<Group> Groups => Set<Group>();

    public DbSet<GroupPoint> GroupPoints => Set<GroupPoint>();

    public DbSet<Point> Points => Set<Point>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>(entity =>
        {
            entity.Property(device => device.Name).HasMaxLength(200);
            entity.Property(device => device.Description).HasMaxLength(500);
            entity.Property(device => device.Status).HasMaxLength(50);
            entity.HasData(
                new Device
                {
                    Id = 1,
                    ParentDeviceId = 0,
                    Name = "Main NET Server",
                    Description = "Primary network server for the demo site",
                    TypeId = (int)eDeviceType.eNetServer,
                    Status = "Online"
                },
                new Device
                {
                    Id = 2,
                    ParentDeviceId = 1,
                    Name = "Nano Controller 1",
                    Description = "Local nano controller handling plant room IO",
                    TypeId = (int)eDeviceType.eNanoServer,
                    Status = "Online"
                },
                new Device
                {
                    Id = 3,
                    ParentDeviceId = 1,
                    Name = "LoRa Gateway",
                    Description = "Wireless LoRa gateway for remote sensors",
                    TypeId = (int)eDeviceType.eLoRaServer,
                    Status = "Warning"
                },
                new Device
                {
                    Id = 4,
                    ParentDeviceId = 2,
                    Name = "Operator Panel",
                    Description = "Client display for local operators",
                    TypeId = (int)eDeviceType.eClient,
                    Status = "Online"
                });
        });

        modelBuilder.Entity<Point>(entity =>
        {
            entity.Property(point => point.Name).HasMaxLength(200);
            entity.Property(point => point.Description).HasMaxLength(500);
            entity.Property(point => point.Address).HasMaxLength(100);
            entity.Property(point => point.Status).HasMaxLength(50);
            entity.Property(point => point.Status0).HasMaxLength(100);
            entity.Property(point => point.Status1).HasMaxLength(100);
            entity.Property(point => point.Units).HasMaxLength(50);
            entity.HasOne(point => point.Device)
                .WithMany(device => device.Points)
                .HasForeignKey(point => point.DeviceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasData(
                new Point
                {
                    Id = 1,
                    DeviceId = 2,
                    Name = "Pump Run Feedback",
                    Description = "Digital input showing the pump run state",
                    TypeId = ePointType.eDigitalInput,
                    Address = "DI:1",
                    Status = "On",
                    RawStatus = 1,
                    Status0 = "Stopped",
                    Status1 = "Running",
                    Scale = 1,
                    Units = string.Empty,
                    TimeStamp = new DateTime(2026, 4, 24, 8, 0, 0, DateTimeKind.Utc)
                },
                new Point
                {
                    Id = 2,
                    DeviceId = 2,
                    Name = "Pump Command",
                    Description = "Digital output command for pump start",
                    TypeId = ePointType.eDigitalOutput,
                    Address = "DO:1",
                    Status = "On",
                    RawStatus = 1,
                    Status0 = "Off",
                    Status1 = "On",
                    Scale = 1,
                    Units = string.Empty,
                    TimeStamp = new DateTime(2026, 4, 24, 8, 0, 5, DateTimeKind.Utc)
                },
                new Point
                {
                    Id = 3,
                    DeviceId = 3,
                    Name = "Tank Level",
                    Description = "Analog tank level from remote sensor",
                    TypeId = ePointType.eAnalogInput,
                    Address = "AI:1",
                    Status = "68.4",
                    RawStatus = 684,
                    Status0 = "Low",
                    Status1 = "High",
                    Scale = 0.1,
                    Units = "%",
                    TimeStamp = new DateTime(2026, 4, 24, 8, 1, 0, DateTimeKind.Utc)
                },
                new Point
                {
                    Id = 4,
                    DeviceId = 2,
                    Name = "Valve Position",
                    Description = "Analog output controlling valve opening",
                    TypeId = ePointType.eAnalogOutput,
                    Address = "AO:1",
                    Status = "45.0",
                    RawStatus = 450,
                    Status0 = "Closed",
                    Status1 = "Open",
                    Scale = 0.1,
                    Units = "%",
                    TimeStamp = new DateTime(2026, 4, 24, 8, 1, 30, DateTimeKind.Utc)
                });
        });

        modelBuilder.Entity<Group>(entity =>
        {
            entity.Property(group => group.Name).HasMaxLength(200);
            entity.Property(group => group.Description).HasMaxLength(500);
            entity.HasData(
                new Group
                {
                    Id = 1,
                    Name = "Plant Room",
                    Description = "Points used to monitor and control plant room equipment"
                },
                new Group
                {
                    Id = 2,
                    Name = "Remote Sensors",
                    Description = "Wireless sensor points reported through the LoRa gateway"
                });
        });

        modelBuilder.Entity<GroupPoint>(entity =>
        {
            entity.HasOne(groupPoint => groupPoint.Group)
                .WithMany(group => group.GroupPoints)
                .HasForeignKey(groupPoint => groupPoint.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(groupPoint => groupPoint.Point)
                .WithMany(point => point.GroupPoints)
                .HasForeignKey(groupPoint => groupPoint.PointId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(groupPoint => new { groupPoint.GroupId, groupPoint.PointId }).IsUnique();
            entity.HasData(
                new GroupPoint
                {
                    Id = 1,
                    GroupId = 1,
                    PointId = 1
                },
                new GroupPoint
                {
                    Id = 2,
                    GroupId = 1,
                    PointId = 2
                },
                new GroupPoint
                {
                    Id = 3,
                    GroupId = 1,
                    PointId = 4
                },
                new GroupPoint
                {
                    Id = 4,
                    GroupId = 2,
                    PointId = 3
                });
        });
    }
}
