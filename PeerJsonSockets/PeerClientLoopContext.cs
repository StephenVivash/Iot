using Iot.Data;

namespace PeerJsonSockets;

public sealed class PeerClientLoopContext
{
	private readonly PeerConnection _connection;
	private readonly PeerConnectionService _connectionService;

	internal PeerClientLoopContext(PeerConnection connection, PeerConnectionService connectionService, IotDatabase database)
	{
		_connection = connection;
		_connectionService = connectionService;
		Database = database;
	}

	internal PeerConnection Connection => _connection;

	public IotDatabase Database { get; }

	public string RemoteDisplayName => _connection.RemoteDisplayName;

	public string? RemotePeerName => _connection.RemotePeerName;

	public int QueuedMessageCount => _connection.IncomingMessages.Count;

	public int SentPollCount { get; internal set; }

	public int ProcessedMessageCount { get; internal set; }

	public Task SendAsync<TPayload>(string messageType, TPayload payload, CancellationToken cancellationToken = default) =>
		_connectionService.SendAndLogAsync(_connection, messageType, payload, cancellationToken);
}
