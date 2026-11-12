using System;
using System.Collections.Generic;

namespace MoneyFlow.Core.Entities;

public static class CompanyFileConstants
{
    public const string Magic = "MYERPDAT";
    public const int CurrentFileFormatVersion = 1;
    public const int CurrentEncryptionVersion = 1; // 1 = AES-256-GCM
    public const int CurrentDataVersion = 6;
    public const int DefaultKdfIterations = 100000;
    public const int SaltLength = 32;
    public const int NonceLength = 12;
    public const int TagLength = 16;
    public const int KeyLength = 32; // 256-bit AES
}

/// <summary>
/// Unencrypted / portable header at the very beginning of the company.data file.
/// Allows rapid scanning of company files across PCs without exposing sensitive business data.
/// </summary>
public class CompanyFileHeader
{
    public string Magic { get; set; } = CompanyFileConstants.Magic;
    public int FileFormatVersion { get; set; } = CompanyFileConstants.CurrentFileFormatVersion;
    public int EncryptionVersion { get; set; } = CompanyFileConstants.CurrentEncryptionVersion;
    public int DataVersion { get; set; } = CompanyFileConstants.CurrentDataVersion;
    public string CompanyId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> HeaderMetadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Cryptographic envelope metadata needed to derive the KEK and unwrap the Company DEK.
/// Portable: does not contain or depend on any machine-specific hardware IDs or local registry secrets.
/// </summary>
public class CompanySecurityMetadata
{
    public int KdfAlgorithm { get; set; } = 1; // 1 = PBKDF2-HMAC-SHA256
    public int KdfIterations { get; set; } = CompanyFileConstants.DefaultKdfIterations;
    public byte[] KdfSalt { get; set; } = Array.Empty<byte>();
    public byte[] WrappedDek { get; set; } = Array.Empty<byte>();
    public byte[] WrappedDekNonce { get; set; } = Array.Empty<byte>();
    public byte[] WrappedDekTag { get; set; } = Array.Empty<byte>();
}

/// <summary>
/// Pure offline, file-persisted company domain dataset.
/// Holds all entities for an isolated company.
/// </summary>
public class CompanyData
{
    public Company Company { get; set; } = new();
    public List<FinancialYear> FinancialYears { get; set; } = new();
    public List<Group> Groups { get; set; } = new();
    public List<Ledger> Ledgers { get; set; } = new();
    public List<VoucherType> VoucherTypes { get; set; } = new();
    public List<Voucher> Vouchers { get; set; } = new();
    public List<VoucherEntry> VoucherEntries { get; set; } = new();
    public List<BillAllocation> BillAllocations { get; set; } = new();
    public List<StockItem> StockItems { get; set; } = new();
    public List<Unit> Units { get; set; } = new();
    public List<User> Users { get; set; } = new();
    public List<Role> Roles { get; set; } = new();
    public List<Permission> Permissions { get; set; } = new();
    public List<AuditLog> AuditLogs { get; set; } = new();
    public List<AppSetting> Settings { get; set; } = new();
    public List<BackupHistory> BackupHistories { get; set; } = new();

    /// <summary>
    /// Extensible key-value store for forward compatibility to avoid data loss during future version upgrades.
    /// </summary>
    public Dictionary<string, string> ExtendedProperties { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public CompanyData Clone()
    {
        // Creates a detached copy for transactional safety / rollback
        var json = System.Text.Json.JsonSerializer.Serialize(this);
        return System.Text.Json.JsonSerializer.Deserialize<CompanyData>(json)!;
    }
}
