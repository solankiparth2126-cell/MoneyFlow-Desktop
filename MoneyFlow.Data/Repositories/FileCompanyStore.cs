using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Data.Repositories;

public interface ICompanyDataStore
{
    CompanyData? CurrentData { get; }
    bool IsLoaded { get; }
    string? CurrentFilePath { get; }
    byte[]? CurrentDek { get; }

    void SetActiveCompanyData(string filePath, CompanyData data, byte[] dek);
    void Clear();
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    List<T> GetEntityList<T>() where T : class;
}

public class CompanyDataStore : ICompanyDataStore
{
    private readonly ICompanyStorageEngine _storageEngine;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public CompanyData? CurrentData { get; private set; }
    public bool IsLoaded => CurrentData != null;
    public string? CurrentFilePath { get; private set; }
    public byte[]? CurrentDek { get; private set; }

    public CompanyDataStore(ICompanyStorageEngine storageEngine)
    {
        _storageEngine = storageEngine;
    }

    public void SetActiveCompanyData(string filePath, CompanyData data, byte[] dek)
    {
        CurrentFilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        CurrentData = data ?? throw new ArgumentNullException(nameof(data));
        CurrentDek = dek ?? throw new ArgumentNullException(nameof(dek));
    }

    public void Clear()
    {
        CurrentData = null;
        CurrentFilePath = null;
        CurrentDek = null;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (CurrentData == null || string.IsNullOrEmpty(CurrentFilePath) || CurrentDek == null)
        {
            return 0;
        }

        await _saveLock.WaitAsync(ct);
        try
        {
            await _storageEngine.SaveCompanyDataAsync(CurrentFilePath, CurrentData, CurrentDek, null, ct);
            return 1;
        }
        finally
        {
            _saveLock.Release();
        }
    }

    public List<T> GetEntityList<T>() where T : class
    {
        if (CurrentData == null)
        {
            return new List<T>();
        }

        var type = typeof(T);

        if (type == typeof(Company))
        {
            return (List<T>)(object)new List<Company> { CurrentData.Company };
        }
        if (type == typeof(FinancialYear))
        {
            return (List<T>)(object)CurrentData.FinancialYears;
        }
        if (type == typeof(Group))
        {
            return (List<T>)(object)CurrentData.Groups;
        }
        if (type == typeof(Ledger))
        {
            return (List<T>)(object)CurrentData.Ledgers;
        }
        if (type == typeof(VoucherType))
        {
            return (List<T>)(object)CurrentData.VoucherTypes;
        }
        if (type == typeof(Voucher))
        {
            return (List<T>)(object)CurrentData.Vouchers;
        }
        if (type == typeof(VoucherEntry))
        {
            return (List<T>)(object)CurrentData.VoucherEntries;
        }
        if (type == typeof(BillAllocation))
        {
            return (List<T>)(object)CurrentData.BillAllocations;
        }
        if (type == typeof(StockItem))
        {
            return (List<T>)(object)CurrentData.StockItems;
        }
        if (type == typeof(Unit))
        {
            return (List<T>)(object)CurrentData.Units;
        }
        if (type == typeof(User))
        {
            return (List<T>)(object)CurrentData.Users;
        }
        if (type == typeof(Role))
        {
            return (List<T>)(object)CurrentData.Roles;
        }
        if (type == typeof(Permission))
        {
            return (List<T>)(object)CurrentData.Permissions;
        }
        if (type == typeof(AuditLog))
        {
            return (List<T>)(object)CurrentData.AuditLogs;
        }
        if (type == typeof(AppSetting))
        {
            return (List<T>)(object)CurrentData.Settings;
        }
        if (type == typeof(BackupHistory))
        {
            return (List<T>)(object)CurrentData.BackupHistories;
        }

        throw new NotSupportedException($"Entity type {type.Name} is not mapped in CompanyDataStore.");
    }
}
