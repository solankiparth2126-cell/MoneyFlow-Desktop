using System;
using System.Collections.Generic;
using System.Linq;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Data.Storage;

/// <summary>
/// Maintains in-memory indexes for fast lookups on frequently accessed entities.
/// Indexes are built on company open and incrementally updated on CRUD operations.
/// </summary>
public sealed class IndexManager
{
    // Primary key indexes
    private readonly Dictionary<int, FinancialYear> _financialYearsById = new();
    private readonly Dictionary<int, Group> _groupsById = new();
    private readonly Dictionary<int, Ledger> _ledgersById = new();
    private readonly Dictionary<int, Voucher> _vouchersById = new();
    private readonly Dictionary<int, VoucherEntry> _voucherEntriesById = new();
    private readonly Dictionary<int, VoucherType> _voucherTypesById = new();
    private readonly Dictionary<int, BillAllocation> _billAllocationsById = new();
    private readonly Dictionary<int, StockItem> _stockItemsById = new();
    private readonly Dictionary<int, Unit> _unitsById = new();
    private readonly Dictionary<int, User> _usersById = new();
    private readonly Dictionary<int, Role> _rolesById = new();
    private readonly Dictionary<int, Permission> _permissionsById = new();

    // Search indexes (case-insensitive)
    private readonly Dictionary<string, Ledger> _ledgersByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Group> _groupsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, User> _usersByUsername = new(StringComparer.OrdinalIgnoreCase);

    // Foreign key indexes
    private readonly Dictionary<int, List<Voucher>> _vouchersByCompanyFy = new(); // key = financialYearId
    private readonly Dictionary<int, List<VoucherEntry>> _entriesByVoucherId = new();
    private readonly Dictionary<int, List<Ledger>> _ledgersByGroupId = new();
    private readonly Dictionary<int, List<BillAllocation>> _billAllocationsByEntryId = new();

    /// <summary>
    /// Builds all indexes from a loaded CompanyDataStore.
    /// Call once after company data is loaded/deserialized.
    /// </summary>
    public void BuildIndexes(CompanyDataStore store)
    {
        Clear();

        foreach (var fy in store.FinancialYears) _financialYearsById[fy.FinancialYearId] = fy;
        foreach (var g in store.Groups) { _groupsById[g.GroupId] = g; _groupsByName[g.GroupName] = g; }
        foreach (var l in store.Ledgers) { _ledgersById[l.LedgerId] = l; _ledgersByName[l.LedgerName] = l; }
        foreach (var v in store.Vouchers) _vouchersById[v.VoucherId] = v;
        foreach (var ve in store.VoucherEntries) _voucherEntriesById[ve.VoucherEntryId] = ve;
        foreach (var vt in store.VoucherTypes) _voucherTypesById[vt.VoucherTypeId] = vt;
        foreach (var ba in store.BillAllocations) _billAllocationsById[ba.BillAllocationId] = ba;
        foreach (var si in store.StockItems) _stockItemsById[si.StockItemId] = si;
        foreach (var u in store.Units) _unitsById[u.UnitId] = u;
        foreach (var usr in store.Users) { _usersById[usr.UserId] = usr; _usersByUsername[usr.Username] = usr; }
        foreach (var r in store.Roles) _rolesById[r.RoleId] = r;
        foreach (var p in store.Permissions) _permissionsById[p.PermissionId] = p;

        // Build foreign-key indexes
        foreach (var v in store.Vouchers)
        {
            if (!_vouchersByCompanyFy.TryGetValue(v.FinancialYearId, out var list))
            {
                list = new List<Voucher>();
                _vouchersByCompanyFy[v.FinancialYearId] = list;
            }
            list.Add(v);
        }

        foreach (var ve in store.VoucherEntries)
        {
            if (!_entriesByVoucherId.TryGetValue(ve.VoucherId, out var list))
            {
                list = new List<VoucherEntry>();
                _entriesByVoucherId[ve.VoucherId] = list;
            }
            list.Add(ve);
        }

        foreach (var l in store.Ledgers)
        {
            if (!_ledgersByGroupId.TryGetValue(l.GroupId, out var list))
            {
                list = new List<Ledger>();
                _ledgersByGroupId[l.GroupId] = list;
            }
            list.Add(l);
        }

        foreach (var ba in store.BillAllocations)
        {
            if (!_billAllocationsByEntryId.TryGetValue(ba.VoucherEntryId, out var list))
            {
                list = new List<BillAllocation>();
                _billAllocationsByEntryId[ba.VoucherEntryId] = list;
            }
            list.Add(ba);
        }
    }

