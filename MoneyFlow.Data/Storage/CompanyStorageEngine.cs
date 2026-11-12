using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Exceptions;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Data.Storage;

public class CompanyStorageEngine : ICompanyStorageEngine
{
    private readonly string _rootPath;
    private readonly ICompanyDataCrypto _crypto;
    private readonly ConcurrentDictionary<string, FileStream> _activeLocks = new(StringComparer.OrdinalIgnoreCase);

    public CompanyStorageEngine(ICompanyDataCrypto crypto, string? customRootPath = null)
    {
        _crypto = crypto;
        _rootPath = string.IsNullOrWhiteSpace(customRootPath)
            ? MoneyFlow.Core.Common.CompanyPathConfiguration.ResolveDataRootPath()
            : customRootPath;

        EnsureSystemDirectories();
    }

    private void EnsureSystemDirectories()
    {
        try
        {
            Directory.CreateDirectory(_rootPath);
            Directory.CreateDirectory(GetCompaniesRootDirectory());
            Directory.CreateDirectory(GetSystemDirectory());
            Directory.CreateDirectory(GetBackupsDirectory());
            Directory.CreateDirectory(Path.Combine(_rootPath, "Logs"));
        }
        catch
        {
            // Silently allow directory creation failures if running in restricted environments
        }
    }

    public string GetDataRootDirectory() => _rootPath;

    public string GetCompaniesRootDirectory()
    {
        return MoneyFlow.Core.Common.CompanyPathConfiguration.ResolveCompaniesPath(_rootPath);
    }

    public string GetSystemDirectory() => Path.Combine(_rootPath, "System");

    public string GetBackupsDirectory(string? companyId = null)
    {
        string baseDir = Path.Combine(_rootPath, "Backups");
        return string.IsNullOrWhiteSpace(companyId) ? baseDir : Path.Combine(baseDir, companyId);
    }

    public string GetCompanyDirectory(string companyId)
    {
        if (string.IsNullOrWhiteSpace(companyId))
        {
            throw new ArgumentException("Company ID cannot be null or empty.", nameof(companyId));
        }

        string baseCompaniesDir = GetCompaniesRootDirectory();
        var searchDirs = new List<string> { baseCompaniesDir };
        string legacyCompaniesDir = Path.Combine(_rootPath, "Companies");
        if (!legacyCompaniesDir.Equals(baseCompaniesDir, StringComparison.OrdinalIgnoreCase) && Directory.Exists(legacyCompaniesDir))
        {
            searchDirs.Add(legacyCompaniesDir);
        }

        foreach (var dirToCheck in searchDirs)
        {
            if (!Directory.Exists(dirToCheck)) continue;

            // 1. Exact directory match
            string exactDir = Path.Combine(dirToCheck, companyId);
            if (Directory.Exists(exactDir))
            {
                return exactDir;
            }

            try
            {
                // 2. Direct prefix match: e.g. "COMP-1002_*"
                var match = Directory.GetDirectories(dirToCheck, $"{companyId}_*").FirstOrDefault();
                if (match != null)
                {
                    return match;
                }

                // 3. Normalized / stripped numeric prefix match: e.g. "010002_*" or "1002_*"
                string cleanNumber = companyId.Replace("COMP-", "", StringComparison.OrdinalIgnoreCase).Trim();
                if (!string.IsNullOrEmpty(cleanNumber))
                {
                    var cleanMatch = Directory.GetDirectories(dirToCheck, $"{cleanNumber}_*").FirstOrDefault();
                    if (cleanMatch != null) return cleanMatch;

                    if (int.TryParse(cleanNumber, out int n))
                    {
                        string padded6 = $"{n:D6}";
                        var paddedMatch = Directory.GetDirectories(dirToCheck, $"{padded6}_*").FirstOrDefault();
                        if (paddedMatch != null) return paddedMatch;
                    }
                }

                // 4. Scan existing directories for matching folder name containing companyId
                var allDirs = Directory.GetDirectories(dirToCheck);
                foreach (var dir in allDirs)
                {
                    string dirName = Path.GetFileName(dir);
                    if (dirName.Equals(companyId, StringComparison.OrdinalIgnoreCase) ||
                        dirName.StartsWith(companyId + "_", StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(cleanNumber) && dirName.StartsWith(cleanNumber + "_", StringComparison.OrdinalIgnoreCase)))
                    {
                        return dir;
                    }
                }
            }
            catch { }
        }

        return Path.Combine(baseCompaniesDir, companyId);
    }

    public string GetCompanyFilePath(string companyId)
    {
        return Path.Combine(GetCompanyDirectory(companyId), "company.data");
    }

