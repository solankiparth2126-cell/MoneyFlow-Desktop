using System;
using System.Security.Cryptography;
using System.Text;

namespace MoneyFlow.Data.Encryption;

/// <summary>
/// Manages DEK generation, wrapping/unwrapping, and KEK derivation.
/// Supports both PasswordProtected and Passwordless security modes.
/// </summary>
public sealed class KeyManager
{
    // Application-level secret used for passwordless DEK protection.
    // This provides encryption-at-rest protection against casual file inspection.
    // For strong security, users should enable company passwords.
    private static readonly byte[] AppSecret = Encoding.UTF8.GetBytes(
        "MyERP-Desktop-v1-F7A3B9C2-4E81-49D6-8B5C-3A7F2D1E9C04");

    private readonly AesGcmEncryptor _encryptor = new();

    /// <summary>
    /// Generates a new random 256-bit Data Encryption Key for a company.
    /// </summary>
    public byte[] GenerateDek() => AesGcmEncryptor.GenerateKey();

    /// <summary>
    /// Generates a new random 32-byte salt for KDF operations.
    /// </summary>
    public byte[] GenerateSalt()
    {
        var salt = new byte[32];
        RandomNumberGenerator.Fill(salt);
        return salt;
    }

    /// <summary>
    /// Creates a new KeyEnvelope for a password-protected company.
    /// Generates a random DEK and wraps it with a password-derived KEK.
    /// </summary>
    public (KeyEnvelope Envelope, byte[] Dek) CreatePasswordProtectedEnvelope(string password, int kdfIterations = 600_000)
    {
        var dek = GenerateDek();
        var salt = GenerateSalt();
        var kek = DeriveKek(password, salt, kdfIterations);

        var envelope = WrapDek(dek, kek, SecurityMode.PasswordProtected, salt, kdfIterations);

        // Clear the KEK from memory
        CryptographicOperations.ZeroMemory(kek);

        return (envelope, dek);
    }

    /// <summary>
    /// Creates a new KeyEnvelope for a passwordless company.
    /// Generates a random DEK and wraps it with an application-derived key.
    /// The company remains encrypted at rest but no password prompt is shown.
    /// </summary>
    public (KeyEnvelope Envelope, byte[] Dek) CreatePasswordlessEnvelope()
    {
        var dek = GenerateDek();
        var salt = GenerateSalt();
        var kek = DerivePasswordlessKek(salt);

        var envelope = WrapDek(dek, kek, SecurityMode.Passwordless, salt, kdfIterations: 1);

        CryptographicOperations.ZeroMemory(kek);

        return (envelope, dek);
    }

