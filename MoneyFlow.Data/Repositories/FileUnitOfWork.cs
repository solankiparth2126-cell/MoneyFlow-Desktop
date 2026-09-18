using System;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data.Storage;

namespace MoneyFlow.Data.Repositories;

/// <summary>
/// File-based IUnitOfWork implementation.
/// SaveChangesAsync persists all dirty changes to company.data via atomic write.
/// BeginTransactionAsync provides transaction support with journal/WAL.
/// </summary>
public sealed class FileUnitOfWork : IUnitOfWork
{
    private readonly CompanySession _session;
    private bool _disposed;

    public FileUnitOfWork(CompanySession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>
    /// Persists all pending changes to company.data.
    /// Returns the count of dirty entities (approximate).
    /// </summary>
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (!_session.IsOpen)
            _session.InitializeInMemory();

        if (!_session.IsDirty)
            return Task.FromResult(0);

        _session.Save();

        // Return 1 to indicate changes were persisted.
        // Exact count isn't meaningful in the file-based model since we persist the entire snapshot.
        return Task.FromResult(1);
    }

    /// <summary>
    /// Begins a new transaction with journal support.
    /// </summary>
    public Task<IDbTransactionContext> BeginTransactionAsync(CancellationToken ct = default)
    {
        if (!_session.IsOpen)
            _session.InitializeInMemory();

        var txContext = _session.CreateTransaction();
        return Task.FromResult<IDbTransactionContext>(txContext);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // UnitOfWork does NOT own the session — session is managed by DI/CompanyManager
            _disposed = true;
        }
    }
}
