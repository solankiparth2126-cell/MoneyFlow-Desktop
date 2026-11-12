using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Core.Interfaces;

public enum JournalOperationType
{
    EntityAdded = 1,
    EntityUpdated = 2,
    EntityDeleted = 3,
    BatchOperation = 4,
    TransactionBegin = 5,
    TransactionCommit = 6,
    TransactionRollback = 7
}

public class JournalRecord
{
    public string TransactionId { get; set; } = Guid.NewGuid().ToString("N");
    public int SequenceNumber { get; set; }
    public JournalOperationType Operation { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? BeforeStateJson { get; set; }
    public string? AfterStateJson { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public interface IJournalManager
{
    Task AppendRecordAsync(string companyId, JournalRecord record, CancellationToken ct = default);
    Task<IReadOnlyList<JournalRecord>> ReadPendingJournalRecordsAsync(string companyId, CancellationToken ct = default);
    Task ClearJournalAsync(string companyId, CancellationToken ct = default);
    Task<bool> HasUncommittedTransactionsAsync(string companyId, CancellationToken ct = default);
}

public interface IFileTransaction : IDisposable, IAsyncDisposable
{
    string TransactionId { get; }
    bool IsCommitted { get; }
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}

public interface ITransactionManager
{
    Task<IFileTransaction> BeginTransactionAsync(string companyId, CancellationToken ct = default);
    Task RecoverPendingJournalAsync(string companyId, CompanyData currentData, CancellationToken ct = default);
}
