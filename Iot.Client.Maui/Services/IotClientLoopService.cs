using Microsoft.Extensions.Logging;
using PeerJsonSockets;

namespace Iot.Client.Maui.Services;

public sealed class IotClientLoopService : IDisposable
{
	private const int DefaultPort = 5050;

	private readonly ILogger _logger;
	private readonly Lock _lock = new();
	private readonly PeerClientService _peerClientService;
	private readonly PeerRuntimeOptions _options;
	private PeerAddress? _peerAddress;
	private CancellationTokenSource? _shutdown;
	private Task? _runTask;

	public IotClientLoopService(
		PeerRuntimeOptions options,
		PeerClientService peerClientService,
		ILoggerFactory loggerFactory)
	{
		_options = options;
		_peerClientService = peerClientService;
		_logger = loggerFactory.CreateLogger("Iot.Client.Maui");
		_peerClientService.PointStatusReceived += OnPointStatusReceived;
	}

	public event Action<PointStatus>? PointStatusReceived;

	private void OnPointStatusReceived(PointStatus pointStatus) =>
		PointStatusReceived?.Invoke(pointStatus);

	public void Start(PeerAddress peerAddress)
	{
		lock (_lock)
		{
			if (_runTask is not null)
			{
				return;
			}

			_peerAddress = peerAddress;
			_shutdown = new CancellationTokenSource();
			_runTask = RunAsync(_shutdown.Token);
		}
	}

	public void ConnectToServer(string serverName)
	{
		PeerAddress peerAddress = new($"{serverName}.local", DefaultPort);
		Stop();
		Start(peerAddress);
	}

	public void Dispose()
	{
		_peerClientService.PointStatusReceived -= OnPointStatusReceived;

		Task? runTask;
		CancellationTokenSource? shutdown;

		lock (_lock)
		{
			runTask = _runTask;
			shutdown = _shutdown;
			_runTask = null;
			_shutdown = null;
		}

		if (shutdown is null)
		{
			return;
		}

		shutdown.Cancel();

		try
		{
			runTask?.GetAwaiter().GetResult();
		}
		catch (OperationCanceledException)
		{
		}
		finally
		{
			shutdown.Dispose();
		}
	}

	public void Stop()
	{
		Task? runTask;
		CancellationTokenSource? shutdown;

		lock (_lock)
		{
			runTask = _runTask;
			shutdown = _shutdown;
			_runTask = null;
			_shutdown = null;
		}

		if (shutdown is null)
		{
			return;
		}

		shutdown.Cancel();
		if (runTask is null)
		{
			shutdown.Dispose();
			return;
		}

		_ = runTask.ContinueWith(
			task =>
			{
				try
				{
					task.GetAwaiter().GetResult();
				}
				catch (OperationCanceledException)
				{
				}
				finally
				{
					shutdown.Dispose();
				}
			},
			TaskScheduler.Default);
	}

	public Task<bool> SendPointControlAsync(int pointId, string status, CancellationToken cancellationToken = default) =>
		_peerClientService.SendPointControlAsync(PeerMessages.CreatePointControl(pointId, status), cancellationToken);

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		PeerAddress peerAddress = _peerAddress ?? new("pi51.local", DefaultPort);
		_logger.LogWarning("Host name: {PeerName}", _options.PeerName);
		_logger.LogWarning("Client connecting to {PeerAddress}.", peerAddress);

		try
		{
			await _peerClientService.RunAsync(peerAddress, cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Client loop failed.");
		}
	}
}
