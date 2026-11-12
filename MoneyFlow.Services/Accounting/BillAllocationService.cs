using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;

namespace MoneyFlow.Services.Accounting;

public class BillAllocationService : IBillAllocationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<BillAllocationService> _logger;

    public BillAllocationService(AppDbContext context, ILogger<BillAllocationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PendingBillDto>> GetPendingBillsAsync(int companyId, int ledgerId, CancellationToken ct = default)
    {
        var allocations = await _context.BillAllocations
            .AsNoTracking()
            .Include(b => b.VoucherEntry)
                .ThenInclude(ve => ve!.Voucher)
            .Where(b => b.CompanyId == companyId
                     && b.LedgerId == ledgerId
                     && b.VoucherEntry != null
                     && b.VoucherEntry.Voucher != null
                     && !b.VoucherEntry.Voucher.IsDeleted)
            .ToListAsync(ct);

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
                var primaryNewRef = newRefs.OrderBy(b => b.VoucherEntry?.Voucher?.VoucherDate ?? b.CreatedAt).FirstOrDefault();
                DateTime billDate = primaryNewRef?.VoucherEntry?.Voucher?.VoucherDate ?? primaryNewRef?.CreatedAt ?? DateTime.Today;
                DateTime? dueDate = primaryNewRef?.DueDate ?? (primaryNewRef?.CreditDays.HasValue == true ? billDate.AddDays(primaryNewRef.CreditDays.Value) : null);

                pendingBills.Add(new PendingBillDto
                {
                    BillName = billName,
                    BillDate = billDate,
                    DueDate = dueDate,
                    CreditDays = primaryNewRef?.CreditDays,
                    OriginalAmount = originalAmt,
                    SettledAmount = settledAmt
                });
            }
        }

        return pendingBills.OrderBy(b => b.BillDate).ToList();
    }

    public async Task<BillAgingSummaryDto> GetBillAgingSummaryAsync(int companyId, int ledgerId, DateTime asOfDate, CancellationToken ct = default)
    {
        var ledger = await _context.Ledgers
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LedgerId == ledgerId && l.CompanyId == companyId, ct);

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
        // Sundry Debtors (Receivables) vs Sundry Creditors (Payables)
        string targetGroupName = isReceivables ? "Sundry Debtors" : "Sundry Creditors";

        var targetGroup = await _context.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.CompanyId == companyId && g.GroupName.ToLower() == targetGroupName.ToLower(), ct);

        if (targetGroup == null) return new List<BillAgingSummaryDto>();

        var ledgers = await _context.Ledgers
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && (l.GroupId == targetGroup.GroupId || l.Group!.ParentGroupId == targetGroup.GroupId) && l.IsActive)
            .OrderBy(l => l.LedgerName)
            .ToListAsync(ct);

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
}
