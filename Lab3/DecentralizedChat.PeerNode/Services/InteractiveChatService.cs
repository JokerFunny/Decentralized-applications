using System.Security.Cryptography;
using System.Text;
using DecentralizedChat.PeerNode.Security;
using Grpc.Net.Client;
using Shared.Protos;

namespace DecentralizedChat.PeerNode.Services
{
	/// <summary>
	/// This is the "Client Half" of the P2P node.
	/// It runs a continuous loop in the console, allowing the user to select peers,
	/// encrypt messages, and send them directly over the decentralized network.
	/// </summary>
	public class InteractiveChatService : BackgroundService
	{
		private readonly NodeIdentityManager _rIdentityManager;
		private readonly RegistryService.RegistryServiceClient _rRegistryClient;
		private readonly ILogger<InteractiveChatService> _rLogger;

		public InteractiveChatService(NodeIdentityManager identityManager, RegistryService.RegistryServiceClient registryClient,
			ILogger<InteractiveChatService> logger)
		{
			_rIdentityManager = identityManager;
			_rRegistryClient = registryClient;
			_rLogger = logger;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			// Wait a few seconds to let the gRPC server and Registry registration finish
			// so our console UI doesn't get swallowed by the startup logs.
			await Task.Delay(3000, stoppingToken);

			Console.WriteLine(new string('=', 50));
			Console.WriteLine($" NODE: {_rIdentityManager.NodeId} READY FOR CHAT");
			Console.WriteLine(new string('=', 50));
			Console.WriteLine("Commands: [list] peers, [send] message, [exit]");

			// Infinite interactive loop
			while (!stoppingToken.IsCancellationRequested)
			{
				Console.Write("\n> ");
				string? command = Console.ReadLine()?.Trim().ToLower();

				if (command == "exit")
				{
					Environment.Exit(0);
				}
				else if (command == "list")
				{
					await ListActivePeersAsync(stoppingToken);
				}
				else if (command == "send")
				{
					await SendMessagePromptAsync(stoppingToken);
				}
			}
		}

		private async Task ListActivePeersAsync(CancellationToken cancellationToken)
		{
			try
			{
				var response = await _rRegistryClient.GetActiveNodesAsync(
					new GetNodesRequest
					{
						RequesterNodeId = _rIdentityManager.NodeId
					},
					cancellationToken: cancellationToken);

				if (response.Peers.Count == 0)
				{
					Console.WriteLine("No other peers are currently online.");
					return;
				}

				Console.WriteLine("\n--- ACTIVE PEERS ---");
				foreach (var peer in response.Peers)
					Console.WriteLine($" - {peer.NodeId} ({peer.Address})");

				Console.WriteLine("--------------------\n");
			}
			catch (Exception ex)
			{
				_rLogger.LogError("Failed to fetch peers from tracker: {Message}", ex.Message);
			}
		}

		private async Task SendMessagePromptAsync(CancellationToken cancellationToken)
		{
			Console.Write("Enter Target Node ID: ");
			string? targetId = Console.ReadLine()?.Trim();

			if (string.IsNullOrEmpty(targetId))
				return;

			// 1. Ask the Tracker for the Target's Address and Public Key
			var response = await _rRegistryClient.GetActiveNodesAsync(
				new GetNodesRequest
				{
					RequesterNodeId = _rIdentityManager.NodeId
				},
				cancellationToken: cancellationToken);

			var targetPeer = response.Peers.FirstOrDefault(p => p.NodeId.Equals(targetId, StringComparison.OrdinalIgnoreCase));

			if (targetPeer == null)
			{
				Console.WriteLine($"[Error] Node '{targetId}' not found or offline.");
				return;
			}

			Console.Write("Enter Message: ");
			string? messageText = Console.ReadLine();

			if (string.IsNullOrEmpty(messageText))
				return;

			try
			{
				byte[] plaintextBytes = Encoding.UTF8.GetBytes(messageText);

				// ========================================================
				// CRYPTOGRAPHY PHASE
				// ========================================================

				// A. Generate a random 32-byte XOR key for this specific message
				byte[] xorKey = XorCryptographyHelper.GenerateRandomKey(32);

				// B. Encrypt the message text using the XOR cipher
				byte[] encryptedContent = XorCryptographyHelper.Process(plaintextBytes, xorKey);

				// C. Sign the ORIGINAL plaintext with OUR Private RSA Key
				byte[] digitalSignature = _rIdentityManager.GetRsaInstance().SignData(plaintextBytes, HashAlgorithmName.SHA256,
					RSASignaturePadding.Pkcs1);

				// D. Encrypt the XOR key with THEIR Public RSA Key
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

				var ack = await peerClient.SendDirectMessageAsync(directMessage, cancellationToken: cancellationToken);

				if (ack.Delivered)
				{
					Console.WriteLine("[Success] Message securely delivered and verified!");
				}
				else
				{
					Console.WriteLine($"[Failed] Target rejected message. Reason: {ack.ErrorMessage}");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[Error] Failed to send message: {ex.Message}");
			}
		}
	}
}
