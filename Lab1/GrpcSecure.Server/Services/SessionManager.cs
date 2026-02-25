using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace GrpcSecure.Server.Services
{
	public interface ISessionManager
	{
		(string SessionId, byte[] Key) CreateSession();
		byte[]? GetKey(string sessionId);
	}

	public class SessionManager : ISessionManager
	{
		// Thread-safe dictionary to store keys per session.
		private readonly ConcurrentDictionary<string, byte[]> _rSessions = new();

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
	}
}
