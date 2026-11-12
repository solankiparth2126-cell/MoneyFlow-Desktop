using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;
<<<<<<< Updated upstream
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
=======

namespace MoneyFlow.Data.Repositories;

public class FileCompanyRepository : FileRepository<Company>, ICompanyRepository
{
    public FileCompanyRepository(ICompanyDataStore dataStore) : base(dataStore)
    {
    }

    public Task<Company?> GetCompanyWithDetailsAsync(int companyId, CancellationToken ct = default)
    {
        var company = List.FirstOrDefault(c => c.CompanyId == companyId);
        if (company != null && DataStore.CurrentData != null)
        {
            company.FinancialYears = DataStore.CurrentData.FinancialYears.Where(f => f.CompanyId == companyId).ToList();
            company.Groups = DataStore.CurrentData.Groups.Where(g => g.CompanyId == companyId).ToList();
            company.Ledgers = DataStore.CurrentData.Ledgers.Where(l => l.CompanyId == companyId).ToList();
        }
        return Task.FromResult(company);
    }
}

public class FileFinancialYearRepository : FileRepository<FinancialYear>, IFinancialYearRepository
{
    public FileFinancialYearRepository(ICompanyDataStore dataStore) : base(dataStore)
    {
    }

    public Task<IReadOnlyList<FinancialYear>> GetByCompanyIdAsync(int companyId, CancellationToken ct = default)
    {
        IReadOnlyList<FinancialYear> result = List
            .Where(f => f.CompanyId == companyId)
            .OrderByDescending(f => f.StartDate)
            .ToList();

        return Task.FromResult(result);
>>>>>>> Stashed changes
    }

    public Task<FinancialYear?> GetCurrentFYAsync(int companyId, DateTime asOfDate, CancellationToken ct = default)
    {
<<<<<<< Updated upstream
        var fy = Store.FinancialYears
            .FirstOrDefault(f => f.CompanyId == companyId
                && f.StartDate <= asOfDate
                && f.EndDate >= asOfDate);
=======
        var fy = List.FirstOrDefault(f => f.CompanyId == companyId && f.StartDate <= asOfDate && f.EndDate >= asOfDate);
>>>>>>> Stashed changes
        return Task.FromResult(fy);
    }
}

<<<<<<< Updated upstream
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
=======
public class FileGroupRepository : FileRepository<Group>, IGroupRepository
{
    public FileGroupRepository(ICompanyDataStore dataStore) : base(dataStore)
    {
    }

    public Task<IReadOnlyList<Group>> GetByCompanyIdAsync(int companyId, CancellationToken ct = default)
    {
        IReadOnlyList<Group> result = List
            .Where(g => g.CompanyId == companyId && g.IsActive)
            .OrderBy(g => g.GroupName)
            .ToList();

        return Task.FromResult(result);
>>>>>>> Stashed changes
    }

    public Task<IReadOnlyList<Group>> GetHierarchicalTreeAsync(int companyId, CancellationToken ct = default)
    {
<<<<<<< Updated upstream
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
=======
        var allGroups = List.Where(g => g.CompanyId == companyId && g.IsActive).ToList();
        var rootGroups = allGroups.Where(g => g.ParentGroupId == null).OrderBy(g => g.GroupName).ToList();

        foreach (var root in rootGroups)
        {
            PopulateChildren(root, allGroups);
        }

        IReadOnlyList<Group> result = rootGroups;
        return Task.FromResult(result);
    }

    private void PopulateChildren(Group parent, List<Group> allGroups)
    {
        parent.SubGroups = allGroups.Where(g => g.ParentGroupId == parent.GroupId).OrderBy(g => g.GroupName).ToList();
        foreach (var child in parent.SubGroups)
        {
            PopulateChildren(child, allGroups);
        }
>>>>>>> Stashed changes
    }

    public Task<bool> GroupNameExistsAsync(int companyId, string groupName, int? excludeGroupId = null, CancellationToken ct = default)
    {
<<<<<<< Updated upstream
        var exists = Store.Groups.Any(g =>
            g.CompanyId == companyId
            && g.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase)
            && g.IsActive
            && (!excludeGroupId.HasValue || g.GroupId != excludeGroupId.Value));
=======
        bool exists = List.Any(g => g.CompanyId == companyId &&
                                    g.GroupName.Equals(groupName, StringComparison.OrdinalIgnoreCase) &&
                                    (!excludeGroupId.HasValue || g.GroupId != excludeGroupId.Value));

>>>>>>> Stashed changes
        return Task.FromResult(exists);
    }
}

