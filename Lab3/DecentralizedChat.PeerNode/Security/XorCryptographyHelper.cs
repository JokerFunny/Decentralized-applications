using System.Security.Cryptography;

namespace DecentralizedChat.PeerNode.Security
{
	public static class XorCryptographyHelper
	{
		/// <summary>
		/// Generates a cryptographically secure random key.
		/// We will generate a fresh key for EVERY single message to ensure maximum security.
		/// </summary>
		/// <param name="length">The length of the key in bytes (default 32 bytes / 256 bits)</param>
		public static byte[] GenerateRandomKey(int length = 32)
		{
			byte[] key = new byte[length];

			// Using RandomNumberGenerator is much more secure than the standard 'new Random()'
			using (var rng = RandomNumberGenerator.Create())
			{
				rng.GetBytes(key);
			}

			return key;
		}

		/// <summary>
		/// Applies the XOR cipher to the input array.
		/// Because XOR is symmetric, you call this EXACT same method to decrypt the data!
		/// </summary>
		/// <param name="input">The plaintext bytes (if encrypting) or ciphertext bytes (if decrypting)</param>
		/// <param name="key">The symmetric XOR key</param>
		public static byte[] Process(byte[] input, byte[] key)
		{
			if (input == null || input.Length == 0)
				throw new ArgumentException("Input data cannot be empty.", nameof(input));

			if (key == null || key.Length == 0)
				throw new ArgumentException("Encryption key cannot be empty.", nameof(key));

			byte[] output = new byte[input.Length];

			for (int i = 0; i < input.Length; i++)
			{
				// We use the modulo operator (%) to loop the key 
				// in case the message text is longer than the key itself.
				output[i] = (byte)(input[i] ^ key[i % key.Length]);
			}

			return output;
		}
	}
}
