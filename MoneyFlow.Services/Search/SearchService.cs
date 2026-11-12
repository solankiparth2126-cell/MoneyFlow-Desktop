using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data.Repositories;
using GroupEntity = MoneyFlow.Core.Entities.Group;
using LedgerEntity = MoneyFlow.Core.Entities.Ledger;

namespace MoneyFlow.Services.Search;

public class SearchService : ISearchService
{
    private readonly ICompanyDataStore _dataStore;
    private readonly ILogger<SearchService> _logger;

    private static readonly List<(string Title, string Keywords, string Subtitle, string Target)> NavigationCatalog = new()
    {
        ("Executive Dashboard", "dashboard kpi overview executive summary working capital liquidity", "Executive KPI Overview & Financial Health", "Dashboard"),
        ("Import / Export Data", "import export data csv json migration excel upload download", "Data Import & Export Wizard", "ImportExport"),
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
        ("Credit Note (Ctrl+F8)", "credit note sales return customer credit", "Sales returns voucher", "CreditNote"),
        ("Backup & Restore System (F10)", "backup restore database company mfb bak snapshot archive recovery", "Local backup and restore manager with SHA-256 validation", "BackupRestore"),
        ("User Management & Security", "user management users security roles permissions access control password", "User accounts, roles, and permission matrix configuration", "UserManagement"),
        ("Audit Trail Register", "audit trail log activity history timestamp user changes compliance", "Audit register of user actions, transactions, and system events", "UserManagement"),
        ("Settings & Configuration (F11)", "settings configuration preferences options defaults theme date format printer backup path lock date", "System and company accounting configuration and preferences", "Settings")
    };

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public SearchService(
        ICompanyDataStore? dataStore = null,
        ILogger<SearchService>? logger = null)
    {
        _dataStore = dataStore!;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SearchService>.Instance;
    }

    internal SearchService(
        MoneyFlow.Data.AppDbContext context,
        ILogger<SearchService> logger)
        : this((ICompanyDataStore)null!, logger)
    {
    }

    public Task<IReadOnlyList<GlobalSearchResultDto>> SearchAsync(
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
            return Task.FromResult<IReadOnlyList<GlobalSearchResultDto>>(results);
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

        var groups = _dataStore.GetEntityList<GroupEntity>().ToDictionary(g => g.GroupId);

        // 2. Ledgers Search
        if (category == GlobalSearchCategory.All || category == GlobalSearchCategory.Ledger)
        {
            var matchedLedgers = _dataStore.GetEntityList<LedgerEntity>()
                .Where(l => l.CompanyId == companyId && l.IsActive &&
                            (l.LedgerName.ToLower().Contains(lowerQuery) ||
                             (groups.TryGetValue(l.GroupId, out var g) && g.GroupName.ToLower().Contains(lowerQuery))))
                .OrderBy(l => l.LedgerName)
                .Take(maxResults)
                .Select(l =>
                {
                    groups.TryGetValue(l.GroupId, out var grp);
                    return new GlobalSearchResultDto
                    {
                        Category = GlobalSearchCategory.Ledger,
                        EntityId = l.LedgerId,
                        Title = l.LedgerName,
                        Subtitle = $"Group: {grp?.GroupName ?? "—"} | Opening: ₹{l.OpeningBalance:N2} {l.OpeningBalanceType}",
                        Amount = l.OpeningBalance
                    };
                });

            results.AddRange(matchedLedgers);
        }

        // 3. Stock Items Search
        if (category == GlobalSearchCategory.All || category == GlobalSearchCategory.StockItem)
        {
            var units = _dataStore.GetEntityList<Unit>().ToDictionary(u => u.UnitId);
            var matchedStockItems = _dataStore.GetEntityList<StockItem>()
                .Where(s => s.CompanyId == companyId && s.IsActive &&
                            (s.ItemName.ToLower().Contains(lowerQuery) ||
                             (s.UnitId.HasValue && units.TryGetValue(s.UnitId.Value, out var u) && u.UnitName.ToLower().Contains(lowerQuery))))
                .OrderBy(s => s.ItemName)
                .Take(maxResults)
                .Select(s =>
                {
                    string unitName = s.UnitId.HasValue && units.TryGetValue(s.UnitId.Value, out var unt) ? unt.UnitName : "—";
                    return new GlobalSearchResultDto
                    {
                        Category = GlobalSearchCategory.StockItem,
                        EntityId = s.StockItemId,
                        Title = s.ItemName,
                        Subtitle = $"Unit: {unitName} | Qty: {s.OpeningQuantity:N2} @ ₹{s.OpeningRate:N2}",
                        Amount = s.OpeningValue
                    };
                });

            results.AddRange(matchedStockItems);
        }

        // 4. Vouchers Search
        if (category == GlobalSearchCategory.All || category == GlobalSearchCategory.Voucher)
        {
            var voucherTypes = _dataStore.GetEntityList<VoucherType>().ToDictionary(vt => vt.VoucherTypeId);
            var entries = _dataStore.GetEntityList<VoucherEntry>().GroupBy(e => e.VoucherId).ToDictionary(g => g.Key, g => g.ToList());

            var matchedVouchers = _dataStore.GetEntityList<Voucher>()
                .Where(v => v.CompanyId == companyId && !v.IsDeleted &&
                            (v.VoucherNumber.ToLower().Contains(lowerQuery) ||
                             (!string.IsNullOrEmpty(v.Narration) && v.Narration.ToLower().Contains(lowerQuery)) ||
                             (!string.IsNullOrEmpty(v.ReferenceNumber) && v.ReferenceNumber.ToLower().Contains(lowerQuery)) ||
                             (parsedAmount.HasValue && entries.TryGetValue(v.VoucherId, out var ve) && ve.Any(e => e.Debit == parsedAmount.Value || e.Credit == parsedAmount.Value))))
                .OrderByDescending(v => v.VoucherDate)
                .Take(maxResults)
                .Select(v =>
                {
                    voucherTypes.TryGetValue(v.VoucherTypeId, out var vt);
                    entries.TryGetValue(v.VoucherId, out var vEntries);
                    decimal totalAmt = vEntries?.Sum(e => e.Debit) ?? 0m;
                    return new GlobalSearchResultDto
                    {
                        Category = GlobalSearchCategory.Voucher,
                        EntityId = v.VoucherId,
                        Title = $"{vt?.Name ?? "Voucher"} #{v.VoucherNumber}",
                        Subtitle = $"{v.VoucherDate:dd-MMM-yyyy} | {v.Narration} | ₹{totalAmt:N2}",
                        Date = v.VoucherDate,
                        Amount = totalAmt
                    };
                });

            results.AddRange(matchedVouchers);
        }

        IReadOnlyList<GlobalSearchResultDto> finalResults = results.Take(maxResults).ToList();
        return Task.FromResult(finalResults);
    }
}
