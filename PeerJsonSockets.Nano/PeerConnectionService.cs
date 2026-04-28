using System;
using System.Collections;

namespace PeerJsonSockets.Nano
{
	public sealed class PeerConnectionService
	{
		private readonly Hashtable _peerNamesByEndpoint = new Hashtable();
		private readonly NanoLog _log;

		public PeerConnectionService(NanoLog log)
		{
			_log = log;
		}

		public JsonPeerMessage ReceiveAndLog(PeerConnection connection)
		{
			JsonPeerMessage message = ReceiveAndLog(connection.Role, connection.Peer);
			if (message != null)
			{
				string peerName = TryReadPeerName(message);
				if (peerName.Length > 0)
				{
					connection.SetRemotePeerName(peerName);
				}
			}

			return message;
		}

		public JsonPeerMessage ReceiveAndLog(PeerRole role, JsonSocketPeer peer)
		{
			JsonPeerMessage message = peer.Receive();
			if (message == null)
			{
				_log.Warn(role.ToString() + " connection " + GetRemoteDisplayName(peer) + " closed.");
				return null;
			}

			string peerName = TryReadPeerName(message);
			if (peerName.Length > 0)
			{
				RememberPeerName(peer, peerName);
			}

			_log.Debug(role.ToString() + " received " + message.Type + " from " + GetRemoteDisplayName(peer) + ".");
			return message;
		}

		public void SendAndLog(PeerConnection connection, string messageType, object payload)
		{
			SendAndLog(connection.Role, connection.Peer, messageType, payload);
		}

		public void SendAndLog(PeerRole role, JsonSocketPeer peer, string messageType, object payload)
		{
			peer.Send(messageType, payload);
			_log.Debug(role.ToString() + " sent " + messageType + " to " + GetRemoteDisplayName(peer) + ".");
		}

		public void ApplyKnownPeerName(PeerConnection connection)
		{
			string key = GetEndpointKey(connection.Peer);
			if (key != null && _peerNamesByEndpoint.Contains(key))
			{
				connection.SetRemotePeerName((string)_peerNamesByEndpoint[key]);
			}
		}

		public string GetRemoteDisplayName(JsonSocketPeer peer)
		{
			string key = GetEndpointKey(peer);
			string peerName = null;
			if (key != null && _peerNamesByEndpoint.Contains(key))
			{
				peerName = (string)_peerNamesByEndpoint[key];
			}

			return PeerConnectionDisplay.Format(peerName, peer.RemoteEndPoint);
		}

		private void RememberPeerName(JsonSocketPeer peer, string peerName)
		{
			string key = GetEndpointKey(peer);
			if (key != null)
			{
				_peerNamesByEndpoint[key] = peerName;
			}
		}

		private static string GetEndpointKey(JsonSocketPeer peer)
		{
			return peer.RemoteEndPoint == null ? null : peer.RemoteEndPoint.ToString();
		}

		private static string TryReadPeerName(JsonPeerMessage message)
		{
			return JsonWire.GetString(message.PayloadJson, "peerName");
		}
	}
}
