using Iot.Data;

namespace PeerJsonSockets;

public sealed class PeerServerLoopContext
{
	private readonly PeerConnectionRegistry _connectionRegistry;
	private readonly PeerConnectionService _connectionService;

	internal PeerServerLoopContext(PeerConnectionRegistry connectionRegistry, PeerConnectionService connectionService, IotDatabase database)
	{
		_connectionRegistry = connectionRegistry;
		_connectionService = connectionService;
		Database = database;
	}

	public IotDatabase Database { get; }

	public int ConnectedPeerCount => _connectionRegistry.Count;

	public int ConnectedClientCount => _connectionRegistry.CountByRole(nameof(PeerRole.Server));

	public int ConnectedServerCount => _connectionRegistry.CountByRole(nameof(PeerRole.Client));

	public async Task SendToConnectedClientsAsync<TPayload>(
		string messageType,
		TPayload payload,
		CancellationToken cancellationToken = default)
	{
		foreach (PeerConnection connection in _connectionRegistry.GetByRole(PeerRole.Server))
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (connection.CancellationToken.IsCancellationRequested)
				continue;
			await _connectionService.SendAndLogAsync(connection, messageType, payload, cancellationToken);
		}
	}

	public async Task SendToConnectedPeersAsync<TPayload>(
		string messageType,
		TPayload payload,
		CancellationToken cancellationToken = default)
	{
		foreach (PeerConnection connection in _connectionRegistry.GetAll())
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (connection.CancellationToken.IsCancellationRequested)
				continue;
			await _connectionService.SendAndLogAsync(connection, messageType, payload, cancellationToken);
		}
	}
}
