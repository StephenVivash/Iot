using System;

namespace PeerJsonSockets.Nano
{
	public sealed class PeerRuntimeOptions
	{
		public PeerRuntimeOptions(string peerName)
		{
			PeerName = peerName;
			LoopDelayMilliseconds = 1000;
			PollIntervalMilliseconds = 10 * 60 * 1000;
			SummaryIntervalMilliseconds = 4 * 60 * 60 * 1000;
			MaintenanceIntervalMilliseconds = 60 * 1000;
			FastReconnectDelayMilliseconds = 10 * 1000;
			SlowReconnectDelayMilliseconds = 60 * 1000;
			FastReconnectAttempts = 10;
		}

		public string PeerName { get; private set; }
		public int LoopDelayMilliseconds { get; set; }
		public int PollIntervalMilliseconds { get; set; }
		public int SummaryIntervalMilliseconds { get; set; }
		public int MaintenanceIntervalMilliseconds { get; set; }
		public int FastReconnectDelayMilliseconds { get; set; }
		public int SlowReconnectDelayMilliseconds { get; set; }
		public int FastReconnectAttempts { get; set; }
	}
}
