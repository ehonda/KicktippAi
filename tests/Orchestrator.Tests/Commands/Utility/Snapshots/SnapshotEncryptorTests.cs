using Orchestrator.Commands.Utility.Snapshots;

namespace Orchestrator.Tests.Commands.Utility.Snapshots;

/// <summary>
/// Tests for <see cref="SnapshotEncryptor"/>.
/// </summary>
public class SnapshotEncryptorTests
{
    [Test]
    public async Task GenerateKey_returns_valid_base64_encoded_256_bit_key()
    {
        // Act
        var key = SnapshotEncryptor.GenerateKey();

        // Assert
        await Assert.That(key).IsNotNull().And.IsNotEmpty();
        
        // Verify it's valid Base64
        var keyBytes = Convert.FromBase64String(key);
        
        // Verify it's 256 bits (32 bytes)
        await Assert.That(keyBytes.Length).IsEqualTo(32);
    }

    [Test]
    public async Task GenerateKey_returns_different_keys_on_each_call()
    {
        // Act
        var key1 = SnapshotEncryptor.GenerateKey();
        var key2 = SnapshotEncryptor.GenerateKey();
        var key3 = SnapshotEncryptor.GenerateKey();

        // Assert
        await Assert.That(key1).IsNotEqualTo(key2);
        await Assert.That(key2).IsNotEqualTo(key3);
        await Assert.That(key1).IsNotEqualTo(key3);
    }

    [Test]
    public async Task Encrypt_returns_output_different_from_input()
    {
        // Arrange
        var key = SnapshotEncryptor.GenerateKey();
        var plaintext = "This is sensitive test data that should be encrypted.";

        // Act
        var encrypted = SnapshotEncryptor.Encrypt(plaintext, key);

        // Assert
        await Assert.That(encrypted).IsNotEqualTo(plaintext);
        await Assert.That(encrypted).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task Encrypt_returns_valid_base64_output()
    {
        // Arrange
        var key = SnapshotEncryptor.GenerateKey();
        var plaintext = "Test content for encryption.";

        // Act
        var encrypted = SnapshotEncryptor.Encrypt(plaintext, key);

        // Assert - should not throw when decoding Base64
        var encryptedBytes = Convert.FromBase64String(encrypted);
        await Assert.That(encryptedBytes.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Encrypt_produces_different_output_for_same_input_due_to_random_nonce()
    {
        // Arrange
        var key = SnapshotEncryptor.GenerateKey();
        var plaintext = "Same content encrypted multiple times.";

        // Act
        var encrypted1 = SnapshotEncryptor.Encrypt(plaintext, key);
        var encrypted2 = SnapshotEncryptor.Encrypt(plaintext, key);

        // Assert - Different nonces should produce different ciphertext
        await Assert.That(encrypted1).IsNotEqualTo(encrypted2);
    }

    [Test]
    public async Task Encrypt_handles_empty_string()
    {
        // Arrange
        var key = SnapshotEncryptor.GenerateKey();
        var plaintext = "";

        // Act
        var encrypted = SnapshotEncryptor.Encrypt(plaintext, key);

        // Assert
        await Assert.That(encrypted).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task Encrypt_handles_unicode_content()
    {
        // Arrange
        var key = SnapshotEncryptor.GenerateKey();
        var plaintext = "Unicode content: äöü ß 日本語 🎉 emoji";

        // Act
        var encrypted = SnapshotEncryptor.Encrypt(plaintext, key);

        // Assert
        await Assert.That(encrypted).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task Encrypt_handles_large_content()
    {
        // Arrange
        var key = SnapshotEncryptor.GenerateKey();
        var plaintext = new string('x', 100_000); // 100KB of data

        // Act
        var encrypted = SnapshotEncryptor.Encrypt(plaintext, key);

        // Assert
        await Assert.That(encrypted).IsNotNull().And.IsNotEmpty();
    }

    [Test]
    public async Task Encrypt_with_invalid_key_length_throws_ArgumentException()
    {
        // Arrange
        var shortKey = Convert.ToBase64String(new byte[16]); // 128 bits instead of 256
        var plaintext = "Test content.";

        // Act & Assert
        await Assert.That(() => SnapshotEncryptor.Encrypt(plaintext, shortKey))
            .Throws<ArgumentException>()
            .WithMessageContaining("256 bits");
    }

    [Test]
    public async Task Encrypt_with_too_long_key_throws_ArgumentException()
    {
        // Arrange
        var longKey = Convert.ToBase64String(new byte[64]); // 512 bits instead of 256
        var plaintext = "Test content.";

        // Act & Assert
        await Assert.That(() => SnapshotEncryptor.Encrypt(plaintext, longKey))
            .Throws<ArgumentException>()
            .WithMessageContaining("256 bits");
    }

    [Test]
    public async Task Encrypt_with_invalid_base64_key_throws_FormatException()
    {
        // Arrange
        var invalidBase64Key = "not-valid-base64!!!";
        var plaintext = "Test content.";

        // Act & Assert
        await Assert.That(() => SnapshotEncryptor.Encrypt(plaintext, invalidBase64Key))
            .Throws<FormatException>();
    }
}
