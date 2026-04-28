using System;
using System.Threading;

namespace PeerJsonSockets.Nano
{
	public sealed class PeerClientService
	{
		private readonly PeerRuntimeOptions _options;
		private readonly PeerConnectionRegistry _connectionRegistry;
		private readonly PeerConnectionService _connectionService;
		private readonly NanoLog _log;
		private readonly IPeerClientLoopTask[] _loopTasks;
		private readonly IPeerPointControlHandler[] _pointControlHandlers;

		public PeerClientService(
			PeerRuntimeOptions options,
			PeerConnectionRegistry connectionRegistry,
			PeerConnectionService connectionService,
			NanoLog log,
			IPeerPointControlHandler[] pointControlHandlers)
			: this(options, connectionRegistry, connectionService, log, null, pointControlHandlers)
		{
		}

		public PeerClientService(
			PeerRuntimeOptions options,
			PeerConnectionRegistry connectionRegistry,
			PeerConnectionService connectionService,
			NanoLog log,
			IPeerClientLoopTask[] loopTasks,
			IPeerPointControlHandler[] pointControlHandlers)
		{
			_options = options;
			_connectionRegistry = connectionRegistry;
			_connectionService = connectionService;
			_log = log;
			_loopTasks = loopTasks == null ? new IPeerClientLoopTask[0] : loopTasks;
			_pointControlHandlers = pointControlHandlers == null ? new IPeerPointControlHandler[0] : pointControlHandlers;
		}

		public void Run(PeerAddress peerAddress)
		{
			int reconnectAttempt = 0;

			while (true)
			{
				try
				{
					ConnectAndRun(peerAddress);
					reconnectAttempt = 0;
					_log.Info("Client outbound connection to " + peerAddress.ToString() + " closed.");
				}
				catch (Exception ex)
				{
					_log.Error("Client outbound connection to " + peerAddress.ToString() + " failed.", ex);
				}

				reconnectAttempt++;
				int delay = reconnectAttempt <= _options.FastReconnectAttempts
					? _options.FastReconnectDelayMilliseconds
					: _options.SlowReconnectDelayMilliseconds;
				_log.Info("Client reconnecting to " + peerAddress.ToString() + " in " + (delay / 1000).ToString() + " seconds.");
				Thread.Sleep(delay);
			}
		}

		private void ConnectAndRun(PeerAddress peerAddress)
		{
			JsonSocketPeer peer = JsonSocketPeer.Connect(peerAddress.Host, peerAddress.Port);
			_log.Warn("Client connected to " + _connectionService.GetRemoteDisplayName(peer) + ".");

			_connectionService.SendAndLog(PeerRole.Client, peer, PeerMessages.HandshakeType, PeerMessages.CreateHandshake(_options.PeerName));
			JsonPeerMessage ackMessage = _connectionService.ReceiveAndLog(PeerRole.Client, peer);
			if (ackMessage == null)
			{
				throw new InvalidOperationException("Client connection closed during handshake.");
			}

			_connectionService.SendAndLog(PeerRole.Client, peer, PeerMessages.StatusType, PeerMessages.CreateStatus(_options.PeerName, 1));
			JsonPeerMessage statusMessage = _connectionService.ReceiveAndLog(PeerRole.Client, peer);
			if (statusMessage == null)
			{
				throw new InvalidOperationException("Client connection closed during status exchange.");
			}

			RunConnectedPeer(peer);
		}

		private void RunConnectedPeer(JsonSocketPeer peer)
		{
			PeerConnection connection = new PeerConnection(Guid.NewGuid().ToString(), peer, PeerRole.Client);
			_connectionService.ApplyKnownPeerName(connection);
			_connectionRegistry.Register(connection);
			_log.Warn("Client registered server connection " + connection.RemoteDisplayName + ".");

			Thread readerThread = new Thread(new ThreadStart(delegate
			{
				ReadPeerMessages(connection);
			}));
			readerThread.Start();

			try
			{
				RunClientLoop(connection);
			}
			finally
			{
				connection.Stop();
				_connectionRegistry.Unregister(connection.Id);
			}
		}

		private void ReadPeerMessages(PeerConnection connection)
		{
			try
			{
				while (!connection.IsStopped)
				{
					JsonPeerMessage message = _connectionService.ReceiveAndLog(connection);
					if (message == null)
					{
						break;
					}

					connection.Enqueue(message);
				}
			}
			catch (Exception ex)
			{
				if (!connection.IsStopped)
				{
					_log.Error("Client reader failed.", ex);
				}
			}
			finally
			{
				connection.Stop();
			}
		}

