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

namespace MoneyFlow.Services.Accounting;

public class BankReconciliationService : IBankReconciliationService
{
    private readonly ICompanyDataStore _dataStore;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<BankReconciliationService> _logger;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public BankReconciliationService(
        ICompanyDataStore dataStore,
        IUnitOfWork unitOfWork,
        ILogger<BankReconciliationService> logger)
    {
        _dataStore = dataStore;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    internal BankReconciliationService(MoneyFlow.Data.AppDbContext context, ILogger<BankReconciliationService> logger)
        : this((ICompanyDataStore)null!, null!, logger)
    {
    }

    public Task<BankReconciliationReportDto> GetBankReconciliationDataAsync(
        int companyId,
        int bankLedgerId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct = default)
    {
        var bankLedger = _dataStore.GetEntityList<LedgerEntity>()
            .FirstOrDefault(l => l.LedgerId == bankLedgerId && l.CompanyId == companyId)
            ?? throw new ArgumentException($"Bank Ledger with ID {bankLedgerId} not found in active company.", nameof(bankLedgerId));

        // 1. Calculate Balance as per Company Books as of toDate
        decimal netOpening = bankLedger.OpeningBalanceType == BalanceType.Debit
            ? bankLedger.OpeningBalance
            : -bankLedger.OpeningBalance;

        var vouchers = _dataStore.GetEntityList<Voucher>()
            .Where(v => v.CompanyId == companyId && !v.IsDeleted && v.VoucherDate.Date <= toDate.Date)
            .ToDictionary(v => v.VoucherId);

        var voucherTypes = _dataStore.GetEntityList<VoucherType>().ToDictionary(vt => vt.VoucherTypeId);
        var ledgers = _dataStore.GetEntityList<LedgerEntity>().ToDictionary(l => l.LedgerId);

        var priorEntries = _dataStore.GetEntityList<VoucherEntry>()
            .Where(ve => ve.LedgerId == bankLedgerId && vouchers.ContainsKey(ve.VoucherId))
            .ToList();

        decimal bookDebit = priorEntries.Sum(e => e.Debit);
        decimal bookCredit = priorEntries.Sum(e => e.Credit);
        decimal balanceAsPerCompanyBooks = netOpening + bookDebit - bookCredit;

        // 2. Select transactions in period OR uncleared prior transactions
        var relevantEntries = priorEntries
            .Where(e => vouchers[e.VoucherId].VoucherDate.Date >= fromDate.Date || e.BankDate == null || e.BankDate > toDate.Date)
            .OrderBy(e => vouchers[e.VoucherId].VoucherDate)
            .ThenBy(e => vouchers[e.VoucherId].VoucherNumber)
            .ToList();

        var lines = new List<BankTransactionItemDto>();
        decimal chequesDepositedNotCleared = 0m;
        decimal chequesIssuedNotPresented = 0m;

        foreach (var entry in relevantEntries)
        {
            var v = vouchers[entry.VoucherId];
            var allVoucherEntries = _dataStore.GetEntityList<VoucherEntry>().Where(e => e.VoucherId == v.VoucherId).ToList();
            var counterpartEntry = allVoucherEntries.FirstOrDefault(e => e.LedgerId != bankLedgerId);
            string particulars = "Self / Transfer";
            if (counterpartEntry != null && ledgers.TryGetValue(counterpartEntry.LedgerId, out var cpLedger))
            {
                particulars = cpLedger.LedgerName;
            }

            voucherTypes.TryGetValue(v.VoucherTypeId, out var vt);

            bool clearedAsOfToDate = entry.BankDate.HasValue && entry.BankDate.Value.Date <= toDate.Date;
            if (!clearedAsOfToDate)
            {
                if (entry.Debit > 0) chequesDepositedNotCleared += entry.Debit;
                if (entry.Credit > 0) chequesIssuedNotPresented += entry.Credit;
            }

            lines.Add(new BankTransactionItemDto
            {
                VoucherEntryId = entry.VoucherEntryId,
                VoucherId = v.VoucherId,
                VoucherDate = v.VoucherDate,
                Particulars = particulars,
                VoucherTypeName = vt?.Name ?? "Voucher",
                VoucherNumber = v.VoucherNumber,
                Debit = entry.Debit,
                Credit = entry.Credit,
                InstrumentNumber = string.IsNullOrWhiteSpace(entry.InstrumentNumber) ? v.ReferenceNumber : entry.InstrumentNumber,
                BankDate = entry.BankDate
            });
        }

        var report = new BankReconciliationReportDto
        {
            CompanyId = companyId,
            BankLedgerId = bankLedgerId,
            BankLedgerName = bankLedger.LedgerName,
            FromDate = fromDate,
            ToDate = toDate,
            BalanceAsPerCompanyBooks = balanceAsPerCompanyBooks,
            ChequesDepositedNotCleared = chequesDepositedNotCleared,
            ChequesIssuedNotPresented = chequesIssuedNotPresented,
            Transactions = lines
        };

        return Task.FromResult(report);
    }

    public async Task<bool> SetBankDateAsync(int voucherEntryId, DateTime? bankDate, CancellationToken ct = default)
    {
        var entry = _dataStore.GetEntityList<VoucherEntry>().FirstOrDefault(e => e.VoucherEntryId == voucherEntryId);
        if (entry == null) return false;

        entry.BankDate = bankDate;
        await _unitOfWork.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateBankClearanceAsync(int companyId, List<BankClearanceUpdateDto> updates, CancellationToken ct = default)
    {
        if (updates == null || updates.Count == 0) return true;

        var entries = _dataStore.GetEntityList<VoucherEntry>();
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

        await _unitOfWork.SaveChangesAsync(ct);
        _logger.LogInformation("Updated bank clearance for {Count} entries in Company {CompanyId}.", updates.Count, companyId);
        return true;
    }
}
