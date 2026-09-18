using System.Collections.Generic;
using System.Linq;
using MoneyFlow.Core.Entities;
using MoneyFlow.Data.Storage;

namespace MoneyFlow.Data;

/// <summary>
/// In-memory data accessor that replaces AppDbContext.
/// Provides IQueryable-like access to entity collections via CompanySession.
/// Services that previously injected AppDbContext should inject this instead.
/// 
/// All collections are backed by in-memory Lists — LINQ operations run against them directly.
/// No SQL. No EF Core. No database connection.
/// </summary>
public class AppDataContext : System.IDisposable
{
    private readonly CompanySession _session;

    public AppDataContext(CompanySession session)
    {
        _session = session;
    }

    public CompanySession Session => _session;
    public Microsoft.EntityFrameworkCore.ModelMetadata Model { get; } = new();
    public void Dispose() {}

    private CompanyDataStore Store
    {
        get
        {
            if (_session.DataStore == null)
                _session.InitializeInMemory();
            return _session.DataStore!;
        }
    }

    private InMemoryDbSet<T> CreateSet<T>(List<T> list) where T : class
        => new(list, e => _session.Indexes?.OnEntityAdded(e), e => _session.Indexes?.OnEntityRemoved(e), () => _session.MarkDirty());

    public InMemoryDbSet<T> Set<T>() where T : class => CreateSet(Store.GetCollection<T>());

    // Entity collection accessors — same property names as old AppDbContext for minimal churn
    public InMemoryDbSet<Company> Companies => CreateSet(new List<Company> { Store.CompanyInfo });
    public InMemoryDbSet<FinancialYear> FinancialYears => CreateSet(Store.FinancialYears);
    public InMemoryDbSet<Group> Groups => CreateSet(Store.Groups);
    public InMemoryDbSet<Ledger> Ledgers => CreateSet(Store.Ledgers);
    public InMemoryDbSet<VoucherType> VoucherTypes => CreateSet(Store.VoucherTypes);
    public InMemoryDbSet<Voucher> Vouchers => CreateSet(Store.Vouchers);
    public InMemoryDbSet<VoucherEntry> VoucherEntries => CreateSet(Store.VoucherEntries);
    public InMemoryDbSet<BillAllocation> BillAllocations => CreateSet(Store.BillAllocations);
    public InMemoryDbSet<StockItem> StockItems => CreateSet(Store.StockItems);
    public InMemoryDbSet<Unit> Units => CreateSet(Store.Units);
    public InMemoryDbSet<User> Users => CreateSet(Store.Users);
    public InMemoryDbSet<Role> Roles => CreateSet(Store.Roles);
    public InMemoryDbSet<Permission> Permissions => CreateSet(Store.Permissions);
    public InMemoryDbSet<AuditLog> AuditLogs => CreateSet(Store.AuditLogs);
    public InMemoryDbSet<AppSetting> AppSettings => CreateSet(Store.Settings);
    public InMemoryDbSet<AppSetting> Settings => AppSettings;
    public InMemoryDbSet<BackupHistory> BackupHistories => CreateSet(Store.BackupHistories);

    /// <summary>Compatibility facade for EF Core Database operations.</summary>
    public AppDatabaseFacade Database { get; } = new AppDatabaseFacade();

    // Write operations — mutate in-memory collections and mark session dirty

    public void Add<T>(T entity) where T : class
    {
        Store.GetCollection<T>().Add(entity);
        _session.Indexes?.OnEntityAdded(entity);
        _session.MarkDirty();
    }

    public void AddRange<T>(IEnumerable<T> entities) where T : class
    {
        var collection = Store.GetCollection<T>();
        foreach (var entity in entities)
        {
            collection.Add(entity);
            _session.Indexes?.OnEntityAdded(entity);
        }
        _session.MarkDirty();
    }

    public void Remove<T>(T entity) where T : class
    {
        Store.GetCollection<T>().Remove(entity);
        _session.Indexes?.OnEntityRemoved(entity);
        _session.MarkDirty();
    }

    public void RemoveRange<T>(IEnumerable<T> entities) where T : class
    {
        var collection = Store.GetCollection<T>();
        foreach (var entity in entities.ToList())
        {
            collection.Remove(entity);
            _session.Indexes?.OnEntityRemoved(entity);
        }
        _session.MarkDirty();
    }

    public void Update<T>(T entity) where T : class
    {
        _session.Indexes?.OnEntityUpdated(entity);
        _session.MarkDirty();
    }

    /// <summary>
    /// Saves all pending changes to company.data.
    /// Replaces DbContext.SaveChangesAsync().
    /// </summary>
    public System.Threading.Tasks.Task<int> SaveChangesAsync(System.Threading.CancellationToken ct = default)
    {
        if (_session.IsDirty)
        {
            _session.Save();
            return System.Threading.Tasks.Task.FromResult(1);
        }
        return System.Threading.Tasks.Task.FromResult(0);
    }

    /// <summary>Synchronous save for legacy code paths.</summary>
    public int SaveChanges()
    {
        if (_session.IsDirty)
        {
            _session.Save();
            return 1;
        }
        return 0;
    }

    /// <summary>Gets the next auto-increment ID for an entity type.</summary>
    public int GetNextId<T>() where T : class => Store.GetNextId<T>();
}

/// <summary>
/// Backwards-compatible alias for AppDataContext.
/// Enables any legacy service or test expecting AppDbContext to work transparently in-memory.
/// </summary>
public class AppDbContext : AppDataContext
{
    public AppDbContext(CompanySession session) : base(session)
    {
    }

    public AppDbContext(Microsoft.EntityFrameworkCore.DbContextOptions<AppDbContext> options) : base(CreateTestSession())
    {
    }

    public AppDbContext(Microsoft.EntityFrameworkCore.DbContextOptions options) : base(CreateTestSession())
    {
    }

    public AppDbContext() : base(CreateTestSession())
    {
    }

    private static CompanySession CreateTestSession()
    {
        var store = new CompanyDataStore();
        store.CompanyInfo = new Company { CompanyId = 1, CompanyName = "Test Company", IsActive = true };
        store.FinancialYears.Add(new FinancialYear { FinancialYearId = 1, CompanyId = 1, YearName = "2026-2027", StartDate = new System.DateTime(2026, 4, 1), EndDate = new System.DateTime(2027, 3, 31), IsClosed = false });
        return CompanySession.CreateInMemory(store);
    }
}

/// <summary>
/// Compatibility facade for legacy _context.Database calls.
/// All methods are safe no-ops or return in-memory stubs.
/// </summary>
public class AppDatabaseFacade
{
    public bool IsRelational() => false;
    public bool CanConnect() => true;
    public System.Threading.Tasks.Task<bool> CanConnectAsync(System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.FromResult(true);
    public System.Threading.Tasks.Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(System.Threading.CancellationToken cancellationToken = default)
        => System.Threading.Tasks.Task.FromResult<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(new Microsoft.EntityFrameworkCore.Storage.InMemoryDbContextTransaction());
    public Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction BeginTransaction()
        => new Microsoft.EntityFrameworkCore.Storage.InMemoryDbContextTransaction();
    public System.Threading.Tasks.Task<int> ExecuteSqlRawAsync(string sql, System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.FromResult(0);
    public string? GetConnectionString() => null;
    public void SetConnectionString(string? connectionString) {}
    public System.Threading.Tasks.Task EnsureCreatedAsync(System.Threading.CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.CompletedTask;
}
