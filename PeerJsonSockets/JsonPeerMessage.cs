using System.Text.Json;

namespace PeerJsonSockets;

public sealed record JsonPeerMessage(
	string Type,
	JsonElement Payload,
	string Id,
	DateTimeOffset SentAtUtc);
