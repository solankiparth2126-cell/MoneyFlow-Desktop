using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Forms;

public class SettingsForm : Form
{
    private readonly ISettingsService _settingsService;
    private readonly ICompanyService _companyService;
    private readonly IAuditService? _auditService;

    // Tab Control
    private TabControl tabControl = null!;

    // Tab 1: Accounting
    private ComboBox cmbDefaultCompany = null!;
    private CheckBox chkEnableLockDate = null!;
    private DateTimePicker dtpLockDate = null!;
    private CheckBox chkAutoRoundOff = null!;
    private CheckBox chkPrintAfterSave = null!;

    // Tab 2: Backup
    private TextBox txtBackupPath = null!;
    private Button btnBrowseBackup = null!;
    private CheckBox chkPromptBackupOnExit = null!;

    // Tab 3: Printing
    private ComboBox cmbPrinter = null!;
    private ComboBox cmbPaperSize = null!;
    private CheckBox chkDirectPrint = null!;

    // Tab 4: Regional
    private ComboBox cmbDateFormat = null!;
    private ComboBox cmbNumberFormat = null!;
    private TextBox txtCurrencySymbol = null!;
    private NumericUpDown nudDecimalPrecision = null!;
    private Label lblFormatPreview = null!;

    // Tab 5: Appearance
    private ComboBox cmbTheme = null!;
    private ComboBox cmbGridDensity = null!;

    // Buttons
    private Button btnSave = null!;
    private Button btnCancel = null!;
    private Button btnRestoreDefaults = null!;

    private ApplicationSettingsDto _currentSettings = new();

    public SettingsForm(
        ISettingsService settingsService,
        ICompanyService companyService,
        IAuditService? auditService = null)
    {
        _settingsService = settingsService;
        _companyService = companyService;
        _auditService = auditService;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Application & System Settings";
        Size = new Size(680, 540);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = Color.FromArgb(245, 247, 250);

        KeyDown += SettingsForm_KeyDown;

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.FromArgb(0, 77, 64),
            Padding = new Padding(15, 10, 15, 10)
        };

        var lblHeaderTitle = new Label
        {
            Text = "Settings & Configuration (F11)",
            Font = ExecLedgerTheme.UIBold12,
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(15, 12)
        };

        var lblHeaderSub = new Label
        {
            Text = "Configure accounting defaults, regional formatting, backup folders, and printer preferences.",
            Font = ExecLedgerTheme.UIRegular8,
            ForeColor = Color.FromArgb(200, 230, 220),
            AutoSize = true,
            Location = new Point(16, 36)
        };

        pnlHeader.Controls.Add(lblHeaderTitle);
        pnlHeader.Controls.Add(lblHeaderSub);

