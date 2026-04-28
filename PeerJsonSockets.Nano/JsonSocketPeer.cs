using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using nanoFramework.Json;

namespace PeerJsonSockets.Nano
{
	public sealed class JsonSocketPeer
	{
		private readonly Socket _socket;
		private readonly object _sendLock = new object();
		private readonly byte[] _receiveBuffer = new byte[1];

		public JsonSocketPeer(Socket socket)
		{
			_socket = socket;
			_socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
		}

		public EndPoint RemoteEndPoint
		{
			get { return _socket.RemoteEndPoint; }
		}

		public static JsonSocketPeer Connect(string host, int port)
		{
			IPAddress[] addresses = Dns.GetHostEntry(host).AddressList;
			if (addresses == null || addresses.Length == 0)
			{
				throw new SocketException(SocketError.HostNotFound);
			}

			Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			socket.Connect(new IPEndPoint(addresses[0], port));
			return new JsonSocketPeer(socket);
		}

		public void Send(string type, object payload)
		{
			string payloadJson = JsonConvert.SerializeObject(payload);
			string json = "{\"type\":\"" + Escape(type) + "\",\"payload\":" + payloadJson + ",\"id\":\"" +
				Guid.NewGuid().ToString() + "\",\"sentAtUtc\":\"" + DateTime.UtcNow.ToString("s") + "Z\"}\n";
			byte[] bytes = Encoding.UTF8.GetBytes(json);

			lock (_sendLock)
			{
				int offset = 0;
				while (offset < bytes.Length)
				{
					offset += _socket.Send(bytes, offset, bytes.Length - offset, SocketFlags.None);
				}
			}
		}

		public JsonPeerMessage Receive()
		{
			string line = ReadLine();
			if (line == null || line.Length == 0)
			{
				return null;
			}

			JsonPeerMessage message = new JsonPeerMessage();
			message.Type = JsonWire.GetString(line, "type");
			message.PayloadJson = JsonWire.GetObject(line, "payload");
			message.Id = JsonWire.GetString(line, "id");
			message.SentAtUtc = JsonWire.GetString(line, "sentAtUtc");
			return message;
		}

		public object ReadPayload(JsonPeerMessage message, Type type)
		{
			return JsonConvert.DeserializeObject(message.PayloadJson, type);
		}

		public void Close()
		{
			try
			{
				_socket.Close();
			}
			catch
			{
			}
		}

		private string ReadLine()
		{
			byte[] bytes = new byte[1024];
			int count = 0;

			while (true)
			{
				int received = _socket.Receive(_receiveBuffer, 0, 1, SocketFlags.None);
				if (received == 0)
				{
					return null;
				}

				byte value = _receiveBuffer[0];
				if (value == 10)
				{
					break;
				}

				if (value == 13)
				{
					continue;
				}

				if (count == bytes.Length)
				{
					byte[] expanded = new byte[bytes.Length + 1024];
					Array.Copy(bytes, expanded, bytes.Length);
					bytes = expanded;
				}

				bytes[count++] = value;
			}

			return Encoding.UTF8.GetString(bytes, 0, count);
		}

		private static string Escape(string value)
		{
			if (value == null)
			{
				return string.Empty;
			}

			return Replace(Replace(value, "\\", "\\\\"), "\"", "\\\"");
		}

		private static string Replace(string value, string oldValue, string newValue)
		{
			int index = value.IndexOf(oldValue);
			if (index < 0)
			{
				return value;
			}

			string result = string.Empty;
			int start = 0;
			while (index >= 0)
			{
				result += value.Substring(start, index - start);
				result += newValue;
				start = index + oldValue.Length;
				index = value.IndexOf(oldValue, start);
			}

			result += value.Substring(start);
			return result;
		}
	}
}
