using System;
using System.IO;
using System.Security.Cryptography;
using MoneyFlow.Data.Encryption;
using MoneyFlow.Data.Serialization;

namespace MoneyFlow.Data.Storage;

/// <summary>
/// Core storage engine that orchestrates reading/writing company.data files.
/// Pipeline: Domain → MessagePack → Brotli → AES-256-GCM → company.data
/// Reverse: company.data → AES-256-GCM → Brotli → MessagePack → Domain
/// </summary>
public sealed class StorageEngine
{
    private readonly AesGcmEncryptor _encryptor = new();
    private readonly KeyManager _keyManager = new();

    /// <summary>
    /// Reads and validates the file header and security header without decrypting the payload.
    /// Used for company discovery, version checks, and determining security mode.
    /// </summary>
    public (FileHeader FileHeader, SecurityHeader SecurityHeader) ReadHeaders(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(fs);

        var fileHeader = FileHeader.ReadFrom(reader);
        var securityHeader = SecurityHeader.ReadFrom(reader);

        return (fileHeader, securityHeader);
    }

    /// <summary>
    /// Opens and decrypts a company.data file, returning the loaded company data.
    /// For PasswordProtected companies, provide the password.
    /// For Passwordless companies, pass null.
    /// </summary>
    public (CompanyDataStore Data, FileHeader Header, byte[] Dek) LoadCompany(string filePath, string? password)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(fs);

        // 1. Read headers
        var fileHeader = FileHeader.ReadFrom(reader);
        var securityHeader = SecurityHeader.ReadFrom(reader);
        var payloadHeader = PayloadHeader.ReadFrom(reader);

        // 2. Version check (downgrade protection)
        if (fileHeader.DataVersion > CompanyFileFormat.CurrentDataVersion)
            throw new UnsupportedCompanyVersionException(fileHeader.DataVersion, CompanyFileFormat.CurrentDataVersion);

        // 3. Unwrap DEK based on security mode
        var envelope = securityHeader.ToKeyEnvelope();
        byte[] dek;

        if (envelope.Mode == SecurityMode.PasswordProtected)
        {
            if (string.IsNullOrEmpty(password))
                throw new CompanyAuthenticationException("Password is required for this company.");
            dek = _keyManager.UnwrapDekWithPassword(envelope, password);
        }
        else
        {
            dek = _keyManager.UnwrapDekPasswordless(envelope);
        }

        // 4. Read encrypted payload
        var encryptedPayload = reader.ReadBytes((int)payloadHeader.PayloadLength);

        // 5. Validate footer
        var footer = FileFooter.ReadFrom(reader);
        if (footer.Revision != fileHeader.RevisionNumber)
            throw new CompanyFileCorruptedException("File revision mismatch between header and footer.");

        // 6. Construct AAD from file header
        var aad = fileHeader.ToAadBytes();

        // 7. Decrypt payload
        byte[] compressedData;
        try
        {
            compressedData = _encryptor.Decrypt(
                encryptedPayload,
                dek,
                payloadHeader.PayloadNonce,
                payloadHeader.PayloadTag,
                aad);
        }
        catch (CryptographicException)
        {
            CryptographicOperations.ZeroMemory(dek);
            throw new CompanyFileCorruptedException("Payload authentication failed. File may be tampered or corrupted.");
        }

        // 8. Decompress
        var serializedData = CompressionHelper.Decompress(compressedData);

        // 9. Deserialize
        var dataStore = CompanySerializer.Deserialize(serializedData);

        // 10. Recalculate ID counters from loaded data
        dataStore.RecalculateIdCounters();

        return (dataStore, fileHeader, dek);
    }

    /// <summary>
    /// Saves company data to a file using atomic write (tmp → flush → replace).
    /// </summary>
    public void SaveCompany(
        string filePath,
        CompanyDataStore data,
        FileHeader fileHeader,
        KeyEnvelope keyEnvelope,
        byte[] dek)
    {
        // Increment revision
        fileHeader.RevisionNumber++;

        // 1. Serialize → Compress → Encrypt pipeline
        var serializedData = CompanySerializer.Serialize(data);
        var compressedData = CompressionHelper.Compress(serializedData);

        // 2. Construct AAD
        var aad = fileHeader.ToAadBytes();

        // 3. Encrypt payload
        var (encryptedPayload, payloadNonce, payloadTag) = _encryptor.Encrypt(compressedData, dek, aad);

        // 4. Write to temporary file first (atomic write)
        var tmpPath = filePath + ".tmp";

        try
        {
            using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new BinaryWriter(fs))
            {
                // File header
                fileHeader.WriteTo(writer);

                // Security header
                var securityHeader = SecurityHeader.FromKeyEnvelope(keyEnvelope);
                securityHeader.WriteTo(writer);

                // Payload header
                var payloadHeader = new PayloadHeader
                {
                    PayloadNonce = payloadNonce,
                    PayloadLength = encryptedPayload.Length,
                    PayloadTag = payloadTag
                };
                payloadHeader.WriteTo(writer);

                // Encrypted payload
                writer.Write(encryptedPayload);

                // Footer
                var footer = new FileFooter
                {
                    Revision = fileHeader.RevisionNumber,
                    TotalFileLength = 0 // Will be set after flush
                };

                var footerPos = fs.Position;
                footer.WriteTo(writer);
                writer.Flush();
                fs.Flush(true); // Flush to disk

                // Update total file length in footer
                footer.TotalFileLength = fs.Length;
                fs.Position = footerPos;
                footer.WriteTo(writer);
                writer.Flush();
                fs.Flush(true);
            }

            // 5. Atomic replace: tmp → final
            if (File.Exists(filePath))
            {
                File.Replace(tmpPath, filePath, filePath + ".bak");
                // Clean up backup file from Replace
                try { File.Delete(filePath + ".bak"); } catch { /* Best effort */ }
            }
            else
            {
                File.Move(tmpPath, filePath);
            }
        }
        catch
        {
            // Cleanup temp file on failure — original remains intact
            try { File.Delete(tmpPath); } catch { /* Best effort */ }
            throw;
        }
    }

    /// <summary>
    /// Creates a brand new company.data file.
    /// </summary>
    public void CreateCompanyFile(
        string filePath,
        CompanyDataStore data,
        string companyId,
        string? password)
    {
        // Generate key envelope based on security mode
        KeyEnvelope envelope;
        byte[] dek;

        if (!string.IsNullOrEmpty(password))
        {
            (envelope, dek) = _keyManager.CreatePasswordProtectedEnvelope(password);
        }
        else
        {
            (envelope, dek) = _keyManager.CreatePasswordlessEnvelope();
        }

        var fileHeader = new FileHeader
        {
            CompanyId = companyId,
            RevisionNumber = 0
        };

        try
        {
            SaveCompany(filePath, data, fileHeader, envelope, dek);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }
    }

    /// <summary>
    /// Validates that a file is a valid MyERP company.data without fully loading it.
    /// </summary>
    public bool ValidateFile(string filePath, out string? errorMessage)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                errorMessage = "File does not exist.";
                return false;
            }

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (fs.Length < 8)
            {
                errorMessage = "File is too small to be a valid company file.";
                return false;
            }

            using var reader = new BinaryReader(fs);
            var magic = reader.ReadBytes(8);
            if (!magic.AsSpan().SequenceEqual(CompanyFileFormat.FileMagic))
            {
                errorMessage = "Not a valid MyERP company data file.";
                return false;
            }

            errorMessage = null;
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }
}
