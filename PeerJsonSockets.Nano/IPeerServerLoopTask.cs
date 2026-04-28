namespace PeerJsonSockets.Nano
{
	public interface IPeerServerLoopTask
	{
		string Name { get; }

		int IntervalMilliseconds { get; }

		void Execute(PeerServerLoopContext context);
	}
}