    public bool IsCompanyLocked(string companyId)
    {
        if (_activeLocks.ContainsKey(companyId)) return true;

        string lockPath = Path.Combine(GetCompanyDirectory(companyId), ".lock");
        if (!File.Exists(lockPath)) return false;

        try
        {
            using var fs = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    public IDisposable AcquireLock(string companyId)
    {
        string dir = GetCompanyDirectory(companyId);
        Directory.CreateDirectory(dir);
        string lockPath = Path.Combine(dir, ".lock");

        try
        {
            var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
            if (!_activeLocks.TryAdd(companyId, stream))
            {
                stream.Dispose();
                throw new CompanyLockedException(companyId);
            }

            return new LockReleaser(() =>
            {
                if (_activeLocks.TryRemove(companyId, out var s))
                {
                    s.Dispose();
                }
            });
        }
        catch (IOException)
        {
            throw new CompanyLockedException(companyId);
        }
    }

    private class LockReleaser : IDisposable
    {
        private Action? _releaseAction;
        public LockReleaser(Action releaseAction) => _releaseAction = releaseAction;
        public void Dispose()
        {
            Interlocked.Exchange(ref _releaseAction, null)?.Invoke();
        }
    }

    public async Task<CompanyFileHeader> ReadHeaderAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Company file not found at: {filePath}", filePath);
        }

        byte[] bytes = await File.ReadAllBytesAsync(filePath, ct);
        return ParseHeader(bytes);
    }

    private static CompanyFileHeader ParseHeader(byte[] bytes)
    {
        if (bytes.Length < 32)
        {
            throw new InvalidCompanyFileException("File is too small to be a valid company.data file.");
        }

        using var ms = new MemoryStream(bytes);
        using var reader = new BinaryReader(ms, Encoding.UTF8);

        string magic = Encoding.ASCII.GetString(reader.ReadBytes(8));
        if (magic != CompanyFileConstants.Magic)
        {
            throw new InvalidCompanyFileException($"Invalid magic identifier: '{magic}'. Expected '{CompanyFileConstants.Magic}'.");
        }

        int fileFormatVersion = reader.ReadInt32();
        if (fileFormatVersion > CompanyFileConstants.CurrentFileFormatVersion)
        {
            throw new UnsupportedFileFormatException(fileFormatVersion, CompanyFileConstants.CurrentFileFormatVersion);
        }

        int encryptionVersion = reader.ReadInt32();
        if (encryptionVersion != CompanyFileConstants.CurrentEncryptionVersion)
        {
            throw new UnsupportedEncryptionVersionException(encryptionVersion);
        }

        int dataVersion = reader.ReadInt32();
        string companyId = reader.ReadString();
        long createdAtTicks = reader.ReadInt64();
        string metaJson = reader.ReadString();

        var meta = string.IsNullOrWhiteSpace(metaJson)
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : JsonSerializer.Deserialize<Dictionary<string, string>>(metaJson) ?? new();

        return new CompanyFileHeader
        {
            Magic = magic,
            FileFormatVersion = fileFormatVersion,
            EncryptionVersion = encryptionVersion,
            DataVersion = dataVersion,
            CompanyId = companyId,
            CreatedAtUtc = new DateTime(createdAtTicks, DateTimeKind.Utc),
            HeaderMetadata = meta
        };
    }

    private static byte[] BuildAssociatedData(CompanyFileHeader header)
    {
        string s = $"{header.Magic}|{header.FileFormatVersion}|{header.EncryptionVersion}|{header.DataVersion}|{header.CompanyId}";
        return Encoding.UTF8.GetBytes(s);
    }

    public async Task<(CompanyData Data, byte[] Dek)> LoadCompanyDataAsync(string filePath, string password, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Company file not found at: {filePath}", filePath);
        }

        byte[] fileBytes = await File.ReadAllBytesAsync(filePath, ct);
        var header = ParseHeader(fileBytes);

        if (header.DataVersion > CompanyFileConstants.CurrentDataVersion)
        {
            throw new UnsupportedDataVersionException(header.DataVersion, CompanyFileConstants.CurrentDataVersion);
        }

        using var ms = new MemoryStream(fileBytes);
        using var reader = new BinaryReader(ms, Encoding.UTF8);

        // Skip header fields
        reader.ReadBytes(8); // Magic
        reader.ReadInt32();   // FileFormatVersion
        reader.ReadInt32();   // EncryptionVersion
        reader.ReadInt32();   // DataVersion
        reader.ReadString();  // CompanyId
        reader.ReadInt64();   // CreatedAtTicks
        reader.ReadString();  // MetadataJson

