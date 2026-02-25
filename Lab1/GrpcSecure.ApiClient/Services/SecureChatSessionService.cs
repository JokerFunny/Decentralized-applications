using System.Collections.Concurrent;
using System.Text;
using Grpc.Core;
using GrpcSecure.Shared.Protos;
using GrpcSecureShared;

namespace GrpcSecure.ApiClient.Services
{
	// Entrypoint, manages the encryption keys AND the background gRPC stream.
	public class SecureChatSessionService : IDisposable
	{
		private string _sessionId = string.Empty;
		private byte[] _sessionKey = Array.Empty<byte>();

		private readonly SecureCommunication.SecureCommunicationClient _rClient;
		private readonly ILogger<SecureChatSessionService> _rLogger;

		// Stream and Inbox state
		private AsyncDuplexStreamingCall<ChatMessage, ChatMessage>? _chatStream;

		private readonly ConcurrentQueue<string> _rInbox = new();
		private readonly CancellationTokenSource _rCST = new();

		public string CurrentSessionId => _sessionId;

		public SecureChatSessionService(SecureCommunication.SecureCommunicationClient client, ILogger<SecureChatSessionService> logger)
		{
			_rClient = client;
			_rLogger = logger;
		}

		public async Task InitializeAsync()
		{
			_rLogger.LogInformation("- Performing handshake with gRPC server...");
			var handshakeResponse = await _rClient.HandshakeAsync(new HandshakeRequest());

			_sessionId = handshakeResponse.SessionId;
			_sessionKey = handshakeResponse.XorKey.ToByteArray();

			_rLogger.LogInformation("- Handshake successful. Session ID: [{SessionId}]", _sessionId);

			// Open the bidirectional stream.
			var headers = new Metadata
		{
			{ "session-id", _sessionId }
		};
			_chatStream = _rClient.ChatStream(headers);

			// Start listening in the background without blocking the API startup.
			_ = Task.Run(ReceiveMessagesLoop, _rCST.Token);
		}

		private async Task ReceiveMessagesLoop()
		{
			try
			{
				await foreach (var response in _chatStream!.ResponseStream.ReadAllAsync(_rCST.Token))
				{
					byte[] decryptedBytes = XorCipherHelper.Process(response.Payload.ToByteArray(), _sessionKey);
					string messageText = Encoding.UTF8.GetString(decryptedBytes);

					// Format and store in the inbox for Swagger to read later.
					string formattedMessage = $"[{DateTime.Now:HH:mm:ss}] From [{response.SenderSessionId}]: {messageText}";
					_rInbox.Enqueue(formattedMessage);

					_rLogger.LogInformation("[System]: New [{message}] message from [{senderSessionId}] added to Inbox.", formattedMessage, response.SenderSessionId);
				}
			}
			catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled) { /* Stream closed */ }
			catch (OperationCanceledException) { /* App shutting down */ }
			catch (Exception ex)
			{
				_rLogger.LogError("[Error]: Receiving stream message: {Message}", ex.Message);
			}
		}

		public async Task<string> SendUnaryMessageAsync(string text)
		{
			var headers = new Metadata
		{
			{ "session-id", _sessionId }
		};
			byte[] payloadBytes = Encoding.UTF8.GetBytes(text);
			byte[] encryptedPayloadBytes = XorCipherHelper.Process(payloadBytes, _sessionKey);

			var request = new SecureMessage { Payload = Google.Protobuf.ByteString.CopyFrom(encryptedPayloadBytes) };

			string messageToSend = Convert.ToBase64String(encryptedPayloadBytes);
			_rLogger.LogInformation("- Sending message to server: [{message}].", messageToSend);

			var response = await _rClient.SendMessageAsync(request, headers);

			byte[] decryptedResponseBytes = XorCipherHelper.Process(response.Payload.ToByteArray(), _sessionKey);
			string serverReply = Encoding.UTF8.GetString(decryptedResponseBytes);

			_rLogger.LogInformation("- Server Reply: [{reply}].", serverReply);

			return serverReply;
		}

		public async Task SendChatMessageAsync(string targetSessionId, string text)
		{
			if (_chatStream == null)
				throw new InvalidOperationException("Chat stream is not open.");

			byte[] payloadBytes = Encoding.UTF8.GetBytes(text);
			byte[] encryptedPayload = XorCipherHelper.Process(payloadBytes, _sessionKey);

			var chatMessage = new ChatMessage
			{
				TargetSessionId = targetSessionId,
				Payload = Google.Protobuf.ByteString.CopyFrom(encryptedPayload)
			};

			await _chatStream.RequestStream.WriteAsync(chatMessage);
		}

		public IEnumerable<string> GetInboxMessages() => _rInbox.ToArray();

		public void ClearInbox() => _rInbox.Clear();

		public void Dispose()
		{
			_rCST.Cancel();
			_chatStream?.RequestStream.CompleteAsync().Wait();
			_chatStream?.Dispose();
		}
	}
}
