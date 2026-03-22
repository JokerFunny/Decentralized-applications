using System.Security.Cryptography;
using System.Text;
using DecentralizedChat.PeerNode.Security;
using DecentralizedChat.PeerNode.Storage;
using Grpc.Core;
using Shared.Protos;

namespace DecentralizedChat.PeerNode.Services
{
	/// <summary>
	/// This is the "Server Half" of the P2P node.
	/// It listens for incoming connections from other peers, decrypts the messages,
	/// and verifies the digital signatures to ensure authenticity.
	/// </summary>
	public class PeerServiceImpl : PeerService.PeerServiceBase
	{
		private readonly NodeIdentityManager _rIdentityManager;
		private readonly RegistryService.RegistryServiceClient _rRegistryClient;
		private readonly MessageStore _rMessageStore;
		private readonly ILogger<PeerServiceImpl> _rLogger;

		public PeerServiceImpl(NodeIdentityManager identityManager, RegistryService.RegistryServiceClient registryClient,
			MessageStore messageStore, ILogger<PeerServiceImpl> logger)
		{
			_rIdentityManager = identityManager;
			_rRegistryClient = registryClient;
			_rMessageStore = messageStore;
			_rLogger = logger;
		}

		public override async Task<MessageAck> SendDirectMessage(DirectMessage request, ServerCallContext context)
		{
			_rLogger.LogInformation("Incoming encrypted message received from [{SenderId}].", request.SenderId);

			try
			{
				// 1. Fetch the sender's Public Key from the Registry to verify their signature.
				// We pass OUR NodeId so the registry returns everyone EXCEPT us.
				var nodesResponse = await _rRegistryClient.GetActiveNodesAsync(
					new GetNodesRequest { RequesterNodeId = _rIdentityManager.NodeId });

				var senderInfo = nodesResponse.Peers.FirstOrDefault(p => p.NodeId == request.SenderId);

				if (senderInfo == null)
				{
					_rLogger.LogWarning("Sender [{SenderId}] is not registered on the tracker.", request.SenderId);
					return new MessageAck { Delivered = false, ErrorMessage = "Sender unknown or not active." };
				}

				// 2. Decrypt the XOR Key using OUR Private RSA Key.
				// We use OaepSHA256 padding to match the high-security standard.
				byte[] decryptedXorKey = _rIdentityManager.GetRsaInstance().Decrypt(request.EncryptedXorKey.ToByteArray(),
					RSAEncryptionPadding.OaepSHA256);

				// 3. Decrypt the Message Content using the XOR Cipher and the freshly decrypted key.
				byte[] decryptedContentBytes = XorCryptographyHelper.Process(
					request.EncryptedContent.ToByteArray(),
					decryptedXorKey);

				// 4. Verify the Digital Signature using the SENDER'S Public RSA Key.
				using var senderRsa = RSA.Create();
				senderRsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(senderInfo.PublicKey), out _);

				bool isSignatureValid = senderRsa.VerifyData(
					decryptedContentBytes,                  // The hashed plaintext.
					request.DigitalSignature.ToByteArray(), // The signature provided in the payload.
					HashAlgorithmName.SHA256,
					RSASignaturePadding.Pkcs1);

				if (!isSignatureValid)
				{
					_rLogger.LogError("SECURITY ALERT: Digital signature validation failed for message from [{SenderId}].", request.SenderId);
					return new MessageAck { Delivered = false, ErrorMessage = "Invalid digital signature. Message may be tampered." };
				}

				// 5. Success! Print the validated message to the console.
				string messageText = Encoding.UTF8.GetString(decryptedContentBytes);

				if (_rLogger.IsEnabled(LogLevel.Debug))
				{
					string readableTimestamp = DateTimeOffset.FromUnixTimeSeconds(request.Timestamp)
						.ToLocalTime()
						.ToString("yyyy-MM-dd HH:mm:ss");

					_rLogger.LogDebug(
						"Incoming encrypted message received from [{SenderId}]\n" +
						"--- CRYPTO DEBUG INFO ---\n" +
						"Decrypted Text       : {DecryptedText}\n" +
						"Encrypted Content    : {EncryptedContent}\n" +
						"Digital Signature    : {DigitalSignature}\n" +
						"Encrypted XOR Key    : {EncryptedXorKey}\n" +
						"Timestamp            : {Timestamp}\n" +
						"-------------------------",
						request.SenderId,
						messageText,
						request.EncryptedContent.ToBase64(),
						request.DigitalSignature.ToBase64(),
						request.EncryptedXorKey.ToBase64(),
						readableTimestamp
					);
				}

				// Save message to store.
				_rMessageStore.AddMessage(request.SenderId, request.Timestamp, messageText);

				return new MessageAck { Delivered = true, ErrorMessage = string.Empty };
			}
			catch (CryptographicException ex)
			{
				_rLogger.LogError("Decryption failed. The keys might be mismatched: {Message}", ex.Message);
				return new MessageAck { Delivered = false, ErrorMessage = "Decryption failed." };
			}
			catch (Exception ex)
			{
				_rLogger.LogError("Error processing incoming message: {Message}", ex.Message);
				return new MessageAck { Delivered = false, ErrorMessage = "Internal node error." };
			}
		}
	}
}
