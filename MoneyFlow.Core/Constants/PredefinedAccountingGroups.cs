using System;
using System.Collections.Generic;
using MoneyFlow.Core.Enums;

namespace MoneyFlow.Core.Constants;

/// <summary>
/// Definition specification for a canonical predefined accounting group.
/// </summary>
public record PredefinedGroupDef(
    string Name,
    GroupNature Nature,
    bool PrimaryGroup,
    bool AffectProfitLoss,
    string? ParentGroupName = null
);

/// <summary>
/// Tally-style canonical 28 predefined accounting groups specification:
/// 15 Primary Groups + 13 Sub-Groups.
/// </summary>
public static class PredefinedAccountingGroups
{
    public static readonly IReadOnlyList<PredefinedGroupDef> PrimaryGroups = new List<PredefinedGroupDef>
    {
        new("Branch/Divisions", GroupNature.Liabilities, true, false),
        new("Capital Account", GroupNature.Liabilities, true, false),
        new("Current Assets", GroupNature.Assets, true, false),
        new("Current Liabilities", GroupNature.Liabilities, true, false),
        new("Direct Expenses", GroupNature.Expenses, true, true),
        new("Direct Incomes", GroupNature.Income, true, true),
        new("Fixed Assets", GroupNature.Assets, true, false),
        new("Indirect Expenses", GroupNature.Expenses, true, true),
        new("Indirect Incomes", GroupNature.Income, true, true),
        new("Investments", GroupNature.Assets, true, false),
        new("Loans (Liability)", GroupNature.Liabilities, true, false),
        new("Misc. Expenses (ASSET)", GroupNature.Assets, true, false),
        new("Purchase Accounts", GroupNature.Expenses, true, true),
        new("Sales Accounts", GroupNature.Income, true, true),
        new("Suspense A/c", GroupNature.Liabilities, true, false)
    };

    public static readonly IReadOnlyList<PredefinedGroupDef> SubGroups = new List<PredefinedGroupDef>
    {
        new("Bank Accounts", GroupNature.Assets, false, false, "Current Assets"),
        new("Bank OD A/c", GroupNature.Liabilities, false, false, "Loans (Liability)"),
        new("Cash-in-hand", GroupNature.Assets, false, false, "Current Assets"),
        new("Deposits (Asset)", GroupNature.Assets, false, false, "Current Assets"),
        new("Duties & Taxes", GroupNature.Liabilities, false, false, "Current Liabilities"),
        new("Loans & Advances (Asset)", GroupNature.Assets, false, false, "Current Assets"),
        new("Provisions", GroupNature.Liabilities, false, false, "Current Liabilities"),
        new("Reserves & Surplus", GroupNature.Liabilities, false, false, "Capital Account"),
        new("Secured Loans", GroupNature.Liabilities, false, false, "Loans (Liability)"),
        new("Stock-in-hand", GroupNature.Assets, false, false, "Current Assets"),
        new("Sundry Creditors", GroupNature.Liabilities, false, false, "Current Liabilities"),
        new("Sundry Debtors", GroupNature.Assets, false, false, "Current Assets"),
        new("Unsecured Loans", GroupNature.Liabilities, false, false, "Loans (Liability)")
    };

    private static readonly HashSet<string> _allPredefinedNames = new(StringComparer.OrdinalIgnoreCase);

    static PredefinedAccountingGroups()
    {
        foreach (var g in PrimaryGroups) _allPredefinedNames.Add(g.Name);
        foreach (var g in SubGroups) _allPredefinedNames.Add(g.Name);

        // Also add common historical / aliased variations so they are protected
        _allPredefinedNames.Add("Direct Income");
        _allPredefinedNames.Add("Indirect Income");
        _allPredefinedNames.Add("Loans");
        _allPredefinedNames.Add("Cash-in-Hand");
    }

    public static bool IsPredefinedGroup(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return _allPredefinedNames.Contains(name.Trim());
    }

    /// <summary>
    /// Reserved ledger names that must never be deleted or created as groups.
    /// </summary>
    public static readonly HashSet<string> ReservedLedgers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Cash",
        "Profit & Loss A/c"
    };

    public static bool IsReservedLedger(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return ReservedLedgers.Contains(name.Trim());
    }
}
