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
		});

		modelBuilder.Entity<Group>(entity =>
		{
			entity.Property(group => group.Name).HasMaxLength(200);
			entity.Property(group => group.Description).HasMaxLength(500);
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
		});
	}

	public void InitialiseDB()
	{
		GroupPoints.RemoveRange(GroupPoints);
		Groups.RemoveRange(Groups);
		Points.RemoveRange(Points);
		Devices.RemoveRange(Devices);
		SaveChanges();

		List<Device> devices =
		[
			new(1, 1, "pi51", "Raspberry Pi 5 server", eDeviceType.eNetServer, ""),
			new(2, 1, "pi31", "Raspberry Pi 3 server", eDeviceType.eNetServer, ""),
			new(3, 1, "piz21", "Raspberry Pi 2 Zero server", eDeviceType.eNetServer, ""),
			new(4, 1, "piz22", "Raspberry Pi 2 Zero server", eDeviceType.eNetServer, ""),
			new(5, 1, "koala", "Windows server", eDeviceType.eNetServer, ""),
			new(6, 1, "wallaby", "Windows server", eDeviceType.eNetServer, ""),
			new(7, 1, "nanodevice_FB2F18", "ESP32 Nano server", eDeviceType.eNanoServer, ""),
			new(8, 1, "nanodevice_1", "ESP32 Nano server", eDeviceType.eNanoServer, ""),
			new(9, 3, "nanodevice_2E3658", "ESP32-S3 Nano server", eDeviceType.eNanoServer, ""),
			new(10, 1, "nanodevice_3", "ESP32-S3 Nano server", eDeviceType.eNanoServer, ""),
			new(11, 1, "esp32-AD3260", "ESP32 LoRa server", eDeviceType.eLoRaServer, ""),
			new(12, 11, "esp32-AC85DC", "ESP32 LoRa server", eDeviceType.eLoRaServer, ""),
			new(13, 1, "Koala Client", "Windows client", eDeviceType.eClient, ""),
			new(14, 1, "OPPO Client", "Android client", eDeviceType.eClient, ""),
			new(15, 1, "stm32_1", "STM32 Nano server", eDeviceType.eNanoServer, "")
		];

		Devices.AddRange(devices);
		SaveChanges();

		List<Point> points =
		[
			new(1, 1, "Pi51 Led1", "Pi51 Led1 Test", ePointType.eDigitalOutput, "18") { Status0 = "Off", Status1 = "On" },
			new(2, 2, "Segment", "Segment Test", ePointType.eTm1637, "DIO=23,CLK=22"),
			new(3, 7, "Radar", "Radar Test", ePointType.eDigitalInput, "27") { Status0 = "Ready", Status1 = "Alert" },
			new(4, 7, "6502", "6502 Test", ePointType.eDigitalInput, "13") { Status0 = "Low", Status1 = "High" },
			new(5, 10, "Pwm1", "Pwm1 Test", ePointType.ePwmOutput, "4") { Status = "", Status0 = "Off", Status1 = "On" },
			new(6, 7, "Supply", "Supply Voltage Test", ePointType.eAnalogInput, "35") { Scale = 0.000805, Units = "Volts" },
			new(7, 7, "Cpu Temp", "Cpu Temp Test", ePointType.eAnalogInput, "34") { Scale = 0.02444, Units = "°C" },
			new(8, 12, "LoRa1 Led1", "LoRa1 Led1 Test", ePointType.eDigitalOutput, "4") { Status0 = "Off", Status1 = "On" },
			new(9, 11, "LoRa2 Led1", "LoRa2 Led1 Test", ePointType.eDigitalOutput, "4") { Status0 = "Off", Status1 = "On" },
			new(10, 2, "Bmp280", "Bmp280 Test", ePointType.eBmp280, "SDA=3,SCL=2"),
			new(11, 10, "Pwm2", "Pwm2 Test", ePointType.ePwmOutput, "5") { Status = "", Status0 = "Off", Status1 = "On" },
			new(12, 7, "6501", "6501 Test", ePointType.eDigitalOutput, "26") { Status0 = "Low", Status1 = "High" },
			new(13, 9, "6401", "6401 Test", ePointType.eDigitalOutput, "4") { Status0 = "Low", Status1 = "High" },
			new(14, 9, "6402", "6402 Test", ePointType.eDigitalInput, "5") { Status0 = "Low", Status1 = "High" },
			new(15, 2, "Temperature", "Temperature Test", ePointType.eAnalogInput, "PID=10,TYP=TEM") { Scale = 1, Units = "°C" },
			new(16, 2, "Pressure", "Pressure Test", ePointType.eAnalogInput, "PID=10,TYP=PRE") { Scale = 1, Units = "mBars" },
			new(17, 2, "Altitude", "Altitude Test", ePointType.eAnalogInput, "PID=10,TYP=ALT") { Scale = 1, Units = "Meters" },
			new(18, 7, "Bmp280", "Bmp280 Test", ePointType.eBmp280, "SDA=21,SCL=22"),
			new(19, 7, "Temperature", "Temperature Test", ePointType.eAnalogInput, "PID=18,TYP=TEM") { Scale = 1, Units = "°C" },
			new(20, 7, "Pressure", "Pressure Test", ePointType.eAnalogInput, "PID=18,TYP=PRE") { Scale = 1, Units = "mBars" },
			new(21, 7, "Altitude", "Altitude Test", ePointType.eAnalogInput, "PID=18,TYP=ALT") { Scale = 1, Units = "Meters" },
			new(22, 7, "ShiftInput", "ShiftInput Test", ePointType.eShiftInput, "LAT=12,CLK=25,DAT=33"),
			new(23, 7, "Shift Bit0", "Shift Bit0 Test", ePointType.eDigitalInput, "PID=22,BIT=0") { Status = "", Status0 = "Off", Status1 = "On" },
			new(24, 7, "Shift Bit5", "Shift Bit5 Test", ePointType.eDigitalInput, "PID=22,BIT=5") { Status = "", Status0 = "Off", Status1 = "On" },
			new(25, 2, "Pi31 Led1", "Pi31 Led1 Test", ePointType.eDigitalOutput, "18") { Status0 = "Off", Status1 = "On" },
			new(26, 3, "Piz21 Led1", "Piz21 Led1 Test", ePointType.eDigitalOutput, "18") { Status0 = "Off", Status1 = "On" }
		];

		Points.AddRange(points);
		SaveChanges();

		List<Group> groups =
		[
			new(1, "Timer 1", "Timer 1 Test"),
			new(2, "Timer 2", "Timer 2 Test")
		];

		Groups.AddRange(groups);
		SaveChanges();

		List<GroupPoint> groupPoints =
		[
			new(1, 1, 1),
			new(2, 1, 5),
			new(3, 2, 8),
			new(4, 2, 9)
		];

		GroupPoints.AddRange(groupPoints);
		SaveChanges();
	}

	public void InitialseDB() => InitialiseDB();
}
