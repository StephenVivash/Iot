using Microsoft.Extensions.Logging;
using PeerJsonSockets;

namespace Iot.Server.Net;

internal sealed class ConsoleServerHeartbeatTask : IPeerServerLoopTask
{
	private readonly ILogger _logger;

	public ConsoleServerHeartbeatTask(ILogger logger)
	{
		_logger = logger;
	}

	public string Name => "console.server.heartbeat";

	public TimeSpan Interval => TimeSpan.FromSeconds(30);

	public Task ExecuteAsync(PeerServerLoopContext context, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"Console app server task heartbeat. Connected peers: {ConnectedPeerCount}. Connected clients: {ConnectedClientCount}.",
			context.ConnectedPeerCount,
			context.ConnectedClientCount);
		return Task.CompletedTask;
	}
}