		private void RunClientLoop(PeerConnection connection)
		{
			LoopTaskScheduler scheduler = new LoopTaskScheduler(_log);
			PeerClientLoopContext context = new PeerClientLoopContext(connection, _connectionService);
			scheduler.Register("client.poll", _options.PollIntervalMilliseconds, RunClientPoll);
			scheduler.Register("client.summary", _options.MaintenanceIntervalMilliseconds, RunClientSummary);
			for (int i = 0; i < _loopTasks.Length; i++)
			{
				IPeerClientLoopTask loopTask = _loopTasks[i];
				scheduler.Register(loopTask.Name, loopTask.IntervalMilliseconds, delegate(object taskContext)
				{
					loopTask.Execute((PeerClientLoopContext)taskContext);
				});
			}

			while (!connection.IsStopped)
			{
				scheduler.RunDueTasks(context);
				JsonPeerMessage message = connection.Dequeue();
				while (message != null)
				{
					ProcessClientMessage(connection, message);
					context.ProcessedMessageCount++;
					message = connection.Dequeue();
				}

				Thread.Sleep(_options.LoopDelayMilliseconds);
			}
		}

		private void RunClientPoll(object context)
		{
			PeerClientLoopContext clientContext = (PeerClientLoopContext)context;
			clientContext.Send(PeerMessages.PollType, PeerMessages.CreatePoll(_options.PeerName));
			clientContext.SentPollCount++;
		}

		private void RunClientSummary(object context)
		{
			PeerClientLoopContext clientContext = (PeerClientLoopContext)context;
			_log.Info("Client summary for " + clientContext.RemoteDisplayName + ". Polls: " + clientContext.SentPollCount.ToString() + ". Processed: " + clientContext.ProcessedMessageCount.ToString() + ".");
		}

		private void ProcessClientMessage(PeerConnection connection, JsonPeerMessage message)
		{
			if (message.Type == PeerMessages.PollAckType)
			{
				return;
			}

			if (message.Type == PeerMessages.PointControlType)
			{
				PointControl pointControl = (PointControl)connection.Peer.ReadPayload(message, typeof(PointControl));
				if (pointControl != null)
				{
					ProcessPointControl(connection, pointControl);
				}
				return;
			}

			if (message.Type == PeerMessages.PointStatusType)
			{
				RelayPointStatusToConnectedClients(connection, message);
			}
		}

		private void ProcessPointControl(PeerConnection connection, PointControl pointControl)
		{
			for (int i = 0; i < _pointControlHandlers.Length; i++)
			{
				PointStatus pointStatus = _pointControlHandlers[i].TryHandlePointControl(pointControl);
				if (pointStatus != null)
				{
					_connectionService.SendAndLog(connection, PeerMessages.PointStatusType, pointStatus);
					RelayPointStatusToConnectedClients(connection, pointStatus);
					return;
				}
			}

			RelayPointControlToConnectedClients(connection, pointControl);
		}

		private void RelayPointStatusToConnectedClients(PeerConnection sourceConnection, JsonPeerMessage pointStatusMessage)
		{
			PointStatus pointStatus = (PointStatus)sourceConnection.Peer.ReadPayload(pointStatusMessage, typeof(PointStatus));
			if (pointStatus != null)
			{
				RelayPointStatusToConnectedClients(sourceConnection, pointStatus);
			}
		}

		private void RelayPointStatusToConnectedClients(PeerConnection sourceConnection, PointStatus pointStatus)
		{
			PeerConnection[] connections = _connectionRegistry.GetByRole(PeerRole.Server);
			for (int i = 0; i < connections.Length; i++)
			{
				if (connections[i].Id != sourceConnection.Id && !connections[i].IsStopped)
				{
					_connectionService.SendAndLog(connections[i], PeerMessages.PointStatusType, pointStatus);
				}
			}
		}

		private void RelayPointControlToConnectedClients(PeerConnection sourceConnection, PointControl pointControl)
		{
			PeerConnection[] connections = _connectionRegistry.GetByRole(PeerRole.Server);
			for (int i = 0; i < connections.Length; i++)
			{
				if (connections[i].Id != sourceConnection.Id && !connections[i].IsStopped)
				{
					_connectionService.SendAndLog(connections[i], PeerMessages.PointControlType, pointControl);
				}
			}
		}
	}
}