<<<<<<< Updated upstream
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
=======
public class FileLedgerRepository : FileRepository<Ledger>, ILedgerRepository
{
    public FileLedgerRepository(ICompanyDataStore dataStore) : base(dataStore)
    {
    }

    private void AttachGroup(Ledger ledger)
    {
        if (ledger.Group == null && DataStore.CurrentData != null)
        {
            ledger.Group = DataStore.CurrentData.Groups.FirstOrDefault(g => g.GroupId == ledger.GroupId);
        }
    }

    public Task<IReadOnlyList<Ledger>> GetByCompanyIdAsync(int companyId, CancellationToken ct = default)
    {
        var ledgers = List
            .Where(l => l.CompanyId == companyId && l.IsActive)
            .OrderBy(l => l.LedgerName)
            .ToList();

        foreach (var l in ledgers)
        {
            AttachGroup(l);
        }

        IReadOnlyList<Ledger> result = ledgers;
        return Task.FromResult(result);
>>>>>>> Stashed changes
    }

    public Task<IReadOnlyList<Ledger>> GetByGroupIdAsync(int companyId, int groupId, CancellationToken ct = default)
    {
<<<<<<< Updated upstream
        var result = Indexes.GetLedgersByGroup(groupId)
            .Where(l => l.CompanyId == companyId && l.IsActive)
            .OrderBy(l => l.LedgerName)
            .ToList()
            .AsReadOnly();
        return Task.FromResult<IReadOnlyList<Ledger>>(result);
=======
        var ledgers = List
            .Where(l => l.CompanyId == companyId && l.GroupId == groupId && l.IsActive)
            .OrderBy(l => l.LedgerName)
            .ToList();

        foreach (var l in ledgers)
        {
            AttachGroup(l);
        }

        IReadOnlyList<Ledger> result = ledgers;
        return Task.FromResult(result);
>>>>>>> Stashed changes
    }

    public Task<Ledger?> GetWithGroupAsync(int ledgerId, CancellationToken ct = default)
    {
<<<<<<< Updated upstream
        var ledger = Indexes.GetLedger(ledgerId);
        if (ledger != null)
        {
            ledger.Group = Indexes.GetGroup(ledger.GroupId);
=======
        var ledger = List.FirstOrDefault(l => l.LedgerId == ledgerId);
        if (ledger != null)
        {
            AttachGroup(ledger);
>>>>>>> Stashed changes
        }
        return Task.FromResult(ledger);
    }

    public Task<bool> LedgerNameExistsAsync(int companyId, string ledgerName, int? excludeLedgerId = null, CancellationToken ct = default)
    {
<<<<<<< Updated upstream
        var exists = Store.Ledgers.Any(l =>
            l.CompanyId == companyId
            && l.LedgerName.Equals(ledgerName, StringComparison.OrdinalIgnoreCase)
            && l.IsActive
            && (!excludeLedgerId.HasValue || l.LedgerId != excludeLedgerId.Value));
=======
        bool exists = List.Any(l => l.CompanyId == companyId &&
                                    l.LedgerName.Equals(ledgerName, StringComparison.OrdinalIgnoreCase) &&
                                    (!excludeLedgerId.HasValue || l.LedgerId != excludeLedgerId.Value));

>>>>>>> Stashed changes
        return Task.FromResult(exists);
    }
}

<<<<<<< Updated upstream
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
=======
public class FileVoucherRepository : FileRepository<Voucher>, IVoucherRepository
{
    public FileVoucherRepository(ICompanyDataStore dataStore) : base(dataStore)
    {
    }

    private void AttachDetails(Voucher voucher)
    {
        if (DataStore.CurrentData == null) return;

        if (voucher.VoucherType == null)
        {
            voucher.VoucherType = DataStore.CurrentData.VoucherTypes.FirstOrDefault(vt => vt.VoucherTypeId == voucher.VoucherTypeId);
        }

        if (voucher.VoucherEntries.Count == 0)
        {
            voucher.VoucherEntries = DataStore.CurrentData.VoucherEntries.Where(ve => ve.VoucherId == voucher.VoucherId).ToList();
        }

        foreach (var entry in voucher.VoucherEntries)
        {
            if (entry.Ledger == null)
            {
                entry.Ledger = DataStore.CurrentData.Ledgers.FirstOrDefault(l => l.LedgerId == entry.LedgerId);
            }
        }
    }

