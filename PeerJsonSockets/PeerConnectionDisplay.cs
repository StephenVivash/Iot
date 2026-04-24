using System.Net;

namespace PeerJsonSockets;

internal static class PeerConnectionDisplay
{
	public static string Format(string? peerName, EndPoint? endPoint)
	{
		if (!string.IsNullOrWhiteSpace(peerName))
		{
			return endPoint is IPEndPoint ipEndPoint
				? $"{peerName}:{ipEndPoint.Port}"
				: peerName;
		}

		return endPoint?.ToString() ?? "unknown peer";
	}
}
