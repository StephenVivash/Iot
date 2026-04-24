using Microsoft.Extensions.Logging;

namespace Iot.Client.Maui.Logging;

[ProviderAlias("File")]
internal sealed class TimestampedFileLoggerProvider : ILoggerProvider
{
	private readonly Lock _lock = new();
	private readonly StreamWriter _writer;

	public TimestampedFileLoggerProvider(string logFilePath)
	{
		string? directory = Path.GetDirectoryName(logFilePath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
		}

		FileStream stream = new(logFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
		_writer = new StreamWriter(stream)
		{
			AutoFlush = true
		};
	}

	public ILogger CreateLogger(string categoryName) => new TimestampedFileLogger(_writer, _lock);

	public void Dispose()
	{
		lock (_lock)
		{
			_writer.Dispose();
		}
	}

	private sealed class TimestampedFileLogger : ILogger
	{
		private readonly Lock _lock;
		private readonly StreamWriter _writer;

		public TimestampedFileLogger(StreamWriter writer, Lock lockObject)
		{
			_writer = writer;
			_lock = lockObject;
		}

		public IDisposable? BeginScope<TState>(TState state)
			where TState : notnull
		{
			return NullScope.Instance;
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

			lock (_lock)
			{
				_writer.Write(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
				_writer.Write(" [");
				_writer.Write(logLevel);
				_writer.Write("] ");
				_writer.WriteLine(message);

				if (exception is not null)
				{
					_writer.WriteLine(exception);
				}
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
