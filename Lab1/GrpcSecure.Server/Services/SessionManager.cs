using System.Collections.Concurrent;
using System.Security.Cryptography;
using Grpc.Core;
using GrpcSecure.Shared.Protos;

namespace GrpcSecure.Server.Services
{
	public interface ISessionManager
	{
		(string SessionId, byte[] Key) CreateSession();
		byte[]? GetKey(string sessionId);
		void RegisterStream(string sessionId, IServerStreamWriter<ChatMessage> stream);
		void RemoveStream(string sessionId);
		IServerStreamWriter<ChatMessage>? GetStream(string sessionId);
	}

	public class SessionManager : ISessionManager
	{
		// Thread-safe dictionary to store keys per session.
		private readonly ConcurrentDictionary<string, byte[]> _rSessions = new();

		// Stores the active gRPC response streams (channels to push data to clients)ю
		private readonly ConcurrentDictionary<string, IServerStreamWriter<ChatMessage>> _rActiveStreams = new();

		public (string SessionId, byte[] Key) CreateSession()
		{
			var sessionId = Guid.NewGuid().ToString();
			var key = new byte[32]; // 256-bit key.

			// Cryptographically secure random key generation.
			using (var rng = RandomNumberGenerator.Create())
			{
				rng.GetBytes(key);
			}

			_rSessions.TryAdd(sessionId, key);
			return (sessionId, key);
		}

		public byte[]? GetKey(string sessionId)
		{
			_rSessions.TryGetValue(sessionId, out var key);
			return key; // Returns null if session is invalid.
		}

		public void RegisterStream(string sessionId, IServerStreamWriter<ChatMessage> stream)
		{
			_rActiveStreams[sessionId] = stream;
		}

		public void RemoveStream(string sessionId)
		{
			_rActiveStreams.TryRemove(sessionId, out _);
		}

		public IServerStreamWriter<ChatMessage>? GetStream(string sessionId)
		{
			_rActiveStreams.TryGetValue(sessionId, out var stream);

			return stream;
		}
	}
}
