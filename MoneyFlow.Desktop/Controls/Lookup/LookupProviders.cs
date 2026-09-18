using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Controls.Lookup;

/// <summary>
/// In-memory generic lookup provider adaptable to any collection of entities.
/// </summary>
public class ListLookupProvider<T> : ILookupProvider
{
    private readonly Func<CancellationToken, Task<IEnumerable<T>>>? _asyncLoader;
    private readonly Func<T, object?> _idSelector;
    private readonly Func<T, string> _nameSelector;
    private readonly Func<T, string?>? _codeSelector;
    private readonly Func<T, string?>? _subtitleSelector;
    private readonly Func<T, object?>? _parentIdSelector;
    private readonly Func<T, int>? _levelSelector;
    private readonly Func<T, bool>? _hasChildrenSelector;
    private List<LookupItem> _cachedItems = new();
    private bool _isLoaded;

    public event Action? DataChanged;

    public ListLookupProvider(
        IEnumerable<T> items,
        Func<T, object?> idSelector,
        Func<T, string> nameSelector,
        Func<T, string?>? codeSelector = null,
        Func<T, string?>? subtitleSelector = null,
        Func<T, object?>? parentIdSelector = null,
        Func<T, int>? levelSelector = null,
        Func<T, bool>? hasChildrenSelector = null)
    {
        _idSelector = idSelector;
        _nameSelector = nameSelector;
        _codeSelector = codeSelector;
        _subtitleSelector = subtitleSelector;
        _parentIdSelector = parentIdSelector;
        _levelSelector = levelSelector;
        _hasChildrenSelector = hasChildrenSelector;
        SetItems(items);
    }

    public ListLookupProvider(
        Func<CancellationToken, Task<IEnumerable<T>>> asyncLoader,
        Func<T, object?> idSelector,
        Func<T, string> nameSelector,
        Func<T, string?>? codeSelector = null,
        Func<T, string?>? subtitleSelector = null,
        Func<T, object?>? parentIdSelector = null,
        Func<T, int>? levelSelector = null,
        Func<T, bool>? hasChildrenSelector = null)
    {
        _asyncLoader = asyncLoader;
        _idSelector = idSelector;
        _nameSelector = nameSelector;
        _codeSelector = codeSelector;
        _subtitleSelector = subtitleSelector;
        _parentIdSelector = parentIdSelector;
        _levelSelector = levelSelector;
        _hasChildrenSelector = hasChildrenSelector;
    }

    public void SetItems(IEnumerable<T> items)
    {
        _cachedItems = items.Select(x => new LookupItem
        {
            Id = _idSelector(x),
            Name = _nameSelector(x),
            Code = _codeSelector?.Invoke(x),
            Subtitle = _subtitleSelector?.Invoke(x),
            ParentId = _parentIdSelector?.Invoke(x),
            Level = _levelSelector?.Invoke(x) ?? 0,
            HasChildren = _hasChildrenSelector?.Invoke(x) ?? false,
            RawData = x
        }).ToList();

        _isLoaded = true;
        DataChanged?.Invoke();
    }

    public async Task<IReadOnlyList<LookupItem>> GetItemsAsync(string? searchText = null, CancellationToken ct = default)
    {
        if (!_isLoaded && _asyncLoader != null)
        {
            var raw = await _asyncLoader(ct);
            SetItems(raw);
        }

        if (string.IsNullOrWhiteSpace(searchText))
            return _cachedItems;

        return _cachedItems.Where(i => i.Matches(searchText)).ToList();
    }

