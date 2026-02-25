using System.Text;
using Grpc.Core;
using GrpcSecure.Shared.Protos;
using GrpcSecureShared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

Console.WriteLine("=== Secure gRPC Client ===");

// Setup Generic Host.
var host = Host.CreateDefaultBuilder(args)
	.ConfigureAppConfiguration((context, config) =>
	{
		config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
		config.AddEnvironmentVariables();
	})
	.ConfigureServices((context, services) =>
	{
		// Read the dynamic endpoint from configuration.
		var grpcEndpoint = context.Configuration["GrpcServer:Endpoint"];
		if (string.IsNullOrEmpty(grpcEndpoint))
			throw new InvalidOperationException("gRPC Server Endpoint is not configured.");

		// Register the gRPC Client using the Factory pattern.
		services.AddGrpcClient<SecureCommunication.SecureCommunicationClient>(options =>
		{
			options.Address = new Uri(grpcEndpoint);
		});

		// Register main application logic.
		services.AddTransient<SecureChatAppV2>();
	})
	.Build();

// Resolve and run the application.
var app = host.Services.GetRequiredService<SecureChatAppV2>();
await app.RunAsync();

public class SecureChatAppV2
{
	private readonly SecureCommunication.SecureCommunicationClient _rClient;
	private readonly ILogger<SecureChatAppV2> _rLogger;
	private readonly object _rConsoleLock = new object();

	private string _currentConsoleInput = string.Empty;

	public SecureChatAppV2(SecureCommunication.SecureCommunicationClient client, ILogger<SecureChatAppV2> logger)
	{
		_rClient = client;
		_rLogger = logger;
	}

