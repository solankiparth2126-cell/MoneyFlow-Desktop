using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data.Storage;

namespace MoneyFlow.Data.Repositories;

// ─── Company Repository ─────────────────────────────────────────────────

public class FileCompanyRepository : FileRepository<Company>, ICompanyRepository
{
    public FileCompanyRepository(CompanySession session) : base(session) { }

    public Task<Company?> GetCompanyWithDetailsAsync(int companyId, CancellationToken ct = default)
    {
        var company = Store.CompanyInfo;
        if (company?.CompanyId == companyId)
        {
            // Populate navigation properties from in-memory data
            company.FinancialYears = Store.FinancialYears
                .Where(fy => fy.CompanyId == companyId).ToList();
            return Task.FromResult<Company?>(company);
        }
        return Task.FromResult<Company?>(null);
    }
}

// ─── Financial Year Repository ──────────────────────────────────────────

public class FileFinancialYearRepository : FileRepository<FinancialYear>, IFinancialYearRepository
{
    public FileFinancialYearRepository(CompanySession session) : base(session) { }

    public Task<IReadOnlyList<FinancialYear>> GetByCompanyIdAsync(int companyId, CancellationToken ct = default)
    {
        var result = Store.FinancialYears
            .Where(fy => fy.CompanyId == companyId)
            .OrderByDescending(fy => fy.StartDate)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<FinancialYear>>(result);
    }

    public Task<FinancialYear?> GetCurrentFYAsync(int companyId, DateTime asOfDate, CancellationToken ct = default)
    {
        var fy = Store.FinancialYears
            .FirstOrDefault(f => f.CompanyId == companyId
                && f.StartDate <= asOfDate
                && f.EndDate >= asOfDate);
        return Task.FromResult(fy);
    }
}

// ─── Group Repository ───────────────────────────────────────────────────

public class FileGroupRepository : FileRepository<Group>, IGroupRepository
{
    public FileGroupRepository(CompanySession session) : base(session) { }

    public Task<IReadOnlyList<Group>> GetByCompanyIdAsync(int companyId, CancellationToken ct = default)
    {
        var result = Store.Groups
            .Where(g => g.CompanyId == companyId && g.IsActive)
            .OrderBy(g => g.GroupName)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<Group>>(result);
    }

    public Task<IReadOnlyList<Group>> GetHierarchicalTreeAsync(int companyId, CancellationToken ct = default)
    {
        var allGroups = Store.Groups
            .Where(g => g.CompanyId == companyId && g.IsActive)
            .ToList();

        // Build navigation properties in-memory
        foreach (var g in allGroups)
        {
            g.ParentGroup = g.ParentGroupId.HasValue
                ? allGroups.FirstOrDefault(x => x.GroupId == g.ParentGroupId.Value)
                : null;
            g.SubGroups = allGroups.Where(x => x.ParentGroupId == g.GroupId).ToList();
            g.Ledgers = Indexes.GetLedgersByGroup(g.GroupId);
        }

        return Task.FromResult<IReadOnlyList<Group>>(allGroups.AsReadOnly());
    }

    public Task<bool> GroupNameExistsAsync(int companyId, string groupName, int? excludeGroupId = null, CancellationToken ct = default)
    {
        var exists = Store.Groups.Any(g =>
            g.CompanyId == companyId
            && g.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase)
            && g.IsActive
            && (!excludeGroupId.HasValue || g.GroupId != excludeGroupId.Value));
        return Task.FromResult(exists);
    }
}

// ─── Ledger Repository ──────────────────────────────────────────────────

public class FileLedgerRepository : FileRepository<Ledger>, ILedgerRepository
{
    public FileLedgerRepository(CompanySession session) : base(session) { }

    public Task<IReadOnlyList<Ledger>> GetByCompanyIdAsync(int companyId, CancellationToken ct = default)
    {
        var result = Store.Ledgers
            .Where(l => l.CompanyId == companyId && l.IsActive)
            .OrderBy(l => l.LedgerName)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<Ledger>>(result);
    }

    public Task<IReadOnlyList<Ledger>> GetByGroupIdAsync(int companyId, int groupId, CancellationToken ct = default)
    {
        var result = Indexes.GetLedgersByGroup(groupId)
            .Where(l => l.CompanyId == companyId && l.IsActive)
            .OrderBy(l => l.LedgerName)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<Ledger>>(result);
    }

