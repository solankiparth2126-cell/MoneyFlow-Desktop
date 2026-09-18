using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MoneyFlow.Data.Transactions;

/// <summary>
/// Transaction state machine states.
/// </summary>
public enum TransactionState
{
    Active,
    Prepared,
    Committing,
    Committed,
    RolledBack
}

/// <summary>
/// A journal entry representing a single transaction operation.
/// </summary>
public sealed class JournalEntry
{
    public string TransactionId { get; set; } = string.Empty;
    public long Sequence { get; set; }
    public TransactionState State { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Write-ahead log (WAL) for atomic transaction support.
/// Journal file: company.data.journal
/// Ensures that incomplete transactions can be detected and recovered on restart.
/// </summary>
public sealed class TransactionJournal : IDisposable
{
    private readonly string _journalPath;
    private readonly ILogger<TransactionJournal>? _logger;
    private StreamWriter? _writer;
    private long _sequence;
    private bool _disposed;

    public string? ActiveTransactionId { get; private set; }
    public TransactionState CurrentState { get; private set; } = TransactionState.Committed;

    public TransactionJournal(string companyFilePath, ILogger<TransactionJournal>? logger = null)
    {
        _journalPath = companyFilePath + ".journal";
        _logger = logger;
    }

    /// <summary>
    /// Begins a new transaction, recording it in the journal.
    /// </summary>
    public string BeginTransaction()
    {
        if (ActiveTransactionId != null)
            throw new InvalidOperationException("A transaction is already active. Commit or rollback before starting a new one.");

        ActiveTransactionId = Guid.NewGuid().ToString("N");
        CurrentState = TransactionState.Active;
        _sequence = 0;

        _writer = new StreamWriter(
            new FileStream(_journalPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite),
            Encoding.UTF8);

        WriteEntry(TransactionState.Active, "Transaction started");

        return ActiveTransactionId;
    }

    /// <summary>
    /// Records that the transaction is prepared (all changes validated).
    /// </summary>
    public void MarkPrepared()
    {
        EnsureActive();
        CurrentState = TransactionState.Prepared;
        WriteEntry(TransactionState.Prepared, "Transaction prepared");
    }

    /// <summary>
    /// Records that the commit is in progress (writing to disk).
    /// </summary>
    public void MarkCommitting()
    {
        EnsureActive();
        CurrentState = TransactionState.Committing;
        WriteEntry(TransactionState.Committing, "Commit in progress");
    }

    /// <summary>
    /// Records successful commit. Cleans up the journal file.
    /// </summary>
    public void MarkCommitted()
    {
        CurrentState = TransactionState.Committed;
        WriteEntry(TransactionState.Committed, "Transaction committed");
        CleanupJournal();
        ActiveTransactionId = null;
    }

    /// <summary>
    /// Records that the transaction was rolled back.
    /// </summary>
    public void MarkRolledBack()
    {
        CurrentState = TransactionState.RolledBack;
        if (_writer != null)
            WriteEntry(TransactionState.RolledBack, "Transaction rolled back");
        CleanupJournal();
        ActiveTransactionId = null;
    }

    /// <summary>
    /// Checks if a journal file exists (indicating a potentially incomplete transaction).
    /// </summary>
    public bool HasPendingJournal() => File.Exists(_journalPath);

    /// <summary>
    /// Reads the journal file to determine the last known transaction state.
    /// Used by TransactionRecovery to decide whether to rollback or replay.
    /// </summary>
    public TransactionState? GetLastJournalState()
    {
        if (!File.Exists(_journalPath))
            return null;

        TransactionState? lastState = null;

        try
        {
            var lines = File.ReadAllLines(_journalPath);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var entry = JsonSerializer.Deserialize<JournalEntry>(line);
                    if (entry != null) lastState = entry.State;
                }
                catch { /* Skip malformed entries */ }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to read transaction journal at {Path}.", _journalPath);
        }

        return lastState;
    }

    /// <summary>
    /// Removes the journal file after successful commit or rollback.
    /// </summary>
    public void CleanupJournal()
    {
        try
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;

            if (File.Exists(_journalPath))
                File.Delete(_journalPath);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to clean up journal file at {Path}.", _journalPath);
        }
    }

    private void WriteEntry(TransactionState state, string description)
    {
        if (_writer == null) return;

        var entry = new JournalEntry
        {
            TransactionId = ActiveTransactionId ?? "unknown",
            Sequence = ++_sequence,
            State = state,
            Timestamp = DateTime.UtcNow,
            Description = description
        };

        var json = JsonSerializer.Serialize(entry);
        _writer.WriteLine(json);
        _writer.Flush();
    }

    private void EnsureActive()
    {
        if (ActiveTransactionId == null)
            throw new InvalidOperationException("No active transaction.");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _writer?.Dispose();
            _disposed = true;
        }
    }
}