    public Task<Voucher?> GetVoucherWithEntriesAsync(int voucherId, CancellationToken ct = default)
    {
        var voucher = List.FirstOrDefault(v => v.VoucherId == voucherId && !v.IsDeleted);
        if (voucher != null)
        {
            AttachDetails(voucher);
>>>>>>> Stashed changes
        }
        return Task.FromResult(voucher);
    }

    public Task<IReadOnlyList<Voucher>> GetVouchersByDateRangeAsync(
<<<<<<< Updated upstream
        int companyId, int financialYearId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        var vouchers = Indexes.GetVouchersByFinancialYear(financialYearId)
            .Where(v => v.CompanyId == companyId
                && !v.IsDeleted
                && v.VoucherDate >= fromDate
                && v.VoucherDate <= toDate)
=======
        int companyId,
        int financialYearId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct = default)
    {
        var vouchers = List
            .Where(v => v.CompanyId == companyId &&
                        v.FinancialYearId == financialYearId &&
                        v.VoucherDate >= fromDate &&
                        v.VoucherDate <= toDate &&
                        !v.IsDeleted)
>>>>>>> Stashed changes
            .OrderBy(v => v.VoucherDate)
            .ThenBy(v => v.VoucherNumber)
            .ToList();

<<<<<<< Updated upstream
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
=======
        foreach (var v in vouchers)
        {
            AttachDetails(v);
        }

        IReadOnlyList<Voucher> result = vouchers;
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Voucher>> GetVouchersByLedgerAsync(
        int companyId,
        int ledgerId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken ct = default)
    {
        var allEntries = DataStore.CurrentData?.VoucherEntries ?? new List<VoucherEntry>();
        var matchingVoucherIds = allEntries
            .Where(e => e.LedgerId == ledgerId)
            .Select(e => e.VoucherId)
            .ToHashSet();

        var vouchers = List
            .Where(v => v.CompanyId == companyId &&
                        v.VoucherDate >= fromDate &&
                        v.VoucherDate <= toDate &&
                        !v.IsDeleted &&
                        matchingVoucherIds.Contains(v.VoucherId))
>>>>>>> Stashed changes
            .OrderBy(v => v.VoucherDate)
            .ThenBy(v => v.VoucherNumber)
            .ToList();

        foreach (var v in vouchers)
        {
<<<<<<< Updated upstream
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
=======
            AttachDetails(v);
        }

        IReadOnlyList<Voucher> result = vouchers;
        return Task.FromResult(result);
    }

    public Task<string> GetNextVoucherNumberAsync(
        int companyId,
        int voucherTypeId,
        int financialYearId,
        CancellationToken ct = default)
    {
        var voucherType = DataStore.CurrentData?.VoucherTypes.FirstOrDefault(vt => vt.VoucherTypeId == voucherTypeId);
        string prefix = voucherType?.Prefix ?? "VCH-";

        int count = List.Count(v => v.CompanyId == companyId &&
                                    v.VoucherTypeId == voucherTypeId &&
                                    v.FinancialYearId == financialYearId);

        return Task.FromResult($"{prefix}{(count + 1):D5}");
    }
}

public class FileTransactionContextAdapter : IDbTransactionContext
{
    private readonly IFileTransaction _fileTx;

    public FileTransactionContextAdapter(IFileTransaction fileTx)
    {
        _fileTx = fileTx;
    }

    public Task CommitAsync(CancellationToken ct = default) => _fileTx.CommitAsync(ct);
    public Task RollbackAsync(CancellationToken ct = default) => _fileTx.RollbackAsync(ct);
    public void Dispose() => _fileTx.Dispose();
    public ValueTask DisposeAsync() => _fileTx.DisposeAsync();
}

public class FileUnitOfWork : IUnitOfWork
{
    private readonly ICompanyDataStore _dataStore;
    private readonly ITransactionManager _transactionManager;

    public FileUnitOfWork(ICompanyDataStore dataStore, ITransactionManager transactionManager)
    {
        _dataStore = dataStore;
        _transactionManager = transactionManager;
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return _dataStore.SaveChangesAsync(ct);
    }

    public async Task<IDbTransactionContext> BeginTransactionAsync(CancellationToken ct = default)
    {
        string companyId = _dataStore.CurrentData?.Company.CompanyNumber ?? "DEFAULT";
        var tx = await _transactionManager.BeginTransactionAsync(companyId, ct);
        return new FileTransactionContextAdapter(tx);
    }

    public void Dispose()
    {
>>>>>>> Stashed changes
    }
}
