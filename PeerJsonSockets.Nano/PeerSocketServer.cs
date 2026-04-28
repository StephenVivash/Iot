using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace PeerJsonSockets.Nano
{
	public delegate void PeerAcceptedHandler(JsonSocketPeer peer);

	public sealed class PeerSocketServer
	{
		private readonly IPAddress _listenAddress;
		private readonly int _port;
		private readonly NanoLog _log;
		private bool _stopped;

		public PeerSocketServer(IPAddress listenAddress, int port, NanoLog log)
		{
			_listenAddress = listenAddress;
			_port = port;
			_log = log;
		}

		public void Run(PeerAcceptedHandler handler)
		{
			Socket listener = CreateListener();

			while (!_stopped)
			{
				try
				{
					Socket socket = listener.Accept();
					JsonSocketPeer peer = new JsonSocketPeer(socket);
					Thread worker = new Thread(new ThreadStart(delegate
					{
						handler(peer);
					}));
					worker.Start();
				}
				catch (SocketException ex)
				{
					_log.Error("Peer server accept failed; restarting listener.", ex);
					SafeClose(listener);
					Thread.Sleep(2000);
					listener = CreateListener();
				}
				catch (Exception ex)
				{
					_log.Error("Peer server accept loop failed; restarting listener.", ex);
					SafeClose(listener);
					Thread.Sleep(2000);
					listener = CreateListener();
				}
			}

			SafeClose(listener);
		}

		public void Stop()
		{
			_stopped = true;
		}

		private Socket CreateListener()
		{
			while (true)
			{
				try
				{
					Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
					listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
					listener.Bind(new IPEndPoint(_listenAddress, _port));
					listener.Listen(4);
					_log.Warn("Peer server listening on " + _port.ToString() + ".");
					return listener;
				}
				catch (Exception ex)
				{
					_log.Error("Peer server could not bind listener; retrying.", ex);
					Thread.Sleep(5000);
				}
			}
		}

		private static void SafeClose(Socket socket)
		{
			if (socket == null)
			{
				return;
			}

			try
			{
				socket.Close();
			}
			catch
			{
			}
		}
	}
}
