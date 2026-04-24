namespace PeerJsonSockets;

public sealed class PeerServerLoopContext
{
	private readonly IPeerConnectionRegistry _connectionRegistry;

	internal PeerServerLoopContext(IPeerConnectionRegistry connectionRegistry)
	{
		_connectionRegistry = connectionRegistry;
	}

	public int ConnectedPeerCount => _connectionRegistry.Count;

	public int ConnectedClientCount => _connectionRegistry.CountByRole(nameof(PeerRole.Server));
}
