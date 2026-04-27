namespace PeerJsonSockets;

public sealed class PeerRuntimeOptions
{
	public PeerRuntimeOptions(
		string peerName,
		TimeSpan? loopDelay = null,
		TimeSpan? pollInterval = null,
		TimeSpan? summaryInterval = null,
		TimeSpan? maintenanceInterval = null,
		TimeSpan? fastReconnectDelay = null,
		TimeSpan? slowReconnectDelay = null,
		int fastReconnectAttempts = 10)
	{
		PeerName = peerName;
		LoopDelay = loopDelay ?? TimeSpan.FromSeconds(1);
		PollInterval = pollInterval ?? TimeSpan.FromMinutes(10);
		SummaryInterval = summaryInterval ?? TimeSpan.FromHours(4);
		MaintenanceInterval = maintenanceInterval ?? TimeSpan.FromMinutes(1);
		FastReconnectDelay = fastReconnectDelay ?? TimeSpan.FromSeconds(10);
		SlowReconnectDelay = slowReconnectDelay ?? TimeSpan.FromMinutes(1);
		FastReconnectAttempts = fastReconnectAttempts;
	}

	public string PeerName { get; }

	public TimeSpan LoopDelay { get; }

	public TimeSpan PollInterval { get; }

	public TimeSpan SummaryInterval { get; }

	public TimeSpan MaintenanceInterval { get; }

	public TimeSpan FastReconnectDelay { get; }

	public TimeSpan SlowReconnectDelay { get; }

	public int FastReconnectAttempts { get; }
}
