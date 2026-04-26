namespace PeerJsonSockets;

public interface IPeerClientLoopTask
{
	string Name { get; }

	TimeSpan Interval { get; }

	Task ExecuteAsync(PeerClientLoopContext context, CancellationToken cancellationToken);
}
