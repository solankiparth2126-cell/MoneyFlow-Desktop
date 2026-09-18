using System;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data.Storage;

namespace MoneyFlow.Data.Repositories;

public class Repository<T> : FileRepository<T> where T : class
{
    public Repository(CompanySession session) : base(session) { }
    public Repository(AppDataContext context) : base(context.Session) { }
}

public class CompanyRepository : FileCompanyRepository
{
    public CompanyRepository(CompanySession session) : base(session) { }
    public CompanyRepository(AppDataContext context) : base(context.Session) { }
}

public class FinancialYearRepository : FileFinancialYearRepository
{
    public FinancialYearRepository(CompanySession session) : base(session) { }
    public FinancialYearRepository(AppDataContext context) : base(context.Session) { }
}

public class GroupRepository : FileGroupRepository
{
    public GroupRepository(CompanySession session) : base(session) { }
    public GroupRepository(AppDataContext context) : base(context.Session) { }
}

public class LedgerRepository : FileLedgerRepository
{
    public LedgerRepository(CompanySession session) : base(session) { }
    public LedgerRepository(AppDataContext context) : base(context.Session) { }
}

public class VoucherRepository : FileVoucherRepository
{
    public VoucherRepository(CompanySession session) : base(session) { }
    public VoucherRepository(AppDataContext context) : base(context.Session) { }
}

public class UnitOfWork : IUnitOfWork
{
    private readonly FileUnitOfWork _inner;

    public UnitOfWork(CompanySession session)
    {
        _inner = new FileUnitOfWork(session);
    }

    public UnitOfWork(AppDataContext context) : this(context.Session)
    {
    }

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _inner.SaveChangesAsync(ct);
    public Task<IDbTransactionContext> BeginTransactionAsync(CancellationToken ct = default) => _inner.BeginTransactionAsync(ct);
    public void Dispose() => _inner.Dispose();
}