    public LookupItem? FindById(object? id)
    {
        if (id == null) return null;
        return _cachedItems.FirstOrDefault(x => Equals(x.Id, id) || string.Equals(x.Id?.ToString(), id.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    public LookupItem? FindByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return _cachedItems.FirstOrDefault(x => string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public void Invalidate()
    {
        _isLoaded = false;
        DataChanged?.Invoke();
    }
}

/// <summary>
/// Specialized provider for Groups that flattens tree nodes into hierarchical LookupItems with levels.
/// </summary>
public class LedgerGroupLookupProvider : ILookupProvider
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<GroupTreeNodeDto>>> _treeLoader;
    private List<LookupItem> _cachedItems = new();
    private bool _isLoaded;

    public event Action? DataChanged;

    public LedgerGroupLookupProvider(Func<CancellationToken, Task<IReadOnlyList<GroupTreeNodeDto>>> treeLoader)
    {
        _treeLoader = treeLoader;
    }

    public LedgerGroupLookupProvider(IGroupService groupService, Func<int> companyIdProvider)
    {
        _treeLoader = async (ct) =>
        {
            int cid = companyIdProvider();
            if (cid <= 0) return Array.Empty<GroupTreeNodeDto>();
            return await groupService.GetGroupTreeAsync(cid, ct);
        };
    }

    public async Task<IReadOnlyList<LookupItem>> GetItemsAsync(string? searchText = null, CancellationToken ct = default)
    {
        if (!_isLoaded)
        {
            var trees = await _treeLoader(ct);
            var list = new List<LookupItem>();
            foreach (var root in trees)
            {
                FlattenNode(root, 0, list);
            }
            _cachedItems = list;
            _isLoaded = true;
        }

        if (string.IsNullOrWhiteSpace(searchText))
            return _cachedItems;

        return _cachedItems.Where(i => i.Matches(searchText)).ToList();
    }

    private static void FlattenNode(GroupTreeNodeDto node, int level, List<LookupItem> list)
    {
        bool hasKids = node.Children != null && node.Children.Count > 0;
        string natureCode = node.Nature.ToString() switch
        {
            "Assets" => "ASST",
            "Liabilities" => "LIAB",
            "Income" => "INCM",
            "Expenses" => "EXPN",
            _ => node.Nature.ToString()
        };

        list.Add(new LookupItem
        {
            Id = node.GroupId,
            Name = node.GroupName,
            Code = natureCode,
            Subtitle = node.Nature.ToString(),
            ParentId = node.ParentGroupId,
            Level = level,
            HasChildren = hasKids,
            IsExpanded = true,
            RawData = node
        });

        if (hasKids)
        {
            foreach (var child in node.Children!)
            {
                FlattenNode(child, level + 1, list);
            }
        }
    }

    public LookupItem? FindById(object? id)
    {
        if (id == null) return null;
        return _cachedItems.FirstOrDefault(x => Equals(x.Id, id) || string.Equals(x.Id?.ToString(), id.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    public LookupItem? FindByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return _cachedItems.FirstOrDefault(x => string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public void Invalidate()
    {
        _isLoaded = false;
        DataChanged?.Invoke();
    }
}

/// <summary>
/// Specialized provider for Ledgers displaying ledger name, group subtitle, and opening balances.
/// </summary>
public class LedgerLookupProvider : ILookupProvider
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<LedgerSummaryDto>>> _loader;
    private List<LookupItem> _cachedItems = new();
    private bool _isLoaded;

    public event Action? DataChanged;

    public LedgerLookupProvider(Func<CancellationToken, Task<IReadOnlyList<LedgerSummaryDto>>> loader)
    {
        _loader = loader;
    }

    public LedgerLookupProvider(ILedgerService ledgerService, Func<int> companyIdProvider, int? groupIdFilter = null)
    {
        _loader = async (ct) =>
        {
            int cid = companyIdProvider();
            if (cid <= 0) return Array.Empty<LedgerSummaryDto>();
            return await ledgerService.GetLedgersByCompanyAsync(cid, null, groupIdFilter, ct);
        };
    }

    public async Task<IReadOnlyList<LookupItem>> GetItemsAsync(string? searchText = null, CancellationToken ct = default)
    {
        if (!_isLoaded)
        {
            var raw = await _loader(ct);
            _cachedItems = raw.Select(l => new LookupItem
            {
                Id = l.LedgerId,
                Name = l.LedgerName,
                Code = l.FormattedOpeningBalance,
                Subtitle = l.DisplayGroup,
                Level = 0,
                RawData = l
            }).ToList();
            _isLoaded = true;
        }

        if (string.IsNullOrWhiteSpace(searchText))
            return _cachedItems;

        return _cachedItems.Where(i => i.Matches(searchText)).ToList();
    }

    public LookupItem? FindById(object? id)
    {
        if (id == null) return null;
        return _cachedItems.FirstOrDefault(x => Equals(x.Id, id) || string.Equals(x.Id?.ToString(), id.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    public LookupItem? FindByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return _cachedItems.FirstOrDefault(x => string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public void Invalidate()
    {
        _isLoaded = false;
        DataChanged?.Invoke();
    }
}

/// <summary>
/// Generic provider for Enum values (e.g. GroupNature, BalanceType).
/// </summary>
public class EnumLookupProvider<TEnum> : ListLookupProvider<TEnum> where TEnum : struct, Enum
{
    public EnumLookupProvider()
        : base(
            Enum.GetValues<TEnum>(),
            e => e,
            e => e.ToString(),
            e => Convert.ToInt32(e).ToString())
    {
    }

    public EnumLookupProvider(Func<TEnum, string> displayNameSelector)
        : base(
            Enum.GetValues<TEnum>(),
            e => e,
            displayNameSelector,
            e => null)
    {
    }
}
