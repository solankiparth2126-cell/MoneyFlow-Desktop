using System;

namespace MoneyFlow.Core.Interfaces;

/// <summary>
/// Authenticated encryption and key management abstraction.
/// Handles per-company DEK generation, password-based KEK derivation, DEK wrapping, and AES-256-GCM authenticated payload encryption.
/// </summary>
public interface ICompanyDataCrypto
{
    /// <summary>
    /// Derives a Key Encryption Key (KEK) from the user password and salt using PBKDF2-HMAC-SHA256.
    /// </summary>
    byte[] DeriveKek(string password, byte[] salt, int iterations);

    /// <summary>
    /// Generates a cryptographically secure random Data Encryption Key (DEK).
    /// </summary>
    byte[] GenerateDek();

    /// <summary>
    /// Generates cryptographically secure random bytes of specified length.
    /// </summary>
    byte[] GenerateRandomBytes(int length);

    /// <summary>
    /// Wraps (encrypts) the company DEK with the KEK using AES-256-GCM.
    /// </summary>
    (byte[] wrappedDek, byte[] nonce, byte[] tag) WrapDek(byte[] dek, byte[] kek);

    /// <summary>
    /// Unwraps (decrypts and verifies) the company DEK with the KEK using AES-256-GCM.
    /// Throws InvalidPasswordException or CompanyDataTamperedException if authentication fails.
    /// </summary>
    byte[] UnwrapDek(byte[] wrappedDek, byte[] nonce, byte[] tag, byte[] kek);

    /// <summary>
    /// Encrypts the company payload using AES-256-GCM with the per-company DEK and cryptographically binds the associatedData.
    /// </summary>
    (byte[] ciphertext, byte[] nonce, byte[] tag) EncryptPayload(byte[] plaintext, byte[] dek, byte[] associatedData);

    /// <summary>
    /// Decrypts the company payload using AES-256-GCM and verifies the authentication tag against the ciphertext and associatedData.
    /// Throws CompanyDataTamperedException if tag does not match.
    /// </summary>
    byte[] DecryptPayload(byte[] ciphertext, byte[] nonce, byte[] tag, byte[] dek, byte[] associatedData);
}
