using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;

namespace MoneyFlow.Services.Search;

public class SearchService : ISearchService
{
    private readonly AppDbContext _context;
    private readonly ILogger<SearchService> _logger;

    private static readonly List<(string Title, string Keywords, string Subtitle, string Target)> NavigationCatalog = new()
    {
        ("Day Book", "day book daily vouchers transactions entries", "Chronological audit register across all 8 voucher types", "DayBook"),
        ("Trial Balance", "trial balance tb debits credits reconciliation", "Double-entry mathematical reconciliation matrix", "TrialBalance"),
        ("Profit & Loss Account", "profit loss p&l pnl income expense trading gross net", "Trading and Profit & Loss Statement", "ProfitLoss"),
        ("Balance Sheet", "balance sheet bs assets liabilities capital property", "T-Format Financial Position Statement", "BalanceSheet"),
        ("Cash / Bank Book", "cash book bank book cash bank deposits withdrawals running balance", "Cash-in-Hand and Bank Accounts register", "CashBankBook"),
        ("Outstanding Analysis", "outstanding receivables payables aging debtors creditors bills", "Aging analysis register for Sundry Debtors and Creditors", "Outstanding"),
        ("Stock Summary", "stock summary inventory items units valuation closing stock", "Inventory valuation overview register", "StockSummary"),
        ("Ledgers", "ledgers chart of accounts party accounts customer supplier", "Chart of Accounts Ledger Master", "Ledgers"),
        ("Groups", "groups chart of accounts primary groups sub groups", "Group Hierarchy Master", "Groups"),
        ("Stock Items", "stock items inventory goods products materials", "Stock Item Master Register", "StockItems"),
        ("Units of Measure", "units of measure uom symbol nos kg box", "Units of Measure Master", "Units"),
        ("Contra Voucher (F4)", "contra f4 bank transfer cash deposit cash withdrawal", "Internal cash and bank transfer voucher", "Contra"),
        ("Payment Voucher (F5)", "payment f5 bank payment cash payment expense vendor payout", "Cash and bank payment voucher", "Payment"),
        ("Receipt Voucher (F6)", "receipt f6 customer collection deposit inflow", "Customer and income receipt voucher", "Receipt"),
        ("Journal Voucher (F7)", "journal f7 adjustment depreciation year end transfer non cash", "General journal adjustment voucher", "Journal"),
        ("Sales Voucher (F8)", "sales f8 invoice customer credit sale bill", "Sales invoice voucher", "Sales"),
        ("Purchase Voucher (F9)", "purchase f9 vendor bill supplier purchase", "Purchase invoice voucher", "Purchase"),
        ("Debit Note (Ctrl+F9)", "debit note purchase return supplier allowance", "Purchase returns voucher", "DebitNote"),
        ("Credit Note (Ctrl+F8)", "credit note sales return customer credit", "Sales returns voucher", "CreditNote")
    };

