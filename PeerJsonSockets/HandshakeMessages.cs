using System.Text.Json;

namespace PeerJsonSockets;

public sealed record Handshake(string PeerName, string ProtocolVersion, string[] SupportedMessageTypes);

public sealed record HandshakeAck(string PeerName, bool Accepted, string Message);

public sealed record Poll(string PeerName, string PollId, DateTimeOffset SentAtUtc);

public sealed record PollAck(string PeerName, string PollId, DateTimeOffset ReceivedAtUtc);

public sealed record PeerStatus(string PeerName, string State, int ActiveConnections);

public static class HandshakeMessages
{
	public const string HandshakeType = "handshake";
	public const string AckType = "handshake.ack";
	public const string PollType = "poll";
	public const string PollAckType = "poll.ack";
	public const string StatusType = "peer.status";

	public static Handshake CreateHello(string peerName) =>
		new(peerName, "1.0", [HandshakeType, AckType, StatusType, PollType, PollAckType]);

	public static HandshakeAck CreateAck(string peerName) =>
		new(peerName, Accepted: true, Message: "Handshake accepted.");

	public static Poll CreatePoll(string peerName) =>
		new(peerName, Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);

	public static PollAck CreatePollAck(string peerName, string pollId) =>
		new(peerName, pollId, DateTimeOffset.UtcNow);

	public static PeerStatus CreateStatus(string peerName, int activeConnections) =>
		new(peerName, "ready", activeConnections);

	//public static string ExampleHelloJson(string peerName = "example-peer") =>
	//	JsonSerializer.Serialize(CreateHello(peerName), JsonSocketPeer.SerializerOptions);

	//public static string ExampleAckJson(string peerName = "example-peer") =>
	//	JsonSerializer.Serialize(CreateAck(peerName), JsonSocketPeer.SerializerOptions);
}
