using System;
using MoneyFlow.Core.Enums;

namespace MoneyFlow.Core.DTOs;

public class LedgerCreateDto
{
    public int GroupId { get; set; }
    public string LedgerName { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public BalanceType OpeningBalanceType { get; set; } = BalanceType.Debit;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PAN { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public int CreditDays { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string IFSC { get; set; } = string.Empty;
}

public class LedgerUpdateDto
{
    public int LedgerId { get; set; }
    public int GroupId { get; set; }
    public string LedgerName { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public BalanceType OpeningBalanceType { get; set; } = BalanceType.Debit;
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PAN { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public int CreditDays { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string IFSC { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public class LedgerSummaryDto
{
    public int LedgerId { get; set; }
    public int GroupId { get; set; }
    public string LedgerName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public GroupNature GroupNature { get; set; }
    public decimal OpeningBalance { get; set; }
    public BalanceType OpeningBalanceType { get; set; }
    public string FormattedOpeningBalance =>
        OpeningBalance == 0
            ? "₹0.00"
            : $"₹{OpeningBalance:N2} {(OpeningBalanceType == BalanceType.Debit ? "Dr" : "Cr")}";
    public bool IsActive { get; set; }
}

public class LedgerDetailDto
{
    public int LedgerId { get; set; }
    public int CompanyId { get; set; }
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public GroupNature GroupNature { get; set; }
    public string LedgerName { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public BalanceType OpeningBalanceType { get; set; }
    public string FormattedOpeningBalance =>
        OpeningBalance == 0
            ? "₹0.00"
            : $"₹{OpeningBalance:N2} {(OpeningBalanceType == BalanceType.Debit ? "Dr" : "Cr")}";
    public string Address { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PAN { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public decimal CreditLimit { get; set; }
    public int CreditDays { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string IFSC { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
