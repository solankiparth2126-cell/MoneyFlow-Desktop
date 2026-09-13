using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Navigation;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Executive Ledger Desktop — Gateway of Accounting.
/// Transformed to match MONEYFLOW DESKTOP ERP [WPF .NET 8 Edition] layout:
/// Top 32px Title Bar, 26px Menu, 40px Action Toolbar, Full-Width Company Banner,
/// 70/30 Split (6 Structured Gateway Cards + 3 Quick Widgets),
/// Horizontal Colored Operations Rail, and Status Bar.
/// </summary>
public class MainForm : Form
{
    // ═══════════════════════════════════════════════════════════════
    //  WIN32 INTEROP for Windows 11 Snap Layouts
    // ═══════════════════════════════════════════════════════════════
    private const int WM_NCHITTEST = 0x0084;
    private const int HTCAPTION = 2;
    private const int HTMAXBUTTON = 9;
    private const int HTMINBUTTON = 8;
    private const int HTCLOSE = 20;
    private const int HTLEFT = 10;
    private const int HTRIGHT = 11;
    private const int HTTOP = 12;
    private const int HTTOPLEFT = 13;
    private const int HTTOPRIGHT = 14;
    private const int HTBOTTOM = 15;
    private const int HTBOTTOMLEFT = 16;
    private const int HTBOTTOMRIGHT = 17;

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    // ═══════════════════════════════════════════════════════════════
    //  SERVICES
    // ═══════════════════════════════════════════════════════════════
    private readonly ICompanyContext _companyContext;
    private readonly IUserContext _userContext;
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;
    private readonly IBackupRestoreService? _backupRestoreService;

    // ═══════════════════════════════════════════════════════════════
    //  UI CONTROLS
    // ═══════════════════════════════════════════════════════════════

    // Title bar
    private Guna2Panel titleBar = null!;
    private Label lblTitleText = null!;
    private Label lblTitleContext = null!;
    private Label lblEngineOnline = null!;
    private Guna2Button btnMinimize = null!;
    private Guna2Button btnMaxRestore = null!;
    private Guna2Button btnClose = null!;

    // Menu
    private MenuStrip menuStrip = null!;

    // Toolbar
    private Guna2Panel toolbarPanel = null!;
    private Label lblSessionTime = null!;
    private System.Windows.Forms.Timer sessionTimer = null!;
    private DateTime sessionStartTime;

    // Company Banner Card (Full Width)
    private Guna2Panel pnlCompanyBanner = null!;
    private Label lblBannerCompName = null!;
    private Label lblBannerCompSubtitle = null!;
    private Label lblBannerBooksBeginning = null!;
    private Label lblBannerFY = null!;
    private Label lblBannerDate = null!;

    // Bottom Colored Operations Rail
    private Guna2Panel operationsRail = null!;

    // Status Bar
    private Guna2Panel statusBar = null!;
    private Label lblStatusCompany = null!;
    private Label lblStatusFY = null!;
    private Label lblStatusUser = null!;
    private Label lblStatusVoucherDate = null!;
    private Label lblStatusTrialBal = null!;
    private Label lblStatusLockKeys = null!;

    // Borderless form component
    private Guna2BorderlessForm borderlessForm = null!;

    // Resize border
    private const int ResizeBorder = 6;

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

        sessionStartTime = DateTime.Now;

        InitializeComponent();

        _companyContext.OnCompanyChanged += UpdateCompanyContextUI;
        _userContext.OnUserChanged += UpdateUserContextUI;
        UpdateCompanyContextUI();
        UpdateUserContextUI();
        _ = ApplyCurrentSettingsThemeAsync();

