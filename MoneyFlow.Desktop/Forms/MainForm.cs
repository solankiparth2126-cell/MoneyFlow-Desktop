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
using MoneyFlow.Data.Storage;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Executive Ledger Desktop — Gateway of Accounting.
/// Transformed to match MONEYFLOW DESKTOP ERP layout:
/// Top 32px Title Bar, 26px Menu, 40px Action Toolbar, Full-Width Company Banner,
/// 70/30 Split (6 Structured Gateway Cards + 3 Quick Widgets),
/// Horizontal Colored Operations Rail, and Status Bar.
/// </summary>
<<<<<<< Updated upstream
public class MainForm : Form, INavigationHost
=======
public class MainForm : Form, IMainContentHost
>>>>>>> Stashed changes
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
    //  UI CONTROLS & EMBEDDED VIEW STACK
    // ═══════════════════════════════════════════════════════════════
    private readonly Stack<Control> _viewStack = new();
    private Panel pnlGatewayView = null!;

    // Title bar
    private Guna2Panel titleBar = null!;
    private Label lblTitleText = null!;
    private Label lblTitleSeparator = null!;
    private Label lblTitleContext = null!;
    private Guna2Button btnMinimize = null!;
    private Guna2Button btnMaxRestore = null!;
    private Guna2Button btnClose = null!;


    // Toolbar
    private Guna2Panel toolbarPanel = null!;
    private System.Windows.Forms.Timer sessionTimer = null!;
    private DateTime sessionStartTime;

<<<<<<< Updated upstream
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

    // Gateway Header
=======
    // Gateway Header & Navigation
