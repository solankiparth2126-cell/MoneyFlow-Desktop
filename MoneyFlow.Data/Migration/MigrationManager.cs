using System;
using Microsoft.Extensions.Logging;
using MoneyFlow.Data.Storage;

namespace MoneyFlow.Data.Migration;

/// <summary>
/// Manages versioned company data migrations.
/// Supports sequential migration (v1→v2→v3), pre-migration backup, and downgrade protection.
/// </summary>
public sealed class MigrationManager
{
    private readonly BackupManager _backupManager;
    private readonly ILogger<MigrationManager>? _logger;

    public MigrationManager(BackupManager backupManager, ILogger<MigrationManager>? logger = null)
    {
        _backupManager = backupManager;
        _logger = logger;
    }

    /// <summary>
    /// Checks whether a company file needs migration and performs it if necessary.
    /// Must be called BEFORE opening the company session.
    /// </summary>
    /// <param name="filePath">Path to company.data file.</param>
    /// <param name="password">Company password (null for passwordless).</param>
    /// <returns>True if migration was performed.</returns>
    public bool MigrateIfNeeded(string filePath, string? password)
    {
        var storageEngine = new StorageEngine();
        var (fileHeader, _) = storageEngine.ReadHeaders(filePath);

        var fileVersion = fileHeader.DataVersion;
        var appVersion = CompanyFileFormat.CurrentDataVersion;

        // Downgrade protection
        if (fileVersion > appVersion)
            throw new UnsupportedCompanyVersionException(fileVersion, appVersion);

        // Already at current version
        if (fileVersion == appVersion)
            return false;

        _logger?.LogInformation(
            "Company {CompanyId} requires migration from v{From} to v{To}.",
            fileHeader.CompanyId, fileVersion, appVersion);

        // Pre-migration backup
        try
        {
            // We need the companyId to find backup dir, but we can use the file path
            var backupPath = filePath + $".pre-migration-v{fileVersion}.bak";
            System.IO.File.Copy(filePath, backupPath, overwrite: true);
            _logger?.LogInformation("Pre-migration backup created: {Path}.", backupPath);
        }
        catch (Exception ex)
        {
            throw new CompanyMigrationException(fileVersion, appVersion,
                $"Failed to create pre-migration backup: {ex.Message}", ex);
        }

        // Sequential migration
        try
        {
            var (data, header, dek) = storageEngine.LoadCompany(filePath, password);
            var keyEnvelope = storageEngine.ReadHeaders(filePath).SecurityHeader.ToKeyEnvelope();

            var currentVersion = fileVersion;
            while (currentVersion < appVersion)
            {
                var nextVersion = (ushort)(currentVersion + 1);
                _logger?.LogInformation("Migrating v{From} → v{To}...", currentVersion, nextVersion);

                ApplyMigration(data, currentVersion, nextVersion);

                currentVersion = nextVersion;
            }

            // Update version in header
            header.DataVersion = appVersion;

            // Save migrated data
            storageEngine.SaveCompany(filePath, data, header, keyEnvelope, dek);

            System.Security.Cryptography.CryptographicOperations.ZeroMemory(dek);

            _logger?.LogInformation("Migration completed: v{From} → v{To}.", fileVersion, appVersion);
            return true;
        }
        catch (UnsupportedCompanyVersionException) { throw; }
        catch (CompanyMigrationException) { throw; }
        catch (Exception ex)
        {
            throw new CompanyMigrationException(fileVersion, appVersion,
                $"Migration failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Applies a single migration step.
    /// Override this to add actual migration logic when data schema changes.
    /// </summary>
    private void ApplyMigration(CompanyDataStore data, ushort fromVersion, ushort toVersion)
    {
        // Currently only v1 exists. Future migrations will be added here:
        // switch (fromVersion)
        // {
        //     case 1:
        //         MigrateV1ToV2(data);
        //         break;
        //     case 2:
        //         MigrateV2ToV3(data);
        //         break;
        // }

        _logger?.LogDebug("Migration step v{From} → v{To} applied (no-op for current version).", fromVersion, toVersion);
    }
}
