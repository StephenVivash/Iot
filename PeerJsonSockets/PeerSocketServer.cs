using System.Net;
using System.Net.Sockets;

namespace PeerJsonSockets;

public sealed class PeerSocketServer : IAsyncDisposable
{
	private readonly TcpListener _listener;
	private readonly List<Task> _connections = [];

	public PeerSocketServer(IPAddress address, int port)
	{
		_listener = new TcpListener(address, port);
	}

	public IPEndPoint LocalEndPoint => (IPEndPoint)_listener.LocalEndpoint;

	public async Task RunAsync(
		Func<JsonSocketPeer, CancellationToken, Task> handlePeerAsync,
		CancellationToken cancellationToken = default)
	{
		_listener.Start();

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				TcpClient client = await _listener.AcceptTcpClientAsync(cancellationToken);
				client.NoDelay = true;

				Task connectionTask = Task.Run(async () =>
				{
					await using JsonSocketPeer peer = new(client);
					await handlePeerAsync(peer, cancellationToken);
				}, CancellationToken.None);

				lock (_connections)
				{
					_connections.RemoveAll(static task => task.IsCompleted);
					_connections.Add(connectionTask);
				}
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	public async ValueTask DisposeAsync()
	{
		_listener.Stop();

		Task[] connections;
		lock (_connections)
		{
			connections = [.. _connections];
			_connections.Clear();
		}

		try
		{
			await Task.WhenAll(connections);
		}
		catch
		{
		}
	}
}
