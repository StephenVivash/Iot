using System;
using System.Net;
using System.Threading;

namespace PeerJsonSockets.Nano
{
	public sealed class PeerServerService
	{
		private readonly IPAddress _listenAddress;
		private readonly int _listenPort;
		private readonly PeerRuntimeOptions _options;
		private readonly PeerConnectionRegistry _connectionRegistry;
		private readonly PeerConnectionService _connectionService;
		private readonly NanoLog _log;
		private readonly IPeerServerLoopTask[] _loopTasks;
		private readonly IPeerPointControlHandler[] _pointControlHandlers;

		public PeerServerService(
			IPAddress listenAddress,
			int listenPort,
			PeerRuntimeOptions options,
			PeerConnectionRegistry connectionRegistry,
			PeerConnectionService connectionService,
			NanoLog log,
			IPeerServerLoopTask[] loopTasks,
			IPeerPointControlHandler[] pointControlHandlers)
		{
			_listenAddress = listenAddress;
			_listenPort = listenPort;
			_options = options;
			_connectionRegistry = connectionRegistry;
			_connectionService = connectionService;
			_log = log;
			_loopTasks = loopTasks == null ? new IPeerServerLoopTask[0] : loopTasks;
			_pointControlHandlers = pointControlHandlers == null ? new IPeerPointControlHandler[0] : pointControlHandlers;
		}

		public void Run()
		{
			PeerSocketServer server = new PeerSocketServer(_listenAddress, _listenPort, _log);
			Thread loopThread = new Thread(new ThreadStart(RunServerLoop));
			loopThread.Start();
			server.Run(HandleIncomingPeer);
		}

		private void HandleIncomingPeer(JsonSocketPeer peer)
		{
			_log.Warn("Server accepted " + _connectionService.GetRemoteDisplayName(peer) + ".");
			try
			{
				JsonPeerMessage helloMessage = _connectionService.ReceiveAndLog(PeerRole.Server, peer);
				if (helloMessage != null && helloMessage.Type == PeerMessages.HandshakeType)
				{
					_connectionService.SendAndLog(PeerRole.Server, peer, PeerMessages.HandshakeAckType, PeerMessages.CreateHandshakeAck(_options.PeerName));
				}

				JsonPeerMessage statusMessage = _connectionService.ReceiveAndLog(PeerRole.Server, peer);
				if (statusMessage != null && statusMessage.Type == PeerMessages.StatusType)
				{
					_connectionService.SendAndLog(PeerRole.Server, peer, PeerMessages.StatusType, PeerMessages.CreateStatus(_options.PeerName, _connectionRegistry.CountByRole(PeerRole.Server) + 1));
				}

				RunAcceptedClient(peer);
			}
			catch (Exception ex)
			{
				_log.Error("Server connection failed.", ex);
				peer.Close();
			}
		}

		private void RunAcceptedClient(JsonSocketPeer peer)
		{
			PeerConnection connection = new PeerConnection(Guid.NewGuid().ToString(), peer, PeerRole.Server);
			_connectionService.ApplyKnownPeerName(connection);
			_connectionRegistry.Register(connection);
			_log.Warn("Server registered accepted client " + connection.RemoteDisplayName + ". Connected clients: " + _connectionRegistry.CountByRole(PeerRole.Server).ToString() + ".");

			try
			{
				while (!connection.IsStopped)
				{
					JsonPeerMessage message = _connectionService.ReceiveAndLog(connection);
					if (message == null)
					{
						break;
					}

					ProcessAcceptedClientMessage(connection, message);
				}
			}
			catch (Exception ex)
			{
				_log.Error("Server accepted client failed.", ex);
			}
			finally
			{
				connection.Stop();
				_connectionRegistry.Unregister(connection.Id);
				_log.Info("Server unregistered accepted client " + connection.RemoteDisplayName + ".");
			}
		}

