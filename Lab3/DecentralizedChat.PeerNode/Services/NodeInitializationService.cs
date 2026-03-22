using DecentralizedChat.PeerNode.Security;
using Grpc.Net.Client;
using Shared.Protos;

namespace DecentralizedChat.PeerNode.Services
{
	// IHostedService allows this code to run automatically when the app starts
	public class NodeInitializationService : IHostedService
	{
		private readonly NodeIdentityManager _rIdentityManager;
		private readonly IConfiguration _rConfig;
		private readonly ILogger<NodeInitializationService> _rLogger;

		public NodeInitializationService(NodeIdentityManager identityManager, IConfiguration config, ILogger<NodeInitializationService> logger)
		{
			_rIdentityManager = identityManager;
			_rConfig = config;
			_rLogger = logger;
		}

		public async Task StartAsync(CancellationToken cancellationToken)
		{
			string listenAddress = _rConfig["NodeConfig:ListenAddress"] ?? "https://localhost:5001";
			string registryAddress = _rConfig["NodeConfig:RegistryAddress"] ?? "https://localhost:7000";

			_rLogger.LogInformation("Starting Peer Node [{NodeId}] at {Address}", _rIdentityManager.NodeId, listenAddress);
			_rLogger.LogInformation("Generated RSA Public Key: {Key}...", _rIdentityManager.PublicKeyBase64[..30]);

			// Connect to the Central Registry Server
			var channel = GrpcChannel.ForAddress(registryAddress);
			var client = new RegistryService.RegistryServiceClient(channel);

			try
			{
				_rLogger.LogInformation("Registering with tracker at {Registry}...", registryAddress);

				var response = await client.RegisterNodeAsync(new RegisterRequest
				{
					NodeId = _rIdentityManager.NodeId,
					Address = listenAddress,
					PublicKey = _rIdentityManager.PublicKeyBase64
				}, cancellationToken: cancellationToken);

				if (response.Success)
				{
					_rLogger.LogInformation("Successfully registered with the Tracker!");
				}
			}
			catch (Exception ex)
			{
				_rLogger.LogError("Failed to register with the tracker. Is it running? Error: {Message}", ex.Message);
			}
		}

		public async Task StopAsync(CancellationToken cancellationToken)
		{
			// Gracefully unregister from the tracker when the application shuts down
			_rLogger.LogInformation("Shutting down... Unregistering from tracker.");

			string registryAddress = _rConfig["NodeConfig:RegistryAddress"] ?? "https://localhost:7000";
			var channel = GrpcChannel.ForAddress(registryAddress);
			var client = new RegistryService.RegistryServiceClient(channel);

			try
			{
				await client.UnregisterNodeAsync(new UnregisterRequest
				{
					NodeId = _rIdentityManager.NodeId
				}, cancellationToken: cancellationToken);

				_rLogger.LogInformation("Successfully unregistered. Goodbye!");
			}
			catch
			{
				// Ignore errors during shutdown to prevent the app from hanging
				_rLogger.LogWarning("Could not reach tracker during shutdown.");
			}
		}
	}
}
