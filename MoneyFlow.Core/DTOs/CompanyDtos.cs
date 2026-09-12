using System;

namespace MoneyFlow.Core.DTOs;

public class CompanyCreateDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = "India";
    public string PAN { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime FinancialYearFrom { get; set; } = new DateTime(2026, 4, 1);
    public DateTime BooksBeginningFrom { get; set; } = new DateTime(2026, 4, 1);
    public string Currency { get; set; } = "₹";
    public string CompanyNumber { get; set; } = string.Empty;
    public string DataDirectory { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? VaultPassword { get => Password; set => Password = value; }
    public string? ConfirmPassword { get; set; }
    public bool AutoBackupOnExit { get; set; } = true;
    public bool CreateDefaultLedgers { get; set; } = true;
}

public class CompanyUpdateDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = "India";
    public string PAN { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Currency { get; set; } = "₹";
    public string CompanyNumber { get; set; } = string.Empty;
    public string DataDirectory { get; set; } = string.Empty;
    public string? NewPassword { get; set; }
    public string? NewVaultPassword { get => NewPassword; set => NewPassword = value; }
    public bool RemovePassword { get; set; } = false;
    public bool AutoBackupOnExit { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class CompanySummaryDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime FinancialYearFrom { get; set; }
    public DateTime BooksBeginningFrom { get; set; }
    public string Currency { get; set; } = "₹";
    public string CompanyNumber { get; set; } = string.Empty;
    public string DataDirectory { get; set; } = string.Empty;
    public bool IsPasswordProtected { get; set; }
    public bool AutoBackupOnExit { get; set; } = true;
    public string PeriodDisplay => $"{FinancialYearFrom:d-MMM-yy} to {FinancialYearFrom.AddYears(1).AddDays(-1):d-MMM-yy}";
    public bool IsActive { get; set; }
}

public class CompanySplitDto
{
    public int SourceCompanyId { get; set; }
    public DateTime SplitFromDate { get; set; } = new DateTime(2027, 4, 1);
    public string NewCompanyName { get; set; } = string.Empty;
    public string NewCompanyNumber { get; set; } = string.Empty;
    public string TargetDataDirectory { get; set; } = string.Empty;
    public bool AutoOpenAfterSplit { get; set; } = true;
    public bool CarryForwardPendingBills { get; set; } = true;
    public bool CarryForwardOpeningBalances { get; set; } = true;
}
