using System.Text;
using System.Text.Json;
using API.Services;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Shared.Configuration;

namespace Api.Services
{
	/// <summary>
	/// Singleton service responsible for maintaining the RabbitMQ connection
	/// and publishing messages asynchronously (compatible with RabbitMQ.Client v7+).
	/// </summary>
	public class RabbitMqProducer : IMessageProducer, IAsyncDisposable
	{
		private readonly RabbitMqOptions _rOptions;
		private readonly ILogger<RabbitMqProducer> _rLogger;
		private IConnection? _connection;
		private IChannel? _channel;
		private readonly SemaphoreSlim _rSemaphore = new(1, 1); // Prevents multiple simultaneous connections.

		public RabbitMqProducer(IOptions<RabbitMqOptions> options, ILogger<RabbitMqProducer> logger)
		{
			_rOptions = options.Value;
			_rLogger = logger;
		}

		// Initialize connection and channel asynchronously.
		private async Task InitAsync()
		{
			if (_channel is not null)
				return;

			// Lock to ensure only one thread creates the connection.
			await _rSemaphore.WaitAsync();
			try
			{
				if (_channel is not null)
					return;

				_rLogger.LogInformation("Attempting to connect to RabbitMQ at {HostName}:{Port}...", _rOptions.HostName, _rOptions.Port);
				var factory = new ConnectionFactory
				{
					HostName = _rOptions.HostName,
					Port = _rOptions.Port,
					UserName = _rOptions.UserName,
					Password = _rOptions.Password,
					VirtualHost = _rOptions.VirtualHost
				};

				// v7 Async methods.
				_connection = await factory.CreateConnectionAsync();
				_channel = await _connection.CreateChannelAsync();

				await _channel.QueueDeclareAsync(
					queue: _rOptions.QueueName,
					durable: true,
					exclusive: false,
					autoDelete: false,
					arguments: null);

				_rLogger.LogInformation("Successfully connected to RabbitMQ and declared queue: {QueueName}", _rOptions.QueueName);
			}
			catch (Exception ex)
			{
				_rLogger.LogCritical(ex, "Failed to connect to RabbitMQ or declare the queue.");
				throw;
			}
			finally
			{
				_rSemaphore.Release();
			}
		}

		public async Task PublishMessageAsync<T>(T message)
		{
			// Ensure connection is established before publishing.
			await InitAsync();

			try
			{
				var jsonString = JsonSerializer.Serialize(message);
				var body = Encoding.UTF8.GetBytes(jsonString);

				// In v7, we create BasicProperties like a normal class.
				var properties = new BasicProperties
				{
					Persistent = true
				};

				_rLogger.LogDebug("Publishing message to queue {QueueName}...", _rOptions.QueueName);

				// Async publish.
				await _channel!.BasicPublishAsync(
					exchange: string.Empty,
					routingKey: _rOptions.QueueName,
					mandatory: false,
					basicProperties: properties,
					body: body);

				_rLogger.LogInformation("Message successfully published to queue {QueueName}.", _rOptions.QueueName);
			}
			catch (Exception ex)
			{
				_rLogger.LogError(ex, "Error occurred while publishing message to RabbitMQ.");
				throw;
			}
		}

		public async ValueTask DisposeAsync()
		{
			_rLogger.LogInformation("Closing RabbitMQ connection and channel...");

			if (_channel is not null)
			{
				await _channel.CloseAsync();
				await _channel.DisposeAsync();
			}

			if (_connection is not null)
			{
				await _connection.CloseAsync();
				await _connection.DisposeAsync();
			}

			_rLogger.LogInformation("RabbitMQ resources disposed.");
		}
	}
}
