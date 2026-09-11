using System;

namespace MoneyFlow.Core.DTOs;

public class ApplicationSettingsDto
{
    // Company & Accounting (Master Prompt Section 48)
    public int? DefaultCompanyId { get; set; }
    public string? DefaultCompanyName { get; set; }
    public DateTime? VoucherLockDate { get; set; }
    public bool AutoRoundOffVouchers { get; set; }
    public bool PrintVoucherAfterSave { get; set; }

    // Backup & Storage (Section 48 & 43)
    public string DefaultBackupPath { get; set; } = string.Empty;
    public bool PromptBackupOnExit { get; set; } = true;

    // Printer & Output (Section 48 & 57)
    public string DefaultPrinterName { get; set; } = string.Empty;
    public string PaperSize { get; set; } = "A4";
    public bool DirectPrintWithoutPreview { get; set; }

    // Regional & Formatting (Section 48)
    public string DateFormat { get; set; } = "dd-MM-yyyy";
    public string NumberFormat { get; set; } = "Indian"; // "Indian" (12,34,567.89) or "Western" (1,234,567.89)
    public int DecimalPrecision { get; set; } = 2;
    public string CurrencySymbol { get; set; } = "₹";

    // Application Appearance & Theme (Section 48)
    public string Theme { get; set; } = "ClassicTeal"; // "ClassicTeal", "DarkSlate", "LightNeutral"
    public string GridDensity { get; set; } = "Compact"; // "Compact", "Comfortable"
}

public class SettingItemDto
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
