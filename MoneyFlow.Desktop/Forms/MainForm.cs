using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Controls;
using MoneyFlow.Desktop.Navigation;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Gateway of Accounting: The primary shell of the MoneyFlow application.
/// Focuses purely on Gateway layout, status bar, and shortcut dispatching via INavigationService.
/// </summary>
public class MainForm : Form
{
    private readonly ICompanyContext _companyContext;
    private readonly IUserContext _userContext;
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;
    private readonly IBackupRestoreService? _backupRestoreService;

    // UI Controls
    private MenuStrip menuStrip = null!;
    private ToolStrip toolStrip = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel lblStatusCompany = null!;
    private ToolStripStatusLabel lblStatusFY = null!;
    private ToolStripStatusLabel lblStatusDatabase = null!;
    private ToolStripStatusLabel lblStatusUser = null!;

    // Gateway navigation panel
    private Panel gatewayPanel = null!;
    private ListBox lstGatewayMenu = null!;
    private Label lblCurrentCompany = null!;
    private Label lblCurrentFY = null!;

    public MainForm(
        ICompanyContext companyContext,
        IUserContext userContext,
        INavigationService navigationService,
        ISettingsService settingsService,
        IBackupRestoreService? backupRestoreService = null)
    {
        _companyContext = companyContext ?? throw new ArgumentNullException(nameof(companyContext));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _backupRestoreService = backupRestoreService;

        InitializeComponent();

        _companyContext.OnCompanyChanged += UpdateCompanyContextUI;
        _userContext.OnUserChanged += UpdateUserContextUI;
        UpdateCompanyContextUI();
        UpdateUserContextUI();
        _ = ApplyCurrentSettingsThemeAsync();
    }

    private void InitializeComponent()
    {
        Text = "MoneyFlow Desktop ERP — Gateway of Accounting";
        Size = new Size(1024, 680);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        BackColor = Color.FromArgb(240, 243, 246);
        Font = new Font("Segoe UI", 9.5F);

        // 1. MenuStrip
        menuStrip = new MenuStrip
        {
            BackColor = Color.FromArgb(24, 43, 73),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5F)
        };

        var menuCompany = new ToolStripMenuItem("&Company");
        menuCompany.DropDownItems.Add("Select Company (F3)", null, (s, e) => _navigationService.OpenCompanyList(this));
        menuCompany.DropDownItems.Add("Create Company", null, (s, e) => _navigationService.OpenCreateCompany(this));
        menuCompany.DropDownItems.Add("Alter Company", null, (s, e) => _navigationService.OpenAlterCompany(this));
        menuCompany.DropDownItems.Add("Change Financial Year (F2)", null, (s, e) => _navigationService.OpenFinancialYearList(this));
        menuCompany.DropDownItems.Add("Close Company", null, (s, e) => _navigationService.CloseActiveCompany(this));
        menuCompany.DropDownItems.Add(new ToolStripSeparator());
        menuCompany.DropDownItems.Add("Switch User / Login...", null, (s, e) => _navigationService.OpenLoginForm(this, UpdateUserContextUI));
        menuCompany.DropDownItems.Add(new ToolStripSeparator());
        menuCompany.DropDownItems.Add("E&xit (Esc)", null, (s, e) => Application.Exit());

        var menuMasters = new ToolStripMenuItem("&Masters");
        menuMasters.DropDownItems.Add("&Groups (Chart of Accounts)", null, (s, e) => _navigationService.OpenGroupList(this));
        menuMasters.DropDownItems.Add("&Ledgers", null, (s, e) => _navigationService.OpenLedgerList(this));
        menuMasters.DropDownItems.Add("&Stock Items", null, (s, e) => _navigationService.OpenStockItemList(this));
        menuMasters.DropDownItems.Add("&Units of Measure", null, (s, e) => _navigationService.OpenUnitList(this));

