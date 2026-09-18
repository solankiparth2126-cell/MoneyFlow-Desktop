using System;
using System.Collections.Generic;
using System.Linq;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Data.Storage;

/// <summary>
/// Central in-memory representation of all company data.
/// This is the serialization root — everything inside gets persisted to company.data.
/// Contains all entity collections for a single company.
/// </summary>
public sealed class CompanyDataStore
{
    // Master data
    public Company CompanyInfo { get; set; } = new();
    public List<FinancialYear> FinancialYears { get; set; } = new();
    public List<Group> Groups { get; set; } = new();
    public List<Ledger> Ledgers { get; set; } = new();
    public List<VoucherType> VoucherTypes { get; set; } = new();

    // Transactions
    public List<Voucher> Vouchers { get; set; } = new();
    public List<VoucherEntry> VoucherEntries { get; set; } = new();
    public List<BillAllocation> BillAllocations { get; set; } = new();

    // Inventory
    public List<StockItem> StockItems { get; set; } = new();
    public List<Unit> Units { get; set; } = new();

    // Security & System
    public List<User> Users { get; set; } = new();
    public List<Role> Roles { get; set; } = new();
    public List<Permission> Permissions { get; set; } = new();
    public List<AuditLog> AuditLogs { get; set; } = new();
    public List<AppSetting> Settings { get; set; } = new();
    public List<BackupHistory> BackupHistories { get; set; } = new();

    // ID generation counters — used for auto-increment
    public IdCounters NextIds { get; set; } = new();

    /// <summary>
    /// Generates the next ID for a given entity type.
    /// Thread-safe via Interlocked in production use.
    /// </summary>
    public int GetNextId<T>() where T : class
    {
        var type = typeof(T);

        if (type == typeof(Company)) return System.Threading.Interlocked.Increment(ref NextIds.CompanyId);
        if (type == typeof(FinancialYear)) return System.Threading.Interlocked.Increment(ref NextIds.FinancialYearId);
        if (type == typeof(Group)) return System.Threading.Interlocked.Increment(ref NextIds.GroupId);
        if (type == typeof(Ledger)) return System.Threading.Interlocked.Increment(ref NextIds.LedgerId);
        if (type == typeof(VoucherType)) return System.Threading.Interlocked.Increment(ref NextIds.VoucherTypeId);
        if (type == typeof(Voucher)) return System.Threading.Interlocked.Increment(ref NextIds.VoucherId);
        if (type == typeof(VoucherEntry)) return System.Threading.Interlocked.Increment(ref NextIds.VoucherEntryId);
        if (type == typeof(BillAllocation)) return System.Threading.Interlocked.Increment(ref NextIds.BillAllocationId);
        if (type == typeof(StockItem)) return System.Threading.Interlocked.Increment(ref NextIds.StockItemId);
        if (type == typeof(Unit)) return System.Threading.Interlocked.Increment(ref NextIds.UnitId);
        if (type == typeof(User)) return System.Threading.Interlocked.Increment(ref NextIds.UserId);
        if (type == typeof(Role)) return System.Threading.Interlocked.Increment(ref NextIds.RoleId);
        if (type == typeof(Permission)) return System.Threading.Interlocked.Increment(ref NextIds.PermissionId);
        if (type == typeof(AuditLog)) return (int)System.Threading.Interlocked.Increment(ref NextIds.AuditLogId);
        if (type == typeof(AppSetting)) return System.Threading.Interlocked.Increment(ref NextIds.SettingId);
        if (type == typeof(BackupHistory)) return System.Threading.Interlocked.Increment(ref NextIds.BackupHistoryId);

        throw new InvalidOperationException($"No ID counter for entity type {type.Name}.");
    }