	public async Task RunAsync()
	{
		_rLogger.LogInformation("- Initiating handshake...");

		try
		{
			// 1. Perform Handshake.
			var handshakeResponse = await _rClient.HandshakeAsync(new HandshakeRequest());
			string sessionId = handshakeResponse.SessionId;
			byte[] sessionKey = handshakeResponse.XorKey.ToByteArray();

			_rLogger.LogInformation("- Handshake successful. Session ID: [{SessionId}].", sessionId);
			Console.WriteLine("\n=================================================");
			Console.WriteLine($"YOUR ID: {sessionId}");
			Console.WriteLine("=================================================\n");

			// Prepare headers (Metadata) for subsequent calls.
			var headers = new Metadata
			{
				{ "session-id", sessionId }
			};

			// 2. Open the Bidirectional Stream.
			using var chatStream = _rClient.ChatStream(headers);

			// 3. Start Background Listener Task.
			var receiveTask = Task.Run(async () =>
			{
				try
				{
					// Listen for incoming messages asynchronously.
					await foreach (var response in chatStream.ResponseStream.ReadAllAsync())
					{
						// Decrypt the payload using YOUR key.
						byte[] decryptedBytes = XorCipherHelper.Process(response.Payload.ToByteArray(), sessionKey);
						string messageText = Encoding.UTF8.GetString(decryptedBytes);

						lock (_rConsoleLock)
						{
							// 1. Return the cursor to the beginning and erase what is currently on the screen with spaces.
							Console.SetCursorPosition(0, Console.CursorTop);
							Console.Write(new string(' ', Console.WindowWidth - 1));

							// 2. Return the cursor to the beginning and print the interlocutor's message.
							Console.SetCursorPosition(0, Console.CursorTop);
							Console.WriteLine($"[{response.SenderSessionId}] says: {messageText}");

							// 3. Draw the prompt and the text that user did not finish printing.
							Console.Write($"> {_currentConsoleInput}");
						}

						//// Overwrite the current console line to keep it clean.
						//Console.WriteLine($"\n[{response.SenderSessionId}] says: {messageText}");
						//// Restore the input prompt.
						//Console.Write("> ");
					}
				}
				catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
				{
					_rLogger.LogWarning("Server disconnected the stream.");
				}
				catch (Exception ex)
				{
					_rLogger.LogError("[Error]: Receiving message: {Message}", ex.Message);
				}
			});

			// 4. Main Thread: Handle User Input
			Console.WriteLine("[System]: Chat started!");
			Console.WriteLine("[System]: 1. Type '/to <TargetSessionID>' to set a chat recipient. After that, you can enter messages to be sent to the target recipient.");
			Console.WriteLine("[System]: 2. Type '/to <TargetSessionID> /m <message>' to set recipient and send instantly.");
			Console.WriteLine("[System]: 3. Type '/server <message>' to ping the server directly (Unary call).");
			Console.WriteLine("[System]: 4. Type 'exit' to quit.");

			string currentTargetId = string.Empty;

			while (true)
			{
				string input = _ReadUserInput();

				if (string.IsNullOrWhiteSpace(input))
					continue;
				if (string.Equals(input, "exit", StringComparison.OrdinalIgnoreCase))
					break;

				// --- Command 1: Unary Call directly to the Server ---
				if (input.StartsWith("/server ", StringComparison.OrdinalIgnoreCase))
				{
					string serverMsg = input.Substring(8).Trim();

					// Encrypt for the server.
					byte[] serverPayloadBytes = Encoding.UTF8.GetBytes(serverMsg);
					byte[] encryptedPayloadBytes = XorCipherHelper.Process(serverPayloadBytes, sessionKey);

					var request = new SecureMessage { Payload = Google.Protobuf.ByteString.CopyFrom(encryptedPayloadBytes) };

					string messageToSend = Convert.ToBase64String(encryptedPayloadBytes);
					Console.WriteLine($"- Sending message: [{messageToSend}].");

					try
					{
						// Send Unary request and wait for the response.
						var response = await _rClient.SendMessageAsync(request, headers);

						// Decrypt the server's reply.
						byte[] decryptedResponseBytes = XorCipherHelper.Process(response.Payload.ToByteArray(), sessionKey);
						string serverReply = Encoding.UTF8.GetString(decryptedResponseBytes);

						Console.WriteLine($"- Server Echo Reply (Unary): [{serverReply}].");
					}
					catch (RpcException ex)
					{
						_rLogger.LogError("[Error]: Server Unary Call Failed: {Detail}", ex.Status.Detail);
					}

					// Skip the rest of the loop so it doesn't send this to the chat stream
					continue;
				}

				// --- Command 2: Switch recipient (and optionally send a message using /m) ---
				if (input.StartsWith("/to ", StringComparison.OrdinalIgnoreCase))
				{
					string remainder = input.Substring(4).Trim();

					if (string.IsNullOrEmpty(remainder))
					{
						Console.WriteLine("[System]: Error - You must set a recipient first. Use '/to <TargetSessionID>' or type '/to <TargetSessionID> /m <message>' to send the message imediatly.");
						continue;
					}

					// Look for the "/m" separator.
					int mIndex = remainder.IndexOf("/m", StringComparison.OrdinalIgnoreCase);

					if (mIndex == -1)
					{
						// No "/m" found at all. Just setting the recipient ID.
						currentTargetId = remainder;

						Console.WriteLine($"[System]: Recipient set to [{currentTargetId}].");
						continue;
					}
					else
					{
						// Both recipient and message provided
						currentTargetId = remainder.Substring(0, mIndex).Trim();
						Console.WriteLine($"[System]: Recipient set to [{currentTargetId}].");

						// Overwrite 'input' with the actual message after "/m".
						input = remainder.Substring(mIndex + 2).Trim();

						// If the message is empty (user typed "/to 123 /m"), skip sending.
						if (string.IsNullOrWhiteSpace(input))
						{
							Console.WriteLine("[System]: Waiting for a message to send to the target client.");
							continue;
						}
					}
				}

				// --- Default: Send to the Chat Stream ---
				if (string.IsNullOrEmpty(currentTargetId))
				{
					Console.WriteLine("[System]: Error - You must set a recipient first. Use '/to <TargetSessionID>' or type '/server <message>'.");
					continue;
				}

				// Encrypt the message using YOUR key (the server will re-encrypt it for the target)
				byte[] payloadBytes = Encoding.UTF8.GetBytes(input);
				byte[] encryptedPayload = XorCipherHelper.Process(payloadBytes, sessionKey);

				var chatMessage = new ChatMessage
				{
					TargetSessionId = currentTargetId,
					Payload = Google.Protobuf.ByteString.CopyFrom(encryptedPayload)
				};

				// Push into the open stream
				await chatStream.RequestStream.WriteAsync(chatMessage);
			}

			// 5. Graceful shutdown.
			Console.WriteLine("[System]: Closing connection...");
			await chatStream.RequestStream.CompleteAsync(); // Tell the server we are done sending.
			await receiveTask; // Wait for the listener to finish.
		}
		catch (RpcException ex)
		{
			_rLogger.LogError("[Error]: gRPC failed. {Detail}.", ex.Status.Detail);
		}
	}

	private string _ReadUserInput()
	{
		_currentConsoleInput = string.Empty;

		lock (_rConsoleLock)
		{
			Console.Write("> ");
		}

		while (true)
		{
			// Read the key, but do NOT automatically display it on the screen (intercept: true).
			var keyInfo = Console.ReadKey(intercept: true);

			lock (_rConsoleLock)
			{
				if (keyInfo.Key == ConsoleKey.Enter)
				{
					Console.WriteLine();
					string result = _currentConsoleInput;
					_currentConsoleInput = string.Empty; // clean after send.
					return result;
				}
				else if (keyInfo.Key == ConsoleKey.Backspace)
				{
					if (_currentConsoleInput.Length > 0)
					{
						// Delete the last character from memory.
						_currentConsoleInput = _currentConsoleInput.Substring(0, _currentConsoleInput.Length - 1);
						// Visually erase a character in the console (backspace, space, backspace).
						Console.Write("\b \b");
					}
				}
				else if (!char.IsControl(keyInfo.KeyChar))
				{
					// Add a regular symbol to buffer and draw it.
					_currentConsoleInput += keyInfo.KeyChar;
					Console.Write(keyInfo.KeyChar);
				}
			}
		}
	}
}
