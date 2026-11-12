using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Data.Storage;

public class FileTransaction : IFileTransaction
{
    private readonly IJournalManager _journalManager;
    private readonly string _companyId;
    private readonly Func<Task> _onRollback;
    private bool _disposed;

    public string TransactionId { get; }
    public bool IsCommitted { get; private set; }

    public FileTransaction(string companyId, IJournalManager journalManager, Func<Task> onRollback)
    {
        _companyId = companyId;
        _journalManager = journalManager;
        _onRollback = onRollback;
        TransactionId = Guid.NewGuid().ToString("N");
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _journalManager.AppendRecordAsync(_companyId, new JournalRecord
        {
            TransactionId = TransactionId,
            Operation = JournalOperationType.TransactionBegin,
            TimestampUtc = DateTime.UtcNow
        }, ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (IsCommitted) return;

        await _journalManager.AppendRecordAsync(_companyId, new JournalRecord
        {
            TransactionId = TransactionId,
            Operation = JournalOperationType.TransactionCommit,
            TimestampUtc = DateTime.UtcNow
        }, ct);

        IsCommitted = true;
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (IsCommitted) return;

        await _journalManager.AppendRecordAsync(_companyId, new JournalRecord
        {
            TransactionId = TransactionId,
            Operation = JournalOperationType.TransactionRollback,
            TimestampUtc = DateTime.UtcNow
        }, ct);

        await _onRollback();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (!IsCommitted)
            {
                RollbackAsync().GetAwaiter().GetResult();
            }
            _disposed = true;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            if (!IsCommitted)
            {
                await RollbackAsync();
            }
            _disposed = true;
        }
    }
}

public class TransactionManager : ITransactionManager
{
    private readonly IJournalManager _journalManager;
    private Func<Task>? _activeRollbackHandler;

    public TransactionManager(IJournalManager journalManager)
    {
        _journalManager = journalManager;
    }

    public void SetRollbackHandler(Func<Task> handler)
    {
        _activeRollbackHandler = handler;
    }

    public async Task<IFileTransaction> BeginTransactionAsync(string companyId, CancellationToken ct = default)
    {
        var tx = new FileTransaction(
            companyId,
            _journalManager,
            async () =>
            {
                if (_activeRollbackHandler != null)
                {
                    await _activeRollbackHandler();
                }
            });

        await tx.InitializeAsync(ct);
        return tx;
    }

    public async Task RecoverPendingJournalAsync(string companyId, CompanyData currentData, CancellationToken ct = default)
    {
        bool hasUncommitted = await _journalManager.HasUncommittedTransactionsAsync(companyId, ct);
        if (hasUncommitted)
        {
            // Clear incomplete WAL to restore consistent durable state
            await _journalManager.ClearJournalAsync(companyId, ct);
        }
    }
}