    /// <summary>Clears all indexes.</summary>
    public void Clear()
    {
        _financialYearsById.Clear();
        _groupsById.Clear();
        _groupsByName.Clear();
        _ledgersById.Clear();
        _ledgersByName.Clear();
        _vouchersById.Clear();
        _voucherEntriesById.Clear();
        _voucherTypesById.Clear();
        _billAllocationsById.Clear();
        _stockItemsById.Clear();
        _unitsById.Clear();
        _usersById.Clear();
        _usersByUsername.Clear();
        _rolesById.Clear();
        _permissionsById.Clear();
        _vouchersByCompanyFy.Clear();
        _entriesByVoucherId.Clear();
        _ledgersByGroupId.Clear();
        _billAllocationsByEntryId.Clear();
    }

    // --- Primary key lookups ---
    public FinancialYear? GetFinancialYear(int id) => _financialYearsById.GetValueOrDefault(id);
    public Group? GetGroup(int id) => _groupsById.GetValueOrDefault(id);
    public Ledger? GetLedger(int id) => _ledgersById.GetValueOrDefault(id);
    public Voucher? GetVoucher(int id) => _vouchersById.GetValueOrDefault(id);
    public VoucherEntry? GetVoucherEntry(int id) => _voucherEntriesById.GetValueOrDefault(id);
    public VoucherType? GetVoucherType(int id) => _voucherTypesById.GetValueOrDefault(id);
    public BillAllocation? GetBillAllocation(int id) => _billAllocationsById.GetValueOrDefault(id);
    public StockItem? GetStockItem(int id) => _stockItemsById.GetValueOrDefault(id);
    public Unit? GetUnit(int id) => _unitsById.GetValueOrDefault(id);
    public User? GetUser(int id) => _usersById.GetValueOrDefault(id);
    public Role? GetRole(int id) => _rolesById.GetValueOrDefault(id);
    public Permission? GetPermission(int id) => _permissionsById.GetValueOrDefault(id);

    // --- Name-based lookups ---
    public Ledger? GetLedgerByName(string name) => _ledgersByName.GetValueOrDefault(name);
    public Group? GetGroupByName(string name) => _groupsByName.GetValueOrDefault(name);
    public User? GetUserByUsername(string username) => _usersByUsername.GetValueOrDefault(username);

    // --- Foreign key lookups ---
    public List<Voucher> GetVouchersByFinancialYear(int fyId) => _vouchersByCompanyFy.GetValueOrDefault(fyId) ?? new();
    public List<VoucherEntry> GetEntriesByVoucher(int voucherId) => _entriesByVoucherId.GetValueOrDefault(voucherId) ?? new();
    public List<Ledger> GetLedgersByGroup(int groupId) => _ledgersByGroupId.GetValueOrDefault(groupId) ?? new();
    public List<BillAllocation> GetBillAllocationsByEntry(int entryId) => _billAllocationsByEntryId.GetValueOrDefault(entryId) ?? new();

    // --- Incremental index updates ---

    public void OnEntityAdded<T>(T entity) where T : class
    {
        switch (entity)
        {
            case FinancialYear fy: _financialYearsById[fy.FinancialYearId] = fy; break;
            case Group g: _groupsById[g.GroupId] = g; _groupsByName[g.GroupName] = g; break;
            case Ledger l:
                _ledgersById[l.LedgerId] = l;
                _ledgersByName[l.LedgerName] = l;
                if (!_ledgersByGroupId.TryGetValue(l.GroupId, out var lgList)) { lgList = new(); _ledgersByGroupId[l.GroupId] = lgList; }
                lgList.Add(l);
                break;
            case Voucher v:
                _vouchersById[v.VoucherId] = v;
                if (!_vouchersByCompanyFy.TryGetValue(v.FinancialYearId, out var vList)) { vList = new(); _vouchersByCompanyFy[v.FinancialYearId] = vList; }
                vList.Add(v);
                break;
            case VoucherEntry ve:
                _voucherEntriesById[ve.VoucherEntryId] = ve;
                if (!_entriesByVoucherId.TryGetValue(ve.VoucherId, out var veList)) { veList = new(); _entriesByVoucherId[ve.VoucherId] = veList; }
                veList.Add(ve);
                break;
            case VoucherType vt: _voucherTypesById[vt.VoucherTypeId] = vt; break;
            case BillAllocation ba:
                _billAllocationsById[ba.BillAllocationId] = ba;
                if (!_billAllocationsByEntryId.TryGetValue(ba.VoucherEntryId, out var baList)) { baList = new(); _billAllocationsByEntryId[ba.VoucherEntryId] = baList; }
                baList.Add(ba);
                break;
            case StockItem si: _stockItemsById[si.StockItemId] = si; break;
            case Unit u: _unitsById[u.UnitId] = u; break;
            case User usr: _usersById[usr.UserId] = usr; _usersByUsername[usr.Username] = usr; break;
            case Role r: _rolesById[r.RoleId] = r; break;
            case Permission p: _permissionsById[p.PermissionId] = p; break;
        }
    }

