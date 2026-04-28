namespace PeerJsonSockets.Nano
{
	public sealed class PeerClientLoopContext
	{
		private readonly PeerConnection _connection;
		private readonly PeerConnectionService _connectionService;

		internal PeerClientLoopContext(PeerConnection connection, PeerConnectionService connectionService)
		{
			_connection = connection;
			_connectionService = connectionService;
		}

		internal PeerConnection Connection
		{
			get { return _connection; }
		}

		public string RemoteDisplayName
		{
			get { return _connection.RemoteDisplayName; }
		}

		public string RemotePeerName
		{
			get { return _connection.RemotePeerName; }
		}

		public int QueuedMessageCount
		{
			get { return _connection.QueuedMessageCount; }
		}

		public int SentPollCount { get; internal set; }

		public int ProcessedMessageCount { get; internal set; }

		public void Send(string messageType, object payload)
		{
			_connectionService.SendAndLog(_connection, messageType, payload);
		}
	}
}
