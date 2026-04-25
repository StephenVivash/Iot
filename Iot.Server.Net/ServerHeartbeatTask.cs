using Microsoft.Extensions.Logging;
using PeerJsonSockets;

namespace Iot.Server.Net;

internal sealed class ServerHeartbeatTask : IPeerServerLoopTask
{
	private readonly ILogger _logger;

	public ServerHeartbeatTask(ILogger logger)
	{
		_logger = logger;
	}

	public string Name => "server.heartbeat";

	public TimeSpan Interval => TimeSpan.FromSeconds(30);

	public Task ExecuteAsync(PeerServerLoopContext context, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"Server heartbeat task. Connected peers: {ConnectedPeerCount}. Connected clients: {ConnectedClientCount}.",
			context.ConnectedPeerCount,
			context.ConnectedClientCount);
		return Task.CompletedTask;
	}
}
