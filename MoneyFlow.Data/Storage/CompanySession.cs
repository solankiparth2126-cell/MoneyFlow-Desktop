using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.Constants;
using MoneyFlow.Data.Encryption;
using MoneyFlow.Data.Transactions;

namespace MoneyFlow.Data.Storage;

/// <summary>
/// Active company session — holds all loaded company state.
/// Lifecycle: Open → Authenticate → Load → Recover → Index → Ready → Operate → Save → Close
/// Only one session per company file at a time (file-locked).
/// </summary>
public sealed class CompanySession : IDisposable
{
    private readonly StorageEngine _storageEngine;
    private readonly KeyManager _keyManager;
    private readonly ILogger<CompanySession>? _logger;

    private FileStream? _lockHandle;
    private bool _disposed;

    // Loaded state
    public CompanyDataStore? DataStore { get; private set; }
    public IndexManager? Indexes { get; private set; }
    public FileHeader? FileHeader { get; private set; }
    public KeyEnvelope? KeyEnvelope { get; private set; }

    public string? CompanyFilePath { get; private set; }
    public string? CompanyId { get; private set; }
    public bool IsOpen => DataStore != null;
    public bool IsDirty { get; private set; }

    // DEK held in memory while session is open — zeroed on close
    private byte[]? _dek;

    public CompanySession(StorageEngine storageEngine, KeyManager keyManager, ILogger<CompanySession>? logger = null)
    {
        _storageEngine = storageEngine;
        _keyManager = keyManager;
        _logger = logger;
    }

    /// <summary>
    /// Creates an in-memory session populated with the given store, suitable for testing and mocks.
    /// </summary>
    public static CompanySession CreateInMemory(CompanyDataStore? store = null)
    {
        var keyManager = new KeyManager();
        var session = new CompanySession(new StorageEngine(), keyManager);
        session.InitializeInMemory(store);
        return session;
    }

