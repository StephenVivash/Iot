using Iot.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PeerJsonSockets;

namespace Iot.Client.Maui.Services;

public sealed class MauiPointControlTestTask : IPeerClientLoopTask
{
	private static readonly int[] DeviceIds = [/*1, 3, 7, 8, 9, 10,*/ 11, 12 ];

	private readonly ILogger<MauiPointControlTestTask> _logger;

	public MauiPointControlTestTask(ILogger<MauiPointControlTestTask> logger)
	{
		_logger = logger;
	}

	public string Name => "client.point-control-test";

	public TimeSpan Interval => TimeSpan.FromSeconds(30);

	bool on = false;

	public async Task ExecuteAsync(PeerClientLoopContext context, CancellationToken cancellationToken)
	{
		await using var dbContext = context.Database.CreateDbContext();
		var points = await dbContext.Points
			.AsNoTracking()
			.Where(point => DeviceIds.Contains(point.DeviceId) &&
				(point.TypeId == PointType.DigitalOutput || point.TypeId == PointType.PwmOutput))
			.OrderBy(point => point.DeviceId)
			.ThenBy(point => point.Id)
			.Select(point => new
			{
				point.Id,
				point.DeviceId,
				point.Name,
				point.Status,
				point.Status0,
				point.Status1
			})
			.ToArrayAsync(cancellationToken);

		//on = !on;
		//string nextStatus = on ? "On" : "Off";

		foreach (var point in points)
		{
			cancellationToken.ThrowIfCancellationRequested();

			string offStatus = GetFallbackStatus(point.Status0, "Off");
			string onStatus = GetFallbackStatus(point.Status1, "On");

			string nextStatus = string.Equals(point.Status, onStatus, StringComparison.OrdinalIgnoreCase)
				? offStatus
				: onStatus;

			_logger.LogInformation("MAUI test sending point control to device {DeviceId}. {PointName} ({PointId}): {Status}.",
				point.DeviceId, point.Name, point.Id, nextStatus);

			await context.SendAsync(PeerMessages.PointControlType,
				PeerMessages.CreatePointControl(point.Id, nextStatus), cancellationToken);
		}
	}

	private static string GetFallbackStatus(string status, string fallback) =>
		string.IsNullOrWhiteSpace(status) ? fallback : status;
}