        var menuTransactions = new ToolStripMenuItem("&Transactions");
        menuTransactions.DropDownItems.Add("F4 - &Contra", null, (s, e) => _navigationService.OpenContraVoucher(this));
        menuTransactions.DropDownItems.Add("F5 - &Payment", null, (s, e) => _navigationService.OpenPaymentVoucher(this));
        menuTransactions.DropDownItems.Add("F6 - &Receipt", null, (s, e) => _navigationService.OpenReceiptVoucher(this));
        menuTransactions.DropDownItems.Add("F7 - &Journal", null, (s, e) => _navigationService.OpenJournalVoucher(this));
        menuTransactions.DropDownItems.Add("F8 - &Sales", null, (s, e) => _navigationService.OpenSalesVoucher(this));
        menuTransactions.DropDownItems.Add("F9 - &Purchase", null, (s, e) => _navigationService.OpenPurchaseVoucher(this));
        menuTransactions.DropDownItems.Add("&Debit Note (Ctrl+F9)", null, (s, e) => _navigationService.OpenDebitNote(this));
        menuTransactions.DropDownItems.Add("&Credit Note (Ctrl+F8)", null, (s, e) => _navigationService.OpenCreditNote(this));

        var menuReports = new ToolStripMenuItem("&Reports");
        menuReports.DropDownItems.Add("&Dashboard", null, (s, e) => _navigationService.OpenDashboard(this));
        menuReports.DropDownItems.Add(new ToolStripSeparator());
        menuReports.DropDownItems.Add("&Day Book", null, (s, e) => _navigationService.OpenDayBook(this));
        menuReports.DropDownItems.Add("&Ledger Statement", null, (s, e) => _navigationService.OpenLedgerStatement(this));
        menuReports.DropDownItems.Add("&Trial Balance", null, (s, e) => _navigationService.OpenTrialBalance(this));
        menuReports.DropDownItems.Add("&Profit & Loss", null, (s, e) => _navigationService.OpenProfitLoss(this));
        menuReports.DropDownItems.Add("&Balance Sheet", null, (s, e) => _navigationService.OpenBalanceSheet(this));
        menuReports.DropDownItems.Add("&Cash / Bank Book", null, (s, e) => _navigationService.OpenCashBankBook(this));
        menuReports.DropDownItems.Add("&Outstanding Analysis", null, (s, e) => _navigationService.OpenOutstandingReport(this));
        menuReports.DropDownItems.Add("&Stock Summary", null, (s, e) => _navigationService.OpenStockSummary(this));

        var menuUtilities = new ToolStripMenuItem("&Utilities");
        menuUtilities.DropDownItems.Add("&Global Search (Alt+G)", null, (s, e) => _navigationService.OpenGlobalSearch(this));
        menuUtilities.DropDownItems.Add("&Import / Export Data...", null, (s, e) => _navigationService.OpenImportExport(this));
        menuUtilities.DropDownItems.Add(new ToolStripSeparator());
        menuUtilities.DropDownItems.Add("&Backup & Restore System (F10)...", null, (s, e) => _navigationService.OpenBackupRestore(this));
        menuUtilities.DropDownItems.Add(new ToolStripSeparator());
        menuUtilities.DropDownItems.Add("&User Management & Permissions...", null, (s, e) => _navigationService.OpenUserManagement(this));
        menuUtilities.DropDownItems.Add("&Settings & Configuration (F11)...", null, (s, e) => _navigationService.OpenSettings(this, () => _ = ApplyCurrentSettingsThemeAsync()));
        menuUtilities.DropDownItems.Add(new ToolStripSeparator());
        menuUtilities.DropDownItems.Add("Connection Diagnostics", null, (s, e) => _navigationService.OpenDatabaseDiagnostics(this));

        menuStrip.Items.AddRange(new ToolStripItem[] {
            menuCompany,
            menuMasters,
            menuTransactions,
            menuReports,
            menuUtilities
        });
        MainMenuStrip = menuStrip;
        Controls.Add(menuStrip);

