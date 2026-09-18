using System;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MoneyFlow.Data.Storage;

/// <summary>
/// Manages MyERP local environment startup detection, data path validation,
/// directory hierarchy initialization, and crash-safe configuration persistence.
/// Strictly enforces local filesystem storage with zero database engine dependencies.
/// </summary>
public static class SystemEnvironmentManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Gets the standard default company data path for new installations:
    /// C:\Users\<User>\MyERP\Data (or C:\Users\Public\MoneyFlow\Data if user profile not accessible).
    /// </summary>
    public static string GetDefaultCompanyDataPath()
    {
        try
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
            {
                return Path.Combine(userProfile, "MoneyFlow", "Data");
            }
        }
        catch
        {
            // Fallback to public folder
        }

        return @"C:\Users\Public\MoneyFlow\Data";
    }

    /// <summary>
    /// Determines whether the application is running for the first time on a new installation.
    /// Returns true if first-time setup is required.
    /// If false, returns the configured storage path and system configuration.
    /// </summary>
    public static bool IsFirstTimeSetup(out string? configuredStoragePath, out SystemConfiguration? systemConfig)
    {
        configuredStoragePath = null;
        systemConfig = null;

        // 1. Check bootstrap pointers in LocalAppData
        var candidatePaths = new[]
        {
            ReadBootstrapPointer(Environment.SpecialFolder.LocalApplicationData, "MoneyFlow"),
            ReadBootstrapPointer(Environment.SpecialFolder.LocalApplicationData, "MyERP"),
            ReadBootstrapPointer(Environment.SpecialFolder.ApplicationData, "MoneyFlow"),
            GetDefaultCompanyDataPath(),
            @"C:\Users\Public\MoneyFlow\Data",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MyERP", "Data"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "MyERP")
        };

        foreach (var candidate in candidatePaths)
        {
            if (string.IsNullOrWhiteSpace(candidate) || !Directory.Exists(candidate))
                continue;

            var manifestPath = Path.Combine(candidate, "System", "installation.dat");
            if (!File.Exists(manifestPath))
                continue;

            try
            {
                var manifestJson = File.ReadAllText(manifestPath);
                var metadata = JsonSerializer.Deserialize<InstallationMetadata>(manifestJson, JsonOptions);

                if (metadata != null && metadata.SetupCompleted)
                {
                    var loadedConfig = SystemConfiguration.Load(candidate);
                    configuredStoragePath = !string.IsNullOrWhiteSpace(metadata.StoragePath)
                        ? metadata.StoragePath
                        : candidate;

                    systemConfig = loadedConfig;
                    return false; // Setup has completed!
                }
            }
            catch
            {
                // Unreadable or corrupted candidate, continue checking
            }
        }

        // No completed installation detected
        return true;
    }

    /// <summary>
    /// Validates a proposed Company Data Path.
    /// Strictly rejects SQL connection strings and database files.
    /// Verifies folder accessibility and write permissions.
    /// </summary>
    public static bool ValidateDataPath(string? path, out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = "The company data path cannot be empty.";
            return false;
        }

        var trimmed = path.Trim();

        // 1. Strictly forbid database / SQL connection strings
        var lower = trimmed.ToLowerInvariant();
        if (lower.Contains("server=") || lower.Contains("data source=") || lower.Contains("initial catalog=") ||
            lower.Contains("user id=") || lower.Contains("sqlexpress") || lower.Contains("localdb") ||
            lower.Contains(".mdf") || lower.Contains(".ldf") || lower.Contains(".db") || lower.Contains(".sqlite"))
        {
            errorMessage = "The selected company data path is invalid. SQL connection strings and database files are forbidden.";
            return false;
        }

        // 2. Validate path syntax
        try
        {
            if (!Path.IsPathRooted(trimmed))
            {
                errorMessage = "The selected company data path is invalid. Please select an absolute local filesystem path.";
                return false;
            }

            var fullPath = Path.GetFullPath(trimmed);
        }
        catch
        {
            errorMessage = "The selected company data path is invalid. Please select another location.";
            return false;
        }

        // 3. Check directory creation & write permissions
        try
        {
            if (!Directory.Exists(trimmed))
            {
                Directory.CreateDirectory(trimmed);
            }

            // Write permission check
            var testFile = Path.Combine(trimmed, $".write_test_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testFile, "write_test");
            File.Delete(testFile);
        }
        catch
        {
            errorMessage = "MyERP cannot write to the selected company data path. Please select another location or grant the required permissions.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <summary>
    /// Initializes the MyERP local storage environment and saves the configuration atomically.
    /// Creates the directory structure: System\, Companies\, Backups\, Logs\
    /// Crash-safe: SetupCompleted is only marked true after files are fully written and verified.
    /// </summary>
    public static void InitializeEnvironment(string storagePath, SystemConfiguration config)
    {
        var normalizedPath = Path.GetFullPath(storagePath.Trim());

        // 1. Create directory structure
        var systemDir = Path.Combine(normalizedPath, "System");
        var companiesDir = Path.Combine(normalizedPath, "Companies");
        var backupsDir = Path.Combine(normalizedPath, "Backups");
        var logsDir = Path.Combine(normalizedPath, "Logs");

        Directory.CreateDirectory(systemDir);
        Directory.CreateDirectory(companiesDir);
        Directory.CreateDirectory(backupsDir);
        Directory.CreateDirectory(logsDir);

        // 2. Configure paths and save system.config atomically
        config.CompanyDataPath = normalizedPath;
        config.SaveAtomic(normalizedPath);

        // 3. Atomically write installation.dat with SetupCompleted = true
        var metadata = new InstallationMetadata
        {
            SetupCompleted = true,
            ConfigurationVersion = 1,
            InstallationId = Guid.NewGuid().ToString("D"),
            StoragePath = normalizedPath,
            InstalledAt = DateTime.UtcNow,
            SetupCompletedAt = DateTime.UtcNow,
            ApplicationVersion = "1.0.0"
        };

        var manifestPath = Path.Combine(systemDir, "installation.dat");
        var tmpManifestPath = manifestPath + ".tmp";
        var manifestJson = JsonSerializer.Serialize(metadata, JsonOptions);

        using (var fs = new FileStream(tmpManifestPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        using (var writer = new StreamWriter(fs))
        {
            writer.Write(manifestJson);
            writer.Flush();
            fs.Flush(true);
        }

        // Validate temporary file readability before atomic commit
        var validatedJson = File.ReadAllText(tmpManifestPath);
        if (string.IsNullOrWhiteSpace(validatedJson))
        {
            throw new IOException("Failed to validate written installation metadata file.");
        }

        if (File.Exists(manifestPath))
        {
            File.Move(tmpManifestPath, manifestPath, overwrite: true);
        }
        else
        {
            File.Move(tmpManifestPath, manifestPath);
        }

        // 4. Save bootstrap pointers for fast startup detection on subsequent launches
        SaveBootstrapPointer(Environment.SpecialFolder.LocalApplicationData, "MoneyFlow", normalizedPath);
        SaveBootstrapPointer(Environment.SpecialFolder.LocalApplicationData, "MyERP", normalizedPath);
    }

    private static string? ReadBootstrapPointer(Environment.SpecialFolder folder, string appSubfolder)
    {
        try
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(folder), appSubfolder);
            var pointerFile = Path.Combine(baseDir, "bootstrap.dat");
            if (File.Exists(pointerFile))
            {
                var target = File.ReadAllText(pointerFile).Trim();
                if (Directory.Exists(target))
                    return target;
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    private static void SaveBootstrapPointer(Environment.SpecialFolder folder, string appSubfolder, string targetPath)
    {
        try
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(folder), appSubfolder);
            Directory.CreateDirectory(baseDir);
            var pointerFile = Path.Combine(baseDir, "bootstrap.dat");
            File.WriteAllText(pointerFile, targetPath);
        }
        catch
        {
            // Best effort
        }
    }
}