>>>>>>> Stashed changes
    private Panel pnlGatewayHeader = null!;
    private Label lblGwTitle = null!;
    private Label lblGwSub = null!;

    // Gateway 3-column cards
    private TableLayoutPanel cardsGrid = null!;
    private Guna2Panel cardMasters = null!;
    private Guna2Panel cardTrans = null!;
    private Guna2Panel cardReports = null!;

    // Dynamic Footer Action State (for Gateway)
    private List<FooterActionItem> _currentFooterActions = new();
    public IReadOnlyList<FooterActionItem> CurrentFooterActions => _currentFooterActions.AsReadOnly();

    // Central Dynamic Workspace Panel & Gateway Subviews
    private Panel mainContainer = null!; // Dynamic Content Panel
    private Panel pnlGatewayWorkspace = null!;

    // Dynamic Module State
    private Form? _activeChildForm;
    private string _currentModuleKey = "Gateway";
    private Func<Form>? _lastFailedFactory;
    private string? _lastFailedModuleKey;
    private string? _lastFailedModuleTitle;

    // Loading & Error Overlays
    private Panel pnlLoadingOverlay = null!;
    private Label lblLoadingText = null!;
    private Guna2ProgressBar progressLoading = null!;

    private Panel pnlErrorOverlay = null!;
    private Label lblErrorTitle = null!;
    private Label lblErrorMessage = null!;
    private Guna2Button btnRetry = null!;
    private Guna2Button btnErrorBack = null!;

    private readonly Dictionary<string, Guna2Panel> _toolbarActionPanels = new(StringComparer.OrdinalIgnoreCase);

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
    private readonly SystemConfiguration? _systemConfig;

    public MainForm(
        ICompanyContext companyContext,
        IUserContext userContext,
        INavigationService navigationService,
        ISettingsService settingsService,
        IBackupRestoreService? backupRestoreService = null,
        SystemConfiguration? systemConfig = null)
    {
        _companyContext = companyContext ?? throw new ArgumentNullException(nameof(companyContext));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _backupRestoreService = backupRestoreService;
        _systemConfig = systemConfig;

        sessionStartTime = DateTime.Now;

        _navigationService.RegisterHost(this);

        InitializeComponent();

        _navigationService.RegisterHost(this);

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

        // 2. Action Toolbar (40px, Pill Buttons + Quick Search + Exit)
        CreateToolbar();

        // 3. Operations Rail (34px Horizontal Fn Button Bar as per reference design)
        CreateOperationsRail();

        // 5. Main Workspace Layout (Fill)
        CreateGatewayLayout();

        // Add controls and enforce strict docking z-order:
        // TitleBar takes top dock edge.
        // mainContainer (dynamicContentPanel) fills the entire space below the title bar.
        Controls.Add(mainContainer);
        Controls.Add(titleBar);

        titleBar.BringToFront();
        mainContainer.BringToFront();

        // 7. Keyboard Shortcuts
        KeyDown += MainForm_KeyDown;

        // 8. Auto-Open Select Company On Startup inside main section panel
        Shown += (s, e) =>
        {
            ApplyResponsiveLayout();
            if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
            {
                _navigationService.OpenCompanyList(this);
            }
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

    /// <summary>
    /// Centralized guard: requires an active company before accessing Dashboard, Masters, Transactions, Reports, Utilities.
    /// Opens the Company List inside the main workspace if none is active.
    /// </summary>
    public bool RequireActiveCompany()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            _navigationService.OpenCompanyList(this);
            return false;
        }

        UpdateCompanyContextUI();
        return true;
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

        // Separator "|" (hidden — company name removed from main title bar per design)
        lblTitleSeparator = new Label
        {
            Text = "|",
            ForeColor = Color.FromArgb(71, 85, 105),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Visible = false,
            Location = new Point(lblTitleText.Right + 8, 7),
            BackColor = Color.Transparent
        };

        // Company context text (hidden — company name removed from main title bar per design)
        lblTitleContext = new Label
        {
            Text = string.Empty,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            AutoSize = true,
            Visible = false,
            Location = new Point(lblTitleSeparator.Right + 8, 7),
            BackColor = Color.Transparent
        };

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

        // Only minimize and close buttons displayed per user request
        titleBar.Controls.Add(btnMinimize);
        titleBar.Controls.Add(btnClose);

        titleBar.Resize += (s, e) => PositionRightControls();
        PositionRightControls();

        // Title bar drag handlers
        titleBar.MouseDown += TitleBar_MouseDown;
        lblTitleText.MouseDown += TitleBar_MouseDown;
        picBadge.MouseDown += TitleBar_MouseDown;
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
        var itemDayBook = CreateToolbarActionItem(
            ExecLedgerIcons.CreateDayBookIcon(Color.FromArgb(124, 58, 237)),
            "Day Book", null,
            () => _navigationService.OpenDayBook(this));
        flowLeft.Controls.Add(itemDayBook);
        if (itemDayBook is Guna2Panel gpDayBook) _toolbarActionPanels["DayBook"] = gpDayBook;

        // 4. Trial Balance
        var itemTrial = CreateToolbarActionItem(
            ExecLedgerIcons.CreateTrialBalanceIcon(Color.FromArgb(234, 88, 12)),
            "Trial Balance", null,
            () => _navigationService.OpenBalanceSheet(this));
        flowLeft.Controls.Add(itemTrial);
        if (itemTrial is Guna2Panel gpTrial) _toolbarActionPanels["TrialBalance"] = gpTrial;

        // 5. P & L
        var itemPL = CreateToolbarActionItem(
            ExecLedgerIcons.CreateProfitLossIcon(Color.FromArgb(147, 51, 234)),
            "P & L", null,
            () => _navigationService.OpenProfitLoss(this));
        flowLeft.Controls.Add(itemPL);
        if (itemPL is Guna2Panel gpPL) _toolbarActionPanels["ProfitLoss"] = gpPL;

        // 6. Balance Sheet
        var itemBS = CreateToolbarActionItem(
            ExecLedgerIcons.CreateBalanceSheetIcon(Color.FromArgb(13, 148, 136)),
            "Balance Sheet", null,
            () => _navigationService.OpenBalanceSheet(this));
        flowLeft.Controls.Add(itemBS);
        if (itemBS is Guna2Panel gpBS) _toolbarActionPanels["BalanceSheet"] = gpBS;

        // 7. Inline Quick Search Bar on the right
        var pnlQuickSearch = new Guna2Panel
        {
            Height = 28,
            Width = 240,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(203, 213, 225),
            BorderThickness = 1,
            BorderRadius = 4,
            Cursor = Cursors.Hand,
            Margin = new Padding(8, 1, 3, 1)
        };
        var picSearch = new PictureBox
        {
            Image = ExecLedgerIcons.CreateSearchIcon(Color.FromArgb(148, 163, 184)),
            Size = new Size(16, 16),
            SizeMode = PictureBoxSizeMode.CenterImage,
            Location = new Point(8, 6),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        var txtSearchPlaceholder = new Label
        {
            Text = "Jump to ledger / voucher (Ctrl+F)",
            Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            AutoSize = true,
            Location = new Point(28, 6),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        pnlQuickSearch.Controls.Add(picSearch);
        pnlQuickSearch.Controls.Add(txtSearchPlaceholder);
        Action openSearch = () => _navigationService.OpenGlobalSearch(this);
        pnlQuickSearch.Click += (s, e) => openSearch();
        picSearch.Click += (s, e) => openSearch();
        txtSearchPlaceholder.Click += (s, e) => openSearch();

        flowLeft.Controls.Add(pnlQuickSearch);

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

        operationsRail.Resize += (s, e) => LayoutOperationsRail();
        UpdateDynamicFooter("Gateway", null);
    }

    private void UpdateDynamicFooter(string moduleKey, Form? activeForm)
    {
        if (operationsRail == null) return;

        operationsRail.SuspendLayout();
        operationsRail.Controls.Clear();

        List<FooterActionItem> actions;
        if (activeForm is IFooterActionProvider provider)
        {
            actions = provider.GetFooterActions().ToList();
        }
        else
        {
            actions = GetDefaultFooterActions(moduleKey);
        }

        _currentFooterActions = actions;

        foreach (var item in actions)
        {
            var btn = new RailButton(item.KeyText, item.ActionText, item.BadgeColor, item.Action)
            {
                IsActive = item.IsActive
            };
            operationsRail.Controls.Add(btn);
        }

        operationsRail.ResumeLayout(true);
        LayoutOperationsRail();
    }

    private List<FooterActionItem> GetDefaultFooterActions(string moduleKey)
    {
        var list = new List<FooterActionItem>
        {
            new("F1", "Help", Color.FromArgb(217, 119, 6), () => _navigationService.OpenGlobalSearch(this)),
            new("F2", "Date", Color.FromArgb(37, 99, 235), () => _navigationService.OpenFinancialYearList(this)),
            new("F3", "Company", Color.FromArgb(37, 99, 235), () => _navigationService.OpenCompanyList(this)),
            new("F4", "Contra", Color.FromArgb(5, 150, 105), () => _navigationService.OpenContraVoucher(this)),
            new("F5", "Payment", Color.FromArgb(5, 150, 105), () => _navigationService.OpenPaymentVoucher(this)),
            new("F6", "Receipt", Color.FromArgb(13, 148, 136), () => _navigationService.OpenReceiptVoucher(this)),
            new("F7", "Journal", Color.FromArgb(13, 148, 136), () => _navigationService.OpenJournalVoucher(this)),
            new("F8", "Sales", Color.FromArgb(13, 148, 136), () => _navigationService.OpenSalesVoucher(this)),
            new("F9", "Purchase", Color.FromArgb(13, 148, 136), () => _navigationService.OpenPurchaseVoucher(this))
        };
        return list;
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

        public bool IsActive { get; set; }
        public string ActionText => _actionText;
        public string KeyText => _keyText;

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

            // 1. Button Rounded Pill (#1D2C42, hover #2A4160, active #0E7490 cyan accent)
            var rect = new Rectangle(0, 0, Width, Height);
            Color bg = IsActive
                ? Color.FromArgb(14, 116, 144)
                : (_isHovered ? Color.FromArgb(42, 65, 96) : Color.FromArgb(29, 44, 66));

            using (var path = CreateRoundedPath(rect, 4))
            using (var brush = new SolidBrush(bg))
            {
                g.FillPath(brush, path);
            }

            if (IsActive)
            {
                using var activePen = new Pen(Color.FromArgb(56, 189, 248), 1);
                using var borderPath = CreateRoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), 4);
                g.DrawPath(activePen, borderPath);
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
            Padding = Padding.Empty,
            AutoScroll = false
        };
        mainContainer.Resize += (s, e) => ApplyResponsiveLayout();

<<<<<<< Updated upstream
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

        // Left Icon Badge with official MoneyFlow Logo from Resources
        var badgePanel = new Guna2Panel
        {
            Size = new Size(40, 40),
            Location = new Point(16, 18),
            FillColor = Color.FromArgb(15, 23, 42),
            BorderRadius = 6
        };
        var picCompanyLogo = new PictureBox
        {
            Image = ExecLedgerIcons.GetAppLogo(28, 28),
            Size = new Size(28, 28),
            Location = new Point(6, 6),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        badgePanel.Controls.Add(picCompanyLogo);
        pnlCompanyBanner.Controls.Add(badgePanel);

        // Company Name + Active Pill + Books Beginning
        lblBannerCompName = new Label
        {
            Text = string.Empty,
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
            Text = "Accounts • Wholesale & Retail Trading • Base Currency: INR (₹)",
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
=======
        pnlGatewayView = new Panel
>>>>>>> Stashed changes
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(14, 8, 14, 8),
            AutoScroll = false
        };
        pnlGatewayView.Resize += (s, e) => ApplyResponsiveLayout();

<<<<<<< Updated upstream
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
            Text = "10-Sep-2026 (Thursday)",
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
            Visible = true
        };
        pnlCompanyBanner.Visible = true;

        // ── B. GATEWAY OF ACCOUNTING (3 Structured Cards) ──
        // Gateway Header
        pnlGatewayHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.Transparent,
            Padding = new Padding(2, 0, 0, 0)
=======
        // ── A. GATEWAY OF ACCOUNTING HEADER (52px, unclipped, generous top padding) ──
        pnlGatewayHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 52,
            BackColor = Color.Transparent,
            Padding = new Padding(6, 4, 6, 4)
>>>>>>> Stashed changes
        };
        pnlGatewayHeader.Resize += (s, e) => LayoutGatewayHeader();

        lblGwTitle = new Label
        {
            Text = "GATEWAY OF ACCOUNTING",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
<<<<<<< Updated upstream
            Location = new Point(2, 2),
=======
            Location = new Point(4, 4),
>>>>>>> Stashed changes
            AutoSize = true
        };
        lblGwSub = new Label
        {
            Text = "Press highlighted underlined key or click category to open master modules",
            Font = new Font("Segoe UI", 8.25F),
            ForeColor = Color.FromArgb(100, 116, 139),
<<<<<<< Updated upstream
            Location = new Point(2, 24),
=======
            Location = new Point(4, 28),
>>>>>>> Stashed changes
            AutoSize = true
        };
        pnlGatewayHeader.Controls.Add(lblGwTitle);
        pnlGatewayHeader.Controls.Add(lblGwSub);

