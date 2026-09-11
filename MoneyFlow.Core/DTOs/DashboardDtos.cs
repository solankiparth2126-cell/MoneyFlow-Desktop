using System;
using System.Collections.Generic;
using MoneyFlow.Core.Enums;

namespace MoneyFlow.Core.DTOs;

public class DashboardDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public DateTime AsOfDate { get; set; }
    public string FinancialYearLabel { get; set; } = string.Empty;

    // Liquidity KPIs
    public decimal CashBalance { get; set; }
    public decimal BankBalance { get; set; }
    public decimal TotalLiquidFunds => CashBalance + BankBalance;

    // Receivables & Payables KPIs
    public decimal TotalReceivables { get; set; }
    public decimal TotalPayables { get; set; }
    public decimal NetWorkingCapital => TotalLiquidFunds + TotalReceivables - TotalPayables;

    // Profitability KPIs
    public decimal TotalSales { get; set; }
    public decimal TotalPurchases { get; set; }
    public decimal TotalDirectExpenses { get; set; }
    public decimal TotalIndirectExpenses { get; set; }
    public decimal TotalIndirectIncomes { get; set; }
    public decimal GrossProfitOrLoss => TotalSales - TotalPurchases - TotalDirectExpenses;
    public decimal NetProfitOrLoss => GrossProfitOrLoss + TotalIndirectIncomes - TotalIndirectExpenses;

    // Inventory KPI
    public decimal TotalStockValuation { get; set; }
    public int TotalStockItemsCount { get; set; }

    // Breakdown Details
    public List<MonthlyFinancialSummaryDto> MonthlyTrends { get; set; } = new();
    public List<TopPartyOutstandingDto> TopDebtors { get; set; } = new();
    public List<TopPartyOutstandingDto> TopCreditors { get; set; } = new();
    public List<RecentVoucherDto> RecentVouchers { get; set; } = new();
}

public class MonthlyFinancialSummaryDto
{
    public string MonthLabel { get; set; } = string.Empty; // e.g., "Apr 2026"
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal SalesAmount { get; set; }
    public decimal PurchaseAmount { get; set; }
    public decimal InflowsAmount { get; set; }
    public decimal OutflowsAmount { get; set; }
}

public class TopPartyOutstandingDto
{
    public int LedgerId { get; set; }
    public string PartyName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public BalanceType BalanceType { get; set; }
}

public class RecentVoucherDto
{
    public int VoucherId { get; set; }
    public DateTime VoucherDate { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherTypeName { get; set; } = string.Empty;
    public string Particulars { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Narration { get; set; } = string.Empty;
}
