using Microsoft.Extensions.Logging;

namespace Iot.Client.Maui.Logging;

[ProviderAlias("Console")]
internal sealed class MainPageLoggerProvider : ILoggerProvider
{
	private readonly MainPageLogSink _sink;

	public MainPageLoggerProvider(MainPageLogSink sink)
	{
		_sink = sink;
	}

	public ILogger CreateLogger(string categoryName) => new MainPageLogger(_sink);

	public void Dispose()
	{
	}

	private sealed class MainPageLogger : ILogger
	{
		private readonly MainPageLogSink _sink;

		public MainPageLogger(MainPageLogSink sink)
		{
			_sink = sink;
		}

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull
		{
			return NullScope.Instance;
		}


		private static string LogLevelString(LogLevel logLevel)
		{
			return logLevel switch
			{
				LogLevel.Trace => "Trace",
				LogLevel.Debug => "Debug",
				LogLevel.Information => "Info",
				LogLevel.Warning => "Warn",
				LogLevel.Error => "Fail",
				LogLevel.Critical => "Crit",
				_ => logLevel.ToString()
			};
		}

		public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter)
		{
			if (!IsEnabled(logLevel))
			{
				return;
			}

			string message = formatter(state, exception);
			if (string.IsNullOrWhiteSpace(message) && exception is null)
			{
				return;
			}

			_sink.WriteLine($"{DateTimeOffset.Now:HH:mm:ss} [{LogLevelString(logLevel)}] {message}");

			if (exception is not null)
			{
				_sink.WriteLine(exception.ToString());
			}
		}
	}

	private sealed class NullScope : IDisposable
	{
		public static NullScope Instance { get; } = new();

		public void Dispose()
		{
		}
	}
}
