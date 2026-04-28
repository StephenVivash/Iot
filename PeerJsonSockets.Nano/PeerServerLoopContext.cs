namespace PeerJsonSockets.Nano
{
	public sealed class PeerServerLoopContext
	{
		private readonly PeerConnectionRegistry _connectionRegistry;
		private readonly PeerConnectionService _connectionService;

		internal PeerServerLoopContext(PeerConnectionRegistry connectionRegistry, PeerConnectionService connectionService)
		{
			_connectionRegistry = connectionRegistry;
			_connectionService = connectionService;
		}

		public int ConnectedPeerCount
		{
			get { return _connectionRegistry.Count; }
		}

		public int ConnectedClientCount
		{
			get { return _connectionRegistry.CountByRole(PeerRole.Server); }
		}

		public int ConnectedServerCount
		{
			get { return _connectionRegistry.CountByRole(PeerRole.Client); }
		}

		public void SendToConnectedClients(string messageType, object payload)
		{
			PeerConnection[] connections = _connectionRegistry.GetByRole(PeerRole.Server);
			for (int i = 0; i < connections.Length; i++)
			{
				if (!connections[i].IsStopped)
				{
					_connectionService.SendAndLog(connections[i], messageType, payload);
				}
			}
		}

		public void SendToConnectedPeers(string messageType, object payload)
		{
			PeerConnection[] connections = _connectionRegistry.GetAll();
			for (int i = 0; i < connections.Length; i++)
			{
				if (!connections[i].IsStopped)
				{
					_connectionService.SendAndLog(connections[i], messageType, payload);
				}
			}
		}
	}
}
