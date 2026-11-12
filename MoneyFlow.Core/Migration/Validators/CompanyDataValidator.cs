using System;
using System.Collections.Generic;
using System.Linq;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Exceptions;

namespace MoneyFlow.Core.Migration.Validators;

public static class CompanyDataValidator
{
    public static void Validate(CompanyData data)
    {
        if (data == null)
        {
            throw new CompanyValidationException("Company data object is null.");
        }

        var errors = new List<string>();

        // 1. Company identity
        if (data.Company == null)
        {
            errors.Add("Company entity is missing.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(data.Company.CompanyNumber))
            {
                errors.Add("Company ID (CompanyNumber) is missing or empty.");
            }
            if (string.IsNullOrWhiteSpace(data.Company.CompanyName))
            {
                errors.Add("Company name is missing or empty.");
            }
        }

        // 2. Groups & Ledgers referential integrity
        var groupIds = data.Groups.Select(g => g.GroupId).ToHashSet();
        foreach (var group in data.Groups)
        {
            if (string.IsNullOrWhiteSpace(group.GroupName))
            {
                errors.Add($"Group with ID {group.GroupId} has an empty GroupName.");
            }
            if (group.ParentGroupId.HasValue && !groupIds.Contains(group.ParentGroupId.Value))
            {
                errors.Add($"Group '{group.GroupName}' references non-existent parent group ID {group.ParentGroupId.Value}.");
            }
        }

        var ledgerIds = data.Ledgers.Select(l => l.LedgerId).ToHashSet();
        foreach (var ledger in data.Ledgers)
        {
            if (string.IsNullOrWhiteSpace(ledger.LedgerName))
            {
                errors.Add($"Ledger with ID {ledger.LedgerId} has an empty LedgerName.");
            }
            if (!groupIds.Contains(ledger.GroupId))
            {
                errors.Add($"Ledger '{ledger.LedgerName}' references non-existent group ID {ledger.GroupId}.");
            }
        }

        // 3. Vouchers & VoucherEntries double-entry accounting integrity
        var voucherIds = data.Vouchers.Select(v => v.VoucherId).ToHashSet();
        var entriesByVoucher = data.VoucherEntries.GroupBy(e => e.VoucherId).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var voucher in data.Vouchers.Where(v => !v.IsDeleted))
        {
            if (string.IsNullOrWhiteSpace(voucher.VoucherNumber))
            {
                errors.Add($"Voucher with ID {voucher.VoucherId} has an empty VoucherNumber.");
            }

            if (entriesByVoucher.TryGetValue(voucher.VoucherId, out var entries))
            {
                decimal totalDebit = entries.Sum(e => e.Debit);
                decimal totalCredit = entries.Sum(e => e.Credit);

                if (Math.Abs(totalDebit - totalCredit) > 0.0001m)
                {
                    errors.Add($"Voucher {voucher.VoucherNumber} (ID: {voucher.VoucherId}) has unbalanced entries: Debit={totalDebit:F2}, Credit={totalCredit:F2}.");
                }

                foreach (var entry in entries)
                {
                    if (entry.Debit < 0 || entry.Credit < 0)
                    {
                        errors.Add($"Voucher {voucher.VoucherNumber} entry {entry.VoucherEntryId} has negative amounts: Debit={entry.Debit}, Credit={entry.Credit}.");
                    }
                    if (entry.Debit > 0 && entry.Credit > 0)
                    {
                        errors.Add($"Voucher {voucher.VoucherNumber} entry {entry.VoucherEntryId} has both Debit and Credit amounts non-zero.");
                    }
                    if (!ledgerIds.Contains(entry.LedgerId))
                    {
                        errors.Add($"Voucher {voucher.VoucherNumber} entry {entry.VoucherEntryId} references non-existent ledger ID {entry.LedgerId}.");
                    }
                }
            }
        }

        if (errors.Count > 0)
        {
            string summary = string.Join("; ", errors.Take(10));
            if (errors.Count > 10)
            {
                summary += $" ...and {errors.Count - 10} more errors.";
            }
            throw new CompanyValidationException($"Company validation failed with {errors.Count} error(s): {summary}");
        }
    }
}
