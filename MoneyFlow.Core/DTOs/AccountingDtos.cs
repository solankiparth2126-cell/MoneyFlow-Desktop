using System;
using System.Collections.Generic;
using MoneyFlow.Core.Enums;

namespace MoneyFlow.Core.DTOs;

public class VoucherEntryDto
{
    public int LedgerId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string Narration { get; set; } = string.Empty;
}

public class VoucherCreateDto
{
    public int FinancialYearId { get; set; }
    public int VoucherTypeId { get; set; }
    public DateTime VoucherDate { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Narration { get; set; } = string.Empty;
    public List<VoucherEntryDto> Entries { get; set; } = new();
}

public class VoucherValidationResult
{
    public bool IsValid { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal Difference => Math.Abs(TotalDebit - TotalCredit);
    public List<string> Errors { get; set; } = new();

    public static VoucherValidationResult Success(decimal debit, decimal credit) =>
        new()
        {
            IsValid = true,
            TotalDebit = debit,
            TotalCredit = credit
        };

    public static VoucherValidationResult Failure(decimal debit, decimal credit, params string[] errors)
    {
        var result = new VoucherValidationResult
        {
            IsValid = false,
            TotalDebit = debit,
            TotalCredit = credit
        };
        result.Errors.AddRange(errors);
        return result;
    }
}

public class LedgerBalanceDto
{
    public int LedgerId { get; set; }
    public string LedgerName { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public BalanceType OpeningType { get; set; } = BalanceType.Debit;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
    public BalanceType ClosingType { get; set; } = BalanceType.Debit;

    public string FormattedClosingBalance =>
        ClosingBalance == 0
            ? "₹0.00"
            : $"₹{ClosingBalance:N2} {(ClosingType == BalanceType.Debit ? "Dr" : "Cr")}";

    public string ClosingBalanceDisplay => FormattedClosingBalance;
    public BalanceType ClosingBalanceType => ClosingType;
}

public class LedgerStatementLineDto
{
    public int VoucherId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherTypeName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Particulars { get; set; } = string.Empty;
    public string Narration { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
    public BalanceType RunningType { get; set; } = BalanceType.Debit;
}

public class LedgerStatementDto
{
    public int LedgerId { get; set; }
    public string LedgerName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public BalanceType OpeningType { get; set; } = BalanceType.Debit;
    public List<LedgerStatementLineDto> Lines { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
    public BalanceType ClosingType { get; set; } = BalanceType.Debit;

    public string FormattedClosingBalance =>
        ClosingBalance == 0
            ? "₹0.00"
            : $"₹{ClosingBalance:N2} {(ClosingType == BalanceType.Debit ? "Dr" : "Cr")}";
}

public class TrialBalanceItemDto
{
    public int LedgerId { get; set; }
    public string LedgerName { get; set; } = string.Empty;
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public GroupNature GroupNature { get; set; }
    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal PeriodDebit { get; set; }
    public decimal PeriodCredit { get; set; }
    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }
}

public class TrialBalanceDto
{
    public int CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<TrialBalanceItemDto> Items { get; set; } = new();
    public decimal TotalOpeningDebit { get; set; }
    public decimal TotalOpeningCredit { get; set; }
    public decimal TotalPeriodDebit { get; set; }
    public decimal TotalPeriodCredit { get; set; }
    public decimal TotalClosingDebit { get; set; }
    public decimal TotalClosingCredit { get; set; }
    public bool IsBalanced => TotalClosingDebit == TotalClosingCredit;
    public decimal Difference => Math.Abs(TotalClosingDebit - TotalClosingCredit);
}

public class DayBookItemDto
{
    public int VoucherId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public VoucherTypeEnum VoucherType { get; set; }
    public string VoucherTypeName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Particulars { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string Narration { get; set; } = string.Empty;
}

public class DayBookReportDto
{
    public int CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public VoucherTypeEnum? FilterVoucherType { get; set; }
    public List<DayBookItemDto> Items { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public int TotalTransactions => Items.Count;
    public bool IsBalanced => TotalDebit == TotalCredit;
    public decimal Difference => Math.Abs(TotalDebit - TotalCredit);
}

public class ProfitLossLineDto
{
    public int LedgerId { get; set; }
    public string LedgerName { get; set; } = string.Empty;
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class ProfitLossCategoryDto
{
    public string CategoryName { get; set; } = string.Empty;
    public bool IsExpense { get; set; }
    public bool IsTrading { get; set; }
    public List<ProfitLossLineDto> Lines { get; set; } = new();
    public decimal TotalAmount => Lines.Sum(l => l.Amount);
}

public class ProfitLossStatementDto
{
    public int CompanyId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    // Trading Account (Direct)
    public List<ProfitLossCategoryDto> TradingRevenues { get; set; } = new();
    public List<ProfitLossCategoryDto> TradingExpenses { get; set; } = new();
    public decimal TotalTradingRevenue => TradingRevenues.Sum(c => c.TotalAmount);
    public decimal TotalTradingExpense => TradingExpenses.Sum(c => c.TotalAmount);
    public decimal GrossProfit => Math.Max(0, TotalTradingRevenue - TotalTradingExpense);
    public decimal GrossLoss => Math.Max(0, TotalTradingExpense - TotalTradingRevenue);
    public bool HasGrossProfit => TotalTradingRevenue >= TotalTradingExpense;

    // Profit & Loss Account (Indirect)
    public List<ProfitLossCategoryDto> IndirectIncomes { get; set; } = new();
    public List<ProfitLossCategoryDto> IndirectExpenses { get; set; } = new();
    public decimal TotalIndirectIncome => IndirectIncomes.Sum(c => c.TotalAmount);
    public decimal TotalIndirectExpense => IndirectExpenses.Sum(c => c.TotalAmount);

