using System;
using System.Security.Cryptography;
using System.Text;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Exceptions;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Data.Storage;

public class CompanyDataCrypto : ICompanyDataCrypto
{
    public byte[] DeriveKek(string password, byte[] salt, int iterations)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Company password cannot be null or empty.", nameof(password));
        }

        if (salt == null || salt.Length < 16)
        {
            throw new ArgumentException("KDF salt must be at least 16 bytes.", nameof(salt));
        }

        if (iterations <= 0)
        {
            iterations = CompanyFileConstants.DefaultKdfIterations;
        }

        return Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            CompanyFileConstants.KeyLength);
    }

    public byte[] GenerateDek()
    {
        return GenerateRandomBytes(CompanyFileConstants.KeyLength);
    }

    public byte[] GenerateRandomBytes(int length)
    {
        byte[] bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return bytes;
    }

    public (byte[] wrappedDek, byte[] nonce, byte[] tag) WrapDek(byte[] dek, byte[] kek)
    {
        if (dek == null || dek.Length != CompanyFileConstants.KeyLength)
        {
            throw new ArgumentException($"DEK must be {CompanyFileConstants.KeyLength} bytes.", nameof(dek));
        }

        if (kek == null || kek.Length != CompanyFileConstants.KeyLength)
        {
            throw new ArgumentException($"KEK must be {CompanyFileConstants.KeyLength} bytes.", nameof(kek));
        }

        byte[] nonce = GenerateRandomBytes(CompanyFileConstants.NonceLength);
        byte[] tag = new byte[CompanyFileConstants.TagLength];
        byte[] wrappedDek = new byte[dek.Length];

        using (var aesGcm = new AesGcm(kek, CompanyFileConstants.TagLength))
        {
            byte[] associatedData = Encoding.UTF8.GetBytes("DEK_WRAP");
            aesGcm.Encrypt(nonce, dek, wrappedDek, tag, associatedData);
        }

        return (wrappedDek, nonce, tag);
    }

    public byte[] UnwrapDek(byte[] wrappedDek, byte[] nonce, byte[] tag, byte[] kek)
    {
        if (wrappedDek == null || wrappedDek.Length != CompanyFileConstants.KeyLength)
        {
            throw new InvalidPasswordException("Invalid wrapped DEK length.");
        }

        if (kek == null || kek.Length != CompanyFileConstants.KeyLength)
        {
            throw new InvalidPasswordException("Invalid KEK length.");
        }

        byte[] dek = new byte[wrappedDek.Length];

        try
        {
            using var aesGcm = new AesGcm(kek, CompanyFileConstants.TagLength);
            byte[] associatedData = Encoding.UTF8.GetBytes("DEK_WRAP");
            aesGcm.Decrypt(nonce, wrappedDek, tag, dek, associatedData);
            return dek;
        }
        catch (CryptographicException)
        {
            throw new InvalidPasswordException("Incorrect company password. Decryption failed.");
        }
    }

    public (byte[] ciphertext, byte[] nonce, byte[] tag) EncryptPayload(byte[] plaintext, byte[] dek, byte[] associatedData)
    {
        if (plaintext == null)
        {
            throw new ArgumentNullException(nameof(plaintext));
        }

        if (dek == null || dek.Length != CompanyFileConstants.KeyLength)
        {
            throw new ArgumentException($"DEK must be {CompanyFileConstants.KeyLength} bytes.", nameof(dek));
        }

        byte[] nonce = GenerateRandomBytes(CompanyFileConstants.NonceLength);
        byte[] tag = new byte[CompanyFileConstants.TagLength];
        byte[] ciphertext = new byte[plaintext.Length];

        using (var aesGcm = new AesGcm(dek, CompanyFileConstants.TagLength))
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag, associatedData);
        }

        return (ciphertext, nonce, tag);
    }

    public byte[] DecryptPayload(byte[] ciphertext, byte[] nonce, byte[] tag, byte[] dek, byte[] associatedData)
    {
        if (ciphertext == null)
        {
            throw new ArgumentNullException(nameof(ciphertext));
        }

        if (dek == null || dek.Length != CompanyFileConstants.KeyLength)
        {
            throw new ArgumentException($"DEK must be {CompanyFileConstants.KeyLength} bytes.", nameof(dek));
        }

        byte[] plaintext = new byte[ciphertext.Length];

        try
        {
            using var aesGcm = new AesGcm(dek, CompanyFileConstants.TagLength);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);
            return plaintext;
        }
        catch (CryptographicException ex)
        {
            throw new CompanyDataTamperedException("The company data file is invalid or has been modified. Authentication tag verification failed.", ex);
        }
    }
}
