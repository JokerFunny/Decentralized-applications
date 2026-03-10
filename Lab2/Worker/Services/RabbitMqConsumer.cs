using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Configuration;

namespace Worker.Services
{
	public class RabbitMqConsumer : IMessageConsumer, IAsyncDisposable
	{
		private readonly RabbitMqOptions _rOptions;
		private readonly ILogger<RabbitMqConsumer> _rLogger;
		private IConnection? _connection;
		private IChannel? _channel;

		public RabbitMqConsumer(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConsumer> logger)
		{
			_rOptions = options.Value;
			_rLogger = logger;
		}

		public async Task StartConsumingAsync(Func<string, Task<bool>> onMessageReceived, CancellationToken cancellationToken)
		{
			_rLogger.LogInformation("Initializing RabbitMQ consumer connection...");

			var factory = new ConnectionFactory
			{
				HostName = _rOptions.HostName,
				Port = _rOptions.Port,
				UserName = _rOptions.UserName,
				Password = _rOptions.Password,
				VirtualHost = _rOptions.VirtualHost
			};

			_connection = await factory.CreateConnectionAsync(cancellationToken);
			_channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

			await _channel.QueueDeclareAsync(
				queue: _rOptions.QueueName,
				durable: true,
				exclusive: false,
				autoDelete: false,
				arguments: null,
				cancellationToken: cancellationToken);

			// QoS (Quality of Service): Fetch 1 message at a time for fair dispatching
			await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: cancellationToken);

			var consumer = new AsyncEventingBasicConsumer(_channel);

			consumer.ReceivedAsync += async (model, ea) =>
			{
				var body = ea.Body.ToArray();
				var message = Encoding.UTF8.GetString(body);

				_rLogger.LogDebug("Message received from queue. Delegating to processor...");

				try
				{
					// Call the business logic callback
					bool isSuccess = await onMessageReceived(message);

					if (isSuccess)
					{
						// Business logic succeeded -> Ack
						await _channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: cancellationToken);
						_rLogger.LogInformation("Message Acked successfully.");
					}
					else
					{
						// Business logic failed gracefully -> Nack
						await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: cancellationToken);
						_rLogger.LogWarning("Message Nacked due to processing failure.");
					}
				}
				catch (Exception ex)
				{
					// Unexpected exception in business logic -> Nack
					_rLogger.LogError(ex, "Unhandled exception during message processing. Nacking message.");
					await _channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true, cancellationToken: cancellationToken);
				}
			};

			await _channel.BasicConsumeAsync(
				queue: _rOptions.QueueName,
				autoAck: false, // Manual ack is strictly enforced
				consumer: consumer,
				cancellationToken: cancellationToken);
		}

		public async ValueTask DisposeAsync()
		{
			if (_channel is not null)
				await _channel.CloseAsync();
			if (_connection is not null)
				await _connection.CloseAsync();
		}
	}
}
