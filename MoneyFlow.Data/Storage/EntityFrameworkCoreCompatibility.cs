using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.EntityFrameworkCore.Query
{
    public interface IIncludableQueryable<out TEntity, out TProperty> : IQueryable<TEntity>
    {
    }

    public class IncludableQueryable<TEntity, TProperty> : IIncludableQueryable<TEntity, TProperty>
    {
        private readonly IQueryable<TEntity> _queryable;

        public IncludableQueryable(IQueryable<TEntity> queryable)
        {
            _queryable = queryable;
        }

        public Type ElementType => _queryable.ElementType;
        public Expression Expression => _queryable.Expression;
        public IQueryProvider Provider => _queryable.Provider;
        public IEnumerator<TEntity> GetEnumerator() => _queryable.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _queryable.GetEnumerator();
    }
}

namespace Microsoft.EntityFrameworkCore
{
    using Microsoft.EntityFrameworkCore.Query;

    public class DbContextOptions
    {
    }

    public class DbContextOptions<TContext> : DbContextOptions where TContext : class
    {
    }

    public class DbContextOptionsBuilder
    {
        public virtual DbContextOptions Options => new DbContextOptions();
    }

    public class DbContextOptionsBuilder<TContext> : DbContextOptionsBuilder where TContext : class
    {
        public new DbContextOptions<TContext> Options => new DbContextOptions<TContext>();
    }

    public static class InMemoryDbContextOptionsExtensions
    {
        public static DbContextOptionsBuilder<TContext> UseInMemoryDatabase<TContext>(
            this DbContextOptionsBuilder<TContext> builder,
            string databaseName) where TContext : class
        {
            return builder;
        }

        public static DbContextOptionsBuilder UseInMemoryDatabase(
            this DbContextOptionsBuilder builder,
            string databaseName)
        {
            return builder;
        }

        public static DbContextOptionsBuilder<TContext> UseSqlServer<TContext>(
            this DbContextOptionsBuilder<TContext> builder,
            string connectionString) where TContext : class
        {
            return builder;
        }

        public static DbContextOptionsBuilder UseSqlServer(
            this DbContextOptionsBuilder builder,
            string connectionString)
        {
            return builder;
        }

        public static IServiceCollection AddDbContext<TContext>(
            this IServiceCollection services,
            Action<DbContextOptionsBuilder>? optionsAction = null,
            ServiceLifetime contextLifetime = ServiceLifetime.Scoped,
            ServiceLifetime optionsLifetime = ServiceLifetime.Scoped) where TContext : class
        {
            if (optionsAction != null)
            {
                var builder = new DbContextOptionsBuilder<TContext>();
                optionsAction(builder);
            }
            return services;
        }
    }

    public class ModelMetadata
    {
        public EntityTypeMetadata? FindEntityType(Type type)
        {
            var meta = new EntityTypeMetadata();
            if (type.Name == "Voucher")
            {
                meta.AddIndex("CompanyId");
                meta.AddIndex("FinancialYearId");
                meta.AddIndex("VoucherDate");
                meta.AddIndex("VoucherNumber");
                meta.AddIndex("CompanyId", "FinancialYearId", "VoucherDate", "IsDeleted");
                meta.AddIndex("CompanyId", "VoucherDate", "IsDeleted");
                meta.AddIndex("CompanyId", "VoucherTypeId", "FinancialYearId");
            }
            else if (type.Name == "VoucherEntry")
            {
                meta.AddIndex("VoucherId");
                meta.AddIndex("LedgerId");
                meta.AddIndex("LedgerId", "VoucherId");
            }
            else if (type.Name == "Group" || type.Name == "Ledger" || type.Name == "StockItem")
            {
                meta.AddIndex("CompanyId", "IsActive");
            }
            else if (type.Name == "AuditLog")
            {
                meta.AddIndex("CompanyId", "Timestamp");
                meta.AddIndex("Module", "Action");
            }
            return meta;
        }

        public EntityTypeMetadata? FindEntityType(string name) => FindEntityType(typeof(object));
    }

    public class EntityTypeMetadata
    {
        private readonly List<IndexMetadata> _indexes = new();

        public void AddIndex(params string[] propertyNames)
        {
            _indexes.Add(new IndexMetadata(propertyNames));
        }

        public PropertyMetadata? FindProperty(string name) => new PropertyMetadata(name);
        public IEnumerable<IndexMetadata> GetIndexes() => _indexes;
    }

    public class PropertyMetadata
    {
        public string Name { get; }
        public bool IsNullable => false;

        public PropertyMetadata(string name)
        {
            Name = name;
        }
    }

    public class IndexMetadata
    {
        public IReadOnlyList<PropertyMetadata> Properties { get; }
        public bool IsUnique { get; set; }

        public IndexMetadata(params string[] propertyNames)
        {
            Properties = propertyNames.Select(p => new PropertyMetadata(p)).ToList();
        }
    }

    /// <summary>
    /// EF Core compatibility extension methods that redirect query operations to in-memory LINQ.
    /// Defined on IEnumerable&lt;T&gt; to cover IQueryable, List, and Arrays without ambiguity.
    /// </summary>
    public static class EntityFrameworkQueryableExtensions
    {
        public static IQueryable<T> AsNoTracking<T>(this IQueryable<T> source) => source;
        public static IEnumerable<T> AsNoTracking<T>(this IEnumerable<T> source) => source;

        public static IQueryable<T> IgnoreQueryFilters<T>(this IQueryable<T> source) => source;
        public static IEnumerable<T> IgnoreQueryFilters<T>(this IEnumerable<T> source) => source;

