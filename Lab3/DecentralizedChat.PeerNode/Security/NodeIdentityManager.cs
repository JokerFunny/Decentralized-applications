using System.Security.Cryptography;

namespace DecentralizedChat.PeerNode.Security
{
	// This class is registered as a Singleton so the same keys are used 
	// throughout the entire lifecycle of the application.
	public class NodeIdentityManager
	{
		private readonly RSA _rRsa;

		public string NodeId { get; }
		public string PublicKeyBase64 { get; }

		public NodeIdentityManager(IConfiguration config)
		{
			// 1. Establish the Node's Identity
			NodeId = config["NodeConfig:NodeId"] ?? $"Peer-{Guid.NewGuid().ToString()[..4]}";

			// 2. Establish the Node's Cryptographic Keys
			_rRsa = RSA.Create(2048);

			// 3. Export the public key in SubjectPublicKeyInfo (SPKI) format.
			byte[] publicKeyBytes = _rRsa.ExportSubjectPublicKeyInfo();

			// 4. Convert to Base64 so it can be easily stored in our string fields in gRPC
			PublicKeyBase64 = Convert.ToBase64String(publicKeyBytes);
		}

		// Expose the underlying RSA instance so we can use it later 
		// for signing outgoing messages and decrypting incoming XOR keys.
		public RSA GetRsaInstance() => _rRsa;
	}
}
