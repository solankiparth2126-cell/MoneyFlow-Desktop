using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace MoneyFlow.Data.Storage;

/// <summary>
/// Manages encrypted backup/restore of company.data files.
/// Backups are encrypted copies — no plaintext business data in backups.
/// </summary>
public sealed class BackupManager
{
    private readonly CompanyManager _companyManager;
    private readonly StorageEngine _storageEngine;
    private readonly ILogger<BackupManager>? _logger;

    public BackupManager(CompanyManager companyManager, StorageEngine storageEngine, ILogger<BackupManager>? logger = null)
    {
        _companyManager = companyManager;
        _storageEngine = storageEngine;
        _logger = logger;
    }

    /// <summary>
    /// Creates an encrypted backup of a company's data file.
    /// The backup preserves the security mode, encryption, and revision.
    /// </summary>
    /// <param name="companyId">Company ID to back up.</param>
    /// <param name="customFileName">Optional custom backup file name.</param>
    /// <returns>Path to the created backup file.</returns>
    public string CreateBackup(string companyId, string? customFileName = null)
    {
        var sourceFile = _companyManager.GetCompanyFilePath(companyId);
        if (sourceFile == null || !File.Exists(sourceFile))
            throw new CompanyFileException($"Company file not found for backup: {companyId}");

        // Validate the source file first
        if (!_storageEngine.ValidateFile(sourceFile, out var error))
            throw new CompanyFileException($"Cannot backup: source file is invalid — {error}");

        var backupDir = _companyManager.GetBackupDirectory(companyId);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var fileName = customFileName ?? $"backup_{companyId}_{timestamp}.data";
        var backupPath = Path.Combine(backupDir, fileName);

        // Copy the encrypted file directly — backup preserves encryption
        File.Copy(sourceFile, backupPath, overwrite: false);

        _logger?.LogInformation("Backup created for {CompanyId}: {Path}.", companyId, backupPath);
        return backupPath;
    }

    /// <summary>
    /// Restores a company from a backup file.
    /// Uses atomic replacement — original is preserved until replacement is verified.
    /// </summary>
    public void RestoreBackup(string companyId, string backupFilePath)
    {
        if (!File.Exists(backupFilePath))
            throw new CompanyRestoreException($"Backup file not found: {backupFilePath}");

        // Validate the backup file
        if (!_storageEngine.ValidateFile(backupFilePath, out var error))
            throw new CompanyRestoreException($"Invalid backup file: {error}");

        var targetFile = _companyManager.GetCompanyFilePath(companyId);
        if (targetFile == null)
            throw new CompanyRestoreException($"Company not found: {companyId}");

        // Create a safety copy of the current file before restore
        var safetyPath = targetFile + ".pre-restore";
        if (File.Exists(targetFile))
            File.Copy(targetFile, safetyPath, overwrite: true);

        try
        {
            // Atomic replacement
            var tmpRestore = targetFile + ".restoring";
            File.Copy(backupFilePath, tmpRestore, overwrite: true);

            // Validate the restored file
            if (!_storageEngine.ValidateFile(tmpRestore, out var restoreError))
            {
                File.Delete(tmpRestore);
                throw new CompanyRestoreException($"Restored file failed validation: {restoreError}");
            }

            // Replace original with restored
            if (File.Exists(targetFile))
            {
                File.Replace(tmpRestore, targetFile, targetFile + ".bak");
                try { File.Delete(targetFile + ".bak"); } catch { }
            }
            else
            {
                File.Move(tmpRestore, targetFile);
            }

            // Clean up safety copy on success
            try { File.Delete(safetyPath); } catch { }

            _logger?.LogInformation("Company {CompanyId} restored from backup: {Path}.", companyId, backupFilePath);
        }
        catch (CompanyRestoreException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Restore failed — recover the original if possible
            if (File.Exists(safetyPath) && !File.Exists(targetFile))
            {
                File.Move(safetyPath, targetFile);
            }
            throw new CompanyRestoreException($"Restore failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists available backup files for a company.
    /// </summary>
    public string[] ListBackups(string companyId)
    {
        var backupDir = _companyManager.GetBackupDirectory(companyId);
        if (!Directory.Exists(backupDir))
            return Array.Empty<string>();

        return Directory.GetFiles(backupDir, "*.data", SearchOption.TopDirectoryOnly);
    }
}
