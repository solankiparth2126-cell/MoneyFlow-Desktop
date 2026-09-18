namespace MoneyFlow.Data.Encryption;

/// <summary>
/// Holds the security envelope data stored in the company.data file header.
/// Contains the wrapped DEK and the parameters needed to unwrap it.
/// </summary>
public sealed class KeyEnvelope
{
    /// <summary>
    /// Security mode: 0 = Passwordless, 1 = PasswordProtected.
    /// </summary>
    public SecurityMode Mode { get; set; }

    /// <summary>KDF algorithm identifier (1 = PBKDF2-HMAC-SHA256).</summary>
    public byte KdfAlgorithm { get; set; } = 1;

    /// <summary>PBKDF2 iteration count.</summary>
    public int KdfIterations { get; set; } = 600_000;

    /// <summary>Random per-company salt (32 bytes).</summary>
    public byte[] Salt { get; set; } = new byte[32];

    /// <summary>Key protection version for future upgrades.</summary>
    public ushort KeyProtectionVersion { get; set; } = 1;

    /// <summary>Nonce used when wrapping the DEK (12 bytes).</summary>
    public byte[] DekWrapNonce { get; set; } = new byte[AesGcmEncryptor.NonceSizeBytes];

    /// <summary>
    /// The DEK encrypted with AES-256-GCM using the KEK.
    /// Length = 32 bytes (DEK ciphertext).
    /// </summary>
    public byte[] WrappedDek { get; set; } = new byte[AesGcmEncryptor.KeySizeBytes];

    /// <summary>Authentication tag from the DEK wrapping operation (16 bytes).</summary>
    public byte[] DekWrapTag { get; set; } = new byte[AesGcmEncryptor.TagSizeBytes];
}

/// <summary>
/// Defines how the company DEK is protected.
/// </summary>
public enum SecurityMode : byte
{
    /// <summary>DEK is protected by an application-derived key. No password prompt.</summary>
    Passwordless = 0,

    /// <summary>DEK is protected by a password-derived KEK (PBKDF2). Password required to open.</summary>
    PasswordProtected = 1
}
