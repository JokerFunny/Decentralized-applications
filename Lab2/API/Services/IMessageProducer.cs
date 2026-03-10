namespace API.Services
{
	/// <summary>
	/// Interface for publishing messages to a message broker.
	/// </summary>
	public interface IMessageProducer
	{
		/// <summary>
		/// Serializes and asynchronously publishes a message to the broker.
		/// </summary>
		/// <typeparam name="T">Type of the message.</typeparam>
		/// <param name="message">The message object to send.</param>
		Task PublishMessageAsync<T>(T message);
	}
}
