using PeerJsonSockets.Nano;

namespace Iot.Server.Nano
{
	public sealed class ServerHeartbeatTask : IPeerServerLoopTask
	{
		private readonly NanoLog _log;

		public ServerHeartbeatTask(NanoLog log)
		{
			_log = log;
		}

		public string Name
		{
			get { return "server.heartbeat"; }
		}

		public int IntervalMilliseconds
		{
			get { return 60 * 1000; }
		}

		public void Execute(PeerServerLoopContext context)
		{
			_log.Info("Server heartbeat. Connected peers: " + context.ConnectedPeerCount.ToString() + ".");
		}
	}
}
