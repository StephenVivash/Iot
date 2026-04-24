using Microsoft.Extensions.Logging;

namespace PeerJsonSockets;

public static class PeerAddressParser
{
	public static PeerAddress? Parse(string value, ILogger logger)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}

		int separatorIndex = value.LastIndexOf(':');
		if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
		{
			logger.LogWarning("Ignoring outbound peer '{OutboundPeer}'. Expected ip:port.", value);
			return null;
		}

		string host = value[..separatorIndex].Trim();
		string portText = value[(separatorIndex + 1)..].Trim();

		if (host.StartsWith('[') && host.EndsWith(']'))
		{
			host = host[1..^1];
		}

		if (!int.TryParse(portText, out int peerPort))
		{
			logger.LogWarning("Ignoring outbound peer '{OutboundPeer}'. Port must be a number.", value);
			return null;
		}

		return new PeerAddress(host, peerPort);
	}
}
