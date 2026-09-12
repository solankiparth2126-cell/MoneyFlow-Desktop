using System;
using System.Collections.Generic;

namespace MoneyFlow.Core.DTOs;

public enum BackupType
{
    CompanyArchive = 1, // .mfb (MoneyFlow Backup archive)
    FullDatabase = 2    // .bak (SQL Server database backup)
}

public class BackupManifestDto
{
    public string AppVersion { get; set; } = "1.0.0";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string FinancialYearLabel { get; set; } = string.Empty;
    public int LedgerCount { get; set; }
    public int VoucherCount { get; set; }
    public int StockItemCount { get; set; }
    public string ChecksumSha256 { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

public class BackupFileInfo
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public BackupType BackupType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public bool IsValid { get; set; } = true;
    public string FormattedSize => FileSizeBytes < 1024 * 1024
        ? $"{FileSizeBytes / 1024.0:F1} KB"
        : $"{FileSizeBytes / (1024.0 * 1024.0):F2} MB";
}

public class BackupCreateOptionsDto
{
    public int CompanyId { get; set; }
    public string? TargetDirectory { get; set; }
    public BackupType BackupType { get; set; } = BackupType.CompanyArchive;
    public string Comment { get; set; } = string.Empty;
}

public class RestoreOptionsDto
{
    public string BackupFilePath { get; set; } = string.Empty;
    public bool RestoreAsNewCompany { get; set; } = true;
    public string? NewCompanyName { get; set; }
    public int? TargetCompanyId { get; set; }
}

public class RestoreResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? RestoredCompanyId { get; set; }
    public int RecordsRestoredCount { get; set; }
}

public class CompanyDto
{
    public int CompanyId { get; set; }
    public int Id => CompanyId;
    public string CompanyName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = "India";
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PAN { get; set; } = string.Empty;
    public string Currency { get; set; } = "₹";
    public string CompanyNumber { get; set; } = string.Empty;
    public string DataDirectory { get; set; } = string.Empty;
    public DateTime FinancialYearFrom { get; set; }
    public DateTime FinancialYearTo => FinancialYearFrom.AddYears(1).AddDays(-1);
    public DateTime BooksBeginningFrom { get; set; }
    public bool IsPasswordProtected { get; set; }
    public bool AutoBackupOnExit { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class FinancialYearDto
{
    public int FinancialYearId { get; set; }
    public int CompanyId { get; set; }
    public string YearName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
}

public class GroupDto
{
    public int GroupId { get; set; }
    public int CompanyId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int? ParentGroupId { get; set; }
    public MoneyFlow.Core.Enums.GroupNature Nature { get; set; }
    public bool PrimaryGroup { get; set; }
    public bool AffectProfitLoss { get; set; }
    public bool IsActive { get; set; }
}

public class VoucherDto
{
    public int VoucherId { get; set; }
    public int CompanyId { get; set; }
    public int VoucherTypeId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateTime VoucherDate { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Narration { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public List<VoucherEntryDto> VoucherEntries { get; set; } = new();
}

public class CompanyBackupPayloadDto
{
    public BackupManifestDto Manifest { get; set; } = new();
    public CompanyDto Company { get; set; } = new();
    public List<FinancialYearDto> FinancialYears { get; set; } = new();
    public List<GroupDto> Groups { get; set; } = new();
    public List<LedgerSummaryDto> Ledgers { get; set; } = new();
    public List<UnitDto> Units { get; set; } = new();
    public List<StockItemDto> StockItems { get; set; } = new();
    public List<VoucherDto> Vouchers { get; set; } = new();
}
