using System;
using System.Collections.Generic;

namespace MoneyFlow.Core.DTOs;

public class BankTransactionItemDto
{
    public int VoucherEntryId { get; set; }
    public int VoucherId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateTime VoucherDate { get; set; }
    public string VoucherTypeName { get; set; } = string.Empty;
    public string Particulars { get; set; } = string.Empty;
    public string InstrumentNumber { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public DateTime? BankDate { get; set; }
    public bool IsReconciled => BankDate.HasValue;
}

public class BankReconciliationReportDto
{
    public int CompanyId { get; set; }
    public int BankLedgerId { get; set; }
    public string BankLedgerName { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal BalanceAsPerCompanyBooks { get; set; }
    public decimal ChequesDepositedNotCleared { get; set; }
    public decimal ChequesIssuedNotPresented { get; set; }
    public decimal AmountsNotReflectedInBank => ChequesIssuedNotPresented - ChequesDepositedNotCleared;
    public decimal BalanceAsPerBank => BalanceAsPerCompanyBooks + AmountsNotReflectedInBank;
    public List<BankTransactionItemDto> Transactions { get; set; } = new();
}

public class BankClearanceUpdateDto
{
    public int VoucherEntryId { get; set; }
    public DateTime? BankDate { get; set; }
    public string InstrumentNumber { get; set; } = string.Empty;
}
