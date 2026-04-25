using Iot.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PeerJsonSockets;

namespace Iot.Server.Net;

internal sealed class ServerDataTask : IPeerServerLoopTask
{
	private readonly ILogger _logger;
	private AppDbContext _dbContext;

	public ServerDataTask(ILogger logger, string basePath)
	{
		_logger = logger;
		_dbContext = IotDataStore.CreateDbContext(Path.Combine(basePath, "data", "Iot.Data.db"));
	}

	public string Name => "server.data";

	public TimeSpan Interval => TimeSpan.FromMinutes(1);

	public Task ExecuteAsync(PeerServerLoopContext context, CancellationToken cancellationToken)
	{
		Data.Models.Point point = _dbContext.Points.Find(1);
		string name = point.Name;
		_logger.LogInformation("Server data task. point: {name}.", name);
		return Task.CompletedTask;
	}
}
