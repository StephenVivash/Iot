using System;

namespace PeerJsonSockets.Nano
{
	public static class PeerAddressParser
	{
		public static PeerAddress Parse(string value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}

			int separator = value.LastIndexOf(':');
			if (separator <= 0 || separator >= value.Length - 1)
			{
				throw new ArgumentException("Expected host:port.", "value");
			}

			string host = value.Substring(0, separator);
			int port = int.Parse(value.Substring(separator + 1));
			return new PeerAddress(host, port);
		}
	}
}