        // Read Security Metadata
        int kdfAlgo = reader.ReadInt32();
        int kdfIterations = reader.ReadInt32();
        byte[] salt = reader.ReadBytes(CompanyFileConstants.SaltLength);
        int wrappedDekLen = reader.ReadInt32();
        byte[] wrappedDek = reader.ReadBytes(wrappedDekLen);
        byte[] wrappedDekNonce = reader.ReadBytes(CompanyFileConstants.NonceLength);
        byte[] wrappedDekTag = reader.ReadBytes(CompanyFileConstants.TagLength);

        // Read Encrypted Payload Metadata
        byte[] payloadNonce = reader.ReadBytes(CompanyFileConstants.NonceLength);
        byte[] payloadTag = reader.ReadBytes(CompanyFileConstants.TagLength);
        int payloadLen = reader.ReadInt32();
        byte[] payloadCiphertext = reader.ReadBytes(payloadLen);

        // 1. Derive KEK from Password
        byte[] kek = _crypto.DeriveKek(password, salt, kdfIterations);

        // 2. Unwrap DEK (throws InvalidPasswordException if tag fails)
        byte[] dek = _crypto.UnwrapDek(wrappedDek, wrappedDekNonce, wrappedDekTag, kek);

        // 3. Decrypt Payload using AES-256-GCM + Header Associated Data (tamper detection!)
        byte[] associatedData = BuildAssociatedData(header);
        byte[] compressedPayload = _crypto.DecryptPayload(payloadCiphertext, payloadNonce, payloadTag, dek, associatedData);

        // 4. Decompress and Deserialize
        var companyData = CompanyDataSerializer.DecompressAndDeserialize(compressedPayload);

