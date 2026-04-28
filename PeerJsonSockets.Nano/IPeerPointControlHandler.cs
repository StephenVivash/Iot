namespace PeerJsonSockets.Nano
{
	public interface IPeerPointControlHandler
	{
		PointStatus TryHandlePointControl(PointControl pointControl);
	}
}