    public SearchService(
        AppDbContext context,
        ILogger<SearchService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GlobalSearchResultDto>> SearchAsync(
        int companyId,
        string query,
        GlobalSearchCategory category = GlobalSearchCategory.All,
        int maxResults = 50,
        CancellationToken ct = default)
    {
        var results = new List<GlobalSearchResultDto>();
        var cleanQuery = query?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(cleanQuery))
        {
            // If query is empty, return default navigation screens
            if (category == GlobalSearchCategory.All || category == GlobalSearchCategory.Navigation)
            {
                foreach (var nav in NavigationCatalog.Take(maxResults))
                {
                    results.Add(new GlobalSearchResultDto
                    {
                        Category = GlobalSearchCategory.Navigation,
                        Title = nav.Title,
                        Subtitle = nav.Subtitle,
                        NavigationTarget = nav.Target
                    });
                }
            }
            return results;
        }

        var lowerQuery = cleanQuery.ToLowerInvariant();
        decimal? parsedAmount = decimal.TryParse(cleanQuery, out var amt) && amt > 0 ? amt : null;

        // 1. Navigation Screens Search
        if (category == GlobalSearchCategory.All || category == GlobalSearchCategory.Navigation)
        {
            var matchedNavs = NavigationCatalog
                .Where(n => n.Title.ToLowerInvariant().Contains(lowerQuery) ||
                            n.Keywords.ToLowerInvariant().Contains(lowerQuery) ||
                            n.Subtitle.ToLowerInvariant().Contains(lowerQuery))
                .Select(n => new GlobalSearchResultDto
                {
                    Category = GlobalSearchCategory.Navigation,
                    Title = n.Title,
                    Subtitle = n.Subtitle,
                    NavigationTarget = n.Target
                });

            results.AddRange(matchedNavs);
        }

        // 2. Ledgers Search
        if (category == GlobalSearchCategory.All || category == GlobalSearchCategory.Ledger)
        {
            var matchedLedgers = await _context.Ledgers
                .AsNoTracking()
                .Include(l => l.Group)
                .Where(l => l.CompanyId == companyId && l.IsActive &&
                            (l.LedgerName.ToLower().Contains(lowerQuery) ||
                             (l.Group != null && l.Group.GroupName.ToLower().Contains(lowerQuery))))
                .OrderBy(l => l.LedgerName)
                .Take(maxResults)
                .Select(l => new GlobalSearchResultDto
                {
                    Category = GlobalSearchCategory.Ledger,
                    EntityId = l.LedgerId,
                    Title = l.LedgerName,
                    Subtitle = $"Group: {(l.Group != null ? l.Group.GroupName : "—")} | Opening: ₹{l.OpeningBalance:N2} {l.OpeningBalanceType}",
                    Amount = l.OpeningBalance
                })
                .ToListAsync(ct);

            results.AddRange(matchedLedgers);
        }

        // 3. Stock Items Search
        if (category == GlobalSearchCategory.All || category == GlobalSearchCategory.StockItem)
        {
            var matchedStockItems = await _context.StockItems
                .AsNoTracking()
                .Include(s => s.Unit)
                .Where(s => s.CompanyId == companyId && s.IsActive &&
                            (s.ItemName.ToLower().Contains(lowerQuery) ||
                             (s.Unit != null && s.Unit.UnitName.ToLower().Contains(lowerQuery))))
                .OrderBy(s => s.ItemName)
                .Take(maxResults)
                .Select(s => new GlobalSearchResultDto
                {
                    Category = GlobalSearchCategory.StockItem,
                    EntityId = s.StockItemId,
                    Title = s.ItemName,
                    Subtitle = $"Unit: {(s.Unit != null ? s.Unit.UnitName : "—")} | Qty: {s.OpeningQuantity:N2} @ ₹{s.OpeningRate:N2}",
                    Amount = s.OpeningValue
                })
                .ToListAsync(ct);

            results.AddRange(matchedStockItems);
        }

        // 4. Vouchers Search
        if (category == GlobalSearchCategory.All || category == GlobalSearchCategory.Voucher)
        {
            var voucherQuery = _context.Vouchers
                .AsNoTracking()
                .Include(v => v.VoucherType)
                .Include(v => v.VoucherEntries)
                    .ThenInclude(e => e.Ledger)
                .Where(v => v.CompanyId == companyId && !v.IsDeleted);

            if (parsedAmount.HasValue)
            {
                voucherQuery = voucherQuery.Where(v =>
                    v.VoucherNumber.ToLower().Contains(lowerQuery) ||
                    (v.ReferenceNumber != null && v.ReferenceNumber.ToLower().Contains(lowerQuery)) ||
                    (v.Narration != null && v.Narration.ToLower().Contains(lowerQuery)) ||
                    v.VoucherEntries.Any(e => e.Debit == parsedAmount.Value || e.Credit == parsedAmount.Value));
            }
            else
            {
                voucherQuery = voucherQuery.Where(v =>
                    v.VoucherNumber.ToLower().Contains(lowerQuery) ||
                    (v.ReferenceNumber != null && v.ReferenceNumber.ToLower().Contains(lowerQuery)) ||
                    (v.Narration != null && v.Narration.ToLower().Contains(lowerQuery)) ||
                    v.VoucherEntries.Any(e => e.Narration != null && e.Narration.ToLower().Contains(lowerQuery)) ||
                    v.VoucherEntries.Any(e => e.Ledger != null && e.Ledger.LedgerName.ToLower().Contains(lowerQuery)));
            }

            var vouchers = await voucherQuery
                .OrderByDescending(v => v.VoucherDate)
                .ThenByDescending(v => v.VoucherId)
                .Take(maxResults)
                .ToListAsync(ct);

            foreach (var v in vouchers)
            {
                decimal totalAmount = v.VoucherEntries.Sum(e => e.Debit);
                var parties = v.VoucherEntries
                    .Select(e => e.Ledger?.LedgerName ?? "Account")
                    .Distinct()
                    .ToList();

                string partyText = parties.Count > 0 ? string.Join(", ", parties) : "—";
                string typeName = v.VoucherType?.Name ?? "Voucher";

                results.Add(new GlobalSearchResultDto
                {
                    Category = GlobalSearchCategory.Voucher,
                    EntityId = v.VoucherId,
                    Title = $"{v.VoucherNumber} ({typeName})",
                    Subtitle = $"Date: {v.VoucherDate:dd-MMM-yyyy} | Particulars: {partyText} | Narration: {v.Narration}",
                    Amount = totalAmount,
                    Date = v.VoucherDate
                });
            }
        }

        return results.Take(maxResults).ToList();
    }
}