        return (companyData, dek);
    }

    public async Task CreateInitialCompanyFileAsync(string filePath, CompanyData initialData, string password, CancellationToken ct = default)
    {
        byte[] dek = _crypto.GenerateDek();
        await SaveCompanyDataAsync(filePath, initialData, dek, password, ct);
    }

    public async Task SaveCompanyDataAsync(string filePath, CompanyData data, byte[] dek, string? updatePassword = null, CancellationToken ct = default)
    {
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string tempPath = filePath + ".tmp";
        string backupPath = filePath + ".bak";

        try
        {
            // 1. Prepare Header
            var header = new CompanyFileHeader
            {
                Magic = CompanyFileConstants.Magic,
                FileFormatVersion = CompanyFileConstants.CurrentFileFormatVersion,
                EncryptionVersion = CompanyFileConstants.CurrentEncryptionVersion,
                DataVersion = CompanyFileConstants.CurrentDataVersion,
                CompanyId = data.Company.CompanyNumber,
                CreatedAtUtc = data.Company.CreatedAt.ToUniversalTime(),
                HeaderMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["CompanyName"] = data.Company.CompanyName,
                    ["FinancialYearFrom"] = data.Company.FinancialYearFrom.ToString("yyyy-MM-dd"),
                    ["BooksBeginningFrom"] = data.Company.BooksBeginningFrom.ToString("yyyy-MM-dd"),
                    ["Currency"] = data.Company.Currency ?? "₹"
                }
            };

            // 2. Prepare Security Metadata
            byte[] salt;
            byte[] kek;
            int iterations = CompanyFileConstants.DefaultKdfIterations;

            // If file already exists and no password change is requested, reuse existing salt
            if (File.Exists(filePath) && string.IsNullOrEmpty(updatePassword))
            {
                byte[] existingBytes = await File.ReadAllBytesAsync(filePath, ct);
                using var msExisting = new MemoryStream(existingBytes);
                using var rExisting = new BinaryReader(msExisting, Encoding.UTF8);

                // Skip header
                rExisting.ReadBytes(8);
                rExisting.ReadInt32();
                rExisting.ReadInt32();
                rExisting.ReadInt32();
                rExisting.ReadString();
                rExisting.ReadInt64();
                rExisting.ReadString();

                rExisting.ReadInt32(); // kdfAlgo
                iterations = rExisting.ReadInt32();
                salt = rExisting.ReadBytes(CompanyFileConstants.SaltLength);
            }
            else
            {
                salt = _crypto.GenerateRandomBytes(CompanyFileConstants.SaltLength);
            }

            // Derive KEK
            string pwd = string.IsNullOrEmpty(updatePassword) ? (data.Company.PasswordHash ?? "MyERP_Default_Secret_2026") : updatePassword;
            kek = _crypto.DeriveKek(pwd, salt, iterations);

            // Wrap DEK
            var (wrappedDek, wrappedDekNonce, wrappedDekTag) = _crypto.WrapDek(dek, kek);

            // 3. Serialize -> Compress
            byte[] compressedBytes = CompanyDataSerializer.SerializeAndCompress(data);

            // 4. Encrypt Payload using AES-256-GCM
            byte[] associatedData = BuildAssociatedData(header);
            var (payloadCiphertext, payloadNonce, payloadTag) = _crypto.EncryptPayload(compressedBytes, dek, associatedData);

            // 5. Write to temporary file
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new BinaryWriter(fs, Encoding.UTF8))
            {
                // Header
                writer.Write(Encoding.ASCII.GetBytes(header.Magic));
                writer.Write(header.FileFormatVersion);
                writer.Write(header.EncryptionVersion);
                writer.Write(header.DataVersion);
                writer.Write(header.CompanyId);
                writer.Write(header.CreatedAtUtc.Ticks);
                writer.Write(JsonSerializer.Serialize(header.HeaderMetadata));

                // Security Metadata
                writer.Write(1); // PBKDF2-HMAC-SHA256
                writer.Write(iterations);
                writer.Write(salt);
                writer.Write(wrappedDek.Length);
                writer.Write(wrappedDek);
                writer.Write(wrappedDekNonce);
                writer.Write(wrappedDekTag);

                // Payload
                writer.Write(payloadNonce);
                writer.Write(payloadTag);
                writer.Write(payloadCiphertext.Length);
                writer.Write(payloadCiphertext);

                writer.Flush();
                fs.Flush(true);
            }

            // 6. Validate temp file format before replacing
            byte[] writtenBytes = await File.ReadAllBytesAsync(tempPath, ct);
            var verifyHeader = ParseHeader(writtenBytes);
            if (verifyHeader.CompanyId != header.CompanyId)
            {
                throw new InvalidCompanyFileException("Temporary file validation failed on written Company ID.");
            }

            // 7. Atomic replace
            if (File.Exists(filePath))
            {
                try
                {
                    File.Replace(tempPath, filePath, backupPath, ignoreMetadataErrors: true);
                }
                catch
                {
                    // Fallback for filesystems that do not support File.Replace
                    if (File.Exists(backupPath)) File.Delete(backupPath);
                    File.Move(filePath, backupPath);
                    File.Move(tempPath, filePath);
                }

                if (File.Exists(backupPath))
                {
                    try { File.Delete(backupPath); } catch { }
                }
            }
            else
            {
                File.Move(tempPath, filePath);
            }
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
            throw;
        }
    }

    public async Task<string> CreateBackupAsync(string companyId, string? customBackupDir = null, string? reason = null, CancellationToken ct = default)
    {
        string sourcePath = GetCompanyFilePath(companyId);
        if (!File.Exists(sourcePath))
        {
            throw new CompanyNotFoundException(companyId);
        }

        string targetDir = string.IsNullOrWhiteSpace(customBackupDir) ? GetBackupsDirectory(companyId) : customBackupDir;
        Directory.CreateDirectory(targetDir);

        string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss");
        string suffix = string.IsNullOrWhiteSpace(reason) ? "manual" : reason;
        string backupFileName = $"{companyId}_{timestamp}_{suffix}.company.bak";
        string backupFilePath = Path.Combine(targetDir, backupFileName);

        // Copy source directly to backup (maintains full encryption at rest)
        File.Copy(sourcePath, backupFilePath, overwrite: true);

        // Verify backup integrity
        byte[] backupBytes = await File.ReadAllBytesAsync(backupFilePath, ct);
        var header = ParseHeader(backupBytes);
        if (header.CompanyId != companyId)
        {
            File.Delete(backupFilePath);
            throw new BackupException("Backup verification failed: Company ID mismatch in backup file.");
        }

        return backupFilePath;
    }

    public async Task RestoreBackupAsync(string backupFilePath, string targetCompanyId, string password, CancellationToken ct = default)
    {
        if (!File.Exists(backupFilePath))
        {
            throw new FileNotFoundException("Backup file not found.", backupFilePath);
        }

        // 1. Verify backup file header & decryption
        var (restoredData, dek) = await LoadCompanyDataAsync(backupFilePath, password, ct);

        // 2. Acquire lock on target company
        using var compLock = AcquireLock(targetCompanyId);

        // 3. Create safety backup of existing company if present
        string targetFilePath = GetCompanyFilePath(targetCompanyId);
        if (File.Exists(targetFilePath))
        {
            await CreateBackupAsync(targetCompanyId, reason: "pre_restore_safety", ct: ct);
        }

        // 4. Overwrite target company file with verified restored data
        await SaveCompanyDataAsync(targetFilePath, restoredData, dek, password, ct);
    }
}
