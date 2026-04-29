using System.Net;
using Iot.Data;
using Iot.Data.Models;
using Microsoft.Extensions.Logging;

namespace PeerJsonSockets;

public sealed class PeerServerService
{
	private readonly IPAddress _listenAddress;
	private readonly int _listenPort;
	private readonly PeerConnectionService _connectionService;
	private readonly PeerConnectionRegistry _connectionRegistry;
	private readonly IReadOnlyList<IPeerServerLoopTask> _loopTasks;
	private readonly IReadOnlyList<IPeerPointControlHandler> _pointControlHandlers;
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
		IEnumerable<IPeerServerLoopTask>? loopTasks = null,
		IEnumerable<IPeerPointControlHandler>? pointControlHandlers = null)
	{
		_listenAddress = listenAddress;
		_listenPort = listenPort;
		_options = options;
		_connectionRegistry = connectionRegistry;
		_connectionService = connectionService;
		_logger = logger;
		_database = database;
		_loopTasks = loopTasks?.ToArray() ?? [];
		_pointControlHandlers = pointControlHandlers?.ToArray() ?? [];
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
					PeerMessages.HandshakeAckType, PeerMessages.CreateHandshakeAck(_options.PeerName),
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
		_logger.LogWarning("Server accepted registered client {RemotePeer}. Connected clients: {ConnectedClientCount}.",
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
		if (message.Type == PeerMessages.PollType)
		{
			Poll? poll = JsonSocketPeer.ReadPayload<Poll>(message);
			string pollId = poll?.PollId ?? message.Id;

			await _connectionService.SendAndLogAsync(connection, PeerMessages.PollAckType,
				PeerMessages.CreatePollAck(_options.PeerName, pollId));
			return;
		}

		if (message.Type == PeerMessages.PointStatusType)
		{
			await ProcessPointStatusAsync(connection, message);
			return;
		}

		if (message.Type == PeerMessages.PointControlType)
		{
			await ProcessPointControlAsync(connection, message);
			return;
		}

		_logger.LogDebug("Server ignored {MessageType} from {RemotePeer}.", message.Type, connection.RemoteDisplayName);
	}

	private async Task ProcessPointControlAsync(PeerConnection connection, JsonPeerMessage message)
	{
		PointControl? pointControl = JsonSocketPeer.ReadPayload<PointControl>(message);
		if (pointControl is null)
		{
			_logger.LogWarning("Server received invalid point control from {RemotePeer}.",
				connection.RemoteDisplayName);
			return;
		}

		PointStatus? pointStatus = await TryHandlePointControlAsync(pointControl, connection.CancellationToken);
		if (pointStatus is not null)
		{
			_logger.LogInformation("Server handled point control from {RemotePeer}. Point {PointId}: {Status}.",
				connection.RemoteDisplayName, pointStatus.Id, pointStatus.Status);

			await SendPointStatusToConnectedPeersAsync(pointStatus, connection.CancellationToken);
			return;
		}

		await RelayPointControlToConnectedPeersAsync(connection, pointControl);
	}

	private async Task<PointStatus?> TryHandlePointControlAsync(PointControl pointControl, CancellationToken cancellationToken)
	{
		foreach (IPeerPointControlHandler pointControlHandler in _pointControlHandlers)
		{
			cancellationToken.ThrowIfCancellationRequested();
			PointStatus? pointStatus = await pointControlHandler.TryHandlePointControlAsync(pointControl, cancellationToken);
			if (pointStatus is not null)
				return pointStatus;
		}

		return null;
	}

	private async Task ProcessPointStatusAsync(PeerConnection connection, JsonPeerMessage message)
	{
		PointStatus? pointStatus = JsonSocketPeer.ReadPayload<PointStatus>(message);
		if (pointStatus is null)
		{
			_logger.LogWarning("Server received invalid point status from {RemotePeer}.",
				connection.RemoteDisplayName);
			return;
		}

		await using AppDbContext dbContext = _database.CreateDbContext();
		Point? point = await dbContext.Points.FindAsync([pointStatus.Id], connection.CancellationToken);
		if (point is null)
		{
			_logger.LogWarning("Server received point status from {RemotePeer} for unknown point {PointId}: {Status}.",
				connection.RemoteDisplayName, pointStatus.Id, pointStatus.Status);
			return;
		}

		if (string.Equals(point.Status, pointStatus.Status, StringComparison.Ordinal))
		{
			_logger.LogDebug("Server ignored unchanged point status from {RemotePeer}. {PointName} ({PointId}): {Status}.",
				connection.RemoteDisplayName, point.Name, point.Id, pointStatus.Status);
			return;
		}

		point.Status = pointStatus.Status;
		point.TimeStamp = DateTime.UtcNow;
		await dbContext.SaveChangesAsync(connection.CancellationToken);

		string units = string.IsNullOrWhiteSpace(point.Units)
			? string.Empty
			: $" {point.Units}";

		_logger.LogInformation("Server received point status from {RemotePeer}. {PointName} ({PointId}): {Status}{Units}.",
			connection.RemoteDisplayName, point.Name, point.Id, point.Status, units);

		await RelayPointStatusToConnectedPeersAsync(connection, pointStatus);
	}

	private Task SendPointStatusToConnectedPeersAsync(PointStatus pointStatus, CancellationToken cancellationToken) =>
		SendToConnectedPeersAsync(PeerMessages.PointStatusType, pointStatus, cancellationToken);

	private async Task RelayPointStatusToConnectedPeersAsync(PeerConnection sourceConnection, PointStatus pointStatus)
	{
		PeerConnection[] peerConnections = _connectionRegistry.GetAll()
			.Where(connection => connection.Id != sourceConnection.Id)
			.ToArray();

		if (peerConnections.Length == 0)
			return;

		_logger.LogDebug("Server relaying point status from {RemotePeer} to {ConnectedPeerCount} connected peers.",
			sourceConnection.RemoteDisplayName, peerConnections.Length);

		foreach (PeerConnection peerConnection in peerConnections)
		{
			if (sourceConnection.CancellationToken.IsCancellationRequested)
				return;

			if (peerConnection.CancellationToken.IsCancellationRequested)
				continue;

			try
			{
				await _connectionService.SendAndLogAsync(peerConnection, PeerMessages.PointStatusType, pointStatus, sourceConnection.CancellationToken);
			}
			catch (OperationCanceledException) when (sourceConnection.CancellationToken.IsCancellationRequested || peerConnection.CancellationToken.IsCancellationRequested)
			{
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Server failed to relay point status to connected peer {RemotePeer}.",
					peerConnection.RemoteDisplayName);
				peerConnection.Stop();
			}
		}
	}

	private async Task RelayPointControlToConnectedPeersAsync(PeerConnection sourceConnection, PointControl pointControl)
	{
		PeerConnection[] peerConnections = _connectionRegistry.GetAll().ToArray();
		PointControlRoute? route = await PointControlRouteResolver.ResolveAsync(
			_database,
			_options.PeerName,
			pointControl.Id,
			sourceConnection,
			peerConnections,
			_logger,
			sourceConnection.CancellationToken);

		if (route is null)
		{
			//_logger.LogWarning("Server received point control from {RemotePeer} for point {PointId}, but no route is available.",
			//	sourceConnection.RemoteDisplayName, pointControl.Id);
			return;
		}

		_logger.LogDebug("Server relaying point control from {RemotePeer} to {NextHopPeer}. Device: {TargetDeviceName} Point {PointId}: {Status}.",
			sourceConnection.RemoteDisplayName, route.Connection.RemoteDisplayName, route.TargetDeviceName, pointControl.Id, pointControl.Status);

		PeerConnection peerConnection = route.Connection;
		if (sourceConnection.CancellationToken.IsCancellationRequested || peerConnection.CancellationToken.IsCancellationRequested)
			return;

		try
		{
			await _connectionService.SendAndLogAsync(peerConnection, PeerMessages.PointControlType, pointControl, sourceConnection.CancellationToken);
		}
		catch (OperationCanceledException) when (sourceConnection.CancellationToken.IsCancellationRequested || peerConnection.CancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Server failed to relay point control to connected peer {RemotePeer}.",
				peerConnection.RemoteDisplayName);
			peerConnection.Stop();
		}
	}

	private async Task SendToConnectedPeersAsync<TPayload>(
		string messageType,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		foreach (PeerConnection peerConnection in _connectionRegistry.GetAll())
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (peerConnection.CancellationToken.IsCancellationRequested)
				continue;

			try
			{
				await _connectionService.SendAndLogAsync(peerConnection, messageType, payload, cancellationToken);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || peerConnection.CancellationToken.IsCancellationRequested)
			{
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Server failed to send {MessageType} to connected peer {RemotePeer}.",
					messageType, peerConnection.RemoteDisplayName);
				peerConnection.Stop();
			}
		}
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
		_logger.LogInformation("Server connection summary. Connected clients: {ConnectedClientCount}.",
			context.ConnectedClientCount);
		return Task.CompletedTask;
	}

	private Task RunServerMaintenanceAsync(PeerServerLoopContext context, CancellationToken cancellationToken)
	{
		_logger.LogDebug("Server maintenance. Connected clients: {ConnectedClientCount}.", context.ConnectedClientCount);
		return Task.CompletedTask;
	}
}
