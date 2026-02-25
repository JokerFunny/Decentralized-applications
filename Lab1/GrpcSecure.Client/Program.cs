using System.Text;
using Grpc.Core;
using GrpcSecure.Shared.Protos;
using GrpcSecureShared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Console.WriteLine("=== Secure gRPC Client ===");

// Setup Generic Host.
var host = Host.CreateDefaultBuilder(args)
	.ConfigureAppConfiguration((context, config) =>
	{
		config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
		config.AddEnvironmentVariables();
	})
	.ConfigureServices((context, services) =>
	{
		// Read the dynamic endpoint from configuration.
		var grpcEndpoint = context.Configuration["GrpcServer:Endpoint"];
		if (string.IsNullOrEmpty(grpcEndpoint))
			throw new InvalidOperationException("gRPC Server Endpoint is not configured.");

		// Register the gRPC Client using the Factory pattern.
		services.AddGrpcClient<SecureCommunication.SecureCommunicationClient>(options =>
		{
			options.Address = new Uri(grpcEndpoint);
		});

		// Register main application logic.
		services.AddTransient<SecureChatApp>();
	})
	.Build();

// Resolve and run the application.
var app = host.Services.GetRequiredService<SecureChatApp>();
await app.RunAsync();

public class SecureChatApp
{
	private readonly SecureCommunication.SecureCommunicationClient _rClient;
	private readonly ILogger<SecureChatApp> _rLogger;

	public SecureChatApp(SecureCommunication.SecureCommunicationClient client, ILogger<SecureChatApp> logger)
	{
		_rClient = client;
		_rLogger = logger;
	}

	public async Task RunAsync()
	{
		_rLogger.LogInformation("- Initiating handshake...");

		try
		{
			// Perform Handshake to get session credentials.
			var handshakeResponse = await _rClient.HandshakeAsync(new HandshakeRequest());
			string sessionId = handshakeResponse.SessionId;
			byte[] sessionKey = handshakeResponse.XorKey.ToByteArray();

			_rLogger.LogInformation("- Handshake successful. Session ID: [{SessionId}].", sessionId);

			// Prepare headers (Metadata) for subsequent calls.
			var headers = new Metadata
			{
				{ "session-id", sessionId }
			};

			// Communication Loop.
			while (true)
			{
				Console.Write("\nEnter message (or type 'exit' to quit): ");
				var input = Console.ReadLine();

				if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
					break;

				// Encrypt.
				byte[] payloadBytes = Encoding.UTF8.GetBytes(input ?? "");
				byte[] encryptedPayloadBytes = XorCipherHelper.Process(payloadBytes, sessionKey);

				var request = new SecureMessage { Payload = Google.Protobuf.ByteString.CopyFrom(encryptedPayloadBytes) };

				string messageToSend = Convert.ToBase64String(encryptedPayloadBytes);
				Console.WriteLine($"- Sending message: [{messageToSend}].");

				// Send request, passing the headers with the session ID.
				var response = await _rClient.SendMessageAsync(request, headers);

				// Decrypt response.
				byte[] decryptedResponseBytes = XorCipherHelper.Process(response.Payload.ToByteArray(), sessionKey);
				string serverReply = Encoding.UTF8.GetString(decryptedResponseBytes);

				Console.WriteLine($"- Server Reply: [{serverReply}].");
			}
		}
		catch (RpcException ex)
		{
			_rLogger.LogError("[Error]: gRPC failed. {Detail}.", ex.Status.Detail);
		}
	}
}



//// Initialize gRPC channel.
//using var channel = GrpcChannel.ForAddress("https://localhost:7162");
//var client = new SecureCommunication.SecureCommunicationClient(channel);

//// 1. Perform Handshake to get session credentials.
//Console.WriteLine("Initiating handshake...");
//var handshakeResponse = await client.HandshakeAsync(new HandshakeRequest());

//string sessionId = handshakeResponse.SessionId;
//byte[] sessionKey = handshakeResponse.XorKey.ToByteArray();

//Console.WriteLine($"Handshake successful. Session ID: [{sessionId}].");

//// 2. Prepare headers (Metadata) for subsequent calls.
//var headers = new Metadata
//{
//	{ "session-id", sessionId }
//};

//// 3. Communication Loop.
//while (true)
//{
//	Console.Write("\nEnter message (or type 'exit' to quit): ");
//	var input = Console.ReadLine();

//	if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
//		break;

//	// Encrypt.
//	byte[] payloadBytes = Encoding.UTF8.GetBytes(input ?? "");
//	byte[] encryptedPayloadBytes = XorCipherHelper.Process(payloadBytes, sessionKey);

//	var request = new SecureMessage { Payload = Google.Protobuf.ByteString.CopyFrom(encryptedPayloadBytes) };

//	try
//	{
//		string messageToSend = Encoding.UTF8.GetString(encryptedPayloadBytes);
//		Console.WriteLine($"- Sending message: [{messageToSend}].");

//		// Send request, passing the headers with the session ID.
//		var response = await client.SendMessageAsync(request, headers);

//		// Decrypt response.
//		byte[] decryptedResponseBytes = XorCipherHelper.Process(response.Payload.ToByteArray(), sessionKey);
//		string serverReply = Encoding.UTF8.GetString(decryptedResponseBytes);

//		Console.WriteLine($"- Server Reply: [{serverReply}].");
//	}
//	catch (RpcException ex)
//	{
//		Console.WriteLine($"[Error]: gRPC failed. {ex.Status.Detail}.");
//	}
//}
