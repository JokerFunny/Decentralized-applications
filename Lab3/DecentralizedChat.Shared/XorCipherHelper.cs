public static class XorCipherHelper
{
	// Applies XOR logic. Since XOR is symmetric, the same method is used for encrypt/decrypt.
	public static byte[] Process(byte[] data, byte[] key)
	{
		if (key == null || key.Length == 0)
			throw new ArgumentException("Key cannot be null or empty.");

		var result = new byte[data.Length];
		for (int i = 0; i < data.Length; i++)
			result[i] = (byte)(data[i] ^ key[i % key.Length]);

		return result;
	}
}