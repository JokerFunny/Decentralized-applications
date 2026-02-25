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
			var sessionIdHeader = context.RequestHeaders.FirstOrDefault(h => h.Key == "session-id");
			if (sessionIdHeader == null)
				throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing session-id header."));

			// 2. Retrieve the specific key for this client.
			var sessionKey = _rSessionManager.GetKey(sessionIdHeader.Value);
			if (sessionKey == null)
				throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid or expired session."));

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
	}
}
