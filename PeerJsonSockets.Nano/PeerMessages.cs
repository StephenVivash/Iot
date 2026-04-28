using System;

namespace PeerJsonSockets.Nano
{
	public sealed class Handshake
	{
		public string peerName { get; set; }
		public string protocolVersion { get; set; }
		public string[] supportedMessageTypes { get; set; }
	}

	public sealed class HandshakeAck
	{
		public string peerName { get; set; }
		public bool accepted { get; set; }
		public string message { get; set; }
	}

	public sealed class Poll
	{
		public string peerName { get; set; }
		public string pollId { get; set; }
		public string sentAtUtc { get; set; }
	}

	public sealed class PollAck
	{
		public string peerName { get; set; }
		public string pollId { get; set; }
		public string receivedAtUtc { get; set; }
	}

	public sealed class PeerStatus
	{
		public string peerName { get; set; }
		public string state { get; set; }
		public int activeConnections { get; set; }
	}

	public sealed class PointStatus
	{
		public int id { get; set; }
		public string status { get; set; }
	}

	public sealed class PointControl
	{
		public int id { get; set; }
		public string status { get; set; }
	}

	public static class PeerMessages
	{
		public const string HandshakeType = "handshake";
		public const string HandshakeAckType = "handshake.ack";
		public const string PollType = "poll";
		public const string PollAckType = "poll.ack";
		public const string StatusType = "peer.status";
		public const string PointStatusType = "point.status";
		public const string PointControlType = "point.control";

		public static Handshake CreateHandshake(string peerName)
		{
			Handshake handshake = new Handshake();
			handshake.peerName = peerName;
			handshake.protocolVersion = "1.0";
			handshake.supportedMessageTypes = new string[]
			{
				HandshakeType,
				HandshakeAckType,
				PollType,
				PollAckType,
				StatusType,
				PointStatusType,
				PointControlType
			};
			return handshake;
		}

		public static HandshakeAck CreateHandshakeAck(string peerName)
		{
			HandshakeAck ack = new HandshakeAck();
			ack.peerName = peerName;
			ack.accepted = true;
			ack.message = "Handshake accepted.";
			return ack;
		}

		public static Poll CreatePoll(string peerName)
		{
			Poll poll = new Poll();
			poll.peerName = peerName;
			poll.pollId = Guid.NewGuid().ToString();
			poll.sentAtUtc = DateTime.UtcNow.ToString("s") + "Z";
			return poll;
		}

		public static PollAck CreatePollAck(string peerName, string pollId)
		{
			PollAck ack = new PollAck();
			ack.peerName = peerName;
			ack.pollId = pollId;
			ack.receivedAtUtc = DateTime.UtcNow.ToString("s") + "Z";
			return ack;
		}

		public static PeerStatus CreateStatus(string peerName, int activeConnections)
		{
			PeerStatus status = new PeerStatus();
			status.peerName = peerName;
			status.state = "ready";
			status.activeConnections = activeConnections;
			return status;
		}

		public static PointStatus CreatePointStatus(int id, string status)
		{
			PointStatus pointStatus = new PointStatus();
			pointStatus.id = id;
			pointStatus.status = status;
			return pointStatus;
		}

		public static PointControl CreatePointControl(int id, string status)
		{
			PointControl pointControl = new PointControl();
			pointControl.id = id;
			pointControl.status = status;
			return pointControl;
		}
	}
}
