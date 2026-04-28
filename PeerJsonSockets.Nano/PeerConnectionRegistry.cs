using System.Collections;

namespace PeerJsonSockets.Nano
{
	public sealed class PeerConnectionRegistry
	{
		private readonly ArrayList _connections = new ArrayList();

		public int Count
		{
			get { lock (_connections.SyncRoot) { return _connections.Count; } }
		}

		public void Register(PeerConnection connection)
		{
			lock (_connections.SyncRoot)
			{
				_connections.Add(connection);
			}
		}

		public void Unregister(string id)
		{
			lock (_connections.SyncRoot)
			{
				for (int i = _connections.Count - 1; i >= 0; i--)
				{
					PeerConnection connection = (PeerConnection)_connections[i];
					if (connection.Id == id)
					{
						_connections.RemoveAt(i);
					}
				}
			}
		}

		public int CountByRole(PeerRole role)
		{
			int count = 0;
			lock (_connections.SyncRoot)
			{
				for (int i = 0; i < _connections.Count; i++)
				{
					PeerConnection connection = (PeerConnection)_connections[i];
					if (connection.Role == role && !connection.IsStopped)
					{
						count++;
					}
				}
			}

			return count;
		}

		public PeerConnection[] GetAll()
		{
			lock (_connections.SyncRoot)
			{
				PeerConnection[] result = new PeerConnection[_connections.Count];
				for (int i = 0; i < _connections.Count; i++)
				{
					result[i] = (PeerConnection)_connections[i];
				}

				return result;
			}
		}

		public PeerConnection[] GetByRole(PeerRole role)
		{
			lock (_connections.SyncRoot)
			{
				int count = CountByRole(role);
				PeerConnection[] result = new PeerConnection[count];
				int index = 0;
				for (int i = 0; i < _connections.Count; i++)
				{
					PeerConnection connection = (PeerConnection)_connections[i];
					if (connection.Role == role && !connection.IsStopped)
					{
						result[index++] = connection;
					}
				}

				return result;
			}
		}
	}
}
