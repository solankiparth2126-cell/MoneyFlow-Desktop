using System;
using System.Collections.Generic;
using MoneyFlow.Core.Enums;

namespace MoneyFlow.Core.Entities;

public class Ledger
{
    public int LedgerId { get; set; }
    public int CompanyId { get; set; }
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
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual Company? Company { get; set; }
    public virtual Group? Group { get; set; }
    public virtual ICollection<VoucherEntry> VoucherEntries { get; set; } = new List<VoucherEntry>();
}
