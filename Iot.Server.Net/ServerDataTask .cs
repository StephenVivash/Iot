using Iot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PeerJsonSockets;

namespace Iot.Server.Net;

internal sealed class ServerDataTask : IPeerServerLoopTask
{
	private readonly ILogger _logger;
	private AppDbContext? dbContext = null;

	public ServerDataTask(ILogger logger, string basePath)
	{
		_logger = logger;

		DatabasePaths.Set(Path.Combine(basePath, "data", "Iot.Data.db"));
		dbContext = IotDataStore.CreateMigratedDbContext();

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

		logger.LogInformation($"Database: {DatabasePaths.GetConnectionString()}");
		logger.LogInformation($"Devices: {devices.Count}");
		logger.LogInformation($"Points: {dbContext.Points.Count()}");
		logger.LogInformation($"Groups: {groups.Count}");
		logger.LogInformation($"GroupPoints: {dbContext.GroupPoints.Count()}");

		foreach (var device in devices)
		{
			logger.LogInformation($"Device #{device.Id} | Parent {device.ParentDeviceId} | {device.Name} | Type {device.TypeId} | {device.Status}");
			foreach (var point in device.Points.OrderBy(point => point.Id))
				logger.LogInformation($"  - {point.Name} | {point.TypeId} | {point.Status} {point.Units}".TrimEnd());
		}
		foreach (var group in groups)
		{
			logger.LogInformation($"Group #{group.Id} | {group.Name}");
			foreach (var groupPoint in group.GroupPoints.OrderBy(groupPoint => groupPoint.Id))
				logger.LogInformation($"  - Point #{groupPoint.PointId} | {groupPoint.Point.Name}");
		}
	}

	public string Name => "server.data";

	public TimeSpan Interval => TimeSpan.FromSeconds(30);

	public Task ExecuteAsync(PeerServerLoopContext context, CancellationToken cancellationToken)
	{
		Data.Models.Point point = dbContext.Points.Find(1);
		string description = point.Description;
		_logger.LogInformation("Server data task. point: {dDescription}.", description);
		return Task.CompletedTask;
	}
}
