using System.Security.Cryptography;
using System.Text;

namespace Orchestrator.Commands.Snapshots;

/// <summary>
/// Provides AES-256-GCM encryption for test fixtures.
/// This is a copy of the logic from KicktippIntegration.Tests to avoid a dependency.
/// </summary>
public static class SnapshotEncryptor
{
    private const int NonceSize = 12; // 96 bits for GCM
    private const int TagSize = 16;   // 128 bits for GCM

    /// <summary>
    /// Generates a new random AES-256 encryption key.
    /// </summary>
    /// <returns>Base64-encoded 256-bit key.</returns>
    public static string GenerateKey()
    {
        var keyBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(keyBytes);
    }

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

    private static void ValidateKey(byte[] key)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException($"Key must be 256 bits (32 bytes). Got {key.Length} bytes.");
        }
    }
}