    /// <summary>
    /// Recalculates all ID counters from the maximum existing IDs in each collection.
    /// Called after loading/deserializing company data.
    /// </summary>
    public void RecalculateIdCounters()
    {
        NextIds.CompanyId = CompanyInfo?.CompanyId ?? 0;
        NextIds.FinancialYearId = FinancialYears.Count > 0 ? FinancialYears.Max(x => x.FinancialYearId) : 0;
        NextIds.GroupId = Groups.Count > 0 ? Groups.Max(x => x.GroupId) : 0;
        NextIds.LedgerId = Ledgers.Count > 0 ? Ledgers.Max(x => x.LedgerId) : 0;
        NextIds.VoucherTypeId = VoucherTypes.Count > 0 ? VoucherTypes.Max(x => x.VoucherTypeId) : 0;
        NextIds.VoucherId = Vouchers.Count > 0 ? Vouchers.Max(x => x.VoucherId) : 0;
        NextIds.VoucherEntryId = VoucherEntries.Count > 0 ? VoucherEntries.Max(x => x.VoucherEntryId) : 0;
        NextIds.BillAllocationId = BillAllocations.Count > 0 ? BillAllocations.Max(x => x.BillAllocationId) : 0;
        NextIds.StockItemId = StockItems.Count > 0 ? StockItems.Max(x => x.StockItemId) : 0;
        NextIds.UnitId = Units.Count > 0 ? Units.Max(x => x.UnitId) : 0;
        NextIds.UserId = Users.Count > 0 ? Users.Max(x => x.UserId) : 0;
        NextIds.RoleId = Roles.Count > 0 ? Roles.Max(x => x.RoleId) : 0;
        NextIds.PermissionId = Permissions.Count > 0 ? Permissions.Max(x => x.PermissionId) : 0;
        NextIds.AuditLogId = AuditLogs.Count > 0 ? AuditLogs.Max(x => x.AuditLogId) : 0;
        NextIds.SettingId = Settings.Count > 0 ? Settings.Max(x => x.SettingId) : 0;
        NextIds.BackupHistoryId = BackupHistories.Count > 0 ? BackupHistories.Max(x => x.BackupHistoryId) : 0;
    }

    /// <summary>
    /// Gets the entity list for a given type. Used by the generic repository.
    /// </summary>
    public List<T> GetCollection<T>() where T : class
    {
        var type = typeof(T);

        if (type == typeof(Company)) return new List<T> { (T)(object)CompanyInfo };
        if (type == typeof(FinancialYear)) return (List<T>)(object)FinancialYears;
        if (type == typeof(Group)) return (List<T>)(object)Groups;
        if (type == typeof(Ledger)) return (List<T>)(object)Ledgers;
        if (type == typeof(VoucherType)) return (List<T>)(object)VoucherTypes;
        if (type == typeof(Voucher)) return (List<T>)(object)Vouchers;
        if (type == typeof(VoucherEntry)) return (List<T>)(object)VoucherEntries;
        if (type == typeof(BillAllocation)) return (List<T>)(object)BillAllocations;
        if (type == typeof(StockItem)) return (List<T>)(object)StockItems;
        if (type == typeof(Unit)) return (List<T>)(object)Units;
        if (type == typeof(User)) return (List<T>)(object)Users;
        if (type == typeof(Role)) return (List<T>)(object)Roles;
        if (type == typeof(Permission)) return (List<T>)(object)Permissions;
        if (type == typeof(AuditLog)) return (List<T>)(object)AuditLogs;
        if (type == typeof(AppSetting)) return (List<T>)(object)Settings;
        if (type == typeof(BackupHistory)) return (List<T>)(object)BackupHistories;

        throw new InvalidOperationException($"No collection for entity type {type.Name}.");
    }
}

/// <summary>
/// Auto-increment ID counters for all entity types.
/// Persisted alongside company data so IDs are never reused.
/// </summary>
public sealed class IdCounters
{
    public int CompanyId;
    public int FinancialYearId;
    public int GroupId;
    public int LedgerId;
    public int VoucherTypeId;
    public int VoucherId;
    public int VoucherEntryId;
    public int BillAllocationId;
    public int StockItemId;
    public int UnitId;
    public int UserId;
    public int RoleId;
    public int PermissionId;
    public long AuditLogId;
    public int SettingId;
    public int BackupHistoryId;
}