        // Bottom Action Panel
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = Color.FromArgb(235, 240, 245),
            Padding = new Padding(15, 10, 15, 10)
        };

        btnRestoreDefaults = new Button
        {
            Text = "Restore Defaults",
            Size = new Size(130, 32),
            Location = new Point(15, 11),
            BackColor = Color.White,
            FlatStyle = FlatStyle.System
        };
        btnRestoreDefaults.Click += (s, e) => RestoreDefaults();

        btnCancel = new Button
        {
            Text = "Cancel",
            Size = new Size(95, 32),
            Location = new Point(555, 11),
            BackColor = Color.White,
            FlatStyle = FlatStyle.System,
            DialogResult = DialogResult.Cancel
        };

        btnSave = new Button
        {
            Text = "Save Settings",
            Size = new Size(115, 32),
            Location = new Point(430, 11),
            BackColor = Color.FromArgb(0, 105, 92),
            ForeColor = Color.White,
            Font = ExecLedgerTheme.UIBold9,
            FlatStyle = FlatStyle.Flat
        };
        btnSave.FlatAppearance.BorderColor = Color.FromArgb(0, 77, 64);
        btnSave.Click += async (s, e) => await SaveSettingsAsync();

        pnlBottom.Controls.Add(btnRestoreDefaults);
        pnlBottom.Controls.Add(btnSave);
        pnlBottom.Controls.Add(btnCancel);

        // Main Tab Control
        tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(12, 6)
        };

        // Build Tabs
        BuildAccountingTab();
        BuildBackupTab();
        BuildPrintingTab();
        BuildRegionalTab();
        BuildAppearanceTab();

        Controls.Add(tabControl);
        Controls.Add(pnlBottom);
        Controls.Add(pnlHeader);

        Shown += async (s, e) => await LoadDataAsync();
    }

    private void BuildAccountingTab()
    {
        var tab = new TabPage("Accounting & Periods");
        tab.BackColor = Color.White;
        tab.Padding = new Padding(20);

        // Default Company
        var lblComp = new Label { Text = "Default Startup Company:", Location = new Point(20, 25), AutoSize = true, Font = ExecLedgerTheme.UIBold9 };
        cmbDefaultCompany = new ComboBox
        {
            Location = new Point(20, 48),
            Width = 400,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        // Voucher Lock Date Group
        var grpLock = new GroupBox
        {
            Text = "Period Lock & Protection",
            Location = new Point(20, 95),
            Size = new Size(600, 130),
            Font = ExecLedgerTheme.UIBold9
        };

        chkEnableLockDate = new CheckBox
        {
            Text = "Enable Voucher Lock Date",
            Location = new Point(20, 30),
            AutoSize = true,
            Font = ExecLedgerTheme.UIRegular9
        };
        chkEnableLockDate.CheckedChanged += (s, e) => dtpLockDate.Enabled = chkEnableLockDate.Checked;

        dtpLockDate = new DateTimePicker
        {
            Location = new Point(20, 60),
            Width = 160,
            Format = DateTimePickerFormat.Short,
            Enabled = false,
            Font = ExecLedgerTheme.UIRegular9
        };

        var lblLockNote = new Label
        {
            Text = "Transactions dated on or before this lock date cannot be added, edited, or cancelled.\nThis protects reconciled and audited financial periods from accidental alterations.",
            Location = new Point(20, 92),
            Size = new Size(560, 32),
            Font = ExecLedgerTheme.UIRegular8,
            ForeColor = Color.FromArgb(100, 110, 120)
        };

        grpLock.Controls.Add(chkEnableLockDate);
        grpLock.Controls.Add(dtpLockDate);
        grpLock.Controls.Add(lblLockNote);

        // Automation Group
        var grpAuto = new GroupBox
        {
            Text = "Transaction Behavior",
            Location = new Point(20, 240),
            Size = new Size(600, 100),
            Font = ExecLedgerTheme.UIBold9
        };

        chkAutoRoundOff = new CheckBox
        {
            Text = "Auto-calculate round-off split on sales/purchase vouchers",
            Location = new Point(20, 30),
            AutoSize = true,
            Font = ExecLedgerTheme.UIRegular9
        };

        chkPrintAfterSave = new CheckBox
        {
            Text = "Automatically open Print Preview dialog immediately after saving voucher",
            Location = new Point(20, 60),
            AutoSize = true,
            Font = ExecLedgerTheme.UIRegular9
        };

        grpAuto.Controls.Add(chkAutoRoundOff);
        grpAuto.Controls.Add(chkPrintAfterSave);

        tab.Controls.Add(lblComp);
        tab.Controls.Add(cmbDefaultCompany);
        tab.Controls.Add(grpLock);
        tab.Controls.Add(grpAuto);

        tabControl.TabPages.Add(tab);
    }

    private void BuildBackupTab()
    {
        var tab = new TabPage("Backup & Storage");
        tab.BackColor = Color.White;
        tab.Padding = new Padding(20);

        var lblBackupDir = new Label { Text = "Default Backup Folder:", Location = new Point(20, 25), AutoSize = true, Font = ExecLedgerTheme.UIBold9 };

        txtBackupPath = new TextBox
        {
            Location = new Point(20, 48),
            Width = 470,
            ReadOnly = true
        };

        btnBrowseBackup = new Button
        {
            Text = "Browse...",
            Location = new Point(500, 47),
            Size = new Size(85, 26)
        };
        btnBrowseBackup.Click += (s, e) =>
        {
            using var fbd = new FolderBrowserDialog();
            fbd.Description = "Select Default Backup Directory";
            if (Directory.Exists(txtBackupPath.Text))
                fbd.SelectedPath = txtBackupPath.Text;
            if (fbd.ShowDialog(this) == DialogResult.OK)
                txtBackupPath.Text = fbd.SelectedPath;
        };

        var grpBackupRules = new GroupBox
        {
            Text = "Backup Security & Automation",
            Location = new Point(20, 95),
            Size = new Size(600, 150),
            Font = ExecLedgerTheme.UIBold9
        };

        chkPromptBackupOnExit = new CheckBox
        {
            Text = "Prompt to create a backup when exiting MoneyFlow application",
            Location = new Point(20, 30),
            AutoSize = true,
            Font = ExecLedgerTheme.UIRegular9
        };

        var lblInfo = new Label
        {
            Text = "Backup System Highlights:\n" +
                   "• Dual format support: Portable compressed .mfb archives and native SQL Server .bak files.\n" +
                   "• Zero-Overwrite policy: Existing files are never silently replaced; timestamp suffixes are added.\n" +
                   "• SHA-256 integrity verification guarantees archive consistency prior to any restoration.",
            Location = new Point(20, 65),
            Size = new Size(560, 70),
            Font = ExecLedgerTheme.UIRegular8,
            ForeColor = Color.FromArgb(70, 80, 90)
        };

        grpBackupRules.Controls.Add(chkPromptBackupOnExit);
        grpBackupRules.Controls.Add(lblInfo);

        tab.Controls.Add(lblBackupDir);
        tab.Controls.Add(txtBackupPath);
        tab.Controls.Add(btnBrowseBackup);
        tab.Controls.Add(grpBackupRules);

        tabControl.TabPages.Add(tab);
    }

    private void BuildPrintingTab()
    {
        var tab = new TabPage("Printing & Output");
        tab.BackColor = Color.White;
        tab.Padding = new Padding(20);

        var lblPrinter = new Label { Text = "Default Windows Printer:", Location = new Point(20, 25), AutoSize = true, Font = ExecLedgerTheme.UIBold9 };
        cmbPrinter = new ComboBox
        {
            Location = new Point(20, 48),
            Width = 400,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        // Populate system printers
        cmbPrinter.Items.Add("[Use Windows Default Printer]");
        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            cmbPrinter.Items.Add(printer);
        }
        cmbPrinter.SelectedIndex = 0;

        var lblPaper = new Label { Text = "Default Paper Size:", Location = new Point(20, 95), AutoSize = true, Font = ExecLedgerTheme.UIBold9 };
        cmbPaperSize = new ComboBox
        {
            Location = new Point(20, 118),
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbPaperSize.Items.AddRange(new object[] { "A4", "Letter", "Legal", "Continuous Feed (Dot Matrix)" });
        cmbPaperSize.SelectedIndex = 0;

        var grpOutput = new GroupBox
        {
            Text = "Print Job Execution",
            Location = new Point(20, 165),
            Size = new Size(600, 90),
            Font = ExecLedgerTheme.UIBold9
        };

        chkDirectPrint = new CheckBox
        {
            Text = "Send directly to printer without displaying print preview modal",
            Location = new Point(20, 35),
            AutoSize = true,
            Font = ExecLedgerTheme.UIRegular9
        };

        grpOutput.Controls.Add(chkDirectPrint);

        tab.Controls.Add(lblPrinter);
        tab.Controls.Add(cmbPrinter);
        tab.Controls.Add(lblPaper);
        tab.Controls.Add(cmbPaperSize);
        tab.Controls.Add(grpOutput);

        tabControl.TabPages.Add(tab);
    }

    private void BuildRegionalTab()
    {
        var tab = new TabPage("Regional & Numbers");
        tab.BackColor = Color.White;
        tab.Padding = new Padding(20);

        // Date format
        var lblDate = new Label { Text = "Date Display Format:", Location = new Point(20, 20), AutoSize = true, Font = ExecLedgerTheme.UIBold9 };
        cmbDateFormat = new ComboBox
        {
            Location = new Point(20, 42),
            Width = 240,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbDateFormat.Items.AddRange(new object[] { "dd-MM-yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "MM/dd/yyyy" });
        cmbDateFormat.SelectedIndex = 0;
        cmbDateFormat.SelectedIndexChanged += (s, e) => UpdateFormatPreview();

        // Number format
        var lblNum = new Label { Text = "Number Grouping Style:", Location = new Point(300, 20), AutoSize = true, Font = ExecLedgerTheme.UIBold9 };
        cmbNumberFormat = new ComboBox
        {
            Location = new Point(300, 42),
            Width = 280,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbNumberFormat.Items.AddRange(new object[]
        {
            "Indian Format (12,34,567.89)",
            "Western Format (1,234,567.89)"
        });
        cmbNumberFormat.SelectedIndex = 0;
        cmbNumberFormat.SelectedIndexChanged += (s, e) => UpdateFormatPreview();

        // Currency Symbol
        var lblCurrency = new Label { Text = "Currency Symbol:", Location = new Point(20, 85), AutoSize = true, Font = ExecLedgerTheme.UIBold9 };
        txtCurrencySymbol = new TextBox
        {
            Location = new Point(20, 107),
            Width = 100,
            Text = "₹"
        };
        txtCurrencySymbol.TextChanged += (s, e) => UpdateFormatPreview();

        // Decimal Precision
        var lblDec = new Label { Text = "Decimal Places:", Location = new Point(300, 85), AutoSize = true, Font = ExecLedgerTheme.UIBold9 };
        nudDecimalPrecision = new NumericUpDown
        {
            Location = new Point(300, 107),
            Width = 80,
            Minimum = 0,
            Maximum = 4,
            Value = 2
        };
        nudDecimalPrecision.ValueChanged += (s, e) => UpdateFormatPreview();

        // Preview Box
        var grpPreview = new GroupBox
        {
            Text = "Live Formatting Preview",
            Location = new Point(20, 160),
            Size = new Size(560, 95),
            Font = ExecLedgerTheme.UIBold9
        };

        lblFormatPreview = new Label
        {
            Location = new Point(20, 30),
            Size = new Size(520, 50),
            Font = ExecLedgerTheme.UIBold11,
            ForeColor = Color.FromArgb(0, 105, 92),
            Text = "Sample Date: 11-09-2026\nSample Amount: ₹ 12,34,567.89"
        };
        grpPreview.Controls.Add(lblFormatPreview);

        tab.Controls.Add(lblDate);
        tab.Controls.Add(cmbDateFormat);
        tab.Controls.Add(lblNum);
        tab.Controls.Add(cmbNumberFormat);
        tab.Controls.Add(lblCurrency);
        tab.Controls.Add(txtCurrencySymbol);
        tab.Controls.Add(lblDec);
        tab.Controls.Add(nudDecimalPrecision);
        tab.Controls.Add(grpPreview);

        tabControl.TabPages.Add(tab);
    }

    private void BuildAppearanceTab()
    {
        var tab = new TabPage("Display & Theme");
        tab.BackColor = Color.White;
        tab.Padding = new Padding(20);

        var lblTheme = new Label { Text = "Application Theme:", Location = new Point(20, 25), AutoSize = true, Font = ExecLedgerTheme.UIBold9 };
        cmbTheme = new ComboBox
        {
            Location = new Point(20, 48),
            Width = 280,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbTheme.Items.AddRange(new object[]
        {
            "Classic Accounting Teal (Tally Style)",
            "Modern Dark Slate",
            "Light Neutral Office"
        });
        cmbTheme.SelectedIndex = 0;

        var lblDensity = new Label { Text = "Grid & UI Density:", Location = new Point(20, 95), AutoSize = true, Font = ExecLedgerTheme.UIBold9 };
        cmbGridDensity = new ComboBox
        {
            Location = new Point(20, 118),
            Width = 280,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbGridDensity.Items.AddRange(new object[]
        {
            "Compact (High density, fast keyboard navigation)",
            "Comfortable (Spacious row padding)"
        });
        cmbGridDensity.SelectedIndex = 0;

        var lblThemeNote = new Label
        {
            Text = "Theme customizations apply globally across all financial ledgers, vouchers, and report grids.",
            Location = new Point(20, 170),
            Size = new Size(560, 40),
            Font = ExecLedgerTheme.UIRegular8,
            ForeColor = Color.FromArgb(100, 110, 120)
        };

        tab.Controls.Add(lblTheme);
        tab.Controls.Add(cmbTheme);
        tab.Controls.Add(lblDensity);
        tab.Controls.Add(cmbGridDensity);
        tab.Controls.Add(lblThemeNote);

        tabControl.TabPages.Add(tab);
    }

    private async Task LoadDataAsync()
    {
        try
        {
            // Load companies into dropdown
            var companies = await _companyService.GetAllCompaniesAsync();
            cmbDefaultCompany.Items.Clear();
            cmbDefaultCompany.Items.Add(new ComboBoxItem(null, "[None / Prompt on Startup]"));

            int selectedCompanyIndex = 0;
            for (int i = 0; i < companies.Count; i++)
            {
                var c = companies[i];
                var item = new ComboBoxItem(c.CompanyId, c.CompanyName);
                cmbDefaultCompany.Items.Add(item);
            }
            cmbDefaultCompany.SelectedIndex = selectedCompanyIndex;

            // Load saved settings
            _currentSettings = await _settingsService.GetSettingsAsync();

            // Populate Tab 1
            if (_currentSettings.DefaultCompanyId.HasValue)
            {
                for (int i = 0; i < cmbDefaultCompany.Items.Count; i++)
                {
                    if (cmbDefaultCompany.Items[i] is ComboBoxItem item && item.Id == _currentSettings.DefaultCompanyId.Value)
                    {
                        cmbDefaultCompany.SelectedIndex = i;
                        break;
                    }
                }
            }

            if (_currentSettings.VoucherLockDate.HasValue)
            {
                chkEnableLockDate.Checked = true;
                dtpLockDate.Value = _currentSettings.VoucherLockDate.Value;
                dtpLockDate.Enabled = true;
            }
            else
            {
                chkEnableLockDate.Checked = false;
                dtpLockDate.Value = DateTime.Today;
                dtpLockDate.Enabled = false;
            }

            chkAutoRoundOff.Checked = _currentSettings.AutoRoundOffVouchers;
            chkPrintAfterSave.Checked = _currentSettings.PrintVoucherAfterSave;

            // Populate Tab 2
            txtBackupPath.Text = _currentSettings.DefaultBackupPath;
            chkPromptBackupOnExit.Checked = _currentSettings.PromptBackupOnExit;

            // Populate Tab 3
            if (!string.IsNullOrEmpty(_currentSettings.DefaultPrinterName))
            {
                int printerIdx = cmbPrinter.FindStringExact(_currentSettings.DefaultPrinterName);
                cmbPrinter.SelectedIndex = printerIdx >= 0 ? printerIdx : 0;
            }
            else
            {
                cmbPrinter.SelectedIndex = 0;
            }

            int paperIdx = cmbPaperSize.FindStringExact(_currentSettings.PaperSize);
            cmbPaperSize.SelectedIndex = paperIdx >= 0 ? paperIdx : 0;
            chkDirectPrint.Checked = _currentSettings.DirectPrintWithoutPreview;

            // Populate Tab 4
            int dateIdx = cmbDateFormat.FindStringExact(_currentSettings.DateFormat);
            cmbDateFormat.SelectedIndex = dateIdx >= 0 ? dateIdx : 0;

            cmbNumberFormat.SelectedIndex = string.Equals(_currentSettings.NumberFormat, "Western", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
            txtCurrencySymbol.Text = _currentSettings.CurrencySymbol;
            nudDecimalPrecision.Value = Math.Clamp(_currentSettings.DecimalPrecision, 0, 4);

            // Populate Tab 5
            if (_currentSettings.Theme == "DarkSlate") cmbTheme.SelectedIndex = 1;
            else if (_currentSettings.Theme == "LightNeutral") cmbTheme.SelectedIndex = 2;
            else cmbTheme.SelectedIndex = 0;

            cmbGridDensity.SelectedIndex = _currentSettings.GridDensity == "Comfortable" ? 1 : 0;

            UpdateFormatPreview();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load settings: {ex.Message}", "Settings Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UpdateFormatPreview()
    {
        try
        {
            var dateFmt = cmbDateFormat.SelectedItem?.ToString() ?? "dd-MM-yyyy";
            var isIndian = cmbNumberFormat.SelectedIndex == 0;
            var symbol = txtCurrencySymbol.Text;
            var precision = (int)nudDecimalPrecision.Value;

            var culture = isIndian ? new System.Globalization.CultureInfo("en-IN") : System.Globalization.CultureInfo.InvariantCulture;
            var sampleAmount = 1234567.89m;
            var formattedAmount = sampleAmount.ToString("N" + precision, culture);
            var formattedDate = DateTime.Today.ToString(dateFmt, System.Globalization.CultureInfo.InvariantCulture);

            lblFormatPreview.Text = $"Sample Date: {formattedDate}\nSample Amount: {symbol} {formattedAmount}";
        }
        catch
        {
            // Ignored on formatting trial
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            var settings = new ApplicationSettingsDto();

            // Tab 1
            if (cmbDefaultCompany.SelectedItem is ComboBoxItem compItem && compItem.Id.HasValue)
            {
                settings.DefaultCompanyId = compItem.Id.Value;
            }
            else
            {
                settings.DefaultCompanyId = null;
            }

            settings.VoucherLockDate = chkEnableLockDate.Checked ? dtpLockDate.Value.Date : null;
            settings.AutoRoundOffVouchers = chkAutoRoundOff.Checked;
            settings.PrintVoucherAfterSave = chkPrintAfterSave.Checked;

            // Tab 2
            settings.DefaultBackupPath = txtBackupPath.Text.Trim();
            settings.PromptBackupOnExit = chkPromptBackupOnExit.Checked;

            // Tab 3
            var selectedPrinter = cmbPrinter.SelectedItem?.ToString();
            settings.DefaultPrinterName = (selectedPrinter != null && !selectedPrinter.StartsWith("[")) ? selectedPrinter : string.Empty;
            settings.PaperSize = cmbPaperSize.SelectedItem?.ToString() ?? "A4";
            settings.DirectPrintWithoutPreview = chkDirectPrint.Checked;

            // Tab 4
            settings.DateFormat = cmbDateFormat.SelectedItem?.ToString() ?? "dd-MM-yyyy";
            settings.NumberFormat = cmbNumberFormat.SelectedIndex == 1 ? "Western" : "Indian";
            settings.CurrencySymbol = txtCurrencySymbol.Text.Trim();
            settings.DecimalPrecision = (int)nudDecimalPrecision.Value;

            // Tab 5
            settings.Theme = cmbTheme.SelectedIndex switch
            {
                1 => "DarkSlate",
                2 => "LightNeutral",
                _ => "ClassicTeal"
            };
            settings.GridDensity = cmbGridDensity.SelectedIndex == 1 ? "Comfortable" : "Compact";

            await _settingsService.SaveSettingsAsync(settings);

            if (_auditService != null)
            {
                await _auditService.LogAsync(settings.DefaultCompanyId, "Edit", "Settings", "1", "Updated application & system configuration settings");
            }

            MessageBox.Show("Settings saved successfully.", "Settings Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save settings: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RestoreDefaults()
    {
        if (MessageBox.Show("Reset all settings to default factory values?", "Restore Defaults", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        cmbDefaultCompany.SelectedIndex = 0;
        chkEnableLockDate.Checked = false;
        dtpLockDate.Value = DateTime.Today;
        dtpLockDate.Enabled = false;
        chkAutoRoundOff.Checked = false;
        chkPrintAfterSave.Checked = false;

        var defaultDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        txtBackupPath.Text = Path.Combine(defaultDocs, "Executive Ledger", "Backups");
        chkPromptBackupOnExit = new CheckBox { Checked = true };

        cmbPrinter.SelectedIndex = 0;
        cmbPaperSize.SelectedIndex = 0;
        chkDirectPrint.Checked = false;

        cmbDateFormat.SelectedIndex = 0;
        cmbNumberFormat.SelectedIndex = 0;
        txtCurrencySymbol.Text = "₹";
        nudDecimalPrecision.Value = 2;

        cmbTheme.SelectedIndex = 0;
        cmbGridDensity.SelectedIndex = 0;

        UpdateFormatPreview();
    }

    private void SettingsForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
        }
    }

    private sealed class ComboBoxItem
    {
        public int? Id { get; }
        public string Text { get; }

        public ComboBoxItem(int? id, string text)
        {
            Id = id;
            Text = text;
        }

        public override string ToString() => Text;
    }
}
