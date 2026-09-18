using Microsoft.Extensions.Logging;

namespace MoneyFlow.Data.Transactions;

/// <summary>
/// Handles recovery from incomplete transactions detected by journal files on company open.
/// </summary>
public sealed class TransactionRecovery
{
    private readonly ILogger<TransactionRecovery>? _logger;

    public TransactionRecovery(ILogger<TransactionRecovery>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Checks for and recovers from incomplete transactions.
    /// Called during company open, before the session is ready.
    /// </summary>
    /// <param name="companyFilePath">Path to the company.data file.</param>
    /// <returns>True if recovery was performed, false if no recovery was needed.</returns>
    public bool RecoverIfNeeded(string companyFilePath)
    {
        var journal = new TransactionJournal(companyFilePath, _logger as ILogger<TransactionJournal>);

        if (!journal.HasPendingJournal())
            return false;

        var lastState = journal.GetLastJournalState();

        _logger?.LogWarning(
            "Incomplete transaction detected for {Path}. Last state: {State}. Performing recovery.",
            companyFilePath, lastState);

        switch (lastState)
        {
            case TransactionState.Committed:
                // The commit completed but the journal wasn't cleaned up.
                // The company.data file should be valid. Just clean up the journal.
                _logger?.LogInformation("Transaction was committed. Cleaning up stale journal.");
                journal.CleanupJournal();
                return true;

            case TransactionState.Committing:
                // Crash during the atomic write phase.
                // Check if the .tmp file exists — if so, the original company.data is still valid.
                var tmpPath = companyFilePath + ".tmp";
                if (System.IO.File.Exists(tmpPath))
                {
                    _logger?.LogWarning("Found incomplete .tmp file during commit. Removing temp file; original data is intact.");
                    try { System.IO.File.Delete(tmpPath); } catch { /* Best effort */ }
                }

                // If .bak exists from File.Replace, the replace may have partially completed
                var bakPath = companyFilePath + ".bak";
                if (System.IO.File.Exists(bakPath) && !System.IO.File.Exists(companyFilePath))
                {
                    // The replace deleted the original but didn't move the temp in.
                    // Recover from the backup.
                    _logger?.LogWarning("Recovering from backup file after interrupted File.Replace.");
                    System.IO.File.Move(bakPath, companyFilePath);
                }
                else if (System.IO.File.Exists(bakPath))
                {
                    try { System.IO.File.Delete(bakPath); } catch { /* Best effort */ }
                }

                journal.CleanupJournal();
                return true;

            case TransactionState.Active:
            case TransactionState.Prepared:
            case TransactionState.RolledBack:
            case null:
                // Transaction never reached commit — the original company.data is valid.
                // Just clean up the journal.
                _logger?.LogInformation("Transaction was not committed (state: {State}). Original data is intact.", lastState);
                journal.CleanupJournal();

                // Also clean up any stale temp files
                var staleTmp = companyFilePath + ".tmp";
                if (System.IO.File.Exists(staleTmp))
                {
                    try { System.IO.File.Delete(staleTmp); } catch { /* Best effort */ }
                }
                return true;

            default:
                _logger?.LogError("Unknown transaction state: {State}. Cleaning up journal conservatively.", lastState);
                journal.CleanupJournal();
                return true;
        }
    }
}
