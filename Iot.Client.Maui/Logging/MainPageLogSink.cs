using System.Text;

namespace Iot.Client.Maui.Logging;

public sealed class MainPageLogSink
{
	private readonly StringBuilder _buffer = new();
	private readonly Lock _lock = new();

	public event Action<string>? LineAppended;

	public string GetText()
	{
		lock (_lock)
		{
			return _buffer.ToString();
		}
	}

	public void WriteLine(string line)
	{
		Action<string>? lineAppended;

		lock (_lock)
		{
			if (_buffer.Length > 0)
			{
				_buffer.AppendLine();
			}

			_buffer.Append(line);
			lineAppended = LineAppended;
		}

		lineAppended?.Invoke(line);
	}
}
