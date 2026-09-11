using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;

namespace MoneyFlow.Services.Dashboard;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        AppDbContext context,
        ILogger<DashboardService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DashboardDto> GetDashboardDataAsync(int companyId, DateTime asOfDate, CancellationToken ct = default)
    {
        var company = await _context.Companies
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CompanyId == companyId, ct);

        if (company == null)
        {
            throw new InvalidOperationException($"Company with ID {companyId} not found.");
        }

        // 1. Resolve Financial Year for asOfDate
        var fy = await _context.FinancialYears
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.CompanyId == companyId && f.StartDate <= asOfDate && f.EndDate >= asOfDate, ct)
            ?? await _context.FinancialYears
                .AsNoTracking()
                .Where(f => f.CompanyId == companyId)
                .OrderByDescending(f => f.StartDate)
                .FirstOrDefaultAsync(ct);

        DateTime fyStart = fy?.StartDate ?? new DateTime(asOfDate.Year, 4, 1);
        DateTime fyEnd = fy?.EndDate ?? new DateTime(asOfDate.Year + 1, 3, 31);
        string fyLabel = fy != null ? $"{fy.StartDate:yyyy}-{fy.EndDate:yy}" : $"{fyStart:yyyy}-{fyEnd:yy}";

        var dashboard = new DashboardDto
        {
            CompanyId = companyId,
            CompanyName = company.CompanyName,
            AsOfDate = asOfDate,
            FinancialYearLabel = fyLabel
        };

        // 2. Fetch all active groups to build hierarchy
        var allGroups = await _context.Groups
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId && g.IsActive)
            .ToListAsync(ct);

        List<int> GetGroupAndDescendantIds(string groupName)
        {
            var matched = allGroups.Where(g => g.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase)).Select(g => g.GroupId).ToList();
            var result = new HashSet<int>(matched);
            bool added;
            do
            {
                added = false;
                var children = allGroups.Where(g => g.ParentGroupId.HasValue && result.Contains(g.ParentGroupId.Value) && !result.Contains(g.GroupId)).Select(g => g.GroupId).ToList();
                if (children.Count > 0)
                {
                    foreach (var c in children) result.Add(c);
                    added = true;
                }
            } while (added);

            return result.ToList();
        }

        var cashGroupIds = GetGroupAndDescendantIds("Cash-in-Hand");
        var bankGroupIds = GetGroupAndDescendantIds("Bank Accounts")
            .Concat(GetGroupAndDescendantIds("Bank OD A/c"))
            .Concat(GetGroupAndDescendantIds("Bank OCC A/c"))
            .Distinct()
            .ToList();

        var debtorsGroupIds = GetGroupAndDescendantIds("Sundry Debtors");
        var creditorsGroupIds = GetGroupAndDescendantIds("Sundry Creditors");
        var salesGroupIds = GetGroupAndDescendantIds("Sales Accounts");
        var purchaseGroupIds = GetGroupAndDescendantIds("Purchase Accounts");
        var directExpGroupIds = GetGroupAndDescendantIds("Direct Expenses");
        var indirectExpGroupIds = GetGroupAndDescendantIds("Indirect Expenses");
        var directIncGroupIds = GetGroupAndDescendantIds("Direct Incomes");
        var indirectIncGroupIds = GetGroupAndDescendantIds("Indirect Incomes");

        // 3. Query all active ledgers with their opening balances
        var ledgers = await _context.Ledgers
            .AsNoTracking()
            .Include(l => l.Group)
            .Where(l => l.CompanyId == companyId && l.IsActive)
            .ToListAsync(ct);

        var ledgerIds = ledgers.Select(l => l.LedgerId).ToList();

        // 4. Query all voucher entries up to asOfDate
        var entriesUpToAsOfDate = await _context.VoucherEntries
            .AsNoTracking()
            .Where(e => ledgerIds.Contains(e.LedgerId) && !e.Voucher!.IsDeleted && e.Voucher.VoucherDate <= asOfDate)
            .Select(e => new
            {
                e.LedgerId,
                e.Voucher!.VoucherDate,
                e.Debit,
                e.Credit,
                VoucherTypeId = e.Voucher.VoucherTypeId
            })
            .ToListAsync(ct);

        var entriesByLedger = entriesUpToAsOfDate
            .GroupBy(e => e.LedgerId)
            .ToDictionary(g => g.Key, g => new { TotalDebit = g.Sum(x => x.Debit), TotalCredit = g.Sum(x => x.Credit) });

        // Calculate closing balance for each ledger as of asOfDate
        decimal ComputeClosingBalance(Core.Entities.Ledger l, out BalanceType outType)
        {
            decimal opDr = l.OpeningBalanceType == BalanceType.Debit ? l.OpeningBalance : 0m;
            decimal opCr = l.OpeningBalanceType == BalanceType.Credit ? l.OpeningBalance : 0m;

            decimal txDr = 0m;
            decimal txCr = 0m;
            if (entriesByLedger.TryGetValue(l.LedgerId, out var tx))
            {
                txDr = tx.TotalDebit;
                txCr = tx.TotalCredit;
            }

            decimal totalDr = opDr + txDr;
            decimal totalCr = opCr + txCr;

            if (totalDr >= totalCr)
            {
                outType = BalanceType.Debit;
                return totalDr - totalCr;
            }
            else
            {
                outType = BalanceType.Credit;
                return totalCr - totalDr;
            }
        }

        // A. Cash & Bank Balances
        decimal cashBal = 0m;
        foreach (var l in ledgers.Where(l => cashGroupIds.Contains(l.GroupId)))
        {
            decimal bal = ComputeClosingBalance(l, out var bType);
            cashBal += (bType == BalanceType.Debit ? bal : -bal);
        }
        dashboard.CashBalance = cashBal;

        decimal bankBal = 0m;
        foreach (var l in ledgers.Where(l => bankGroupIds.Contains(l.GroupId)))
        {
            decimal bal = ComputeClosingBalance(l, out var bType);
            bankBal += (bType == BalanceType.Debit ? bal : -bal);
        }
        dashboard.BankBalance = bankBal;

        // B. Receivables (Sundry Debtors) & Payables (Sundry Creditors)
        decimal totalReceivables = 0m;
        var debtorsList = new List<TopPartyOutstandingDto>();
        foreach (var l in ledgers.Where(l => debtorsGroupIds.Contains(l.GroupId)))
        {
            decimal bal = ComputeClosingBalance(l, out var bType);
            if (bal > 0)
            {
                debtorsList.Add(new TopPartyOutstandingDto
                {
                    LedgerId = l.LedgerId,
                    PartyName = l.LedgerName,
                    GroupName = l.Group?.GroupName ?? "Sundry Debtors",
                    Balance = bal,
                    BalanceType = bType
                });

                if (bType == BalanceType.Debit)
                {
                    totalReceivables += bal;
                }
            }
        }
        dashboard.TotalReceivables = totalReceivables;
        dashboard.TopDebtors = debtorsList
            .OrderByDescending(d => d.BalanceType == BalanceType.Debit ? d.Balance : -d.Balance)
            .Take(5)
            .ToList();

        decimal totalPayables = 0m;
        var creditorsList = new List<TopPartyOutstandingDto>();
        foreach (var l in ledgers.Where(l => creditorsGroupIds.Contains(l.GroupId)))
        {
            decimal bal = ComputeClosingBalance(l, out var bType);
            if (bal > 0)
            {
                creditorsList.Add(new TopPartyOutstandingDto
                {
                    LedgerId = l.LedgerId,
                    PartyName = l.LedgerName,
                    GroupName = l.Group?.GroupName ?? "Sundry Creditors",
                    Balance = bal,
                    BalanceType = bType
                });

                if (bType == BalanceType.Credit)
                {
                    totalPayables += bal;
                }
            }
        }
        dashboard.TotalPayables = totalPayables;
        dashboard.TopCreditors = creditorsList
            .OrderByDescending(c => c.BalanceType == BalanceType.Credit ? c.Balance : -c.Balance)
            .Take(5)
            .ToList();

        // C. Profitability (FY-to-Date up to asOfDate)
        var fyEntries = entriesUpToAsOfDate.Where(e => e.VoucherDate >= fyStart && e.VoucherDate <= asOfDate).ToList();
        var fyEntriesByLedger = fyEntries
            .GroupBy(e => e.LedgerId)
            .ToDictionary(g => g.Key, g => new { TotalDebit = g.Sum(x => x.Debit), TotalCredit = g.Sum(x => x.Credit) });

        decimal GetLedgerGroupNet(List<int> groupIds, bool isCreditNormal)
        {
            decimal total = 0m;
            foreach (var l in ledgers.Where(l => groupIds.Contains(l.GroupId)))
            {
                if (fyEntriesByLedger.TryGetValue(l.LedgerId, out var tx))
                {
                    total += isCreditNormal ? (tx.TotalCredit - tx.TotalDebit) : (tx.TotalDebit - tx.TotalCredit);
                }
            }
            return total;
        }

        dashboard.TotalSales = Math.Max(0m, GetLedgerGroupNet(salesGroupIds, isCreditNormal: true));
        dashboard.TotalPurchases = Math.Max(0m, GetLedgerGroupNet(purchaseGroupIds, isCreditNormal: false));
        dashboard.TotalDirectExpenses = Math.Max(0m, GetLedgerGroupNet(directExpGroupIds, isCreditNormal: false));
        dashboard.TotalIndirectExpenses = Math.Max(0m, GetLedgerGroupNet(indirectExpGroupIds, isCreditNormal: false));
        dashboard.TotalIndirectIncomes = Math.Max(0m, GetLedgerGroupNet(indirectIncGroupIds.Concat(directIncGroupIds).Distinct().ToList(), isCreditNormal: true));

        // D. Stock Inventory Valuation
        var stockItems = await _context.StockItems
            .AsNoTracking()
            .Where(s => s.CompanyId == companyId && s.IsActive)
            .ToListAsync(ct);

        dashboard.TotalStockItemsCount = stockItems.Count;
        dashboard.TotalStockValuation = stockItems.Sum(s => s.OpeningValue);

        // E. Monthly Financial Trends for the active Financial Year
        var voucherTypes = await _context.VoucherTypes.AsNoTracking().ToListAsync(ct);
        var salesTypeId = voucherTypes.FirstOrDefault(t => t.Type == VoucherTypeEnum.Sales)?.VoucherTypeId ?? 0;
        var purchaseTypeId = voucherTypes.FirstOrDefault(t => t.Type == VoucherTypeEnum.Purchase)?.VoucherTypeId ?? 0;
        var receiptTypeId = voucherTypes.FirstOrDefault(t => t.Type == VoucherTypeEnum.Receipt)?.VoucherTypeId ?? 0;
        var paymentTypeId = voucherTypes.FirstOrDefault(t => t.Type == VoucherTypeEnum.Payment)?.VoucherTypeId ?? 0;

        var fyVouchers = await _context.Vouchers
            .AsNoTracking()
            .Include(v => v.VoucherEntries)
            .Where(v => v.CompanyId == companyId && !v.IsDeleted && v.VoucherDate >= fyStart && v.VoucherDate <= asOfDate)
            .ToListAsync(ct);

        var monthlyMap = new Dictionary<(int Year, int Month), MonthlyFinancialSummaryDto>();

        // Pre-populate months from fyStart up to asOfDate
        var curMonth = new DateTime(fyStart.Year, fyStart.Month, 1);
        var endMonth = new DateTime(asOfDate.Year, asOfDate.Month, 1);
        while (curMonth <= endMonth)
        {
            var key = (curMonth.Year, curMonth.Month);
            monthlyMap[key] = new MonthlyFinancialSummaryDto
            {
                Year = curMonth.Year,
                Month = curMonth.Month,
                MonthLabel = curMonth.ToString("MMM yyyy", CultureInfo.InvariantCulture)
            };
            curMonth = curMonth.AddMonths(1);
        }

        foreach (var v in fyVouchers)
        {
            var key = (v.VoucherDate.Year, v.VoucherDate.Month);
            if (!monthlyMap.TryGetValue(key, out var summary))
            {
                summary = new MonthlyFinancialSummaryDto
                {
                    Year = v.VoucherDate.Year,
                    Month = v.VoucherDate.Month,
                    MonthLabel = v.VoucherDate.ToString("MMM yyyy", CultureInfo.InvariantCulture)
                };
                monthlyMap[key] = summary;
            }

            decimal voucherTotal = v.VoucherEntries.Sum(e => e.Debit);

            if (v.VoucherTypeId == salesTypeId)
            {
                summary.SalesAmount += voucherTotal;
            }
            else if (v.VoucherTypeId == purchaseTypeId)
            {
                summary.PurchaseAmount += voucherTotal;
            }
            else if (v.VoucherTypeId == receiptTypeId)
            {
                summary.InflowsAmount += voucherTotal;
            }
            else if (v.VoucherTypeId == paymentTypeId)
            {
                summary.OutflowsAmount += voucherTotal;
            }
        }

        dashboard.MonthlyTrends = monthlyMap.Values.OrderBy(m => m.Year).ThenBy(m => m.Month).ToList();

        // F. Recent Vouchers (latest 10)
        var recentVouchers = await _context.Vouchers
            .AsNoTracking()
            .Include(v => v.VoucherType)
            .Include(v => v.VoucherEntries)
                .ThenInclude(e => e.Ledger)
            .Where(v => v.CompanyId == companyId && !v.IsDeleted)
            .OrderByDescending(v => v.VoucherDate)
            .ThenByDescending(v => v.VoucherId)
            .Take(10)
            .ToListAsync(ct);

        foreach (var v in recentVouchers)
        {
            decimal totalAmount = v.VoucherEntries.Sum(e => e.Debit);
            var parties = v.VoucherEntries
                .Select(e => e.Ledger?.LedgerName ?? "Account")
                .Distinct()
                .Take(2)
                .ToList();

            dashboard.RecentVouchers.Add(new RecentVoucherDto
            {
                VoucherId = v.VoucherId,
                VoucherDate = v.VoucherDate,
                VoucherNumber = v.VoucherNumber,
                VoucherTypeName = v.VoucherType?.Name ?? "Voucher",
                Particulars = string.Join(", ", parties),
                Amount = totalAmount,
                Narration = v.Narration ?? string.Empty
            });
        }

        return dashboard;
    }
}
