using Iot.Data;
using Microsoft.EntityFrameworkCore;

namespace Iot.Data.Test;

internal static class Program
{
	private const string basePath = @"C:\Src\Iot\Iot.Server.Net";
	private static int Main(string[] args)
	{
		DataTest();
		return 0;
	}

	private static void DataTest()
	{
		using var dbContext = IotDataStore.CreateDbContext(
			Path.Combine(basePath, "data", "Iot.Data.db"));

		var devices = dbContext.Devices
			.AsNoTracking()
			.Include(device => device.Points)
			.OrderBy(device => device.Id)
			.ToList();
		var groups = dbContext.Groups
			.AsNoTracking()
			.Include(group => group.GroupPoints)
			.ThenInclude(groupPoint => groupPoint.Point)
			.OrderBy(group => group.Id)
			.ToList();

		Console.WriteLine($"Devices: {devices.Count}");
		Console.WriteLine($"Points: {dbContext.Points.Count()}");
		Console.WriteLine($"Groups: {groups.Count}");
		Console.WriteLine($"GroupPoints: {dbContext.GroupPoints.Count()}");
		Console.WriteLine();

		foreach (var device in devices)
		{
			Console.WriteLine($"Device #{device.Id} | Parent {device.ParentDeviceId} | {device.Name} | Type {device.TypeId} | {device.Status}");
			foreach (var point in device.Points.OrderBy(point => point.Id))
				Console.WriteLine($"  - {point.Name} | {point.TypeId} | {point.Status} {point.Units}".TrimEnd());
		}
		foreach (var group in groups)
		{
			Console.WriteLine($"Group #{group.Id} | {group.Name}");
			foreach (var groupPoint in group.GroupPoints.OrderBy(groupPoint => groupPoint.Id))
				Console.WriteLine($"  - Point #{groupPoint.PointId} | {groupPoint.Point.Name}");
		}
	}
}
