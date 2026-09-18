using System;
using System.Collections.Generic;
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
using GroupEntity = MoneyFlow.Core.Entities.Group;
using LedgerEntity = MoneyFlow.Core.Entities.Ledger;

namespace MoneyFlow.Services.Accounting;

/// <summary>
/// Reusable centralized accounting hierarchy engine.
/// Builds dynamic Chart of Accounts trees, resolves paths, computes recursive balances,
/// and powers hierarchical reporting across the ERP.
/// </summary>
public class AccountingHierarchyService : IAccountingHierarchyService
{
    private readonly AppDbContext _context;
    private readonly ILogger<AccountingHierarchyService> _logger;

    public AccountingHierarchyService(
        AppDbContext context,
        ILogger<AccountingHierarchyService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AccountHierarchyNodeDto>> GetAccountTreeAsync(
        int companyId,
        bool includeLedgers = true,
        bool activeOnly = true,
        CancellationToken ct = default)
    {
        var groupQuery = _context.Groups
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId);

        if (activeOnly)
        {
            groupQuery = groupQuery.Where(g => g.IsActive);
        }

        var groups = await groupQuery
            .OrderBy(g => g.GroupName)
            .ToListAsync(ct);

        List<LedgerEntity> ledgers = new();
        if (includeLedgers)
        {
            var ledgerQuery = _context.Ledgers
                .AsNoTracking()
                .Where(l => l.CompanyId == companyId);

            if (activeOnly)
            {
                ledgerQuery = ledgerQuery.Where(l => l.IsActive);
            }

            ledgers = await ledgerQuery
                .OrderBy(l => l.LedgerName)
                .ToListAsync(ct);
        }

        // Build path lookup dictionary
        var groupMap = groups.ToDictionary(g => g.GroupId);
        var pathMap = new Dictionary<int, string>();

        string ResolveGroupPath(int groupId)
        {
            if (pathMap.TryGetValue(groupId, out var cached)) return cached;

            var stack = new List<string>();
            var visited = new HashSet<int>();
            int? currentId = groupId;

            while (currentId.HasValue && visited.Add(currentId.Value) && groupMap.TryGetValue(currentId.Value, out var g))
            {
                stack.Insert(0, g.GroupName);
                currentId = g.ParentGroupId;
            }

            var path = string.Join(" > ", stack);
            pathMap[groupId] = path;
            return path;
        }

        foreach (var g in groups)
        {
            ResolveGroupPath(g.GroupId);
        }

        // Group ledgers by group ID
        var ledgersByGroup = ledgers.GroupBy(l => l.GroupId).ToDictionary(g => g.Key, g => g.ToList());

        // Recursive tree builder
        AccountHierarchyNodeDto BuildNode(GroupEntity g)
        {
            var node = new AccountHierarchyNodeDto
            {
                Id = g.GroupId,
                Name = g.GroupName,
                IsGroup = true,
                ParentGroupId = g.ParentGroupId,
                Nature = g.Nature,
                PrimaryGroup = g.PrimaryGroup,
                AffectProfitLoss = g.AffectProfitLoss,
                Path = pathMap.TryGetValue(g.GroupId, out var p) ? p : g.GroupName,
                IsActive = g.IsActive
            };

            // Add child groups
            var childGroups = groups.Where(child => child.ParentGroupId == g.GroupId).OrderBy(c => c.GroupName);
            foreach (var childGroup in childGroups)
            {
                node.Children.Add(BuildNode(childGroup));
            }

            // Add child ledgers
            if (includeLedgers && ledgersByGroup.TryGetValue(g.GroupId, out var childLedgers))
            {
                foreach (var ledger in childLedgers.OrderBy(l => l.LedgerName))
                {
                    node.Children.Add(new AccountHierarchyNodeDto
                    {
                        Id = ledger.LedgerId,
                        Name = ledger.LedgerName,
                        IsGroup = false,
                        ParentGroupId = g.GroupId,
                        Nature = g.Nature,
                        PrimaryGroup = false,
                        AffectProfitLoss = g.AffectProfitLoss,
                        Path = $"{node.Path} > {ledger.LedgerName}",
                        Balance = ledger.OpeningBalance,
                        BalanceType = ledger.OpeningBalanceType,
                        IsActive = ledger.IsActive
                    });
                }
            }

            return node;
        }

        var roots = groups.Where(g => g.ParentGroupId == null).OrderBy(g => g.GroupName);
        var result = new List<AccountHierarchyNodeDto>();
        foreach (var root in roots)
        {
            result.Add(BuildNode(root));
        }

        return result;
    }