    /// <summary>
    /// Initializes this session in-memory with seed data or existing store if not already open.
    /// </summary>
    public void InitializeInMemory(CompanyDataStore? store = null, Core.Entities.Company? company = null, Core.Entities.FinancialYear? fy = null)
    {
        if (IsOpen && DataStore != null) return;

        DataStore = store ?? new CompanyDataStore();
        if (company != null)
        {
            DataStore.CompanyInfo = company;
        }
        else if (DataStore.CompanyInfo == null)
        {
            DataStore.CompanyInfo = new Core.Entities.Company
            {
                CompanyId = 1,
                CompanyName = "MoneyFlow Technologies Pvt Ltd",
                CompanyNumber = "010001",
                State = "Gujarat",
                Currency = "INR",
                IsActive = true,
                CreatedAt = DateTime.Now
            };
        }

        if (fy != null)
        {
            if (!DataStore.FinancialYears.Any(f => f.FinancialYearId == fy.FinancialYearId))
                DataStore.FinancialYears.Add(fy);
        }
        else if (DataStore.FinancialYears.Count == 0)
        {
            DataStore.FinancialYears.Add(new Core.Entities.FinancialYear
            {
                FinancialYearId = 1,
                CompanyId = DataStore.CompanyInfo.CompanyId,
                YearName = "2026-2027",
                StartDate = new DateTime(2026, 4, 1),
                EndDate = new DateTime(2027, 3, 31),
                IsClosed = false,
                CreatedAt = DateTime.Now
            });
        }

        // Seed default predefined groups if empty
        if (DataStore.Groups.Count == 0)
        {
            int gId = 1;
            foreach (var g in PredefinedAccountingGroups.PrimaryGroups)
            {
                DataStore.Groups.Add(new Core.Entities.Group
                {
                    GroupId = gId++,
                    CompanyId = DataStore.CompanyInfo.CompanyId,
                    GroupName = g.Name,
                    Nature = g.Nature,
                    PrimaryGroup = g.PrimaryGroup,
                    AffectProfitLoss = g.AffectProfitLoss,
                    IsPredefined = true,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                });
            }
        }

        Indexes = new IndexManager();
        Indexes.BuildIndexes(DataStore);

        if (string.IsNullOrEmpty(CompanyFilePath))
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"moneyflow_session_{Guid.NewGuid():N}.data");
            CompanyFilePath = tempFile;
        }

        CompanyId = DataStore.CompanyInfo?.CompanyId.ToString() ?? "1";
        FileHeader = new FileHeader { CompanyId = CompanyId, RevisionNumber = 1 };

        var (env, dek) = _keyManager.CreatePasswordlessEnvelope();
        KeyEnvelope = env;
        _dek = dek;
        IsDirty = false;
    }

    /// <summary>
    /// Binds the active session to a target company file path and company ID.
    /// </summary>
    public void SetTargetFilePath(string filePath, string companyId)
    {
        CompanyFilePath = filePath;
        CompanyId = companyId;
        if (FileHeader != null)
        {
            FileHeader.CompanyId = companyId;
        }
        else
        {
            FileHeader = new FileHeader { CompanyId = companyId, RevisionNumber = 0 };
        }
        IsDirty = true;
    }

    /// <summary>
    /// Opens a company file, authenticating with a password if required.
    /// For passwordless companies, pass null.
    /// </summary>
    public void Open(string filePath, string? password)
    {
        if (IsOpen)
        {
            Close();
        }

        if (!File.Exists(filePath))
            throw new CompanyFileException($"Company file not found: {filePath}");

        // 1. Acquire file lock
        AcquireLock(filePath);

        try
        {
            // 2. Run crash recovery
            var recovery = new TransactionRecovery(_logger as ILogger<TransactionRecovery>);
            var recovered = recovery.RecoverIfNeeded(filePath);
            if (recovered)
                _logger?.LogInformation("Transaction recovery completed for {Path}.", filePath);

            // 3. Read headers to check version and security mode
            var (fileHeader, securityHeader) = _storageEngine.ReadHeaders(filePath);

            // 4. Load and decrypt
            var (data, header, dek) = _storageEngine.LoadCompany(filePath, password);

            // 5. Store session state
            CompanyFilePath = filePath;
            CompanyId = header.CompanyId;
            FileHeader = header;
            KeyEnvelope = securityHeader.ToKeyEnvelope();
            DataStore = data;
            _dek = dek;
            IsDirty = false;

            // 6. Build indexes
            Indexes = new IndexManager();
            Indexes.BuildIndexes(DataStore);

            _logger?.LogInformation("Company opened: {CompanyId} from {Path} (revision {Rev}).",
                CompanyId, filePath, header.RevisionNumber);
        }
        catch
        {
            ReleaseLock();
            throw;
        }
    }

    /// <summary>
    /// Persists all changes to company.data via atomic write.
    /// </summary>
    public void Save()
    {
        EnsureOpen();

        if (!IsDirty)
        {
            _logger?.LogDebug("No changes to save for {CompanyId}.", CompanyId);
            return;
        }

        FileHeader ??= new FileHeader { CompanyId = CompanyId ?? "1", RevisionNumber = 0 };
        if (KeyEnvelope == null || _dek == null)
        {
            var (env, dek) = _keyManager.CreatePasswordlessEnvelope();
            KeyEnvelope = env;
            _dek = dek;
        }

        var dir = Path.GetDirectoryName(CompanyFilePath!);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var journal = new TransactionJournal(CompanyFilePath!, _logger as ILogger<TransactionJournal>);

        try
        {
            journal.BeginTransaction();
            journal.MarkPrepared();
            journal.MarkCommitting();

            _storageEngine.SaveCompany(CompanyFilePath!, DataStore!, FileHeader, KeyEnvelope, _dek);

            journal.MarkCommitted();
            IsDirty = false;

            _logger?.LogInformation("Company saved: {CompanyId} (revision {Rev}).", CompanyId, FileHeader!.RevisionNumber);
        }
        catch (Exception ex)
        {
            journal.MarkRolledBack();
            _logger?.LogError(ex, "Failed to save company {CompanyId}.", CompanyId);
            throw;
        }
    }

    /// <summary>
    /// Marks the session as dirty (has unsaved changes).
    /// Called by repositories when entities are modified.
    /// </summary>
    public void MarkDirty() => IsDirty = true;

    /// <summary>
    /// Updates the key envelope (e.g., after password change) and saves immediately.
    /// </summary>
    public void UpdateSecurityEnvelope(KeyEnvelope newEnvelope)
    {
        EnsureOpen();
        KeyEnvelope = newEnvelope;
        IsDirty = true;
        Save();
    }

    /// <summary>
    /// Closes the session, releasing all resources.
    /// </summary>
    public void Close()
    {
        if (!IsOpen) return;

        // Zero the DEK from memory
        if (_dek != null)
        {
            CryptographicOperations.ZeroMemory(_dek);
            _dek = null;
        }

        DataStore = null;
        Indexes?.Clear();
        Indexes = null;
        FileHeader = null;
        KeyEnvelope = null;
        CompanyFilePath = null;
        CompanyId = null;
        IsDirty = false;

        ReleaseLock();

        _logger?.LogInformation("Company session closed.");
    }

    /// <summary>
    /// Creates a TransactionContext for use by FileUnitOfWork.
    /// </summary>
    public Transactions.TransactionContext CreateTransaction()
    {
        EnsureOpen();

        var journal = new TransactionJournal(CompanyFilePath!, _logger as ILogger<TransactionJournal>);

        return new Transactions.TransactionContext(
            journal,
            onCommit: () => Save(),
            onRollback: () =>
            {
                // Rollback: reload data from disk to discard in-memory changes
                _logger?.LogInformation("Transaction rolled back for {CompanyId}. Reloading from disk.", CompanyId);
                ReloadFromDisk();
            });
    }

    /// <summary>
    /// Reloads company data from disk, discarding all in-memory changes.
    /// Used by transaction rollback.
    /// </summary>
    private void ReloadFromDisk()
    {
        if (CompanyFilePath == null || _dek == null || !File.Exists(CompanyFilePath)) return;

        // We need the password to reload, but we have the DEK already.
        // Re-read the file and decrypt using the DEK we already hold.
        // For simplicity, we use the full load path — the KeyManager can unwrap
        // using the envelope stored in the file, and we already authenticated.
        // However, we can't pass the password again. Instead, we directly re-read.

        var (fileHeader, secHeader) = _storageEngine.ReadHeaders(CompanyFilePath);
        var envelope = secHeader.ToKeyEnvelope();

        // We still hold the DEK, so we can decrypt directly via StorageEngine internals
        // For now, reconstruct using raw file read
        using var fs = new FileStream(CompanyFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(fs);

        // Skip headers (re-read them)
        var fh = FileHeader.ReadFrom(reader);
        var sh = SecurityHeader.ReadFrom(reader);
        var ph = PayloadHeader.ReadFrom(reader);

        var encryptedPayload = reader.ReadBytes((int)ph.PayloadLength);
        var footer = FileFooter.ReadFrom(reader);

        var aad = fh.ToAadBytes();
        var encryptor = new Encryption.AesGcmEncryptor();

        byte[] compressedData;
        try
        {
            compressedData = encryptor.Decrypt(encryptedPayload, _dek, ph.PayloadNonce, ph.PayloadTag, aad);
        }
        catch (CryptographicException)
        {
            throw new CompanyFileCorruptedException("Failed to reload company data after rollback.");
        }

        var serialized = Serialization.CompressionHelper.Decompress(compressedData);
        var data = Serialization.CompanySerializer.Deserialize(serialized);
        data.RecalculateIdCounters();

        DataStore = data;
        FileHeader = fh;
        KeyEnvelope = envelope;
        IsDirty = false;

        Indexes?.Clear();
        Indexes = new IndexManager();
        Indexes.BuildIndexes(DataStore);
    }

    private void AcquireLock(string filePath)
    {
        try
        {
            _lockHandle = new FileStream(
                filePath + ".lock",
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
        }
        catch (IOException)
        {
            throw new CompanyLockException(filePath);
        }
    }

    private void ReleaseLock()
    {
        if (_lockHandle != null)
        {
            var lockPath = _lockHandle.Name;
            _lockHandle.Dispose();
            _lockHandle = null;
            try { File.Delete(lockPath); } catch { /* Best effort */ }
        }
    }

    private void EnsureOpen()
    {
        if (!IsOpen)
            InitializeInMemory();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Close();
            _disposed = true;
        }
    }
}
