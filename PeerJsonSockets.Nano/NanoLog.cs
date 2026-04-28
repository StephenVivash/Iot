using System;
using System.Diagnostics;

namespace PeerJsonSockets.Nano
{
	public sealed class NanoLog
	{
		private readonly string _name;

		public NanoLog(string name)
		{
			_name = name;
		}

		public void Debug(string message)
		{
			Write("debug", message);
		}

		public void Info(string message)
		{
			Write("info", message);
		}

		public void Warn(string message)
		{
			Write("warn", message);
		}

		public void Error(string message)
		{
			Write("error", message);
		}

		public void Error(string message, Exception exception)
		{
			Write("error", message + " " + exception.Message);
		}

		private void Write(string level, string message)
		{
			System.Diagnostics.Debug.WriteLine(DateTime.UtcNow.ToString("s") + " [" + level + "] " + _name + " " + message);
		}
	}
}
