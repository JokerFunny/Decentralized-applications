using GrpcSecure.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddConsole();

// Add services to the container.
builder.Services.AddGrpc();
builder.Services.AddSingleton<ISessionManager, SessionManager>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.MapGrpcService<SecureCommunicationService>();
app.MapGet("/", () => "Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");

app.Run();
