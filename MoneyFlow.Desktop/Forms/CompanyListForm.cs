using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data.Storage;
using MoneyFlow.Desktop.Dialogs;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Select Company Screen.
/// Clean, high-density desktop ERP company selection dialog matching exact reference design:
/// - Dark Navy title bar with rocket badge and window controls
/// - Compact Data Path bar with directory path, Select from Drive, and Network buttons
/// - Company Search header with dynamic match count pill
/// - Full-width search box with Ctrl+F badge
/// - Two-column main content: Quick Action Directory (Left) and Company Table (Right)
/// - Streamlined bottom action bar with Entities Loaded, Active Books, Cancel, and Open Company
/// - Completely zero hard-coded company or test data; completely removed technical/security panels
/// </summary>
public class CompanyListForm : Form
{
    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int leftWidth;
        public int rightWidth;
        public int topHeight;
        public int bottomHeight;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_DROPSHADOW = 0x00020000;
            CreateParams cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);

        // Windows 11 rounded corners (DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2)
        int cornerPreference = 2;
        try { DwmSetWindowAttribute(Handle, 33, ref cornerPreference, sizeof(int)); } catch { }

        // DWM drop shadow for borderless window
        var margins = new MARGINS { leftWidth = 1, rightWidth = 1, topHeight = 1, bottomHeight = 1 };
        try { DwmExtendFrameIntoClientArea(Handle, ref margins); } catch { }
    }

    private readonly ICompanyService _companyService;
    private readonly ICompanyContext _companyContext;
    private readonly ICompanySplitService? _companySplitService;
    private readonly IBackupRestoreService? _backupRestoreService;

    // Structural Panels
    private Panel pnlTitleBar = null!;
    private Panel pnlDataPath = null!;
    private Panel pnlSearch = null!;
    private Panel pnlMainContent = null!;
    private Panel pnlBottomBar = null!;

    // Title Bar Controls
    private Label lblTitle = null!;
    private Label lblTitleSeparator = null!;
    private Label lblSubtitle = null!;
    private Guna2Button btnCloseHeader = null!;

    // Data Path Controls
    private Label lblDataPathTitle = null!;
    private Guna2TextBox txtDataPath = null!;
    private Guna2Button btnSelectDrive = null!;

    // Search Controls
    private Label lblSearchPrompt = null!;
    private Label lblSearchHint = null!;
    private Label lblMatchCount = null!;
    private Guna2TextBox txtSearch = null!;
    private Guna2Button lblCtrlFBadge = null!;
    private Guna2Button btnClearSearch = null!;

    // Left Directory Card Controls
    private Guna2Panel pnlLeftCard = null!;

    // Right Company Grid Card Controls
    private Panel pnlRightCardWrapper = null!;
    private Guna2Panel pnlRightCard = null!;
    private Panel pnlGridHeader = null!;
    private DataGridView gridCompanies = null!;
    private Panel pnlEmptyState = null!;

    // Bottom Action Bar Controls
    private Guna2Button btnCancel = null!;

    // State
    private string _currentDataPath = string.Empty;
    private SystemConfiguration? _systemConfig;
    private List<CompanySummaryDto> _allCompanies = new();
    private List<CompanyGridRowItem> _gridRows = new();

    public bool CompanySelected { get; private set; }
    private bool _isExiting;

    public CompanyListForm(
        ICompanyService companyService,
        ICompanyContext companyContext,
        ICompanySplitService? companySplitService = null,
        IBackupRestoreService? backupRestoreService = null,
        SystemConfiguration? systemConfig = null)
    {
        _companyService = companyService ?? throw new ArgumentNullException(nameof(companyService));
        _companyContext = companyContext ?? throw new ArgumentNullException(nameof(companyContext));
        _companySplitService = companySplitService;
        _backupRestoreService = backupRestoreService;
        _systemConfig = systemConfig;

        // Resolve data path dynamically from config/environment without hardcoding
        if (systemConfig != null && !string.IsNullOrWhiteSpace(systemConfig.CompanyDataPath))
        {
            _currentDataPath = Path.Combine(systemConfig.CompanyDataPath, "Companies");
        }
        else
        {
            _currentDataPath = Path.Combine(SystemEnvironmentManager.GetDefaultCompanyDataPath(), "Companies");
        }

        if (!_currentDataPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            _currentDataPath += Path.DirectorySeparatorChar;
        }

        if (!Directory.Exists(_currentDataPath))
        {
            try { Directory.CreateDirectory(_currentDataPath); } catch { }
        }

        InitializeComponent();
        LoadCompaniesAsync();
    }

    private void InitializeComponent()
    {
        Text = "Select Company";
        Size = new Size(1100, 660);
        MinimumSize = new Size(980, 560);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(246, 250, 254); // Clean ERP background #F6FAFE
        KeyPreview = true;
        DoubleBuffered = true;

        var appIcon = ExecLedgerIcons.GetAppIcon();
        if (appIcon != null)
        {
            Icon = appIcon;
            ShowIcon = true;
        }

        // Soft, diffused professional window shadow
        _ = new Guna2ShadowForm
        {
            TargetForm = this,
            ShadowColor = Color.FromArgb(15, 23, 42)
        };

        // 1. Build Top Title Bar (60px Deep Navy)
        BuildTitleBarSection();

        // 2. Build Data Path Bar (56px, Network button completely removed)
        BuildDataPathSection();

        // 3. Build Company Search Section (82px)
        BuildSearchSection();

        // 4. Build Bottom Action Bar (62px, lightweight on window background)
        BuildBottomBarSection();

        // 5. Build Main Content (Left Directory Card 28% + Right Grid Card 72%)
        BuildMainContentSection();

        // Add controls in docking layout order
        Controls.Add(pnlMainContent);
        Controls.Add(pnlBottomBar);
        Controls.Add(pnlSearch);
        Controls.Add(pnlDataPath);
        Controls.Add(pnlTitleBar);

        pnlMainContent.BringToFront();

        Shown += (s, e) => txtSearch.Focus();
        KeyDown += OnFormKeyDown;
        Paint += OnFormPaint;
    }

    private void OnFormPaint(object? sender, PaintEventArgs e)
    {
        using var pen = new Pen(Color.FromArgb(201, 220, 236), 1);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    // ═══════════════════════════════════════════════════════════════
    //  1. TITLE BAR SECTION (60px Deep Dark Navy #0A2540)
    // ═══════════════════════════════════════════════════════════════
    private void BuildTitleBarSection()
    {
        pnlTitleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.FromArgb(10, 37, 64) // Deep Dark Navy #0A2540
        };
        pnlTitleBar.MouseDown += Header_MouseDown;

        // Official application logo from Resources (app_preview.png / app.ico)
        var picLogo = new PictureBox
        {
            Image = ExecLedgerIcons.GetAppLogo(32, 32),
            Size = new Size(32, 32),
            Location = new Point(18, 14),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        picLogo.MouseDown += Header_MouseDown;
        pnlTitleBar.Controls.Add(picLogo);

        // Title: Select Company (Bold ~20-22px)
        lblTitle = new Label
        {
            Text = "Select Company",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(60, 16),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblTitle.MouseDown += Header_MouseDown;
        pnlTitleBar.Controls.Add(lblTitle);

        // Separator pipe
        lblTitleSeparator = new Label
        {
            Text = "|",
            Font = new Font("Segoe UI", 11F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(lblTitle.Right + 8, 19),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblTitleSeparator.MouseDown += Header_MouseDown;
        pnlTitleBar.Controls.Add(lblTitleSeparator);

        // Subtitle: MoneyFlow ERP Client v4.8 (Regular/medium ~16-18px)
        lblSubtitle = new Label
        {
            Text = "MoneyFlow ERP",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(203, 213, 225),
            Location = new Point(lblTitleSeparator.Right + 8, 19),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblSubtitle.MouseDown += Header_MouseDown;
        pnlTitleBar.Controls.Add(lblSubtitle);

        // Window Close Button: ONLY Close button on the right (No minimize, No maximize)
        btnCloseHeader = new Guna2Button
        {
            Text = "✕",
            Size = new Size(52, 60),
            Font = new Font("Segoe UI", 12F, FontStyle.Regular),
            ForeColor = Color.FromArgb(226, 232, 240),
            FillColor = Color.Transparent,
            BorderThickness = 0,
            BorderRadius = 0,
            Cursor = Cursors.Hand,
            HoverState = { FillColor = Color.FromArgb(220, 38, 38), ForeColor = Color.White }
        };
        btnCloseHeader.Click += (s, e) => Close();
        pnlTitleBar.Controls.Add(btnCloseHeader);

        pnlTitleBar.Resize += (s, e) =>
        {
            btnCloseHeader.Location = new Point(pnlTitleBar.Width - btnCloseHeader.Width, 0);
            lblTitleSeparator.Location = new Point(lblTitle.Right + 8, 19);
            lblSubtitle.Location = new Point(lblTitleSeparator.Right + 8, 19);
        };
    }

    private void Header_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, 0x0112, 0xF010 + 2, 0);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  2. DATA PATH ROW (56px - Network Button Completely Removed)
    // ═══════════════════════════════════════════════════════════════
    private void BuildDataPathSection()
    {
        pnlDataPath = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.FromArgb(246, 250, 254)
        };

        lblDataPathTitle = new Label
        {
            Text = "📁   DATA PATH",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(11, 39, 66),
            Location = new Point(20, 19),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlDataPath.Controls.Add(lblDataPathTitle);

        txtDataPath = new Guna2TextBox
        {
            Text = _currentDataPath,
            Font = new Font("Consolas", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(30, 41, 59),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(201, 220, 236),
            BorderRadius = 6,
            BorderThickness = 1,
            Height = 40,
            ReadOnly = true
        };
        pnlDataPath.Controls.Add(txtDataPath);

        btnSelectDrive = new Guna2Button
        {
            Text = "📁  Select Path (F3)",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(11, 39, 66),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(201, 220, 236),
            BorderThickness = 1,
            BorderRadius = 6,
            Size = new Size(180, 40),
            Cursor = Cursors.Hand,
            HoverState = { FillColor = Color.FromArgb(240, 247, 255), BorderColor = Color.FromArgb(148, 163, 184) }
        };
        btnSelectDrive.Click += (s, e) => SelectDataPath();
        pnlDataPath.Controls.Add(btnSelectDrive);

        pnlDataPath.Resize += (s, e) =>
        {
            int y = (pnlDataPath.Height - 40) / 2;
            btnSelectDrive.Location = new Point(pnlDataPath.Width - 20 - btnSelectDrive.Width, y);
            lblDataPathTitle.Location = new Point(20, (pnlDataPath.Height - lblDataPathTitle.Height) / 2);
            txtDataPath.Location = new Point(lblDataPathTitle.Right + 18, y);
            txtDataPath.Width = Math.Max(200, btnSelectDrive.Left - txtDataPath.Left - 14);
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  3. SEARCH SECTION (82px)
    // ═══════════════════════════════════════════════════════════════
    private void BuildSearchSection()
    {
        pnlSearch = new Panel
        {
            Dock = DockStyle.Top,
            Height = 82,
            BackColor = Color.FromArgb(246, 250, 254)
        };

        // Header Line: NAME OF COMPANY on Left, Hint next to it, Pill Badge on Right
        lblSearchPrompt = new Label
        {
            Text = "NAME OF COMPANY",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(11, 39, 66),
            Location = new Point(20, 8),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlSearch.Controls.Add(lblSearchPrompt);

        lblSearchHint = new Label
        {
            Text = "(press Enter to load, Esc to abort)",
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(lblSearchPrompt.Right + 8, 9),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlSearch.Controls.Add(lblSearchHint);

        // Dynamic Match Count Pill Badge
        lblMatchCount = new Label
        {
            Text = "0 MATCHES FOUND",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(2, 132, 199),
            BackColor = Color.FromArgb(224, 238, 250),
            TextAlign = ContentAlignment.MiddleCenter,
            Padding = new Padding(10, 4, 10, 4),
            AutoSize = true
        };
        pnlSearch.Controls.Add(lblMatchCount);

        // Full-Width Search Box
        txtSearch = new Guna2TextBox
        {
            Text = "",
            PlaceholderText = "🔍   Search company name, entity number, or code...",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(15, 23, 42),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(201, 220, 236),
            BorderThickness = 1,
            BorderRadius = 6,
            Location = new Point(20, 32),
            Height = 44
        };
        txtSearch.KeyDown += OnSearchKeyDown;
        pnlSearch.Controls.Add(txtSearch);

        // Ctrl+F shortcut indicator badge inside search box (styled rounded keyboard badge matching reference)
        lblCtrlFBadge = new Guna2Button
        {
            Text = "Ctrl+F",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            FillColor = Color.FromArgb(241, 245, 249),
            BorderColor = Color.FromArgb(226, 232, 240),
            BorderThickness = 1,
            BorderRadius = 4,
            Size = new Size(62, 24),
            TextAlign = HorizontalAlignment.Center,
            TextOffset = new Point(0, 0),
            Cursor = Cursors.Hand,
            HoverState = { FillColor = Color.FromArgb(226, 232, 240), BorderColor = Color.FromArgb(203, 213, 225) }
        };
        lblCtrlFBadge.Click += (s, e) =>
        {
            txtSearch.Focus();
            txtSearch.SelectAll();
        };
        pnlSearch.Controls.Add(lblCtrlFBadge);
        lblCtrlFBadge.BringToFront();

        // Clear button
        btnClearSearch = new Guna2Button
        {
            Text = "✕",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            FillColor = Color.Transparent,
            Size = new Size(20, 20),
            BorderThickness = 0,
            Cursor = Cursors.Hand,
            Visible = false,
            HoverState = { ForeColor = Color.FromArgb(239, 68, 68) }
        };
        btnClearSearch.Click += (s, e) =>
        {
            txtSearch.Clear();
            btnClearSearch.Visible = false;
            LayoutSearchControls();
            txtSearch.Focus();
        };
        pnlSearch.Controls.Add(btnClearSearch);
        btnClearSearch.BringToFront();

        void LayoutSearchControls()
        {
            lblSearchPrompt.Location = new Point(20, 8);
            lblSearchHint.Location = new Point(lblSearchPrompt.Right + 8, 9);
            lblMatchCount.Location = new Point(pnlSearch.Width - 20 - lblMatchCount.Width, 6);

            txtSearch.Location = new Point(20, 32);
            txtSearch.Width = Math.Max(200, pnlSearch.Width - 40);

            // Dynamic badge width ensuring "+F" is never clipped at any DPI scaling
            int badgeW = Math.Max(62, TextRenderer.MeasureText(lblCtrlFBadge.Text, lblCtrlFBadge.Font).Width + 20);
            lblCtrlFBadge.Size = new Size(badgeW, 24);

            // Perfectly centered vertically inside txtSearch
            int centerY = txtSearch.Top + (txtSearch.Height - lblCtrlFBadge.Height) / 2;

            if (btnClearSearch.Visible)
            {
                int clearY = txtSearch.Top + (txtSearch.Height - btnClearSearch.Height) / 2;
                btnClearSearch.Location = new Point(txtSearch.Right - btnClearSearch.Width - 10, clearY);
                lblCtrlFBadge.Location = new Point(btnClearSearch.Left - lblCtrlFBadge.Width - 8, centerY);
            }
            else
            {
                lblCtrlFBadge.Location = new Point(txtSearch.Right - lblCtrlFBadge.Width - 12, centerY);
            }

            lblCtrlFBadge.BringToFront();
            btnClearSearch.BringToFront();
        }

        txtSearch.TextChanged += (s, e) =>
        {
            btnClearSearch.Visible = !string.IsNullOrEmpty(txtSearch.Text);
            LayoutSearchControls();
            ApplyFilter();
        };

        pnlSearch.Resize += (s, e) => LayoutSearchControls();
        LayoutSearchControls();
    }

    // ═══════════════════════════════════════════════════════════════
    //  4. BOTTOM ACTION BAR (62px, Lightweight on Window Background)
    // ═══════════════════════════════════════════════════════════════
    private void BuildBottomBarSection()
    {
        pnlBottomBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 62,
            BackColor = Color.FromArgb(246, 250, 254)
        };

        btnCancel = new Guna2Button
        {
            Text = "Cancel (Esc)",
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(15, 23, 42),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(201, 220, 236),
            BorderThickness = 1,
            BorderRadius = 6,
            Size = new Size(120, 40),
            Cursor = Cursors.Hand,
            HoverState = { FillColor = Color.FromArgb(241, 245, 249), BorderColor = Color.FromArgb(148, 163, 184) }
        };
        btnCancel.Click += (s, e) => Close();
        pnlBottomBar.Controls.Add(btnCancel);

        pnlBottomBar.Resize += (s, e) =>
        {
            int yBtn = (pnlBottomBar.Height - 40) / 2;
            btnCancel.Location = new Point(pnlBottomBar.Width - 20 - btnCancel.Width, yBtn);
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  5. MAIN CONTENT (Two Rounded Cards: Left Directory + Right Grid)
    // ═══════════════════════════════════════════════════════════════
    private void BuildMainContentSection()
    {
        pnlMainContent = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(246, 250, 254),
            Padding = new Padding(20, 6, 20, 10)
        };

        // Left Card (~280px, ~28%)
        BuildLeftDirectoryCard();

        // Right Card (Remaining width, ~72%)
        BuildRightGridCard();

        pnlMainContent.Controls.Add(pnlRightCardWrapper);
        pnlMainContent.Controls.Add(pnlLeftCard);

        pnlMainContent.Resize += (s, e) =>
        {
            int totalW = pnlMainContent.ClientSize.Width - pnlMainContent.Padding.Horizontal;
            if (totalW > 300)
            {
                pnlLeftCard.Width = Math.Max(260, (int)(totalW * 0.28));
            }
        };
    }

    private void BuildLeftDirectoryCard()
    {
        pnlLeftCard = new Guna2Panel
        {
            Dock = DockStyle.Left,
            Width = 280,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(212, 227, 241),
            BorderThickness = 1,
            BorderRadius = 8,
            Padding = new Padding(1, 0, 1, 1)
        };
        pnlLeftCard.ShadowDecoration.Enabled = false;

        // Header: QUICK ACTION DIRECTORY | ALT MENU
        var pnlDirHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.Transparent
        };
        pnlDirHeader.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var topPath = CreateTopRoundedRectanglePath(new Rectangle(0, 0, pnlDirHeader.Width, pnlDirHeader.Height), 8);
            using var bgBrush = new SolidBrush(Color.FromArgb(240, 247, 255));
            e.Graphics.FillPath(bgBrush, topPath);

            using var pen = new Pen(Color.FromArgb(212, 227, 241), 1);
            e.Graphics.DrawLine(pen, 0, pnlDirHeader.Height - 1, pnlDirHeader.Width, pnlDirHeader.Height - 1);
        };

        var lblDirTitle = new Label
        {
            Text = "QUICK ACTION DIRECTORY",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(11, 39, 66),
            Location = new Point(14, 14),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlDirHeader.Controls.Add(lblDirTitle);

        var lblAltMenu = new Label
        {
            Text = "ALT MENU",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(2, 132, 199),
            BackColor = Color.FromArgb(224, 238, 250),
            Padding = new Padding(6, 2, 6, 2),
            AutoSize = true
        };
        pnlDirHeader.Controls.Add(lblAltMenu);
        pnlDirHeader.Resize += (s, e) =>
        {
            lblAltMenu.Location = new Point(pnlDirHeader.Width - lblAltMenu.Width - 14, 13);
        };

        // Action Items: Create Company & Select Path (Remote and redundant Specify Path removed per request)
        var rowCreate = CreateActionRow("🏢", "Create Company", "Alt+C", () => CreateNewCompany());
        var rowSelectPath = CreateActionRow("📁", "Select Path", "Alt+S", () => SelectDataPath());

        pnlLeftCard.Controls.Add(pnlDirHeader);
        pnlLeftCard.Controls.Add(rowCreate);
        pnlLeftCard.Controls.Add(rowSelectPath);

        pnlDirHeader.BringToFront();
        rowCreate.BringToFront();
        rowSelectPath.BringToFront();
    }

    private Guna2Panel CreateActionRow(string icon, string title, string shortcut, Action onClick)
    {
        var rowPanel = new Guna2Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            FillColor = Color.White,
            BorderThickness = 0,
            Cursor = Cursors.Hand,
            Padding = new Padding(14, 0, 14, 0)
        };

        rowPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(241, 245, 249), 1);
            e.Graphics.DrawLine(pen, 12, rowPanel.Height - 1, rowPanel.Width - 12, rowPanel.Height - 1);
        };

        var lblIconText = new Label
        {
            Text = $"{icon}   {title}",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(14, 13),
            AutoSize = true,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        rowPanel.Controls.Add(lblIconText);

        var lblShort = new Label
        {
            Text = shortcut,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(2, 132, 199),
            BackColor = Color.FromArgb(240, 247, 255),
            Padding = new Padding(6, 2, 6, 2),
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        rowPanel.Controls.Add(lblShort);

        void SetHover(bool isHover)
        {
            rowPanel.FillColor = isHover ? Color.FromArgb(240, 247, 255) : Color.White;
            lblIconText.ForeColor = isHover ? Color.FromArgb(2, 132, 199) : Color.FromArgb(30, 41, 59);
            lblShort.BackColor = isHover ? Color.FromArgb(224, 238, 250) : Color.FromArgb(240, 247, 255);
        }

        rowPanel.MouseEnter += (s, e) => SetHover(true);
        rowPanel.MouseLeave += (s, e) => SetHover(false);
        lblIconText.MouseEnter += (s, e) => SetHover(true);
        lblIconText.MouseLeave += (s, e) => SetHover(false);
        lblShort.MouseEnter += (s, e) => SetHover(true);
        lblShort.MouseLeave += (s, e) => SetHover(false);

        rowPanel.Click += (s, e) => onClick();
        lblIconText.Click += (s, e) => onClick();
        lblShort.Click += (s, e) => onClick();

        rowPanel.Resize += (s, e) =>
        {
            lblShort.Location = new Point(rowPanel.Width - lblShort.Width - 14, 12);
        };

        return rowPanel;
    }

    private void BuildRightGridCard()
    {
        pnlRightCardWrapper = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(14, 0, 0, 0)
        };

        pnlRightCard = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(212, 227, 241),
            BorderThickness = 1,
            BorderRadius = 8,
            Padding = new Padding(1, 0, 1, 1)
        };
        pnlRightCard.ShadowDecoration.Enabled = false;

        // Dedicated Top Navy Header Panel with rounded top corners
        pnlGridHeader = new Panel
        {
            Dock = DockStyle.None,
            Height = 44,
            BackColor = Color.Transparent
        };

        pnlGridHeader.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var topPath = CreateTopRoundedRectanglePath(new Rectangle(0, 0, pnlGridHeader.Width, pnlGridHeader.Height), 8);
            using var navyBrush = new SolidBrush(Color.FromArgb(11, 39, 66));
            e.Graphics.FillPath(navyBrush, topPath);

            int w0 = 32;
            int scrollWidth = gridCompanies.Controls.OfType<VScrollBar>().FirstOrDefault(sb => sb.Visible)?.Width ?? 0;
            int avail = pnlGridHeader.Width - w0 - scrollWidth;
            if (avail <= 0) return;

            int w1 = (int)(avail * 0.56);
            int w2 = (int)(avail * 0.20);
            int w3 = avail - w1 - w2;

            using var headerFont = new Font("Segoe UI", 8F, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            using var penSep = new Pen(Color.FromArgb(24, 56, 90), 1);

            // COMPANY NAME
            var rName = new Rectangle(w0 + 10, 0, w1 - 10, pnlGridHeader.Height);
            var sfLeft = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString("COMPANY NAME", headerFont, textBrush, rName, sfLeft);

            // ENTITY CODE
            e.Graphics.DrawLine(penSep, w0 + w1, 8, w0 + w1, pnlGridHeader.Height - 8);
            var rCode = new Rectangle(w0 + w1, 0, w2, pnlGridHeader.Height);
            var sfCenter = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString("ENTITY CODE", headerFont, textBrush, rCode, sfCenter);

            // FINANCIAL PERIOD
            e.Graphics.DrawLine(penSep, w0 + w1 + w2, 8, w0 + w1 + w2, pnlGridHeader.Height - 8);
            var rPeriod = new Rectangle(w0 + w1 + w2, 0, w3, pnlGridHeader.Height);
            e.Graphics.DrawString("FINANCIAL PERIOD", headerFont, textBrush, rPeriod, sfCenter);
        };

        gridCompanies = new DataGridView
        {
            Dock = DockStyle.None,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.None,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            GridColor = Color.White,
            RowHeadersVisible = false,
            ColumnHeadersVisible = false, // Handled cleanly by pnlGridHeader
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AllowUserToResizeColumns = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ReadOnly = true,
            EnableHeadersVisualStyles = false,
            AutoGenerateColumns = false
        };

        // Row Styling: Height: 44px
        gridCompanies.RowTemplate.Height = 44;
        gridCompanies.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Color.FromArgb(11, 39, 66),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            SelectionBackColor = Color.FromArgb(234, 244, 255), // Light blue #EAF4FF
            SelectionForeColor = Color.FromArgb(11, 39, 66),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0)
        };
        gridCompanies.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Color.FromArgb(11, 39, 66),
            SelectionBackColor = Color.FromArgb(234, 244, 255),
            SelectionForeColor = Color.FromArgb(11, 39, 66)
        };

        // Columns: Indicator, Name, Code, Period
        var colIndicator = new DataGridViewTextBoxColumn
        {
            Name = "ColIndicator",
            HeaderText = "",
            Width = 32,
            Resizable = DataGridViewTriState.False
        };

        var colName = new DataGridViewTextBoxColumn
        {
            Name = "ColName",
            HeaderText = "COMPANY NAME",
            MinimumWidth = 260,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        };

        var colCode = new DataGridViewTextBoxColumn
        {
            Name = "ColCode",
            HeaderText = "ENTITY CODE",
            Width = 140,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colPeriod = new DataGridViewTextBoxColumn
        {
            Name = "ColPeriod",
            HeaderText = "FINANCIAL PERIOD",
            Width = 180,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        gridCompanies.Columns.AddRange(colIndicator, colName, colCode, colPeriod);

        // Dynamic column width proportional calculation on resize
        void AdjustColumns()
        {
            int scrollWidth = gridCompanies.Controls.OfType<VScrollBar>().FirstOrDefault(s => s.Visible)?.Width ?? 0;
            int avail = gridCompanies.ClientSize.Width - colIndicator.Width - scrollWidth;
            if (avail > 200)
            {
                colName.Width = (int)(avail * 0.56);
                colCode.Width = (int)(avail * 0.20);
                colPeriod.Width = Math.Max(120, avail - colName.Width - colCode.Width);
            }
            pnlGridHeader.Invalidate();
        }

        void LayoutRightCard()
        {
            int w = pnlRightCard.ClientSize.Width;
            int h = pnlRightCard.ClientSize.Height;
            if (w <= 0 || h <= 0) return;

            pnlGridHeader.Location = new Point(1, 1);
            pnlGridHeader.Size = new Size(w - 2, 44);

            // gridCompanies starts precisely at Y = 45 (immediately below 44px navy header)
            gridCompanies.Location = new Point(1, 45);
            gridCompanies.Size = new Size(w - 2, Math.Max(0, h - 46));

            if (pnlEmptyState != null)
            {
                pnlEmptyState.Location = new Point(1, 45);
                pnlEmptyState.Size = new Size(Math.Max(100, w - 2), Math.Max(100, h - 46));
            }

            AdjustColumns();
        }

        pnlRightCard.Resize += (s, e) => LayoutRightCard();
        gridCompanies.Resize += (s, e) => AdjustColumns();
        gridCompanies.CellPainting += OnGridCellPainting;
        gridCompanies.DoubleClick += (s, e) => ExecuteCurrentSelection();
        gridCompanies.SelectionChanged += (s, e) => gridCompanies.Invalidate();
        gridCompanies.CellClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < gridCompanies.Rows.Count)
            {
                gridCompanies.ClearSelection();
                gridCompanies.Rows[e.RowIndex].Selected = true;
                gridCompanies.CurrentCell = gridCompanies.Rows[e.RowIndex].Cells[1];
                gridCompanies.Invalidate();
            }
        };
        gridCompanies.KeyDown += OnGridKeyDown;

        // Empty State Panel
        BuildEmptyStateSection();

        pnlRightCard.Controls.Add(gridCompanies);
        pnlRightCard.Controls.Add(pnlEmptyState);
        pnlRightCard.Controls.Add(pnlGridHeader);

        pnlGridHeader.BringToFront();
        LayoutRightCard();

        pnlRightCardWrapper.Controls.Add(pnlRightCard);
    }

    private void BuildEmptyStateSection()
    {
        pnlEmptyState = new Panel
        {
            BackColor = Color.White,
            Visible = false
        };

        var lblEmptyIcon = new Label
        {
            Text = "🏢",
            Font = new Font("Segoe UI", 28F),
            ForeColor = Color.FromArgb(148, 163, 184),
            AutoSize = true
        };
        pnlEmptyState.Controls.Add(lblEmptyIcon);

        var lblEmptyTitle = new Label
        {
            Text = "No Companies Found",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(11, 39, 66),
            AutoSize = true
        };
        pnlEmptyState.Controls.Add(lblEmptyTitle);

        var lblEmptySubtitle = new Label
        {
            Text = "No company was found in the selected location.\r\nCreate your first company or select another data path.",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            TextAlign = ContentAlignment.TopCenter,
            AutoSize = true
        };
        pnlEmptyState.Controls.Add(lblEmptySubtitle);

        var btnEmptyCreate = new Guna2Button
        {
            Text = "+ Create Company",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.White,
            FillColor = Color.FromArgb(11, 39, 66),
            BorderRadius = 6,
            Size = new Size(165, 38),
            Cursor = Cursors.Hand,
            HoverState = { FillColor = Color.FromArgb(18, 63, 104) }
        };
        btnEmptyCreate.Click += (s, e) => CreateNewCompany();
        pnlEmptyState.Controls.Add(btnEmptyCreate);

        var btnEmptyBrowse = new Guna2Button
        {
            Text = "Select Path...",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(30, 41, 59),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(201, 220, 236),
            BorderThickness = 1,
            BorderRadius = 6,
            Size = new Size(140, 38),
            Cursor = Cursors.Hand,
            HoverState = { FillColor = Color.FromArgb(240, 247, 255) }
        };
        btnEmptyBrowse.Click += (s, e) => SelectDataPath();
        pnlEmptyState.Controls.Add(btnEmptyBrowse);

        void LayoutEmptyState()
        {
            int cx = pnlEmptyState.Width / 2;
            int startY = Math.Max(20, (pnlEmptyState.Height - 200) / 2);

            lblEmptyIcon.Location = new Point(cx - lblEmptyIcon.Width / 2, startY);
            lblEmptyTitle.Location = new Point(cx - lblEmptyTitle.Width / 2, lblEmptyIcon.Bottom + 6);
            lblEmptySubtitle.Location = new Point(cx - lblEmptySubtitle.Width / 2, lblEmptyTitle.Bottom + 8);
            btnEmptyCreate.Location = new Point(cx - btnEmptyCreate.Width - 8, lblEmptySubtitle.Bottom + 16);
            btnEmptyBrowse.Location = new Point(cx + 8, lblEmptySubtitle.Bottom + 16);
        }

        pnlEmptyState.Resize += (s, e) => LayoutEmptyState();
        LayoutEmptyState();
    }

    // ═══════════════════════════════════════════════════════════════
    //  CELL PAINTING (Indicator, Icon, Entity Code, Financial Period)
    // ═══════════════════════════════════════════════════════════════
    private void OnGridCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.Graphics == null) return;

        // Header Cells (Borderless Deep Navy #0B2742)
        if (e.RowIndex == -1)
        {
            using var bgHdr = new SolidBrush(Color.FromArgb(11, 39, 66));
            e.Graphics.FillRectangle(bgHdr, e.CellBounds);

            // Subtle vertical column separator in header
            if (e.ColumnIndex > 0)
            {
                using var penSep = new Pen(Color.FromArgb(24, 56, 90), 1);
                e.Graphics.DrawLine(penSep, e.CellBounds.Left, 6, e.CellBounds.Left, e.CellBounds.Height - 6);
            }

            var sf = new StringFormat
            {
                Alignment = (e.ColumnIndex <= 1) ? StringAlignment.Near : StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            int textPad = (e.ColumnIndex == 1) ? 10 : 0;
            var headerTextRect = new Rectangle(e.CellBounds.Left + textPad, e.CellBounds.Top, e.CellBounds.Width - textPad, e.CellBounds.Height);
            using var headerFont = new Font("Segoe UI", 8F, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            e.Graphics.DrawString(gridCompanies.Columns[e.ColumnIndex].HeaderText, headerFont, textBrush, headerTextRect, sf);

            e.Handled = true;
            return;
        }

        if (e.RowIndex < 0 || e.RowIndex >= _gridRows.Count) return;

        var rowItem = _gridRows[e.RowIndex];
        bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;

        // Custom row background
        Color rowBgColor = isSelected
            ? Color.FromArgb(234, 244, 255) // Soft light blue #EAF4FF
            : Color.White;

        using (var bgBrush = new SolidBrush(rowBgColor))
        {
            e.Graphics.FillRectangle(bgBrush, e.CellBounds);
        }
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        // Subtle row bottom divider line
        using (var linePen = new Pen(Color.FromArgb(241, 245, 249), 1))
        {
            e.Graphics.DrawLine(linePen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right, e.CellBounds.Bottom - 1);
        }

        // Column 0: Selection Pointer Indicator (▶)
        if (e.ColumnIndex == 0)
        {
            if (isSelected)
            {
                using var arrowBrush = new SolidBrush(Color.FromArgb(2, 132, 199));
                var arrowPoints = new Point[]
                {
                    new Point(e.CellBounds.Left + 11, e.CellBounds.Top + e.CellBounds.Height / 2 - 5),
                    new Point(e.CellBounds.Left + 18, e.CellBounds.Top + e.CellBounds.Height / 2),
                    new Point(e.CellBounds.Left + 11, e.CellBounds.Top + e.CellBounds.Height / 2 + 5)
                };
                e.Graphics.FillPolygon(arrowBrush, arrowPoints);
            }
            e.Handled = true;
            return;
        }

        // Column 1: COMPANY NAME (Building icon + text)
        if (e.ColumnIndex == 1)
        {
            int left = e.CellBounds.Left + 8;
            int top = e.CellBounds.Top + (e.CellBounds.Height - 16) / 2;

            // Draw crisp vector building icon
            DrawBuildingIcon(e.Graphics, left, top, Color.FromArgb(2, 132, 199));
            left += 26;

            using var fontName = new Font("Segoe UI", 9F, isSelected ? FontStyle.Bold : FontStyle.Regular);
            using var brushName = new SolidBrush(Color.FromArgb(11, 39, 66));
            e.Graphics.DrawString(rowItem.CompanyName, fontName, brushName, left, top - 1);

            e.Handled = true;
            return;
        }

        // Column 2: ENTITY CODE (Centered e.g. (010000))
        if (e.ColumnIndex == 2)
        {
            using var fontCode = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            using var brushCode = new SolidBrush(Color.FromArgb(11, 39, 66));
            var sfCode = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(rowItem.CompanyNumber, fontCode, brushCode, e.CellBounds, sfCode);
            e.Handled = true;
            return;
        }

        // Column 3: FINANCIAL PERIOD (Centered e.g. 1-Apr-26 to 31-Mar-27)
        if (e.ColumnIndex == 3)
        {
            using var fontPeriod = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            using var brushPeriod = new SolidBrush(Color.FromArgb(11, 39, 66));
            var sfPeriod = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(rowItem.FinancialPeriod, fontPeriod, brushPeriod, e.CellBounds, sfPeriod);
            e.Handled = true;
            return;
        }

        e.Handled = true;
    }

    private void DrawBuildingIcon(Graphics g, int x, int y, Color color)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);

        // Main building structure
        g.FillRectangle(brush, x, y + 2, 14, 14);
        // Roof peak
        g.FillRectangle(brush, x + 3, y, 8, 2);

        // Windows
        using var winBrush = new SolidBrush(Color.White);
        g.FillRectangle(winBrush, x + 3, y + 4, 3, 2);
        g.FillRectangle(winBrush, x + 8, y + 4, 3, 2);
        g.FillRectangle(winBrush, x + 3, y + 8, 3, 2);
        g.FillRectangle(winBrush, x + 8, y + 8, 3, 2);
        // Door
        g.FillRectangle(winBrush, x + 5, y + 12, 4, 4);
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = Math.Max(1, radius * 2);
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static GraphicsPath CreateTopRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = Math.Max(1, radius * 2);
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddLine(rect.Right, rect.Bottom, rect.X, rect.Bottom);
        path.CloseFigure();
        return path;
    }

    // ═══════════════════════════════════════════════════════════════
    //  DATA LOADING & FILTERING (NO HARDCODED/SAMPLE DATA)
    // ═══════════════════════════════════════════════════════════════
    private async void LoadCompaniesAsync()
    {
        try
        {
            var list = new List<CompanySummaryDto>();
            try
            {
                var companies = await _companyService.GetAllCompaniesAsync();
                if (companies != null)
                {
                    list.AddRange(companies);
                }
            }
            catch
            {
                // Graceful fallback if storage service unavailable
            }

            // Also directly discover any companies in _currentDataPath
            if (!string.IsNullOrWhiteSpace(_currentDataPath) && Directory.Exists(_currentDataPath))
            {
                var seenNumbers = new HashSet<string>(list.Where(c => !string.IsNullOrWhiteSpace(c.CompanyNumber)).Select(c => c.CompanyNumber!), StringComparer.OrdinalIgnoreCase);

                void CheckCompanyDirectory(string dir)
                {
                    string dataFilePath = Path.Combine(dir, "company.data");
                    if (!File.Exists(dataFilePath)) return;

                    string folderName = Path.GetFileName(dir);
                    try
                    {
                        var storageEngine = new StorageEngine();
                        var (hdr, sec) = storageEngine.ReadHeaders(dataFilePath);
                        string compNum = string.IsNullOrWhiteSpace(hdr.CompanyId) ? folderName : hdr.CompanyId;
                        if (seenNumbers.Contains(compNum)) return;

                        string compName = folderName;
                        DateTime fyFrom = new DateTime(DateTime.Today.Year, 4, 1);
                        string curr = _systemConfig?.CurrencySymbol ?? "₹";
                        bool isPwd = sec.SecurityMode == MoneyFlow.Data.Encryption.SecurityMode.PasswordProtected;


                        if (!isPwd)
                        {
                            try
                            {
                                var (store, _, _) = storageEngine.LoadCompany(dataFilePath, null);
                                if (!string.IsNullOrWhiteSpace(store.CompanyInfo?.CompanyName))
                                {
                                    compName = store.CompanyInfo.CompanyName;
                                }
                                if (store.CompanyInfo != null && store.CompanyInfo.FinancialYearFrom != default && store.CompanyInfo.FinancialYearFrom.Year > 1900)
                                {
                                    fyFrom = store.CompanyInfo.FinancialYearFrom;
                                }
                                if (!string.IsNullOrWhiteSpace(store.CompanyInfo?.Currency))
                                {
                                    curr = store.CompanyInfo.Currency;
                                }
                            }
                            catch { }
                        }

                        int compId = 0;
                        if (int.TryParse(compNum, out int parsedId)) compId = parsedId;
                        else if (int.TryParse(folderName, out int parsedFId)) compId = parsedFId;
                        if (compId == 0) compId = Math.Abs(compName.GetHashCode()) % 90000 + 10000;

                        seenNumbers.Add(compNum);
                        list.Add(new CompanySummaryDto
                        {
                            CompanyId = compId,
                            CompanyName = compName,
                            CompanyNumber = compNum,
                            FinancialYearFrom = fyFrom,
                            BooksBeginningFrom = fyFrom,
                            Currency = curr,
                            DataDirectory = dir,
                            IsPasswordProtected = isPwd,
                            IsActive = true
                        });
                    }
                    catch { }
                }

                try
                {
                    foreach (var subDir in Directory.GetDirectories(_currentDataPath))
                    {
                        CheckCompanyDirectory(subDir);
                    }
                    CheckCompanyDirectory(_currentDataPath);
                }
                catch { }
            }


            _allCompanies = list;
            ApplyFilter();
        }
        catch
        {
            ApplyFilter();
        }
    }

    private void ApplyFilter()
    {
        string query = txtSearch.Text.Trim();
        btnClearSearch.Visible = !string.IsNullOrEmpty(query);
        _gridRows.Clear();

        int currentCompanyId = _companyContext.CurrentCompany?.CompanyId ?? 0;

        foreach (var c in _allCompanies)
        {
            if (string.IsNullOrWhiteSpace(c.CompanyName) || c.CompanyName.Trim() == "010000")
                continue;

            bool isCurrent = c.CompanyId == currentCompanyId;
            string periodStr = c.PeriodDisplay; // Dynamically calculated from c.FinancialYearFrom
            string codeStr = string.IsNullOrWhiteSpace(c.CompanyNumber) ? string.Empty : $"({c.CompanyNumber})";

            var rowItem = new CompanyGridRowItem
            {
                CompanyId = c.CompanyId,
                CompanyName = c.CompanyName,
                CompanyNumber = codeStr,
                FinancialPeriod = periodStr,
                Currency = c.Currency,
                Status = isCurrent ? "ACTIVE / LOADED" : (c.IsActive ? "Available" : "Inactive"),
                LastSynchronized = "",
                IsPasswordProtected = c.IsPasswordProtected,
                IsCurrentDefault = isCurrent,
                CompanyDto = c
            };

            if (string.IsNullOrWhiteSpace(query)
                || c.CompanyName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || c.CompanyNumber.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                _gridRows.Add(rowItem);
            }
        }

        // Populate DataGridView
        gridCompanies.Rows.Clear();
        foreach (var r in _gridRows)
        {
            int rowIdx = gridCompanies.Rows.Add(
                "",
                r.CompanyName,
                r.CompanyNumber,
                r.FinancialPeriod
            );
            gridCompanies.Rows[rowIdx].Tag = r;
        }

        // Dynamic match count
        int matchCount = _gridRows.Count;
        if (matchCount == 0)
            lblMatchCount.Text = "NO MATCHES FOUND";
        else if (matchCount == 1)
            lblMatchCount.Text = "1 MATCH FOUND";
        else
            lblMatchCount.Text = $"{matchCount} MATCHES FOUND";

        lblMatchCount.Location = new Point(pnlSearch.Width - 20 - lblMatchCount.Width, 6);

        // Keep gridCompanies ALWAYS visible so the navy header is never hidden
        gridCompanies.Visible = true;
        pnlEmptyState.Visible = (_gridRows.Count == 0);
        if (pnlEmptyState.Visible)
        {
            pnlEmptyState.BringToFront();
        }

        // Select first or default active row
        if (gridCompanies.Rows.Count > 0)
        {
            int defaultIdx = _gridRows.FindIndex(x => x.IsCurrentDefault);
            gridCompanies.ClearSelection();
            int selectIdx = defaultIdx >= 0 ? defaultIdx : 0;
            gridCompanies.Rows[selectIdx].Selected = true;
            gridCompanies.CurrentCell = gridCompanies.Rows[selectIdx].Cells[1];
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  ACTIONS & SHORTCUT HANDLERS
    // ═══════════════════════════════════════════════════════════════
    private void CreateNewCompany()
    {
        using var form = new CompanyCreateEditForm(_companyService, _systemConfig, null, _currentDataPath);
        form.ShowDialog(this);
        LoadCompaniesAsync();
    }

    private void OpenStartupConfiguration()
    {
        using var form = new StartupConfigurationForm(_systemConfig);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _systemConfig = form.Config;
            _currentDataPath = Path.Combine(_systemConfig.CompanyDataPath, "Companies");
            if (!_currentDataPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                _currentDataPath += Path.DirectorySeparatorChar;

            txtDataPath.Text = _currentDataPath;
            LoadCompaniesAsync();
        }
    }

    private void SelectRemoteCompany()
    {
        MessageBox.Show(
            this,
            "Remote Company discovery is configured for Local File Storage mode.\r\nTo select a network company, please use 'Specify Path' to choose a mapped network directory.",
            "Remote Company",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void SpecifyDataPath()
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "Specify Company Data Directory",
            SelectedPath = _currentDataPath,
            ShowNewFolderButton = true
        };

        if (fbd.ShowDialog(this) == DialogResult.OK)
        {
            _currentDataPath = fbd.SelectedPath;
            if (!_currentDataPath.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                _currentDataPath += Path.DirectorySeparatorChar;
            }
            txtDataPath.Text = _currentDataPath;
            LoadCompaniesAsync();
        }
    }

    private void SelectDataPath()
    {
        SpecifyDataPath();
    }

    // ═══════════════════════════════════════════════════════════════
    //  SELECTION & EXECUTION (NO HARDCODED DATA)
    // ═══════════════════════════════════════════════════════════════
    private void ExecuteCurrentSelection()
    {
        CompanyGridRowItem? item = null;
        if (gridCompanies.CurrentRow?.Tag is CompanyGridRowItem cur)
        {
            item = cur;
        }
        else if (gridCompanies.SelectedRows.Count > 0 && gridCompanies.SelectedRows[0].Tag is CompanyGridRowItem sel)
        {
            item = sel;
        }
        else if (gridCompanies.Rows.Count > 0 && gridCompanies.Rows[0].Tag is CompanyGridRowItem first)
        {
            item = first;
        }

        if (item == null) return;

        SelectCompany(item.CompanyId);
    }

    private async void SelectCompany(int companyId)
    {
        var comp = _allCompanies.FirstOrDefault(c => c.CompanyId == companyId);
        if (comp == null && _allCompanies.Count > 0)
        {
            comp = _allCompanies[0];
        }

        if (comp == null) return;

        if (comp.IsPasswordProtected)
        {
            using var pwdDlg = new CompanyPasswordPromptDialog(comp.CompanyName, comp.CompanyNumber);
            bool verified = false;
            while (!verified)
            {
                if (pwdDlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                bool valid = await _companyService.VerifyCompanyPasswordAsync(companyId, pwdDlg.EnteredPassword);
                if (valid)
                {
                    verified = true;
                }
                else
                {
                    pwdDlg.SetError("Incorrect Executive Ledger / Tally Vault password. Please try again.");
                }
            }
        }

        try
        {
            bool ok = await _companyService.OpenCompanyAsync(companyId);
            if (!ok)
            {
                var company = new Core.Entities.Company
                {
                    CompanyId = comp.CompanyId,
                    CompanyName = comp.CompanyName,
                    CompanyNumber = comp.CompanyNumber,
                    State = comp.State,
                    Currency = comp.Currency,
                    IsActive = comp.IsActive,
                    CreatedAt = DateTime.UtcNow
                };

                var fy = new Core.Entities.FinancialYear
                {
                    FinancialYearId = 1,
                    CompanyId = comp.CompanyId,
                    YearName = $"{comp.FinancialYearFrom:yyyy}-{(comp.FinancialYearFrom.AddYears(1).Year % 100):D2}",
                    StartDate = comp.FinancialYearFrom,
                    EndDate = comp.FinancialYearFrom.AddYears(1).AddDays(-1),
                    IsClosed = false,
                    CreatedAt = DateTime.UtcNow
                };

                _companyContext.SetActiveCompany(company, fy);
            }

            CompanySelected = true;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception)
        {
            var company = new Core.Entities.Company
            {
                CompanyId = comp.CompanyId,
                CompanyName = comp.CompanyName,
                CompanyNumber = comp.CompanyNumber,
                State = comp.State,
                Currency = comp.Currency,
                IsActive = comp.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            var fy = new Core.Entities.FinancialYear
            {
                FinancialYearId = 1,
                CompanyId = comp.CompanyId,
                YearName = $"{comp.FinancialYearFrom:yyyy}-{(comp.FinancialYearFrom.AddYears(1).Year % 100):D2}",
                StartDate = comp.FinancialYearFrom,
                EndDate = comp.FinancialYearFrom.AddYears(1).AddDays(-1),
                IsClosed = false,
                CreatedAt = DateTime.UtcNow
            };

            _companyContext.SetActiveCompany(company, fy);
            CompanySelected = true;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  KEYBOARD NAVIGATION
    // ═══════════════════════════════════════════════════════════════
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // Ctrl+F -> Search
        if (keyData == (Keys.Control | Keys.F))
        {
            txtSearch.Focus();
            txtSearch.SelectAll();
            return true;
        }

        // Alt Shortcuts
        if (keyData == (Keys.Alt | Keys.C))
        {
            CreateNewCompany();
            return true;
        }
        if (keyData == (Keys.Alt | Keys.S) || keyData == (Keys.Alt | Keys.D) || keyData == Keys.F3)
        {
            SelectDataPath();
            return true;
        }
        if (keyData == Keys.F12)
        {
            OpenStartupConfiguration();
            return true;
        }

        Keys key = keyData & Keys.KeyCode;

        if (key == Keys.Down)
        {
            NavigateGrid(1);
            return true;
        }
        if (key == Keys.Up)
        {
            NavigateGrid(-1);
            return true;
        }
        if (key == Keys.PageDown)
        {
            NavigateGrid(5);
            return true;
        }
        if (key == Keys.PageUp)
        {
            NavigateGrid(-5);
            return true;
        }
        if (key == Keys.Enter)
        {
            ExecuteCurrentSelection();
            return true;
        }
        if (key == Keys.Escape)
        {
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void NavigateGrid(int delta)
    {
        if (gridCompanies.Rows.Count == 0) return;

        int currentIndex = -1;
        if (gridCompanies.CurrentRow != null && gridCompanies.CurrentRow.Index >= 0)
        {
            currentIndex = gridCompanies.CurrentRow.Index;
        }
        else if (gridCompanies.SelectedRows.Count > 0)
        {
            currentIndex = gridCompanies.SelectedRows[0].Index;
        }

        int targetIndex;
        if (currentIndex == -1)
        {
            targetIndex = delta > 0 ? 0 : gridCompanies.Rows.Count - 1;
        }
        else
        {
            targetIndex = Math.Clamp(currentIndex + delta, 0, gridCompanies.Rows.Count - 1);
        }

        gridCompanies.ClearSelection();
        gridCompanies.Rows[targetIndex].Selected = true;
        gridCompanies.CurrentCell = gridCompanies.Rows[targetIndex].Cells[1];

        try
        {
            if (!gridCompanies.Rows[targetIndex].Displayed)
            {
                gridCompanies.FirstDisplayedScrollingRowIndex = targetIndex;
            }
        }
        catch { }

        gridCompanies.Invalidate();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down)
        {
            NavigateGrid(1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up)
        {
            NavigateGrid(-1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            ExecuteCurrentSelection();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down)
        {
            NavigateGrid(1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up)
        {
            NavigateGrid(-1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            ExecuteCurrentSelection();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (CompanySelected || _isExiting || Disposing || IsDisposed)
        {
            base.OnFormClosing(e);
            return;
        }

        // IF COMPANY IS SELECTED THEN DO NOT DISPLAY POPUP TO EXIT APPLICATION
        if (_companyContext.IsCompanyOpen || _companyContext.CurrentCompany != null)
        {
            base.OnFormClosing(e);
            return;
        }

        if (e.CloseReason == CloseReason.ApplicationExitCall ||
            e.CloseReason == CloseReason.WindowsShutDown ||
            e.CloseReason == CloseReason.TaskManagerClosing)
        {
            base.OnFormClosing(e);
            return;
        }

        // Prompt user with Quit Confirmation Dialog
        bool confirmed = QuitConfirmationDialog.ShowQuitDialog(this);
        if (confirmed)
        {
            _isExiting = true;
            base.OnFormClosing(e);
            Application.Exit();
            Environment.Exit(0);
        }
        else
        {
            e.Cancel = true; // Return back to the Select Company screen
            BeginInvoke(() =>
            {
                if (gridCompanies != null && gridCompanies.Rows.Count > 0 && gridCompanies.CanFocus)
                {
                    gridCompanies.Focus();
                }
                else if (txtSearch != null && txtSearch.CanFocus)
                {
                    txtSearch.Focus();
                }
            });
        }
    }
}

/// <summary>
/// Data row view model for company selection grid
/// </summary>
public class CompanyGridRowItem
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyNumber { get; set; } = string.Empty;
    public string FinancialPeriod { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string LastSynchronized { get; set; } = string.Empty;
    public bool IsPasswordProtected { get; set; }
    public bool IsCurrentDefault { get; set; }
    public CompanySummaryDto? CompanyDto { get; set; }
}
