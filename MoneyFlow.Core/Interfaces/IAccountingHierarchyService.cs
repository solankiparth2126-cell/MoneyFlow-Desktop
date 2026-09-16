using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;

namespace MoneyFlow.Core.Interfaces;

/// <summary>
/// Reusable centralized accounting hierarchy engine.
/// Provides dynamic chart of accounts tree generation, full hierarchy paths,
/// ancestor/descendant resolution, recursive group balance roll-up, and hierarchical trial balance.
/// </summary>
public interface IAccountingHierarchyService
{
    /// <summary>
    /// Generates the complete Chart of Accounts tree for a company, including groups and ledgers.
    /// </summary>
    Task<IReadOnlyList<AccountHierarchyNodeDto>> GetAccountTreeAsync(
        int companyId,
        bool includeLedgers = true,
        bool activeOnly = true,
        CancellationToken ct = default);

    /// <summary>
    /// Computes the full breadcrumb path of a group (e.g., "Current Assets > Bank Accounts").
    /// </summary>
    Task<string> GetGroupPathAsync(int groupId, CancellationToken ct = default);

    /// <summary>
    /// Computes the full breadcrumb path of a ledger (e.g., "Current Assets > Bank Accounts > HDFC Bank").
    /// </summary>
    Task<string> GetLedgerPathAsync(int ledgerId, CancellationToken ct = default);

    /// <summary>
    /// Recursively retrieves all descendant group IDs under the specified root group.
    /// </summary>
    Task<IReadOnlyList<int>> GetDescendantGroupIdsAsync(int companyId, int rootGroupId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves all ancestor group IDs for a group, ordered from immediate parent up to primary group.
    /// </summary>
    Task<IReadOnlyList<int>> GetAncestorGroupIdsAsync(int companyId, int groupId, CancellationToken ct = default);

    /// <summary>
    /// Computes the recursive group balance, summing all direct child ledgers and all sub-groups.
    /// </summary>
    Task<GroupBalanceDto> GetGroupBalanceAsync(
        int companyId,
        int groupId,
        DateTime? asOfDate = null,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a hierarchical, group-wise Trial Balance where group totals are calculated recursively.
    /// </summary>
    Task<IReadOnlyList<HierarchicalTrialBalanceItemDto>> GetGroupWiseTrialBalanceAsync(
        int companyId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct = default);

    /// <summary>
    /// Searches ledgers by name or group name, returning matched ledgers with their full hierarchy breadcrumb paths.
    /// </summary>
    Task<IReadOnlyList<LedgerHierarchySearchDto>> SearchLedgersAsync(
        int companyId,
        string searchTerm,
        CancellationToken ct = default);
}
