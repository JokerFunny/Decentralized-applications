namespace Worker.Services
{
	public interface IMessageConsumer
	{
		/// <summary>
		/// Starts consuming messages from the broker.
		/// </summary>
		/// <param name="onMessageReceived">Callback function. Must return true to Ack, false to Nack.</param>
		/// <param name="cancellationToken">Cancellation token to stop consumption.</param>
		Task StartConsumingAsync(Func<string, Task<bool>> onMessageReceived, CancellationToken cancellationToken);
	}
}
