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
    private Label lblTitleSeparator = null!;
    private Label lblTitleContext = null!;
    private Guna2Button btnMinimize = null!;
    private Guna2Button btnMaxRestore = null!;
    private Guna2Button btnClose = null!;

    // Menu
    private MenuStrip menuStrip = null!;

    // Toolbar
    private Guna2Panel toolbarPanel = null!;
    private System.Windows.Forms.Timer sessionTimer = null!;
    private DateTime sessionStartTime;

    // Company Banner Card (Full Width)
    private Guna2Panel pnlCompanyBanner = null!;
    private Panel pnlBannerSpacer = null!;
    private Label lblBannerCompName = null!;
    private Label lblBannerCompSubtitle = null!;
    private Label lblBannerBooksBeginning = null!;
    private Label lblBannerFY = null!;
    private Label lblBannerDate = null!;

    // Main Content Container
    private Panel mainContainer = null!;

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

        // 4. Bottom Status Bar (24px)
        CreateStatusBar();

        // 5. Operations Rail (34px Horizontal Colored Buttons)
        CreateOperationsRail();

        // 6. Main Workspace Layout (Fill)
        CreateGatewayLayout();

        // Add controls in reverse docking order for proper z-order placement
        Controls.Add(mainContainer);
        Controls.Add(operationsRail);
        Controls.Add(statusBar);
        Controls.Add(toolbarPanel);
        Controls.Add(menuStrip);
        Controls.Add(titleBar);

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
            FillColor = Color.FromArgb(11, 25, 44), // #0B192C Dark Slate Navy matching Image 2
            BorderRadius = 0,
            BorderThickness = 0
        };

        // Icon Badge with stylized emerald monogram
        var iconBadge = new Guna2Panel
        {
            Size = new Size(22, 22),
            Location = new Point(10, 5),
            FillColor = Color.FromArgb(19, 62, 77), // #133E4D Deep Slate-Teal
            BorderRadius = 5
        };
        var picBadge = new PictureBox
        {
            Image = ExecLedgerIcons.CreateAppLogoIcon(Color.FromArgb(16, 185, 129)),
            Size = new Size(18, 18),
            Location = new Point(2, 2),
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = Color.Transparent
        };
        iconBadge.Controls.Add(picBadge);
        titleBar.Controls.Add(iconBadge);

        // App Title
        lblTitleText = new Label
        {
            Text = "MONEYFLOW DESKTOP ERP [WPF .NET 8 Edition]",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(38, 7),
            BackColor = Color.Transparent
        };
        titleBar.Controls.Add(lblTitleText);

        // Separator "|"
        lblTitleSeparator = new Label
        {
            Text = "|",
            ForeColor = Color.FromArgb(71, 85, 105), // #475569
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            AutoSize = true,
            Location = new Point(lblTitleText.Right + 8, 7),
            BackColor = Color.Transparent
        };
        titleBar.Controls.Add(lblTitleSeparator);

        // Company context text (e.g. "ABC TRADERS • FY 2026-27")
        lblTitleContext = new Label
        {
            Text = "",
            ForeColor = Color.FromArgb(148, 163, 184), // #94A3B8 Slate Gray
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            AutoSize = true,
            Location = new Point(lblTitleSeparator.Right + 8, 7),
            BackColor = Color.Transparent
        };
        titleBar.Controls.Add(lblTitleContext);

        // Window Control Buttons
        int btnW = 46;
        int btnH = 32;
        Color titleBg = Color.FromArgb(11, 25, 44);

        btnClose = new Guna2Button
        {
            Text = "✕",
            ForeColor = Color.FromArgb(203, 213, 225),
            FillColor = titleBg,
            BackColor = titleBg,
            BorderThickness = 0,
            BorderRadius = 0,
            Size = new Size(btnW, btnH),
            Font = new Font("Segoe UI", 9.5F),
            HoverState = { FillColor = Color.FromArgb(220, 38, 38), ForeColor = Color.White },
            Cursor = Cursors.Hand
        };
        btnClose.Click += (s, e) => Application.Exit();

        btnMaxRestore = new Guna2Button
        {
            Text = "▢",
            ForeColor = Color.FromArgb(203, 213, 225),
            FillColor = titleBg,
            BackColor = titleBg,
            BorderThickness = 0,
            BorderRadius = 0,
            Size = new Size(btnW, btnH),
            Font = new Font("Segoe UI", 9.5F),
            HoverState = { FillColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White },
            Cursor = Cursors.Hand
        };
        btnMaxRestore.Click += (s, e) =>
        {
            WindowState = WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
            btnMaxRestore.Text = WindowState == FormWindowState.Maximized ? "❐" : "▢";
        };

        btnMinimize = new Guna2Button
        {
            Text = "—",
            ForeColor = Color.FromArgb(203, 213, 225),
            FillColor = titleBg,
            BackColor = titleBg,
            BorderThickness = 0,
            BorderRadius = 0,
            Size = new Size(btnW, btnH),
            Font = new Font("Segoe UI", 9.5F),
            HoverState = { FillColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White },
            Cursor = Cursors.Hand
        };
        btnMinimize.Click += (s, e) => WindowState = FormWindowState.Minimized;

        titleBar.Controls.Add(btnMinimize);
        titleBar.Controls.Add(btnMaxRestore);
        titleBar.Controls.Add(btnClose);

        // Position from right on resize
        void PositionRightControls()
        {
            btnClose.Location = new Point(titleBar.Width - btnW, 0);
            btnMaxRestore.Location = new Point(titleBar.Width - btnW * 2, 0);
            btnMinimize.Location = new Point(titleBar.Width - btnW * 3, 0);
        }

        titleBar.Resize += (s, e) => PositionRightControls();
        PositionRightControls();

        // Title bar drag handlers
        titleBar.MouseDown += TitleBar_MouseDown;
        lblTitleText.MouseDown += TitleBar_MouseDown;
        lblTitleSeparator.MouseDown += TitleBar_MouseDown;
        lblTitleContext.MouseDown += TitleBar_MouseDown;
        iconBadge.MouseDown += TitleBar_MouseDown;
        picBadge.MouseDown += TitleBar_MouseDown;

        titleBar.DoubleClick += (s, e) => btnMaxRestore.PerformClick();
        lblTitleText.DoubleClick += (s, e) => btnMaxRestore.PerformClick();
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
            Padding = new Padding(8, 6, 8, 6)
        };

        // Left Action Pills & Inline Search Container
        var flowLeft = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(2, 0, 2, 0),
            AutoScroll = false
        };

        // 1. Change Co. F3
        flowLeft.Controls.Add(CreateToolbarActionItem(
            ExecLedgerIcons.CreateCompanyIcon(Color.FromArgb(37, 99, 235)),
            "Change Co.", "F3",
            () => _navigationService.OpenCompanyList(this)));

        // 2. Date F2
        flowLeft.Controls.Add(CreateToolbarActionItem(
            ExecLedgerIcons.CreateCalendarIcon(Color.FromArgb(16, 185, 129)),
            "Date", "F2",
            () => _navigationService.OpenFinancialYearList(this)));

        // Separator
        flowLeft.Controls.Add(CreateToolbarSeparator());

        // 3. Day Book
        flowLeft.Controls.Add(CreateToolbarActionItem(
            ExecLedgerIcons.CreateDayBookIcon(Color.FromArgb(124, 58, 237)),
            "Day Book", null,
            () => _navigationService.OpenDayBook(this)));

        // 4. Trial Balance
        flowLeft.Controls.Add(CreateToolbarActionItem(
            ExecLedgerIcons.CreateTrialBalanceIcon(Color.FromArgb(217, 119, 6)),
            "Trial Balance", null,
            () => _navigationService.OpenTrialBalance(this)));

        // 5. P & L
        flowLeft.Controls.Add(CreateToolbarActionItem(
            ExecLedgerIcons.CreateProfitLossIcon(Color.FromArgb(147, 51, 234)),
            "P & L", null,
            () => _navigationService.OpenProfitLoss(this)));

        // 6. Balance Sheet
        flowLeft.Controls.Add(CreateToolbarActionItem(
            ExecLedgerIcons.CreateBalanceSheetIcon(Color.FromArgb(13, 148, 136)),
            "Balance Sheet", null,
            () => _navigationService.OpenBalanceSheet(this)));

        // Separator
        flowLeft.Controls.Add(CreateToolbarSeparator());

        // 7. Jump to ledger / voucher Search Box (inline in flow)
        var pnlSearch = new Guna2Panel
        {
            Size = new Size(220, 28),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(203, 213, 225),
            BorderThickness = 1,
            BorderRadius = 14,
            Cursor = Cursors.Hand,
            Margin = new Padding(3, 1, 3, 1)
        };
        var picSearch = new PictureBox
        {
            Image = ExecLedgerIcons.CreateSearchIcon(Color.FromArgb(148, 163, 184)),
            Size = new Size(16, 16),
            Location = new Point(10, 6),
            SizeMode = PictureBoxSizeMode.CenterImage,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        var lblSearchPlaceholder = new Label
        {
            Text = "Jump to ledger / voucher (Ctrl+F)",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8.25F),
            Location = new Point(28, 6),
            AutoSize = true,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        pnlSearch.Controls.Add(picSearch);
        pnlSearch.Controls.Add(lblSearchPlaceholder);

        Action openSearch = () => _navigationService.OpenGlobalSearch(this);
        pnlSearch.Click += (s, e) => openSearch();
        picSearch.Click += (s, e) => openSearch();
        lblSearchPlaceholder.Click += (s, e) => openSearch();

        pnlSearch.MouseEnter += (s, e) => pnlSearch.BorderColor = Color.FromArgb(100, 116, 139);
        pnlSearch.MouseLeave += (s, e) => pnlSearch.BorderColor = Color.FromArgb(203, 213, 225);
        lblSearchPlaceholder.MouseEnter += (s, e) => pnlSearch.BorderColor = Color.FromArgb(100, 116, 139);
        lblSearchPlaceholder.MouseLeave += (s, e) => pnlSearch.BorderColor = Color.FromArgb(203, 213, 225);

        flowLeft.Controls.Add(pnlSearch);

        toolbarPanel.Controls.Add(flowLeft);
    }

    private Control CreateToolbarActionItem(Image icon, string text, string? badge, Action onClick)
    {
        var pnl = new Guna2Panel
        {
            Height = 28,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(203, 213, 225),
            BorderThickness = 1,
            BorderRadius = 14,
            Cursor = Cursors.Hand,
            Margin = new Padding(3, 1, 3, 1)
        };

        int curX = 9;

        if (icon != null)
        {
            var pic = new PictureBox
            {
                Image = icon,
                Size = new Size(16, 16),
                SizeMode = PictureBoxSizeMode.CenterImage,
                Location = new Point(curX, 6),
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            pnl.Controls.Add(pic);
            pic.Click += (s, e) => onClick();
            curX += 20;
        }

        var lblText = new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
            ForeColor = Color.FromArgb(51, 65, 85),
            AutoSize = true,
            Location = new Point(curX, 6),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        pnl.Controls.Add(lblText);
        lblText.Click += (s, e) => onClick();
        curX += TextRenderer.MeasureText(text, lblText.Font).Width + 4;

        if (!string.IsNullOrEmpty(badge))
        {
            var pnlBadge = new Guna2Panel
            {
                FillColor = Color.FromArgb(241, 245, 249),
                BorderColor = Color.FromArgb(203, 213, 225),
                BorderThickness = 1,
                BorderRadius = 4,
                Height = 17,
                Location = new Point(curX, 5),
                Cursor = Cursors.Hand
            };
            var lblBadge = new Label
            {
                Text = badge,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 116, 139),
                AutoSize = true,
                BackColor = Color.Transparent,
                Location = new Point(3, 1),
                Cursor = Cursors.Hand
            };
            pnlBadge.Controls.Add(lblBadge);
            pnlBadge.Width = TextRenderer.MeasureText(badge, lblBadge.Font).Width + 8;
            pnl.Controls.Add(pnlBadge);
            pnlBadge.Click += (s, e) => onClick();
            lblBadge.Click += (s, e) => onClick();
            curX += pnlBadge.Width + 8;
        }
        else
        {
            curX += 6;
        }

        pnl.Width = curX;

        void SetHover(bool hover)
        {
            pnl.FillColor = hover ? Color.FromArgb(241, 245, 249) : Color.White;
            pnl.BorderColor = hover ? Color.FromArgb(148, 163, 184) : Color.FromArgb(203, 213, 225);
        }

        pnl.MouseEnter += (s, e) => SetHover(true);
        pnl.MouseLeave += (s, e) => SetHover(false);
        foreach (Control c in pnl.Controls)
        {
            c.MouseEnter += (s, e) => SetHover(true);
            c.MouseLeave += (s, e) => SetHover(false);
        }

        pnl.Click += (s, e) => onClick();

        return pnl;
    }

    private Control CreateToolbarSeparator()
    {
        return new Panel
        {
            Width = 1,
            Height = 18,
            BackColor = Color.FromArgb(226, 232, 240),
            Margin = new Padding(4, 5, 4, 5)
        };
    }

    private void UpdateSessionTime()
    {
        // Session time tracking without toolbar display
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
        mainContainer = new Panel
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
            Height = 76,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(226, 232, 240),
            BorderThickness = 1,
            BorderRadius = 6,
            Margin = new Padding(0, 0, 0, 10),
            Padding = Padding.Empty
        };

        // Left Icon Badge "MF"
        var badgePanel = new Guna2Panel
        {
            Size = new Size(40, 40),
            Location = new Point(16, 18),
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
            Location = new Point(68, 14),
            BackColor = Color.Transparent,
            UseMnemonic = false
        };
        pnlCompanyBanner.Controls.Add(lblBannerCompName);

        var pnlActivePill = new Guna2Panel
        {
            Size = new Size(95, 20),
            Location = new Point(200, 16),
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
            BackColor = Color.Transparent,
            UseMnemonic = false
        };
        pnlActivePill.Controls.Add(lblActivePillText);
        pnlCompanyBanner.Controls.Add(pnlActivePill);

        lblBannerBooksBeginning = new Label
        {
            Text = "|   Books Beginning: 01-Apr-2026",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            Location = new Point(302, 18),
            BackColor = Color.Transparent,
            UseMnemonic = false
        };
        pnlCompanyBanner.Controls.Add(lblBannerBooksBeginning);

        lblBannerCompSubtitle = new Label
        {
            Text = "Commercial Accounts • Wholesale & Retail Trading • Base Currency: INR (₹)",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            Location = new Point(68, 44),
            BackColor = Color.Transparent,
            UseMnemonic = false
        };
        pnlCompanyBanner.Controls.Add(lblBannerCompSubtitle);

        // Right Side FY and Date Unified Card (matching design reference)
        var pnlBannerRight = new Panel
        {
            Dock = DockStyle.Right,
            Width = 400,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 10, 16, 10)
        };

        var boxUnified = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(226, 232, 240),
            BorderThickness = 1,
            BorderRadius = 6
        };

        // Left Section: Financial Year
        var lblFYTag = new Label
        {
            Text = "FINANCIAL YEAR",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(14, 8),
            AutoSize = true,
            BackColor = Color.Transparent,
            UseMnemonic = false
        };
        lblBannerFY = new Label
        {
            Text = "2026 - 2027",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(14, 26),
            AutoSize = true,
            BackColor = Color.Transparent,
            UseMnemonic = false,
            Cursor = Cursors.Hand
        };
        lblBannerFY.Click += (s, e) => _navigationService.OpenFinancialYearList(this);
        boxUnified.Controls.Add(lblFYTag);
        boxUnified.Controls.Add(lblBannerFY);

        // Middle Divider Line
        var sepFYDate = new Panel
        {
            Width = 1,
            Height = 36,
            Location = new Point(140, 10),
            BackColor = Color.FromArgb(226, 232, 240)
        };
        boxUnified.Controls.Add(sepFYDate);

        // Right Section: Current Voucher Date
        var lblDateTag = new Label
        {
            Text = "CURRENT VOUCHER DATE",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(154, 8),
            AutoSize = true,
            BackColor = Color.Transparent,
            UseMnemonic = false
        };
        lblBannerDate = new Label
        {
            Text = DateTime.Today.ToString("dd-MMM-yyyy (dddd)"),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(13, 148, 136), // #0D9488 Teal
            Location = new Point(154, 26),
            AutoSize = true,
            BackColor = Color.Transparent,
            UseMnemonic = false,
            Cursor = Cursors.Hand
        };
        boxUnified.Controls.Add(lblDateTag);
        boxUnified.Controls.Add(lblBannerDate);

        pnlBannerRight.Controls.Add(boxUnified);
        pnlCompanyBanner.Controls.Add(pnlBannerRight);

        // Position Active pill dynamically after company name
        lblBannerCompName.SizeChanged += (s, e) =>
        {
            pnlActivePill.Location = new Point(lblBannerCompName.Right + 8, 16);
            lblBannerBooksBeginning.Location = new Point(pnlActivePill.Right + 8, 18);
        };

        // Spacer below banner
        pnlBannerSpacer = new Panel
        {
            Dock = DockStyle.Top,
            Height = 10,
            BackColor = Color.Transparent,
            Visible = _companyContext.IsCompanyOpen && _companyContext.CurrentCompany != null
        };
        pnlCompanyBanner.Visible = pnlBannerSpacer.Visible;

        // ── B. GATEWAY OF ACCOUNTING (3 Structured Cards) ──
        var pnlLeftGateway = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = new Padding(0)
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

        // 3-Column Grid of 3 Main Cards: MASTERS, TRANSACTIONS, REPORTS
        var cardsGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 0, 0)
        };
        cardsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        cardsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        cardsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        cardsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

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

        // Add fill first, top last so header is at Y=0
        pnlLeftGateway.Controls.Add(cardsGrid);
        pnlLeftGateway.Controls.Add(pnlGatewayHeader);

        // Add fill first, top last so banner is at Y=0 and pnlLeftGateway fills below
        mainContainer.Controls.Add(pnlLeftGateway);
        mainContainer.Controls.Add(pnlBannerSpacer);
        mainContainer.Controls.Add(pnlCompanyBanner);
    }

    // ═══════════════════════════════════════════════════════════════
    //  CARD FACTORY HELPERS
    // ═══════════════════════════════════════════════════════════════

    private Guna2Panel CreateStructuredCard(string title, string hotkey, Color dotColor)
    {
        var card = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(226, 232, 240),
            BorderThickness = 1,
            BorderRadius = 8,
            Margin = new Padding(6, 4, 6, 4),
            Padding = new Padding(0)
        };

        // Header Panel
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(16, 0, 16, 0)
        };

        // Dot + Title + Bottom line
        header.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(dotColor);
            e.Graphics.FillEllipse(brush, 14, 17, 10, 10);

            using var font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.FromArgb(15, 23, 42));
            e.Graphics.DrawString(title, font, textBrush, 32, 12);

            using var linePen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(linePen, 0, header.Height - 1, header.Width, header.Height - 1);
        };

        // Hotkey badge on right
        if (!string.IsNullOrEmpty(hotkey))
        {
            var badge = new Guna2Panel
            {
                Size = new Size(TextRenderer.MeasureText(hotkey, new Font("Segoe UI", 7.5F, FontStyle.Bold)).Width + 14, 22),
                FillColor = Color.FromArgb(241, 245, 249),
                BorderColor = Color.FromArgb(203, 213, 225),
                BorderThickness = 1,
                BorderRadius = 4,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            var lblHotkey = new Label
            {
                Text = hotkey,
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            badge.Controls.Add(lblHotkey);
            header.Controls.Add(badge);
            header.Resize += (s, e) =>
            {
                badge.Location = new Point(header.Width - badge.Width - 14, 11);
            };
        }

        var pnlContent = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(8, 6, 8, 6),
            AutoScroll = true
        };

        var tblRows = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 0,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        tblRows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        pnlContent.Controls.Add(tblRows);
        card.Controls.Add(pnlContent);
        card.Controls.Add(header);
        card.Tag = tblRows;
        return card;
    }

    private void AddCardActionRow(Guna2Panel card, string title, string rightTag, Action click, bool isHighlighted = false, bool isGoldBadge = false)
    {
        if (card.Tag is not TableLayoutPanel tblRows) return;

        int rowIndex = tblRows.RowCount;
        tblRows.RowCount = rowIndex + 1;
        tblRows.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));

        var row = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            Height = 36,
            Margin = new Padding(0, 1, 0, 1),
            Padding = new Padding(12, 0, 12, 0),
            FillColor = Color.Transparent,
            BorderRadius = 4,
            Cursor = Cursors.Hand
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(30, 41, 59),
            AutoSize = true,
            Location = new Point(14, 9),
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
                    Size = new Size(54, 22),
                    FillColor = Color.FromArgb(219, 234, 254),
                    BorderRadius = 4,
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
                row.Resize += (s, e) => pill.Location = new Point(row.Width - pill.Width - 14, 7);
                pill.Click += (s, e) => click();
                lblPill.Click += (s, e) => click();
            }
            else if (isGoldBadge)
            {
                // Gold pill
                var pill = new Guna2Panel
                {
                    Size = new Size(56, 22),
                    FillColor = Color.FromArgb(254, 243, 199),
                    BorderRadius = 4,
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
                row.Resize += (s, e) => pill.Location = new Point(row.Width - pill.Width - 14, 7);
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
                row.Resize += (s, e) => lblTag.Location = new Point(row.Width - lblTag.Width - 14, 9);
                lblTag.Click += (s, e) => click();
            }
        }

        // Hover Effect
        Action<bool> setHover = isHover =>
        {
            row.FillColor = isHover ? Color.FromArgb(241, 245, 249) : Color.Transparent;
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

        tblRows.Controls.Add(row, 0, rowIndex);
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

            if (pnlCompanyBanner != null) pnlCompanyBanner.Visible = true;
            if (pnlBannerSpacer != null) pnlBannerSpacer.Visible = true;

            if (lblBannerCompName != null) lblBannerCompName.Text = company.CompanyName;
            if (lblBannerBooksBeginning != null && fy != null)
                lblBannerBooksBeginning.Text = $"|   Books Beginning: {fy.StartDate:dd-MMM-yyyy}";
            if (lblBannerCompSubtitle != null)
                lblBannerCompSubtitle.Text = $"Commercial Accounts • Wholesale & Retail Trading • Base Currency: {(string.IsNullOrWhiteSpace(company.Currency) ? "INR (₹)" : company.Currency)}";
            if (lblBannerFY != null && fy != null)
            {
                string yearStr = fy.YearName;
                if (fy.StartDate.Year > 1900 && fy.EndDate.Year > 1900)
                {
                    yearStr = $"{fy.StartDate.Year} - {fy.EndDate.Year}";
                }
                else if (!string.IsNullOrWhiteSpace(yearStr) && yearStr.Contains('-') && !yearStr.Contains(" - "))
                {
                    yearStr = yearStr.Replace("-", " - ");
                }
                lblBannerFY.Text = yearStr;
            }
            if (lblBannerDate != null)
                lblBannerDate.Text = DateTime.Today.ToString("dd-MMM-yyyy (dddd)");

            if (lblStatusCompany != null) lblStatusCompany.Text = $"● Company: {company.CompanyName}";
            if (lblStatusFY != null) lblStatusFY.Text = fy != null ? $"FY: {fy.YearName}" : "FY: Not set";
            if (lblTitleSeparator != null && lblTitleText != null)
            {
                lblTitleSeparator.Visible = true;
                lblTitleSeparator.Location = new Point(lblTitleText.Right + 8, 7);
            }
            if (lblTitleContext != null && lblTitleSeparator != null)
            {
                lblTitleContext.Text = $"{company.CompanyName} • FY {(fy != null ? fy.YearName : "2026-27")}";
                lblTitleContext.Location = new Point(lblTitleSeparator.Right + 8, 7);
            }
        }
        else
        {
            if (pnlCompanyBanner != null) pnlCompanyBanner.Visible = false;
            if (pnlBannerSpacer != null) pnlBannerSpacer.Visible = false;

            if (lblStatusCompany != null) lblStatusCompany.Text = "● Company: [None Selected]";
            if (lblStatusFY != null) lblStatusFY.Text = "FY: Not Selected";
            if (lblTitleSeparator != null) lblTitleSeparator.Visible = false;
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
            btnMaxRestore.Text = WindowState == FormWindowState.Maximized ? "❐" : "▢";
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
