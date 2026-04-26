using Microsoft.Extensions.Logging;

namespace PeerJsonSockets;

internal sealed class LoopTaskScheduler<TContext>
{
	private readonly ILogger _logger;
	private readonly List<ScheduledLoopTask<TContext>> _tasks = [];

	public LoopTaskScheduler(ILogger logger)
	{
		_logger = logger;
	}

	public void Register(
		string name,
		TimeSpan interval,
		Func<TContext, CancellationToken, Task> executeAsync)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(name);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
		ArgumentNullException.ThrowIfNull(executeAsync);

		_tasks.Add(new ScheduledLoopTask<TContext>(name, interval, executeAsync, _logger));
	}

	public async Task RunDueTasksAsync(TContext context, CancellationToken cancellationToken)
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;

		foreach (ScheduledLoopTask<TContext> task in _tasks)
		{
			if (now < task.NextRunAtUtc)
			{
				continue;
			}

			await task.RunAsync(context, cancellationToken);
		}
	}
}

internal sealed class ScheduledLoopTask<TContext>
{
	private readonly Func<TContext, CancellationToken, Task> _executeAsync;
	private readonly ILogger _logger;

	public ScheduledLoopTask(
		string name,
		TimeSpan interval,
		Func<TContext, CancellationToken, Task> executeAsync,
		ILogger logger)
	{
		Name = name;
		Interval = interval;
		NextRunAtUtc = DateTimeOffset.UtcNow.Add(interval);
		_executeAsync = executeAsync;
		_logger = logger;
	}

	public string Name { get; }

	public TimeSpan Interval { get; }

	public DateTimeOffset NextRunAtUtc { get; private set; }

	public async Task RunAsync(TContext context, CancellationToken cancellationToken)
	{
		try
		{
			await _executeAsync(context, cancellationToken);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Scheduled task '{TaskName}' failed.", Name);
		}
		finally
		{
			NextRunAtUtc = DateTimeOffset.UtcNow.Add(Interval);
		}
	}
}
