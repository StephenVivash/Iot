namespace PeerJsonSockets.Nano
{
	public interface IPeerClientLoopTask
	{
		string Name { get; }

		int IntervalMilliseconds { get; }

		void Execute(PeerClientLoopContext context);
	}
}