<<<<<<< Updated upstream
        // 3-Column Grid of 3 Main Cards: MASTERS, TRANSACTIONS, REPORTS (Stretches to 100% available client height)
=======
        // Navigation Tip Pill with key badges
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

        // ── B. 3-Column Grid of 3 Main Cards: MASTERS, TRANSACTIONS, REPORTS (Fills remaining height) ──
>>>>>>> Stashed changes
        cardsGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 4, 0, 4)
        };
        cardsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        cardsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        cardsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        cardsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        cardsGrid.Resize += (s, e) => UpdateCardRowHeights();

        _gatewayItems.Clear();

        // ── Card 1: MASTERS [M] ──
        cardMasters = CreateStructuredCard("MASTERS", "M", Color.FromArgb(37, 99, 235), 0);
<<<<<<< Updated upstream
        AddCardActionRow(cardMasters, 0, "Group", "List & Creation", () => _navigationService.OpenGroupList(this), hotkeyChar: 'G');
        AddCardActionRow(cardMasters, 0, "Ledgers", "Primary", () => _navigationService.OpenLedgerList(this), hotkeyChar: 'L', isHighlighted: true);
=======
        AddCardActionRow(cardMasters, 0, "Groups", "Hierarchy", () => _navigationService.OpenGroupList(this), hotkeyChar: 'G');
        AddCardActionRow(cardMasters, 0, "Ledgers Master", "Primary", () => _navigationService.OpenLedgerList(this), hotkeyChar: 'L', isHighlighted: true);
        AddCardActionRow(cardMasters, 0, "Financial Years", "Period", () => _navigationService.OpenFinancialYearList(this), hotkeyChar: 'F');
        AddCardActionRow(cardMasters, 0, "Alter Company", "Setup", () => _navigationService.OpenAlterCompany(this), hotkeyChar: 'A');
        AddCardActionRow(cardMasters, 0, "Select Company", "Switch", () => _navigationService.OpenCompanyList(this), hotkeyChar: 'S');
        AddCardActionRow(cardMasters, 0, "User Management", "Security", () => _navigationService.OpenUserManagement(this), hotkeyChar: 'U');
>>>>>>> Stashed changes
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
        AddCardActionRow(cardReports, 2, "Bank Reconciliation", "Banking", () => _navigationService.OpenBankReconciliation(this), hotkeyChar: 'R');
        cardsGrid.Controls.Add(cardReports, 2, 0);

        UpdateGatewaySelectionUI();

<<<<<<< Updated upstream
        // 1. Gateway of Accounting Workspace Subpanel (Dynamic Content on Gateway)
        // Contains Company Banner at the top, Spacer, Gateway Header, 3 Cards Grid filling remainder, and Operations Rail at the bottom
        pnlGatewayWorkspace = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };
        var pnlToolbarSpacer = new Panel
        {
            Dock = DockStyle.Top,
            Height = 8,
            BackColor = Color.Transparent
        };

        pnlGatewayWorkspace.Controls.Add(cardsGrid);
        pnlGatewayWorkspace.Controls.Add(pnlGatewayHeader);
        pnlGatewayWorkspace.Controls.Add(pnlBannerSpacer);
        pnlGatewayWorkspace.Controls.Add(pnlCompanyBanner);
        pnlGatewayWorkspace.Controls.Add(pnlToolbarSpacer);
        pnlGatewayWorkspace.Controls.Add(toolbarPanel);
        pnlGatewayWorkspace.Controls.Add(operationsRail);

        operationsRail.Dock = DockStyle.Bottom;
        toolbarPanel.Dock = DockStyle.Top;
        pnlToolbarSpacer.Dock = DockStyle.Top;
        pnlCompanyBanner.Dock = DockStyle.Top;
        pnlBannerSpacer.Dock = DockStyle.Top;
        pnlGatewayHeader.Dock = DockStyle.Top;
        cardsGrid.Dock = DockStyle.Fill;

        cardsGrid.BringToFront();
        pnlGatewayHeader.SendToBack();
        pnlBannerSpacer.SendToBack();
        pnlCompanyBanner.SendToBack();
        pnlToolbarSpacer.SendToBack();
        toolbarPanel.SendToBack();
        operationsRail.SendToBack();

        pnlGatewayWorkspace.Resize += (s, e) =>
        {
            LayoutCompanyBanner();
            LayoutGatewayHeader();
            UpdateCardRowHeights();
            LayoutOperationsRail();
        };

        // 3. Create subtle loading & error overlays inside dynamic content panel
        CreateLoadingOverlay();
        CreateErrorOverlay();

        // 4. Central Dynamic Content Panel holds Gateway and overlays
        mainContainer.Controls.Clear();
        mainContainer.Controls.Add(pnlGatewayWorkspace);
        mainContainer.Controls.Add(pnlLoadingOverlay);
        mainContainer.Controls.Add(pnlErrorOverlay);

        pnlGatewayWorkspace.BringToFront();
=======
        // Dock cardsGrid (Fill) and pnlGatewayHeader (Top) inside pnlGatewayView
        pnlGatewayView.Controls.Add(cardsGrid);
        pnlGatewayView.Controls.Add(pnlGatewayHeader);

        pnlGatewayHeader.BringToFront();
        cardsGrid.BringToFront();

        mainContainer.Controls.Add(pnlGatewayView);
