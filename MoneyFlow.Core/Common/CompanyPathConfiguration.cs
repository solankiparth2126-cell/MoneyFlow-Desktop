using System;
using System.IO;

namespace MoneyFlow.Core.Common;

/// <summary>
/// Authoritative path configuration resolver for MoneyFlow offline ERP data.
/// Dynamically resolves storage directories based on:
/// 1. Custom runtime path argument
/// 2. Explicitly configured paths (e.g. from appsettings.json or DI)
/// 3. Environment variables (MONEYFLOW_COMPANIES_PATH, MONEYFLOW_DATA_PATH)
/// 4. First-time setup marker file (.setup_completed)
/// 5. appsettings.json in application base directory
/// 6. Default fallback directories (C:\MoneyFlow\Data\Companies\)
/// </summary>
public static class CompanyPathConfiguration
{
    private static string? _configuredDataRootPath;
    private static string? _configuredCompaniesPath;

    public static string DefaultDataRootPath => @"C:\MoneyFlow\Data";
    public static string DefaultCompaniesPath => @"C:\MoneyFlow\Data\";

    public static string? ConfiguredDataRootPath
    {
        get => _configuredDataRootPath;
        set => _configuredDataRootPath = value;
    }

    public static string? ConfiguredCompaniesPath
    {
        get => _configuredCompaniesPath;
        set => _configuredCompaniesPath = value != null ? EnsureTrailingSeparator(value) : null;
    }

    /// <summary>
    /// Configures the active storage paths for the running process.
    /// </summary>
    public static void SetConfiguredPaths(string? dataRootPath, string? companiesPath = null)
    {
        if (!string.IsNullOrWhiteSpace(dataRootPath))
        {
            _configuredDataRootPath = dataRootPath.Trim();
        }

        string? compPath = companiesPath ?? dataRootPath;
        if (!string.IsNullOrWhiteSpace(compPath))
        {
            _configuredCompaniesPath = EnsureTrailingSeparator(compPath.Trim());
        }
    }

    /// <summary>
    /// Resets all in-memory configured paths to null for clean testing.
    /// </summary>
    public static void Reset()
    {
        _configuredDataRootPath = null;
        _configuredCompaniesPath = null;
    }

    /// <summary>
    /// Ensures that a directory path ends with the appropriate directory separator char.
    /// </summary>
    public static string EnsureTrailingSeparator(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        char last = path[^1];
        if (last != Path.DirectorySeparatorChar && last != Path.AltDirectorySeparatorChar)
        {
            return path + Path.DirectorySeparatorChar;
        }
        return path;
    }

    /// <summary>
    /// Dynamically resolves the companies directory path as per configuration.
    /// </summary>
    public static string ResolveCompaniesPath(string? customPath = null)
    {
        // 1. Explicit custom path override
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            return EnsureTrailingSeparator(customPath.Trim());
        }

        // 2. Explicitly configured companies path
        if (!string.IsNullOrWhiteSpace(_configuredCompaniesPath))
        {
            return EnsureTrailingSeparator(_configuredCompaniesPath);
        }

        // 3. Explicitly configured data root path
        if (!string.IsNullOrWhiteSpace(_configuredDataRootPath))
        {
            return EnsureTrailingSeparator(_configuredDataRootPath);
        }

        // 4. Environment variable overrides
        string? envCompanies = Environment.GetEnvironmentVariable("MONEYFLOW_COMPANIES_PATH");
        if (!string.IsNullOrWhiteSpace(envCompanies))
        {
            return EnsureTrailingSeparator(envCompanies.Trim());
        }

        string? envDataRoot = Environment.GetEnvironmentVariable("MONEYFLOW_DATA_PATH");
        if (!string.IsNullOrWhiteSpace(envDataRoot))
        {
            return EnsureTrailingSeparator(envDataRoot.Trim());
        }

        // 5. Try reading setup marker
        string? setupPath = TryReadSetupMarkerDataPath();
        if (!string.IsNullOrWhiteSpace(setupPath))
        {
            return EnsureTrailingSeparator(setupPath);
        }

        // 6. Try reading appsettings.json from application base directory
        string? appSettingsCompanies = TryReadAppSettingsCompaniesPath();
        if (!string.IsNullOrWhiteSpace(appSettingsCompanies))
        {
            return EnsureTrailingSeparator(appSettingsCompanies);
        }

        string? appSettingsDataRoot = TryReadAppSettingsDataPath();
        if (!string.IsNullOrWhiteSpace(appSettingsDataRoot))
        {
            return EnsureTrailingSeparator(appSettingsDataRoot);
        }

        // 7. Standard default
        return DefaultCompaniesPath;
    }

    /// <summary>
    /// Dynamically resolves the root data storage directory as per configuration.
    /// </summary>
    public static string ResolveDataRootPath(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            return customPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        if (!string.IsNullOrWhiteSpace(_configuredDataRootPath))
        {
            return _configuredDataRootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        if (!string.IsNullOrWhiteSpace(_configuredCompaniesPath))
        {
            var parent = Directory.GetParent(_configuredCompaniesPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (parent != null)
            {
                return parent.FullName;
            }
        }

        string? envDataRoot = Environment.GetEnvironmentVariable("MONEYFLOW_DATA_PATH");
        if (!string.IsNullOrWhiteSpace(envDataRoot))
        {
            return envDataRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        string? setupPath = TryReadSetupMarkerDataPath();
        if (!string.IsNullOrWhiteSpace(setupPath))
        {
            return setupPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        string? appSettingsDataRoot = TryReadAppSettingsDataPath();
        if (!string.IsNullOrWhiteSpace(appSettingsDataRoot))
        {
            return appSettingsDataRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        return DefaultDataRootPath;
    }

    private static string? TryReadSetupMarkerDataPath()
    {
        try
        {
            string markerPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "MyERP",
                "System",
                ".setup_completed");

            if (File.Exists(markerPath))
            {
                var lines = File.ReadAllLines(markerPath);
                foreach (var line in lines)
                {
                    if (line.StartsWith("DataPath=", StringComparison.OrdinalIgnoreCase))
                    {
                        string val = line.Substring("DataPath=".Length).Trim();
                        if (!string.IsNullOrWhiteSpace(val)) return val;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private static string? TryReadAppSettingsCompaniesPath()
    {
        try
        {
            string appSettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(appSettingsFile))
            {
                string json = File.ReadAllText(appSettingsFile);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("StorageSettings", out var storageElem))
                {
                    if (storageElem.TryGetProperty("CompaniesPath", out var compProp) && compProp.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        string? compVal = compProp.GetString();
                        if (!string.IsNullOrWhiteSpace(compVal)) return compVal;
                    }
                }
            }
        }
        catch { }
        return null;
    }

    private static string? TryReadAppSettingsDataPath()
    {
        try
        {
            string appSettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(appSettingsFile))
            {
                string json = File.ReadAllText(appSettingsFile);
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("StorageSettings", out var storageElem))
                {
                    if (storageElem.TryGetProperty("DataRootPath", out var rootProp) && rootProp.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        string? rootVal = rootProp.GetString();
                        if (!string.IsNullOrWhiteSpace(rootVal)) return rootVal;
                    }
                }
            }
        }
        catch { }
        return null;
    }
}
