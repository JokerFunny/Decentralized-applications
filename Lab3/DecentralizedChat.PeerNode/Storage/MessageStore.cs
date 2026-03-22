using System.Collections.Concurrent;

namespace DecentralizedChat.PeerNode.Storage
{
	/// <summary>
	/// Represents a single received chat message sent over the P2P network.
	/// </summary>
	public record ChatMessageDto(
		string SenderId,
		DateTimeOffset Timestamp,
		string MessageText
	);

	/// <summary>
	/// A thread-safe in-memory store for incoming chat messages.
	/// </summary>
	public class MessageStore
	{
		// Upgraded the queue to store strongly-typed DTOs instead of raw strings
		private readonly ConcurrentQueue<ChatMessageDto> _rInbox = new();

		public void AddMessage(string senderId, long timestampUnix, string messageText)
		{
			var timestamp = DateTimeOffset.FromUnixTimeSeconds(timestampUnix).ToLocalTime();

			// 1. Create and store the structured DTO
			var messageDto = new ChatMessageDto(senderId, timestamp, messageText);
			_rInbox.Enqueue(messageDto);

			// 2. Preserve your excellent formatted console output for Docker logs
			string formattedMessage = $"  - Time: [{timestamp:HH:mm:ss}];{Environment.NewLine}" +
				$"  - Sender: [{senderId}];{Environment.NewLine}" +
				$"  - Text: [{messageText}].";

			Console.WriteLine($"Received message: [{Environment.NewLine}{formattedMessage}{Environment.NewLine}]");
		}

		public IEnumerable<ChatMessageDto> GetAllMessages()
			=> _rInbox.ToArray();
	}
}