>>>>>>> Stashed changes

        LayoutGatewayHeader();
        UpdateCardRowHeights();
        LayoutOperationsRail();
    }

    // ═══════════════════════════════════════════════════════════════
    //  DYNAMIC WORKSPACE / CONTENT PANEL (INavigationHost Implementation)
    // ═══════════════════════════════════════════════════════════════

    public string CurrentModuleKey => _currentModuleKey;
    public bool IsOnGateway => _currentModuleKey == "Gateway" && _activeChildForm == null;

    public void ShowInWorkspace(Func<Form> formFactory, string moduleKey, string moduleTitle)
    {
        if (InvokeRequired)
        {
            Invoke(new Action(() => ShowInWorkspace(formFactory, moduleKey, moduleTitle)));
            return;
        }

        if (_currentModuleKey == moduleKey && _activeChildForm != null)
        {
            _activeChildForm.Focus();
            return;
        }

        // Show subtle desktop ERP loading indicator inside dynamic workspace
        ShowLoadingOverlay($"Loading {moduleTitle}...");

        try
        {
            // Close and remove previous child form cleanly
            if (_activeChildForm != null)
            {
                var oldForm = _activeChildForm;
                _activeChildForm = null;
                mainContainer.Controls.Remove(oldForm);
                oldForm.Close();
                oldForm.Dispose();
            }

            var childForm = formFactory();

            // Enforce embedded child hosting parameters
            childForm.TopLevel = false;
            childForm.FormBorderStyle = FormBorderStyle.None;
            childForm.Dock = DockStyle.Fill;
            childForm.KeyPreview = true;

            // When child form closes (via ESC or form close), smoothly return to Gateway
            childForm.FormClosed += (s, e) =>
            {
                if (_activeChildForm == childForm)
                {
                    ReturnToGateway();
                }
            };

            // Switch view inside dynamic workspace
            pnlGatewayWorkspace.Visible = false;
            pnlErrorOverlay.Visible = false;
            pnlLoadingOverlay.Visible = false;

            mainContainer.Controls.Add(childForm);
            _activeChildForm = childForm;
            _currentModuleKey = moduleKey;

            childForm.Dock = DockStyle.Fill;
            childForm.BringToFront();
            childForm.Show();
            childForm.Focus();

            UpdateActiveNavigationState(moduleKey);
        }
        catch (Exception ex)
        {
            _lastFailedFactory = formFactory;
            _lastFailedModuleKey = moduleKey;
            _lastFailedModuleTitle = moduleTitle;
            ShowErrorOverlay(moduleTitle, ex.Message);
        }
    }

    public void ReturnToGateway()
    {
        if (InvokeRequired)
        {
            Invoke(new Action(ReturnToGateway));
            return;
        }

        if (_activeChildForm != null)
        {
            var form = _activeChildForm;
            _activeChildForm = null;
            mainContainer.Controls.Remove(form);
            form.Close();
            form.Dispose();
        }

        _currentModuleKey = "Gateway";
        pnlLoadingOverlay.Visible = false;
        pnlErrorOverlay.Visible = false;
        pnlGatewayWorkspace.Visible = true;
        pnlGatewayWorkspace.BringToFront();

        UpdateActiveNavigationState("Gateway");
        UpdateGatewaySelectionUI();
        cardsGrid?.Focus();
    }

    public bool NavigateBack()
    {
        if (_activeChildForm is IBackNavigable backNavigable && backNavigable.HandleBackNavigation())
        {
            return true;
        }

        if (_activeChildForm != null)
        {
            return MoneyFlowEscController.HandleEsc(
                _activeChildForm.ActiveControl,
                _activeChildForm,
                closeAction: () =>
                {
                    ReturnToGateway();
                });
        }
        return false;
    }

    private void CreateLoadingOverlay()
    {
        pnlLoadingOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(241, 245, 249),
            Visible = false
        };

        var centerCard = new Guna2Panel
        {
            Size = new Size(320, 110),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(226, 232, 240),
            BorderThickness = 1,
            BorderRadius = 6
        };

        lblLoadingText = new Label
        {
            Text = "Loading module...",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(24, 26),
            AutoSize = true
        };

        progressLoading = new Guna2ProgressBar
        {
            Location = new Point(24, 60),
            Size = new Size(272, 6),
            Style = ProgressBarStyle.Marquee,
            ProgressColor = Color.FromArgb(37, 99, 235),
            ProgressColor2 = Color.FromArgb(59, 130, 246),
            BorderRadius = 3
        };

        centerCard.Controls.Add(lblLoadingText);
        centerCard.Controls.Add(progressLoading);

        pnlLoadingOverlay.Controls.Add(centerCard);
        pnlLoadingOverlay.Resize += (s, e) =>
        {
            centerCard.Location = new Point(
                Math.Max(10, (pnlLoadingOverlay.Width - centerCard.Width) / 2),
                Math.Max(10, (pnlLoadingOverlay.Height - centerCard.Height) / 2));
        };
    }

    private void ShowLoadingOverlay(string text)
    {
        lblLoadingText.Text = text;
        pnlLoadingOverlay.Visible = true;
        pnlLoadingOverlay.BringToFront();
        Application.DoEvents();
    }

    private void CreateErrorOverlay()
    {
        pnlErrorOverlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(241, 245, 249),
            Visible = false
        };

        var card = new Guna2Panel
        {
            Size = new Size(440, 180),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(254, 202, 202),
            BorderThickness = 1,
            BorderRadius = 6
        };

        lblErrorTitle = new Label
        {
            Text = "Unable to load module",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(185, 28, 28),
            Location = new Point(24, 20),
            AutoSize = true
        };

        lblErrorMessage = new Label
        {
            Text = "An unexpected error occurred while loading this module.",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(24, 52),
            Size = new Size(392, 50),
            AutoEllipsis = true
        };

        btnRetry = new Guna2Button
        {
            Text = "Retry",
            Size = new Size(90, 32),
            Location = new Point(24, 122),
            FillColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White,
            BorderRadius = 4,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnRetry.Click += (s, e) =>
        {
            if (_lastFailedFactory != null && _lastFailedModuleKey != null && _lastFailedModuleTitle != null)
            {
                ShowInWorkspace(_lastFailedFactory, _lastFailedModuleKey, _lastFailedModuleTitle);
            }
        };

        btnErrorBack = new Guna2Button
        {
            Text = "Back to Gateway",
            Size = new Size(130, 32),
            Location = new Point(122, 122),
            FillColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(71, 85, 105),
            BorderColor = Color.FromArgb(203, 213, 225),
            BorderThickness = 1,
            BorderRadius = 4,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnErrorBack.Click += (s, e) => ReturnToGateway();

        card.Controls.Add(lblErrorTitle);
        card.Controls.Add(lblErrorMessage);
        card.Controls.Add(btnRetry);
        card.Controls.Add(btnErrorBack);

        pnlErrorOverlay.Controls.Add(card);
        pnlErrorOverlay.Resize += (s, e) =>
        {
            card.Location = new Point(
                Math.Max(10, (pnlErrorOverlay.Width - card.Width) / 2),
                Math.Max(10, (pnlErrorOverlay.Height - card.Height) / 2));
        };
    }

    private void ShowErrorOverlay(string moduleTitle, string errorDetails)
    {
        pnlLoadingOverlay.Visible = false;
        lblErrorTitle.Text = $"Unable to load {moduleTitle}";
        lblErrorMessage.Text = string.IsNullOrWhiteSpace(errorDetails) ? "Unable to load module." : errorDetails;
        pnlErrorOverlay.Visible = true;
        pnlErrorOverlay.BringToFront();
    }

    private void UpdateActiveNavigationState(string moduleKey)
    {
        if (operationsRail != null)
        {
            foreach (Control c in operationsRail.Controls)
            {
                if (c is RailButton rb)
                {
                    rb.IsActive = !string.Equals(moduleKey, "Gateway", StringComparison.OrdinalIgnoreCase) &&
                        (rb.ActionText.Equals(moduleKey, StringComparison.OrdinalIgnoreCase)
                         || (moduleKey == "Payment" && rb.ActionText == "Payment")
                         || (moduleKey == "Receipt" && rb.ActionText == "Receipt")
                         || (moduleKey == "Contra" && rb.ActionText == "Contra")
                         || (moduleKey == "Journal" && rb.ActionText == "Journal")
                         || (moduleKey == "Sales" && rb.ActionText == "Sales")
                         || (moduleKey == "Purchase" && rb.ActionText == "Purchase"));
                    rb.Invalidate();
                }
            }
        }

        foreach (var kvp in _toolbarActionPanels)
        {
            bool isActive = !string.Equals(moduleKey, "Gateway", StringComparison.OrdinalIgnoreCase) &&
                            kvp.Key.Equals(moduleKey, StringComparison.OrdinalIgnoreCase);
            kvp.Value.FillColor = isActive ? Color.FromArgb(224, 231, 255) : Color.White;
            kvp.Value.BorderColor = isActive ? Color.FromArgb(99, 102, 241) : Color.FromArgb(203, 213, 225);
            kvp.Value.Invalidate();
        }
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
            Margin = new Padding(6, 4, 6, 6),
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
            Padding = new Padding(10, 6, 10, 10),
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

        var moduleDeck = CreateModuleDeck(colIndex);

        pnlContent.Controls.Add(moduleDeck);
        pnlContent.Controls.Add(tblRows);

        card.Controls.Add(pnlContent);
        card.Controls.Add(header);
        card.Tag = tblRows;
        return card;
    }

    private Control CreateModuleDeck(int colIndex)
    {
        var deck = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            FillColor = Color.FromArgb(248, 250, 252),
            BorderColor = Color.FromArgb(226, 232, 240),
            BorderThickness = 1,
            BorderRadius = 6,
            Margin = new Padding(0, 10, 0, 0),
            Padding = new Padding(12, 8, 12, 8)
        };

        string deckHeader;
        Color accentColor;
        (string Key, string Desc)[] items;

        if (colIndex == 0) // Masters
        {
            deckHeader = "CHART OF ACCOUNTS & SETUP";
            accentColor = Color.FromArgb(37, 99, 235);
            items = new[]
            {
                ("Alt+C", "Quick create Master in any voucher"),
                ("Hierarchy", "28 Groups, Primary & Sub-groups"),
                ("Security", "User role permissions & audit trail"),
                ("Multi-Co", "Company switch & FY period support")
            };
        }
        else if (colIndex == 1) // Transactions
        {
            deckHeader = "TRANSACTION REGISTRY & VOUCHERS";
            accentColor = Color.FromArgb(16, 185, 129);
            items = new[]
            {
                ("Double Entry", "Strict debit / credit validation"),
                ("Quick Keys", "F4 Contra, F5 Payment, F6 Receipt"),
                ("Registers", "F7 Journal, F8 Sales, F9 Purchase"),
                ("Integrity", "Sequential numbering & audit log")
            };
        }
        else // Reports
        {
            deckHeader = "FINANCIAL INTELLIGENCE & AUDIT";
            accentColor = Color.FromArgb(245, 158, 11);
            items = new[]
            {
                ("Real-time", "Instant Balance Sheet & Profit & Loss"),
                ("Drill-Down", "Direct navigation from ledger to vouchers"),
                ("Banking", "Bank reconciliation & statement sync"),
                ("Compliance", "Trial balance verified to Ind AS")
            };
        }

        var lblHeader = new Label
        {
            Text = deckHeader,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = accentColor,
            Dock = DockStyle.Top,
            Height = 22,
            BackColor = Color.Transparent
        };
        deck.Controls.Add(lblHeader);

        var pnlList = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            AutoScroll = false,
            Padding = new Padding(0, 2, 0, 0)
        };

        int curY = 4;
        foreach (var (key, desc) in items)
        {
            var rowPnl = new Panel
            {
                Location = new Point(0, curY),
                Size = new Size(deck.Width - 24, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.Transparent
            };

            var badge = new Guna2Panel
            {
                Size = new Size(74, 18),
                Location = new Point(0, 2),
                FillColor = Color.White,
                BorderColor = Color.FromArgb(203, 213, 225),
                BorderThickness = 1,
                BorderRadius = 3
            };
            var lblKey = new Label
            {
                Text = key,
                Font = new Font("Segoe UI", 7F, FontStyle.Bold),
                ForeColor = Color.FromArgb(51, 65, 85),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            badge.Controls.Add(lblKey);

            var lblDesc = new Label
            {
                Text = desc,
                Font = new Font("Segoe UI", 7.75F),
                ForeColor = Color.FromArgb(71, 85, 105),
                Location = new Point(80, 2),
                Size = new Size(Math.Max(10, rowPnl.Width - 84), 18),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                AutoEllipsis = true,
                BackColor = Color.Transparent
            };

            rowPnl.Controls.Add(badge);
            rowPnl.Controls.Add(lblDesc);
            pnlList.Controls.Add(rowPnl);
            curY += 24;
        }

        deck.Controls.Add(pnlList);
        pnlList.BringToFront();
        return deck;
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

            int keyIndex = hotkeyChar.HasValue ? title.IndexOf(hotkeyChar.Value.ToString(), StringComparison.OrdinalIgnoreCase) : -1;
            if (keyIndex >= 0)
            {
                string preStr = title.Substring(0, keyIndex);
                string keyStr = title.Substring(keyIndex, 1);
                string postStr = title.Substring(keyIndex + 1);

                using var normFont = new Font(ExecLedgerTheme.UiFontFamily, 9F, FontStyle.Regular);
                using var keyFont = new Font(ExecLedgerTheme.UiFontFamily, 9F, FontStyle.Bold | FontStyle.Underline);

                int curX = startX;
                if (!string.IsNullOrEmpty(preStr))
                {
                    var preSize = TextRenderer.MeasureText(e.Graphics, preStr, normFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                    TextRenderer.DrawText(e.Graphics, preStr, normFont, new Point(curX, startY), textColor, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                    curX += preSize.Width;
                }

                var keySize = TextRenderer.MeasureText(e.Graphics, keyStr, keyFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                TextRenderer.DrawText(e.Graphics, keyStr, keyFont, new Point(curX, startY), textColor, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                curX += keySize.Width;

                if (!string.IsNullOrEmpty(postStr))
                {
                    TextRenderer.DrawText(e.Graphics, postStr, normFont, new Point(curX, startY), textColor, TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                }
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
                int pillW = Math.Max(54, TextRenderer.MeasureText(rightTag, lblPill.Font).Width + 14);
                var pill = new Guna2Panel
                {
                    Size = new Size(pillW, 22),
                    FillColor = Color.FromArgb(219, 234, 254),
                    BorderRadius = 4,
                    Cursor = Cursors.Hand
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
                int pillW = Math.Max(56, TextRenderer.MeasureText(rightTag, lblPill.Font).Width + 14);
                var pill = new Guna2Panel
                {
                    Size = new Size(pillW, 22),
                    FillColor = Color.FromArgb(254, 243, 199),
                    BorderRadius = 4,
                    Cursor = Cursors.Hand
                };
                pill.Controls.Add(lblPill);
                row.Controls.Add(pill);
                rightBadgeControl = pill;
                pill.Click += (s, e) => { SetGatewaySelection(colIndex, rowIndex); click(); };
                lblPill.Click += (s, e) => { SetGatewaySelection(colIndex, rowIndex); click(); };
            }
            else if (isKeyBadge)
            {
                // Key badge (e.g. F5, F6, Ctrl+F9, etc.)
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
                int badgeW = Math.Max(32, TextRenderer.MeasureText(rightTag, lblKey.Font).Width + 12);
                var keyBox = new Guna2Panel
                {
                    Size = new Size(badgeW, 22),
                    FillColor = Color.FromArgb(248, 250, 252),
                    BorderColor = Color.FromArgb(203, 213, 225),
                    BorderThickness = 1,
                    BorderRadius = 4,
                    Cursor = Cursors.Hand
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
                    Size = new Size(95, 20),
                    TextAlign = ContentAlignment.MiddleRight,
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
            if (RequireActiveCompany()) click();
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
        if (!RequireActiveCompany()) return;
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
        LayoutGatewayHeader();
        UpdateCardRowHeights();
        LayoutOperationsRail();
    }

    private void AdaptTierDimensions()
    {
<<<<<<< Updated upstream
        if (mainContainer == null || pnlCompanyBanner == null || pnlBannerSpacer == null || pnlGatewayHeader == null || pnlGatewayWorkspace == null) return;

        mainContainer.Padding = Padding.Empty;
=======
        if (mainContainer == null || pnlGatewayHeader == null) return;
>>>>>>> Stashed changes

        var tier = ScreenFittingManager.ClassifyTier(mainContainer.ClientSize.Width, mainContainer.ClientSize.Height);

        switch (tier)
        {
            case LayoutTier.Compact:
<<<<<<< Updated upstream
                pnlCompanyBanner.Height = 68;
                pnlBannerSpacer.Height = 8;
                pnlGatewayHeader.Height = 38;
                pnlGatewayWorkspace.Padding = new Padding(12, 4, 12, 6);
                break;

            case LayoutTier.Standard:
                pnlCompanyBanner.Height = 74;
                pnlBannerSpacer.Height = 10;
                pnlGatewayHeader.Height = 42;
                pnlGatewayWorkspace.Padding = new Padding(14, 4, 14, 8);
=======
                pnlGatewayHeader.Height = 44;
                if (pnlGatewayView != null)
                {
                    pnlGatewayView.Padding = new Padding(10, 6, 10, 6);
                }
                break;

            case LayoutTier.Standard:
                pnlGatewayHeader.Height = 48;
                if (pnlGatewayView != null)
                {
                    pnlGatewayView.Padding = new Padding(12, 8, 12, 8);
                }
>>>>>>> Stashed changes
                break;

            case LayoutTier.Large:
            default:
<<<<<<< Updated upstream
                pnlCompanyBanner.Height = 76;
                pnlBannerSpacer.Height = 12;
                pnlGatewayHeader.Height = 44;
                pnlGatewayWorkspace.Padding = new Padding(16, 4, 16, 10);
=======
                pnlGatewayHeader.Height = 52;
                if (pnlGatewayView != null)
                {
                    pnlGatewayView.Padding = new Padding(16, 10, 16, 10);
                }
>>>>>>> Stashed changes
                break;
        }
    }

    private void PositionRightControls()
    {
        if (titleBar == null || btnClose == null || btnMinimize == null) return;
        int btnW = 46;
        btnClose.Location = new Point(titleBar.Width - btnW, 0);
        btnMinimize.Location = new Point(titleBar.Width - btnW * 2, 0);
        if (btnMaxRestore != null)
        {
            btnMaxRestore.Visible = false;
        }
    }

    private void LayoutGatewayHeader()
    {
<<<<<<< Updated upstream
        if (pnlGatewayHeader == null || lblGwTitle == null || lblGwSub == null) return;
        lblGwSub.Text = "Press highlighted underlined key or click category to open master modules";
        lblGwSub.Visible = true;
=======
        if (pnlGatewayHeader == null || pnlNavTip == null || lblGwTitle == null || lblGwSub == null) return;

        int headerW = pnlGatewayHeader.Width;
        lblGwTitle.Location = new Point(4, 2);
        lblGwSub.Location = new Point(4, 26);

        if (headerW >= 760)
        {
            pnlNavTip.Visible = true;
            pnlNavTip.Location = new Point(headerW - pnlNavTip.Width - 4, 8);
        }
        else
        {
            pnlNavTip.Visible = false;
        }
>>>>>>> Stashed changes
    }

    private void UpdateCardRowHeights()
    {
        if (cardsGrid == null || mainContainer == null) return;

        var tier = ScreenFittingManager.ClassifyTier(mainContainer.ClientSize.Width, mainContainer.ClientSize.Height);
        float targetRowH = tier switch
        {
            LayoutTier.Compact => 38f,
            LayoutTier.Large => 44f,
            _ => 42f
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
<<<<<<< Updated upstream
        if (pnlCompanyBanner != null) pnlCompanyBanner.Visible = true;
        if (pnlBannerSpacer != null) pnlBannerSpacer.Visible = true;
=======
        if (InvokeRequired)
        {
            BeginInvoke(UpdateCompanyContextUI);
            return;
        }
>>>>>>> Stashed changes

        if (_companyContext.IsCompanyOpen && _companyContext.CurrentCompany != null)
        {
            var company = _companyContext.CurrentCompany;
            var fy = _companyContext.CurrentFinancialYear;

<<<<<<< Updated upstream
            if (lblBannerCompName != null) lblBannerCompName.Text = company.CompanyName;
            if (fy != null)
            {
                _booksBeginningDateStr = fy.StartDate.ToString("dd-MMM-yyyy");
            }
            if (lblBannerBooksBeginning != null)
                lblBannerBooksBeginning.Text = $"|   Books Beginning: {_booksBeginningDateStr}";
            if (lblBannerCompSubtitle != null)
            {
                string curr = !string.IsNullOrWhiteSpace(company.Currency) ? company.Currency : "INR (₹)";
                string stateStr = !string.IsNullOrWhiteSpace(company.State) ? $" • State: {company.State}" : "";
                lblBannerCompSubtitle.Text = $"Accounts{stateStr} • Base Currency: {curr}";
            }
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
            if (lblStatusFY != null) lblStatusFY.Text = fy != null ? $"FY: {fy.YearName}" : "FY: 2026-27";

            // Company name permanently removed from main title bar per design
            if (lblTitleSeparator != null)
=======
            string fyRange = fy != null 
                ? $"{fy.StartDate:dd-MMM-yyyy} to {fy.EndDate:dd-MMM-yyyy}" 
                : "01-Apr-2026 to 31-Mar-2027";

            if (lblStatusCompany != null) lblStatusCompany.Text = $"● Company: {company.CompanyName} ({company.CompanyNumber})";
            if (lblStatusFY != null) lblStatusFY.Text = fy != null ? $"FY: {fy.YearName}" : "FY: Not set";
            if (lblTitleSeparator != null && lblTitleText != null)
>>>>>>> Stashed changes
            {
                lblTitleSeparator.Visible = false;
                lblTitleSeparator.Text = string.Empty;
            }
            if (lblTitleContext != null)
            {
<<<<<<< Updated upstream
                lblTitleContext.Visible = false;
                lblTitleContext.Text = string.Empty;
=======
                lblTitleContext.Text = $"{company.CompanyName} • Entity: {company.CompanyNumber} • FY: {fyRange}";
                lblTitleContext.Location = new Point(lblTitleSeparator.Right + 8, 7);
>>>>>>> Stashed changes
            }

            if (lblGwSub != null)
            {
                lblGwSub.Text = $"Active Company: {company.CompanyName} ({company.CompanyNumber})   |   FY: {fyRange}   |   Press highlighted key or category to open";
            }

            PositionRightControls();
        }
        else
        {
<<<<<<< Updated upstream
            if (lblBannerCompName != null) lblBannerCompName.Text = "No Company Selected";
            if (lblBannerBooksBeginning != null) lblBannerBooksBeginning.Text = string.Empty;
            if (lblBannerCompSubtitle != null) lblBannerCompSubtitle.Text = "Accounts";
            if (lblBannerFY != null) lblBannerFY.Text = "--";
            if (lblBannerDate != null) lblBannerDate.Text = DateTime.Today.ToString("dd-MMM-yyyy (dddd)");

            if (lblStatusCompany != null) lblStatusCompany.Text = "● No Company Selected";
            if (lblStatusFY != null) lblStatusFY.Text = string.Empty;
            if (lblTitleSeparator != null)
            {
                lblTitleSeparator.Visible = false;
                lblTitleSeparator.Text = string.Empty;
            }
            if (lblTitleContext != null)
            {
                lblTitleContext.Visible = false;
                lblTitleContext.Text = string.Empty;
            }
            LayoutCompanyBanner();
=======
            if (lblStatusCompany != null) lblStatusCompany.Text = "● Company: [None Selected]";
            if (lblStatusFY != null) lblStatusFY.Text = "FY: Not Selected";
            if (lblTitleSeparator != null) lblTitleSeparator.Visible = false;
            if (lblTitleContext != null) lblTitleContext.Text = "";

            if (lblGwSub != null)
            {
                lblGwSub.Text = "No Company Selected   |   Press [F3] to Select Company or [Alt+C] to Create Company";
            }

>>>>>>> Stashed changes
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
    //  IMainContentHost IMPLEMENTATION (Tally-Style Embedded Views)
    // ═══════════════════════════════════════════════════════════════

    public bool HasActiveChildView => _viewStack.Count > 0;

    public void ShowView(Form viewForm)
    {
        if (viewForm == null || IsDisposed) return;

        SuspendLayout();
        mainContainer.SuspendLayout();

        // 1. Configure child form as borderless embedded view
        viewForm.TopLevel = false;
        viewForm.FormBorderStyle = FormBorderStyle.None;
        viewForm.Dock = DockStyle.Fill;

        // 2. Hide currently active view (top of stack, or gateway view)
        if (_viewStack.Count > 0)
        {
            _viewStack.Peek().Visible = false;
        }
        else if (pnlGatewayView != null)
        {
            pnlGatewayView.Visible = false;
        }

        // 3. Push onto stack and add to main container
        _viewStack.Push(viewForm);
        mainContainer.Controls.Add(viewForm);

        // 4. Hook closed event to pop view and restore previous
        viewForm.FormClosed += (s, e) =>
        {
            PopView(viewForm);
        };

        mainContainer.ResumeLayout(true);
        ResumeLayout(true);

        viewForm.Visible = true;
        viewForm.BringToFront();
        viewForm.Focus();
    }

    public void CloseCurrentView()
    {
        if (_viewStack.Count > 0)
        {
            var current = _viewStack.Peek();
            if (current is Form f)
            {
                f.Close();
            }
            else
            {
                PopView(current);
            }
        }
    }

    public void ReturnToGateway()
    {
        while (_viewStack.Count > 0)
        {
            var view = _viewStack.Pop();
            if (view is Form f)
            {
                f.Close();
                f.Dispose();
            }
            if (mainContainer.Controls.Contains(view))
            {
                mainContainer.Controls.Remove(view);
            }
        }

        if (pnlGatewayView != null)
        {
            pnlGatewayView.Visible = true;
            pnlGatewayView.BringToFront();
            UpdateCompanyContextUI();
            pnlGatewayView.Focus();
        }
    }

    private void PopView(Control view)
    {
        if (IsDisposed) return;

        SuspendLayout();
        mainContainer.SuspendLayout();

        if (_viewStack.Contains(view))
        {
            if (_viewStack.Count > 0 && _viewStack.Peek() == view)
            {
                _viewStack.Pop();
            }
            else
            {
                var remaining = _viewStack.Where(v => v != view).Reverse().ToList();
                _viewStack.Clear();
                foreach (var v in remaining) _viewStack.Push(v);
            }
        }

        if (mainContainer.Controls.Contains(view))
        {
            mainContainer.Controls.Remove(view);
        }

        if (_viewStack.Count > 0)
        {
            var top = _viewStack.Peek();
            top.Visible = true;
            top.BringToFront();
            top.Focus();
        }
        else if (pnlGatewayView != null)
        {
            pnlGatewayView.Visible = true;
            pnlGatewayView.BringToFront();
            UpdateCompanyContextUI();
            pnlGatewayView.Focus();
        }

        mainContainer.ResumeLayout(true);
        ResumeLayout(true);
    }

    // ═══════════════════════════════════════════════════════════════
    //  KEYBOARD SHORTCUTS
    // ═══════════════════════════════════════════════════════════════

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        // When an embedded child view is active in the main container, let it handle its own keys
        if (HasActiveChildView) return;

        if ((e.Alt && e.KeyCode == Keys.G) || (e.Control && e.KeyCode == Keys.K) || (e.Control && e.KeyCode == Keys.F))
        {
            _navigationService.OpenGlobalSearch(this);
            e.Handled = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.F9)
        {
            _navigationService.OpenJournalVoucher(this);
            e.Handled = true;
            return;
        }

        // Single hotkeys for dashboard navigation (Tally style) - only when on Gateway
        if (IsOnGateway && !e.Control && !e.Alt && ActiveControl is not TextBox and not Guna2TextBox)
        {
            switch (e.KeyCode)
            {
                case Keys.G: _navigationService.OpenGroupList(this); e.Handled = true; return;
                case Keys.L: _navigationService.OpenLedgerList(this); e.Handled = true; return;
                case Keys.D: _navigationService.OpenDayBook(this); e.Handled = true; return;
                case Keys.P: _navigationService.OpenProfitLoss(this); e.Handled = true; return;
                case Keys.B: _navigationService.OpenBalanceSheet(this); e.Handled = true; return;
<<<<<<< Updated upstream
                case Keys.C: _navigationService.OpenCashBankBook(this); e.Handled = true; return;
=======
                case Keys.P: _navigationService.OpenProfitLoss(this); e.Handled = true; return;
                case Keys.C: _navigationService.OpenCashBankBook(this); e.Handled = true; return;
                case Keys.A: _navigationService.OpenAlterCompany(this); e.Handled = true; return;
                case Keys.S: _navigationService.OpenCompanyList(this); e.Handled = true; return;
                case Keys.U: _navigationService.OpenUserManagement(this); e.Handled = true; return;
>>>>>>> Stashed changes
            }
        }

        switch (e.KeyCode)
        {
            case Keys.Escape:
                if (!IsOnGateway)
                {
                    NavigateBack();
                    e.Handled = true;
                    return;
                }
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
<<<<<<< Updated upstream
        if (keyData == Keys.Escape && !IsOnGateway)
        {
            NavigateBack();
            return true;
=======
        // When an embedded child view is active in the main container, do not intercept any keys!
        if (HasActiveChildView)
        {
            return base.ProcessCmdKey(ref msg, keyData);
>>>>>>> Stashed changes
        }

        // Don't intercept when user is typing in a text field
        if (ActiveControl is TextBox or Guna2TextBox or ComboBox or RichTextBox)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        if (IsOnGateway)
        {
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

    private bool _isExiting = false;



    private void PromptExitApplication()
    {
        string? companyName = _companyContext.CurrentCompany?.CompanyName;
        string? snapshotPath = null;
        if (_companyContext.CurrentCompany != null)
        {
            string baseDir = !string.IsNullOrWhiteSpace(_companyContext.CurrentCompany.DataDirectory)
                ? _companyContext.CurrentCompany.DataDirectory
                : (_systemConfig?.CompanyDataPath ?? SystemEnvironmentManager.GetDefaultCompanyDataPath());
            snapshotPath = Path.Combine(baseDir, "AutoSave");
        }

        bool confirmed = QuitConfirmationDialog.ShowQuitDialog(this, companyName, snapshotPath);
        if (confirmed)
        {
            _isExiting = true;
            Application.Exit();
        }
    }

    private void LoadApplicationIcon()
    {
        try
        {
            var appIcon = ExecLedgerIcons.GetAppIcon();
            if (appIcon != null)
            {
                Icon = appIcon;
            }
        }
        catch
        {
            // Graceful fallback
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  FORM CLOSING (quit confirmation, auto-backup & timer cleanup)
    // ═══════════════════════════════════════════════════════════════

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        if (_isExiting || Disposing || IsDisposed)
        {
            sessionTimer?.Stop();
            sessionTimer?.Dispose();
            base.OnFormClosing(e);
            return;
        }

        if (e.CloseReason == CloseReason.ApplicationExitCall ||
            e.CloseReason == CloseReason.WindowsShutDown ||
            e.CloseReason == CloseReason.TaskManagerClosing)
        {
            sessionTimer?.Stop();
            sessionTimer?.Dispose();
            base.OnFormClosing(e);
            return;
        }

        string? companyName = _companyContext.CurrentCompany?.CompanyName;
        string? snapshotPath = null;
        if (_companyContext.CurrentCompany != null)
        {
            string baseDir = !string.IsNullOrWhiteSpace(_companyContext.CurrentCompany.DataDirectory)
                ? _companyContext.CurrentCompany.DataDirectory
                : (_systemConfig?.CompanyDataPath ?? SystemEnvironmentManager.GetDefaultCompanyDataPath());
            snapshotPath = Path.Combine(baseDir, "AutoSave");
        }

        bool confirmed = QuitConfirmationDialog.ShowQuitDialog(this, companyName, snapshotPath);
        if (!confirmed)
        {
            e.Cancel = true;
            return;
        }

        _isExiting = true;
        _navigationService.UnregisterHost(this);
        sessionTimer?.Stop();
        sessionTimer?.Dispose();
        base.OnFormClosing(e);

        if (_companyContext.CurrentCompany != null && _companyContext.CurrentCompany.AutoBackupOnExit && _backupRestoreService != null)
        {
            try
            {
                var comp = _companyContext.CurrentCompany;
                string baseDir = !string.IsNullOrWhiteSpace(comp.DataDirectory) 
                    ? comp.DataDirectory 
                    : (_systemConfig?.CompanyDataPath ?? SystemEnvironmentManager.GetDefaultCompanyDataPath());
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
