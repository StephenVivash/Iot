using System.Collections.Concurrent;

namespace PeerJsonSockets;

public interface IPeerConnectionRegistry
{
	int Count { get; }

	int CountByRole(string role);
}

public sealed class PeerConnectionRegistry : IPeerConnectionRegistry
{
	private readonly ConcurrentDictionary<Guid, PeerConnection> _connections = new();

	public int Count => _connections.Count;

	public int CountByRole(string role) =>
		_connections.Values.Count(connection => string.Equals(connection.Role.ToString(), role, StringComparison.OrdinalIgnoreCase));

	internal int CountByRole(PeerRole role) =>
		_connections.Values.Count(connection => connection.Role == role);

	internal IReadOnlyCollection<PeerConnection> GetByRole(PeerRole role) =>
		_connections.Values
			.Where(connection => connection.Role == role)
			.ToArray();

	internal IReadOnlyCollection<PeerConnection> GetAll() =>
		_connections.Values.ToArray();

	internal void Register(PeerConnection connection)
	{
		_connections[connection.Id] = connection;
	}

	internal bool Unregister(Guid connectionId) => _connections.TryRemove(connectionId, out _);
}
