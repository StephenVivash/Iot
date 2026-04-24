using System.Collections.Concurrent;
using System.Net;

namespace PeerJsonSockets;

internal enum PeerRole
{
	Client,
	Server
}

internal sealed class PeerConnection
{
	private readonly CancellationTokenSource _shutdown;

	public PeerConnection(Guid id, JsonSocketPeer peer, PeerRole role, CancellationTokenSource shutdown)
	{
		Id = id;
		Peer = peer;
		Role = role;
		_shutdown = shutdown;
	}

	public Guid Id { get; }

	public JsonSocketPeer Peer { get; }

	public PeerRole Role { get; }

	public string? RemotePeerName { get; private set; }

	public string RemoteDisplayName => PeerConnectionDisplay.Format(RemotePeerName, RemoteEndPoint);

	public EndPoint? RemoteEndPoint => Peer.RemoteEndPoint;

	public ConcurrentQueue<JsonPeerMessage> IncomingMessages { get; } = new();

	public CancellationToken CancellationToken => _shutdown.Token;

	public void SetRemotePeerName(string peerName) => RemotePeerName = peerName;

	public void Stop() => _shutdown.Cancel();
}
