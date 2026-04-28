using System.Net;

namespace PeerJsonSockets.Nano
{
	internal static class PeerConnectionDisplay
	{
		public static string Format(string peerName, EndPoint endPoint)
		{
			string endpoint = endPoint == null ? "unknown endpoint" : endPoint.ToString();
			if (peerName == null || peerName.Length == 0)
			{
				return endpoint;
			}

			return peerName + " (" + endpoint + ")";
		}
	}
}
