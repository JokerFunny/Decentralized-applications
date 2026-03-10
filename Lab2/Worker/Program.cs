using Microsoft.EntityFrameworkCore;
using Shared.Configuration;
using Worker;
using Worker.Data;
using Worker.Services;

var builder = Host.CreateApplicationBuilder(args);

// --- 1. Configure Logging ---
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// --- 2. Bind RabbitMQ Configuration ---
builder.Services.Configure<RabbitMqOptions>(
	builder.Configuration.GetSection(RabbitMqOptions.SectionName));

// --- 3. Configure Database Context Factory (PostgreSQL) ---
builder.Services.AddDbContextFactory<AppDbContext>(options =>
{
	var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
	options.UseNpgsql(connectionString);
});

// --- 4. Register the Background Worker Service ---
builder.Services.AddSingleton<IMessageConsumer, RabbitMqConsumer>();
builder.Services.AddSingleton<IMedicineService, MedicineService>();
builder.Services.AddHostedService<MedicineConsumerWorker>();

var host = builder.Build();

// --- 5. Automatic Database Migration ---
using (var scope = host.Services.CreateScope())
{
	var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
	var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

	try
	{
		logger.LogInformation("Checking for pending database migrations...");

		using var context = await contextFactory.CreateDbContextAsync();

		// This will create the database if it doesn't exist 
		// and apply all migrations found in the /Migrations folder.
		await context.Database.MigrateAsync();

		logger.LogInformation("Database is up to date.");
	}
	catch (Exception ex)
	{
		logger.LogCritical(ex, "A fatal error occurred while migrating the database. The application will shut down.");
		throw;
	}
}

// --- 6. Start the Worker ---
host.Run();
