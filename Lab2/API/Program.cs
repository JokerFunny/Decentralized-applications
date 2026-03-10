using Api.Services;
using API.Services;
using Microsoft.OpenApi;
using Shared.Configuration;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Configure Logging ---
// WebApplication.CreateBuilder automatically adds Console and Debug loggers by default based on appsettings.json.
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// --- 2. Bind Configuration (IOptions) ---
// This takes the "RabbitMq" section from appsettings.json and binds it to our RabbitMqOptions class.
builder.Services.Configure<RabbitMqOptions>(
	builder.Configuration.GetSection(RabbitMqOptions.SectionName));

// --- 3. Register Services (DI) ---
builder.Services.AddSingleton<IMessageProducer, RabbitMqProducer>();

// --- 4. Add Framework Services ---
builder.Services.AddControllers();

// Configure Swagger/OpenAPI for easy testing
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
	c.SwaggerDoc("v1", new OpenApiInfo
	{
		Title = "Medicine Producer API",
		Version = "v1",
		Description = "API for validating and sending medicine data to RabbitMQ"
	});
});

var app = builder.Build();

// --- 5. Configure the HTTP Request Pipeline ---
app.UseSwagger();
app.UseSwaggerUI(c =>
{
	c.SwaggerEndpoint("/swagger/v1/swagger.json", "Medicine API v1");
	// Serves Swagger UI at the application's root (http://localhost:<port>/)
	c.RoutePrefix = string.Empty;
});

// Enable routing and authorization middlewares
app.UseAuthorization();
app.MapControllers();

// --- 6. Run the Application ---
app.Run();
