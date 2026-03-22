using System.Security.Cryptography;
using System.Text;
using DecentralizedChat.PeerNode.Security;
using Grpc.Net.Client;
using Shared.Protos;

namespace DecentralizedChat.PeerNode.Services
{
	/// <summary>
	/// A reusable service responsible for the "Client Half" of the P2P node.
	/// It handles peer discovery, hybrid cryptography (XOR+RSA), and gRPC transmission.
	/// </summary>
	public class P2PClientService
	{
		private readonly NodeIdentityManager _rIdentityManager;
		private readonly ILogger<P2PClientService> _rLogger;

		public P2PClientService(NodeIdentityManager identityManager, ILogger<P2PClientService> logger)
		{
			_rIdentityManager = identityManager;
			_rLogger = logger;
		}

		/// <summary>
		/// Encrypts and sends a message directly to a target peer.
		/// Returns a tuple indicating success and a descriptive message.
		/// </summary>
		public async Task<(bool Success, string Message)> SendSecureMessageAsync(PeerInfo targetPeer, string messageText)
		{
			if (string.IsNullOrEmpty(messageText))
				return (false, $"Message can't be empty!");

			try
			{
				byte[] plaintextBytes = Encoding.UTF8.GetBytes(messageText);

				// ========================================================
				// CRYPTOGRAPHY PHASE
				// ========================================================

				// A. Generate a random 32-byte XOR key for this specific message.
				byte[] xorKey = XorCryptographyHelper.GenerateRandomKey(32);

				// B. Encrypt the message text using the XOR cipher.
				byte[] encryptedContent = XorCryptographyHelper.Process(plaintextBytes, xorKey);

				// C. Sign the ORIGINAL plaintext with OUR Private RSA Key.
				byte[] digitalSignature = _rIdentityManager.GetRsaInstance().SignData(
					plaintextBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

				// D. Encrypt the XOR key with THEIR Public RSA Key.
				using var targetRsa = RSA.Create();
				targetRsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(targetPeer.PublicKey), out _);
				byte[] encryptedXorKey = targetRsa.Encrypt(xorKey, RSAEncryptionPadding.OaepSHA256);

				// ========================================================
				// NETWORK PHASE (Direct P2P)
				// ========================================================
				using var channel = GrpcChannel.ForAddress(targetPeer.Address);
				var peerClient = new PeerService.PeerServiceClient(channel);

				var directMessage = new DirectMessage
				{
					SenderId = _rIdentityManager.NodeId,
					EncryptedXorKey = Google.Protobuf.ByteString.CopyFrom(encryptedXorKey),
					EncryptedContent = Google.Protobuf.ByteString.CopyFrom(encryptedContent),
					DigitalSignature = Google.Protobuf.ByteString.CopyFrom(digitalSignature),
					Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
				};

				if (_rLogger.IsEnabled(LogLevel.Debug))
				{
					string readableTimestamp = DateTimeOffset.FromUnixTimeSeconds(directMessage.Timestamp)
						.ToLocalTime()
						.ToString("yyyy-MM-dd HH:mm:ss");

					_rLogger.LogDebug(
						"Sending secure message to [{TargetNodeId}] at [{TargetAddress}].\n" +
						"--- CRYPTO DEBUG INFO ---\n" +
						"Original Text        : {OriginalText}\n" +
						"Generated XOR Key    : {XorKey}\n" +
						"Encrypted Content    : {EncryptedContent}\n" +
						"Digital Signature    : {DigitalSignature}\n" +
						"Encrypted XOR Key    : {EncryptedXorKey}\n" +
						"Timestamp            : {Timestamp}\n" +
						"-------------------------",
						targetPeer.NodeId,
						targetPeer.Address,
						messageText,
						Convert.ToBase64String(xorKey),
						Convert.ToBase64String(encryptedContent),
						Convert.ToBase64String(digitalSignature),
						Convert.ToBase64String(encryptedXorKey),
						readableTimestamp
					);
				}
				else
					_rLogger.LogInformation("Transmitting secure P2P message to [{TargetNodeId}]...", targetPeer.NodeId);

				var ack = await peerClient.SendDirectMessageAsync(directMessage);

				if (ack.Delivered)
					return (true, "Message securely delivered and verified!");

				return (false, $"Target rejected message. Reason: {ack.ErrorMessage}");
			}
			catch (Exception ex)
			{
				_rLogger.LogError("Failed to send message: {Error}", ex.Message);
				return (false, $"Internal error during transmission: {ex.Message}");
			}
		}
	}
}
