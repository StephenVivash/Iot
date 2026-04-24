namespace PeerJsonSockets;

public sealed record PeerAddress(string Host, int Port)
{
	public override string ToString() => $"{Host}:{Port}";
}
