using System;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Data.Transactions;

/// <summary>
/// File-based transaction context implementing IDbTransactionContext.
/// Wraps the TransactionJournal to provide the same BeginTransaction/Commit/Rollback
/// contract that the existing application services expect.
/// </summary>
public sealed class TransactionContext : IDbTransactionContext
{
    private readonly TransactionJournal _journal;
    private readonly Action _onCommit;
    private readonly Action _onRollback;
    private bool _committed;
    private bool _rolledBack;

    public string TransactionId { get; }

    /// <summary>
    /// Creates a new transaction context.
    /// </summary>
    /// <param name="journal">The journal to record transaction state.</param>
    /// <param name="onCommit">Callback invoked when commit is requested — triggers persistence.</param>
    /// <param name="onRollback">Callback invoked when rollback is requested — discards changes.</param>
    public TransactionContext(TransactionJournal journal, Action onCommit, Action onRollback)
    {
        _journal = journal;
        _onCommit = onCommit;
        _onRollback = onRollback;
        TransactionId = _journal.BeginTransaction();
    }

    public Task CommitAsync(CancellationToken ct = default)
    {
        if (_committed || _rolledBack)
            throw new InvalidOperationException("Transaction has already been completed.");

        _journal.MarkPrepared();
        _journal.MarkCommitting();

        _onCommit();

        _journal.MarkCommitted();
        _committed = true;

        return Task.CompletedTask;
    }

    public Task RollbackAsync(CancellationToken ct = default)
    {
        if (_committed || _rolledBack)
            return Task.CompletedTask; // Already completed, nothing to do

        _onRollback();

        _journal.MarkRolledBack();
        _rolledBack = true;

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (!_committed && !_rolledBack)
        {
            // Auto-rollback if not explicitly committed
            try { RollbackAsync().GetAwaiter().GetResult(); }
            catch { /* Best effort during dispose */ }
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