    public decimal TotalIncomeSide => (HasGrossProfit ? GrossProfit : 0m) + TotalIndirectIncome;
    public decimal TotalExpenseSide => (!HasGrossProfit ? GrossLoss : 0m) + TotalIndirectExpense;

    public decimal NetProfit => Math.Max(0, TotalIncomeSide - TotalExpenseSide);
    public decimal NetLoss => Math.Max(0, TotalExpenseSide - TotalIncomeSide);
    public bool HasNetProfit => TotalIncomeSide >= TotalExpenseSide;

    // Balanced Grand Totals
    public decimal GrandTradingTotal => Math.Max(TotalTradingRevenue, TotalTradingExpense);
    public decimal GrandPLTotal => Math.Max(TotalIncomeSide, TotalExpenseSide);
}

public class BalanceSheetLineDto
{
    public int LedgerId { get; set; }
    public string LedgerName { get; set; } = string.Empty;
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public BalanceType BalanceType { get; set; }
}

public class BalanceSheetGroupDto
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public GroupNature Nature { get; set; }
    public List<BalanceSheetLineDto> Lines { get; set; } = new();
    public decimal TotalAmount => Lines.Sum(l => l.Amount);
}

public class BalanceSheetDto
{
    public int CompanyId { get; set; }
    public DateTime AsOfDate { get; set; }

    // Capital & Liabilities
    public List<BalanceSheetGroupDto> Liabilities { get; set; } = new();
    public decimal TotalGroupLiabilities => Liabilities.Sum(g => g.TotalAmount);

    // Current Period Profit / Loss
    public decimal NetProfit { get; set; }
    public decimal NetLoss { get; set; }
    public bool HasNetProfit => NetProfit >= NetLoss;

    // Assets
    public List<BalanceSheetGroupDto> Assets { get; set; } = new();
    public decimal TotalGroupAssets => Assets.Sum(g => g.TotalAmount);

    // Side Totals (Net Profit on Liabilities side, Net Loss on Assets side)
    public decimal TotalLiabilitiesSide => TotalGroupLiabilities + (HasNetProfit ? NetProfit : 0m);
    public decimal TotalAssetsSide => TotalGroupAssets + (!HasNetProfit ? NetLoss : 0m);

    public decimal Difference => Math.Abs(TotalLiabilitiesSide - TotalAssetsSide);
    public bool IsBalanced => Math.Round(TotalLiabilitiesSide, 2) == Math.Round(TotalAssetsSide, 2);
}

public class AgingBucketsDto
{
    public decimal Days0To30 { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Days61To90 { get; set; }
    public decimal DaysOver90 { get; set; }
    public decimal Total => Days0To30 + Days31To60 + Days61To90 + DaysOver90;
}

public class OutstandingPartyDto
{
    public int LedgerId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public decimal TotalOutstanding { get; set; }
    public BalanceType BalanceType { get; set; }
    public AgingBucketsDto Aging { get; set; } = new();
}

public class OutstandingReportDto
{
    public int CompanyId { get; set; }
    public DateTime AsOfDate { get; set; }
    public bool IsReceivables { get; set; } // true = Receivables (Sundry Debtors), false = Payables (Sundry Creditors)
    public List<OutstandingPartyDto> Parties { get; set; } = new();

    public decimal TotalOutstandingAmount => Parties.Sum(p => p.TotalOutstanding);
    public decimal TotalDays0To30 => Parties.Sum(p => p.Aging.Days0To30);
    public decimal TotalDays31To60 => Parties.Sum(p => p.Aging.Days31To60);
    public decimal TotalDays61To90 => Parties.Sum(p => p.Aging.Days61To90);
    public decimal TotalDaysOver90 => Parties.Sum(p => p.Aging.DaysOver90);
}

public enum CashBankBookType
{
    CashBook,
    BankBook
}

public class CashBankBookLineDto
{
    public int VoucherId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherTypeName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Particulars { get; set; } = string.Empty;
    public string Narration { get; set; } = string.Empty;
    public decimal Debit { get; set; } // Receipts / Deposits
    public decimal Credit { get; set; } // Payments / Withdrawals
    public decimal RunningBalance { get; set; }
    public BalanceType RunningType { get; set; } = BalanceType.Debit;
    public string AccountName { get; set; } = string.Empty;
}

public class CashBankAccountSummaryDto
{
    public int LedgerId { get; set; }
    public string LedgerName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public BalanceType OpeningType { get; set; } = BalanceType.Debit;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
    public BalanceType ClosingType { get; set; } = BalanceType.Debit;

    public string FormattedClosingBalance =>
        ClosingBalance == 0
            ? "₹0.00"
            : $"₹{ClosingBalance:N2} {(ClosingType == BalanceType.Debit ? "Dr" : "Cr")}";
}

public class CashBankBookReportDto
{
    public int CompanyId { get; set; }
    public CashBankBookType BookType { get; set; }
    public int? SelectedLedgerId { get; set; }
    public string SelectedLedgerName { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public BalanceType OpeningType { get; set; } = BalanceType.Debit;
    public List<CashBankBookLineDto> Lines { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
    public BalanceType ClosingType { get; set; } = BalanceType.Debit;
    public List<CashBankAccountSummaryDto> AccountSummaries { get; set; } = new();

    public string FormattedOpeningBalance =>
        OpeningBalance == 0
            ? "₹0.00"
            : $"₹{OpeningBalance:N2} {(OpeningType == BalanceType.Debit ? "Dr" : "Cr")}";

    public string FormattedClosingBalance =>
        ClosingBalance == 0
            ? "₹0.00"
            : $"₹{ClosingBalance:N2} {(ClosingType == BalanceType.Debit ? "Dr" : "Cr")}";
}
