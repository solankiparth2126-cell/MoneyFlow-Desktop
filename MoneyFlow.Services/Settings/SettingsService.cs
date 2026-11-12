using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Services.Settings;

public class SettingsService : ISettingsService
{
    private readonly IRepository<AppSetting> _settingRepo;
    private readonly ICompanyRepository _companyRepo;
    private readonly IUnitOfWork _unitOfWork;
    private ApplicationSettingsDto? _cachedSettings;

    // Setting key constants
    public const string KeyDefaultCompanyId = "Company.DefaultCompanyId";
    public const string KeyVoucherLockDate = "Accounting.VoucherLockDate";
    public const string KeyAutoRoundOff = "Accounting.AutoRoundOff";
    public const string KeyPrintAfterSave = "Accounting.PrintAfterSave";
    public const string KeyBackupPath = "Backup.DefaultPath";
    public const string KeyPromptBackupOnExit = "Backup.PromptOnExit";
    public const string KeyPrinterName = "Printer.DefaultName";
    public const string KeyPaperSize = "Printer.PaperSize";
    public const string KeyDirectPrint = "Printer.DirectPrint";
    public const string KeyDateFormat = "Regional.DateFormat";
    public const string KeyNumberFormat = "Regional.NumberFormat";
    public const string KeyDecimalPrecision = "Regional.DecimalPrecision";
    public const string KeyCurrencySymbol = "Regional.CurrencySymbol";
    public const string KeyTheme = "Appearance.Theme";
    public const string KeyGridDensity = "Appearance.GridDensity";

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public SettingsService(
        IRepository<AppSetting> settingRepo,
        ICompanyRepository companyRepo,
        IUnitOfWork unitOfWork)
    {
        _settingRepo = settingRepo;
        _companyRepo = companyRepo;
        _unitOfWork = unitOfWork;
    }

    internal SettingsService(MoneyFlow.Data.AppDbContext context)
        : this(new MoneyFlow.Data.Repositories.Repository<AppSetting>(context), new MoneyFlow.Data.Repositories.CompanyRepository(context), new MoneyFlow.Data.Repositories.UnitOfWork(context))
    {
    }

    public async Task<ApplicationSettingsDto> GetSettingsAsync()
    {
        var allSettings = await _settingRepo.GetAllAsync();
        var settingsDict = allSettings.ToDictionary(s => s.SettingKey, s => s.SettingValue, StringComparer.OrdinalIgnoreCase);

        var dto = new ApplicationSettingsDto();

        // Default Company
        if (settingsDict.TryGetValue(KeyDefaultCompanyId, out var compIdStr) && int.TryParse(compIdStr, out var compId))
        {
            dto.DefaultCompanyId = compId;
            var comp = await _companyRepo.GetByIdAsync(compId);
            dto.DefaultCompanyName = comp?.CompanyName;
        }

        // Voucher Lock Date
        if (settingsDict.TryGetValue(KeyVoucherLockDate, out var lockDateStr) && DateTime.TryParse(lockDateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var lockDate))
        {
            dto.VoucherLockDate = lockDate;
        }

        // Booleans
        dto.AutoRoundOffVouchers = GetBool(settingsDict, KeyAutoRoundOff, false);
        dto.PrintVoucherAfterSave = GetBool(settingsDict, KeyPrintAfterSave, false);
        dto.PromptBackupOnExit = GetBool(settingsDict, KeyPromptBackupOnExit, true);
        dto.DirectPrintWithoutPreview = GetBool(settingsDict, KeyDirectPrint, false);

        // Backup Path
        var defaultDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var defaultBackup = Path.Combine(defaultDocs, "MoneyFlow", "Backups");
        dto.DefaultBackupPath = settingsDict.TryGetValue(KeyBackupPath, out var bPath) && !string.IsNullOrWhiteSpace(bPath)
            ? bPath
            : defaultBackup;

        // Printer
        dto.DefaultPrinterName = settingsDict.TryGetValue(KeyPrinterName, out var pName) ? pName : string.Empty;
        dto.PaperSize = settingsDict.TryGetValue(KeyPaperSize, out var pSize) && !string.IsNullOrWhiteSpace(pSize) ? pSize : "A4";

        // Regional & Formatting
        dto.DateFormat = settingsDict.TryGetValue(KeyDateFormat, out var dFmt) && !string.IsNullOrWhiteSpace(dFmt) ? dFmt : "dd-MM-yyyy";
        dto.NumberFormat = settingsDict.TryGetValue(KeyNumberFormat, out var nFmt) && !string.IsNullOrWhiteSpace(nFmt) ? nFmt : "Indian";
        dto.DecimalPrecision = settingsDict.TryGetValue(KeyDecimalPrecision, out var dPrec) && int.TryParse(dPrec, out var prec) ? Math.Clamp(prec, 0, 4) : 2;
        dto.CurrencySymbol = settingsDict.TryGetValue(KeyCurrencySymbol, out var cSym) && !string.IsNullOrWhiteSpace(cSym) ? cSym : "₹";

        // Appearance
        dto.Theme = settingsDict.TryGetValue(KeyTheme, out var thm) && !string.IsNullOrWhiteSpace(thm) ? thm : "ClassicTeal";
        dto.GridDensity = settingsDict.TryGetValue(KeyGridDensity, out var dens) && !string.IsNullOrWhiteSpace(dens) ? dens : "Compact";

        _cachedSettings = dto;
        return dto;
    }

