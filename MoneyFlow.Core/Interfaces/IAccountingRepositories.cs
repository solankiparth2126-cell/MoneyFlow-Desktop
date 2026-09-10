using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Core.Interfaces;

public interface ICompanyRepository : IRepository<Company>
{
    Task<Company?> GetCompanyWithDetailsAsync(int companyId, CancellationToken ct = default);
}

public interface IFinancialYearRepository : IRepository<FinancialYear>
{
    Task<IReadOnlyList<FinancialYear>> GetByCompanyIdAsync(int companyId, CancellationToken ct = default);
    Task<FinancialYear?> GetCurrentFYAsync(int companyId, DateTime asOfDate, CancellationToken ct = default);
}

public interface IGroupRepository : IRepository<Group>
{
    Task<IReadOnlyList<Group>> GetByCompanyIdAsync(int companyId, CancellationToken ct = default);
    Task<IReadOnlyList<Group>> GetHierarchicalTreeAsync(int companyId, CancellationToken ct = default);
    Task<bool> GroupNameExistsAsync(int companyId, string groupName, int? excludeGroupId = null, CancellationToken ct = default);
}

public interface ILedgerRepository : IRepository<Ledger>
{
    Task<IReadOnlyList<Ledger>> GetByCompanyIdAsync(int companyId, CancellationToken ct = default);
    Task<IReadOnlyList<Ledger>> GetByGroupIdAsync(int companyId, int groupId, CancellationToken ct = default);
    Task<Ledger?> GetWithGroupAsync(int ledgerId, CancellationToken ct = default);
    Task<bool> LedgerNameExistsAsync(int companyId, string ledgerName, int? excludeLedgerId = null, CancellationToken ct = default);
}

public interface IVoucherRepository : IRepository<Voucher>
{
    Task<Voucher?> GetVoucherWithEntriesAsync(int voucherId, CancellationToken ct = default);
    Task<IReadOnlyList<Voucher>> GetVouchersByDateRangeAsync(int companyId, int financialYearId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<IReadOnlyList<Voucher>> GetVouchersByLedgerAsync(int companyId, int ledgerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<string> GetNextVoucherNumberAsync(int companyId, int voucherTypeId, int financialYearId, CancellationToken ct = default);
}
