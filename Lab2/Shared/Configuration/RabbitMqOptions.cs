namespace Shared.Configuration
{
	/// <summary>
	/// Comprehensive configuration options for RabbitMQ connection.
	/// </summary>
	public class RabbitMqOptions
	{
		public const string SectionName = "RabbitMq";

		// Broker host (e.g., "localhost", "rabbitmq-server", or IP address).
		public string HostName { get; set; } = "localhost";

		// AMQP port (default is 5672).
		public int Port { get; set; } = 5672;

		// Authentication credentials.
		public string UserName { get; set; } = "guest";
		public string Password { get; set; } = "guest";

		// Virtual host for logical isolation (default is "/").
		public string VirtualHost { get; set; } = "/";

		// Target queue name.
		public string QueueName { get; set; } = string.Empty;
	}
}
