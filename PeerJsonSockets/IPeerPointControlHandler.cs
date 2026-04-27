namespace PeerJsonSockets;

public interface IPeerPointControlHandler
{
	Task<PointStatus?> TryHandlePointControlAsync(PointControl pointControl, CancellationToken cancellationToken);
}