        // Setup session timer
        sessionTimer = new System.Windows.Forms.Timer { Interval = 60000 };
        sessionTimer.Tick += (s, e) => UpdateSessionTime();
        sessionTimer.Start();
    }

    private void InitializeComponent()
    {
        // Form base setup
        Text = "MONEYFLOW DESKTOP ERP [WPF .NET 8 Edition]";
        Size = new Size(1366, 820);
        MinimumSize = new Size(1100, 680);
        StartPosition = FormStartPosition.CenterScreen;
        WindowState = FormWindowState.Maximized;
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        BackColor = Color.FromArgb(241, 245, 249); // Soft light slate canvas
        Font = ExecLedgerTheme.UIRegular9;
        DoubleBuffered = true;

        // Guna2BorderlessForm for clean borderless management
        borderlessForm = new Guna2BorderlessForm();
        borderlessForm.ContainerControl = this;
        borderlessForm.AnimateWindow = false;
        borderlessForm.BorderRadius = 0;
        borderlessForm.ResizeForm = true;
        borderlessForm.DragForm = false; // We handle drag manually

        SuspendLayout();

        // 1. Custom Title Bar (32px, Dark Navy)
        CreateTitleBar();

        // 2. Menu Bar (26px, Clean White)
        CreateMenuBar();

        // 3. Action Toolbar (40px, Pill Buttons + Quick Search + Exit)
        CreateToolbar();

        // 4. Bottom Status Bar (24px) — Add first to dock at very bottom
        CreateStatusBar();

        // 5. Operations Rail (34px Horizontal Colored Buttons) — Above Status Bar
        CreateOperationsRail();

        // 6. Main Workspace Layout (Fill)
        CreateGatewayLayout();

        // 7. Keyboard Shortcuts
        KeyDown += MainForm_KeyDown;

        // 8. Auto-Open Select Company On Startup
        Shown += (s, e) =>
        {
            _navigationService.OpenCompanyList(this);
        };

        ResumeLayout(true);
    }

    // ═══════════════════════════════════════════════════════════════
    //  1. TITLE BAR (32px Dark Navy)
    // ═══════════════════════════════════════════════════════════════

    private void CreateTitleBar()
    {
        titleBar = new Guna2Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            FillColor = Color.FromArgb(13, 30, 50), // #0D1E32 Dark Navy
            BorderRadius = 0,
            BorderThickness = 0
        };

        // Icon Badge "MF"
        var iconBadge = new Guna2Panel
        {
            Size = new Size(20, 20),
            Location = new Point(8, 6),
            FillColor = Color.FromArgb(30, 58, 138),
            BorderRadius = 3
        };
        var lblBadgeText = new Label
        {
            Text = "MF",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        iconBadge.Controls.Add(lblBadgeText);
        titleBar.Controls.Add(iconBadge);

        // App Title
        lblTitleText = new Label
        {
            Text = "MONEYFLOW DESKTOP ERP [WPF .NET 8 Edition]",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(34, 7),
            BackColor = Color.Transparent
        };
        titleBar.Controls.Add(lblTitleText);

        // Company context text (e.g. "  |  ABC TRADERS • FY 2026-27")
        lblTitleContext = new Label
        {
            Text = "",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            AutoSize = true,
            Location = new Point(325, 8),
            BackColor = Color.Transparent
        };
        titleBar.Controls.Add(lblTitleContext);

        // Engine Status on right
        lblEngineOnline = new Label
        {
            Text = "● WPF Runtime .NET 8   |   Engine: Online",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            AutoSize = true,
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        titleBar.Controls.Add(lblEngineOnline);

        // Window Control Buttons
        int btnW = 46;
        int btnH = 32;

        btnClose = new Guna2Button
        {
            Text = "✕",
            ForeColor = Color.White,
            FillColor = Color.Transparent,
            BorderThickness = 0,
            BorderRadius = 0,
            Size = new Size(btnW, btnH),
            Font = new Font("Segoe UI", 10F),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            HoverState = { FillColor = Color.FromArgb(220, 38, 38), ForeColor = Color.White },
            Cursor = Cursors.Hand
        };
        btnClose.Click += (s, e) => Application.Exit();

        btnMaxRestore = new Guna2Button
        {
            Text = "☐",
            ForeColor = Color.White,
            FillColor = Color.Transparent,
            BorderThickness = 0,
            BorderRadius = 0,
            Size = new Size(btnW, btnH),
            Font = new Font("Segoe UI", 10F),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            HoverState = { FillColor = Color.FromArgb(50, 255, 255, 255) },
            Cursor = Cursors.Hand
        };
        btnMaxRestore.Click += (s, e) =>
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
            btnMaxRestore.Text = WindowState == FormWindowState.Maximized ? "❐" : "☐";
        };

        btnMinimize = new Guna2Button
        {
            Text = "—",
            ForeColor = Color.White,
            FillColor = Color.Transparent,
            BorderThickness = 0,
            BorderRadius = 0,
            Size = new Size(btnW, btnH),
            Font = new Font("Segoe UI", 10F),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            HoverState = { FillColor = Color.FromArgb(50, 255, 255, 255) },
            Cursor = Cursors.Hand
        };
        btnMinimize.Click += (s, e) => WindowState = FormWindowState.Minimized;

        titleBar.Controls.Add(btnMinimize);
        titleBar.Controls.Add(btnMaxRestore);
        titleBar.Controls.Add(btnClose);

        // Position from right on resize
        titleBar.Resize += (s, e) =>
        {
            btnClose.Location = new Point(titleBar.Width - btnW, 0);
            btnMaxRestore.Location = new Point(titleBar.Width - btnW * 2, 0);
            btnMinimize.Location = new Point(titleBar.Width - btnW * 3, 0);
            lblEngineOnline.Location = new Point(titleBar.Width - btnW * 3 - lblEngineOnline.Width - 16, 9);
        };

        // Title bar drag handlers
        titleBar.MouseDown += TitleBar_MouseDown;
        lblTitleText.MouseDown += TitleBar_MouseDown;
        lblTitleContext.MouseDown += TitleBar_MouseDown;
        iconBadge.MouseDown += TitleBar_MouseDown;

        titleBar.DoubleClick += (s, e) => btnMaxRestore.PerformClick();
        lblTitleText.DoubleClick += (s, e) => btnMaxRestore.PerformClick();

        Controls.Add(titleBar);
    }

    private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, 0x0112, 0xF010 + 2, 0); // WM_SYSCOMMAND + SC_MOVE + HTCAPTION
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  2. MENU BAR (26px)
    // ═══════════════════════════════════════════════════════════════

    private void CreateMenuBar()
    {
        menuStrip = new MenuStrip
        {
            Dock = DockStyle.Top,
            Padding = new Padding(8, 0, 0, 0),
            BackColor = Color.White
        };
        ExecLedgerStyler.StyleMenuStrip(menuStrip);

        var menuFile = new ToolStripMenuItem("&File");
        menuFile.DropDownItems.Add("Close Company", null, (s, e) => _navigationService.CloseActiveCompany(this));
        menuFile.DropDownItems.Add(new ToolStripSeparator());
        menuFile.DropDownItems.Add("E&xit\tEsc", null, (s, e) => Application.Exit());

        var menuCompany = new ToolStripMenuItem("&Company");
        menuCompany.DropDownItems.Add("Select Company\tF3", null, (s, e) => _navigationService.OpenCompanyList(this));
        menuCompany.DropDownItems.Add("Create Company\tAlt+C", null, (s, e) => _navigationService.OpenCreateCompany(this));
        menuCompany.DropDownItems.Add("Alter Company\tAlt+A", null, (s, e) => _navigationService.OpenAlterCompany(this));
        menuCompany.DropDownItems.Add("Change Financial Year\tF2", null, (s, e) => _navigationService.OpenFinancialYearList(this));
        menuCompany.DropDownItems.Add("Close Active Company", null, (s, e) => _navigationService.CloseActiveCompany(this));
        menuCompany.DropDownItems.Add(new ToolStripSeparator());
        menuCompany.DropDownItems.Add("E&xit\tEsc", null, (s, e) => Application.Exit());

        var menuMasters = new ToolStripMenuItem("&Masters");
        menuMasters.DropDownItems.Add("&Groups (Chart of Accounts)", null, (s, e) => _navigationService.OpenGroupList(this));
        menuMasters.DropDownItems.Add("&Ledgers", null, (s, e) => _navigationService.OpenLedgerList(this));
        menuMasters.DropDownItems.Add("&Stock Items", null, (s, e) => _navigationService.OpenStockItemList(this));
        menuMasters.DropDownItems.Add("&Units of Measure", null, (s, e) => _navigationService.OpenUnitList(this));

        var menuTransactions = new ToolStripMenuItem("&Transactions");
        menuTransactions.DropDownItems.Add("&Contra\tF4", null, (s, e) => _navigationService.OpenContraVoucher(this));
        menuTransactions.DropDownItems.Add("&Payment\tF5", null, (s, e) => _navigationService.OpenPaymentVoucher(this));
        menuTransactions.DropDownItems.Add("&Receipt\tF6", null, (s, e) => _navigationService.OpenReceiptVoucher(this));
        menuTransactions.DropDownItems.Add("&Journal\tF7", null, (s, e) => _navigationService.OpenJournalVoucher(this));
        menuTransactions.DropDownItems.Add("&Sales Voucher\tF8", null, (s, e) => _navigationService.OpenSalesVoucher(this));
        menuTransactions.DropDownItems.Add("&Purchase Voucher\tF9", null, (s, e) => _navigationService.OpenPurchaseVoucher(this));
        menuTransactions.DropDownItems.Add(new ToolStripSeparator());
        menuTransactions.DropDownItems.Add("&Debit Note\tCtrl+F9", null, (s, e) => _navigationService.OpenDebitNote(this));
        menuTransactions.DropDownItems.Add("&Credit Note\tCtrl+F8", null, (s, e) => _navigationService.OpenCreditNote(this));

        var menuReports = new ToolStripMenuItem("&Reports");
        menuReports.DropDownItems.Add("&Day Book", null, (s, e) => _navigationService.OpenDayBook(this));
        menuReports.DropDownItems.Add("&Ledger Statement", null, (s, e) => _navigationService.OpenLedgerStatement(this));
        menuReports.DropDownItems.Add("&Trial Balance", null, (s, e) => _navigationService.OpenTrialBalance(this));
        menuReports.DropDownItems.Add(new ToolStripSeparator());
        menuReports.DropDownItems.Add("&Profit && Loss", null, (s, e) => _navigationService.OpenProfitLoss(this));
        menuReports.DropDownItems.Add("&Balance Sheet", null, (s, e) => _navigationService.OpenBalanceSheet(this));
        menuReports.DropDownItems.Add(new ToolStripSeparator());
        menuReports.DropDownItems.Add("&Cash / Bank Book", null, (s, e) => _navigationService.OpenCashBankBook(this));
        menuReports.DropDownItems.Add("&Outstanding Analysis", null, (s, e) => _navigationService.OpenOutstandingReport(this));
        menuReports.DropDownItems.Add("&Stock Summary", null, (s, e) => _navigationService.OpenStockSummary(this));
        menuReports.DropDownItems.Add(new ToolStripSeparator());
        menuReports.DropDownItems.Add("&Dashboard", null, (s, e) => _navigationService.OpenDashboard(this));

        var menuUtilities = new ToolStripMenuItem("&Utilities");
        menuUtilities.DropDownItems.Add("&Global Search\tCtrl+F", null, (s, e) => _navigationService.OpenGlobalSearch(this));
        menuUtilities.DropDownItems.Add("&Import / Export Data...", null, (s, e) => _navigationService.OpenImportExport(this));
        menuUtilities.DropDownItems.Add(new ToolStripSeparator());
        menuUtilities.DropDownItems.Add("&Backup && Restore\tF10", null, (s, e) => _navigationService.OpenBackupRestore(this));
        menuUtilities.DropDownItems.Add(new ToolStripSeparator());
        menuUtilities.DropDownItems.Add("&Settings\tF11", null, (s, e) => _navigationService.OpenSettings(this, () => _ = ApplyCurrentSettingsThemeAsync()));
        menuUtilities.DropDownItems.Add(new ToolStripSeparator());
        menuUtilities.DropDownItems.Add("Connection Diagnostics", null, (s, e) => _navigationService.OpenDatabaseDiagnostics(this));

        var menuHelp = new ToolStripMenuItem("&Help");
        menuHelp.DropDownItems.Add("Keyboard Accelerators Reference\tF1", null, (s, e) => _navigationService.OpenGlobalSearch(this));
        menuHelp.DropDownItems.Add("About MoneyFlow ERP", null, (s, e) => MessageBox.Show(this, "MoneyFlow Desktop ERP [WPF .NET 8 Edition]\nVersion 2.0.0", "About MoneyFlow", MessageBoxButtons.OK, MessageBoxIcon.Information));

        menuStrip.Items.AddRange(new ToolStripItem[] { menuFile, menuCompany, menuMasters, menuTransactions, menuReports, menuUtilities, menuHelp });
        MainMenuStrip = menuStrip;
        Controls.Add(menuStrip);
    }

    // ═══════════════════════════════════════════════════════════════
    //  3. ACTION TOOLBAR (40px with Pill Buttons, Search, Exit)
    // ═══════════════════════════════════════════════════════════════

    private void CreateToolbar()
    {
        toolbarPanel = new Guna2Panel
        {
            Dock = DockStyle.Top,
            Height = 42,
            FillColor = Color.FromArgb(248, 250, 252),
            BorderColor = Color.FromArgb(226, 232, 240),
            BorderThickness = 1,
            BorderRadius = 0,
            Padding = new Padding(8, 5, 8, 5)
        };

        int x = 12;

        // Left Pill Buttons
        var leftPills = new (string Text, Action Click)[]
        {
            ("Change Co.  F3", () => _navigationService.OpenCompanyList(this)),
            ("Date  F2", () => _navigationService.OpenFinancialYearList(this)),
            ("Day Book", () => _navigationService.OpenDayBook(this)),
            ("Trial Balance", () => _navigationService.OpenTrialBalance(this)),
            ("P & L", () => _navigationService.OpenProfitLoss(this)),
            ("Balance Sheet", () => _navigationService.OpenBalanceSheet(this)),
        };

        foreach (var (text, click) in leftPills)
        {
            var btn = CreateActionPill(text);
            btn.Location = new Point(x, 6);
            btn.Click += (s, e) => click();
            toolbarPanel.Controls.Add(btn);
            x += btn.Width + 6;
        }

        // Right side controls container
        var pnlRightControls = new Panel
        {
            Dock = DockStyle.Right,
            Width = 460,
            BackColor = Color.Transparent
        };

        // Jump to ledger / voucher Search Box
        var pnlSearch = new Guna2Panel
        {
            Size = new Size(240, 30),
            Location = new Point(10, 6),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(203, 213, 225),
            BorderThickness = 1,
            BorderRadius = 15,
            Cursor = Cursors.Hand
        };
        var lblSearchIcon = new Label
        {
            Text = "🔍 Jump to ledger / voucher (Ctrl+F)",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8.25F),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        pnlSearch.Controls.Add(lblSearchIcon);
        Action openSearch = () => _navigationService.OpenGlobalSearch(this);
        pnlSearch.Click += (s, e) => openSearch();
        lblSearchIcon.Click += (s, e) => openSearch();
        pnlRightControls.Controls.Add(pnlSearch);

        // Session Time Label
        lblSessionTime = new Label
        {
            Text = "Session Time: 00:00 hrs",
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8F),
            Location = new Point(260, 13),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlRightControls.Controls.Add(lblSessionTime);

        // Red Exit Pill
        var btnExit = new Guna2Button
        {
            Text = "Exit  Esc",
            Size = new Size(72, 28),
            Location = new Point(380, 7),
            FillColor = Color.FromArgb(254, 242, 242),
            BorderColor = Color.FromArgb(239, 68, 68),
            BorderThickness = 1,
            BorderRadius = 14,
            ForeColor = Color.FromArgb(220, 38, 38),
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnExit.HoverState.FillColor = Color.FromArgb(254, 226, 226);
        btnExit.Click += (s, e) => Application.Exit();
        pnlRightControls.Controls.Add(btnExit);

        toolbarPanel.Controls.Add(pnlRightControls);
        Controls.Add(toolbarPanel);
    }

    private Guna2Button CreateActionPill(string text)
    {
        int width = TextRenderer.MeasureText(text, ExecLedgerTheme.UIRegular8).Width + 24;
        return new Guna2Button
        {
            Text = text,
            Size = new Size(width, 28),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(226, 232, 240),
            BorderThickness = 1,
            BorderRadius = 14,
            ForeColor = Color.FromArgb(51, 65, 85),
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            Cursor = Cursors.Hand,
            HoverState =
            {
                FillColor = Color.FromArgb(241, 245, 249),
                BorderColor = Color.FromArgb(148, 163, 184)
            }
        };
    }

    private void UpdateSessionTime()
    {
        var elapsed = DateTime.Now - sessionStartTime;
        if (lblSessionTime != null)
            lblSessionTime.Text = $"Session Time: {elapsed.Hours:D2}:{elapsed.Minutes:D2} hrs";
    }

    // ═══════════════════════════════════════════════════════════════
    //  4. BOTTOM STATUS BAR (24px)
    // ═══════════════════════════════════════════════════════════════

    private void CreateStatusBar()
    {
        statusBar = new Guna2Panel
        {
            Dock = DockStyle.Bottom,
            Height = 24,
            FillColor = Color.FromArgb(241, 245, 249),
            BorderColor = Color.FromArgb(226, 232, 240),
            BorderThickness = 1,
            BorderRadius = 0,
            Padding = new Padding(12, 0, 12, 0)
        };

        // Left items
        int sx = 12;
        lblStatusCompany = CreateStatusItem("● Company: [None]", ref sx);
        CreateStatusSeparator(ref sx);
        lblStatusFY = CreateStatusItem("FY: 2026-27", ref sx);
        CreateStatusSeparator(ref sx);
        lblStatusUser = CreateStatusItem("Operator: Administrator", ref sx);
        CreateStatusSeparator(ref sx);
        lblStatusVoucherDate = CreateStatusItem($"Voucher Date: {DateTime.Today:dd-MMM-yyyy}", ref sx);
        CreateStatusSeparator(ref sx);
        lblStatusTrialBal = CreateStatusItem("Trial Bal: Balanced (₹ 0.00)", ref sx);
        lblStatusTrialBal.ForeColor = Color.FromArgb(22, 101, 52);

        // Right lock keys indicator
        lblStatusLockKeys = new Label
        {
            Text = "CAPS: OFF   NUM: ON   INS: ON",
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.Transparent
        };
        statusBar.Controls.Add(lblStatusLockKeys);

        statusBar.Resize += (s, e) =>
        {
            lblStatusLockKeys.Location = new Point(statusBar.Width - lblStatusLockKeys.Width - 14, 5);
        };

        Controls.Add(statusBar);
    }

    private Label CreateStatusItem(string text, ref int x)
    {
        var lbl = new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(51, 65, 85),
            AutoSize = true,
            Location = new Point(x, 4),
            BackColor = Color.Transparent
        };
        statusBar.Controls.Add(lbl);
        x += TextRenderer.MeasureText(text, lbl.Font).Width + 8;
        return lbl;
    }

    private void CreateStatusSeparator(ref int x)
    {
        var sep = new Panel
        {
            Location = new Point(x, 5),
            Size = new Size(1, 14),
            BackColor = Color.FromArgb(203, 213, 225)
        };
        statusBar.Controls.Add(sep);
        x += 9;
    }

    // ═══════════════════════════════════════════════════════════════
    //  5. OPERATIONS RAIL (34px Colored Horizontal Buttons)
    // ═══════════════════════════════════════════════════════════════

    private void CreateOperationsRail()
    {
        operationsRail = new Guna2Panel
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            FillColor = Color.FromArgb(15, 23, 42), // #0F172A Dark Slate
            BorderRadius = 0,
            BorderThickness = 0,
            Padding = new Padding(6, 3, 6, 3)
        };

        int ox = 8;

        // Colored function keys
        var fnKeys = new (string Text, Color Bg, Action Action)[]
        {
            ("F1 Help", Color.FromArgb(217, 119, 6), () => _navigationService.OpenGlobalSearch(this)),
            ("F2 Date", Color.FromArgb(37, 99, 235), () => _navigationService.OpenFinancialYearList(this)),
            ("F3 Company", Color.FromArgb(37, 99, 235), () => _navigationService.OpenCompanyList(this)),
            ("F4 Contra", Color.FromArgb(5, 150, 105), () => _navigationService.OpenContraVoucher(this)),
            ("F5 Payment", Color.FromArgb(5, 150, 105), () => _navigationService.OpenPaymentVoucher(this)),
            ("F6 Receipt", Color.FromArgb(13, 148, 136), () => _navigationService.OpenReceiptVoucher(this)),
            ("F7 Journal", Color.FromArgb(13, 148, 136), () => _navigationService.OpenJournalVoucher(this)),
            ("F8 Sales", Color.FromArgb(37, 99, 235), () => _navigationService.OpenSalesVoucher(this)),
            ("F9 Purchase", Color.FromArgb(13, 148, 136), () => _navigationService.OpenPurchaseVoucher(this)),
            ("F10 Menu", Color.FromArgb(30, 58, 138), () => _navigationService.OpenBackupRestore(this)),
            ("F11 Features", Color.FromArgb(67, 56, 202), () => _navigationService.OpenSettings(this, () => _ = ApplyCurrentSettingsThemeAsync())),
            ("F12 Reports", Color.FromArgb(217, 119, 6), () => _navigationService.OpenTrialBalance(this)),
        };

        foreach (var (text, bg, action) in fnKeys)
        {
            var btn = CreateRailButton(text, bg);
            btn.Location = new Point(ox, 4);
            btn.Click += (s, e) => action();
            operationsRail.Controls.Add(btn);
            ox += btn.Width + 4;
        }

        // Right quick buttons container
        var pnlRightRail = new Panel
        {
            Dock = DockStyle.Right,
            Width = 260,
            BackColor = Color.Transparent
        };

        var btnCtrlSearch = CreateRailButton("Ctrl+F Search", Color.FromArgb(30, 41, 59));
        btnCtrlSearch.Location = new Point(0, 4);
        btnCtrlSearch.Click += (s, e) => _navigationService.OpenGlobalSearch(this);
        pnlRightRail.Controls.Add(btnCtrlSearch);

        var btnCtrlSave = CreateRailButton("Ctrl+S Save", Color.FromArgb(22, 163, 74));
        btnCtrlSave.Location = new Point(btnCtrlSearch.Width + 4, 4);
        btnCtrlSave.Click += (s, e) => { /* Quick Save trigger */ };
        pnlRightRail.Controls.Add(btnCtrlSave);

        var btnEscExit = CreateRailButton("Esc Exit", Color.FromArgb(220, 38, 38));
        btnEscExit.Location = new Point(btnCtrlSearch.Width + btnCtrlSave.Width + 8, 4);
        btnEscExit.Click += (s, e) => Application.Exit();
        pnlRightRail.Controls.Add(btnEscExit);

        operationsRail.Controls.Add(pnlRightRail);
        Controls.Add(operationsRail);
    }

    private Guna2Button CreateRailButton(string text, Color bg)
    {
        int width = TextRenderer.MeasureText(text, ExecLedgerTheme.UIBold8).Width + 14;
        return new Guna2Button
        {
            Text = text,
            Size = new Size(width, 24),
            FillColor = bg,
            BorderThickness = 0,
            BorderRadius = 3,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  6. MAIN WORKSPACE LAYOUT (Full-Width Banner + 70/30 Split)
    // ═══════════════════════════════════════════════════════════════

    private void CreateGatewayLayout()
    {
        var mainContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(12, 10, 12, 10),
            AutoScroll = true
        };

        // ── A. FULL-WIDTH COMPANY BANNER CARD ──
        pnlCompanyBanner = new Guna2Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(226, 232, 240),
            BorderThickness = 1,
            BorderRadius = 6,
            Margin = new Padding(0, 0, 0, 10),
            Padding = new Padding(12, 8, 16, 8)
        };

        // Left Icon Badge "MF"
        var badgePanel = new Guna2Panel
        {
            Size = new Size(38, 38),
            Location = new Point(12, 17),
            FillColor = Color.FromArgb(15, 23, 42),
            BorderRadius = 6
        };
        var lblBadge = new Label
        {
            Text = "MF",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        badgePanel.Controls.Add(lblBadge);
        pnlCompanyBanner.Controls.Add(badgePanel);

        // Company Name + Active Pill + Books Beginning
        lblBannerCompName = new Label
        {
            Text = "ABC TRADERS",
            Font = new Font("Segoe UI", 12.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            AutoSize = true,
            Location = new Point(60, 12),
            BackColor = Color.Transparent
        };
        pnlCompanyBanner.Controls.Add(lblBannerCompName);

        var pnlActivePill = new Guna2Panel
        {
            Size = new Size(95, 20),
            Location = new Point(190, 15),
            FillColor = Color.FromArgb(220, 252, 231),
            BorderRadius = 4
        };
        var lblActivePillText = new Label
        {
            Text = "Active Company",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 101, 52),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        pnlActivePill.Controls.Add(lblActivePillText);
        pnlCompanyBanner.Controls.Add(pnlActivePill);

        lblBannerBooksBeginning = new Label
        {
            Text = "|   Books Beginning: 01-Apr-2026",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            Location = new Point(292, 17),
            BackColor = Color.Transparent
        };
        pnlCompanyBanner.Controls.Add(lblBannerBooksBeginning);

        lblBannerCompSubtitle = new Label
        {
            Text = "Commercial Accounts • Wholesale & Retail Trading • Base Currency: INR (₹)",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            Location = new Point(60, 40),
            BackColor = Color.Transparent
        };
        pnlCompanyBanner.Controls.Add(lblBannerCompSubtitle);

        // Right Side FY and Date Info
        var pnlBannerRight = new Panel
        {
            Dock = DockStyle.Right,
            Width = 360,
            BackColor = Color.Transparent
        };

        var lblFYTag = new Label
        {
            Text = "FINANCIAL YEAR",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(20, 14),
            AutoSize = true
        };
        lblBannerFY = new Label
        {
            Text = "2026 - 2027",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(20, 32),
            AutoSize = true
        };
        pnlBannerRight.Controls.Add(lblFYTag);
        pnlBannerRight.Controls.Add(lblBannerFY);

        var lblDateTag = new Label
        {
            Text = "CURRENT VOUCHER DATE",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(170, 14),
            AutoSize = true
        };
        lblBannerDate = new Label
        {
            Text = DateTime.Today.ToString("dd-MMM-yyyy (dddd)"),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(170, 32),
            AutoSize = true
        };
        pnlBannerRight.Controls.Add(lblDateTag);
        pnlBannerRight.Controls.Add(lblBannerDate);

        pnlCompanyBanner.Controls.Add(pnlBannerRight);
        mainContainer.Controls.Add(pnlCompanyBanner);

        // Position Active pill dynamically after company name
        lblBannerCompName.SizeChanged += (s, e) =>
        {
            pnlActivePill.Location = new Point(lblBannerCompName.Right + 8, 15);
            lblBannerBooksBeginning.Location = new Point(pnlActivePill.Right + 8, 17);
        };

        // Spacer below banner
        var spacer = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = Color.Transparent };
        mainContainer.Controls.Add(spacer);

        // ── B. 70/30 SPLIT WORKSPACE ──
        var splitTable = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent
        };
        splitTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 69F)); // Left 69%
        splitTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 31F)); // Right 31%
        splitTable.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        // ───────────────────────────────────────────────────────────
        //  LEFT SECTION: GATEWAY OF ACCOUNTING (6 Structured Cards)
        // ───────────────────────────────────────────────────────────
        var pnlLeftGateway = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 8, 0)
        };

        // Gateway Header
        var pnlGatewayHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.Transparent
        };
        var lblGwTitle = new Label
        {
            Text = "GATEWAY OF ACCOUNTING",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(0, 2),
            AutoSize = true
        };
        var lblGwSub = new Label
        {
            Text = "Press highlighted underlined key or click category to open master modules",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(0, 22),
            AutoSize = true
        };
        pnlGatewayHeader.Controls.Add(lblGwTitle);
        pnlGatewayHeader.Controls.Add(lblGwSub);

        // Navigation Tip Pill
        var pnlNavTip = new Guna2Panel
        {
            Size = new Size(330, 26),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FillColor = Color.FromArgb(239, 246, 255),
            BorderColor = Color.FromArgb(191, 219, 254),
            BorderThickness = 1,
            BorderRadius = 4
        };
        var lblNavTip = new Label
        {
            Text = "Navigation Tip: Use  [Arrow Keys] + [Enter]  or single hotkeys.",
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = Color.FromArgb(29, 78, 216),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        pnlNavTip.Controls.Add(lblNavTip);
        pnlGatewayHeader.Controls.Add(pnlNavTip);
        pnlGatewayHeader.Resize += (s, e) =>
        {
            pnlNavTip.Location = new Point(pnlGatewayHeader.Width - pnlNavTip.Width, 6);
        };
        pnlLeftGateway.Controls.Add(pnlGatewayHeader);

        // 3x2 Grid of 6 Cards
        var cardsGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 2,
            BackColor = Color.Transparent
        };
        cardsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        cardsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        cardsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        cardsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        cardsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        // ── Card 1: MASTERS [M] ──
        var cardMasters = CreateStructuredCard("MASTERS", "M", Color.FromArgb(37, 99, 235));
        AddCardActionRow(cardMasters, "Groups", "Hierarchy", () => _navigationService.OpenGroupList(this));
        AddCardActionRow(cardMasters, "Ledgers Master", "Primary", () => _navigationService.OpenLedgerList(this), isHighlighted: true);
        AddCardActionRow(cardMasters, "Stock Items", "Inventory", () => _navigationService.OpenStockItemList(this));
        AddCardActionRow(cardMasters, "Units of Measure", "Qty", () => _navigationService.OpenUnitList(this));
        AddCardActionRow(cardMasters, "Voucher Types", "Config", () => _navigationService.OpenSettings(this, () => _ = ApplyCurrentSettingsThemeAsync()));
        cardsGrid.Controls.Add(cardMasters, 0, 0);

        // ── Card 2: TRANSACTIONS [T] ──
        var cardTrans = CreateStructuredCard("TRANSACTIONS", "T", Color.FromArgb(13, 148, 136));
        AddCardActionRow(cardTrans, "Payment", "F5", () => _navigationService.OpenPaymentVoucher(this));
        AddCardActionRow(cardTrans, "Receipt", "F6", () => _navigationService.OpenReceiptVoucher(this));
        AddCardActionRow(cardTrans, "Contra", "F4", () => _navigationService.OpenContraVoucher(this));
        AddCardActionRow(cardTrans, "Journal", "F7", () => _navigationService.OpenJournalVoucher(this));
        AddCardActionRow(cardTrans, "Sales Voucher", "F8", () => _navigationService.OpenSalesVoucher(this));
        AddCardActionRow(cardTrans, "Purchase Voucher", "F9", () => _navigationService.OpenPurchaseVoucher(this));
        AddCardActionRow(cardTrans, "Debit / Credit Note", "Ctrl+F9", () => _navigationService.OpenDebitNote(this));
        cardsGrid.Controls.Add(cardTrans, 1, 0);

        // ── Card 3: REPORTS [R] ──
        var cardReports = CreateStructuredCard("REPORTS", "R", Color.FromArgb(217, 119, 6));
        AddCardActionRow(cardReports, "Day Book", "Daily Ledger", () => _navigationService.OpenDayBook(this));
        AddCardActionRow(cardReports, "Ledger Accounts", "Statement", () => _navigationService.OpenLedgerStatement(this));
        AddCardActionRow(cardReports, "Trial Balance", "Auditing", () => _navigationService.OpenTrialBalance(this), isGoldBadge: true);
        AddCardActionRow(cardReports, "Profit & Loss", "P&L Stmt", () => _navigationService.OpenProfitLoss(this));
        AddCardActionRow(cardReports, "Balance Sheet", "Financials", () => _navigationService.OpenBalanceSheet(this));
        AddCardActionRow(cardReports, "Cash & Bank Book", "Funds Flow", () => _navigationService.OpenCashBankBook(this));
        AddCardActionRow(cardReports, "Outstandings (AR/AP)", "Aging", () => _navigationService.OpenOutstandingReport(this));
        cardsGrid.Controls.Add(cardReports, 2, 0);

        // ── Card 4: COMPANY OPERATIONS [C] ──
        var cardCompany = CreateStructuredCard("COMPANY OPERATIONS", "C", Color.FromArgb(124, 58, 237));
        AddCardActionRow(cardCompany, "Select Company", "F1", () => _navigationService.OpenCompanyList(this));
        AddCardActionRow(cardCompany, "Create New Company", "Setup", () => _navigationService.OpenCreateCompany(this));
        AddCardActionRow(cardCompany, "Alter Company Info", "Edit", () => _navigationService.OpenAlterCompany(this));
        AddCardActionRow(cardCompany, "Financial Year Change", "FY 26-27", () => _navigationService.OpenFinancialYearList(this));
        cardsGrid.Controls.Add(cardCompany, 0, 1);

        // ── Card 5: GLOBAL SEARCH [Ctrl+F] ──
        var cardSearch = CreateStructuredCard("GLOBAL SEARCH", "Ctrl+f", Color.FromArgb(6, 182, 212));
        var lblSearchDesc = new Label
        {
            Text = "Fast indexing across all ledgers, voucher numbers, amounts, and dates.",
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = Color.FromArgb(100, 116, 139),
            Dock = DockStyle.Top,
            Height = 34,
            Padding = new Padding(12, 4, 12, 0),
            BackColor = Color.Transparent
        };
        cardSearch.Controls.Add(lblSearchDesc);
        lblSearchDesc.BringToFront();

        AddCardActionRow(cardSearch, "Quick Query Filter", "Instant", () => _navigationService.OpenGlobalSearch(this));

        // Search Filter Chips
        var pnlChips = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            Padding = new Padding(12, 2, 12, 2),
            BackColor = Color.Transparent
        };
        var chip1 = CreateFilterChip("Ledger", () => _navigationService.OpenGlobalSearch(this));
        chip1.Location = new Point(12, 2);
        var chip2 = CreateFilterChip("Voucher", () => _navigationService.OpenGlobalSearch(this));
        chip2.Location = new Point(chip1.Right + 6, 2);
        var chip3 = CreateFilterChip("Amount", () => _navigationService.OpenGlobalSearch(this));
        chip3.Location = new Point(chip2.Right + 6, 2);
        pnlChips.Controls.AddRange(new Control[] { chip1, chip2, chip3 });
        cardSearch.Controls.Add(pnlChips);
        pnlChips.BringToFront();

        cardsGrid.Controls.Add(cardSearch, 1, 1);

        // ── Card 6: UTILITIES & SYSTEM [U] ──
        var cardUtils = CreateStructuredCard("UTILITIES & SYSTEM", "U", Color.FromArgb(71, 85, 105));
        AddCardActionRow(cardUtils, "Backup Company Data", "ZIP/SQL", () => _navigationService.OpenBackupRestore(this));
        AddCardActionRow(cardUtils, "Restore From Backup", "Archive", () => _navigationService.OpenBackupRestore(this));
        AddCardActionRow(cardUtils, "Import Masters (CSV/Excel)", "Batch", () => _navigationService.OpenImportExport(this));
        AddCardActionRow(cardUtils, "Export Day Books / Trial Bal.", "PDF/XLS", () => _navigationService.OpenImportExport(this));
        cardsGrid.Controls.Add(cardUtils, 2, 1);

        pnlLeftGateway.Controls.Add(cardsGrid);
        splitTable.Controls.Add(pnlLeftGateway, 0, 0);

        // ───────────────────────────────────────────────────────────
        //  RIGHT SECTION: QUICK WIDGETS (3 Stacked Widgets)
        // ───────────────────────────────────────────────────────────
        var pnlRightWidgets = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            AutoScroll = true
        };

        // Stacked widgets layout
        var widgetsStack = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.Transparent
        };
        widgetsStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // Balance glance
        widgetsStack.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));   // Today's Vouchers table
        widgetsStack.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));   // Accelerators

        // ── Widget 1: QUICK BALANCE GLANCE ──
        var widgetBalance = CreateWidgetContainer("QUICK BALANCE GLANCE", "Refresh (F5)", () => { /* refresh balance */ });
        var pnlBalBoxes = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(8, 2, 8, 8)
        };
        pnlBalBoxes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        pnlBalBoxes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        var boxCash = CreateBalanceBox("Cash in Hand", "₹ 42,850.00 Dr", Color.FromArgb(15, 23, 42));
        var boxBank = CreateBalanceBox("Bank Accounts", "₹ 3,84,120.00 Dr", Color.FromArgb(15, 23, 42));
        pnlBalBoxes.Controls.Add(boxCash, 0, 0);
        pnlBalBoxes.Controls.Add(boxBank, 1, 0);
        widgetBalance.Controls.Add(pnlBalBoxes);
        widgetsStack.Controls.Add(widgetBalance, 0, 0);

        // ── Widget 2: TODAY'S VOUCHERS ──
        var widgetVouchers = CreateWidgetContainer("Today's Vouchers (10-Sep)", "Total: 4", null);
        var gridVouchers = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ReadOnly = true,
            Font = new Font("Segoe UI", 8F),
            EnableHeadersVisualStyles = false
        };
        gridVouchers.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        gridVouchers.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(100, 116, 139);
        gridVouchers.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
        gridVouchers.ColumnHeadersHeight = 26;
        gridVouchers.RowTemplate.Height = 24;

        gridVouchers.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Vch No", Width = 65 });
        gridVouchers.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", Width = 60 });
        gridVouchers.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Particulars", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        var colAmt = new DataGridViewTextBoxColumn { HeaderText = "Amount (₹)", Width = 80 };
        colAmt.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        colAmt.DefaultCellStyle.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
        gridVouchers.Columns.Add(colAmt);

        gridVouchers.Rows.Add("P-00042", "Payment", "Office Stat..", "1,250.00");
        gridVouchers.Rows.Add("R-00019", "Receipt", "Shiv Hard..", "18,500.00");
        gridVouchers.Rows.Add("S-00108", "Sales", "Apex Enter..", "45,000.00");
        gridVouchers.Rows.Add("C-00008", "Contra", "Cash - HDFC", "10,000.00");

        widgetVouchers.Controls.Add(gridVouchers);
        widgetsStack.Controls.Add(widgetVouchers, 0, 1);

        // ── Widget 3: KEYBOARD ACCELERATORS ──
        var widgetAccel = CreateWidgetContainer("KEYBOARD ACCELERATORS", "Accounting Standard", null);
        var pnlAccelBody = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(12, 6, 12, 6),
            AutoScroll = true
        };

        var shortcutsData = new[]
        {
            ("F2", "Date", "F4", "Contra"),
            ("F5", "Payment", "F6", "Receipt"),
            ("F7", "Journal", "F8", "Sales"),
            ("F9", "Purchase", "Ctrl+F", "Search")
        };

        int ay = 8;
        foreach (var (k1, n1, k2, n2) in shortcutsData)
        {
            var lblK1 = new Label { Text = k1, Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), ForeColor = Color.FromArgb(37, 99, 235), AutoSize = true, Location = new Point(12, ay), BackColor = Color.Transparent };
            var lblN1 = new Label { Text = n1, Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(51, 65, 85), AutoSize = true, Location = new Point(48, ay), BackColor = Color.Transparent };
            var lblK2 = new Label { Text = k2, Font = new Font("Segoe UI", 7.5F, FontStyle.Bold), ForeColor = Color.FromArgb(37, 99, 235), AutoSize = true, Location = new Point(140, ay), BackColor = Color.Transparent };
            var lblN2 = new Label { Text = n2, Font = new Font("Segoe UI", 7.5F), ForeColor = Color.FromArgb(51, 65, 85), AutoSize = true, Location = new Point(190, ay), BackColor = Color.Transparent };

            pnlAccelBody.Controls.AddRange(new Control[] { lblK1, lblN1, lblK2, lblN2 });
            ay += 22;
        }

        widgetAccel.Controls.Add(pnlAccelBody);
        widgetsStack.Controls.Add(widgetAccel, 0, 2);

        pnlRightWidgets.Controls.Add(widgetsStack);
        splitTable.Controls.Add(pnlRightWidgets, 1, 0);

        mainContainer.Controls.Add(splitTable);
        Controls.Add(mainContainer);
    }

    // ═══════════════════════════════════════════════════════════════
    //  CARD & WIDGET FACTORY HELPERS
    // ═══════════════════════════════════════════════════════════════

    private Guna2Panel CreateStructuredCard(string title, string hotkey, Color dotColor)
    {
        var card = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(226, 232, 240),
            BorderThickness = 1,
            BorderRadius = 6,
            Margin = new Padding(4),
            Padding = new Padding(0)
        };

        // Header Panel
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Color.Transparent,
            Padding = new Padding(12, 0, 12, 0)
        };

        // Dot + Title
        header.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(dotColor);
            e.Graphics.FillEllipse(brush, 12, 12, 8, 8);

            using var font = new Font("Segoe UI", 8F, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
            e.Graphics.DrawString(title, font, textBrush, 26, 9);
        };

        // Hotkey badge on right
        if (!string.IsNullOrEmpty(hotkey))
        {
            var badge = new Guna2Panel
            {
                Size = new Size(TextRenderer.MeasureText(hotkey, new Font("Segoe UI", 7F, FontStyle.Bold)).Width + 10, 18),
                FillColor = Color.FromArgb(241, 245, 249),
                BorderColor = Color.FromArgb(203, 213, 225),
                BorderThickness = 1,
                BorderRadius = 3,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            var lblHotkey = new Label
            {
                Text = hotkey,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            badge.Controls.Add(lblHotkey);
            header.Controls.Add(badge);
            header.Resize += (s, e) =>
            {
                badge.Location = new Point(header.Width - badge.Width - 12, 8);
            };
        }

        card.Controls.Add(header);
        return card;
    }

    private void AddCardActionRow(Guna2Panel card, string title, string rightTag, Action click, bool isHighlighted = false, bool isGoldBadge = false)
    {
        var row = new Panel
        {
            Dock = DockStyle.Top,
            Height = 26,
            Padding = new Padding(12, 0, 12, 0),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
            ForeColor = Color.FromArgb(30, 41, 59),
            AutoSize = true,
            Location = new Point(14, 4),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        row.Controls.Add(lblTitle);

        if (!string.IsNullOrEmpty(rightTag))
        {
            if (isHighlighted)
            {
                // Soft blue pill
                var pill = new Guna2Panel
                {
                    Size = new Size(48, 18),
                    FillColor = Color.FromArgb(219, 234, 254),
                    BorderRadius = 3,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    Cursor = Cursors.Hand
                };
                var lblPill = new Label
                {
                    Text = rightTag,
                    Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(30, 64, 175),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                pill.Controls.Add(lblPill);
                row.Controls.Add(pill);
                row.Resize += (s, e) => pill.Location = new Point(row.Width - pill.Width - 14, 4);
                pill.Click += (s, e) => click();
                lblPill.Click += (s, e) => click();
            }
            else if (isGoldBadge)
            {
                // Gold pill
                var pill = new Guna2Panel
                {
                    Size = new Size(50, 18),
                    FillColor = Color.FromArgb(254, 243, 199),
                    BorderRadius = 3,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    Cursor = Cursors.Hand
                };
                var lblPill = new Label
                {
                    Text = rightTag,
                    Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(146, 64, 14),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                pill.Controls.Add(lblPill);
                row.Controls.Add(pill);
                row.Resize += (s, e) => pill.Location = new Point(row.Width - pill.Width - 14, 4);
                pill.Click += (s, e) => click();
                lblPill.Click += (s, e) => click();
            }
            else
            {
                // Plain tag
                var lblTag = new Label
                {
                    Text = rightTag,
                    Font = new Font("Segoe UI", 7.5F),
                    ForeColor = Color.FromArgb(148, 163, 184),
                    AutoSize = true,
                    Anchor = AnchorStyles.Top | AnchorStyles.Right,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand
                };
                row.Controls.Add(lblTag);
                row.Resize += (s, e) => lblTag.Location = new Point(row.Width - lblTag.Width - 14, 5);
                lblTag.Click += (s, e) => click();
            }
        }

        // Hover Effect
        Action<bool> setHover = isHover =>
        {
            row.BackColor = isHover ? Color.FromArgb(241, 245, 249) : Color.Transparent;
            lblTitle.ForeColor = isHover ? Color.FromArgb(37, 99, 235) : Color.FromArgb(30, 41, 59);
        };

        row.MouseEnter += (s, e) => setHover(true);
        row.MouseLeave += (s, e) =>
        {
            if (!row.ClientRectangle.Contains(row.PointToClient(Cursor.Position)))
                setHover(false);
        };
        lblTitle.MouseEnter += (s, e) => setHover(true);
        lblTitle.MouseLeave += (s, e) =>
        {
            if (!row.ClientRectangle.Contains(row.PointToClient(Cursor.Position)))
                setHover(false);
        };

        row.Click += (s, e) => click();
        lblTitle.Click += (s, e) => click();

        card.Controls.Add(row);
        row.BringToFront();
    }

    private Guna2Button CreateFilterChip(string text, Action click)
    {
        return new Guna2Button
        {
            Text = text,
            Size = new Size(58, 22),
            FillColor = Color.FromArgb(241, 245, 249),
            BorderColor = Color.FromArgb(203, 213, 225),
            BorderThickness = 1,
            BorderRadius = 3,
            ForeColor = Color.FromArgb(51, 65, 85),
            Font = new Font("Segoe UI", 7F),
            Cursor = Cursors.Hand
        };
    }

    private Guna2Panel CreateWidgetContainer(string title, string rightNote, Action? rightClick)
    {
        var widget = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(226, 232, 240),
            BorderThickness = 1,
            BorderRadius = 6,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(0)
        };

        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Color.Transparent,
            Padding = new Padding(12, 0, 12, 0)
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(12, 7),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        header.Controls.Add(lblTitle);

        if (!string.IsNullOrEmpty(rightNote))
        {
            var lblRight = new Label
            {
                Text = rightNote,
                Font = new Font("Segoe UI", 7.5F),
                ForeColor = rightClick != null ? Color.FromArgb(37, 99, 235) : Color.FromArgb(148, 163, 184),
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.Transparent,
                Cursor = rightClick != null ? Cursors.Hand : Cursors.Default
            };
            if (rightClick != null) lblRight.Click += (s, e) => rightClick();
            header.Controls.Add(lblRight);
            header.Resize += (s, e) => lblRight.Location = new Point(header.Width - lblRight.Width - 12, 8);
        }

        widget.Controls.Add(header);
        return widget;
    }

    private Guna2Panel CreateBalanceBox(string label, string amount, Color textCol)
    {
        var box = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            FillColor = Color.FromArgb(248, 250, 252),
            BorderColor = Color.FromArgb(226, 232, 240),
            BorderThickness = 1,
            BorderRadius = 4,
            Margin = new Padding(4),
            Padding = new Padding(8, 6, 8, 6)
        };

        var lbl = new Label
        {
            Text = label,
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(8, 6),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        var val = new Label
        {
            Text = amount,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = textCol,
            Location = new Point(8, 26),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        box.Controls.Add(lbl);
        box.Controls.Add(val);
        return box;
    }

    // ═══════════════════════════════════════════════════════════════
    //  CONTEXT UPDATES (Company, Financial Year, Status)
    // ═══════════════════════════════════════════════════════════════

    private void UpdateCompanyContextUI()
    {
        if (_companyContext.IsCompanyOpen && _companyContext.CurrentCompany != null)
        {
            var company = _companyContext.CurrentCompany;
            var fy = _companyContext.CurrentFinancialYear;

            if (lblBannerCompName != null) lblBannerCompName.Text = company.CompanyName;
            if (lblBannerBooksBeginning != null && fy != null)
                lblBannerBooksBeginning.Text = $"|   Books Beginning: {fy.StartDate:dd-MMM-yyyy}";
            if (lblBannerCompSubtitle != null)
                lblBannerCompSubtitle.Text = $"Commercial Accounts • Wholesale & Retail Trading • Base Currency: {(string.IsNullOrWhiteSpace(company.Currency) ? "INR (₹)" : company.Currency)}";
            if (lblBannerFY != null && fy != null)
                lblBannerFY.Text = fy.YearName;
            if (lblBannerDate != null)
                lblBannerDate.Text = DateTime.Today.ToString("dd-MMM-yyyy (dddd)");

            if (lblStatusCompany != null) lblStatusCompany.Text = $"● Company: {company.CompanyName}";
            if (lblStatusFY != null) lblStatusFY.Text = fy != null ? $"FY: {fy.YearName}" : "FY: Not set";
            if (lblTitleContext != null)
                lblTitleContext.Text = $"  |  {company.CompanyName} • FY {(fy != null ? fy.YearName : "2026-27")}";
        }
        else
        {
            if (lblBannerCompName != null) lblBannerCompName.Text = "[No Company Selected]";
            if (lblBannerBooksBeginning != null) lblBannerBooksBeginning.Text = "|   Books Beginning: Not Set";
            if (lblBannerCompSubtitle != null) lblBannerCompSubtitle.Text = "No company open. Press F3 to select a company.";
            if (lblBannerFY != null) lblBannerFY.Text = "Not Selected";
            if (lblBannerDate != null) lblBannerDate.Text = DateTime.Today.ToString("dd-MMM-yyyy (dddd)");

            if (lblStatusCompany != null) lblStatusCompany.Text = "● Company: [None Selected]";
            if (lblStatusFY != null) lblStatusFY.Text = "FY: Not Selected";
            if (lblTitleContext != null) lblTitleContext.Text = "";
        }
    }

    private void UpdateUserContextUI()
    {
        if (lblStatusUser != null)
            lblStatusUser.Text = $"Operator: {_userContext.Username} ({_userContext.RoleName})";
    }

    private async Task ApplyCurrentSettingsThemeAsync()
    {
        try
        {
            var settings = await _settingsService.GetSettingsAsync();
            ThemeManager.SetTheme(settings.Theme, settings.GridDensity);
        }
        catch
        {
            // Fallback non-blocking
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  KEYBOARD SHORTCUTS
    // ═══════════════════════════════════════════════════════════════

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
                var confirm = MessageBox.Show(this, "Do you want to exit MoneyFlow Desktop ERP?", "Quit",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes) Application.Exit();
                break;
            case Keys.F1: _navigationService.OpenGlobalSearch(this); break;
            case Keys.F2: _navigationService.OpenFinancialYearList(this); break;
            case Keys.F3: _navigationService.OpenCompanyList(this); break;
            case Keys.F4: _navigationService.OpenContraVoucher(this); break;
            case Keys.F5: _navigationService.OpenPaymentVoucher(this); break;
            case Keys.F6: _navigationService.OpenReceiptVoucher(this); break;
            case Keys.F7: _navigationService.OpenJournalVoucher(this); break;
            case Keys.F8: _navigationService.OpenSalesVoucher(this); break;
            case Keys.F9: _navigationService.OpenPurchaseVoucher(this); break;
            case Keys.F10: _navigationService.OpenBackupRestore(this); break;
            case Keys.F11: _navigationService.OpenSettings(this, () => _ = ApplyCurrentSettingsThemeAsync()); break;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  WINDOW MANAGEMENT (Snap Layouts, Resize, Bounds)
    // ═══════════════════════════════════════════════════════════════

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_NCHITTEST)
        {
            base.WndProc(ref m);

            var cursor = PointToClient(Cursor.Position);

            // Windows 11 Snap Layouts: report maximize button area as HTMAXBUTTON
            if (btnMaxRestore != null && cursor.Y < 32)
            {
                var maxBtnRect = btnMaxRestore.Bounds;
                if (maxBtnRect.Contains(cursor))
                {
                    m.Result = (IntPtr)HTMAXBUTTON;
                    return;
                }
            }

            // Edge resize areas for borderless window
            if (WindowState == FormWindowState.Normal)
            {
                if (cursor.X <= ResizeBorder && cursor.Y <= ResizeBorder)
                    m.Result = (IntPtr)HTTOPLEFT;
                else if (cursor.X >= Width - ResizeBorder && cursor.Y <= ResizeBorder)
                    m.Result = (IntPtr)HTTOPRIGHT;
                else if (cursor.X <= ResizeBorder && cursor.Y >= Height - ResizeBorder)
                    m.Result = (IntPtr)HTBOTTOMLEFT;
                else if (cursor.X >= Width - ResizeBorder && cursor.Y >= Height - ResizeBorder)
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                else if (cursor.X <= ResizeBorder)
                    m.Result = (IntPtr)HTLEFT;
                else if (cursor.X >= Width - ResizeBorder)
                    m.Result = (IntPtr)HTRIGHT;
                else if (cursor.Y <= ResizeBorder)
                    m.Result = (IntPtr)HTTOP;
                else if (cursor.Y >= Height - ResizeBorder)
                    m.Result = (IntPtr)HTBOTTOM;
            }

            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (btnMaxRestore != null)
            btnMaxRestore.Text = WindowState == FormWindowState.Maximized ? "❐" : "☐";
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Maximized)
        {
            var screen = Screen.FromControl(this);
            MaximizedBounds = screen.WorkingArea;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  FORM CLOSING (auto-backup & timer cleanup)
    // ═══════════════════════════════════════════════════════════════

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        sessionTimer?.Stop();
        sessionTimer?.Dispose();

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
                    Directory.CreateDirectory(backupDir);

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
                // Non-blocking exit
            }
        }
    }
}
