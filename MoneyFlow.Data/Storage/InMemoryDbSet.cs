using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace MoneyFlow.Data.Storage;

/// <summary>
/// In-memory replacement for EF Core DbSet&lt;T&gt;.
/// Provides full IQueryable LINQ support and mutator methods against in-memory entity lists.
/// </summary>
public class InMemoryDbSet<T> : IQueryable<T>, IEnumerable<T> where T : class
{
    private readonly List<T> _list;
    private readonly Action<T>? _onAdd;
    private readonly Action<T>? _onRemove;
    private readonly Action? _markDirty;

    public InMemoryDbSet(List<T> list, Action<T>? onAdd = null, Action<T>? onRemove = null, Action? markDirty = null)
    {
        _list = list;
        _onAdd = onAdd;
        _onRemove = onRemove;
        _markDirty = markDirty;
    }

    public Type ElementType => _list.AsQueryable().ElementType;
    public Expression Expression => _list.AsQueryable().Expression;
    public IQueryProvider Provider => _list.AsQueryable().Provider;

    public IEnumerator<T> GetEnumerator() => _list.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _list.GetEnumerator();

    public void Add(T entity)
    {
        _list.Add(entity);
        _onAdd?.Invoke(entity);
        _markDirty?.Invoke();
    }

    public ValueTask<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        Add(entity);
        return ValueTask.FromResult(entity);
    }

    public void AddRange(IEnumerable<T> entities)
    {
        foreach (var entity in entities)
        {
            _list.Add(entity);
            _onAdd?.Invoke(entity);
        }
        _markDirty?.Invoke();
    }

    public void AddRange(params T[] entities) => AddRange((IEnumerable<T>)entities);

    public Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        AddRange(entities);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(params T[] entities)
    {
        AddRange(entities);
        return Task.CompletedTask;
    }

    public void Remove(T entity)
    {
        _list.Remove(entity);
        _onRemove?.Invoke(entity);
        _markDirty?.Invoke();
    }

    public void RemoveRange(IEnumerable<T> entities)
    {
        foreach (var entity in entities.ToList())
        {
            _list.Remove(entity);
            _onRemove?.Invoke(entity);
        }
        _markDirty?.Invoke();
    }

    public void RemoveRange(params T[] entities) => RemoveRange((IEnumerable<T>)entities);

    public void Update(T entity)
    {
        _markDirty?.Invoke();
    }

    public void Attach(T entity)
    {
        // No-op for in-memory
    }

    public ValueTask<T?> FindAsync(params object[] keyValues)
    {
        if (keyValues == null || keyValues.Length == 0) return ValueTask.FromResult<T?>(null);
        var key = keyValues[0];
        if (key is int id)
        {
            var prop = typeof(T).GetProperty($"{typeof(T).Name}Id") ?? typeof(T).GetProperty("Id");
            if (prop != null)
            {
                var match = _list.FirstOrDefault(x =>
                {
                    var val = prop.GetValue(x);
                    return val != null && Convert.ToInt32(val) == id;
                });
                return ValueTask.FromResult(match);
            }
        }
        return ValueTask.FromResult<T?>(_list.FirstOrDefault());
    }

    public ValueTask<T?> FindAsync(object[] keyValues, CancellationToken cancellationToken)
        => FindAsync(keyValues);
}
