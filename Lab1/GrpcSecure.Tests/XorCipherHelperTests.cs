using System.Text;
using AwesomeAssertions;
using GrpcSecureShared;

namespace GrpcSecure.Tests
{
	public class XorCipherHelperTests
	{
		[Fact]
		public void Process_WithValidData_Should_SymmetricallyEncryptAndDecrypt()
		{
			string originalText = "Hello, Secure gRPC World!";
			byte[] originalBytes = Encoding.UTF8.GetBytes(originalText);
			byte[] key = Encoding.UTF8.GetBytes("SuperSecretKey123");

			byte[] encryptedBytes = XorCipherHelper.Process(originalBytes, key);
			byte[] decryptedBytes = XorCipherHelper.Process(encryptedBytes, key);
			string decryptedText = Encoding.UTF8.GetString(decryptedBytes);

			encryptedBytes.Should().NotBeEquivalentTo(originalBytes, "Encrypted data should differ from original.");
			decryptedBytes.Should().BeEquivalentTo(originalBytes, "Decrypted data must match original bytes.");
			decryptedText.Should().Be(originalText, "Decrypted text must match original text.");
		}

		[Theory]
		[InlineData(null)]
		[InlineData(new byte[] { })]
		public void Process_WithNullOrEmptyKey_Should_ThrowArgumentException(byte[] invalidKey)
		{
			byte[] data = Encoding.UTF8.GetBytes("Some data");

			Action action = () => XorCipherHelper.Process(data, invalidKey);

			action.Should().Throw<ArgumentException>()
				.WithMessage("Key cannot be null or empty.");
		}
	}
}
