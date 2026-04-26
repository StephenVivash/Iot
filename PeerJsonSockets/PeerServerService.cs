using System.Net;
using Iot.Data;
using Microsoft.Extensions.Logging;

namespace PeerJsonSockets;

public sealed class PeerServerService
{
	private readonly IPAddress _listenAddress;
	private readonly int _listenPort;
	private readonly PeerConnectionService _connectionService;
	private readonly PeerConnectionRegistry _connectionRegistry;
	private readonly IReadOnlyList<IPeerServerLoopTask> _loopTasks;
	private readonly ILogger _logger;
	private readonly PeerRuntimeOptions _options;
	private readonly IotDatabase _database;

	public PeerServerService(
		IPAddress listenAddress,
		int listenPort,
		PeerRuntimeOptions options,
		PeerConnectionRegistry connectionRegistry,
		PeerConnectionService connectionService,
		ILogger logger,
		IotDatabase database,
		IEnumerable<IPeerServerLoopTask>? loopTasks = null)
	{
		_listenAddress = listenAddress;
		_listenPort = listenPort;
		_options = options;
		_connectionRegistry = connectionRegistry;
		_connectionService = connectionService;
		_logger = logger;
		_database = database;
		_loopTasks = loopTasks?.ToArray() ?? [];
	}

	public async Task RunAsync(CancellationToken cancellationToken)
	{
		await using PeerSocketServer server = new(_listenAddress, _listenPort);
		Task serverTask = server.RunAsync(HandleIncomingPeerAsync, cancellationToken);
		Task serverLoopTask = RunServerLoopAsync(cancellationToken);
		await Task.WhenAll(serverTask, serverLoopTask);
	}

	private async Task HandleIncomingPeerAsync(JsonSocketPeer peer, CancellationToken cancellationToken)
	{
		_logger.LogWarning("Server accepted {RemotePeer}.", _connectionService.GetRemoteDisplayName(peer));

		try
		{
			JsonPeerMessage? helloMessage = await _connectionService.ReceiveAndLogAsync(PeerRole.Server, peer, cancellationToken);
			if (helloMessage?.Type == PeerMessages.HandshakeType)
			{
				await _connectionService.SendAndLogAsync(PeerRole.Server, peer,
					PeerMessages.AckType, PeerMessages.CreateAck(_options.PeerName),
					cancellationToken);
			}

			JsonPeerMessage? statusMessage = await _connectionService.ReceiveAndLogAsync(PeerRole.Server, peer, cancellationToken);
			if (statusMessage?.Type == PeerMessages.StatusType)
			{
				await _connectionService.SendAndLogAsync(PeerRole.Server, peer,
					PeerMessages.StatusType, PeerMessages.CreateStatus(_options.PeerName, _connectionRegistry.CountByRole(PeerRole.Server) + 1),
					cancellationToken);
			}

			await RunAcceptedClientAsync(peer, cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Server connection {RemotePeer} failed.", _connectionService.GetRemoteDisplayName(peer));
		}
	}

	private async Task RunAcceptedClientAsync(JsonSocketPeer peer, CancellationToken cancellationToken)
	{
		using CancellationTokenSource connectionShutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		PeerConnection connection = new(Guid.NewGuid(), peer, PeerRole.Server, connectionShutdown);
		_connectionService.ApplyKnownPeerName(connection);

		_connectionRegistry.Register(connection);
		_logger.LogWarning("Server registered accepted client {RemotePeer}. Connected clients: {ConnectedClientCount}.",
			connection.RemoteDisplayName, _connectionRegistry.CountByRole(PeerRole.Server));

		try
		{
			while (!connection.CancellationToken.IsCancellationRequested)
			{
				JsonPeerMessage? message = await _connectionService.ReceiveAndLogAsync(connection);
				if (message is null)
					break;
				await ProcessAcceptedClientMessageAsync(connection, message);
			}
		}
		catch (OperationCanceledException) when (connection.CancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Server accepted client {RemotePeer} failed.", connection.RemoteDisplayName);
		}
		finally
		{
			connection.Stop();
			_connectionRegistry.Unregister(connection.Id);

			_logger.LogInformation("Server unregistered accepted client {RemotePeer}. Connected clients: {ConnectedClientCount}.",
				connection.RemoteDisplayName, _connectionRegistry.CountByRole(PeerRole.Server));
		}
	}

	private async Task ProcessAcceptedClientMessageAsync(PeerConnection connection, JsonPeerMessage message)
	{
		if (message.Type != PeerMessages.PollType)
		{
			return;
		}

		Poll? poll = JsonSocketPeer.ReadPayload<Poll>(message);
		string pollId = poll?.PollId ?? message.Id;

		await _connectionService.SendAndLogAsync(connection, PeerMessages.PollAckType,
			PeerMessages.CreatePollAck(_options.PeerName, pollId));
	}

	private async Task RunServerLoopAsync(CancellationToken cancellationToken)
	{
		LoopTaskScheduler<PeerServerLoopContext> scheduler = CreateServerLoopScheduler();
		PeerServerLoopContext context = new(_connectionRegistry, _connectionService, _database);

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				await scheduler.RunDueTasksAsync(context, cancellationToken);
				await Task.Delay(_options.LoopDelay, cancellationToken);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private LoopTaskScheduler<PeerServerLoopContext> CreateServerLoopScheduler()
	{
		LoopTaskScheduler<PeerServerLoopContext> scheduler = new(_logger);

		scheduler.Register("server.connection-summary", _options.SummaryInterval, RunServerConnectionSummaryAsync);
		scheduler.Register("server.maintenance", _options.MaintenanceInterval, RunServerMaintenanceAsync);
		foreach (IPeerServerLoopTask loopTask in _loopTasks)
		{
			scheduler.Register(loopTask.Name, loopTask.Interval, loopTask.ExecuteAsync);
		}

		return scheduler;
	}

	private Task RunServerConnectionSummaryAsync(PeerServerLoopContext context, CancellationToken cancellationToken)
	{
		_logger.LogInformation("Server task connection summary. Connected clients: {ConnectedClientCount}.",
			context.ConnectedClientCount);
		return Task.CompletedTask;
	}

	private Task RunServerMaintenanceAsync(PeerServerLoopContext context, CancellationToken cancellationToken)
	{
		_logger.LogDebug("Server task maintenance. Connected clients: {ConnectedClientCount}.", context.ConnectedClientCount);
		return Task.CompletedTask;
	}
}