    public Task<Ledger?> GetWithGroupAsync(int ledgerId, CancellationToken ct = default)
    {
        var ledger = Indexes.GetLedger(ledgerId);
        if (ledger != null)
        {
            ledger.Group = Indexes.GetGroup(ledger.GroupId);
        }
        return Task.FromResult(ledger);
    }

    public Task<bool> LedgerNameExistsAsync(int companyId, string ledgerName, int? excludeLedgerId = null, CancellationToken ct = default)
    {
        var exists = Store.Ledgers.Any(l =>
            l.CompanyId == companyId
            && l.LedgerName.Equals(ledgerName, StringComparison.OrdinalIgnoreCase)
            && l.IsActive
            && (!excludeLedgerId.HasValue || l.LedgerId != excludeLedgerId.Value));
        return Task.FromResult(exists);
    }
}

// ─── Voucher Repository ─────────────────────────────────────────────────

public class FileVoucherRepository : FileRepository<Voucher>, IVoucherRepository
{
    public FileVoucherRepository(CompanySession session) : base(session) { }

    public Task<Voucher?> GetVoucherWithEntriesAsync(int voucherId, CancellationToken ct = default)
    {
        var voucher = Indexes.GetVoucher(voucherId);
        if (voucher != null)
        {
            voucher.VoucherEntries = Indexes.GetEntriesByVoucher(voucherId);
            voucher.VoucherType = Indexes.GetVoucherType(voucher.VoucherTypeId);
        }
        return Task.FromResult(voucher);
    }

    public Task<IReadOnlyList<Voucher>> GetVouchersByDateRangeAsync(
        int companyId, int financialYearId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var vouchers = Indexes.GetVouchersByFinancialYear(financialYearId)
            .Where(v => v.CompanyId == companyId
                && !v.IsDeleted
                && v.VoucherDate >= fromDate
                && v.VoucherDate <= toDate)
            .OrderBy(v => v.VoucherDate)
            .ThenBy(v => v.VoucherNumber)
            .ToList();

        // Populate entries
        foreach (var v in vouchers)
        {
            v.VoucherEntries = Indexes.GetEntriesByVoucher(v.VoucherId);
            v.VoucherType = Indexes.GetVoucherType(v.VoucherTypeId);
        }

        return Task.FromResult<IReadOnlyList<Voucher>>(vouchers.AsReadOnly());
    }

    public Task<IReadOnlyList<Voucher>> GetVouchersByLedgerAsync(
        int companyId, int ledgerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        // Find all voucher IDs that have entries for this ledger
        var voucherIds = Store.VoucherEntries
            .Where(ve => ve.LedgerId == ledgerId)
            .Select(ve => ve.VoucherId)
            .Distinct()
            .ToHashSet();

        var vouchers = Store.Vouchers
            .Where(v => v.CompanyId == companyId
                && !v.IsDeleted
                && voucherIds.Contains(v.VoucherId)
                && v.VoucherDate >= fromDate
                && v.VoucherDate <= toDate)
            .OrderBy(v => v.VoucherDate)
            .ThenBy(v => v.VoucherNumber)
            .ToList();

        foreach (var v in vouchers)
        {
            v.VoucherEntries = Indexes.GetEntriesByVoucher(v.VoucherId);
            v.VoucherType = Indexes.GetVoucherType(v.VoucherTypeId);
        }

        return Task.FromResult<IReadOnlyList<Voucher>>(vouchers.AsReadOnly());
    }

    public Task<string> GetNextVoucherNumberAsync(int companyId, int voucherTypeId, int financialYearId, CancellationToken ct = default)
    {
        var voucherType = Indexes.GetVoucherType(voucherTypeId);
        var prefix = voucherType?.Prefix?.TrimEnd('-') ?? "V";

        var existingNumbers = Indexes.GetVouchersByFinancialYear(financialYearId)
            .Where(v => v.CompanyId == companyId && v.VoucherTypeId == voucherTypeId && !v.IsDeleted)
            .Select(v => v.VoucherNumber)
            .ToList();

        int maxNum = 0;
        foreach (var num in existingNumbers)
        {
            var numPart = num.Replace(prefix, "").Replace("-", "").Replace("/", "").Trim();
            if (int.TryParse(numPart, out var parsed) && parsed > maxNum)
                maxNum = parsed;
        }

        return Task.FromResult($"{prefix}-{(maxNum + 1):D4}");
    }
}
