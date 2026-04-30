using System;
using System.Diagnostics;
using System.Device.Wifi;
using System.Net;
using System.Threading;
using nanoFramework.Networking;
using PeerJsonSockets.Nano;

namespace Iot.Server.Nano
{
	public class Program
	{
		private static readonly bool RunServer = true;
		private static readonly bool RunClient = true;
		private const int DefaultPort = 5050;

		private const string Ssid = "OPPO A52";
		private const string Password = "";
		private static readonly string ClientPeer = "pi51.local:5050";
		private static readonly int DeviceId = 8;

		public static void Main()
		{
			NanoLog log = new NanoLog("Iot.Server.Nano");
			string peerName = "nano" + DeviceId.ToString();

			log.Warn("Host name: " + peerName);
			EnsureWifiConnected(log);

			PeerRuntimeOptions options = new PeerRuntimeOptions(peerName);
			PeerConnectionRegistry connectionRegistry = new PeerConnectionRegistry();
			PeerConnectionService connectionService = new PeerConnectionService(log);
			PointStore pointStore = PointStore.CreateDefault();
			ServerGpioTask serverGpioTask = new ServerGpioTask(log, DeviceId, pointStore);

			if (RunServer)
			{
				IPeerServerLoopTask[] serverLoopTasks = new IPeerServerLoopTask[]
				{
					new ServerHeartbeatTask(log),
					serverGpioTask
				};

				IPeerPointControlHandler[] controlHandlers = new IPeerPointControlHandler[]
				{
					serverGpioTask
				};

				PeerServerService serverService = new PeerServerService(
					IPAddress.Any,
					DefaultPort,
					options,
					connectionRegistry,
					connectionService,
					log,
					serverLoopTasks,
					controlHandlers);

				Thread serverThread = new Thread(new ThreadStart(serverService.Run));
				serverThread.Start();
			}

			if (RunClient)
			{
				IPeerPointControlHandler[] controlHandlers = new IPeerPointControlHandler[]
				{
					serverGpioTask
				};

				PeerClientService clientService = new PeerClientService(
					options,
					connectionRegistry,
					connectionService,
					log,
					controlHandlers);

				PeerAddress peerAddress = PeerAddressParser.Parse(ClientPeer);
				Thread clientThread = new Thread(new ThreadStart(delegate
				{
					clientService.Run(peerAddress);
				}));
				clientThread.Start();
			}

			Debug.WriteLine("Iot.Server.Nano started.");
			Thread.Sleep(Timeout.Infinite);
		}

		private static void EnsureWifiConnected(NanoLog log)
		{
			while (true)
			{
				try
				{
					if (WifiNetworkHelper.Status == NetworkHelperStatus.NetworkIsReady)
					{
						log.Warn("WiFi already connected.");
						return;
					}

					log.Warn("Connecting WiFi SSID: " + Ssid + ". Current status: " + WifiNetworkHelper.Status.ToString());
					bool connected = false;
					try
					{
						connected = WifiNetworkHelper.Reconnect(false, 0, CancellationToken.None);
					}
					catch (Exception ex)
					{
						log.Error("WiFi reconnect using saved configuration failed.", ex);
					}

					if (!connected)
					{
						connected = WifiNetworkHelper.ConnectDhcp(
							Ssid,
							Password,
							WifiReconnectionKind.Automatic,
							false,
							0,
							CancellationToken.None);
					}

					if (connected)
					{
						log.Warn("WiFi connected.");
						return;
					}

					log.Warn("WiFi connection failed. Status: " + WifiNetworkHelper.Status.ToString());
				}
				catch (Exception ex)
				{
					log.Error("WiFi connection failed.", ex);
				}

				Thread.Sleep(5000);
			}
		}
	}
}