    public async Task<string> GetGroupPathAsync(int groupId, CancellationToken ct = default)
    {
        var group = await _context.Groups.AsNoTracking().FirstOrDefaultAsync(g => g.GroupId == groupId, ct);
        if (group == null) return string.Empty;

        var allGroups = await _context.Groups
            .AsNoTracking()
            .Where(g => g.CompanyId == group.CompanyId)
            .Select(g => new { g.GroupId, g.GroupName, g.ParentGroupId })
            .ToListAsync(ct);

        var map = allGroups.DistinctBy(g => g.GroupId).ToDictionary(g => g.GroupId);
        var parts = new List<string>();
        var visited = new HashSet<int>();
        int? current = groupId;

        while (current.HasValue && visited.Add(current.Value) && map.TryGetValue(current.Value, out var g))
        {
            parts.Insert(0, g.GroupName);
            current = g.ParentGroupId;
        }

        return string.Join(" > ", parts);
    }

    public async Task<string> GetLedgerPathAsync(int ledgerId, CancellationToken ct = default)
    {
        var ledger = await _context.Ledgers.AsNoTracking().FirstOrDefaultAsync(l => l.LedgerId == ledgerId, ct);
        if (ledger == null) return string.Empty;

        var groupPath = await GetGroupPathAsync(ledger.GroupId, ct);
        return string.IsNullOrEmpty(groupPath) ? ledger.LedgerName : $"{groupPath} > {ledger.LedgerName}";
    }

    public async Task<IReadOnlyList<int>> GetDescendantGroupIdsAsync(int companyId, int rootGroupId, CancellationToken ct = default)
    {
        var groups = await _context.Groups
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId && g.IsActive)
            .Select(g => new { g.GroupId, g.ParentGroupId })
            .ToListAsync(ct);

        var descendants = new HashSet<int>();
        void Collect(int parentId)
        {
            var children = groups.Where(g => g.ParentGroupId == parentId).Select(g => g.GroupId);
            foreach (var childId in children)
            {
                if (descendants.Add(childId))
                {
                    Collect(childId);
                }
            }
        }

