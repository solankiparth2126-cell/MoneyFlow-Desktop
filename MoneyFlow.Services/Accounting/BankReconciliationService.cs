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

public class BankReconciliationService : IBankReconciliationService
{
    private readonly AppDbContext _context;
    private readonly ILogger<BankReconciliationService> _logger;

    public BankReconciliationService(AppDbContext context, ILogger<BankReconciliationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BankReconciliationReportDto> GetBankReconciliationDataAsync(
        int companyId,
        int bankLedgerId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct = default)
    {
        var bankLedger = await _context.Ledgers
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LedgerId == bankLedgerId && l.CompanyId == companyId, ct)
            ?? throw new ArgumentException($"Bank Ledger with ID {bankLedgerId} not found in active company.", nameof(bankLedgerId));

        // 1. Calculate Balance as per Company Books as of toDate
        decimal netOpening = bankLedger.OpeningBalanceType == BalanceType.Debit
            ? bankLedger.OpeningBalance
            : -bankLedger.OpeningBalance;

        var priorEntries = await _context.VoucherEntries
            .AsNoTracking()
            .Include(ve => ve.Voucher)
            .Where(ve => ve.LedgerId == bankLedgerId
                      && ve.Voucher != null
                      && ve.Voucher.CompanyId == companyId
                      && !ve.Voucher.IsDeleted
                      && ve.Voucher.VoucherDate.Date <= toDate.Date)
            .ToListAsync(ct);

        decimal bookDebit = priorEntries.Sum(e => e.Debit);
        decimal bookCredit = priorEntries.Sum(e => e.Credit);
        decimal balanceAsPerCompanyBooks = netOpening + bookDebit - bookCredit;

        // 2. Select transactions: transactions in period OR uncleared prior transactions
        var relevantEntries = priorEntries
            .Where(e => e.Voucher!.VoucherDate.Date >= fromDate.Date || e.BankDate == null || e.BankDate > toDate.Date)
            .OrderBy(e => e.Voucher!.VoucherDate)
            .ToList();

        // Voucher IDs to load counter entries for particulars
        var voucherIds = relevantEntries.Select(e => e.VoucherId).Distinct().ToList();
        var allVoucherEntries = await _context.VoucherEntries
            .AsNoTracking()
            .Include(ve => ve.Ledger)
            .Include(ve => ve.Voucher)
                .ThenInclude(v => v!.VoucherType)
            .Where(ve => voucherIds.Contains(ve.VoucherId))
            .ToListAsync(ct);

        var transactions = new List<BankTransactionItemDto>();
        decimal chequesDepositedNotCleared = 0m;
        decimal chequesIssuedNotPresented = 0m;

        foreach (var entry in relevantEntries)
        {
            // Find opposite ledger name for particulars
            var otherEntries = allVoucherEntries
                .Where(ve => ve.VoucherId == entry.VoucherId && ve.VoucherEntryId != entry.VoucherEntryId)
                .ToList();

            string particulars = otherEntries.Count == 1
                ? otherEntries[0].Ledger?.LedgerName ?? "(Opposite Account)"
                : otherEntries.Count > 1
                    ? $"As per details ({otherEntries[0].Ledger?.LedgerName} & others)"
                    : entry.Narration;

            if (string.IsNullOrWhiteSpace(particulars))
            {
                particulars = entry.Voucher?.Narration ?? "Bank Transaction";
            }

            // Amounts not reflected as of toDate:
            bool notClearedInBank = !entry.BankDate.HasValue || entry.BankDate.Value.Date > toDate.Date;
            if (notClearedInBank)
            {
                if (entry.Debit > 0) chequesDepositedNotCleared += entry.Debit;
                if (entry.Credit > 0) chequesIssuedNotPresented += entry.Credit;
            }

            transactions.Add(new BankTransactionItemDto
            {
                VoucherEntryId = entry.VoucherEntryId,
                VoucherId = entry.VoucherId,
                VoucherNumber = entry.Voucher?.VoucherNumber ?? string.Empty,
                VoucherDate = entry.Voucher?.VoucherDate ?? DateTime.Today,
                VoucherTypeName = entry.Voucher?.VoucherType?.Name ?? "Voucher",
                Particulars = particulars,
                InstrumentNumber = entry.InstrumentNumber,
                Debit = entry.Debit,
                Credit = entry.Credit,
                BankDate = entry.BankDate
            });
        }

        return new BankReconciliationReportDto
        {
            CompanyId = companyId,
            BankLedgerId = bankLedgerId,
            BankLedgerName = bankLedger.LedgerName,
            FromDate = fromDate,
            ToDate = toDate,
            BalanceAsPerCompanyBooks = balanceAsPerCompanyBooks,
            ChequesDepositedNotCleared = chequesDepositedNotCleared,
            ChequesIssuedNotPresented = chequesIssuedNotPresented,
            Transactions = transactions
        };
    }

    public async Task<bool> UpdateBankClearanceAsync(int companyId, List<BankClearanceUpdateDto> updates, CancellationToken ct = default)
    {
        if (updates == null || updates.Count == 0) return true;

        var entryIds = updates.Select(u => u.VoucherEntryId).Distinct().ToList();
        var entries = await _context.VoucherEntries
            .Include(e => e.Voucher)
            .Where(e => entryIds.Contains(e.VoucherEntryId) && e.Voucher!.CompanyId == companyId)
            .ToListAsync(ct);

        var updateMap = updates.ToDictionary(u => u.VoucherEntryId);

        foreach (var entry in entries)
        {
            if (updateMap.TryGetValue(entry.VoucherEntryId, out var update))
            {
                entry.BankDate = update.BankDate;
                if (!string.IsNullOrWhiteSpace(update.InstrumentNumber))
                {
                    entry.InstrumentNumber = update.InstrumentNumber.Trim();
                }
            }
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Updated bank clearance for {Count} entries in Company {CompanyId}.", entries.Count, companyId);
        return true;
    }
}
