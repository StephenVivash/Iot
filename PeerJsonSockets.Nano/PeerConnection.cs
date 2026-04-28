using System;
using System.Collections;
using System.Net;

namespace PeerJsonSockets.Nano
{
	public sealed class PeerConnection
	{
		private readonly Queue _incomingMessages = new Queue();

		public PeerConnection(string id, JsonSocketPeer peer, PeerRole role)
		{
			Id = id;
			Peer = peer;
			Role = role;
			IsStopped = false;
		}

		public string Id { get; private set; }

		public JsonSocketPeer Peer { get; private set; }

		public PeerRole Role { get; private set; }

		public string RemotePeerName { get; private set; }

		public bool IsStopped { get; private set; }

		public EndPoint RemoteEndPoint
		{
			get { return Peer.RemoteEndPoint; }
		}

		public string RemoteDisplayName
		{
			get { return PeerConnectionDisplay.Format(RemotePeerName, RemoteEndPoint); }
		}

		public int QueuedMessageCount
		{
			get { return _incomingMessages.Count; }
		}

		public void SetRemotePeerName(string peerName)
		{
			RemotePeerName = peerName;
		}

		public void Enqueue(JsonPeerMessage message)
		{
			lock (_incomingMessages.SyncRoot)
			{
				_incomingMessages.Enqueue(message);
			}
		}

		public JsonPeerMessage Dequeue()
		{
			lock (_incomingMessages.SyncRoot)
			{
				if (_incomingMessages.Count == 0)
				{
					return null;
				}

				return (JsonPeerMessage)_incomingMessages.Dequeue();
			}
		}

		public void Stop()
		{
			IsStopped = true;
			Peer.Close();
		}
	}
}