		private void ProcessAcceptedClientMessage(PeerConnection connection, JsonPeerMessage message)
		{
			if (message.Type == PeerMessages.PollType)
			{
				Poll poll = (Poll)connection.Peer.ReadPayload(message, typeof(Poll));
				string pollId = poll == null ? message.Id : poll.pollId;
				_connectionService.SendAndLog(connection, PeerMessages.PollAckType, PeerMessages.CreatePollAck(_options.PeerName, pollId));
				return;
			}

			if (message.Type == PeerMessages.PointStatusType)
			{
				PointStatus pointStatus = (PointStatus)connection.Peer.ReadPayload(message, typeof(PointStatus));
				if (pointStatus != null)
				{
					RelayPointStatusToConnectedPeers(connection, pointStatus);
				}
				return;
			}

			if (message.Type == PeerMessages.PointControlType)
			{
				PointControl pointControl = (PointControl)connection.Peer.ReadPayload(message, typeof(PointControl));
				if (pointControl != null)
				{
					ProcessPointControl(connection, pointControl);
				}
			}
		}

		private void ProcessPointControl(PeerConnection sourceConnection, PointControl pointControl)
		{
			for (int i = 0; i < _pointControlHandlers.Length; i++)
			{
				PointStatus pointStatus = _pointControlHandlers[i].TryHandlePointControl(pointControl);
				if (pointStatus != null)
				{
					SendPointStatusToConnectedPeers(pointStatus);
					return;
				}
			}

			RelayPointControlToConnectedPeers(sourceConnection, pointControl);
		}

		private void SendPointStatusToConnectedPeers(PointStatus pointStatus)
		{
			PeerConnection[] connections = _connectionRegistry.GetAll();
			for (int i = 0; i < connections.Length; i++)
			{
				if (!connections[i].IsStopped)
				{
					_connectionService.SendAndLog(connections[i], PeerMessages.PointStatusType, pointStatus);
				}
			}
		}

		private void RelayPointStatusToConnectedPeers(PeerConnection sourceConnection, PointStatus pointStatus)
		{
			PeerConnection[] connections = _connectionRegistry.GetAll();
			for (int i = 0; i < connections.Length; i++)
			{
				if (connections[i].Id != sourceConnection.Id && !connections[i].IsStopped)
				{
					_connectionService.SendAndLog(connections[i], PeerMessages.PointStatusType, pointStatus);
				}
			}
		}

		private void RelayPointControlToConnectedPeers(PeerConnection sourceConnection, PointControl pointControl)
		{
			PeerConnection[] connections = _connectionRegistry.GetAll();
			for (int i = 0; i < connections.Length; i++)
			{
				if (connections[i].Id != sourceConnection.Id && !connections[i].IsStopped)
				{
					_connectionService.SendAndLog(connections[i], PeerMessages.PointControlType, pointControl);
				}
			}
		}

		private void RunServerLoop()
		{
			LoopTaskScheduler scheduler = new LoopTaskScheduler(_log);
			scheduler.Register("server.connection-summary", _options.SummaryIntervalMilliseconds, RunServerConnectionSummary);
			scheduler.Register("server.maintenance", _options.MaintenanceIntervalMilliseconds, RunServerMaintenance);
			for (int i = 0; i < _loopTasks.Length; i++)
			{
				IPeerServerLoopTask loopTask = _loopTasks[i];
				scheduler.Register(loopTask.Name, loopTask.IntervalMilliseconds, delegate(object context)
				{
					loopTask.Execute((PeerServerLoopContext)context);
				});
			}

			PeerServerLoopContext serverContext = new PeerServerLoopContext(_connectionRegistry, _connectionService);
			while (true)
			{
				scheduler.RunDueTasks(serverContext);
				Thread.Sleep(_options.LoopDelayMilliseconds);
			}
		}

		private void RunServerConnectionSummary(object context)
		{
			PeerServerLoopContext serverContext = (PeerServerLoopContext)context;
			_log.Info("Server connection summary. Connected clients: " + serverContext.ConnectedClientCount.ToString() + ".");
		}

		private void RunServerMaintenance(object context)
		{
			PeerServerLoopContext serverContext = (PeerServerLoopContext)context;
			_log.Debug("Server maintenance. Connected peers: " + serverContext.ConnectedPeerCount.ToString() + ".");
		}
	}
}
