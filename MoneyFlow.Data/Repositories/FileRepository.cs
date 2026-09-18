using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data.Storage;

namespace MoneyFlow.Data.Repositories;

/// <summary>
/// Generic file-based repository operating against CompanyDataStore in-memory collections.
/// Replaces the EF Core Repository&lt;T&gt;.
/// All operations are in-memory (fast) — persistence is handled by CompanySession.Save().
/// </summary>
public class FileRepository<T> : IRepository<T> where T : class
{
    protected readonly CompanySession Session;

    public FileRepository(CompanySession session)
    {
        Session = session ?? throw new ArgumentNullException(nameof(session));
    }

    protected CompanyDataStore Store
    {
        get
        {
            if (Session.DataStore == null)
                Session.InitializeInMemory();
            return Session.DataStore!;
        }
    }

    protected IndexManager Indexes
    {
        get
        {
            if (Session.Indexes == null)
                Session.InitializeInMemory();
            return Session.Indexes!;
        }
    }

    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (Session.DataStore == null)
            return Task.FromResult<T?>(default);

        // Try indexed lookup first for known entity types
        var result = TryGetByIdFromIndex(id);
        if (result != null)
            return Task.FromResult<T?>(result);

        // Fallback: scan collection using reflection to find the ID property
        var collection = Store.GetCollection<T>();
        var idProp = typeof(T).GetProperty(typeof(T).Name.Replace("Entity", "") + "Id")
                    ?? typeof(T).GetProperties().FirstOrDefault(p => p.Name.EndsWith("Id") && p.PropertyType == typeof(int));

        if (idProp != null)
        {
            var entity = collection.FirstOrDefault(e => (int)(idProp.GetValue(e) ?? 0) == id);
            return Task.FromResult(entity);
        }

        return Task.FromResult<T?>(default);
    }

    public Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
    {
        if (Session.DataStore == null)
            return Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());

        var collection = Store.GetCollection<T>();
        return Task.FromResult<IReadOnlyList<T>>(collection.ToList().AsReadOnly());
    }

    public Task<IReadOnlyList<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        if (Session.DataStore == null)
            return Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());

        var collection = Store.GetCollection<T>();
        var compiled = predicate.Compile();
        var result = collection.Where(compiled).ToList().AsReadOnly();
        return Task.FromResult<IReadOnlyList<T>>(result);
    }

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        if (Session.DataStore == null)
            return Task.FromResult(false);

        var collection = Store.GetCollection<T>();
        var compiled = predicate.Compile();
        return Task.FromResult(collection.Any(compiled));
    }

    public Task AddAsync(T entity, CancellationToken ct = default)
    {
        // Auto-assign ID
        AssignId(entity);

        var collection = Store.GetCollection<T>();
        collection.Add(entity);

        // Update indexes
        Indexes.OnEntityAdded(entity);

        Session.MarkDirty();
        return Task.CompletedTask;
    }

    public void Update(T entity)
    {
        // Entity is already in the in-memory collection (same reference).
        // Just update indexes and mark dirty.
        Indexes.OnEntityUpdated(entity);
        Session.MarkDirty();
    }

    public void Delete(T entity)
    {
        var collection = Store.GetCollection<T>();
        collection.Remove(entity);

        Indexes.OnEntityRemoved(entity);
        Session.MarkDirty();
    }

    /// <summary>Tries to get an entity by ID from the IndexManager.</summary>
    private T? TryGetByIdFromIndex(int id)
    {
        var type = typeof(T);

        if (type == typeof(Core.Entities.FinancialYear)) return Indexes.GetFinancialYear(id) as T;
        if (type == typeof(Core.Entities.Group)) return Indexes.GetGroup(id) as T;
        if (type == typeof(Core.Entities.Ledger)) return Indexes.GetLedger(id) as T;
        if (type == typeof(Core.Entities.Voucher)) return Indexes.GetVoucher(id) as T;
        if (type == typeof(Core.Entities.VoucherEntry)) return Indexes.GetVoucherEntry(id) as T;
        if (type == typeof(Core.Entities.VoucherType)) return Indexes.GetVoucherType(id) as T;
        if (type == typeof(Core.Entities.BillAllocation)) return Indexes.GetBillAllocation(id) as T;
        if (type == typeof(Core.Entities.StockItem)) return Indexes.GetStockItem(id) as T;
        if (type == typeof(Core.Entities.Unit)) return Indexes.GetUnit(id) as T;
        if (type == typeof(Core.Entities.User)) return Indexes.GetUser(id) as T;
        if (type == typeof(Core.Entities.Role)) return Indexes.GetRole(id) as T;
        if (type == typeof(Core.Entities.Permission)) return Indexes.GetPermission(id) as T;

        return null;
    }

    /// <summary>Auto-assigns an ID to a new entity using CompanyDataStore counters.</summary>
    private void AssignId(T entity)
    {
        var nextId = Store.GetNextId<T>();
        var type = typeof(T);

        // Set the ID property using known entity patterns
        if (type == typeof(Core.Entities.Company))
            ((Core.Entities.Company)(object)entity).CompanyId = nextId;
        else if (type == typeof(Core.Entities.FinancialYear))
            ((Core.Entities.FinancialYear)(object)entity).FinancialYearId = nextId;
        else if (type == typeof(Core.Entities.Group))
            ((Core.Entities.Group)(object)entity).GroupId = nextId;
        else if (type == typeof(Core.Entities.Ledger))
            ((Core.Entities.Ledger)(object)entity).LedgerId = nextId;
        else if (type == typeof(Core.Entities.VoucherType))
            ((Core.Entities.VoucherType)(object)entity).VoucherTypeId = nextId;
        else if (type == typeof(Core.Entities.Voucher))
            ((Core.Entities.Voucher)(object)entity).VoucherId = nextId;
        else if (type == typeof(Core.Entities.VoucherEntry))
            ((Core.Entities.VoucherEntry)(object)entity).VoucherEntryId = nextId;
        else if (type == typeof(Core.Entities.BillAllocation))
            ((Core.Entities.BillAllocation)(object)entity).BillAllocationId = nextId;
        else if (type == typeof(Core.Entities.StockItem))
            ((Core.Entities.StockItem)(object)entity).StockItemId = nextId;
        else if (type == typeof(Core.Entities.Unit))
            ((Core.Entities.Unit)(object)entity).UnitId = nextId;
        else if (type == typeof(Core.Entities.User))
            ((Core.Entities.User)(object)entity).UserId = nextId;
        else if (type == typeof(Core.Entities.Role))
            ((Core.Entities.Role)(object)entity).RoleId = nextId;
        else if (type == typeof(Core.Entities.Permission))
            ((Core.Entities.Permission)(object)entity).PermissionId = nextId;
        else if (type == typeof(Core.Entities.AuditLog))
            ((Core.Entities.AuditLog)(object)entity).AuditLogId = nextId;
        else if (type == typeof(Core.Entities.AppSetting))
            ((Core.Entities.AppSetting)(object)entity).SettingId = nextId;
        else if (type == typeof(Core.Entities.BackupHistory))
            ((Core.Entities.BackupHistory)(object)entity).BackupHistoryId = nextId;
    }
}
