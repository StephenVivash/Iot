using System;

namespace PeerJsonSockets.Nano
{
	public sealed class JsonPeerMessage
	{
		public JsonPeerMessage()
		{
			Type = string.Empty;
			PayloadJson = "{}";
			Id = string.Empty;
			SentAtUtc = string.Empty;
		}

		public string Type { get; set; }

		public string PayloadJson { get; set; }

		public string Id { get; set; }

		public string SentAtUtc { get; set; }
	}
}