        // 2. ToolStrip (Quick Action & Shortcuts)
        toolStrip = new ToolStrip
        {
            BackColor = Color.FromArgb(234, 240, 246),
            GripStyle = ToolStripGripStyle.Hidden,
            Font = new Font("Segoe UI", 9F)
        };
        toolStrip.Items.Add(new ToolStripLabel("Shortcuts: "));
        toolStrip.Items.Add(new ToolStripButton("Go To (Alt+G)", null, (s, e) => _navigationService.OpenGlobalSearch(this)) { BackColor = Color.FromArgb(24, 43, 73), ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
        toolStrip.Items.Add(new ToolStripButton("Dashboard", null, (s, e) => _navigationService.OpenDashboard(this)) { BackColor = Color.FromArgb(41, 128, 185), ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripButton("F2: Period / FY", null, (s, e) => _navigationService.OpenFinancialYearList(this)));
        toolStrip.Items.Add(new ToolStripButton("F3: Company", null, (s, e) => _navigationService.OpenCompanyList(this)));
        toolStrip.Items.Add(new ToolStripButton("F4: Contra", null, (s, e) => _navigationService.OpenContraVoucher(this)));
        toolStrip.Items.Add(new ToolStripButton("F5: Payment", null, (s, e) => _navigationService.OpenPaymentVoucher(this)));
        toolStrip.Items.Add(new ToolStripButton("F6: Receipt", null, (s, e) => _navigationService.OpenReceiptVoucher(this)));
        toolStrip.Items.Add(new ToolStripButton("F7: Journal", null, (s, e) => _navigationService.OpenJournalVoucher(this)));
        toolStrip.Items.Add(new ToolStripButton("F8: Sales", null, (s, e) => _navigationService.OpenSalesVoucher(this)));
        toolStrip.Items.Add(new ToolStripButton("F9: Purchase", null, (s, e) => _navigationService.OpenPurchaseVoucher(this)));
        toolStrip.Items.Add(new ToolStripButton("Debit Note (Ctrl+F9)", null, (s, e) => _navigationService.OpenDebitNote(this)));
        toolStrip.Items.Add(new ToolStripButton("Credit Note (Ctrl+F8)", null, (s, e) => _navigationService.OpenCreditNote(this)));
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripButton("F10: Backup / Restore", null, (s, e) => _navigationService.OpenBackupRestore(this)) { BackColor = Color.FromArgb(39, 174, 96), ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
        toolStrip.Items.Add(new ToolStripButton("F11: Settings", null, (s, e) => _navigationService.OpenSettings(this, () => _ = ApplyCurrentSettingsThemeAsync())) { BackColor = Color.FromArgb(0, 105, 92), ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripButton("DB Diagnostics", null, (s, e) => _navigationService.OpenDatabaseDiagnostics(this)));
        Controls.Add(toolStrip);

        // 3. Central Gateway of Accounting Layout
        CreateGatewayLayout();

        // 4. StatusStrip
        statusStrip = new StatusStrip
        {
            BackColor = Color.FromArgb(24, 43, 73),
            ForeColor = Color.White
        };

        lblStatusCompany = new ToolStripStatusLabel("Company: [None Selected]") { ForeColor = Color.White };
        lblStatusFY = new ToolStripStatusLabel(" | FY: Not Selected") { ForeColor = Color.LightGreen };
        lblStatusDatabase = new ToolStripStatusLabel(" | DB: MoneyFlowDB (Connected)") { ForeColor = Color.LightSkyBlue };
        lblStatusUser = new ToolStripStatusLabel(" | User: admin (Administrator)") { ForeColor = Color.LightYellow };

        statusStrip.Items.AddRange(new ToolStripItem[] {
            lblStatusCompany,
            lblStatusFY,
            lblStatusDatabase,
            lblStatusUser
        });
        Controls.Add(statusStrip);

        // 5. Global Keyboard Shortcuts Handler
        KeyDown += MainForm_KeyDown;
    }

    private Label lblCurrentPeriod = null!;
    private Label lblCurrentDate = null!;
    private Label lblCompanyNameRow = null!;
    private Label lblLastEntryDate = null!;
    private TallySideActionBar sideActionBar = null!;

    private void CreateGatewayLayout()
    {
        sideActionBar = new TallySideActionBar();
        sideActionBar.DateClicked += () => _navigationService.OpenFinancialYearList(this);
        sideActionBar.CompanyClicked += () => _navigationService.OpenCompanyList(this);
        sideActionBar.ContraClicked += () => _navigationService.OpenContraVoucher(this);
        sideActionBar.PaymentClicked += () => _navigationService.OpenPaymentVoucher(this);
        sideActionBar.ReceiptClicked += () => _navigationService.OpenReceiptVoucher(this);
        sideActionBar.JournalClicked += () => _navigationService.OpenJournalVoucher(this);
        sideActionBar.SalesClicked += () => _navigationService.OpenSalesVoucher(this);
        sideActionBar.PurchaseClicked += () => _navigationService.OpenPurchaseVoucher(this);
        Controls.Add(sideActionBar);

        var centerContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(20, 15, 20, 15)
        };

        var splitLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.White
        };
        splitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        splitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        // --- Left Panel: Period, Current Date, Company Name & Last Entry Date ---
        var pnlLeftInfo = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(10)
        };

        var lblCurrentPeriodTag = new Label
        {
            Text = "CURRENT PERIOD",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 90, 156),
            Location = new Point(10, 8),
            AutoSize = true
        };

        lblCurrentPeriod = new Label
        {
            Text = "1-Apr-26 to 31-Mar-27",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.Black,
            Location = new Point(10, 24),
            AutoSize = true
        };

        var lblCurrentDateTag = new Label
        {
            Text = "CURRENT DATE",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 90, 156),
            Location = new Point(260, 8),
            AutoSize = true
        };

