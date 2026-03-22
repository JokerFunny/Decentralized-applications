using DecentralizedChat.PeerNode;
using DecentralizedChat.PeerNode.Security;
using DecentralizedChat.PeerNode.Services;
using DecentralizedChat.PeerNode.Storage;
using Shared.Protos;

var builder = WebApplication.CreateBuilder(args);

// 1. Add REST API Controllers.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// 2. Add Customized Swagger Configuration.
builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new()
	{
		Title = "Decentralized P2P Node API",
		Version = "v1",
		Description = "REST Gateway to securely communicate over the P2P gRPC network."
	});

	c.DocumentFilter<NodeInfoDocumentFilter>();
});

// 3. Add gRPC support.
builder.Services.AddGrpc();

// 4. Register Custom Services:
// Register the RSA Key Manager as a Singleton so the keys persist.
builder.Services.AddSingleton<NodeIdentityManager>();
// Register the message storage.
builder.Services.AddSingleton<MessageStore>();
// Register the reusable P2P sending logic.
builder.Services.AddTransient<P2PClientService>();

// 5. Register the gRPC Client to talk to the Registry.
string registryAddress = builder.Configuration["NodeConfig:RegistryAddress"] ?? "https://localhost:7000";
builder.Services.AddGrpcClient<RegistryService.RegistryServiceClient>(o =>
{
	o.Address = new Uri(registryAddress);
});

// 6. Register the background startup service.
builder.Services.AddHostedService<NodeInitializationService>();

// Register the interactive chat UI loop.
//builder.Services.AddHostedService<InteractiveChatService>();

var app = builder.Build();

// Add Swagger UI.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
	// By setting RoutePrefix to empty, Swagger UI will load at the root (http://localhost:8081/) instead of http://localhost:8081/swagger.
	c.RoutePrefix = string.Empty;
	c.SwaggerEndpoint("/swagger/v1/swagger.json", "P2P Node API v1");
});

// 8. Map Endpoints:
// REST endpoints (port 8081).
app.MapControllers();
// Map the incoming P2P service.
app.MapGrpcService<PeerServiceImpl>();

app.Run();
