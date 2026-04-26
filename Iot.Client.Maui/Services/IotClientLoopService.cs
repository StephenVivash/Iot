using Microsoft.Extensions.Logging;
using PeerJsonSockets;

namespace Iot.Client.Maui.Services;

public sealed class IotClientLoopService : IDisposable
{
	private static readonly PeerAddress LocalPeerAddress = new("localhost", 5050);

	private readonly ILogger _logger;
	private readonly Lock _lock = new();
	private readonly PeerClientService _peerClientService;
	private readonly PeerRuntimeOptions _options;
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
	}

	public void Start()
	{
		lock (_lock)
		{
			if (_runTask is not null)
			{
				return;
			}

			_shutdown = new CancellationTokenSource();
			_runTask = RunAsync(_shutdown.Token);
		}
	}

	public void Dispose()
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

	private async Task RunAsync(CancellationToken cancellationToken)
	{
		_logger.LogWarning("Host name: {PeerName}", _options.PeerName);
		_logger.LogWarning("Client connecting to {PeerAddress}.", LocalPeerAddress);

		try
		{
			await _peerClientService.RunAsync(LocalPeerAddress, cancellationToken);
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
