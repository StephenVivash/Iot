using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace PeerJsonSockets;

public sealed class PeerClientService
{
	private readonly PeerConnectionService _connectionService;
	private readonly PeerConnectionRegistry _connectionRegistry;
	private readonly IReadOnlyList<IPeerClientLoopTask> _loopTasks;
	private readonly ILogger _logger;
	private readonly PeerRuntimeOptions _options;

	public PeerClientService(
		PeerRuntimeOptions options,
		PeerConnectionRegistry connectionRegistry,
		PeerConnectionService connectionService,
		ILogger logger,
		IEnumerable<IPeerClientLoopTask>? loopTasks = null)
	{
		_options = options;
		_connectionRegistry = connectionRegistry;
		_connectionService = connectionService;
		_logger = logger;
		_loopTasks = loopTasks?.ToArray() ?? [];
	}

	public async Task RunAsync(PeerAddress peerAddress, CancellationToken cancellationToken)
	{
		int reconnectAttempt = 0;

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				await ConnectAndRunAsync(peerAddress, cancellationToken);
				reconnectAttempt = 0;
				_logger.LogInformation("Client outbound connection to {PeerAddress} closed.", peerAddress);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			catch (SocketException)
			{
				_logger.LogWarning("Client outbound connection to {PeerAddress} failed.", peerAddress);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Client outbound connection to {PeerAddress} failed.", peerAddress);
			}

			reconnectAttempt++;
			TimeSpan delay = reconnectAttempt <= _options.FastReconnectAttempts
				? _options.FastReconnectDelay
				: _options.SlowReconnectDelay;

			_logger.LogInformation(
				"Client reconnecting to {PeerAddress} in {DelaySeconds:N0} seconds. Attempt {ReconnectAttempt}.",
				peerAddress, delay.TotalSeconds, reconnectAttempt);

			try
			{
				await Task.Delay(delay, cancellationToken);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				break;
			}
		}
	}

	private async Task RunConnectedPeerAsync(JsonSocketPeer peer, CancellationToken cancellationToken)
	{
		using CancellationTokenSource connectionShutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		PeerConnection connection = new(Guid.NewGuid(), peer, PeerRole.Client, connectionShutdown);
		_connectionService.ApplyKnownPeerName(connection);

		_connectionRegistry.Register(connection);
		_logger.LogWarning(
			"Client registered server connection {RemotePeer}. Connected to server: {ConnectedToServer}.",
			connection.RemoteDisplayName,
			_connectionRegistry.CountByRole(PeerRole.Client) > 0);

		Task readerTask = ReadPeerMessagesAsync(connection);
		Task clientLoopTask = RunClientLoopAsync(connection);

		try
		{
			await Task.WhenAny(readerTask, clientLoopTask);
		}
		finally
		{
			connection.Stop();
			_connectionRegistry.Unregister(connection.Id);

			try
			{
				await Task.WhenAll(readerTask, clientLoopTask);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || connectionShutdown.IsCancellationRequested)
			{
			}

			_logger.LogWarning(
				"Client unregistered server connection {RemotePeer}. Connected to server: {ConnectedToServer}.",
				connection.RemoteDisplayName,
				_connectionRegistry.CountByRole(PeerRole.Client) > 0);
		}
	}

	private async Task ConnectAndRunAsync(PeerAddress peerAddress, CancellationToken cancellationToken)
	{
		await using JsonSocketPeer peer = await JsonSocketPeer.ConnectAsync(peerAddress.Host, peerAddress.Port, cancellationToken);
		_logger.LogWarning("Client connected to {RemotePeer}.", _connectionService.GetRemoteDisplayName(peer));

		await _connectionService.SendAndLogAsync(
			PeerRole.Client,
			peer,
			PeerMessages.HandshakeType,
			PeerMessages.CreateHello(_options.PeerName),
			cancellationToken);
		JsonPeerMessage? ackMessage = await _connectionService.ReceiveAndLogAsync(PeerRole.Client, peer, cancellationToken);
		if (ackMessage is null)
		{
			throw new InvalidOperationException("Client connection closed during handshake.");
		}

		await _connectionService.SendAndLogAsync(
			PeerRole.Client,
			peer,
			PeerMessages.StatusType,
			PeerMessages.CreateStatus(_options.PeerName, 1),
			cancellationToken);
		JsonPeerMessage? statusMessage = await _connectionService.ReceiveAndLogAsync(PeerRole.Client, peer, cancellationToken);
		if (statusMessage is null)
		{
			throw new InvalidOperationException("Client connection closed during status exchange.");
		}

		await RunConnectedPeerAsync(peer, cancellationToken);
	}

	private async Task ReadPeerMessagesAsync(PeerConnection connection)
	{
		try
		{
			while (!connection.CancellationToken.IsCancellationRequested)
			{
				JsonPeerMessage? message = await _connectionService.ReceiveAndLogAsync(connection);
				if (message is null)
				{
					break;
				}

				connection.IncomingMessages.Enqueue(message);
			}
		}
		catch (OperationCanceledException) when (connection.CancellationToken.IsCancellationRequested)
		{
		}
		catch (IOException ex)
		{
			if (ex.InnerException is SocketException)
			{
				_logger.LogWarning("Client connection to {RemotePeer} closed by remote peer.", connection.RemoteDisplayName);
			}
			else
			{
				_logger.LogError(ex, "Client reader for {RemotePeer} failed.", connection.RemoteDisplayName);
			}
		}
		finally
		{
			connection.Stop();
		}
	}

	private async Task RunClientLoopAsync(PeerConnection connection)
	{
		LoopTaskScheduler<PeerClientLoopContext> scheduler = CreateClientLoopScheduler();
		PeerClientLoopContext context = new(connection);

		try
		{
			while (!connection.CancellationToken.IsCancellationRequested)
			{
				await scheduler.RunDueTasksAsync(context, connection.CancellationToken);

				while (connection.IncomingMessages.TryDequeue(out JsonPeerMessage? message))
				{
					await ProcessClientMessageAsync(connection, message);
					context.ProcessedMessageCount++;
				}

				await Task.Delay(_options.LoopDelay, connection.CancellationToken);
			}
		}
		catch (OperationCanceledException) when (connection.CancellationToken.IsCancellationRequested)
		{
		}
	}

	private LoopTaskScheduler<PeerClientLoopContext> CreateClientLoopScheduler()
	{
		LoopTaskScheduler<PeerClientLoopContext> scheduler = new(_logger);

		scheduler.Register("client.poll", _options.PollInterval, RunClientPollAsync);
		scheduler.Register("client.summary", _options.MaintenanceInterval, RunClientSummaryAsync);
		foreach (IPeerClientLoopTask loopTask in _loopTasks)
		{
			scheduler.Register(loopTask.Name, loopTask.Interval, loopTask.ExecuteAsync);
		}

		return scheduler;
	}

	private async Task RunClientPollAsync(PeerClientLoopContext context, CancellationToken cancellationToken)
	{
		PeerConnection connection = context.Connection;

		await SendPollAsync(connection);
		context.SentPollCount++;
	}

	private Task RunClientSummaryAsync(PeerClientLoopContext context, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"Client summary for {RemotePeer}. Sent polls: {SentPollCount}. Processed messages: {ProcessedMessageCount}. Queued messages: {QueuedMessageCount}.",
			context.RemoteDisplayName,
			context.SentPollCount,
			context.ProcessedMessageCount,
			context.QueuedMessageCount);
		return Task.CompletedTask;
	}

	private async Task SendPollAsync(PeerConnection connection)
	{
		try
		{
			Poll poll = PeerMessages.CreatePoll(_options.PeerName);
			await _connectionService.SendAndLogAsync(connection, PeerMessages.PollType, poll);
		}
		catch (Exception ex) when (!connection.CancellationToken.IsCancellationRequested)
		{
			_logger.LogError(ex, "Client failed to send poll to {RemotePeer}.", connection.RemoteDisplayName);
			connection.Stop();
		}
	}

	private Task ProcessClientMessageAsync(PeerConnection connection, JsonPeerMessage message)
	{
		if (message.Type == PeerMessages.PollAckType)
		{
			return Task.CompletedTask;
		}

		if (message.Type == PeerMessages.PointStatusType)
		{
			//PointStatus ps = message.Payload.Deserialize<PointStatus>()!;
			//_logger.LogDebug("Client processed point status from {RemotePeer}; point {id} {status}.", connection.RemoteDisplayName, ps.Id, ps.Status);
		}

		if (message.Type == PeerMessages.PollType)
		{
			_logger.LogDebug("Client ignored poll from {RemotePeer}; poll acknowledgements are handled by server connections.", connection.RemoteDisplayName);
		}

		return Task.CompletedTask;
	}
}
