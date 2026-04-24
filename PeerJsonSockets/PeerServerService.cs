using System.Net;
using Microsoft.Extensions.Logging;

namespace PeerJsonSockets;

public sealed class PeerServerService
{
	private readonly IPAddress _listenAddress;
	private readonly int _listenPort;
	private readonly PeerConnectionService _connectionService;
	private readonly PeerConnectionRegistry _connectionRegistry;
	private readonly ILogger _logger;
	private readonly PeerRuntimeOptions _options;

	public PeerServerService(
		IPAddress listenAddress,
		int listenPort,
		PeerRuntimeOptions options,
		PeerConnectionRegistry connectionRegistry,
		PeerConnectionService connectionService,
		ILogger logger)
	{
		_listenAddress = listenAddress;
		_listenPort = listenPort;
		_options = options;
		_connectionRegistry = connectionRegistry;
		_connectionService = connectionService;
		_logger = logger;
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
			if (helloMessage?.Type == HandshakeMessages.HandshakeType)
			{
				await _connectionService.SendAndLogAsync(
					PeerRole.Server,
					peer,
					HandshakeMessages.AckType,
					HandshakeMessages.CreateAck(_options.PeerName),
					cancellationToken);
			}

			JsonPeerMessage? statusMessage = await _connectionService.ReceiveAndLogAsync(PeerRole.Server, peer, cancellationToken);
			if (statusMessage?.Type == HandshakeMessages.StatusType)
			{
				await _connectionService.SendAndLogAsync(
					PeerRole.Server,
					peer,
					HandshakeMessages.StatusType,
					HandshakeMessages.CreateStatus(_options.PeerName, _connectionRegistry.CountByRole(PeerRole.Server) + 1),
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
		_logger.LogWarning(
			"Server registered accepted client {RemotePeer}. Connected clients: {ConnectedClientCount}.",
			connection.RemoteDisplayName,
			_connectionRegistry.CountByRole(PeerRole.Server));

		try
		{
			while (!connection.CancellationToken.IsCancellationRequested)
			{
				JsonPeerMessage? message = await _connectionService.ReceiveAndLogAsync(connection);
				if (message is null)
				{
					break;
				}

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

			_logger.LogInformation(
				"Server unregistered accepted client {RemotePeer}. Connected clients: {ConnectedClientCount}.",
				connection.RemoteDisplayName,
				_connectionRegistry.CountByRole(PeerRole.Server));
		}
	}

	private async Task ProcessAcceptedClientMessageAsync(PeerConnection connection, JsonPeerMessage message)
	{
		if (message.Type != HandshakeMessages.PollType)
		{
			return;
		}

		Poll? poll = JsonSocketPeer.ReadPayload<Poll>(message);
		string pollId = poll?.PollId ?? message.Id;

		await _connectionService.SendAndLogAsync(
			connection,
			HandshakeMessages.PollAckType,
			HandshakeMessages.CreatePollAck(_options.PeerName, pollId));
	}

	private async Task RunServerLoopAsync(CancellationToken cancellationToken)
	{
		LoopTaskScheduler<ServerLoopContext> scheduler = CreateServerLoopScheduler();
		ServerLoopContext context = new();

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

	private LoopTaskScheduler<ServerLoopContext> CreateServerLoopScheduler()
	{
		LoopTaskScheduler<ServerLoopContext> scheduler = new(_logger);

		scheduler.Register("server.connection-summary", _options.SummaryInterval, RunServerConnectionSummaryAsync);
		scheduler.Register("server.maintenance", _options.MaintenanceInterval, RunServerMaintenanceAsync);

		return scheduler;
	}

	private Task RunServerConnectionSummaryAsync(ServerLoopContext context, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"Server task connection summary. Connected clients: {ConnectedClientCount}.",
			_connectionRegistry.CountByRole(PeerRole.Server));
		return Task.CompletedTask;
	}

	private Task RunServerMaintenanceAsync(ServerLoopContext context, CancellationToken cancellationToken)
	{
		_logger.LogDebug("Server task maintenance. Connected clients: {ConnectedClientCount}.", _connectionRegistry.CountByRole(PeerRole.Server));
		return Task.CompletedTask;
	}

	private sealed class ServerLoopContext;
}
