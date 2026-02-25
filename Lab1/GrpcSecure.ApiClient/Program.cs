using System.Text;
using Grpc.Core;
using GrpcSecure.ApiClient;
using GrpcSecure.Shared.Protos;
using GrpcSecureShared;

var builder = WebApplication.CreateBuilder(args);

// Get gRPC server URL from config/environment.
var grpcEndpoint = builder.Configuration["GrpcServer:Endpoint"] ?? "http://localhost:8080";

// Register gRPC Client.
builder.Services.AddGrpcClient<SecureCommunication.SecureCommunicationClient>(options =>
{
	options.Address = new Uri(grpcEndpoint);
});
builder.Services.AddSingleton<SecureChatSession>();

// Add Swagger/OpenAPI services.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new()
	{
		Title = "GrpcSecure API Client Gateway",
		Version = "v1",
		Description = "Gateway to securely communicate with the backend."
	});

	c.DocumentFilter<SessionInfoDocumentFilter>();
});

var app = builder.Build();

// 3. Enable Swagger Middleware.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
	// By setting RoutePrefix to empty, Swagger UI will load at the root (http://localhost:8081/) instead of http://localhost:8081/swagger.
	c.RoutePrefix = string.Empty;
	c.SwaggerEndpoint("/swagger/v1/swagger.json", "GrpcSecure API Client v1");
});

// Initialize Handshake on startup.
using (var scope = app.Services.CreateScope())
{
	var session = scope.ServiceProvider.GetRequiredService<SecureChatSession>();
	await session.InitializeAsync();
}

// Expose the endpoint to send messages.
// Usage: http://localhost:<port>/send/YourMessageHere
app
	.MapGet("/send/{message}", async (string message, SecureChatSession chatSession) =>
	{
		try
		{
			string serverReply = await chatSession.SendMessageAsync(message);
			return Results.Ok(new
			{
				Status = "Success",
				SentMessage = message,
				DecryptedServerReply = serverReply
			});
		}
		catch (Exception ex)
		{
			return Results.Problem($"Failed to send message: {ex.Message}");
		}
	})
	.WithName("SendMessageToGrpc")
	.WithSummary("Sends an encrypted message via gRPC")
	.WithDescription("Encrypts the provided route parameter using the active XOR session key and proxies it to the backend gRPC Server.");

app
	.MapGet("/status", (SecureChatSession chatSession) =>
	{
		return Results.Ok(new
		{
			ContainerName = Environment.MachineName,
			ActiveSessionId = chatSession.CurrentSessionId,
			IsConnected = !string.IsNullOrEmpty(chatSession.CurrentSessionId)
		});
	})
	.WithName("GetClientStatus")
	.WithSummary("Gets the current connection status")
	.WithDescription("Returns the active gRPC Session ID and internal container info.");

app.Run();


// --- Session Manager ---
public class SecureChatSession
{
	private string _sessionId = string.Empty;
	private byte[] _sessionKey = Array.Empty<byte>();

	private readonly SecureCommunication.SecureCommunicationClient _rClient;
	private readonly ILogger<SecureChatSession> _rLogger;

	// [DN]: used for a demonstration.
	public string CurrentSessionId => _sessionId;

	public SecureChatSession(SecureCommunication.SecureCommunicationClient client, ILogger<SecureChatSession> logger)
	{
		_rClient = client;
		_rLogger = logger;
	}

	public async Task InitializeAsync()
	{
		_rLogger.LogInformation("- Performing handshake with gRPC server...");
		var handshakeResponse = await _rClient.HandshakeAsync(new HandshakeRequest());

		_sessionId = handshakeResponse.SessionId;
		_sessionKey = handshakeResponse.XorKey.ToByteArray();

		_rLogger.LogInformation("- Handshake successful. Session ID: [{SessionId}].", _sessionId);
	}

	public async Task<string> SendMessageAsync(string text)
	{
		var headers = new Metadata { { "session-id", _sessionId } };

		byte[] payloadBytes = Encoding.UTF8.GetBytes(text);
		byte[] encryptedPayloadBytes = XorCipherHelper.Process(payloadBytes, _sessionKey);

		var request = new SecureMessage { Payload = Google.Protobuf.ByteString.CopyFrom(encryptedPayloadBytes) };

		string messageToSend = Convert.ToBase64String(encryptedPayloadBytes);
		_rLogger.LogInformation("- Sending message: [{message}].", messageToSend);

		var response = await _rClient.SendMessageAsync(request, headers);

		byte[] decryptedResponseBytes = XorCipherHelper.Process(response.Payload.ToByteArray(), _sessionKey);
		string serverReply = Encoding.UTF8.GetString(decryptedResponseBytes);

		_rLogger.LogInformation("- Server Reply: [{reply}].", serverReply);

		return serverReply;
	}
}