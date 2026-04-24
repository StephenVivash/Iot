using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace PeerJsonSockets;

public sealed class JsonSocketPeer : IAsyncDisposable
{
	public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = false
	};

	private readonly TcpClient _client;
	private readonly StreamReader _reader;
	private readonly StreamWriter _writer;
	private readonly SemaphoreSlim _sendLock = new(1, 1);

	public JsonSocketPeer(TcpClient client)
	{
		_client = client;

		NetworkStream stream = client.GetStream();
		_reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
		_writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), bufferSize: 4096, leaveOpen: true)
		{
			AutoFlush = true,
			NewLine = "\n"
		};
	}

	public EndPoint? RemoteEndPoint => _client.Client.RemoteEndPoint;

	public static async Task<JsonSocketPeer> ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
	{
		TcpClient client = new();
		await client.ConnectAsync(host, port, cancellationToken);
		client.NoDelay = true;

		return new JsonSocketPeer(client);
	}

	public async Task SendAsync<TPayload>(string type, TPayload payload, CancellationToken cancellationToken = default)
	{
		using JsonDocument payloadDocument = JsonSerializer.SerializeToDocument(payload, SerializerOptions);
		JsonPeerMessage message = new(
			type,
			payloadDocument.RootElement.Clone(),
			Guid.NewGuid().ToString("N"),
			DateTimeOffset.UtcNow);

		string json = JsonSerializer.Serialize(message, SerializerOptions);

		await _sendLock.WaitAsync(cancellationToken);
		try
		{
			await _writer.WriteLineAsync(json.AsMemory(), cancellationToken);
			await _writer.FlushAsync(cancellationToken);
		}
		finally
		{
			_sendLock.Release();
		}
	}

	public async Task<JsonPeerMessage?> ReceiveAsync(CancellationToken cancellationToken = default)
	{
		string? json = await _reader.ReadLineAsync(cancellationToken);
		if (string.IsNullOrWhiteSpace(json))
		{
			return null;
		}

		return JsonSerializer.Deserialize<JsonPeerMessage>(json, SerializerOptions);
	}

	public static TPayload? ReadPayload<TPayload>(JsonPeerMessage message) =>
		message.Payload.Deserialize<TPayload>(SerializerOptions);

	public async ValueTask DisposeAsync()
	{
		await _sendLock.WaitAsync();
		try
		{
			await _writer.DisposeAsync();
			_reader.Dispose();
			_client.Dispose();
		}
		finally
		{
			_sendLock.Release();
			_sendLock.Dispose();
		}
	}
}
