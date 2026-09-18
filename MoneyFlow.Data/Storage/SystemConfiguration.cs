using System;
using System.IO;
using System.Text.Json;

namespace MoneyFlow.Data.Storage;

/// <summary>
/// Application-level configuration stored in System\system.config.
/// Contains non-sensitive settings like regional defaults, company data path, last company, and UI preferences.
/// Company-sensitive data belongs in company.data, NOT here.
/// </summary>
public sealed class SystemConfiguration
{
    public int ConfigurationVersion { get; set; } = 1;

    // Regional & Accounting Configuration
    public string Country { get; set; } = "India";
    public string CurrencySymbol { get; set; } = "₹";
    public string CurrencyCode { get; set; } = "INR";
    public string AccountingTerminology { get; set; } = "India / SAARC";
    public string ComplianceProfile { get; set; } = "GST Compliant";
    public string CompanyDataPath { get; set; } = string.Empty;
    public string FinancialYearCycle { get; set; } = "01-Apr to 31-Mar";
    public string DecimalPrecision { get; set; } = "2 Decimals (0.00)";
    public int DecimalPlaces { get; set; } = 2;

    // Application Preferences
    public string? LastCompanyId { get; set; }
    public string? LastCompanyDisplayName { get; set; }
    public string Theme { get; set; } = "ExecLedger";
    public bool RememberLastCompany { get; set; } = true;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Loads system configuration from disk.
    /// Returns default config if file doesn't exist or is corrupted.
    /// </summary>
    public static SystemConfiguration Load(string basePath)
    {
        var configPath = Path.Combine(basePath, "System", "system.config");

        if (!File.Exists(configPath))
            return new SystemConfiguration();

        try
        {
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<SystemConfiguration>(json, JsonOptions) ?? new();
        }
        catch
        {
            return new SystemConfiguration();
        }
    }

    /// <summary>
    /// Saves system configuration to disk using an atomic write (tmp -> flush -> validate -> atomic replace).
    /// </summary>
    public void Save(string basePath)
    {
        SaveAtomic(basePath);
    }

    /// <summary>
    /// Atomically writes system configuration to disk.
    /// </summary>
    public void SaveAtomic(string basePath)
    {
        var systemDir = Path.Combine(basePath, "System");
        Directory.CreateDirectory(systemDir);

        var configPath = Path.Combine(systemDir, "system.config");
        var tmpPath = configPath + ".tmp";
        var json = JsonSerializer.Serialize(this, JsonOptions);

        using (var fs = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
        using (var writer = new StreamWriter(fs))
        {
            writer.Write(json);
            writer.Flush();
            fs.Flush(true);
        }

        // Validate temporary file readability before atomic commit
        var validatedJson = File.ReadAllText(tmpPath);
        if (string.IsNullOrWhiteSpace(validatedJson))
        {
            throw new IOException("Failed to validate written system configuration file.");
        }

        if (File.Exists(configPath))
        {
            File.Move(tmpPath, configPath, overwrite: true);
        }
        else
        {
            File.Move(tmpPath, configPath);
        }
    }
}
