using System.Security.Cryptography;
using System.Text;

namespace KicktippIntegration.Tests.Infrastructure;

/// <summary>
/// Provides AES-256-GCM encryption/decryption for test fixtures.
/// </summary>
public static class FixtureEncryptor
{
    private const int NonceSize = 12; // 96 bits for GCM
    private const int TagSize = 16;   // 128 bits for GCM

    /// <summary>
    /// Encrypts plaintext content using AES-256-GCM.
    /// </summary>
    /// <param name="plaintext">The content to encrypt.</param>
    /// <param name="base64Key">Base64-encoded 256-bit key.</param>
    /// <returns>Base64-encoded encrypted data (nonce + ciphertext + tag).</returns>
    public static string Encrypt(string plaintext, string base64Key)
    {
        var key = Convert.FromBase64String(base64Key);
        ValidateKey(key);

        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintextBytes, ciphertext, tag);

        // Combine: nonce (12) + ciphertext (variable) + tag (16)
        var result = new byte[NonceSize + ciphertext.Length + TagSize];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(ciphertext, 0, result, NonceSize, ciphertext.Length);
        Buffer.BlockCopy(tag, 0, result, NonceSize + ciphertext.Length, TagSize);

        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Decrypts AES-256-GCM encrypted content.
    /// </summary>
    /// <param name="encryptedBase64">Base64-encoded encrypted data (nonce + ciphertext + tag).</param>
    /// <param name="base64Key">Base64-encoded 256-bit key.</param>
    /// <returns>Decrypted plaintext content.</returns>
    public static string Decrypt(string encryptedBase64, string base64Key)
    {
        var key = Convert.FromBase64String(base64Key);
        ValidateKey(key);

        var encrypted = Convert.FromBase64String(encryptedBase64);
        if (encrypted.Length < NonceSize + TagSize)
        {
            throw new ArgumentException("Encrypted data is too short to contain nonce and tag.");
        }

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var ciphertextLength = encrypted.Length - NonceSize - TagSize;
        var ciphertext = new byte[ciphertextLength];
        var plaintext = new byte[ciphertextLength];

        Buffer.BlockCopy(encrypted, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(encrypted, NonceSize, ciphertext, 0, ciphertextLength);
        Buffer.BlockCopy(encrypted, NonceSize + ciphertextLength, tag, 0, TagSize);

        using var aes = new AesGcm(key, TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    private static void ValidateKey(byte[] key)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException($"Key must be 256 bits (32 bytes). Got {key.Length} bytes.");
        }
    }
}
