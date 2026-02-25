using System.Text;
using Grpc.Core;
using GrpcSecure.Shared.Protos;
using GrpcSecureShared;

namespace GrpcSecure.Server.Services
{
	public class SecureCommunicationService : SecureCommunication.SecureCommunicationBase
	{
		private readonly ISessionManager _rSessionManager;
		private readonly ILogger<SecureCommunicationService> _rLogger;

		public SecureCommunicationService(ISessionManager sessionManager, ILogger<SecureCommunicationService> logger)
		{
			_rSessionManager = sessionManager;
			_rLogger = logger;
		}

		public override Task<HandshakeResponse> Handshake(HandshakeRequest request, ServerCallContext context)
		{
			var (sessionId, key) = _rSessionManager.CreateSession();
			_rLogger.LogInformation("- [{SessionId}]: New session created!", sessionId);

			return Task.FromResult(new HandshakeResponse
			{
				SessionId = sessionId,
				XorKey = Google.Protobuf.ByteString.CopyFrom(key)
			});
		}

		public override Task<SecureMessage> SendMessage(SecureMessage request, ServerCallContext context)
		{
			// 1. Extract session ID from headers (Metadata).
			var sessionIdHeader = context.RequestHeaders.FirstOrDefault(h => h.Key == "session-id")
				?? throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing session-id header."));

			// 2. Retrieve the specific key for this client.
			var sessionKey = _rSessionManager.GetKey(sessionIdHeader.Value)
				?? throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or expired session."));

			// 3. Decrypt the incoming payload.
			byte[] encryptedClientMessage = request.Payload.ToByteArray();
			_rLogger.LogInformation("[{SessionId}]: Received encrypted [{Message}] message.", sessionIdHeader.Value, Convert.ToBase64String(encryptedClientMessage));

			byte[] decryptedBytes = XorCipherHelper.Process(encryptedClientMessage, sessionKey);
			string clientMessage = Encoding.UTF8.GetString(decryptedBytes);
			_rLogger.LogInformation("[{SessionId}]: Received decrypted [{Message}] message.", sessionIdHeader.Value, clientMessage);

			// 4. Prepare and encrypt the response.
			string replyMessage = $"Server processed your message: [{clientMessage}]";
			byte[] replyBytes = Encoding.UTF8.GetBytes(replyMessage);
			byte[] encryptedReply = XorCipherHelper.Process(replyBytes, sessionKey);

			return Task.FromResult(new SecureMessage
			{
				Payload = Google.Protobuf.ByteString.CopyFrom(encryptedReply)
			});
		}

		public override async Task ChatStream(
			IAsyncStreamReader<ChatMessage> requestStream,
			IServerStreamWriter<ChatMessage> responseStream,
			ServerCallContext context)
		{
			// 1. Authenticate the sender.
			var sessionIdHeader = context.RequestHeaders.FirstOrDefault(h => h.Key == "session-id")
				?? throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing session-id header."));
			string senderSessionId = sessionIdHeader.Value;
			var senderKey = _rSessionManager.GetKey(senderSessionId)
				?? throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or expired session."));

			// 2. Register the sender's stream so others can send messages to them.
			_rSessionManager.RegisterStream(senderSessionId, responseStream);
			_rLogger.LogInformation("[{SessionId}]: Client connected to ChatStream.", senderSessionId);

			try
			{
				// 3. Keep the connection open and listen for incoming messages in a loop.
				while (await requestStream.MoveNext(context.CancellationToken))
				{
					var incomingMessage = requestStream.Current;
					string targetSessionId = incomingMessage.TargetSessionId;

					if (string.IsNullOrEmpty(targetSessionId))
					{
						_rLogger.LogWarning("Sender [{Sender}] did not specify a target session.", senderSessionId);
						continue;
					}

					// 4. Find the target client and their encryption key.
					var targetKey = _rSessionManager.GetKey(targetSessionId);
					var targetStream = _rSessionManager.GetStream(targetSessionId);

					if (targetKey == null || targetStream == null)
					{
						_rLogger.LogWarning("Target clinet [{Target}] not found or offline.", targetSessionId);

						string errorMessage = $"[System]: User '{targetSessionId}' is not found or offline.";
						byte[] errorBytes = Encoding.UTF8.GetBytes(errorMessage);

						// Encrypt the error using the SENDER'S key.
						byte[] encryptedError = XorCipherHelper.Process(errorBytes, senderKey);

						var systemReply = new ChatMessage
						{
							TargetSessionId = senderSessionId,
							SenderSessionId = "SERVER", // Special ID to indicate a system message
							Payload = Google.Protobuf.ByteString.CopyFrom(encryptedError)
						};

						// Write back to the sender's stream.
						await responseStream.WriteAsync(systemReply);

						continue;
					}

					// 5. DECRYPT the message using the SENDER'S key.
					byte[] encryptedClientMessage = incomingMessage.Payload.ToByteArray();
					_rLogger.LogInformation("[{SessionId}]: Received encrypted [{Message}] message.", sessionIdHeader.Value, Convert.ToBase64String(encryptedClientMessage));

					byte[] decryptedBytes = XorCipherHelper.Process(encryptedClientMessage, senderKey);
					string clientMessage = Encoding.UTF8.GetString(decryptedBytes);
					_rLogger.LogInformation("[{SessionId}]: Received decrypted [{Message}] message.", sessionIdHeader.Value, clientMessage);

					// 6. ENCRYPT the message using the TARGET'S key.
					byte[] encryptedForTarget = XorCipherHelper.Process(decryptedBytes, targetKey);
					string encryptedMessage = Convert.ToBase64String(encryptedForTarget);
					_rLogger.LogInformation("[{TargetSessionId}]: Formated encrypted [{Message}] message for a reciever.", targetSessionId, encryptedMessage);

					// 7. Forward the message to the target client.
					var messageToForward = new ChatMessage
					{
						TargetSessionId = targetSessionId,
						SenderSessionId = senderSessionId,
						Payload = Google.Protobuf.ByteString.CopyFrom(encryptedForTarget)
					};

					await targetStream.WriteAsync(messageToForward);

					_rLogger.LogInformation("Routed [{Message}] message from [{Sender}] to [{Target}].", encryptedMessage, senderSessionId, targetSessionId);
				}
			}
			catch (IOException)
			{
				_rLogger.LogInformation("[{SessionId}]: Connection to client was lost.", senderSessionId);
			}
			catch (Exception ex)
			{
				_rLogger.LogError(ex, "[{SessionId}]: Error in ChatStream", senderSessionId);
			}
			finally
			{
				// 8. Cleanup: Remove the stream when the client disconnects or an error occurs.
				_rSessionManager.RemoveStream(senderSessionId);
				_rLogger.LogInformation("[{SessionId}]: Client disconnected from ChatStream.", senderSessionId);
			}
		}
	}
}
