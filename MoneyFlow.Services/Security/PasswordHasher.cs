using System;
using System.Security.Cryptography;

namespace MoneyFlow.Services.Security;

public static class PasswordHasher
{
    private const int SaltSize = 16; // 128 bit
    private const int KeySize = 32;  // 256 bit
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public static (string Hash, string Salt) HashPassword(string password)
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            saltBytes,
            Iterations,
            Algorithm,
            KeySize);

        return (Convert.ToHexString(hashBytes), Convert.ToHexString(saltBytes));
    }

    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(storedSalt))
        {
            return false;
        }

        try
        {
            byte[] saltBytes = Convert.FromHexString(storedSalt);
            byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(
                password,
                saltBytes,
                Iterations,
                Algorithm,
                KeySize);

            string computedHash = Convert.ToHexString(hashBytes);
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(computedHash),
                Convert.FromHexString(storedHash));
        }
        catch
        {
            return false;
        }
    }
}
