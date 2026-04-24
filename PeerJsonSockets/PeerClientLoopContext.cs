namespace PeerJsonSockets;

public sealed class PeerClientLoopContext
{
	private readonly PeerConnection _connection;

	internal PeerClientLoopContext(PeerConnection connection)
	{
		_connection = connection;
	}

	internal PeerConnection Connection => _connection;

	public string RemoteDisplayName => _connection.RemoteDisplayName;

	public string? RemotePeerName => _connection.RemotePeerName;

	public int QueuedMessageCount => _connection.IncomingMessages.Count;

	public int SentPollCount { get; internal set; }

	public int ProcessedMessageCount { get; internal set; }
}
