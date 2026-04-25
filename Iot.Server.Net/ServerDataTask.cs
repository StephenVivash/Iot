using Iot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PeerJsonSockets;

namespace Iot.Server.Net;

internal sealed class ServerDataTask : IPeerServerLoopTask
{
	private readonly ILogger _logger;
	private readonly AppDbContext _dbContext;

	public ServerDataTask(ILogger logger, string basePath)
	{
		_logger = logger;
		_dbContext = IotDataStore.CreateDbContext(Path.Combine(basePath, "data", "Iot.Data.db"));
	}

	public string Name => "server.data";

	public TimeSpan Interval => TimeSpan.FromMinutes(1);

	public async Task ExecuteAsync(PeerServerLoopContext context, CancellationToken cancellationToken)
	{
		PointStatus[] pointStatuses = await _dbContext.Points
			.AsNoTracking()
			.OrderBy(point => point.Id)
			.Select(point => HandshakeMessages.CreatePointStatus(point.Id, point.Status))
			.ToArrayAsync(cancellationToken);

		foreach (PointStatus pointStatus in pointStatuses)
			await context.SendToConnectedClientsAsync(HandshakeMessages.PointStatusType, pointStatus, cancellationToken);
		//_logger.LogInformation();
	}
}
