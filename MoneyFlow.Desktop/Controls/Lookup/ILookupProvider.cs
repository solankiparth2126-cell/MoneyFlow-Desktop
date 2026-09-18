using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MoneyFlow.Desktop.Controls.Lookup;

/// <summary>
/// Provider abstraction decoupling the universal lookup UI from data storage and services.
/// </summary>
public interface ILookupProvider
{
    event Action? DataChanged;

    Task<IReadOnlyList<LookupItem>> GetItemsAsync(string? searchText = null, CancellationToken ct = default);

    LookupItem? FindById(object? id);

    LookupItem? FindByName(string? name);

    void Invalidate();
}
