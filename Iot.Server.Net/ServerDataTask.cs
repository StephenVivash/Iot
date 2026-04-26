using System.Globalization;
using Iot.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PeerJsonSockets;

namespace Iot.Server.Net;

internal sealed class ServerDataTask : IPeerServerLoopTask
{
	private readonly ILogger _logger;

	public ServerDataTask(ILogger logger)
	{
		_logger = logger;
	}

	public string Name => "server.data";

	public TimeSpan Interval => TimeSpan.FromMinutes(1);

	public async Task ExecuteAsync(PeerServerLoopContext context, CancellationToken cancellationToken)
	{
		await using var dbContext = context.Database.CreateDbContext();
		var points = await dbContext.Points
			.AsNoTracking()
			.OrderBy(point => point.Id)
			.Select(point => new
			{
				point.Id,
				point.TypeId,
				point.Status,
				point.Status0,
				point.Status1
			})
			.ToArrayAsync(cancellationToken);

		PointStatus[] pointStatuses = points
			.Select(point => PeerMessages.CreatePointStatus(
				point.Id,
				CreateRandomStatus(point.TypeId, point.Status, point.Status0, point.Status1)))
			.ToArray();

		foreach (PointStatus pointStatus in pointStatuses)
			await context.SendToConnectedClientsAsync(PeerMessages.PointStatusType, pointStatus, cancellationToken);
	}

	private static string CreateRandomStatus(ePointType typeId, string currentStatus, string status0, string status1)
	{
		return typeId switch
		{
			ePointType.eDigitalInput or ePointType.eDigitalOutput =>
				Random.Shared.Next(0, 2) == 0
					? GetFallbackStatus(status0, "Off")
					: GetFallbackStatus(status1, "On"),

			ePointType.eAnalogInput or ePointType.eAnalogOutput or ePointType.ePwmOutput =>
				(Random.Shared.NextDouble() * 100).ToString("0.0", CultureInfo.InvariantCulture),

			_ => currentStatus
		};
	}

	private static string GetFallbackStatus(string status, string fallback)
	{
		return string.IsNullOrWhiteSpace(status) ? fallback : status;
	}
}
