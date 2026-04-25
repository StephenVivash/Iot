using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PeerJsonSockets;

public sealed class PeerConnectionService
{
	private readonly ConcurrentDictionary<string, string> _peerNamesByEndpoint = new();
	private readonly ILogger _logger;

	public PeerConnectionService(ILogger logger)
	{
		_logger = logger;
	}

	internal async Task<JsonPeerMessage?> ReceiveAndLogAsync(PeerConnection connection)
	{
		JsonPeerMessage? message = await ReceiveAndLogAsync(connection.Role, connection.Peer, connection.CancellationToken);
		if (message is not null && TryReadPeerName(message, out string? peerName))
		{
			connection.SetRemotePeerName(peerName);
		}

		return message;
	}

	internal async Task<JsonPeerMessage?> ReceiveAndLogAsync(PeerRole role, JsonSocketPeer peer, CancellationToken cancellationToken)
	{
		JsonPeerMessage? message = await peer.ReceiveAsync(cancellationToken);
		if (message is null)
		{
			_logger.LogWarning("{PeerRole} connection {RemotePeer} closed.", role, GetRemoteDisplayName(peer));
			return null;
		}

		if (TryReadPeerName(message, out string? peerName))
		{
			RememberPeerName(peer, peerName);
		}

		_logger.LogDebug(
			"{PeerRole} received {MessageType} from {RemotePeer}. Payload: {Payload}",
			role,
			message.Type,
			GetRemoteDisplayName(peer),
			message.Payload.GetRawText());

		return message;
	}

	internal Task SendAndLogAsync<TPayload>(
		PeerConnection connection,
		string messageType,
		TPayload payload,
		CancellationToken cancellationToken = default)
	{
		if (!cancellationToken.CanBeCanceled)
		{
			return SendAndLogAsync(connection.Role, connection.Peer, messageType, payload, connection.CancellationToken);
		}

		return SendAndLogWithLinkedCancellationAsync(connection, messageType, payload, cancellationToken);
	}

	private async Task SendAndLogWithLinkedCancellationAsync<TPayload>(
		PeerConnection connection,
		string messageType,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		using CancellationTokenSource linkedCancellation =
			CancellationTokenSource.CreateLinkedTokenSource(connection.CancellationToken, cancellationToken);

		await SendAndLogAsync(connection.Role, connection.Peer, messageType, payload, linkedCancellation.Token);
	}

	internal async Task SendAndLogAsync<TPayload>(
		PeerRole role,
		JsonSocketPeer peer,
		string messageType,
		TPayload payload,
		CancellationToken cancellationToken)
	{
		await peer.SendAsync(messageType, payload, cancellationToken);
		_logger.LogDebug(
			"{PeerRole} sent {MessageType} to {RemotePeer}. Payload: {Payload}",
			role,
			messageType,
			GetRemoteDisplayName(peer),
			JsonSerializer.Serialize(payload, JsonSocketPeer.SerializerOptions));
	}

	internal string GetRemoteDisplayName(PeerConnection connection) => connection.RemoteDisplayName;

	internal void ApplyKnownPeerName(PeerConnection connection)
	{
		string? endpointKey = GetEndpointKey(connection.Peer);
		if (endpointKey is not null && _peerNamesByEndpoint.TryGetValue(endpointKey, out string? peerName))
		{
			connection.SetRemotePeerName(peerName);
		}
	}

	internal string GetRemoteDisplayName(JsonSocketPeer peer)
	{
		string? endpointKey = GetEndpointKey(peer);
		if (endpointKey is not null && _peerNamesByEndpoint.TryGetValue(endpointKey, out string? peerName))
		{
			return PeerConnectionDisplay.Format(peerName, peer.RemoteEndPoint);
		}

		return PeerConnectionDisplay.Format(peerName: null, peer.RemoteEndPoint);
	}

	private void RememberPeerName(JsonSocketPeer peer, string peerName)
	{
		string? endpointKey = GetEndpointKey(peer);
		if (endpointKey is not null)
		{
			_peerNamesByEndpoint[endpointKey] = peerName;
		}
	}

	private static string? GetEndpointKey(JsonSocketPeer peer) => peer.RemoteEndPoint?.ToString();

	private static bool TryReadPeerName(JsonPeerMessage message, out string peerName)
	{
		if (message.Payload.ValueKind == JsonValueKind.Object &&
			message.Payload.TryGetProperty("peerName", out JsonElement peerNameElement) &&
			peerNameElement.ValueKind == JsonValueKind.String)
		{
			string? value = peerNameElement.GetString();
			if (!string.IsNullOrWhiteSpace(value))
			{
				peerName = value;
				return true;
			}
		}

		peerName = string.Empty;
		return false;
	}
}
