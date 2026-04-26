namespace PeerJsonSockets;

public interface IPeerServerLoopTask
{
	string Name { get; }

	TimeSpan Interval { get; }

	Task ExecuteAsync(PeerServerLoopContext context, CancellationToken cancellationToken);
}
