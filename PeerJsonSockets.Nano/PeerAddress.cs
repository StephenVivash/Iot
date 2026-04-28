namespace PeerJsonSockets.Nano
{
	public sealed class PeerAddress
	{
		public PeerAddress(string host, int port)
		{
			Host = host;
			Port = port;
		}

		public string Host { get; private set; }

		public int Port { get; private set; }

		public override string ToString()
		{
			return Host + ":" + Port;
		}
	}
}