    /// <summary>
    /// Unwraps (decrypts) the DEK from a password-protected envelope.
    /// Throws CompanyAuthenticationException if the password is wrong.
    /// </summary>
    public byte[] UnwrapDekWithPassword(KeyEnvelope envelope, string password)
    {
        if (envelope.Mode != SecurityMode.PasswordProtected)
            throw new InvalidOperationException("Company is not password-protected.");

        var kek = DeriveKek(password, envelope.Salt, envelope.KdfIterations);

        try
        {
            var dek = _encryptor.Decrypt(
                envelope.WrappedDek,
                kek,
                envelope.DekWrapNonce,
                envelope.DekWrapTag);

            return dek;
        }
        catch (CryptographicException)
        {
            throw new CompanyAuthenticationException();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    /// <summary>
    /// Unwraps (decrypts) the DEK from a passwordless envelope.
    /// </summary>
    public byte[] UnwrapDekPasswordless(KeyEnvelope envelope)
    {
        if (envelope.Mode != SecurityMode.Passwordless)
            throw new InvalidOperationException("Company is password-protected.");

        var kek = DerivePasswordlessKek(envelope.Salt);

        try
        {
            return _encryptor.Decrypt(
                envelope.WrappedDek,
                kek,
                envelope.DekWrapNonce,
                envelope.DekWrapTag);
        }
        catch (CryptographicException)
        {
            throw new CompanyFileCorruptedException("Failed to unwrap company encryption key. File may be corrupted.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(kek);
        }
    }

    /// <summary>
    /// Changes the password on a password-protected company.
    /// Re-wraps the existing DEK with a new password-derived KEK.
    /// The encrypted payload does NOT need to be re-encrypted.
    /// </summary>
    public KeyEnvelope ChangePassword(KeyEnvelope currentEnvelope, string currentPassword, string newPassword)
    {
        // Unwrap DEK with current password
        var dek = UnwrapDekWithPassword(currentEnvelope, currentPassword);

        try
        {
            // Re-wrap DEK with new password
            var newSalt = GenerateSalt();
            var newKek = DeriveKek(newPassword, newSalt, currentEnvelope.KdfIterations);

            var newEnvelope = WrapDek(dek, newKek, SecurityMode.PasswordProtected, newSalt, currentEnvelope.KdfIterations);

            CryptographicOperations.ZeroMemory(newKek);
            return newEnvelope;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    /// <summary>
    /// Enables password protection on a currently passwordless company.
    /// Re-wraps the existing DEK with a password-derived KEK.
    /// </summary>
    public KeyEnvelope EnablePassword(KeyEnvelope currentEnvelope, string newPassword, int kdfIterations = 600_000)
    {
        var dek = UnwrapDekPasswordless(currentEnvelope);

        try
        {
            var newSalt = GenerateSalt();
            var newKek = DeriveKek(newPassword, newSalt, kdfIterations);

            var newEnvelope = WrapDek(dek, newKek, SecurityMode.PasswordProtected, newSalt, kdfIterations);

            CryptographicOperations.ZeroMemory(newKek);
            return newEnvelope;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    /// <summary>
    /// Disables password protection. Requires current password for authentication.
    /// Re-wraps DEK with passwordless key protection.
    /// </summary>
    public KeyEnvelope DisablePassword(KeyEnvelope currentEnvelope, string currentPassword)
    {
        var dek = UnwrapDekWithPassword(currentEnvelope, currentPassword);

        try
        {
            var newSalt = GenerateSalt();
            var newKek = DerivePasswordlessKek(newSalt);

            var newEnvelope = WrapDek(dek, newKek, SecurityMode.Passwordless, newSalt, kdfIterations: 1);

            CryptographicOperations.ZeroMemory(newKek);
            return newEnvelope;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    /// <summary>
    /// Derives a Key Encryption Key from a password using PBKDF2-HMAC-SHA256.
    /// </summary>
    private byte[] DeriveKek(string password, byte[] salt, int iterations)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(AesGcmEncryptor.KeySizeBytes);
    }

    /// <summary>
    /// Derives a KEK for passwordless companies using app secret + per-company salt.
    /// Provides encryption-at-rest; portable across machines with the same MyERP version.
    /// </summary>
    private byte[] DerivePasswordlessKek(byte[] salt)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(
            AppSecret,
            salt,
            100_000,
            HashAlgorithmName.SHA256);

        return pbkdf2.GetBytes(AesGcmEncryptor.KeySizeBytes);
    }

    /// <summary>
    /// Wraps a DEK with the given KEK using AES-256-GCM.
    /// </summary>
    private KeyEnvelope WrapDek(byte[] dek, byte[] kek, SecurityMode mode, byte[] salt, int kdfIterations)
    {
        var wrapNonce = AesGcmEncryptor.GenerateNonce();
        var wrapTag = new byte[AesGcmEncryptor.TagSizeBytes];
        var wrappedDek = _encryptor.EncryptWithNonce(dek, kek, wrapNonce, wrapTag);

        return new KeyEnvelope
        {
            Mode = mode,
            KdfAlgorithm = 1, // PBKDF2-HMAC-SHA256
            KdfIterations = kdfIterations,
            Salt = salt,
            KeyProtectionVersion = 1,
            DekWrapNonce = wrapNonce,
            WrappedDek = wrappedDek,
            DekWrapTag = wrapTag
        };
    }
}
