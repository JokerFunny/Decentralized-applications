using GrpcSecure.ApiClient;
using GrpcSecure.ApiClient.DTO;
using GrpcSecure.ApiClient.Services;
using GrpcSecure.Shared.Protos;

var builder = WebApplication.CreateBuilder(args);

// Get gRPC server URL from config/environment.
var grpcEndpoint = builder.Configuration["GrpcServer:Endpoint"] ?? "http://localhost:8080";

// Register gRPC Client.
builder.Services.AddGrpcClient<SecureCommunication.SecureCommunicationClient>(options =>
{
	options.Address = new Uri(grpcEndpoint);
});
builder.Services.AddSingleton<SecureChatSessionService>();

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

// Initialize Handshake and Background Stream on startup.
using (var scope = app.Services.CreateScope())
{
	var session = scope.ServiceProvider.GetRequiredService<SecureChatSessionService>();
	await session.InitializeAsync();
}

// ====================================================================
// ENDPOINTS REGISTRATION
// ====================================================================
app
	.MapGet("/status", (SecureChatSessionService chatSession) =>
	{
		return Results.Ok(new { ActiveSessionId = chatSession.CurrentSessionId });
	})
	.WithName("GetStatus")
	.WithTags("1. System")
	.WithSummary("Gets the current connection status")
	.WithDescription("Returns the active gRPC Session ID and internal container info.");

app
	.MapGet("/unary/ping/{message}", async (string message, SecureChatSessionService chatSession) =>
	{
		try
		{
			string serverReply = await chatSession.SendUnaryMessageAsync(message);
			return Results.Ok(new
			{
				Status = "Success",
				Sent = message,
				ServerReply = serverReply
			});
		}
		catch (Exception ex)
		{
			return Results.Problem(ex.Message);
		}
	})
	.WithName("PingServer")
	.WithTags("2. Direct Server Call")
	.WithSummary("Sends an encrypted message via gRPC (like '/server <msg>' in console)")
	.WithDescription("Encrypts the provided route parameter using the active XOR session key and proxies it to the backend gRPC Server via a Unary call.");

app
	.MapPost("/chat/send", async (ChatRequest request, SecureChatSessionService chatSession) =>
	{
		try
		{
			await chatSession.SendChatMessageAsync(request.TargetSessionId, request.Message);
			return Results.Ok(new { Status = "Message Sent to Stream" });
		}
		catch (Exception ex) { return Results.Problem(ex.Message); }
	})
	.WithName("SendChatMessage")
	.WithTags("3. Client-to-Client Chat")
	.WithSummary("Sends an encrypted message from this client to a target client (like '/to <id> /m <msg>' in console)")
	.WithDescription("Encrypts the payload using this client's XOR key and pushes it into the active gRPC Bidirectional Stream. The server will decrypt it, re-encrypt it with the target's key, and route it to the destination.");

app
	.MapGet("/chat/inbox", (SecureChatSessionService chatSession) =>
	{
		return Results.Ok(chatSession.GetInboxMessages());
	})
	.WithName("CheckInbox")
	.WithTags("3. Client-to-Client Chat")
	.WithSummary("Retrieves all received messages from the background stream")
	.WithDescription("Reads the local in-memory queue (Inbox) of messages that have been asynchronously received via the gRPC Bidirectional Stream and decrypted using this client's XOR key.");

app
	.MapDelete("/chat/inbox", (SecureChatSessionService chatSession) =>
	{
		chatSession.ClearInbox();
		return Results.Ok(new { Status = "Inbox Cleared" });
	})
	.WithName("ClearInbox")
	.WithTags("3. Client-to-Client Chat")
	.WithSummary("Clears all messages from the local inbox")
	.WithDescription("Empties the in-memory queue of received messages. This action only affects this specific API Gateway instance and does not impact the server or other clients.");

app.Run();
