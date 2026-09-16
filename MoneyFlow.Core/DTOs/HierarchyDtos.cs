using System;
using System.Collections.Generic;
using System.Linq;
using MoneyFlow.Core.Enums;

namespace MoneyFlow.Core.DTOs;

/// <summary>
/// Unified hierarchical node representing either an Account Group or a Ledger in the Chart of Accounts / Account Selector.
/// </summary>
public class AccountHierarchyNodeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsGroup { get; set; }
    public int? ParentGroupId { get; set; }
    public GroupNature Nature { get; set; }
    public bool PrimaryGroup { get; set; }
    public bool AffectProfitLoss { get; set; }
    public string Path { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
    public BalanceType BalanceType { get; set; } = BalanceType.Debit;
    public bool IsActive { get; set; } = true;

    public List<AccountHierarchyNodeDto> Children { get; set; } = new();

    public int SubGroupsCount => Children.Count(c => c.IsGroup);
    public int LedgersCount => Children.Count(c => !c.IsGroup);

    public string DisplayText => IsGroup ? $"📁 {Name}" : $"📄 {Name}";
}

/// <summary>
/// Represents a search result for a ledger including its full group hierarchy path.
/// </summary>
public class LedgerHierarchySearchDto
{
    public int LedgerId { get; set; }
    public string LedgerName { get; set; } = string.Empty;
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public GroupNature GroupNature { get; set; }
    public string FullHierarchyPath { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public BalanceType OpeningBalanceType { get; set; } = BalanceType.Debit;
    public decimal CurrentBalance { get; set; }
    public BalanceType CurrentBalanceType { get; set; } = BalanceType.Debit;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Recursive group balance rollup containing direct child ledgers and recursive sub-groups.
/// </summary>
public class GroupBalanceDto
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public GroupNature Nature { get; set; }
    public string Path { get; set; } = string.Empty;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal NetBalance { get; set; }
    public BalanceType BalanceType { get; set; } = BalanceType.Debit;
    public int DirectLedgerCount { get; set; }
    public int TotalLedgerCount { get; set; }
    public int DirectSubGroupCount { get; set; }
    public int TotalSubGroupCount { get; set; }
}

/// <summary>
/// Hierarchical Trial Balance row supporting recursive group aggregation and ledger details.
/// </summary>
public class HierarchicalTrialBalanceItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsGroup { get; set; }
    public int Level { get; set; }
    public int? ParentGroupId { get; set; }
    public GroupNature Nature { get; set; }
    public string Path { get; set; } = string.Empty;

    public decimal OpeningDebit { get; set; }
    public decimal OpeningCredit { get; set; }
    public decimal PeriodDebit { get; set; }
    public decimal PeriodCredit { get; set; }
    public decimal ClosingDebit { get; set; }
    public decimal ClosingCredit { get; set; }

    public List<HierarchicalTrialBalanceItemDto> Children { get; set; } = new();
}
