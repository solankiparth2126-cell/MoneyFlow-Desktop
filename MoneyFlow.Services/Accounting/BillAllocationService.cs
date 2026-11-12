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
using LedgerEntity = MoneyFlow.Core.Entities.Ledger;
using GroupEntity = MoneyFlow.Core.Entities.Group;

namespace MoneyFlow.Services.Accounting;

public class BillAllocationService : IBillAllocationService
{
    private readonly ICompanyDataStore _dataStore;
    private readonly ILogger<BillAllocationService> _logger;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public BillAllocationService(ICompanyDataStore dataStore, ILogger<BillAllocationService> logger)
    {
        _dataStore = dataStore;
        _logger = logger;
    }

    internal BillAllocationService(MoneyFlow.Data.AppDbContext context, ILogger<BillAllocationService> logger)
        : this((ICompanyDataStore)null!, logger)
    {
    }

    public Task<IReadOnlyList<PendingBillDto>> GetPendingBillsAsync(int companyId, int ledgerId, CancellationToken ct = default)
    {
        var vouchers = _dataStore.GetEntityList<Voucher>().ToDictionary(v => v.VoucherId);
        var entries = _dataStore.GetEntityList<VoucherEntry>().ToDictionary(e => e.VoucherEntryId);

        var allocations = _dataStore.GetEntityList<BillAllocation>()
            .Where(b => b.CompanyId == companyId && b.LedgerId == ledgerId)
            .ToList();

        var grouped = allocations.GroupBy(b => b.BillName.Trim(), StringComparer.OrdinalIgnoreCase);
        var pendingBills = new List<PendingBillDto>();

        foreach (var group in grouped)
        {
            string billName = group.Key;
            var newRefs = group.Where(b => b.BillType == BillType.NewRef || b.BillType == BillType.Advance).ToList();
            var agstRefs = group.Where(b => b.BillType == BillType.AgstRef).ToList();

            decimal originalAmt = newRefs.Sum(b => b.Amount);
            decimal settledAmt = agstRefs.Sum(b => b.Amount);
            decimal pendingAmt = originalAmt - settledAmt;

            if (pendingAmt > 0)
            {
                var primaryNewRef = newRefs.FirstOrDefault();
                Voucher? v = null;
                if (primaryNewRef != null && entries.TryGetValue(primaryNewRef.VoucherEntryId, out var ve))
                {
                    vouchers.TryGetValue(ve.VoucherId, out v);
                }

                DateTime billDate = v?.VoucherDate ?? primaryNewRef?.CreatedAt ?? DateTime.Today;
                DateTime? dueDate = primaryNewRef?.DueDate ?? (primaryNewRef?.CreditDays.HasValue == true ? billDate.AddDays(primaryNewRef.CreditDays.Value) : null);

                pendingBills.Add(new PendingBillDto
                {
                    BillName = billName,
                    OriginalAmount = originalAmt,
                    SettledAmount = Math.Max(0, originalAmt - pendingAmt),
                    BillDate = billDate,
                    DueDate = dueDate
                });
            }
        }

        IReadOnlyList<PendingBillDto> result = pendingBills.OrderBy(b => b.BillDate).ToList();
        return Task.FromResult(result);
    }

    public async Task<BillAgingSummaryDto> GetBillAgingSummaryAsync(int companyId, int ledgerId, DateTime asOfDate, CancellationToken ct = default)
    {
        var ledger = _dataStore.GetEntityList<LedgerEntity>().FirstOrDefault(l => l.LedgerId == ledgerId && l.CompanyId == companyId);
        var pendingBills = await GetPendingBillsAsync(companyId, ledgerId, ct);

        var summary = new BillAgingSummaryDto
        {
            LedgerId = ledgerId,
            LedgerName = ledger?.LedgerName ?? "Unknown Ledger",
            Bills = pendingBills.ToList()
        };

        foreach (var bill in pendingBills)
        {
            DateTime baseDate = bill.DueDate ?? bill.BillDate;
            int days = Math.Max(0, (asOfDate.Date - baseDate.Date).Days);

            if (days <= 30)
            {
                summary.CurrentAmount += bill.PendingAmount;
            }
            else if (days <= 60)
            {
                summary.Days31To60 += bill.PendingAmount;
            }
            else if (days <= 90)
            {
                summary.Days61To90 += bill.PendingAmount;
            }
            else
            {
                summary.Over90Days += bill.PendingAmount;
            }
        }

        return summary;
    }

    public async Task<IReadOnlyList<BillAgingSummaryDto>> GetAllOutstandingAgingAsync(int companyId, bool isReceivables, DateTime asOfDate, CancellationToken ct = default)
    {
        string targetGroupName = isReceivables ? "Sundry Debtors" : "Sundry Creditors";

        var targetGroup = _dataStore.GetEntityList<GroupEntity>()
            .FirstOrDefault(g => g.CompanyId == companyId && g.GroupName.Equals(targetGroupName, StringComparison.OrdinalIgnoreCase));

        if (targetGroup == null) return new List<BillAgingSummaryDto>();

        var ledgers = _dataStore.GetEntityList<LedgerEntity>()
            .Where(l => l.CompanyId == companyId && l.GroupId == targetGroup.GroupId && l.IsActive)
            .OrderBy(l => l.LedgerName)
            .ToList();

        var result = new List<BillAgingSummaryDto>();
        foreach (var ledger in ledgers)
        {
            var aging = await GetBillAgingSummaryAsync(companyId, ledger.LedgerId, asOfDate, ct);
            if (aging.TotalPending > 0)
            {
                result.Add(aging);
            }
        }

        return result;
    }

    public Task<decimal> GetLedgerBalanceAsync(int companyId, int ledgerId, CancellationToken ct = default)
    {
        var ledger = _dataStore.GetEntityList<LedgerEntity>().FirstOrDefault(l => l.LedgerId == ledgerId && l.CompanyId == companyId);
        if (ledger == null) return Task.FromResult(0m);

        decimal balance = ledger.OpeningBalanceType == BalanceType.Debit ? ledger.OpeningBalance : -ledger.OpeningBalance;

        var entries = _dataStore.GetEntityList<VoucherEntry>().Where(e => e.LedgerId == ledgerId);
        foreach (var entry in entries)
        {
            balance += (entry.Debit - entry.Credit);
        }

        return Task.FromResult(balance);
    }
}