        Collect(rootGroupId);
        return descendants.ToList();
    }

    public async Task<IReadOnlyList<int>> GetAncestorGroupIdsAsync(int companyId, int groupId, CancellationToken ct = default)
    {
        var groups = await _context.Groups
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId && g.IsActive)
            .Select(g => new { g.GroupId, g.ParentGroupId })
            .ToListAsync(ct);

        var map = groups.ToDictionary(g => g.GroupId);
        var ancestors = new List<int>();
        var visited = new HashSet<int>();

        if (map.TryGetValue(groupId, out var current))
        {
            int? parent = current.ParentGroupId;
            while (parent.HasValue && visited.Add(parent.Value) && map.TryGetValue(parent.Value, out var parentGroup))
            {
                ancestors.Add(parent.Value);
                parent = parentGroup.ParentGroupId;
            }
        }

        return ancestors;
    }

    public async Task<GroupBalanceDto> GetGroupBalanceAsync(
        int companyId,
        int groupId,
        DateTime? asOfDate = null,
        CancellationToken ct = default)
    {
        var group = await _context.Groups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.GroupId == groupId && g.CompanyId == companyId, ct)
            ?? throw new KeyNotFoundException($"Group with ID {groupId} not found.");

        var descendantGroupIds = await GetDescendantGroupIdsAsync(companyId, groupId, ct);
        var targetGroupIds = new List<int> { groupId };
        targetGroupIds.AddRange(descendantGroupIds);

        // Fetch all ledgers under this group and its sub-groups
        var ledgers = await _context.Ledgers
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.IsActive && targetGroupIds.Contains(l.GroupId))
            .ToListAsync(ct);

        var ledgerIds = ledgers.Select(l => l.LedgerId).ToList();

        // Calculate opening balances
        decimal totalOpeningDebit = 0m;
        decimal totalOpeningCredit = 0m;
        foreach (var l in ledgers)
        {
            if (l.OpeningBalanceType == BalanceType.Debit)
                totalOpeningDebit += l.OpeningBalance;
            else
                totalOpeningCredit += l.OpeningBalance;
        }

        // Voucher entries up to asOfDate
        var entryQuery = _context.VoucherEntries
            .AsNoTracking()
            .Where(ve => ve.Voucher!.CompanyId == companyId &&
                         !ve.Voucher.IsDeleted &&
                         ledgerIds.Contains(ve.LedgerId));

        if (asOfDate.HasValue)
        {
            var endOfDay = asOfDate.Value.Date.AddDays(1).AddTicks(-1);
            entryQuery = entryQuery.Where(ve => ve.Voucher!.VoucherDate <= endOfDay);
        }

        var entryTotals = await entryQuery
            .GroupBy(ve => 1)
            .Select(g => new
            {
                Debit = g.Sum(x => x.Debit),
                Credit = g.Sum(x => x.Credit)
            })
            .FirstOrDefaultAsync(ct);

        decimal periodDebit = entryTotals?.Debit ?? 0m;
        decimal periodCredit = entryTotals?.Credit ?? 0m;

        decimal totalDebit = totalOpeningDebit + periodDebit;
        decimal totalCredit = totalOpeningCredit + periodCredit;

        bool isDebitNormal = group.Nature == GroupNature.Assets || group.Nature == GroupNature.Expenses;
        decimal netBalance = isDebitNormal ? (totalDebit - totalCredit) : (totalCredit - totalDebit);
        var balanceType = totalDebit >= totalCredit ? BalanceType.Debit : BalanceType.Credit;

        var path = await GetGroupPathAsync(groupId, ct);

        return new GroupBalanceDto
        {
            GroupId = groupId,
            GroupName = group.GroupName,
            Nature = group.Nature,
            Path = path,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            NetBalance = Math.Abs(netBalance),
            BalanceType = balanceType,
            DirectLedgerCount = ledgers.Count(l => l.GroupId == groupId),
            TotalLedgerCount = ledgers.Count,
            DirectSubGroupCount = await _context.Groups.CountAsync(g => g.ParentGroupId == groupId && g.IsActive, ct),
            TotalSubGroupCount = descendantGroupIds.Count
        };
    }

    public async Task<IReadOnlyList<HierarchicalTrialBalanceItemDto>> GetGroupWiseTrialBalanceAsync(
        int companyId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct = default)
    {
        var startOfDay = fromDate.Date;
        var endOfDay = toDate.Date.AddDays(1).AddTicks(-1);

        var groups = await _context.Groups
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId && g.IsActive)
            .OrderBy(g => g.GroupName)
            .ToListAsync(ct);

        var ledgers = await _context.Ledgers
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.IsActive)
            .OrderBy(l => l.LedgerName)
            .ToListAsync(ct);

        var allEntries = await _context.VoucherEntries
            .AsNoTracking()
            .Where(ve => ve.Voucher!.CompanyId == companyId &&
                         !ve.Voucher.IsDeleted &&
                         ve.Voucher.VoucherDate <= endOfDay)
            .Select(ve => new
            {
                ve.LedgerId,
                ve.Debit,
                ve.Credit,
                ve.Voucher!.VoucherDate
            })
            .ToListAsync(ct);

        var priorLookup = allEntries
            .Where(e => e.VoucherDate < startOfDay)
            .GroupBy(e => e.LedgerId)
            .ToDictionary(g => g.Key, g => new { Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) });

        var periodLookup = allEntries
            .Where(e => e.VoucherDate >= startOfDay && e.VoucherDate <= endOfDay)
            .GroupBy(e => e.LedgerId)
            .ToDictionary(g => g.Key, g => new { Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) });

        // Calculate balances per ledger
        var ledgerItems = new Dictionary<int, HierarchicalTrialBalanceItemDto>();
        foreach (var l in ledgers)
        {
            decimal masterOpeningNet = l.OpeningBalanceType == BalanceType.Debit ? l.OpeningBalance : -l.OpeningBalance;
            decimal priorDr = priorLookup.TryGetValue(l.LedgerId, out var pr) ? pr.Debit : 0m;
            decimal priorCr = pr != null ? pr.Credit : 0m;

            decimal openingNet = masterOpeningNet + priorDr - priorCr;
            decimal openDr = openingNet >= 0 ? openingNet : 0m;
            decimal openCr = openingNet < 0 ? Math.Abs(openingNet) : 0m;

            decimal periodDr = periodLookup.TryGetValue(l.LedgerId, out var cur) ? cur.Debit : 0m;
            decimal periodCr = cur != null ? cur.Credit : 0m;

            decimal closingNet = openingNet + periodDr - periodCr;
            decimal closeDr = closingNet >= 0 ? closingNet : 0m;
            decimal closeCr = closingNet < 0 ? Math.Abs(closingNet) : 0m;

            ledgerItems[l.LedgerId] = new HierarchicalTrialBalanceItemDto
            {
                Id = l.LedgerId,
                Name = l.LedgerName,
                IsGroup = false,
                ParentGroupId = l.GroupId,
                OpeningDebit = openDr,
                OpeningCredit = openCr,
                PeriodDebit = periodDr,
                PeriodCredit = periodCr,
                ClosingDebit = closeDr,
                ClosingCredit = closeCr
            };
        }

        var ledgersByGroup = ledgers.GroupBy(l => l.GroupId).ToDictionary(g => g.Key, g => g.ToList());

        HierarchicalTrialBalanceItemDto BuildGroupItem(GroupEntity g, int level)
        {
            var item = new HierarchicalTrialBalanceItemDto
            {
                Id = g.GroupId,
                Name = g.GroupName,
                IsGroup = true,
                Level = level,
                ParentGroupId = g.ParentGroupId,
                Nature = g.Nature
            };

            // Add sub-groups
            var subGroups = groups.Where(sub => sub.ParentGroupId == g.GroupId).OrderBy(s => s.GroupName);
            foreach (var sub in subGroups)
            {
                var subItem = BuildGroupItem(sub, level + 1);
                item.Children.Add(subItem);

                // Roll up sub-group totals
                item.OpeningDebit += subItem.OpeningDebit;
                item.OpeningCredit += subItem.OpeningCredit;
                item.PeriodDebit += subItem.PeriodDebit;
                item.PeriodCredit += subItem.PeriodCredit;
                item.ClosingDebit += subItem.ClosingDebit;
                item.ClosingCredit += subItem.ClosingCredit;
            }

            // Add direct ledgers
            if (ledgersByGroup.TryGetValue(g.GroupId, out var directLedgers))
            {
                foreach (var dl in directLedgers.OrderBy(l => l.LedgerName))
                {
                    if (ledgerItems.TryGetValue(dl.LedgerId, out var dlItem))
                    {
                        dlItem.Level = level + 1;
                        dlItem.Nature = g.Nature;
                        item.Children.Add(dlItem);

                        item.OpeningDebit += dlItem.OpeningDebit;
                        item.OpeningCredit += dlItem.OpeningCredit;
                        item.PeriodDebit += dlItem.PeriodDebit;
                        item.PeriodCredit += dlItem.PeriodCredit;
                        item.ClosingDebit += dlItem.ClosingDebit;
                        item.ClosingCredit += dlItem.ClosingCredit;
                    }
                }
            }

            return item;
        }

        var roots = groups.Where(g => g.ParentGroupId == null).OrderBy(g => g.GroupName);
        var result = new List<HierarchicalTrialBalanceItemDto>();
        foreach (var r in roots)
        {
            result.Add(BuildGroupItem(r, 0));
        }

        return result;
    }

    public async Task<IReadOnlyList<LedgerHierarchySearchDto>> SearchLedgersAsync(
        int companyId,
        string searchTerm,
        CancellationToken ct = default)
    {
        var term = (searchTerm ?? string.Empty).Trim().ToLowerInvariant();

        var query = _context.Ledgers
            .AsNoTracking()
            .Include(l => l.Group)
            .Where(l => l.CompanyId == companyId && l.IsActive);

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(l => l.LedgerName.ToLower().Contains(term) ||
                                     (l.Group != null && l.Group.GroupName.ToLower().Contains(term)));
        }

        var matchedLedgers = await query
            .OrderBy(l => l.LedgerName)
            .ToListAsync(ct);

        var allGroups = await _context.Groups
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId)
            .Select(g => new { g.GroupId, g.GroupName, g.ParentGroupId })
            .ToListAsync(ct);

        var groupMap = allGroups.ToDictionary(g => g.GroupId);
        var pathCache = new Dictionary<int, string>();

        string GetPathForGroup(int gId)
        {
            if (pathCache.TryGetValue(gId, out var cached)) return cached;

            var stack = new List<string>();
            var visited = new HashSet<int>();
            int? cur = gId;

            while (cur.HasValue && visited.Add(cur.Value) && groupMap.TryGetValue(cur.Value, out var grp))
            {
                stack.Insert(0, grp.GroupName);
                cur = grp.ParentGroupId;
            }

            var p = string.Join(" > ", stack);
            pathCache[gId] = p;
            return p;
        }

        var ledgerIds = matchedLedgers.Select(l => l.LedgerId).ToList();
        var totals = await _context.VoucherEntries
            .AsNoTracking()
            .Where(ve => ve.Voucher!.CompanyId == companyId &&
                         !ve.Voucher.IsDeleted &&
                         ledgerIds.Contains(ve.LedgerId))
            .GroupBy(ve => ve.LedgerId)
            .Select(g => new
            {
                LedgerId = g.Key,
                TotalDebit = g.Sum(x => x.Debit),
                TotalCredit = g.Sum(x => x.Credit)
            })
            .ToDictionaryAsync(x => x.LedgerId, ct);

        var results = new List<LedgerHierarchySearchDto>();
        foreach (var l in matchedLedgers)
        {
            var grpPath = GetPathForGroup(l.GroupId);
            var fullPath = string.IsNullOrEmpty(grpPath) ? l.LedgerName : $"{grpPath} > {l.LedgerName}";

            totals.TryGetValue(l.LedgerId, out var entry);
            var dr = (l.OpeningBalanceType == BalanceType.Debit ? l.OpeningBalance : 0m) + (entry?.TotalDebit ?? 0m);
            var cr = (l.OpeningBalanceType == BalanceType.Credit ? l.OpeningBalance : 0m) + (entry?.TotalCredit ?? 0m);
            var net = dr - cr;

            results.Add(new LedgerHierarchySearchDto
            {
                LedgerId = l.LedgerId,
                LedgerName = l.LedgerName,
                GroupId = l.GroupId,
                GroupName = l.Group?.GroupName ?? string.Empty,
                GroupNature = l.Group?.Nature ?? GroupNature.Assets,
                FullHierarchyPath = fullPath,
                OpeningBalance = l.OpeningBalance,
                OpeningBalanceType = l.OpeningBalanceType,
                CurrentBalance = Math.Abs(net),
                CurrentBalanceType = net >= 0 ? BalanceType.Debit : BalanceType.Credit,
                IsActive = l.IsActive
            });
        }

        return results;
    }
}
