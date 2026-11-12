using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data.Repositories;
using GroupEntity = MoneyFlow.Core.Entities.Group;
using LedgerEntity = MoneyFlow.Core.Entities.Ledger;

namespace MoneyFlow.Services.Dashboard;

public class DashboardService : IDashboardService
{
    private readonly ICompanyDataStore _dataStore;
    private readonly ICompanyRepository _companyRepo;
    private readonly IFinancialYearRepository _fyRepo;
    private readonly ILogger<DashboardService> _logger;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public DashboardService(
        ICompanyRepository companyRepo,
        IFinancialYearRepository fyRepo,
        ILogger<DashboardService> logger,
        ICompanyDataStore? dataStore = null)
    {
        _dataStore = dataStore!;
        _companyRepo = companyRepo;
        _fyRepo = fyRepo;
        _logger = logger;
    }

    internal DashboardService(
        MoneyFlow.Data.AppDbContext context,
        ILogger<DashboardService> logger)
        : this(
            new MoneyFlow.Data.Repositories.CompanyRepository(context),
            new MoneyFlow.Data.Repositories.FinancialYearRepository(context),
            logger,
            null)
    {
    }

    public async Task<DashboardDto> GetDashboardDataAsync(int companyId, DateTime asOfDate, CancellationToken ct = default)
    {
        var company = await _companyRepo.GetByIdAsync(companyId, ct);
        if (company == null)
        {
            company = _dataStore.CurrentData?.Company;
        }

        if (company == null)
        {
            throw new ArgumentException($"Company with ID {companyId} not found.", nameof(companyId));
        }

        var fys = await _fyRepo.GetByCompanyIdAsync(companyId, ct);
        var fy = fys.FirstOrDefault(f => f.StartDate <= asOfDate && f.EndDate >= asOfDate)
              ?? fys.OrderByDescending(f => f.StartDate).FirstOrDefault();

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

        var allGroups = _dataStore.GetEntityList<GroupEntity>().Where(g => g.CompanyId == companyId && g.IsActive).ToList();

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

        var ledgers = _dataStore.GetEntityList<LedgerEntity>().Where(l => l.CompanyId == companyId && l.IsActive).ToList();
        var ledgerIds = ledgers.Select(l => l.LedgerId).ToHashSet();

        var vouchers = _dataStore.GetEntityList<Voucher>()
            .Where(v => v.CompanyId == companyId && !v.IsDeleted && v.VoucherDate <= asOfDate)
            .ToDictionary(v => v.VoucherId);

        var voucherEntries = _dataStore.GetEntityList<VoucherEntry>()
            .Where(e => ledgerIds.Contains(e.LedgerId) && vouchers.ContainsKey(e.VoucherId))
            .ToList();

        var entriesByLedger = voucherEntries
            .GroupBy(e => e.LedgerId)
            .ToDictionary(g => g.Key, g => new { TotalDebit = g.Sum(x => x.Debit), TotalCredit = g.Sum(x => x.Credit) });

        decimal ComputeClosingBalance(LedgerEntity l, out BalanceType outType)
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

        // B. Receivables & Payables
        decimal totalReceivables = 0m;
        var debtorsList = new List<TopPartyOutstandingDto>();
        foreach (var l in ledgers.Where(l => debtorsGroupIds.Contains(l.GroupId)))
        {
            decimal bal = ComputeClosingBalance(l, out var bType);
            if (bal > 0)
            {
                var group = allGroups.FirstOrDefault(g => g.GroupId == l.GroupId);
                debtorsList.Add(new TopPartyOutstandingDto
                {
                    LedgerId = l.LedgerId,
                    PartyName = l.LedgerName,
                    GroupName = group?.GroupName ?? "Sundry Debtors",
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
                var group = allGroups.FirstOrDefault(g => g.GroupId == l.GroupId);
                creditorsList.Add(new TopPartyOutstandingDto
                {
                    LedgerId = l.LedgerId,
                    PartyName = l.LedgerName,
                    GroupName = group?.GroupName ?? "Sundry Creditors",
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

        // C. Profitability
        var fyEntries = voucherEntries
            .Where(e => vouchers.TryGetValue(e.VoucherId, out var v) && v.VoucherDate >= fyStart && v.VoucherDate <= asOfDate)
            .ToList();

        var fyEntriesByLedger = fyEntries
            .GroupBy(e => e.LedgerId)
            .ToDictionary(g => g.Key, g => new { TotalDebit = g.Sum(x => x.Debit), TotalCredit = g.Sum(x => x.Credit) });

        decimal GetLedgerGroupNet(List<int> gIds, bool isCreditNormal)
        {
            decimal total = 0m;
            foreach (var l in ledgers.Where(l => gIds.Contains(l.GroupId)))
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
        var stockItems = _dataStore.GetEntityList<StockItem>().Where(s => s.CompanyId == companyId && s.IsActive).ToList();
        dashboard.TotalStockItemsCount = stockItems.Count;
        dashboard.TotalStockValuation = stockItems.Sum(s => s.OpeningValue);

        // E. Recent Transactions
        var recentVouchers = _dataStore.GetEntityList<Voucher>()
            .Where(v => v.CompanyId == companyId && !v.IsDeleted)
            .OrderByDescending(v => v.VoucherDate)
            .ThenByDescending(v => v.VoucherId)
            .Take(10)
            .ToList();

        var voucherTypes = _dataStore.GetEntityList<VoucherType>().ToDictionary(vt => vt.VoucherTypeId);

        var recentList = new List<RecentVoucherDto>();
        foreach (var v in recentVouchers)
        {
            var vEntries = _dataStore.GetEntityList<VoucherEntry>().Where(e => e.VoucherId == v.VoucherId).ToList();
            decimal totalAmt = vEntries.Sum(e => e.Debit);

            var firstEntry = vEntries.FirstOrDefault(e => e.Debit > 0) ?? vEntries.FirstOrDefault();
            string partyName = string.Empty;
            if (firstEntry != null)
            {
                var l = ledgers.FirstOrDefault(led => led.LedgerId == firstEntry.LedgerId);
                partyName = l?.LedgerName ?? string.Empty;
            }

            voucherTypes.TryGetValue(v.VoucherTypeId, out var vt);

            recentList.Add(new RecentVoucherDto
            {
                VoucherId = v.VoucherId,
                VoucherDate = v.VoucherDate,
                VoucherTypeName = vt?.Name ?? "Voucher",
                VoucherNumber = v.VoucherNumber,
                Particulars = partyName,
                Amount = totalAmt,
                Narration = v.Narration
            });
        }
        dashboard.RecentVouchers = recentList;

        return dashboard;
    }
}