        lblCurrentDate = new Label
        {
            Text = DateTime.Today.ToString("dddd, d-MMM-yyyy"),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.Black,
            Location = new Point(260, 24),
            AutoSize = true
        };

        var lineSep = new Panel
        {
            Location = new Point(10, 52),
            Size = new Size(420, 1),
            BackColor = Color.FromArgb(178, 212, 235)
        };

        var lblCompanyHeader = new Label
        {
            Text = "NAME OF COMPANY",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 90, 156),
            Location = new Point(10, 60),
            AutoSize = true
        };

        var lblLastEntryHeader = new Label
        {
            Text = "DATE OF LAST ENTRY",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 90, 156),
            Location = new Point(260, 60),
            AutoSize = true
        };

        lblCompanyNameRow = new Label
        {
            Text = "Test",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.Black,
            Location = new Point(10, 84),
            AutoSize = true
        };

        var lblBackupNote = new Label
        {
            Text = "(Schedule Backup)",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(40, 40, 40),
            Location = new Point(140, 84),
            AutoSize = true
        };

        lblLastEntryDate = new Label
        {
            Text = DateTime.Today.ToString("d-MMM-yy"),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.Black,
            Location = new Point(260, 84),
            AutoSize = true
        };

        pnlLeftInfo.Controls.Add(lblCurrentPeriodTag);
        pnlLeftInfo.Controls.Add(lblCurrentPeriod);
        pnlLeftInfo.Controls.Add(lblCurrentDateTag);
        pnlLeftInfo.Controls.Add(lblCurrentDate);
        pnlLeftInfo.Controls.Add(lineSep);
        pnlLeftInfo.Controls.Add(lblCompanyHeader);
        pnlLeftInfo.Controls.Add(lblLastEntryHeader);
        pnlLeftInfo.Controls.Add(lblCompanyNameRow);
        pnlLeftInfo.Controls.Add(lblBackupNote);
        pnlLeftInfo.Controls.Add(lblLastEntryDate);

        // --- Center Panel: Floating "Gateway of Tally" Card ---
        var pnlCenterCardWrapper = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        gatewayPanel = new Panel
        {
            Size = new Size(330, 470),
            Location = new Point(15, 20),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(220, 238, 250) // #DCEEFA
        };

        var headerGateway = new Panel
        {
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = Color.FromArgb(0, 75, 135) // #004B87
        };

        var lblGatewayTitle = new Label
        {
            Text = "Gateway of Tally",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill
        };
        headerGateway.Controls.Add(lblGatewayTitle);
        gatewayPanel.Controls.Add(headerGateway);

        lstGatewayMenu = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.25F),
            ItemHeight = 21,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(220, 238, 250),
            DrawMode = DrawMode.OwnerDrawFixed,
            IntegralHeight = false
        };

        lstGatewayMenu.Items.AddRange(new object[] {
            "-- MASTERS --",
            "Create",
            "Alter",
            "Chart of Accounts",
            "",
            "-- TRANSACTIONS --",
            "Vouchers",
            "Day Book",
            "",
            "-- UTILITIES --",
            "Banking",
            "Bank Reconciliation (BRS)",
            "",
            "-- REPORTS --",
            "Balance Sheet",
            "Profit & Loss A/c",
            "Ratio Analysis",
            "Display More Reports",
            "Dashboard",
            "",
            "Quit"
        });

        lstGatewayMenu.DrawItem += (s, e) =>
        {
            if (e.Index < 0 || e.Index >= lstGatewayMenu.Items.Count) return;

            string item = lstGatewayMenu.Items[e.Index].ToString() ?? string.Empty;
            bool isHeader = item.StartsWith("-- ") && item.EndsWith(" --");
            bool isEmpty = string.IsNullOrWhiteSpace(item);
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected && !isHeader && !isEmpty;

            using var bgBrush = new SolidBrush(isSelected ? TallyPrimeTheme.FlyoutSelectedBg : Color.FromArgb(220, 238, 250));
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            if (isEmpty) return;

            if (isHeader)
            {
                string headerText = item.Replace("-- ", "").Replace(" --", "");
                using var headerBrush = new SolidBrush(Color.FromArgb(0, 90, 156));
                e.Graphics.DrawString(headerText, new Font("Segoe UI", 8F, FontStyle.Bold), headerBrush, e.Bounds.Left + 25, e.Bounds.Top + 3);
            }
            else
            {
                using var textBrush = new SolidBrush(isSelected ? Color.Black : Color.FromArgb(0, 40, 80));
                Font font = isSelected ? new Font("Segoe UI", 9.25F, FontStyle.Bold) : new Font("Segoe UI", 9.25F);
                e.Graphics.DrawString(item, font, textBrush, e.Bounds.Left + 35, e.Bounds.Top + 2);
            }

            e.DrawFocusRectangle();
        };

        lstGatewayMenu.SelectedIndex = 1; // Default to Create

        lstGatewayMenu.DoubleClick += (s, e) =>
        {
            var item = lstGatewayMenu.SelectedItem?.ToString() ?? string.Empty;
            if (!item.StartsWith("--") && !string.IsNullOrWhiteSpace(item))
            {
                _navigationService.HandleGatewaySelection(item, this);
            }
        };

        lstGatewayMenu.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                var item = lstGatewayMenu.SelectedItem?.ToString() ?? string.Empty;
                if (!item.StartsWith("--") && !string.IsNullOrWhiteSpace(item))
                {
                    _navigationService.HandleGatewaySelection(item, this);
                    e.Handled = true;
                }
            }
        };

        gatewayPanel.Controls.Add(lstGatewayMenu);
        headerGateway.BringToFront();
        pnlCenterCardWrapper.Controls.Add(gatewayPanel);

        splitLayout.Controls.Add(pnlLeftInfo, 0, 0);
        splitLayout.Controls.Add(pnlCenterCardWrapper, 1, 0);

        centerContainer.Controls.Add(splitLayout);
        Controls.Add(centerContainer);
    }

    private void UpdateCompanyContextUI()
    {
        if (_companyContext.IsCompanyOpen && _companyContext.CurrentCompany != null)
        {
            var company = _companyContext.CurrentCompany;
            var fy = _companyContext.CurrentFinancialYear;

            if (lblCompanyNameRow != null)
            {
                lblCompanyNameRow.Text = company.CompanyName;
            }
            if (lblCurrentPeriod != null && fy != null)
            {
                lblCurrentPeriod.Text = $"{fy.StartDate:d-MMM-yy} to {fy.EndDate:d-MMM-yy}";
            }
            if (lblCurrentDate != null)
            {
                lblCurrentDate.Text = DateTime.Today.ToString("dddd, d-MMM-yyyy");
            }
            if (lblLastEntryDate != null)
            {
                lblLastEntryDate.Text = DateTime.Today.ToString("d-MMM-yy");
            }

            lblStatusCompany.Text = $"Company: {company.CompanyName}";
            lblStatusFY.Text = fy != null ? $" | FY: {fy.YearName}" : " | FY: Not set";
        }
        else
        {
            if (lblCompanyNameRow != null)
            {
                lblCompanyNameRow.Text = "[No Company Open]";
            }
            lblStatusCompany.Text = "Company: [None Selected]";
            lblStatusFY.Text = " | FY: Not Selected";
        }
    }

    private void UpdateUserContextUI()
    {
        if (lblStatusUser != null)
        {
            lblStatusUser.Text = $" | User: {_userContext.Username} ({_userContext.RoleName})";
        }
    }

    private async Task ApplyCurrentSettingsThemeAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            ThemeManager.SetTheme(settings.Theme, settings.GridDensity);
            ThemeManager.ApplyTheme(this);
        }
        catch
        {
            // Non-blocking graceful fallback
        }
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.Alt && e.KeyCode == Keys.G) || (e.Control && e.KeyCode == Keys.K) || (e.Control && e.KeyCode == Keys.F))
        {
            _navigationService.OpenGlobalSearch(this);
            e.Handled = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.F9)
        {
            _navigationService.OpenDebitNote(this);
            e.Handled = true;
            return;
        }
        if (e.Control && e.KeyCode == Keys.F8)
        {
            _navigationService.OpenCreditNote(this);
            e.Handled = true;
            return;
        }

        switch (e.KeyCode)
        {
            case Keys.Escape:
                var confirm = MessageBox.Show(this, "Do you want to exit MoneyFlow?", "Quit", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes) Application.Exit();
                break;
            case Keys.F2:
                _navigationService.OpenFinancialYearList(this);
                break;
            case Keys.F3:
                _navigationService.OpenCompanyList(this);
                break;
            case Keys.F4:
                _navigationService.OpenContraVoucher(this);
                break;
            case Keys.F5:
                _navigationService.OpenPaymentVoucher(this);
                break;
            case Keys.F6:
                _navigationService.OpenReceiptVoucher(this);
                break;
            case Keys.F7:
                _navigationService.OpenJournalVoucher(this);
                break;
            case Keys.F8:
                _navigationService.OpenSalesVoucher(this);
                break;
            case Keys.F9:
                _navigationService.OpenPurchaseVoucher(this);
                break;
            case Keys.F10:
                _navigationService.OpenBackupRestore(this);
                break;
            case Keys.F11:
                _navigationService.OpenSettings(this, () => _ = ApplyCurrentSettingsThemeAsync());
                break;
        }
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);

        if (_companyContext.CurrentCompany != null && _companyContext.CurrentCompany.AutoBackupOnExit && _backupRestoreService != null)
        {
            try
            {
                var comp = _companyContext.CurrentCompany;
                string baseDir = !string.IsNullOrWhiteSpace(comp.DataDirectory) ? comp.DataDirectory : @"C:\MoneyFlow\Data";
                string companyFolder = Path.Combine(baseDir, comp.CompanyNumber ?? comp.CompanyId.ToString("D5"));
                string backupDir = Path.Combine(companyFolder, "Backups");
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                await _backupRestoreService.CreateBackupAsync(new BackupCreateOptionsDto
                {
                    CompanyId = comp.CompanyId,
                    TargetDirectory = backupDir,
                    BackupType = BackupType.CompanyArchive,
                    Comment = "Auto-Backup on Application Exit"
                });
            }
            catch
            {
                // Non-blocking graceful exit
            }
        }
    }
}
