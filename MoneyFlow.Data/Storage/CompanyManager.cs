using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MoneyFlow.Data.Encryption;

namespace MoneyFlow.Data.Storage;

/// <summary>
/// Discovered company metadata (read from headers without decrypting).
/// </summary>
public sealed class CompanyInfo
{
    public string CompanyId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public SecurityMode SecurityMode { get; set; }
    public ushort DataVersion { get; set; }
    public ulong Revision { get; set; }
}

/// <summary>
/// Company registration entry for the installation manifest.
/// </summary>
public sealed class CompanyRegistration
{
    public string CompanyId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Manages company lifecycle: discovery, creation, deletion, import.
/// Operates on the C:\ProgramData\MyERP\ directory structure.
/// </summary>
public sealed class CompanyManager
{
    private readonly string _basePath;
    private readonly StorageEngine _storageEngine;
    private readonly KeyManager _keyManager;
    private readonly ILogger<CompanyManager>? _logger;

    private readonly string _companiesPath;
    private readonly string _backupsPath;
    private readonly string _logsPath;
    private readonly string _systemPath;
    private readonly string _manifestPath;

    public CompanyManager(string basePath, StorageEngine storageEngine, KeyManager keyManager, ILogger<CompanyManager>? logger = null)
    {
        _basePath = basePath;
        _storageEngine = storageEngine;
        _keyManager = keyManager;
        _logger = logger;

        _companiesPath = Path.Combine(_basePath, "Companies");
        _backupsPath = Path.Combine(_basePath, "Backups");
        _logsPath = Path.Combine(_basePath, "Logs");
        _systemPath = Path.Combine(_basePath, "System");
        _manifestPath = Path.Combine(_systemPath, "companies.dat");
    }

    /// <summary>
    /// Initializes the MyERP directory structure.
    /// </summary>
    public void InitializeDirectories()
    {
        Directory.CreateDirectory(_companiesPath);
        Directory.CreateDirectory(_backupsPath);
        Directory.CreateDirectory(_logsPath);
        Directory.CreateDirectory(_systemPath);

        _logger?.LogInformation("MyERP directories initialized at {Path}.", _basePath);
    }

