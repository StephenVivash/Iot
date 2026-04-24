using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PeerJsonSockets;
using Iot.Server.Net;

internal static class Program
{
	private const int DefaultPort = 5050;

	private static async Task Main(string[] args)
	{
		using ILoggerFactory loggerFactory = CreateLoggerFactory();

		ILogger logger = loggerFactory.CreateLogger("Iot.Server.Net");
		StartupMode startupMode;
		try
		{
			startupMode = ParseStartupMode(args, logger);
		}
		catch (ArgumentException ex)
		{
			logger.LogError("{Message}", ex.Message);
			logger.LogInformation("Usage: ConsoleApp1 [-server <port>] [-client <host:port>]");
			return;
		}

		using CancellationTokenSource shutdown = new();
		Console.CancelKeyPress += (_, eventArgs) =>
		{
			eventArgs.Cancel = true;
			shutdown.Cancel();
		};

		PeerRuntimeOptions options = new(Environment.MachineName);
		PeerConnectionRegistry connectionRegistry = new();
		PeerConnectionService connectionService = new(logger);
		logger.LogWarning("Host name: {PeerName}", options.PeerName);

		List<Task> tasks = [];
		if (startupMode.RunServer)
		{
			List<IPeerServerLoopTask> serverLoopTasks =
			[
				new ConsoleServerHeartbeatTask(logger)
			];

			PeerServerService serverService = new(IPAddress.Any, startupMode.ServerPort,
				options, connectionRegistry, connectionService,
				logger,
				serverLoopTasks);

			logger.LogWarning("Server listening on port {ListenPort}. Press Ctrl+C to stop.", startupMode.ServerPort);
			tasks.Add(serverService.RunAsync(shutdown.Token));
		}

		if (startupMode.ClientPeer is not null)
		{
			PeerClientService clientService = new(options, connectionRegistry,
				connectionService, logger);
			tasks.Add(clientService.RunAsync(startupMode.ClientPeer, shutdown.Token));
		}

		await Task.WhenAll(tasks);
	}

	private static StartupMode ParseStartupMode(string[] args, ILogger logger)
	{
		bool serverSpecified = false;
		int serverPort = DefaultPort;
		PeerAddress? clientPeer = null;

		for (int i = 0; i < args.Length; i++)
		{
			switch (args[i].ToLowerInvariant())
			{
				case "-server":
					if (++i >= args.Length || !int.TryParse(args[i], out serverPort))
					{
						throw new ArgumentException("Expected: -server <port>");
					}

					serverSpecified = true;
					break;

				case "-client":
					if (++i >= args.Length)
					{
						throw new ArgumentException("Expected: -client <host:port>");
					}

					clientPeer = PeerAddressParser.Parse(args[i], logger)
						?? throw new ArgumentException("Expected: -client <host:port>");
					break;

				default:
					throw new ArgumentException(
						$"Unknown argument '{args[i]}'. Expected -server <port> and/or -client <host:port>.");
			}
		}

		bool runServer = serverSpecified || clientPeer is null;
		return new StartupMode(runServer, serverPort, clientPeer);
	}

	private sealed record StartupMode(bool RunServer, int ServerPort, PeerAddress? ClientPeer);

	private static ILoggerFactory CreateLoggerFactory()
	{
		IConfigurationRoot configuration = new ConfigurationBuilder()
			.SetBasePath(AppContext.BaseDirectory)
			.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
			.Build();

		string logDirectory = configuration["FileLogging:Directory"] ?? "logs";
		string logFileName = $"{DateTime.Now:yyyy-MM-dd HH-mm-ss}.log";
		string logFilePath = Path.Combine(AppContext.BaseDirectory, logDirectory, logFileName);

		return LoggerFactory.Create(builder =>
		{
			builder.AddConfiguration(configuration.GetSection("Logging"));
			builder.AddSimpleConsole(options =>
			{
				options.SingleLine = true;
				options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
			});
			builder.AddProvider(new TimestampedFileLoggerProvider(logFilePath));
		});
	}
}
