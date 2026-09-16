using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Dialogs;
using MoneyFlow.Desktop.Navigation;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Executive Ledger Desktop — Gateway of Accounting.
/// Transformed to match MONEYFLOW DESKTOP ERP layout:
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
    private Guna2Panel pnlActivePill = null!;
    private Panel pnlBannerRight = null!;
    private Guna2Panel boxUnified = null!;
    private Label lblFYTag = null!;
    private Panel sepFYDate = null!;
    private Label lblDateTag = null!;
    private string _booksBeginningDateStr = "01-Apr-2026";

    // Gateway Header & Navigation Tip
    private Panel pnlGatewayHeader = null!;
    private Label lblGwTitle = null!;
    private Label lblGwSub = null!;
    private Guna2Panel pnlNavTip = null!;

    // Gateway 3-column cards
    private TableLayoutPanel cardsGrid = null!;
    private Guna2Panel cardMasters = null!;
    private Guna2Panel cardTrans = null!;
    private Guna2Panel cardReports = null!;

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

    // Gateway of Accounting keyboard navigation state
    private class GatewayItem
    {
        public int ColumnIndex { get; set; }
        public int RowIndex { get; set; }
        public string Title { get; set; } = string.Empty;
        public string RightTag { get; set; } = string.Empty;
        public char? HotkeyChar { get; set; }
        public bool IsGoldBadge { get; set; }
        public bool IsKeyBadge { get; set; }
        public Action Action { get; set; } = () => { };
        public Guna2Panel? RowPanel { get; set; }
    }

    private readonly List<GatewayItem> _gatewayItems = new();
    private int _gatewayCol = 0; // 0: Masters, 1: Transactions, 2: Reports
    private int _gatewayRow = 1; // Default to Ledgers Master
    private string _lastMonitorName = "";

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
        Text = "MONEYFLOW DESKTOP ERP";
        AutoScaleMode = AutoScaleMode.Dpi;
        ShowIcon = true;
        LoadApplicationIcon();

        // Native monitor work area detection & window placement
        ScreenFittingManager.InitializeWindowPlacement(this, startMaximized: true);
        FormBorderStyle = FormBorderStyle.None;
        KeyPreview = true;
        BackColor = Color.FromArgb(241, 245, 249); // Soft light slate canvas
        Font = ExecLedgerTheme.UIRegular9;
        DoubleBuffered = true;

        // Guna2BorderlessForm for clean borderless management (disable internal resizing so WM_GETMINMAXINFO & WM_NCHITTEST govern)
        borderlessForm = new Guna2BorderlessForm();
        borderlessForm.ContainerControl = this;
        borderlessForm.AnimateWindow = false;
        borderlessForm.BorderRadius = 0;
        borderlessForm.ResizeForm = false;
        borderlessForm.DragForm = false; // We handle drag manually

        SuspendLayout();

        // 1. Custom Title Bar (32px, Dark Navy)
        CreateTitleBar();

        // 2. Menu Bar (26px, Clean White)
        CreateMenuBar();

        // 3. Action Toolbar (40px, Pill Buttons + Quick Search + Exit)
        CreateToolbar();

        // 4. Operations Rail (34px Horizontal Fn Button Bar as per reference design)
        CreateOperationsRail();

        // 5. Main Workspace Layout (Fill)
        CreateGatewayLayout();

        // Add controls and enforce strict docking z-order:
        // Top bars and bottom rail take outer dock edges, mainContainer fills the exact inner canvas.
        Controls.Add(mainContainer);
        Controls.Add(operationsRail);
        Controls.Add(toolbarPanel);
        Controls.Add(menuStrip);
        Controls.Add(titleBar);

        titleBar.BringToFront();
        menuStrip.BringToFront();
        toolbarPanel.BringToFront();
        operationsRail.BringToFront();
        mainContainer.SendToBack();

        // 7. Keyboard Shortcuts
        KeyDown += MainForm_KeyDown;

        // 8. Auto-Open Select Company On Startup
        Shown += (s, e) =>
        {
            ApplyResponsiveLayout();
            _navigationService.OpenCompanyList(this);
        };

        // Listen for OS display settings changes
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (s, e) =>
        {
            if (IsDisposed) return;
            BeginInvoke(() =>
            {
                var screen = Screen.FromHandle(Handle);
                if (screen != null) MaximizedBounds = screen.WorkingArea;
                ApplyResponsiveLayout();
            });
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

        // Purple growth-arrow logo directly placed on dark navy title bar (zero white-corner artifacts)
        var picBadge = new PictureBox
        {
            Image = ExecLedgerIcons.CreateAppLogoIcon(),
            Size = new Size(20, 20),
            Location = new Point(10, 6),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        titleBar.Controls.Add(picBadge);

        // App Title
        lblTitleText = new Label
        {
            Text = "MONEYFLOW DESKTOP ERP",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(36, 7),
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
        btnClose.Click += (s, e) => PromptExitApplication();

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

        titleBar.Resize += (s, e) => PositionRightControls();
        PositionRightControls();

        // Title bar drag handlers
        titleBar.MouseDown += TitleBar_MouseDown;
        lblTitleText.MouseDown += TitleBar_MouseDown;
        lblTitleSeparator.MouseDown += TitleBar_MouseDown;
        lblTitleContext.MouseDown += TitleBar_MouseDown;
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
        AddMenuItem(menuFile, "Close Company", (s, e) => _navigationService.CloseActiveCompany(this));
        menuFile.DropDownItems.Add(new ToolStripSeparator());
        AddMenuItem(menuFile, "E&xit\tEsc", (s, e) => PromptExitApplication());

        var menuCompany = new ToolStripMenuItem("&Company");
        AddMenuItem(menuCompany, "Select Company\tF3", (s, e) => _navigationService.OpenCompanyList(this));
        AddMenuItem(menuCompany, "Create Company\tAlt+C", (s, e) => _navigationService.OpenCreateCompany(this));
        AddMenuItem(menuCompany, "Alter Company\tAlt+A", (s, e) => _navigationService.OpenAlterCompany(this));
        AddMenuItem(menuCompany, "Change Financial Year\tF2", (s, e) => _navigationService.OpenFinancialYearList(this));
        AddMenuItem(menuCompany, "Close Active Company", (s, e) => _navigationService.CloseActiveCompany(this));
        menuCompany.DropDownItems.Add(new ToolStripSeparator());
        AddMenuItem(menuCompany, "E&xit\tEsc", (s, e) => PromptExitApplication());

        var menuMasters = new ToolStripMenuItem("&Masters");
        AddMenuItem(menuMasters, "&Groups (Chart of Accounts)", (s, e) => _navigationService.OpenGroupList(this));
        AddMenuItem(menuMasters, "&Ledgers", (s, e) => _navigationService.OpenLedgerList(this));

        var menuTransactions = new ToolStripMenuItem("&Transactions");
        AddMenuItem(menuTransactions, "&Contra\tF4", (s, e) => _navigationService.OpenContraVoucher(this));
        AddMenuItem(menuTransactions, "&Payment\tF5", (s, e) => _navigationService.OpenPaymentVoucher(this));
        AddMenuItem(menuTransactions, "&Receipt\tF6", (s, e) => _navigationService.OpenReceiptVoucher(this));
        AddMenuItem(menuTransactions, "&Journal\tF7", (s, e) => _navigationService.OpenJournalVoucher(this));
        AddMenuItem(menuTransactions, "&Sales Voucher\tF8", (s, e) => _navigationService.OpenSalesVoucher(this));
        AddMenuItem(menuTransactions, "&Purchase Voucher\tF9", (s, e) => _navigationService.OpenPurchaseVoucher(this));

        var menuReports = new ToolStripMenuItem("&Reports");
        AddMenuItem(menuReports, "&Day Book", (s, e) => _navigationService.OpenDayBook(this));
        AddMenuItem(menuReports, "&Ledger Statement", (s, e) => _navigationService.OpenLedgerStatement(this));
        menuReports.DropDownItems.Add(new ToolStripSeparator());
        AddMenuItem(menuReports, "&Profit && Loss", (s, e) => _navigationService.OpenProfitLoss(this));
        AddMenuItem(menuReports, "&Balance Sheet", (s, e) => _navigationService.OpenBalanceSheet(this));
        menuReports.DropDownItems.Add(new ToolStripSeparator());
        AddMenuItem(menuReports, "&Cash / Bank Book", (s, e) => _navigationService.OpenCashBankBook(this));

        var menuUtilities = new ToolStripMenuItem("&Utilities");
        AddMenuItem(menuUtilities, "&Global Search\tCtrl+F", (s, e) => _navigationService.OpenGlobalSearch(this));
        AddMenuItem(menuUtilities, "&Import / Export Data...", (s, e) => _navigationService.OpenImportExport(this));
        menuUtilities.DropDownItems.Add(new ToolStripSeparator());
        AddMenuItem(menuUtilities, "&Backup && Restore\tF10", (s, e) => _navigationService.OpenBackupRestore(this));

        var menuHelp = new ToolStripMenuItem("&Help");
        AddMenuItem(menuHelp, "Keyboard Accelerators Reference\tF1", (s, e) => _navigationService.OpenGlobalSearch(this));
        AddMenuItem(menuHelp, "About MoneyFlow ERP", (s, e) => MessageBox.Show(this, "MoneyFlow Desktop ERP\nVersion 2.0.0", "About MoneyFlow", MessageBoxButtons.OK, MessageBoxIcon.Information));

        menuStrip.Items.AddRange(new ToolStripItem[] { menuFile, menuCompany, menuMasters, menuTransactions, menuReports, menuUtilities, menuHelp });
        ExecLedgerStyler.StyleMenuStrip(menuStrip);
        MainMenuStrip = menuStrip;
    }

    private static ToolStripMenuItem AddMenuItem(ToolStripDropDownItem parent, string textWithShortcut, EventHandler onClick)
    {
        string text = textWithShortcut;
        string? shortcut = null;

        int tabIdx = textWithShortcut.IndexOf('\t');
        if (tabIdx >= 0)
        {
            text = textWithShortcut.Substring(0, tabIdx);
            shortcut = textWithShortcut.Substring(tabIdx + 1);
        }

        var item = new ToolStripMenuItem(text, null, onClick)
        {
            Padding = new Padding(14, 7, 22, 7),
            Font = new Font(ExecLedgerTheme.UiFontFamily, 9.25F, FontStyle.Regular)
        };
        if (!string.IsNullOrEmpty(shortcut))
        {
            item.ShortcutKeyDisplayString = shortcut;
            item.ShowShortcutKeys = true;
        }

        parent.DropDownItems.Add(item);
        return item;
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
            FillColor = Color.FromArgb(27, 54, 93), // Exact Prussian Navy sampled from Image 2 (#1B365D)
            BackColor = Color.FromArgb(27, 54, 93),
            BorderRadius = 0,
            BorderThickness = 0,
            Padding = new Padding(6, 4, 6, 4)
        };

        // Compound function keys with rounded colored badges + white text matching Image 2
        var fnKeys = new (string Key, string ActionText, Color BadgeColor, Action Action)[]
        {
            ("F1", "Help", Color.FromArgb(217, 119, 6), () => _navigationService.OpenGlobalSearch(this)),
            ("F2", "Date", Color.FromArgb(37, 99, 235), () => _navigationService.OpenFinancialYearList(this)),
            ("F3", "Company", Color.FromArgb(37, 99, 235), () => _navigationService.OpenCompanyList(this)),
            ("F4", "Contra", Color.FromArgb(5, 150, 105), () => _navigationService.OpenContraVoucher(this)),
            ("F5", "Payment", Color.FromArgb(5, 150, 105), () => _navigationService.OpenPaymentVoucher(this)),
            ("F6", "Receipt", Color.FromArgb(13, 148, 136), () => _navigationService.OpenReceiptVoucher(this)),
            ("F7", "Journal", Color.FromArgb(13, 148, 136), () => _navigationService.OpenJournalVoucher(this)),
            ("F8", "Sales", Color.FromArgb(13, 148, 136), () => _navigationService.OpenSalesVoucher(this)),
            ("F9", "Purchase", Color.FromArgb(13, 148, 136), () => _navigationService.OpenPurchaseVoucher(this)),
        };

        foreach (var (key, actionText, badgeColor, action) in fnKeys)
        {
            var btn = CreateRailCompoundKey(key, actionText, badgeColor, action);
            operationsRail.Controls.Add(btn);
        }

        operationsRail.Resize += (s, e) => LayoutOperationsRail();
        LayoutOperationsRail();
    }

    private void LayoutOperationsRail()
    {
        if (operationsRail == null) return;
        int ox = 8;
        int railW = operationsRail.ClientSize.Width;
        int spacing = (railW < 900) ? 2 : 4;
        foreach (Control btn in operationsRail.Controls)
        {
            if (btn is RailButton rb)
            {
                rb.RecalculateLayout();
            }
            btn.Location = new Point(ox, Math.Max(2, (operationsRail.Height - btn.Height) / 2));
            ox += btn.Width + spacing;
        }
    }

    private Control CreateRailCompoundKey(string keyText, string actionText, Color badgeColor, Action onClick)
    {
        return new RailButton(keyText, actionText, badgeColor, onClick);
    }

    private sealed class RailButton : Control
    {
        private readonly string _keyText;
        private readonly string _actionText;
        private readonly Color _badgeColor;
        private readonly Action _onClick;
        private bool _isHovered;

        public RailButton(string keyText, string actionText, Color badgeColor, Action onClick)
        {
            _keyText = keyText;
            _actionText = actionText;
            _badgeColor = badgeColor;
            _onClick = onClick;
            Cursor = Cursors.Hand;
            DoubleBuffered = true;
            TabStop = false;
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.FromArgb(27, 54, 93);

            RecalculateLayout();

            MouseEnter += (s, e) => { _isHovered = true; Invalidate(); };
            MouseLeave += (s, e) => { _isHovered = false; Invalidate(); };
            Click += (s, e) => _onClick();
        }

        public void RecalculateLayout()
        {
            using var fontBadge = new Font("Segoe UI", 7F, FontStyle.Bold);
            using var fontText = new Font("Segoe UI", 7.5F, FontStyle.Bold);

            int keyWidth = TextRenderer.MeasureText(_keyText, fontBadge).Width + 6;
            int textWidth = TextRenderer.MeasureText(_actionText, fontText).Width;
            int totalWidth = 3 + keyWidth + 5 + textWidth + 7;

            Size = new Size(totalWidth, 24);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 0. Completely erase background with rail color (#1B365D) to prevent any white corner boxes
            using (var clearBrush = new SolidBrush(Color.FromArgb(27, 54, 93)))
            {
                g.FillRectangle(clearBrush, ClientRectangle);
            }

            // 1. Button Rounded Pill (#1D2C42, hover #2A4160) - zero border to eliminate white frames
            var rect = new Rectangle(0, 0, Width, Height);
            Color bg = _isHovered ? Color.FromArgb(42, 65, 96) : Color.FromArgb(29, 44, 66);
            using (var path = CreateRoundedPath(rect, 4))
            using (var brush = new SolidBrush(bg))
            {
                g.FillPath(brush, path);
            }

            // 2. Key Badge (rounded rectangle with solid badge color)
            using var fontBadge = new Font("Segoe UI", 7F, FontStyle.Bold);
            using var fontText = new Font("Segoe UI", 7.5F, FontStyle.Bold);

            int keyWidth = TextRenderer.MeasureText(_keyText, fontBadge).Width + 6;
            var badgeRect = new Rectangle(3, 3, keyWidth, 18);
            using (var badgePath = CreateRoundedPath(badgeRect, 3))
            using (var badgeBrush = new SolidBrush(_badgeColor))
            {
                g.FillPath(badgeBrush, badgePath);
            }

            // 3. Key Text inside badge
            var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            using (var textBrush = new SolidBrush(Color.White))
            {
                g.DrawString(_keyText, fontBadge, textBrush, badgeRect, sfCenter);
            }

            // 4. Action Text
            var textRect = new Rectangle(3 + keyWidth + 5, 1, Width - (3 + keyWidth + 5) - 2, 22);
            var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            using (var textBrush = new SolidBrush(Color.White))
            {
                g.DrawString(_actionText, fontText, textBrush, textRect, sfLeft);
            }
        }
    }

    private static GraphicsPath CreateRoundedPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
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
            AutoScroll = true,
            AutoScrollMinSize = new Size(960, 520)
        };
        mainContainer.Resize += (s, e) => ApplyResponsiveLayout();

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
        pnlCompanyBanner.Resize += (s, e) => LayoutCompanyBanner();

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
            UseMnemonic = false,
            AutoEllipsis = true
        };
        pnlCompanyBanner.Controls.Add(lblBannerCompName);

        pnlActivePill = new Guna2Panel
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
            Text = "Accounts",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            Location = new Point(68, 44),
            BackColor = Color.Transparent,
            UseMnemonic = false
        };
        pnlCompanyBanner.Controls.Add(lblBannerCompSubtitle);

        // Right Side FY and Date Unified Card (matching design reference)
        pnlBannerRight = new Panel
        {
            Dock = DockStyle.Right,
            Width = 380,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 10, 16, 10)
        };

        boxUnified = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(226, 232, 240),
            BorderThickness = 1,
            BorderRadius = 6
        };
        boxUnified.Resize += (s, e) => LayoutUnifiedDateBox();

        // Left Section: Financial Year
        lblFYTag = new Label
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
        sepFYDate = new Panel
        {
            Width = 1,
            Height = 36,
            Location = new Point(140, 10),
            BackColor = Color.FromArgb(226, 232, 240)
        };
        boxUnified.Controls.Add(sepFYDate);

        // Right Section: Current Voucher Date
        lblDateTag = new Label
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
            Cursor = Cursors.Hand,
            AutoEllipsis = true
        };
        boxUnified.Controls.Add(lblDateTag);
        boxUnified.Controls.Add(lblBannerDate);

        pnlBannerRight.Controls.Add(boxUnified);
        pnlCompanyBanner.Controls.Add(pnlBannerRight);

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
        pnlGatewayHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.Transparent
        };
        pnlGatewayHeader.Resize += (s, e) => LayoutGatewayHeader();

        lblGwTitle = new Label
        {
            Text = "GATEWAY OF ACCOUNTING",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(0, 2),
            AutoSize = true
        };
        lblGwSub = new Label
        {
            Text = "Press highlighted underlined key or click category to open master modules",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(0, 22),
            AutoSize = true
        };
        pnlGatewayHeader.Controls.Add(lblGwTitle);
        pnlGatewayHeader.Controls.Add(lblGwSub);

        // Navigation Tip Pill with key badges (matching Image 1)
        pnlNavTip = new Guna2Panel
        {
            Size = new Size(395, 28),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FillColor = Color.FromArgb(239, 246, 255),
            BorderColor = Color.FromArgb(191, 219, 254),
            BorderThickness = 1,
            BorderRadius = 4
        };

        var flowNav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            AutoSize = false,
            BackColor = Color.Transparent,
            Padding = new Padding(8, 3, 8, 3)
        };

        var lblNavPrefix = new Label
        {
            Text = "Navigation Tip: Use",
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = Color.FromArgb(29, 78, 216),
            AutoSize = true,
            Margin = new Padding(0, 3, 2, 0)
        };

        var badgeArrow = new Guna2Panel
        {
            Size = new Size(72, 20),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(191, 219, 254),
            BorderThickness = 1,
            BorderRadius = 3,
            Margin = new Padding(2, 0, 2, 0)
        };
        var lblArrow = new Label
        {
            Text = "Arrow Keys",
            Font = new Font("Segoe UI", 7F),
            ForeColor = Color.FromArgb(51, 65, 85),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        badgeArrow.Controls.Add(lblArrow);

        var lblNavPlus = new Label
        {
            Text = "+",
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = Color.FromArgb(29, 78, 216),
            AutoSize = true,
            Margin = new Padding(2, 3, 2, 0)
        };

        var badgeEnter = new Guna2Panel
        {
            Size = new Size(38, 20),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(191, 219, 254),
            BorderThickness = 1,
            BorderRadius = 3,
            Margin = new Padding(2, 0, 2, 0)
        };
        var lblEnter = new Label
        {
            Text = "Enter",
            Font = new Font("Segoe UI", 7F),
            ForeColor = Color.FromArgb(51, 65, 85),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        badgeEnter.Controls.Add(lblEnter);

        var lblNavSuffix = new Label
        {
            Text = "or single hotkeys.",
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = Color.FromArgb(29, 78, 216),
            AutoSize = true,
            Margin = new Padding(2, 3, 0, 0)
        };

        flowNav.Controls.Add(lblNavPrefix);
        flowNav.Controls.Add(badgeArrow);
        flowNav.Controls.Add(lblNavPlus);
        flowNav.Controls.Add(badgeEnter);
        flowNav.Controls.Add(lblNavSuffix);
        pnlNavTip.Controls.Add(flowNav);
        pnlGatewayHeader.Controls.Add(pnlNavTip);

        // 3-Column Grid of 3 Main Cards: MASTERS, TRANSACTIONS, REPORTS (Fit to screen)
        cardsGrid = new TableLayoutPanel
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
        cardsGrid.Resize += (s, e) => UpdateCardRowHeights();

        _gatewayItems.Clear();

        // ── Card 1: MASTERS [M] ──
        cardMasters = CreateStructuredCard("MASTERS", "M", Color.FromArgb(37, 99, 235), 0);
        AddCardActionRow(cardMasters, 0, "Groups", "Hierarchy", () => _navigationService.OpenGroupList(this), hotkeyChar: 'G');
        AddCardActionRow(cardMasters, 0, "Ledgers Master", "Primary", () => _navigationService.OpenLedgerList(this), hotkeyChar: 'L', isHighlighted: true);
        cardsGrid.Controls.Add(cardMasters, 0, 0);

        // ── Card 2: TRANSACTIONS [T] ──
        cardTrans = CreateStructuredCard("TRANSACTIONS", "T", Color.FromArgb(16, 185, 129), 1);
        AddCardActionRow(cardTrans, 1, "Payment", "F5", () => _navigationService.OpenPaymentVoucher(this), hotkeyChar: 'P', isKeyBadge: true);
        AddCardActionRow(cardTrans, 1, "Receipt", "F6", () => _navigationService.OpenReceiptVoucher(this), hotkeyChar: 'R', isKeyBadge: true);
        AddCardActionRow(cardTrans, 1, "Contra", "F4", () => _navigationService.OpenContraVoucher(this), hotkeyChar: 'C', isKeyBadge: true);
        AddCardActionRow(cardTrans, 1, "Journal", "F7", () => _navigationService.OpenJournalVoucher(this), hotkeyChar: 'J', isKeyBadge: true);
        AddCardActionRow(cardTrans, 1, "Sales Voucher", "F8", () => _navigationService.OpenSalesVoucher(this), hotkeyChar: 'S', isKeyBadge: true);
        AddCardActionRow(cardTrans, 1, "Purchase Voucher", "F9", () => _navigationService.OpenPurchaseVoucher(this), hotkeyChar: 'P', isKeyBadge: true);
        cardsGrid.Controls.Add(cardTrans, 1, 0);

        // ── Card 3: REPORTS [R] ──
        cardReports = CreateStructuredCard("REPORTS", "R", Color.FromArgb(245, 158, 11), 2);
        AddCardActionRow(cardReports, 2, "Day Book", "Daily Ledger", () => _navigationService.OpenDayBook(this), hotkeyChar: 'D');
        AddCardActionRow(cardReports, 2, "Ledger Accounts", "Statement", () => _navigationService.OpenLedgerStatement(this), hotkeyChar: 'L');
        AddCardActionRow(cardReports, 2, "Profit & Loss", "P&L Stmt", () => _navigationService.OpenProfitLoss(this), hotkeyChar: 'P');
        AddCardActionRow(cardReports, 2, "Balance Sheet", "Financials", () => _navigationService.OpenBalanceSheet(this), hotkeyChar: 'B');
        AddCardActionRow(cardReports, 2, "Cash & Bank Book", "Funds Flow", () => _navigationService.OpenCashBankBook(this), hotkeyChar: 'C');
        cardsGrid.Controls.Add(cardReports, 2, 0);

        UpdateGatewaySelectionUI();

        // Add cardsGrid and header to gateway panel
        pnlLeftGateway.Controls.Add(cardsGrid);
        pnlLeftGateway.Controls.Add(pnlGatewayHeader);

        // Add to main container
        mainContainer.Controls.Add(pnlLeftGateway);
        mainContainer.Controls.Add(pnlBannerSpacer);
        mainContainer.Controls.Add(pnlCompanyBanner);

        LayoutCompanyBanner();
        LayoutGatewayHeader();
        UpdateCardRowHeights();
    }

    // ═══════════════════════════════════════════════════════════════
    //  CARD FACTORY HELPERS
    // ═══════════════════════════════════════════════════════════════

    private Guna2Panel CreateStructuredCard(string title, string hotkey, Color dotColor, int colIndex)
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
            Padding = new Padding(16, 0, 16, 0),
            Cursor = Cursors.Hand
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

        // Hotkey badge on right: [ M ], [ T ], [ R ]
        if (!string.IsNullOrEmpty(hotkey))
        {
            var pnlBadgeRight = new Panel
            {
                Dock = DockStyle.Right,
                Width = 44,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };

            var badge = new Guna2Panel
            {
                Size = new Size(26, 22),
                Location = new Point(4, 11),
                FillColor = Color.FromArgb(241, 245, 249),
                BorderColor = Color.FromArgb(203, 213, 225),
                BorderThickness = 1,
                BorderRadius = 4,
                Cursor = Cursors.Hand
            };
            var lblHotkey = new Label
            {
                Text = hotkey,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = Color.FromArgb(71, 85, 105),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand
            };
            badge.Controls.Add(lblHotkey);
            pnlBadgeRight.Controls.Add(badge);
            header.Controls.Add(pnlBadgeRight);

            Action selectCol = () => JumpToGatewayColumn(colIndex);
            badge.Click += (s, e) => selectCol();
            lblHotkey.Click += (s, e) => selectCol();
            pnlBadgeRight.Click += (s, e) => selectCol();
            header.Click += (s, e) => selectCol();
        }

        var pnlContent = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(8, 6, 8, 6),
            AutoScroll = false
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

    private void AddCardActionRow(
        Guna2Panel card,
        int colIndex,
        string title,
        string rightTag,
        Action click,
        char? hotkeyChar = null,
        bool isHighlighted = false,
        bool isGoldBadge = false,
        bool isKeyBadge = false)
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
            FillColor = isHighlighted ? Color.FromArgb(239, 246, 255) : Color.Transparent,
            BorderColor = isHighlighted ? Color.FromArgb(191, 219, 254) : Color.Transparent,
            BorderThickness = isHighlighted ? 1 : 0,
            BorderRadius = 4,
            Cursor = Cursors.Hand
        };
        row.Tag = isHighlighted;

        var item = new GatewayItem
        {
            ColumnIndex = colIndex,
            RowIndex = rowIndex,
            Title = title,
            RightTag = rightTag,
            HotkeyChar = hotkeyChar,
            IsGoldBadge = isGoldBadge,
            IsKeyBadge = isKeyBadge,
            Action = click,
            RowPanel = row
        };
        _gatewayItems.Add(item);

        bool isHovered = false;

        // Custom text painting to draw the hotkey letter with bold underline matching Image 1
        row.Paint += (s, e) =>
        {
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            bool isSelected = (row.Tag is true);

            Color textColor = isHovered
                ? Color.FromArgb(37, 99, 235)
                : (isSelected ? Color.FromArgb(30, 64, 175) : Color.FromArgb(30, 41, 59));

            int startX = 14;
            using var sampleFont = new Font(ExecLedgerTheme.UiFontFamily, 9F, FontStyle.Regular);
            int textH = TextRenderer.MeasureText(e.Graphics, "Ag", sampleFont, Size.Empty, TextFormatFlags.NoPadding).Height;
            int startY = Math.Max(2, (row.Height - textH) / 2);

            if (hotkeyChar.HasValue && title.StartsWith(hotkeyChar.Value))
            {
                string keyStr = hotkeyChar.Value.ToString();
                string restStr = title.Substring(1);

                using var keyFont = new Font(ExecLedgerTheme.UiFontFamily, 9F, FontStyle.Bold | FontStyle.Underline);
                using var restFont = new Font(ExecLedgerTheme.UiFontFamily, 9F, FontStyle.Regular);

                var keySize = TextRenderer.MeasureText(e.Graphics, keyStr, keyFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(e.Graphics, keyStr, keyFont, new Point(startX, startY), textColor, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(e.Graphics, restStr, restFont, new Point(startX + keySize.Width, startY), textColor, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
            else
            {
                using var normFont = new Font(ExecLedgerTheme.UiFontFamily, 9F, FontStyle.Regular);
                TextRenderer.DrawText(e.Graphics, title, normFont, new Point(startX, startY), textColor, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            }
        };

        Control? rightBadgeControl = null;
        if (!string.IsNullOrEmpty(rightTag))
        {
            if (isHighlighted)
            {
                // Soft blue pill (e.g. Primary)
                var pill = new Guna2Panel
                {
                    Size = new Size(54, 22),
                    FillColor = Color.FromArgb(219, 234, 254),
                    BorderRadius = 4,
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
                    Cursor = Cursors.Hand,
                    UseMnemonic = false
                };
                pill.Controls.Add(lblPill);
                row.Controls.Add(pill);
                rightBadgeControl = pill;
                pill.Click += (s, e) => { SetGatewaySelection(colIndex, rowIndex); click(); };
                lblPill.Click += (s, e) => { SetGatewaySelection(colIndex, rowIndex); click(); };
            }
            else if (isGoldBadge)
            {
                // Gold pill (e.g. Auditing)
                var pill = new Guna2Panel
                {
                    Size = new Size(56, 22),
                    FillColor = Color.FromArgb(254, 243, 199),
                    BorderRadius = 4,
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
                    Cursor = Cursors.Hand,
                    UseMnemonic = false
                };
                pill.Controls.Add(lblPill);
                row.Controls.Add(pill);
                rightBadgeControl = pill;
                pill.Click += (s, e) => { SetGatewaySelection(colIndex, rowIndex); click(); };
                lblPill.Click += (s, e) => { SetGatewaySelection(colIndex, rowIndex); click(); };
            }
            else if (isKeyBadge)
            {
                // Key badge (e.g. F5, F6, F4, etc.)
                var keyBox = new Guna2Panel
                {
                    Size = new Size(32, 22),
                    FillColor = Color.FromArgb(248, 250, 252),
                    BorderColor = Color.FromArgb(203, 213, 225),
                    BorderThickness = 1,
                    BorderRadius = 4,
                    Cursor = Cursors.Hand
                };
                var lblKey = new Label
                {
                    Text = rightTag,
                    Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(71, 85, 105),
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                    UseMnemonic = false
                };
                keyBox.Controls.Add(lblKey);
                row.Controls.Add(keyBox);
                rightBadgeControl = keyBox;
                keyBox.Click += (s, e) => { SetGatewaySelection(colIndex, rowIndex); click(); };
                lblKey.Click += (s, e) => { SetGatewaySelection(colIndex, rowIndex); click(); };
            }
            else
            {
                // Plain tag (e.g. Hierarchy, Inventory, P&L Stmt)
                var lblTag = new Label
                {
                    Text = rightTag,
                    Font = new Font("Segoe UI", 7.5F),
                    ForeColor = Color.FromArgb(148, 163, 184),
                    AutoSize = true,
                    BackColor = Color.Transparent,
                    Cursor = Cursors.Hand,
                    UseMnemonic = false
                };
                row.Controls.Add(lblTag);
                rightBadgeControl = lblTag;
                lblTag.Click += (s, e) => { SetGatewaySelection(colIndex, rowIndex); click(); };
            }

            void RepositionBadge()
            {
                if (rightBadgeControl != null)
                {
                    int by = Math.Max(2, (row.Height - rightBadgeControl.Height) / 2);
                    rightBadgeControl.Location = new Point(row.Width - rightBadgeControl.Width - 14, by);
                }
            }

            row.Resize += (s, e) => RepositionBadge();
            RepositionBadge();
        }

        // Hover Effect
        row.MouseEnter += (s, e) =>
        {
            isHovered = true;
            bool isSelected = (row.Tag is true);
            if (!isSelected) row.FillColor = Color.FromArgb(241, 245, 249);
            row.Invalidate();
        };

        row.MouseLeave += (s, e) =>
        {
            if (!row.ClientRectangle.Contains(row.PointToClient(Cursor.Position)))
            {
                isHovered = false;
                bool isSelected = (row.Tag is true);
                if (!isSelected) row.FillColor = Color.Transparent;
                row.Invalidate();
            }
        };

        row.Click += (s, e) =>
        {
            SetGatewaySelection(colIndex, rowIndex);
            click();
        };

        tblRows.Controls.Add(row, 0, rowIndex);
    }

    private void SetGatewaySelection(int col, int row)
    {
        _gatewayCol = col;
        _gatewayRow = row;
        UpdateGatewaySelectionUI();
    }

    private void NavigateGateway(int deltaCol, int deltaRow)
    {
        if (_gatewayItems.Count == 0) return;

        if (deltaCol != 0)
        {
            _gatewayCol = Math.Clamp(_gatewayCol + deltaCol, 0, 2);
        }

        var colItems = _gatewayItems.Where(i => i.ColumnIndex == _gatewayCol).OrderBy(i => i.RowIndex).ToList();
        if (colItems.Count == 0) return;

        if (deltaRow != 0)
        {
            _gatewayRow += deltaRow;
            if (_gatewayRow < 0) _gatewayRow = colItems.Count - 1;
            else if (_gatewayRow >= colItems.Count) _gatewayRow = 0;
        }
        else
        {
            _gatewayRow = Math.Clamp(_gatewayRow, 0, colItems.Count - 1);
        }

        UpdateGatewaySelectionUI();
    }

    private void JumpToGatewayColumn(int col)
    {
        _gatewayCol = Math.Clamp(col, 0, 2);
        var colItems = _gatewayItems.Where(i => i.ColumnIndex == _gatewayCol).ToList();
        if (_gatewayRow >= colItems.Count)
            _gatewayRow = 0;

        UpdateGatewaySelectionUI();
    }

    private void ExecuteSelectedGatewayItem()
    {
        var item = _gatewayItems.FirstOrDefault(i => i.ColumnIndex == _gatewayCol && i.RowIndex == _gatewayRow);
        item?.Action?.Invoke();
    }

    private void UpdateGatewaySelectionUI()
    {
        foreach (var item in _gatewayItems)
        {
            if (item.RowPanel == null) continue;

            bool isSelected = (item.ColumnIndex == _gatewayCol && item.RowIndex == _gatewayRow);
            item.RowPanel.FillColor = isSelected ? Color.FromArgb(239, 246, 255) : Color.Transparent;
            item.RowPanel.BorderColor = isSelected ? Color.FromArgb(191, 219, 254) : Color.Transparent;
            item.RowPanel.BorderThickness = isSelected ? 1 : 0;
            item.RowPanel.Tag = isSelected;
            item.RowPanel.Invalidate();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  RESPONSIVE LAYOUT ENGINE
    // ═══════════════════════════════════════════════════════════════

    public void ApplyResponsiveLayout()
    {
        PositionRightControls();
        AdaptTierDimensions();
        LayoutCompanyBanner();
        LayoutGatewayHeader();
        UpdateCardRowHeights();
        LayoutOperationsRail();
    }

    private void AdaptTierDimensions()
    {
        if (mainContainer == null || pnlCompanyBanner == null || pnlBannerSpacer == null || pnlGatewayHeader == null) return;

        var tier = ScreenFittingManager.ClassifyTier(mainContainer.ClientSize.Width, mainContainer.ClientSize.Height);

        switch (tier)
        {
            case LayoutTier.Compact:
                pnlCompanyBanner.Height = 62;
                pnlBannerSpacer.Height = 6;
                pnlGatewayHeader.Height = 36;
                mainContainer.Padding = new Padding(8, 6, 8, 6);
                mainContainer.AutoScrollMinSize = new Size(800, 360);
                break;

            case LayoutTier.Standard:
                pnlCompanyBanner.Height = 72;
                pnlBannerSpacer.Height = 8;
                pnlGatewayHeader.Height = 40;
                mainContainer.Padding = new Padding(12, 8, 12, 8);
                mainContainer.AutoScrollMinSize = new Size(900, 420);
                break;

            case LayoutTier.Large:
            default:
                pnlCompanyBanner.Height = 78;
                pnlBannerSpacer.Height = 10;
                pnlGatewayHeader.Height = 44;
                mainContainer.Padding = new Padding(16, 10, 16, 10);
                mainContainer.AutoScrollMinSize = new Size(960, 460);
                break;
        }
    }

    private void PositionRightControls()
    {
        if (titleBar == null || btnClose == null || btnMaxRestore == null || btnMinimize == null) return;
        int btnW = 46;
        btnClose.Location = new Point(titleBar.Width - btnW, 0);
        btnMaxRestore.Location = new Point(titleBar.Width - btnW * 2, 0);
        btnMinimize.Location = new Point(titleBar.Width - btnW * 3, 0);

        if (lblTitleContext != null && lblTitleSeparator != null)
        {
            int maxCtxW = Math.Max(20, titleBar.Width - (btnW * 3 + 16) - lblTitleContext.Left);
            lblTitleContext.MaximumSize = new Size(maxCtxW, 20);
            lblTitleContext.AutoEllipsis = true;
        }
    }

    private void LayoutUnifiedDateBox()
    {
        if (boxUnified == null || sepFYDate == null || lblFYTag == null || lblBannerFY == null || lblDateTag == null || lblBannerDate == null) return;

        int boxW = boxUnified.Width;
        int sepX = Math.Clamp((int)(boxW * 0.38f), 110, 150);

        sepFYDate.Location = new Point(sepX, (boxUnified.Height - sepFYDate.Height) / 2);
        lblFYTag.Location = new Point(12, 8);
        lblBannerFY.Location = new Point(12, 26);

        int dateX = sepX + 12;
        lblDateTag.Location = new Point(dateX, 8);
        lblBannerDate.Location = new Point(dateX, 26);
        lblBannerDate.MaximumSize = new Size(Math.Max(50, boxW - dateX - 8), 24);
        lblBannerDate.AutoEllipsis = true;
    }

    private void LayoutCompanyBanner()
    {
        if (pnlCompanyBanner == null || lblBannerCompName == null || pnlActivePill == null || lblBannerBooksBeginning == null || pnlBannerRight == null) return;

        // Dynamic width for right box based on banner width
        int bannerW = pnlCompanyBanner.Width;
        int rightW = Math.Clamp((int)(bannerW * 0.32f), 320, 420);
        pnlBannerRight.Width = rightW;
        LayoutUnifiedDateBox();

        int leftAvail = bannerW - rightW - 76;
        int nameW = TextRenderer.MeasureText(lblBannerCompName.Text, lblBannerCompName.Font).Width;
        int pillW = pnlActivePill.Width;
        int booksW = TextRenderer.MeasureText(lblBannerBooksBeginning.Text, lblBannerBooksBeginning.Font).Width;

        // If company name + active pill + books beginning fit on line 1:
        if (nameW + pillW + booksW + 24 <= leftAvail)
        {
            lblBannerCompName.Location = new Point(68, 14);
            lblBannerCompName.MaximumSize = new Size(Math.Max(100, leftAvail - pillW - booksW - 24), 28);
            lblBannerCompName.AutoEllipsis = true;
            pnlActivePill.Location = new Point(lblBannerCompName.Right + 8, 16);
            lblBannerBooksBeginning.Text = _booksBeginningDateStr.StartsWith("|")
                ? _booksBeginningDateStr
                : $"|   Books Beginning: {_booksBeginningDateStr}";
            lblBannerBooksBeginning.Location = new Point(pnlActivePill.Right + 8, 18);
            lblBannerCompSubtitle.Location = new Point(68, 44);
        }
        else
        {
            // Compact two-line layout: Line 1 has Company Name + Pill, Line 2 has Subtitle + Books Beginning
            lblBannerCompName.Location = new Point(68, 12);
            lblBannerCompName.MaximumSize = new Size(Math.Max(100, leftAvail - pillW - 16), 28);
            lblBannerCompName.AutoEllipsis = true;
            pnlActivePill.Location = new Point(lblBannerCompName.Right + 8, 14);

            lblBannerCompSubtitle.Location = new Point(68, 42);
            lblBannerBooksBeginning.Text = $"|   Books Beginning: {_booksBeginningDateStr}";
            lblBannerBooksBeginning.Location = new Point(lblBannerCompSubtitle.Right + 8, 43);
        }
    }

    private void LayoutGatewayHeader()
    {
        if (pnlGatewayHeader == null || pnlNavTip == null || lblGwTitle == null || lblGwSub == null) return;

        int headerW = pnlGatewayHeader.Width;
        if (headerW >= 860)
        {
            lblGwSub.Text = "Press highlighted underlined key or click category to open master modules";
            lblGwSub.Visible = true;
            pnlNavTip.Visible = true;
            pnlNavTip.Location = new Point(headerW - pnlNavTip.Width, 6);
        }
        else if (headerW >= 660)
        {
            lblGwSub.Text = "Click category or press underlined key to open modules";
            lblGwSub.Visible = true;
            pnlNavTip.Visible = true;
            pnlNavTip.Location = new Point(headerW - pnlNavTip.Width, 6);
        }
        else
        {
            lblGwSub.Text = "Click category or press key to open";
            pnlNavTip.Visible = false;
        }
    }

    private void UpdateCardRowHeights()
    {
        if (cardsGrid == null || mainContainer == null) return;

        var tier = ScreenFittingManager.ClassifyTier(mainContainer.ClientSize.Width, mainContainer.ClientSize.Height);
        float targetRowH = tier switch
        {
            LayoutTier.Compact => 34f,
            LayoutTier.Large => 38f,
            _ => 36f
        };

        var cards = new[] { cardMasters, cardTrans, cardReports };
        foreach (var card in cards)
        {
            if (card?.Tag is TableLayoutPanel tbl)
            {
                tbl.SuspendLayout();
                for (int r = 0; r < tbl.RowStyles.Count; r++)
                {
                    tbl.RowStyles[r].SizeType = SizeType.Absolute;
                    tbl.RowStyles[r].Height = targetRowH + 2;
                }
                foreach (Control rowCtrl in tbl.Controls)
                {
                    rowCtrl.Height = (int)targetRowH;
                    rowCtrl.Invalidate();
                }
                tbl.ResumeLayout(true);
            }
        }
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
            if (fy != null)
            {
                _booksBeginningDateStr = fy.StartDate.ToString("dd-MMM-yyyy");
            }
            if (lblBannerBooksBeginning != null)
                lblBannerBooksBeginning.Text = $"|   Books Beginning: {_booksBeginningDateStr}";
            if (lblBannerCompSubtitle != null)
                lblBannerCompSubtitle.Text = $"Accounts";
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

            LayoutCompanyBanner();
            PositionRightControls();
        }
        else
        {
            if (pnlCompanyBanner != null) pnlCompanyBanner.Visible = false;
            if (pnlBannerSpacer != null) pnlBannerSpacer.Visible = false;

            if (lblStatusCompany != null) lblStatusCompany.Text = "● Company: [None Selected]";
            if (lblStatusFY != null) lblStatusFY.Text = "FY: Not Selected";
            if (lblTitleSeparator != null) lblTitleSeparator.Visible = false;
            if (lblTitleContext != null) lblTitleContext.Text = "";
            PositionRightControls();
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

        // Single hotkeys for dashboard navigation (Tally style)
        if (!e.Control && !e.Alt && ActiveControl is not TextBox and not Guna2TextBox)
        {
            switch (e.KeyCode)
            {
                case Keys.G: _navigationService.OpenGroupList(this); e.Handled = true; return;
                case Keys.L: _navigationService.OpenLedgerList(this); e.Handled = true; return;
                case Keys.D: _navigationService.OpenDayBook(this); e.Handled = true; return;
                case Keys.B: _navigationService.OpenBalanceSheet(this); e.Handled = true; return;
            }
        }

        switch (e.KeyCode)
        {
            case Keys.Escape:
                PromptExitApplication();
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
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Don't intercept when user is typing in a text field
        if (ActiveControl is TextBox or Guna2TextBox or ComboBox or RichTextBox)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        switch (keyData)
        {
            case Keys.Down:
                NavigateGateway(0, 1);
                return true;
            case Keys.Up:
                NavigateGateway(0, -1);
                return true;
            case Keys.Right:
                NavigateGateway(1, 0);
                return true;
            case Keys.Left:
                NavigateGateway(-1, 0);
                return true;
            case Keys.Enter:
                ExecuteSelectedGatewayItem();
                return true;
            case Keys.M:
                JumpToGatewayColumn(0);
                return true;
            case Keys.T:
                JumpToGatewayColumn(1);
                return true;
            case Keys.R:
                JumpToGatewayColumn(2);
                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ═══════════════════════════════════════════════════════════════
    //  WINDOW MANAGEMENT (Snap Layouts, Resize, Bounds)
    // ═══════════════════════════════════════════════════════════════

    protected override void WndProc(ref Message m)
    {
        // 1. Enforce taskbar-aware maximized bounds via Win32 MINMAXINFO
        if (m.Msg == ScreenFittingManager.WM_GETMINMAXINFO)
        {
            base.WndProc(ref m);
            ScreenFittingManager.HandleGetMinMaxInfo(m.HWnd, m.LParam);
            return;
        }

        // 2. Dynamic DPI change across monitors (Windows PerMonitorV2)
        if (m.Msg == ScreenFittingManager.WM_DPICHANGED)
        {
            base.WndProc(ref m);
            ApplyResponsiveLayout();
            return;
        }

        // 3. Monitor display resolution / scaling changed in Windows
        if (m.Msg == ScreenFittingManager.WM_DISPLAYCHANGE)
        {
            base.WndProc(ref m);
            var screen = Screen.FromHandle(Handle);
            if (screen != null) MaximizedBounds = screen.WorkingArea;
            ApplyResponsiveLayout();
            return;
        }

        // 4. Edge resize areas for borderless window
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

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (IsHandleCreated)
        {
            var screen = Screen.FromHandle(Handle);
            if (screen != null && screen.DeviceName != _lastMonitorName)
            {
                _lastMonitorName = screen.DeviceName;
                MaximizedBounds = screen.WorkingArea;
                ApplyResponsiveLayout();
            }
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Maximized)
        {
            var screen = (IsHandleCreated ? Screen.FromHandle(Handle) : null) ?? Screen.PrimaryScreen;
            if (screen != null) MaximizedBounds = screen.WorkingArea;
        }
        ApplyResponsiveLayout();
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyResponsiveLayout();
    }

    private void PromptExitApplication()
    {
        string? companyName = _companyContext.CurrentCompany?.CompanyName;
        string? snapshotPath = null;
        if (_companyContext.CurrentCompany != null)
        {
            string baseDir = !string.IsNullOrWhiteSpace(_companyContext.CurrentCompany.DataDirectory)
                ? _companyContext.CurrentCompany.DataDirectory
                : @"C:\MoneyFlow\Data";
            snapshotPath = Path.Combine(baseDir, "AutoSave");
        }

        bool confirmed = QuitConfirmationDialog.ShowQuitDialog(this, companyName, snapshotPath);
        if (confirmed)
        {
            Application.Exit();
        }
    }

    private void LoadApplicationIcon()
    {
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
            if (File.Exists(iconPath))
            {
                Icon = new Icon(iconPath);
                return;
            }

            if (!string.IsNullOrEmpty(Application.ExecutablePath) && File.Exists(Application.ExecutablePath))
            {
                var extracted = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (extracted != null)
                {
                    Icon = extracted;
                    return;
                }
            }
        }
        catch
        {
            // Graceful fallback
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