    /// <summary>
    /// Discovers all companies by scanning the Companies directory.
    /// Reads headers from each company.data file without decrypting.
    /// </summary>
    public List<CompanyInfo> DiscoverCompanies()
    {
        var companies = new List<CompanyInfo>();

        if (!Directory.Exists(_companiesPath))
            return companies;

        foreach (var dir in Directory.GetDirectories(_companiesPath))
        {
            var dataFile = Path.Combine(dir, "company.data");
            if (!File.Exists(dataFile))
                continue;

            try
            {
                var (fileHeader, secHeader) = _storageEngine.ReadHeaders(dataFile);

                // Get display name from manifest or decrypt passwordless company.data directly
                string displayName = fileHeader.CompanyId;
                var registration = GetRegistration(fileHeader.CompanyId);
                if (registration != null && !string.IsNullOrWhiteSpace(registration.DisplayName))
                {
                    displayName = registration.DisplayName;
                }
                else if (secHeader.SecurityMode == SecurityMode.Passwordless)
                {
                    try
                    {
                        var (store, _, _) = _storageEngine.LoadCompany(dataFile, null);
                        if (!string.IsNullOrWhiteSpace(store.CompanyInfo?.CompanyName))
                        {
                            displayName = store.CompanyInfo.CompanyName;
                        }
                    }
                    catch { }
                }

                companies.Add(new CompanyInfo
                {
                    CompanyId = fileHeader.CompanyId,
                    DisplayName = displayName,
                    FolderPath = dir,
                    FilePath = dataFile,
                    SecurityMode = secHeader.SecurityMode,
                    DataVersion = fileHeader.DataVersion,
                    Revision = fileHeader.RevisionNumber
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to read company at {Path}. Skipping.", dataFile);
            }
        }

        return companies.OrderBy(c => c.DisplayName).ToList();
    }

    /// <summary>
    /// Creates a new company with atomic directory/file creation.
    /// </summary>
    public string CreateCompany(string displayName, CompanyDataStore initialData, string? password)
    {
        // Generate unique company ID
        var companyId = GenerateCompanyId();
        var folderName = companyId;
        var companyDir = Path.Combine(_companiesPath, folderName);
        var tmpDir = companyDir + ".creating";

        try
        {
            // Create in a temp directory first (atomic creation)
            Directory.CreateDirectory(tmpDir);

            var dataFile = Path.Combine(tmpDir, "company.data");
            _storageEngine.CreateCompanyFile(dataFile, initialData, companyId, password);

            // Validate the created file
            if (!_storageEngine.ValidateFile(dataFile, out var error))
                throw new CompanyFileException($"Created company file failed validation: {error}");

            // Atomic finalize: rename temp dir to final
            Directory.Move(tmpDir, companyDir);

            // Register in manifest
            RegisterCompany(new CompanyRegistration
            {
                CompanyId = companyId,
                DisplayName = displayName,
                FolderName = folderName,
                CreatedAt = DateTime.UtcNow
            });

            _logger?.LogInformation("Company created: {CompanyId} ({Name}).", companyId, displayName);
            return companyId;
        }
        catch
        {
            // Cleanup on failure
            try { if (Directory.Exists(tmpDir)) Directory.Delete(tmpDir, true); }
            catch { /* Best effort */ }
            throw;
        }
    }

    /// <summary>
    /// Deletes a company after confirmation.
    /// </summary>
    public void DeleteCompany(string companyId)
    {
        var info = DiscoverCompanies().FirstOrDefault(c => c.CompanyId == companyId);
        if (info == null)
            throw new CompanyFileException($"Company not found: {companyId}");

        if (Directory.Exists(info.FolderPath))
            Directory.Delete(info.FolderPath, true);

        UnregisterCompany(companyId);

        _logger?.LogInformation("Company deleted: {CompanyId}.", companyId);
    }

    /// <summary>
    /// Updates the display name for a company in the manifest.
    /// Does not change CompanyId or DEK.
    /// </summary>
    public void RenameCompany(string companyId, string newDisplayName)
    {
        var registrations = LoadManifest();
        var reg = registrations.FirstOrDefault(r => r.CompanyId == companyId);
        if (reg != null)
        {
            reg.DisplayName = newDisplayName;
            SaveManifest(registrations);
            _logger?.LogInformation("Company renamed: {CompanyId} → {Name}.", companyId, newDisplayName);
        }
    }

    /// <summary>
    /// Imports a company.data file into the Companies directory.
    /// </summary>
    public string ImportCompany(string sourceFilePath, string displayName)
    {
        if (!_storageEngine.ValidateFile(sourceFilePath, out var error))
            throw new CompanyFileException($"Cannot import: {error}");

        var (fileHeader, _) = _storageEngine.ReadHeaders(sourceFilePath);

        // Check for ID conflict
        var existing = DiscoverCompanies().FirstOrDefault(c => c.CompanyId == fileHeader.CompanyId);
        if (existing != null)
        {
            // Assign new company ID to avoid conflict
            _logger?.LogWarning("Company ID {Id} already exists. Importing as new company.", fileHeader.CompanyId);
        }

        var companyId = GenerateCompanyId();
        var companyDir = Path.Combine(_companiesPath, companyId);
        Directory.CreateDirectory(companyDir);

        var destFile = Path.Combine(companyDir, "company.data");
        File.Copy(sourceFilePath, destFile);

        RegisterCompany(new CompanyRegistration
        {
            CompanyId = companyId,
            DisplayName = displayName,
            FolderName = companyId,
            CreatedAt = DateTime.UtcNow
        });

        _logger?.LogInformation("Company imported: {CompanyId} ({Name}) from {Path}.", companyId, displayName, sourceFilePath);
        return companyId;
    }

    /// <summary>
    /// Gets the file path for a company's data file.
    /// </summary>
    public string? GetCompanyFilePath(string companyId)
    {
        var info = DiscoverCompanies().FirstOrDefault(c => c.CompanyId == companyId);
        return info?.FilePath;
    }

    /// <summary>
    /// Gets the backup directory for a company.
    /// </summary>
    public string GetBackupDirectory(string companyId)
    {
        var path = Path.Combine(_backupsPath, companyId);
        Directory.CreateDirectory(path);
        return path;
    }

    // ─── ID Generation ──────────────────────────────────────────────────

    private string GenerateCompanyId()
    {
        var registrations = LoadManifest();
        var maxNum = 0;

        foreach (var reg in registrations)
        {
            if (reg.CompanyId.StartsWith("COMP-") && int.TryParse(reg.CompanyId[5..], out var num))
            {
                if (num > maxNum) maxNum = num;
            }
        }

        return $"COMP-{(maxNum + 1):D4}";
    }

    // ─── Manifest Management ────────────────────────────────────────────

    private List<CompanyRegistration> LoadManifest()
    {
        if (!File.Exists(_manifestPath))
            return new List<CompanyRegistration>();

        try
        {
            var json = File.ReadAllText(_manifestPath);
            return JsonSerializer.Deserialize<List<CompanyRegistration>>(json) ?? new();
        }
        catch
        {
            return new List<CompanyRegistration>();
        }
    }

    private void SaveManifest(List<CompanyRegistration> registrations)
    {
        Directory.CreateDirectory(_systemPath);
        var json = JsonSerializer.Serialize(registrations, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_manifestPath, json);
    }

    public void RegisterCompany(CompanyRegistration reg)
    {
        var registrations = LoadManifest();
        registrations.RemoveAll(r => r.CompanyId == reg.CompanyId);
        registrations.Add(reg);
        SaveManifest(registrations);
    }

    public void UnregisterCompany(string companyId)
    {
        var registrations = LoadManifest();
        registrations.RemoveAll(r => r.CompanyId == companyId);
        SaveManifest(registrations);
    }

    public CompanyRegistration? GetRegistration(string companyId)
    {
        return LoadManifest().FirstOrDefault(r => r.CompanyId == companyId);
    }
}
