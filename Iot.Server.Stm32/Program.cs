using System;
using System.Diagnostics;
using System.Threading;

namespace Iot.Server.Stm32
{
	public class Program
	{
		public static void Main()
		{
			Console.WriteLine("Hello from nanoFramework!");

			for (int i = 0; i == 0;)
			{
				Console.WriteLine("Hello from nanoFramework!");
				Thread.Sleep(100);
			}

			// Browse our samples repository: https://github.com/nanoframework/samples
			// Check our documentation online: https://docs.nanoframework.net/
			// Join our lively Discord community: https://discord.gg/gCyBu8T
		}
	}
}