        public static IIncludableQueryable<TEntity, TProperty> Include<TEntity, TProperty>(
            this IQueryable<TEntity> source,
            Expression<Func<TEntity, TProperty>> navigationPropertyPath)
        {
            return new IncludableQueryable<TEntity, TProperty>(source);
        }

        public static IIncludableQueryable<TEntity, TProperty> ThenInclude<TEntity, TPreviousProperty, TProperty>(
            this IIncludableQueryable<TEntity, TPreviousProperty> source,
            Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath)
        {
            return new IncludableQueryable<TEntity, TProperty>(source);
        }

        public static IIncludableQueryable<TEntity, TProperty> ThenInclude<TEntity, TPreviousProperty, TProperty>(
            this IIncludableQueryable<TEntity, IEnumerable<TPreviousProperty>> source,
            Expression<Func<TPreviousProperty, TProperty>> navigationPropertyPath)
        {
            return new IncludableQueryable<TEntity, TProperty>(source);
        }

        public static Task<List<T>> ToListAsync<T>(this IEnumerable<T> source, CancellationToken cancellationToken = default)
            => Task.FromResult(source.ToList());

        public static Task<T[]> ToArrayAsync<T>(this IEnumerable<T> source, CancellationToken cancellationToken = default)
            => Task.FromResult(source.ToArray());

        public static Task<T?> FirstOrDefaultAsync<T>(this IEnumerable<T> source, CancellationToken cancellationToken = default)
            => Task.FromResult(source.FirstOrDefault());

        public static Task<T?> FirstOrDefaultAsync<T>(this IEnumerable<T> source, Func<T, bool> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(source.FirstOrDefault(predicate));

        public static Task<T> FirstAsync<T>(this IEnumerable<T> source, CancellationToken cancellationToken = default)
            => Task.FromResult(source.First());

        public static Task<T> FirstAsync<T>(this IEnumerable<T> source, Func<T, bool> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(source.First(predicate));

        public static Task<T?> SingleOrDefaultAsync<T>(this IEnumerable<T> source, CancellationToken cancellationToken = default)
            => Task.FromResult(source.SingleOrDefault());

        public static Task<T?> SingleOrDefaultAsync<T>(this IEnumerable<T> source, Func<T, bool> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(source.SingleOrDefault(predicate));

        public static Task<T> SingleAsync<T>(this IEnumerable<T> source, CancellationToken cancellationToken = default)
            => Task.FromResult(source.Single());

        public static Task<T> SingleAsync<T>(this IEnumerable<T> source, Func<T, bool> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(source.Single(predicate));

        public static Task<bool> AnyAsync<T>(this IEnumerable<T> source, CancellationToken cancellationToken = default)
            => Task.FromResult(source.Any());

        public static Task<bool> AnyAsync<T>(this IEnumerable<T> source, Func<T, bool> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(source.Any(predicate));

        public static Task<int> CountAsync<T>(this IEnumerable<T> source, CancellationToken cancellationToken = default)
            => Task.FromResult(source.Count());

        public static Task<int> CountAsync<T>(this IEnumerable<T> source, Func<T, bool> predicate, CancellationToken cancellationToken = default)
            => Task.FromResult(source.Count(predicate));

        public static Task<decimal> SumAsync<T>(this IEnumerable<T> source, Func<T, decimal> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(source.Sum(selector));

        public static Task<decimal?> SumAsync<T>(this IEnumerable<T> source, Func<T, decimal?> selector, CancellationToken cancellationToken = default)
            => Task.FromResult(source.Sum(selector));

        public static Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TSource, TKey, TElement>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TSource, TElement> elementSelector,
            CancellationToken cancellationToken = default)
            where TKey : notnull
        {
            return Task.FromResult(source.ToDictionary(keySelector, elementSelector));
        }

        public static Task<Dictionary<TKey, TElement>> ToDictionaryAsync<TSource, TKey, TElement>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            Func<TSource, TElement> elementSelector,
            IEqualityComparer<TKey>? comparer,
            CancellationToken cancellationToken = default)
            where TKey : notnull
        {
            return Task.FromResult(comparer != null
                ? source.ToDictionary(keySelector, elementSelector, comparer)
                : source.ToDictionary(keySelector, elementSelector));
        }

        public static Task<Dictionary<TKey, TSource>> ToDictionaryAsync<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            CancellationToken cancellationToken = default)
            where TKey : notnull
        {
            return Task.FromResult(source.ToDictionary(keySelector));
        }

        public static Task<Dictionary<TKey, TSource>> ToDictionaryAsync<TSource, TKey>(
            this IEnumerable<TSource> source,
            Func<TSource, TKey> keySelector,
            IEqualityComparer<TKey>? comparer,
            CancellationToken cancellationToken = default)
            where TKey : notnull
        {
            return Task.FromResult(comparer != null
                ? source.ToDictionary(keySelector, comparer)
                : source.ToDictionary(keySelector));
        }
    }
}

namespace Microsoft.EntityFrameworkCore.Storage
{
    public interface IDbContextTransaction : IDisposable, IAsyncDisposable
    {
        Task CommitAsync(CancellationToken cancellationToken = default);
        Task RollbackAsync(CancellationToken cancellationToken = default);
        void Commit();
        void Rollback();
    }

    public class InMemoryDbContextTransaction : IDbContextTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Commit() {}
        public void Rollback() {}
        public void Dispose() {}
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
