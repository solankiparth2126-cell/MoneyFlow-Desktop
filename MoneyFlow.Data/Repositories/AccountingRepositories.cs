using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Data.Repositories;

public class CompanyRepository : Repository<Company>, ICompanyRepository
{
    public CompanyRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Company?> GetCompanyWithDetailsAsync(int companyId, CancellationToken ct = default)
    {
        return await DbSet
            .Include(c => c.FinancialYears)
            .Include(c => c.Groups)
            .Include(c => c.Ledgers)
            .FirstOrDefaultAsync(c => c.CompanyId == companyId, ct);
    }
}

public class FinancialYearRepository : Repository<FinancialYear>, IFinancialYearRepository
{
    public FinancialYearRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<FinancialYear>> GetByCompanyIdAsync(int companyId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(f => f.CompanyId == companyId)
            .OrderByDescending(f => f.StartDate)
            .ToListAsync(ct);
    }

    public async Task<FinancialYear?> GetCurrentFYAsync(int companyId, DateTime asOfDate, CancellationToken ct = default)
    {
        return await DbSet
            .FirstOrDefaultAsync(f => f.CompanyId == companyId && f.StartDate <= asOfDate && f.EndDate >= asOfDate, ct);
    }
}

public class GroupRepository : Repository<Group>, IGroupRepository
{
    public GroupRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Group>> GetByCompanyIdAsync(int companyId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId && g.IsActive)
            .OrderBy(g => g.GroupName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Group>> GetHierarchicalTreeAsync(int companyId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId && g.ParentGroupId == null && g.IsActive)
            .Include(g => g.SubGroups)
            .OrderBy(g => g.GroupName)
            .ToListAsync(ct);
    }

    public async Task<bool> GroupNameExistsAsync(int companyId, string groupName, int? excludeGroupId = null, CancellationToken ct = default)
    {
        var query = DbSet.Where(g => g.CompanyId == companyId && g.GroupName.ToLower() == groupName.ToLower());
        if (excludeGroupId.HasValue)
        {
            query = query.Where(g => g.GroupId != excludeGroupId.Value);
        }
        return await query.AnyAsync(ct);
    }
}

public class LedgerRepository : Repository<Ledger>, ILedgerRepository
{
    public LedgerRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Ledger>> GetByCompanyIdAsync(int companyId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(l => l.Group)
            .Where(l => l.CompanyId == companyId && l.IsActive)
            .OrderBy(l => l.LedgerName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Ledger>> GetByGroupIdAsync(int companyId, int groupId, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(l => l.CompanyId == companyId && l.GroupId == groupId && l.IsActive)
            .OrderBy(l => l.LedgerName)
            .ToListAsync(ct);
    }

    public async Task<Ledger?> GetWithGroupAsync(int ledgerId, CancellationToken ct = default)
    {
        return await DbSet
            .Include(l => l.Group)
            .FirstOrDefaultAsync(l => l.LedgerId == ledgerId, ct);
    }

    public async Task<bool> LedgerNameExistsAsync(int companyId, string ledgerName, int? excludeLedgerId = null, CancellationToken ct = default)
    {
        var query = DbSet.Where(l => l.CompanyId == companyId && l.LedgerName.ToLower() == ledgerName.ToLower());
        if (excludeLedgerId.HasValue)
        {
            query = query.Where(l => l.LedgerId != excludeLedgerId.Value);
        }
        return await query.AnyAsync(ct);
    }
}

public class VoucherRepository : Repository<Voucher>, IVoucherRepository
{
    public VoucherRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<Voucher?> GetVoucherWithEntriesAsync(int voucherId, CancellationToken ct = default)
    {
        return await DbSet
            .Include(v => v.VoucherType)
            .Include(v => v.VoucherEntries)
                .ThenInclude(e => e.Ledger)
            .FirstOrDefaultAsync(v => v.VoucherId == voucherId && !v.IsDeleted, ct);
    }

    public async Task<IReadOnlyList<Voucher>> GetVouchersByDateRangeAsync(int companyId, int financialYearId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(v => v.VoucherType)
            .Include(v => v.VoucherEntries)
                .ThenInclude(e => e.Ledger)
            .Where(v => v.CompanyId == companyId &&
                        v.FinancialYearId == financialYearId &&
                        v.VoucherDate >= fromDate &&
                        v.VoucherDate <= toDate &&
                        !v.IsDeleted)
            .OrderBy(v => v.VoucherDate)
            .ThenBy(v => v.VoucherNumber)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Voucher>> GetVouchersByLedgerAsync(int companyId, int ledgerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Include(v => v.VoucherType)
            .Include(v => v.VoucherEntries)
                .ThenInclude(e => e.Ledger)
            .Where(v => v.CompanyId == companyId &&
                        v.VoucherDate >= fromDate &&
                        v.VoucherDate <= toDate &&
                        !v.IsDeleted &&
                        v.VoucherEntries.Any(e => e.LedgerId == ledgerId))
            .OrderBy(v => v.VoucherDate)
            .ToListAsync(ct);
    }

    public async Task<string> GetNextVoucherNumberAsync(int companyId, int voucherTypeId, int financialYearId, CancellationToken ct = default)
    {
        var voucherType = await Context.VoucherTypes.FindAsync(new object[] { voucherTypeId }, ct);
        var prefix = voucherType?.Prefix ?? "VCH-";

        var count = await DbSet
            .CountAsync(v => v.CompanyId == companyId &&
                             v.VoucherTypeId == voucherTypeId &&
                             v.FinancialYearId == financialYearId, ct);

        return $"{prefix}{(count + 1):D5}";
    }
}
