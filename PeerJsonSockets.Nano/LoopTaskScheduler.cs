using System;
using System.Collections;

namespace PeerJsonSockets.Nano
{
	public delegate void LoopTaskHandler(object context);

	public sealed class LoopTaskScheduler
	{
		private readonly ArrayList _tasks = new ArrayList();
		private readonly NanoLog _log;

		public LoopTaskScheduler(NanoLog log)
		{
			_log = log;
		}

		public void Register(string name, int intervalMilliseconds, LoopTaskHandler handler)
		{
			_tasks.Add(new ScheduledLoopTask(name, intervalMilliseconds, handler));
		}

		public void RunDueTasks(object context)
		{
			DateTime now = DateTime.UtcNow;
			for (int i = 0; i < _tasks.Count; i++)
			{
				ScheduledLoopTask task = (ScheduledLoopTask)_tasks[i];
				if (now < task.NextRunAtUtc)
				{
					continue;
				}

				try
				{
					task.Handler(context);
				}
				catch (Exception ex)
				{
					_log.Error("Scheduled task '" + task.Name + "' failed.", ex);
				}
				finally
				{
					task.NextRunAtUtc = DateTime.UtcNow.AddMilliseconds(task.IntervalMilliseconds);
				}
			}
		}

		private sealed class ScheduledLoopTask
		{
			public ScheduledLoopTask(string name, int intervalMilliseconds, LoopTaskHandler handler)
			{
				Name = name;
				IntervalMilliseconds = intervalMilliseconds;
				Handler = handler;
				NextRunAtUtc = DateTime.UtcNow.AddMilliseconds(intervalMilliseconds);
			}

			public string Name;
			public int IntervalMilliseconds;
			public DateTime NextRunAtUtc;
			public LoopTaskHandler Handler;
		}
	}
}
