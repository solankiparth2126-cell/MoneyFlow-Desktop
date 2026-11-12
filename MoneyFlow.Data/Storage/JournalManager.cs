using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Data.Storage;

public class JournalManager : IJournalManager
{
    private readonly ICompanyStorageEngine _storageEngine;
    private static readonly SemaphoreSlim _fileLock = new(1, 1);

    public JournalManager(ICompanyStorageEngine storageEngine)
    {
        _storageEngine = storageEngine;
    }

    private string GetWalFilePath(string companyId)
    {
        string dir = _storageEngine.GetCompanyDirectory(companyId);
        return Path.Combine(dir, "company.wal");
    }

    public async Task AppendRecordAsync(string companyId, JournalRecord record, CancellationToken ct = default)
    {
        await _fileLock.WaitAsync(ct);
        try
        {
            string walPath = GetWalFilePath(companyId);
            string line = JsonSerializer.Serialize(record) + Environment.NewLine;
            await File.AppendAllTextAsync(walPath, line, ct);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<IReadOnlyList<JournalRecord>> ReadPendingJournalRecordsAsync(string companyId, CancellationToken ct = default)
    {
        string walPath = GetWalFilePath(companyId);
        if (!File.Exists(walPath))
        {
            return Array.Empty<JournalRecord>();
        }

        await _fileLock.WaitAsync(ct);
        try
        {
            var list = new List<JournalRecord>();
            var lines = await File.ReadAllLinesAsync(walPath, ct);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var rec = JsonSerializer.Deserialize<JournalRecord>(line);
                    if (rec != null) list.Add(rec);
                }
                catch
                {
                    // Ignore corrupted trailing line in crash scenario
                }
            }
            return list;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task ClearJournalAsync(string companyId, CancellationToken ct = default)
    {
        await _fileLock.WaitAsync(ct);
        try
        {
            string walPath = GetWalFilePath(companyId);
            if (File.Exists(walPath))
            {
                File.Delete(walPath);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<bool> HasUncommittedTransactionsAsync(string companyId, CancellationToken ct = default)
    {
        var records = await ReadPendingJournalRecordsAsync(companyId, ct);
        if (records.Count == 0) return false;

        var openTx = new HashSet<string>();
        foreach (var r in records)
        {
            if (r.Operation == JournalOperationType.TransactionBegin)
            {
                openTx.Add(r.TransactionId);
            }
            else if (r.Operation == JournalOperationType.TransactionCommit ||
                     r.Operation == JournalOperationType.TransactionRollback)
            {
                openTx.Remove(r.TransactionId);
            }
        }

        return openTx.Count > 0;
    }
}