    public async Task SaveSettingsAsync(ApplicationSettingsDto settings)
    {
        var entries = new Dictionary<string, (string Value, string Description)>
        {
            { KeyDefaultCompanyId, (settings.DefaultCompanyId?.ToString() ?? string.Empty, "Default company opened on startup") },
            { KeyVoucherLockDate, (settings.VoucherLockDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty, "Accounting lock date before which vouchers cannot be modified") },
            { KeyAutoRoundOff, (settings.AutoRoundOffVouchers.ToString(), "Auto-suggest round-off splits on vouchers") },
            { KeyPrintAfterSave, (settings.PrintVoucherAfterSave.ToString(), "Prompt/open print preview after saving voucher") },
            { KeyBackupPath, (settings.DefaultBackupPath, "Default folder location for local backups") },
            { KeyPromptBackupOnExit, (settings.PromptBackupOnExit.ToString(), "Prompt for backup creation when closing application") },
            { KeyPrinterName, (settings.DefaultPrinterName, "Default Windows printer name") },
            { KeyPaperSize, (settings.PaperSize, "Default paper size (A4, Letter, etc.)") },
            { KeyDirectPrint, (settings.DirectPrintWithoutPreview.ToString(), "Direct print without showing preview dialog") },
            { KeyDateFormat, (settings.DateFormat, "System date display format") },
            { KeyNumberFormat, (settings.NumberFormat, "Number formatting style (Indian vs Western)") },
            { KeyDecimalPrecision, (settings.DecimalPrecision.ToString(), "Monetary decimal places precision") },
            { KeyCurrencySymbol, (settings.CurrencySymbol, "Currency sign prefix") },
            { KeyTheme, (settings.Theme, "Application theme") },
            { KeyGridDensity, (settings.GridDensity, "Data grid display density") }
        };

        var existingList = await _settingRepo.GetAllAsync();
        var existingDict = existingList.ToDictionary(s => s.SettingKey, StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in entries)
        {
            if (existingDict.TryGetValue(kvp.Key, out var existing))
            {
                existing.SettingValue = kvp.Value.Value;
                existing.Description = kvp.Value.Description;
                _settingRepo.Update(existing);
            }
            else
            {
                var newSetting = new AppSetting
                {
                    SettingKey = kvp.Key,
                    SettingValue = kvp.Value.Value,
                    Description = kvp.Value.Description
                };
                await _settingRepo.AddAsync(newSetting);
            }
        }

        await _unitOfWork.SaveChangesAsync();
        _cachedSettings = settings;
    }

    public async Task<string?> GetSettingValueAsync(string key, string? defaultValue = null)
    {
        var setting = (await _settingRepo.FindAsync(s => s.SettingKey.ToLower() == key.ToLower())).FirstOrDefault();
        return setting?.SettingValue ?? defaultValue;
    }

    public async Task SetSettingValueAsync(string key, string value, string? description = null)
    {
        var existing = (await _settingRepo.FindAsync(s => s.SettingKey.ToLower() == key.ToLower())).FirstOrDefault();
        if (existing != null)
        {
            existing.SettingValue = value;
            if (description != null) existing.Description = description;
            _settingRepo.Update(existing);
        }
        else
        {
            await _settingRepo.AddAsync(new AppSetting
            {
                SettingKey = key,
                SettingValue = value,
                Description = description ?? string.Empty
            });
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task<bool> IsDateLockedAsync(DateTime date)
    {
        var settings = _cachedSettings ?? await GetSettingsAsync();
        if (settings.VoucherLockDate.HasValue)
        {
            return date.Date <= settings.VoucherLockDate.Value.Date;
        }
        return false;
    }

    public string FormatDate(DateTime date)
    {
        var format = _cachedSettings?.DateFormat ?? "dd-MM-yyyy";
        return date.ToString(format, CultureInfo.InvariantCulture);
    }

    public string FormatCurrency(decimal amount)
    {
        var precision = _cachedSettings?.DecimalPrecision ?? 2;
        var formatType = _cachedSettings?.NumberFormat ?? "Indian";
        var symbol = _cachedSettings?.CurrencySymbol ?? "₹";

        CultureInfo culture;
        if (string.Equals(formatType, "Indian", StringComparison.OrdinalIgnoreCase))
        {
            culture = new CultureInfo("en-IN");
        }
        else
        {
            culture = CultureInfo.InvariantCulture;
        }

        var formatSpecifier = "N" + precision;
        var formattedNumber = Math.Abs(amount).ToString(formatSpecifier, culture);

        var prefix = amount < 0 ? "-" : "";
        var space = string.IsNullOrWhiteSpace(symbol) ? "" : " ";

        return $"{prefix}{symbol}{space}{formattedNumber}";
    }

    private static bool GetBool(Dictionary<string, string> dict, string key, bool defaultValue)
    {
        if (dict.TryGetValue(key, out var val) && bool.TryParse(val, out var parsed))
        {
            return parsed;
        }
        return defaultValue;
    }
}