    public void OnEntityRemoved<T>(T entity) where T : class
    {
        switch (entity)
        {
            case FinancialYear fy: _financialYearsById.Remove(fy.FinancialYearId); break;
            case Group g: _groupsById.Remove(g.GroupId); _groupsByName.Remove(g.GroupName); break;
            case Ledger l:
                _ledgersById.Remove(l.LedgerId);
                _ledgersByName.Remove(l.LedgerName);
                if (_ledgersByGroupId.TryGetValue(l.GroupId, out var lgList)) lgList.Remove(l);
                break;
            case Voucher v:
                _vouchersById.Remove(v.VoucherId);
                if (_vouchersByCompanyFy.TryGetValue(v.FinancialYearId, out var vList)) vList.Remove(v);
                break;
            case VoucherEntry ve:
                _voucherEntriesById.Remove(ve.VoucherEntryId);
                if (_entriesByVoucherId.TryGetValue(ve.VoucherId, out var veList)) veList.Remove(ve);
                break;
            case VoucherType vt: _voucherTypesById.Remove(vt.VoucherTypeId); break;
            case BillAllocation ba:
                _billAllocationsById.Remove(ba.BillAllocationId);
                if (_billAllocationsByEntryId.TryGetValue(ba.VoucherEntryId, out var baList)) baList.Remove(ba);
                break;
            case StockItem si: _stockItemsById.Remove(si.StockItemId); break;
            case Unit u: _unitsById.Remove(u.UnitId); break;
            case User usr: _usersById.Remove(usr.UserId); _usersByUsername.Remove(usr.Username); break;
            case Role r: _rolesById.Remove(r.RoleId); break;
            case Permission p: _permissionsById.Remove(p.PermissionId); break;
        }
    }

    public void OnEntityUpdated<T>(T entity) where T : class
    {
        // For most entities, the ID-based index entry is already correct (same reference).
        // We need to update name-based indexes in case the name changed.
        switch (entity)
        {
            case Group g: _groupsById[g.GroupId] = g; RebuildGroupNameIndex(g); break;
            case Ledger l: _ledgersById[l.LedgerId] = l; RebuildLedgerNameIndex(l); break;
            case User usr: _usersById[usr.UserId] = usr; RebuildUserNameIndex(usr); break;
        }
    }

    private void RebuildGroupNameIndex(Group g)
    {
        // Remove old name entry if name changed
        var staleKey = _groupsByName.FirstOrDefault(kv => kv.Value.GroupId == g.GroupId && !kv.Key.Equals(g.GroupName, StringComparison.OrdinalIgnoreCase)).Key;
        if (staleKey != null) _groupsByName.Remove(staleKey);
        _groupsByName[g.GroupName] = g;
    }

    private void RebuildLedgerNameIndex(Ledger l)
    {
        var staleKey = _ledgersByName.FirstOrDefault(kv => kv.Value.LedgerId == l.LedgerId && !kv.Key.Equals(l.LedgerName, StringComparison.OrdinalIgnoreCase)).Key;
        if (staleKey != null) _ledgersByName.Remove(staleKey);
        _ledgersByName[l.LedgerName] = l;
    }

    private void RebuildUserNameIndex(User u)
    {
        var staleKey = _usersByUsername.FirstOrDefault(kv => kv.Value.UserId == u.UserId && !kv.Key.Equals(u.Username, StringComparison.OrdinalIgnoreCase)).Key;
        if (staleKey != null) _usersByUsername.Remove(staleKey);
        _usersByUsername[u.Username] = u;
    }
}
