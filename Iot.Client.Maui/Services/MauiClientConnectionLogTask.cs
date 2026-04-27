using Microsoft.Extensions.Logging;
using PeerJsonSockets;

namespace Iot.Client.Maui.Services;

public sealed class MauiClientConnectionLogTask : IPeerClientLoopTask
{
	private readonly ILogger<MauiClientConnectionLogTask> _logger;
	//private readonly ILogger _logger;

	public MauiClientConnectionLogTask(ILogger<MauiClientConnectionLogTask> logger)
	//public MauiClientConnectionLogTask(ILogger logger)
	{
		_logger = logger;
	}

	public string Name => "client.connection-log"; // maui.

	public TimeSpan Interval => TimeSpan.FromSeconds(30);

	public Task ExecuteAsync(PeerClientLoopContext context, CancellationToken cancellationToken)
	{
		_logger.LogInformation("Client observed {RemotePeer}. Messages - Polls: {SentPollCount}. Processed: {ProcessedMessageCount}. Queued: {QueuedMessageCount}.",
			context.RemoteDisplayName, context.SentPollCount, context.ProcessedMessageCount, context.QueuedMessageCount);
		return Task.CompletedTask;
	}
}
