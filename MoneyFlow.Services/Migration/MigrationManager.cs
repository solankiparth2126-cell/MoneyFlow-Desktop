using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Exceptions;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Core.Migration;
using MoneyFlow.Core.Migration.Migrations;
using MoneyFlow.Core.Migration.Validators;

namespace MoneyFlow.Services.Migration;

public class MigrationManager : IMigrationManager
{
    private readonly ICompanyStorageEngine _storageEngine;
    private readonly ILogger<MigrationManager> _logger;
    private readonly List<IDataMigration> _migrations;

    public int CurrentDataVersion => CompanyFileConstants.CurrentDataVersion;

    public MigrationManager(ICompanyStorageEngine storageEngine, ILogger<MigrationManager> logger)
    {
        _storageEngine = storageEngine;
        _logger = logger;
        _migrations = new List<IDataMigration>
        {
            new MigrationV1ToV2(),
            new MigrationV2ToV3(),
            new MigrationV3ToV4(),
            new MigrationV4ToV5(),
            new MigrationV5ToV6()
        };
    }

    public async Task<bool> NeedsMigrationAsync(string filePath, CancellationToken ct = default)
    {
        var header = await _storageEngine.ReadHeaderAsync(filePath, ct);

        if (header.DataVersion > CurrentDataVersion)
        {
            throw new UnsupportedDataVersionException(header.DataVersion, CurrentDataVersion);
        }

        return header.DataVersion < CurrentDataVersion;
    }

    public async Task MigrateCompanyFileAsync(
        string filePath,
        string password,
        IProgress<MigrationProgressInfo>? progress = null,
        CancellationToken ct = default)
    {
        var header = await _storageEngine.ReadHeaderAsync(filePath, ct);

        if (header.DataVersion > CurrentDataVersion)
        {
            throw new UnsupportedDataVersionException(header.DataVersion, CurrentDataVersion);
        }

        if (header.DataVersion == CurrentDataVersion)
        {
            return; // Already current
        }

        int sourceVersion = header.DataVersion;
        int targetVersion = CurrentDataVersion;
        string companyId = header.CompanyId;
        string companyName = header.HeaderMetadata.TryGetValue("CompanyName", out var name) ? name : companyId;

        _logger.LogInformation("Starting sequential migration for company {CompanyId} from v{SourceVersion} to v{TargetVersion}",
            companyId, sourceVersion, targetVersion);

        // 1. Acquire file lock
        using var compLock = _storageEngine.AcquireLock(companyId);

        // 2. Pre-migration backup
        progress?.Report(new MigrationProgressInfo
        {
            CompanyId = companyId,
            CompanyName = companyName,
            FromVersion = sourceVersion,
            TargetVersion = targetVersion,
            CurrentStep = 1,
            TotalSteps = (targetVersion - sourceVersion) + 3,
            StatusMessage = "Creating verified pre-migration backup...",
            ProgressPercentage = 10.0
        });

        string backupPath;
        try
        {
            backupPath = await _storageEngine.CreateBackupAsync(
                companyId,
                reason: $"pre_migration_v{sourceVersion}_to_v{targetVersion}",
                ct: ct);

            _logger.LogInformation("Pre-migration backup created at {BackupPath}", backupPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create pre-migration backup for company {CompanyId}", companyId);
            throw new BackupException($"Pre-migration backup failed: {ex.Message}. Migration aborted to protect original data.", ex);
        }

        // 3. Load and decrypt into memory working model
        CompanyData workingData;
        byte[] dek;
        try
        {
            (workingData, dek) = await _storageEngine.LoadCompanyDataAsync(filePath, password, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load/decrypt company data for migration {CompanyId}", companyId);
            throw;
        }

        // 4. Execute sequential migrations
        int currentVersion = sourceVersion;
        int step = 2;
        int totalSteps = (targetVersion - sourceVersion) + 3;

        while (currentVersion < targetVersion)
        {
            int nextVersion = currentVersion + 1;
            var migration = _migrations.FirstOrDefault(m => m.FromVersion == currentVersion && m.ToVersion == nextVersion);

            if (migration == null)
            {
                throw new MigrationException(currentVersion, nextVersion,
                    $"No sequential migration found from v{currentVersion} to v{nextVersion}.");
            }

            _logger.LogInformation("Executing migration {FromVersion} -> {ToVersion}: {Desc}",
                migration.FromVersion, migration.ToVersion, migration.Description);

            progress?.Report(new MigrationProgressInfo
            {
                CompanyId = companyId,
                CompanyName = companyName,
                FromVersion = sourceVersion,
                TargetVersion = targetVersion,
                CurrentStep = step,
                TotalSteps = totalSteps,
                StatusMessage = $"Migrating data schema v{migration.FromVersion} → v{migration.ToVersion}...",
                ProgressPercentage = 10.0 + ((double)(step - 1) / totalSteps * 80.0)
            });

            try
            {
                await migration.MigrateAsync(workingData, ct);
                currentVersion = nextVersion;
                step++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Migration step v{From} to v{To} failed for {CompanyId}. Original company.data is preserved intact.",
                    migration.FromVersion, migration.ToVersion, companyId);
                throw new MigrationException(migration.FromVersion, migration.ToVersion,
                    $"Step failed: {ex.Message}. Original data preserved.", ex);
            }
        }

        // 5. Post-migration validation
        progress?.Report(new MigrationProgressInfo
        {
            CompanyId = companyId,
            CompanyName = companyName,
            FromVersion = sourceVersion,
            TargetVersion = targetVersion,
            CurrentStep = step,
            TotalSteps = totalSteps,
            StatusMessage = "Validating migrated accounting and referential integrity...",
            ProgressPercentage = 92.0
        });

        try
        {
            CompanyDataValidator.Validate(workingData);
            _logger.LogInformation("Post-migration validation passed for company {CompanyId}", companyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Post-migration validation failed for {CompanyId}. Rolling back.", companyId);
            throw new CompanyValidationException($"Validation failed after migration: {ex.Message}. Original company data preserved.", ex);
        }

        // 6. Atomic replacement commit
        progress?.Report(new MigrationProgressInfo
        {
            CompanyId = companyId,
            CompanyName = companyName,
            FromVersion = sourceVersion,
            TargetVersion = targetVersion,
            CurrentStep = totalSteps,
            TotalSteps = totalSteps,
            StatusMessage = "Saving migrated company data atomically...",
            ProgressPercentage = 98.0
        });

        try
        {
            await _storageEngine.SaveCompanyDataAsync(filePath, workingData, dek, password, ct);
            _logger.LogInformation("Migration successfully committed for company {CompanyId} to v{TargetVersion}", companyId, targetVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Atomic commit failed during migration for company {CompanyId}", companyId);
            throw new MigrationException(sourceVersion, targetVersion, $"Atomic commit failed: {ex.Message}", ex);
        }

        progress?.Report(new MigrationProgressInfo
        {
            CompanyId = companyId,
            CompanyName = companyName,
            FromVersion = sourceVersion,
            TargetVersion = targetVersion,
            CurrentStep = totalSteps,
            TotalSteps = totalSteps,
            StatusMessage = "Migration completed successfully.",
            ProgressPercentage = 100.0
        });
    }
}
