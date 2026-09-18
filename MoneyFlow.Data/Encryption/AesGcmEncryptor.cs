using System;
using System.Security.Cryptography;

namespace MoneyFlow.Data.Encryption;

/// <summary>
/// AES-256-GCM encryption/decryption operations.
/// 32-byte key, 12-byte nonce, 16-byte authentication tag.
/// </summary>
public sealed class AesGcmEncryptor
{
    public const int KeySizeBytes = 32;   // 256-bit
    public const int NonceSizeBytes = 12; // 96-bit
    public const int TagSizeBytes = 16;   // 128-bit

    /// <summary>
    /// Encrypts plaintext using AES-256-GCM.
    /// </summary>
    /// <param name="plaintext">Data to encrypt.</param>
    /// <param name="key">256-bit encryption key.</param>
    /// <param name="associatedData">Optional AAD for authentication (not encrypted, but authenticated).</param>
    /// <returns>Tuple of (ciphertext, nonce, tag).</returns>
    public (byte[] Ciphertext, byte[] Nonce, byte[] Tag) Encrypt(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> associatedData = default)
    {
        if (key.Length != KeySizeBytes)
            throw new ArgumentException($"Key must be {KeySizeBytes} bytes.", nameof(key));

        var nonce = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSizeBytes];

        using var aesGcm = new AesGcm(key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        return (ciphertext, nonce, tag);
    }

    /// <summary>
    /// Encrypts plaintext using AES-256-GCM with a caller-provided nonce.
    /// Only use when you explicitly need a specific nonce (e.g. DEK wrapping with stored nonce).
    /// </summary>
    public byte[] EncryptWithNonce(
        ReadOnlySpan<byte> plaintext,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce,
        Span<byte> tag,
        ReadOnlySpan<byte> associatedData = default)
    {
        if (key.Length != KeySizeBytes)
            throw new ArgumentException($"Key must be {KeySizeBytes} bytes.", nameof(key));
        if (nonce.Length != NonceSizeBytes)
            throw new ArgumentException($"Nonce must be {NonceSizeBytes} bytes.", nameof(nonce));

        var ciphertext = new byte[plaintext.Length];

        using var aesGcm = new AesGcm(key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);

        return ciphertext;
    }

    /// <summary>
    /// Decrypts ciphertext using AES-256-GCM.
    /// Throws CryptographicException if authentication fails (wrong key, tampered data, or wrong AAD).
    /// </summary>
    public byte[] Decrypt(
        ReadOnlySpan<byte> ciphertext,
        ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> nonce,
        ReadOnlySpan<byte> tag,
        ReadOnlySpan<byte> associatedData = default)
    {
        if (key.Length != KeySizeBytes)
            throw new ArgumentException($"Key must be {KeySizeBytes} bytes.", nameof(key));
        if (nonce.Length != NonceSizeBytes)
            throw new ArgumentException($"Nonce must be {NonceSizeBytes} bytes.", nameof(nonce));
        if (tag.Length != TagSizeBytes)
            throw new ArgumentException($"Tag must be {TagSizeBytes} bytes.", nameof(tag));

        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, TagSizeBytes);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);

        return plaintext;
    }

    /// <summary>
    /// Generates a cryptographically secure random key.
    /// </summary>
    public static byte[] GenerateKey()
    {
        var key = new byte[KeySizeBytes];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    /// <summary>
    /// Generates a cryptographically secure random nonce.
    /// </summary>
    public static byte[] GenerateNonce()
    {
        var nonce = new byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);
        return nonce;
    }
}
