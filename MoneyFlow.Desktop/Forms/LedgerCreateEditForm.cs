using System;
using System.Globalization;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Controls.Fields;
using MoneyFlow.Desktop.Controls.Lookup;
using MoneyFlow.Desktop.Navigation;

namespace MoneyFlow.Desktop.Forms;

public partial class LedgerCreateEditForm : Form
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
        int cornerPreference = 2; // DWMWCP_ROUND
        try { DwmSetWindowAttribute(Handle, 33, ref cornerPreference, sizeof(int)); } catch { }
        var margins = new MARGINS { leftWidth = 1, rightWidth = 1, topHeight = 1, bottomHeight = 1 };
        try { DwmExtendFrameIntoClientArea(Handle, ref margins); } catch { }
    }

    private readonly ILedgerService _ledgerService;
    private readonly IGroupService _groupService;
    private readonly ISettingsService? _settingsService;
    private readonly int _companyId;
    private readonly int? _ledgerId;

    private TextBox _txtName = null!;
    private MoneyFlowTextLookup _cmbGroup = null!;
    private NumericUpDown _numOpeningBalance = null!;
    private MoneyFlowTextLookup _cmbBalanceType = null!;
    private Button _btnDr = null!;
    private Button _btnCr = null!;
    private TextBox _txtEffectiveFrom = null!;
    private TextBox _txtAlias = null!;
    private Button _btnSave = null!;
    private Button _btnSaveAndNew = null!;
    private Button _btnCancel = null!;
    private Label _lblStatusBanner = null!;
    private System.Windows.Forms.Timer? _statusBannerTimer;
    private LedgerDetailDto? _existingLedger;

    private readonly int? _initialGroupId;

    public bool HasSavedAny { get; private set; }
    public int? LastSavedLedgerId { get; private set; }
    public event Action? LedgerSaved;

    public string EffectiveFromText => _txtEffectiveFrom?.Text ?? string.Empty;

    public LedgerCreateEditForm(
        ILedgerService ledgerService,
        IGroupService groupService,
        int companyId,
        int? ledgerId = null,
        int? initialGroupId = null,
        ISettingsService? settingsService = null)
    {
        _ledgerService = ledgerService;
        _groupService = groupService;
        _companyId = companyId;
        _ledgerId = ledgerId;
        _initialGroupId = initialGroupId;
        _settingsService = settingsService;

        InitializeComponent();
    }

    private string FormatEffectiveDate(DateTime date)
    {
        return _settingsService?.FormatDate(date) ?? date.ToString("dd-MM-yyyy");
    }

    private static GraphicsPath CreateRoundedRectangle(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        if (d > rect.Width) d = rect.Width;
        if (d > rect.Height) d = rect.Height;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static Control BuildFieldLabel(string text, bool required = false)
    {
        var pnl = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 20,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 4),
            BackColor = Color.Transparent
        };
        var lbl = new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            Margin = new Padding(0, 0, 3, 0)
        };
        pnl.Controls.Add(lbl);
        if (required)
        {
            var star = new Label
            {
                Text = "*",
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(239, 68, 68),
                Margin = new Padding(0)
            };
            pnl.Controls.Add(star);
        }
        return pnl;
    }

    private void InitializeComponent()
    {
        Text = "MoneyFlow Desktop — Ledger Master";
        var workingArea = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1280, 720);
        int targetWidth = Math.Min(1020, Math.Max(960, workingArea.Width - 40));
        int targetHeight = 330;
        Size = new Size(targetWidth, targetHeight);
        MinimumSize = new Size(880, targetHeight);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = true;
        MinimizeBox = true;
        BackColor = ExecLedgerTheme.ApplicationCanvas;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        KeyPreview = true;
        DoubleBuffered = true;

        var appIcon = ExecLedgerIcons.GetAppIcon();
        if (appIcon != null)
        {
            Icon = appIcon;
            ShowIcon = true;
        }

        Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(51, 65, 85), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };

        // -------- TOP DARK TITLE BAR (Image 3 style) --------
        var darkTitleBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(10, 16, 29) // Deep dark navy/black matching Reference Image 3
        };
        darkTitleBar.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, 0x0112, 0xF010 + 2, 0);
            }
        };

        var picTitleLogo = new PictureBox
        {
            Image = ExecLedgerIcons.GetAppLogo(18, 18),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(18, 18),
            Location = new Point(14, 9),
            BackColor = Color.Transparent
        };
        picTitleLogo.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0x0112, 0xF010 + 2, 0); } };
        darkTitleBar.Controls.Add(picTitleLogo);

        var titleBarText = new Label
        {
            Text = "MoneyFlow Desktop — " + (_ledgerId.HasValue ? "Alter Ledger" : "Create Ledger"),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(38, 9),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        titleBarText.MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0x0112, 0xF010 + 2, 0); } };
        darkTitleBar.Controls.Add(titleBarText);

        var winBtnsPnl = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        var btnMin = new Button
        {
            Text = "—",
            Size = new Size(42, 36),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Margin = new Padding(0),
            Cursor = Cursors.Hand
        };
        btnMin.FlatAppearance.BorderSize = 0;
        btnMin.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 41, 59);
        btnMin.Click += (s, e) => WindowState = FormWindowState.Minimized;

        var btnMax = new Button
        {
            Text = "口",
            Size = new Size(42, 36),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Margin = new Padding(0),
            Cursor = Cursors.Hand
        };
        btnMax.FlatAppearance.BorderSize = 0;
        btnMax.FlatAppearance.MouseOverBackColor = Color.FromArgb(30, 41, 59);
        btnMax.Click += (s, e) => WindowState = (WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized);

        var btnClose = new Button
        {
            Text = "✕",
            Size = new Size(46, 36),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            Margin = new Padding(0),
            Cursor = Cursors.Hand
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 38, 38);
        btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(185, 28, 28);
        btnClose.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        winBtnsPnl.Controls.Add(btnMin);
        winBtnsPnl.Controls.Add(btnMax);
        winBtnsPnl.Controls.Add(btnClose);
        darkTitleBar.Controls.Add(winBtnsPnl);

        // -------- TOP HEADER CARD --------
        var headerCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = Color.White
        };
        headerCard.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, headerCard.Height - 1, headerCard.Width, headerCard.Height - 1);
        };

        var titleFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Left,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Padding = new Padding(20, 13, 0, 0),
            BackColor = Color.Transparent
        };

        var picLogo = new PictureBox
        {
            Image = ExecLedgerIcons.GetAppLogo(28, 28),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(28, 28),
            Margin = new Padding(0, 1, 10, 0),
            BackColor = Color.Transparent
        };

        var screenTitle = new Label
        {
            Text = _ledgerId.HasValue ? "ALTER LEDGER" : "CREATE NEW LEDGER",
            Font = new Font("Segoe UI", 15F, FontStyle.Bold),
            ForeColor = ExecLedgerTheme.DeepNavy,
            AutoSize = true,
            Margin = new Padding(0),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent
        };

        titleFlow.Controls.Add(picLogo);
        titleFlow.Controls.Add(screenTitle);

        _lblStatusBanner = new Label
        {
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(22, 101, 52),
            BackColor = Color.FromArgb(220, 252, 231),
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(14, 3, 0, 0),
            AutoSize = true,
            Visible = false
        };
        _lblStatusBanner.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(134, 239, 172), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, _lblStatusBanner.Width - 1, _lblStatusBanner.Height - 1);
        };
        titleFlow.Controls.Add(_lblStatusBanner);

        headerCard.Controls.Add(titleFlow);

        var tipPnl = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Right,
            Padding = new Padding(0, 14, 20, 0),
            BackColor = Color.Transparent
        };
        var lblTab = new Label
        {
            Text = "Tab",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            BackColor = Color.Transparent,
            AutoSize = false,
            Size = new Size(38, 24),
            Margin = new Padding(0, 0, 6, 0)
        };
        lblTab.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, lblTab.Width - 1, lblTab.Height - 1);
            using var path = CreateRoundedRectangle(r, 4);
            using var bg = new SolidBrush(Color.FromArgb(241, 245, 249));
            g.FillPath(bg, path);
            using var pen = new Pen(Color.FromArgb(203, 213, 225), 1);
            g.DrawPath(pen, path);
            TextRenderer.DrawText(g, "Tab", new Font("Segoe UI", 8.5F, FontStyle.Bold),
                r, Color.FromArgb(71, 85, 105),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        tipPnl.Controls.Add(lblTab);

        tipPnl.Controls.Add(new Label
        {
            Text = "Next Field",
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Margin = new Padding(0, 4, 18, 0),
            AutoSize = true
        });

        var lblCtrlS = new Label
        {
            Text = "Ctrl+S",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            BackColor = Color.Transparent,
            AutoSize = false,
            Size = new Size(52, 24),
            Margin = new Padding(0, 0, 6, 0)
        };
        lblCtrlS.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, lblCtrlS.Width - 1, lblCtrlS.Height - 1);
            using var path = CreateRoundedRectangle(r, 4);
            using var bg = new SolidBrush(Color.FromArgb(241, 245, 249));
            g.FillPath(bg, path);
            using var pen = new Pen(Color.FromArgb(203, 213, 225), 1);
            g.DrawPath(pen, path);
            TextRenderer.DrawText(g, "Ctrl+S", new Font("Segoe UI", 8.5F, FontStyle.Bold),
                r, Color.FromArgb(71, 85, 105),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        tipPnl.Controls.Add(lblCtrlS);

        tipPnl.Controls.Add(new Label
        {
            Text = "Quick Save",
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Margin = new Padding(0, 4, 0, 0),
            AutoSize = true
        });

        headerCard.Controls.Add(tipPnl);

        // ===================== SECTION: PRIMARY CLASSIFICATION & BALANCES =====================
        var primaryCardWrapper = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 14, 24, 14),
            BackColor = Color.Transparent
        };
        var primaryCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(18, 14, 18, 14),
            Margin = new Padding(0)
        };
        primaryCard.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, primaryCard.Width - 1, primaryCard.Height - 1);
            using var path = CreateRoundedRectangle(rect, 8);
            using var bg = new SolidBrush(Color.White);
            g.FillPath(bg, path);
            using var borderPen = new Pen(Color.FromArgb(226, 232, 240), 1);
            g.DrawPath(borderPen, path);
        };
        primaryCardWrapper.Controls.Add(primaryCard);

        var primaryTlp = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(0),
            Margin = new Padding(0),
            BackColor = Color.Transparent
        };
        primaryTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        primaryTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        primaryTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        primaryTlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        primaryTlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        primaryTlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));

        // Row 0: Ledger Name (50%) + Under Group (50%)
        var pName = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0), BackColor = Color.Transparent };
        pName.Controls.Add(BuildFieldLabel("Ledger Name", true));
        var nameBox = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.White,
            Padding = new Padding(10, 7, 6, 5)
        };
        nameBox.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, nameBox.Width - 1, nameBox.Height - 1);
            using var path = CreateRoundedRectangle(rect, 6);
            using var bg = new SolidBrush(Color.White);
            g.FillPath(bg, path);
            using var pen = new Pen(Color.FromArgb(203, 213, 225), 1);
            g.DrawPath(pen, path);
        };
        _txtName = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            BorderStyle = BorderStyle.None,
            ForeColor = Color.FromArgb(15, 23, 42),
            PlaceholderText = "e.g. Acme Industrial Solutions Pvt Ltd"
        };
        var altNBadge = new Label
        {
            Dock = DockStyle.Right,
            Width = 56,
            BackColor = Color.Transparent,
            Cursor = Cursors.Default,
            Margin = new Padding(0)
        };
        altNBadge.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 1, altNBadge.Width - 1, altNBadge.Height - 3);
            using var path = CreateRoundedRectangle(rect, 4);
            using var bg = new SolidBrush(Color.FromArgb(248, 250, 252));
            g.FillPath(bg, path);
            using var pen = new Pen(Color.FromArgb(203, 213, 225), 1);
            g.DrawPath(pen, path);
            TextRenderer.DrawText(g, "ALT + N", new Font("Segoe UI", 7.5F, FontStyle.Bold),
                rect, Color.FromArgb(100, 116, 139),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        nameBox.Controls.Add(_txtName);
        nameBox.Controls.Add(altNBadge);
        _txtName.BringToFront();
        pName.Controls.Add(nameBox);
        nameBox.BringToFront();
        primaryTlp.Controls.Add(pName, 0, 0);
        primaryTlp.SetColumnSpan(pName, 2);

        var pGrp = new Panel { Dock = DockStyle.Fill, Margin = new Padding(10, 0, 0, 0), BackColor = Color.Transparent };
        pGrp.Controls.Add(BuildFieldLabel("Under Group", true));
        var grpBox = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.White,
            Padding = new Padding(2, 2, 2, 2)
        };
        grpBox.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, grpBox.Width - 1, grpBox.Height - 1);
            using var path = CreateRoundedRectangle(rect, 6);
            using var bg = new SolidBrush(Color.White);
            g.FillPath(bg, path);
            using var pen = new Pen(Color.FromArgb(203, 213, 225), 1);
            g.DrawPath(pen, path);
        };
        var grpChevron = new Label
        {
            Dock = DockStyle.Right,
            Width = 28,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        grpChevron.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(100, 116, 139), 1.8F);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            int cx = grpChevron.Width / 2;
            int cy = grpChevron.Height / 2;
            g.DrawLine(pen, cx - 4, cy - 2, cx, cy + 2);
            g.DrawLine(pen, cx, cy + 2, cx + 4, cy - 2);
        };
        _cmbGroup = new MoneyFlowTextLookup
        {
            Dock = DockStyle.Fill,
            Height = 32,
            MaximumSize = new Size(0, 32),
            Margin = new Padding(0),
            DrawBorder = false,
            PlaceholderText = "Sundry Debtors (Receivables)"
        };
        _cmbGroup.CreateRequested += OnCreateGroupRequested;
        _cmbGroup.SelectionCommitted += (s, e) =>
        {
            BeginInvoke(new Action(() => FocusField(2)));
        };
        grpChevron.Click += (s, e) => _cmbGroup.Focus();
        grpBox.Controls.Add(_cmbGroup);
        grpBox.Controls.Add(grpChevron);
        _cmbGroup.BringToFront();
        pGrp.Controls.Add(grpBox);
        grpBox.BringToFront();
        primaryTlp.Controls.Add(pGrp, 2, 0);
        primaryTlp.SetColumnSpan(pGrp, 2);

        // Row 1: Opening Balance (25%) + Balance Type (25%) + Effective From (25%) + Alias/Code (25%)
        var pOb = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 0), BackColor = Color.Transparent };
        pOb.Controls.Add(BuildFieldLabel("Opening Balance (₹)", false));
        var obPnl = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = Color.White,
            Padding = new Padding(12, 8, 12, 6)
        };
        bool obFocused = false;
        obPnl.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, obPnl.Width - 1, obPnl.Height - 1);
            using var path = CreateRoundedRectangle(rect, 6);
            using var bg = new SolidBrush(Color.White);
            g.FillPath(bg, path);
            Color borderColor = obFocused ? ExecLedgerTheme.SystemFocusBlue : Color.FromArgb(203, 213, 225);
            float borderWidth = obFocused ? 1.5F : 1F;
            using var pen = new Pen(borderColor, borderWidth);
            g.DrawPath(pen, path);
        };
        var sym = new Label
        {
            Text = "₹",
            Dock = DockStyle.Left,
            Width = 24,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            TextAlign = ContentAlignment.MiddleLeft,
            BackColor = Color.Transparent,
            Cursor = Cursors.IBeam
        };
        _numOpeningBalance = new CleanNumericUpDown
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            DecimalPlaces = 2,
            Maximum = 999999999,
            Minimum = 0,
            ThousandsSeparator = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = Color.FromArgb(15, 23, 42),
            TextAlign = HorizontalAlignment.Right,
            Margin = new Padding(0)
        };
        _numOpeningBalance.Enter += (s, e) => { obFocused = true; obPnl.Invalidate(); };
        _numOpeningBalance.Leave += (s, e) => { obFocused = false; obPnl.Invalidate(); };
        sym.Click += (s, e) => _numOpeningBalance.Focus();
        obPnl.Click += (s, e) => _numOpeningBalance.Focus();

        var numHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 2, 4, 0)
        };
        numHost.Controls.Add(_numOpeningBalance);
        obPnl.Controls.Add(numHost);
        obPnl.Controls.Add(sym);
        numHost.BringToFront();
        pOb.Controls.Add(obPnl);
        obPnl.BringToFront();
        primaryTlp.Controls.Add(pOb, 0, 1);

        var pBt = new Panel { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 8, 0), BackColor = Color.Transparent };
        pBt.Controls.Add(BuildFieldLabel("Balance Type", false));
        var segPnl = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(3),
            BackColor = Color.FromArgb(241, 245, 249)
        };
        segPnl.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, segPnl.Width - 1, segPnl.Height - 1);
            using var path = CreateRoundedRectangle(rect, 6);
            using var bg = new SolidBrush(Color.FromArgb(241, 245, 249));
            g.FillPath(bg, path);
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            g.DrawPath(pen, path);
        };
        _btnDr = new Button
        {
            Text = "",
            Dock = DockStyle.Left,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Cursor = Cursors.Hand,
            TabIndex = 3
        };
        _btnDr.FlatAppearance.BorderSize = 0;
        _btnCr = new Button
        {
            Text = "",
            Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Margin = new Padding(0),
            Cursor = Cursors.Hand,
            TabIndex = 4
        };
        _btnCr.FlatAppearance.BorderSize = 0;
        segPnl.Resize += (s, e) =>
        {
            int half = (segPnl.ClientSize.Width - segPnl.Padding.Horizontal) / 2;
            _btnDr.Width = Math.Max(70, half);
        };

        _cmbBalanceType = new MoneyFlowTextLookup { Visible = false };
        var balProvider = new EnumLookupProvider<BalanceType>(b => b == BalanceType.Debit ? "Debit (Dr)" : "Credit (Cr)");
        _cmbBalanceType.SetProvider(balProvider, new LookupConfig
        {
            Title = "BALANCE TYPE",
            Placeholder = "Dr / Cr",
            AllowClear = false
        });
        bool isUpdatingBalanceType = false;

        _btnDr.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bt = (BalanceType?)_cmbBalanceType.SelectedValue ?? BalanceType.Debit;
            if (bt == BalanceType.Debit)
            {
                var rect = new Rectangle(0, 0, _btnDr.Width - 1, _btnDr.Height - 1);
                using var path = CreateRoundedRectangle(rect, 5);
                using var bg = new SolidBrush(Color.White);
                g.FillPath(bg, path);
                using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
                g.DrawPath(pen, path);
                TextRenderer.DrawText(g, "Debit (Dr)", new Font("Segoe UI", 9F, FontStyle.Bold),
                    rect, Color.FromArgb(37, 99, 235),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                var rect = new Rectangle(0, 0, _btnDr.Width, _btnDr.Height);
                TextRenderer.DrawText(g, "Debit (Dr)", new Font("Segoe UI", 9F, FontStyle.Regular),
                    rect, Color.FromArgb(100, 116, 139),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        };

        _btnCr.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bt = (BalanceType?)_cmbBalanceType.SelectedValue ?? BalanceType.Debit;
            if (bt == BalanceType.Credit)
            {
                var rect = new Rectangle(0, 0, _btnCr.Width - 1, _btnCr.Height - 1);
                using var path = CreateRoundedRectangle(rect, 5);
                using var bg = new SolidBrush(Color.White);
                g.FillPath(bg, path);
                using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
                g.DrawPath(pen, path);
                TextRenderer.DrawText(g, "Credit (Cr)", new Font("Segoe UI", 9F, FontStyle.Bold),
                    rect, Color.FromArgb(37, 99, 235),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                var rect = new Rectangle(0, 0, _btnCr.Width, _btnCr.Height);
                TextRenderer.DrawText(g, "Credit (Cr)", new Font("Segoe UI", 9F, FontStyle.Regular),
                    rect, Color.FromArgb(100, 116, 139),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        };

        void SetBalanceTypeUi(BalanceType bt)
        {
            _btnDr.Invalidate();
            _btnCr.Invalidate();
        }

        _cmbBalanceType.SelectedValue = BalanceType.Debit;
        SetBalanceTypeUi(BalanceType.Debit);

        _btnDr.Click += (s, e) =>
        {
            if (isUpdatingBalanceType) return;
            isUpdatingBalanceType = true;
            try
            {
                _cmbBalanceType.SelectedValue = BalanceType.Debit;
                SetBalanceTypeUi(BalanceType.Debit);
            }
            finally
            {
                isUpdatingBalanceType = false;
            }
        };

        _btnCr.Click += (s, e) =>
        {
            if (isUpdatingBalanceType) return;
            isUpdatingBalanceType = true;
            try
            {
                _cmbBalanceType.SelectedValue = BalanceType.Credit;
                SetBalanceTypeUi(BalanceType.Credit);
            }
            finally
            {
                isUpdatingBalanceType = false;
            }
        };

        void HandleBalanceKey(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.D)
            {
                _cmbBalanceType.SelectedValue = BalanceType.Debit;
                SetBalanceTypeUi(BalanceType.Debit);
                _btnDr.Focus();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Right || e.KeyCode == Keys.C)
            {
                _cmbBalanceType.SelectedValue = BalanceType.Credit;
                SetBalanceTypeUi(BalanceType.Credit);
                _btnCr.Focus();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Space)
            {
                var cur = (BalanceType?)_cmbBalanceType.SelectedValue ?? BalanceType.Debit;
                var next = cur == BalanceType.Debit ? BalanceType.Credit : BalanceType.Debit;
                _cmbBalanceType.SelectedValue = next;
                SetBalanceTypeUi(next);
                if (next == BalanceType.Debit) _btnDr.Focus(); else _btnCr.Focus();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }
        _btnDr.KeyDown += (s, e) => HandleBalanceKey(e);
        _btnCr.KeyDown += (s, e) => HandleBalanceKey(e);

        _cmbBalanceType.SelectedValueChanged += (s, e) =>
        {
            if (isUpdatingBalanceType) return;
            isUpdatingBalanceType = true;
            try
            {
                var bt = (BalanceType?)_cmbBalanceType.SelectedValue ?? BalanceType.Debit;
                SetBalanceTypeUi(bt);
            }
            finally
            {
                isUpdatingBalanceType = false;
            }
        };

        segPnl.Controls.Add(_btnDr);
        segPnl.Controls.Add(_btnCr);
        segPnl.Resize += (s, e) =>
        {
            int w = (segPnl.ClientSize.Width - 6) / 2;
            _btnDr.Width = Math.Max(0, w);
        };
        segPnl.HandleCreated += (s, e) =>
        {
            int w = (segPnl.ClientSize.Width - 6) / 2;
            _btnDr.Width = Math.Max(0, w);
        };
        pBt.Controls.Add(segPnl);
        pBt.Controls.Add(_cmbBalanceType);
        segPnl.BringToFront();
        primaryTlp.Controls.Add(pBt, 1, 1);

        var pEff = new Panel { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 8, 0), BackColor = Color.Transparent };
        pEff.Controls.Add(BuildFieldLabel("Effective From", false));
        var effBox = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = Color.White,
            Padding = new Padding(10, 8, 8, 4)
        };
        bool effFocused = false;
        effBox.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, effBox.Width - 1, effBox.Height - 1);
            using var path = CreateRoundedRectangle(rect, 6);
            using var bg = new SolidBrush(Color.White);
            g.FillPath(bg, path);
            Color borderColor = effFocused ? ExecLedgerTheme.SystemFocusBlue : Color.FromArgb(203, 213, 225);
            float borderWidth = effFocused ? 1.5F : 1F;
            using var pen = new Pen(borderColor, borderWidth);
            g.DrawPath(pen, path);
        };
        _txtEffectiveFrom = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            BorderStyle = BorderStyle.None,
            ForeColor = Color.FromArgb(15, 23, 42),
            Margin = new Padding(0),
            PlaceholderText = "DD-MM-YYYY"
        };
        _txtEffectiveFrom.Enter += (s, e) => { effFocused = true; effBox.Invalidate(); };
        _txtEffectiveFrom.Leave += (s, e) => { effFocused = false; effBox.Invalidate(); };
        effBox.Click += (s, e) => _txtEffectiveFrom.Focus();
        if (!_ledgerId.HasValue)
        {
            _txtEffectiveFrom.Text = FormatEffectiveDate(DateTime.Today);
        }
        effBox.Controls.Add(_txtEffectiveFrom);
        pEff.Controls.Add(effBox);
        effBox.BringToFront();
        primaryTlp.Controls.Add(pEff, 2, 1);

        var pAl = new Panel { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 0), BackColor = Color.Transparent };
        pAl.Controls.Add(BuildFieldLabel("Alias / Code", false));
        var alBox = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = Color.White,
            Padding = new Padding(10, 8, 8, 4)
        };
        bool alFocused = false;
        alBox.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, alBox.Width - 1, alBox.Height - 1);
            using var path = CreateRoundedRectangle(rect, 6);
            using var bg = new SolidBrush(Color.White);
            g.FillPath(bg, path);
            Color borderColor = alFocused ? ExecLedgerTheme.SystemFocusBlue : Color.FromArgb(203, 213, 225);
            float borderWidth = alFocused ? 1.5F : 1F;
            using var pen = new Pen(borderColor, borderWidth);
            g.DrawPath(pen, path);
        };
        _txtAlias = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            BorderStyle = BorderStyle.None,
            ForeColor = Color.FromArgb(15, 23, 42),
            Margin = new Padding(0),
            PlaceholderText = "e.g. CUST-0104"
        };
        _txtAlias.Enter += (s, e) => { alFocused = true; alBox.Invalidate(); };
        _txtAlias.Leave += (s, e) => { alFocused = false; alBox.Invalidate(); };
        alBox.Click += (s, e) => _txtAlias.Focus();
        alBox.Controls.Add(_txtAlias);
        pAl.Controls.Add(alBox);
        alBox.BringToFront();
        primaryTlp.Controls.Add(pAl, 3, 1);

        primaryCard.Controls.Add(primaryTlp);
        primaryTlp.Dock = DockStyle.Fill;

        // ===================== FOOTER ACTION BAR =====================
        var footerBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = Color.FromArgb(248, 250, 252),
            Padding = new Padding(0)
        };
        footerBar.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, footerBar.Width, 0);
        };

        var tipFlow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Left,
            Padding = new Padding(22, 16, 0, 0),
            BackColor = Color.Transparent
        };
        var tipDivider = new Label
        {
            Text = "|",
            Font = new Font("Segoe UI", 11F, FontStyle.Regular),
            ForeColor = Color.FromArgb(203, 213, 225),
            AutoSize = true,
            Margin = new Padding(0, 2, 8, 0)
        };
        var tipPrefix = new Label
        {
            Text = "Press",
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            Margin = new Padding(0, 4, 6, 0)
        };
        var tipKey = new Label
        {
            Text = "Ctrl+Enter",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85),
            BackColor = Color.Transparent,
            AutoSize = false,
            Size = new Size(74, 24),
            Margin = new Padding(0, 2, 6, 0)
        };
        tipKey.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new Rectangle(0, 0, tipKey.Width - 1, tipKey.Height - 1);
            using var path = CreateRoundedRectangle(r, 4);
            using var bg = new SolidBrush(Color.FromArgb(241, 245, 249));
            g.FillPath(bg, path);
            using var pen = new Pen(Color.FromArgb(203, 213, 225), 1);
            g.DrawPath(pen, path);
            TextRenderer.DrawText(g, "Ctrl+Enter", new Font("Segoe UI", 8.5F, FontStyle.Bold),
                r, Color.FromArgb(51, 65, 85),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        var tipSuffix = new Label
        {
            Text = "to quick commit",
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0)
        };
        tipFlow.Controls.Add(tipDivider);
        tipFlow.Controls.Add(tipPrefix);
        tipFlow.Controls.Add(tipKey);
        tipFlow.Controls.Add(tipSuffix);
        footerBar.Controls.Add(tipFlow);

        var btnFlow = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Right,
            Padding = new Padding(0, 10, 20, 0),
            BackColor = Color.Transparent
        };
        _btnCancel = new Button
        {
            Text = "",
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(116, 36),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 10, 0)
        };
        _btnCancel.FlatAppearance.BorderSize = 0;
        _btnCancel.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, _btnCancel.Width - 1, _btnCancel.Height - 1);
            using var path = CreateRoundedRectangle(rect, 6);
            using var bg = new SolidBrush(Color.White);
            g.FillPath(bg, path);
            using var pen = new Pen(Color.FromArgb(203, 213, 225), 1);
            g.DrawPath(pen, path);

            TextRenderer.DrawText(g, "Cancel", new Font("Segoe UI", 9.5F, FontStyle.Regular),
                new Rectangle(14, 0, _btnCancel.Width - 54, _btnCancel.Height),
                Color.FromArgb(30, 41, 59),
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            var chip = new Rectangle(_btnCancel.Width - 44, 9, 34, 18);
            using var chipPath = CreateRoundedRectangle(chip, 4);
            using var chipBg = new SolidBrush(Color.FromArgb(248, 250, 252));
            g.FillPath(chipBg, chipPath);
            using var chipPen = new Pen(Color.FromArgb(203, 213, 225), 1);
            g.DrawPath(chipPen, chipPath);
            TextRenderer.DrawText(g, "Esc", new Font("Segoe UI", 8F, FontStyle.Bold),
                chip, Color.FromArgb(71, 85, 105),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        _btnCancel.Click += (s, e) =>
        {
            MoneyFlowEscController.HandleEsc(
                ActiveControl, this,
                closeAction: () =>
                {
                    DialogResult = HasSavedAny ? DialogResult.OK : DialogResult.Cancel;
                    Close();
                });
        };

        _btnSaveAndNew = new Button
        {
            Text = "",
            UseMnemonic = false,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(150, 36),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 10, 0)
        };
        _btnSaveAndNew.FlatAppearance.BorderSize = 0;
        _btnSaveAndNew.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, _btnSaveAndNew.Width - 1, _btnSaveAndNew.Height - 1);
            using var path = CreateRoundedRectangle(rect, 6);
            using var bg = new SolidBrush(Color.FromArgb(239, 246, 255));
            g.FillPath(bg, path);
            using var pen = new Pen(Color.FromArgb(191, 219, 254), 1);
            g.DrawPath(pen, path);

            TextRenderer.DrawText(g, "Save & New", new Font("Segoe UI", 9.5F, FontStyle.Bold),
                new Rectangle(14, 0, _btnSaveAndNew.Width - 62, _btnSaveAndNew.Height),
                ExecLedgerTheme.SystemFocusBlue,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

            var chip = new Rectangle(_btnSaveAndNew.Width - 52, 9, 42, 18);
            using var chipPath = CreateRoundedRectangle(chip, 4);
            using var chipBg = new SolidBrush(Color.FromArgb(219, 234, 254));
            g.FillPath(chipBg, chipPath);
            using var chipPen = new Pen(Color.FromArgb(147, 197, 253), 1);
            g.DrawPath(chipPen, chipPath);
            TextRenderer.DrawText(g, "Alt+S", new Font("Segoe UI", 8F, FontStyle.Bold),
                chip, ExecLedgerTheme.SystemFocusBlue,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        _btnSaveAndNew.Click += async (s, e) => await OnSaveAndNewAsync();

        _btnSave = new Button
        {
            Text = "",
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(198, 36),
            Cursor = Cursors.Hand,
            Margin = new Padding(0)
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, _btnSave.Width - 1, _btnSave.Height - 1);
            using var path = CreateRoundedRectangle(rect, 6);
            using var bg = new SolidBrush(Color.FromArgb(30, 58, 138));
            g.FillPath(bg, path);

            using var checkPen = new Pen(Color.White, 2.2F);
            checkPen.StartCap = LineCap.Round;
            checkPen.EndCap = LineCap.Round;
            g.DrawLine(checkPen, 16, 18, 20, 22);
            g.DrawLine(checkPen, 20, 22, 28, 12);

            string btnText = _ledgerId.HasValue ? "Update Ledger" : "Save Ledger";
            TextRenderer.DrawText(g, btnText, new Font("Segoe UI", 9.5F, FontStyle.Bold),
                new Rectangle(34, 0, _btnSave.Width - 96, _btnSave.Height),
                Color.White,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            var chip = new Rectangle(_btnSave.Width - 62, 9, 52, 18);
            using var chipPath = CreateRoundedRectangle(chip, 4);
            using var chipBrush = new SolidBrush(Color.FromArgb(29, 78, 216));
            g.FillPath(chipBrush, chipPath);
            using var chipPen = new Pen(Color.FromArgb(59, 130, 246));
            g.DrawPath(chipPen, chipPath);
            TextRenderer.DrawText(g, "↵ Enter", new Font("Segoe UI", 7.5F, FontStyle.Bold),
                chip, Color.FromArgb(239, 246, 255),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };
        _btnSave.Click += async (s, e) => await OnSaveAsync();

        btnFlow.Controls.Add(_btnCancel);
        btnFlow.Controls.Add(_btnSaveAndNew);
        btnFlow.Controls.Add(_btnSave);

        footerBar.Controls.Add(btnFlow);

        // ===================== FINAL FORM CONTROL TREE (TARGET GEOMETRY) =====================
        Controls.Clear();
        Controls.Add(primaryCardWrapper);      // Dock = Fill (contains primary classification card)
        Controls.Add(footerBar);               // Dock = Bottom (fixed action bar)
        Controls.Add(headerCard);              // Dock = Top (banner header with title & shortcut keys)
        Controls.Add(darkTitleBar);            // Dock = Top (dark navy title bar 36px)

        // Ensure proper Top dock stacking order:
        // darkTitleBar (topmost) -> headerCard -> footerBar -> primaryCardWrapper (fills center)
        footerBar.SendToBack();
        headerCard.SendToBack();
        darkTitleBar.SendToBack();
        primaryCardWrapper.BringToFront();

        Load += async (s, e) => await OnFormLoadAsync();
        Shown += (s, e) => { _txtName.Focus(); _txtName.SelectAll(); };
    }

    public int GetCurrentFieldIndex()
    {
        if (_txtName != null && (_txtName.Focused || _txtName.ContainsFocus)) return 0;
        if (_cmbGroup != null && (_cmbGroup.Focused || _cmbGroup.ContainsFocus)) return 1;
        if (_numOpeningBalance != null && (_numOpeningBalance.Focused || _numOpeningBalance.ContainsFocus)) return 2;
        if ((_btnDr != null && _btnDr.Focused) || (_btnCr != null && _btnCr.Focused)) return 3;
        if (_txtEffectiveFrom != null && (_txtEffectiveFrom.Focused || _txtEffectiveFrom.ContainsFocus)) return 4;
        if (_txtAlias != null && (_txtAlias.Focused || _txtAlias.ContainsFocus)) return 5;
        if (_btnCancel != null && _btnCancel.Focused) return 6;
        if (_btnSaveAndNew != null && _btnSaveAndNew.Focused) return 7;
        if (_btnSave != null && _btnSave.Focused) return 8;
        return -1;
    }

    public void FocusField(int index)
    {
        switch (index)
        {
            case 0:
                _txtName?.Focus();
                _txtName?.SelectAll();
                break;
            case 1:
                _cmbGroup?.Focus();
                break;
            case 2:
                _numOpeningBalance?.Focus();
                var tb = _numOpeningBalance?.Controls.OfType<TextBox>().FirstOrDefault();
                if (tb != null)
                {
                    tb.Focus();
                    tb.SelectAll();
                }
                break;
            case 3:
                var bt = (BalanceType?)_cmbBalanceType.SelectedValue ?? BalanceType.Debit;
                if (bt == BalanceType.Credit) _btnCr?.Focus();
                else _btnDr?.Focus();
                break;
            case 4:
                _txtEffectiveFrom?.Focus();
                _txtEffectiveFrom?.SelectAll();
                break;
            case 5:
                _txtAlias?.Focus();
                _txtAlias?.SelectAll();
                break;
            case 6:
                _btnCancel?.Focus();
                break;
            case 7:
                _btnSaveAndNew?.Focus();
                break;
            case 8:
                _btnSave?.Focus();
                break;
        }
    }

    public void MoveToNextField()
    {
        int cur = GetCurrentFieldIndex();
        if (cur >= 0 && cur < 5)
        {
            FocusField(cur + 1);
        }
        else if (cur == 5)
        {
            _btnSave?.Focus();
        }
        else if (cur == 8) // _btnSave
        {
            _btnCancel?.Focus();
        }
        else if (cur == 6) // _btnCancel
        {
            _btnSaveAndNew?.Focus();
        }
        else if (cur == 7) // _btnSaveAndNew
        {
            FocusField(0);
        }
        else
        {
            FocusField(0);
        }
    }

    public void MoveToPreviousField()
    {
        int cur = GetCurrentFieldIndex();
        if (cur > 0 && cur <= 5)
        {
            FocusField(cur - 1);
        }
        else if (cur == 8) // _btnSave
        {
            FocusField(5); // _txtAlias
        }
        else if (cur == 7) // _btnSaveAndNew
        {
            _btnCancel?.Focus();
        }
        else if (cur == 6) // _btnCancel
        {
            _btnSave?.Focus();
        }
        else if (cur == 0)
        {
            _btnSaveAndNew?.Focus();
        }
        else
        {
            FocusField(0);
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            if (MoneyFlow.Desktop.Controls.Lookup.MoneyFlowLookupPopup.WasAnyPopupJustClosedByEsc || (_cmbGroup != null && _cmbGroup.IsPopupOpen))
            {
                _cmbGroup?.ClosePopup();
                return true;
            }

            return MoneyFlowEscController.HandleEsc(
                ActiveControl,
                this,
                closeAction: () =>
                {
                    DialogResult = HasSavedAny ? DialogResult.OK : DialogResult.Cancel;
                    Close();
                });
        }
        if (keyData == (Keys.Alt | Keys.S))
        {
            if (_btnSaveAndNew != null && _btnSaveAndNew.Enabled)
            {
                _ = OnSaveAndNewAsync();
                return true;
            }
        }
        if (keyData == (Keys.Control | Keys.S) || keyData == (Keys.Control | Keys.Enter))
        {
            if (_btnSave != null && _btnSave.Enabled)
            {
                _ = OnSaveAsync();
                return true;
            }
        }

        // 1. Shift+Tab or Shift+Enter -> Previous Field
        if (keyData == (Keys.Tab | Keys.Shift) || keyData == (Keys.Enter | Keys.Shift))
        {
            MoveToPreviousField();
            return true;
        }

        // 2. Tab -> Next Field
        if (keyData == Keys.Tab)
        {
            if (_cmbGroup != null && _cmbGroup.IsPopupOpen)
            {
                _cmbGroup.CloseLookup();
            }
            MoveToNextField();
            return true;
        }

        // 3. Plain Enter -> Next Field (when on input fields, unless lookup popup is open)
        if (keyData == Keys.Enter && !keyData.HasFlag(Keys.Alt) && !keyData.HasFlag(Keys.Control))
        {
            if (_cmbGroup != null && _cmbGroup.IsPopupOpen)
            {
                return base.ProcessCmdKey(ref msg, keyData);
            }

            int cur = GetCurrentFieldIndex();
            if (cur >= 0 && cur <= 5)
            {
                MoveToNextField();
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async Task OnSaveAndNewAsync()
    {
        var name = _txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MoneyFlowMessageBox.Show("Please enter a ledger name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtName.Focus();
            return;
        }

        var groupId = (int?)_cmbGroup.SelectedValue ?? 0;
        if (groupId <= 0)
        {
            MoneyFlowMessageBox.Show("Please select an under group.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _cmbGroup.Focus();
            return;
        }

        var effDateStr = _txtEffectiveFrom.Text.Trim();
        string[] dateFormats = { "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };
        if (string.IsNullOrWhiteSpace(effDateStr) ||
            (!DateTime.TryParseExact(effDateStr, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _) &&
             !DateTime.TryParse(effDateStr, out _)))
        {
            MoneyFlowMessageBox.Show("Please enter a valid Effective From date (e.g. DD-MM-YYYY).", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtEffectiveFrom.Focus();
            return;
        }

        var balanceType = (BalanceType)(_cmbBalanceType.SelectedValue ?? BalanceType.Debit);

        try
        {
            _btnSave.Enabled = false;
            _btnSaveAndNew.Enabled = false;

            string savedName = name;
            if (_ledgerId.HasValue)
            {
                var updateDto = new LedgerUpdateDto
                {
                    LedgerId = _ledgerId.Value,
                    GroupId = groupId,
                    LedgerName = name,
                    OpeningBalance = _numOpeningBalance.Value,
                    OpeningBalanceType = balanceType,
                    Address = _existingLedger?.Address ?? string.Empty,
                    State = _existingLedger?.State ?? string.Empty,
                    Phone = _existingLedger?.Phone ?? string.Empty,
                    Email = _existingLedger?.Email ?? string.Empty,
                    PAN = _existingLedger?.PAN ?? string.Empty,
                    BankName = _existingLedger?.BankName ?? string.Empty,
                    BankAccountNumber = _existingLedger?.BankAccountNumber ?? string.Empty,
                    IFSC = _existingLedger?.IFSC ?? string.Empty,
                    CreditLimit = _existingLedger?.CreditLimit ?? 0m,
                    CreditDays = _existingLedger?.CreditDays ?? 0,
                    IsActive = _existingLedger?.IsActive ?? true
                };
                await _ledgerService.UpdateLedgerAsync(updateDto);
                LastSavedLedgerId = _ledgerId.Value;
            }
            else
            {
                var createDto = new LedgerCreateDto
                {
                    GroupId = groupId,
                    LedgerName = name,
                    OpeningBalance = _numOpeningBalance.Value,
                    OpeningBalanceType = balanceType
                };
                var created = await _ledgerService.CreateLedgerAsync(_companyId, createDto);
                LastSavedLedgerId = created.LedgerId;
            }

            HasSavedAny = true;
            LedgerSaved?.Invoke();

            if (Owner is LedgerListForm listForm)
            {
                _ = listForm.LoadLedgersFromDatabaseAsync(selectLedgerId: LastSavedLedgerId);
            }

            ShowSuccessStatus($"✓ Ledger '{savedName}' saved successfully! Enter next ledger.");

            _txtName.Text = string.Empty;
            _numOpeningBalance.Value = 0;
            _cmbBalanceType.SelectedValue = BalanceType.Debit;
            _txtEffectiveFrom.Text = FormatEffectiveDate(DateTime.Today);
            _txtAlias.Text = string.Empty;
            if (_initialGroupId.HasValue)
            {
                _cmbGroup.SelectedValue = _initialGroupId.Value;
            }
            else
            {
                _cmbGroup.SelectedValue = null;
            }
            _txtName.Focus();
        }
        catch (Exception ex)
        {
            MoneyFlowMessageBox.Show($"Failed to save ledger: {ex.Message}", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _btnSave.Enabled = true;
            _btnSaveAndNew.Enabled = true;
        }
    }

    private async Task OnFormLoadAsync()
    {
        try
        {
            var groups = await _groupService.GetGroupsByCompanyAsync(_companyId);
            var groupMap = groups.ToDictionary(g => g.GroupId);
            string ResolvePath(GroupSummaryDto g)
            {
                var stack = new List<string> { g.GroupName };
                var cur = g.ParentGroupId;
                var visited = new HashSet<int> { g.GroupId };
                while (cur.HasValue && visited.Add(cur.Value) && groupMap.TryGetValue(cur.Value, out var parent))
                {
                    stack.Insert(0, parent.GroupName);
                    cur = parent.ParentGroupId;
                }
                return string.Join(" > ", stack);
            }

            var groupItems = groups
                .OrderBy(g => ResolvePath(g))
                .Select(g => new LookupItem
                {
                    Id = g.GroupId,
                    Name = g.GroupName,
                    Subtitle = ResolvePath(g),
                    Code = g.Nature.ToString(),
                    RawData = g
                }).ToList();

            var groupProvider = new ListLookupProvider<LookupItem>(groupItems, x => x.Id, x => x.Name, x => x.Code, x => x.Subtitle);
            _cmbGroup.SetProvider(groupProvider, new LookupConfig
            {
                Title = "LIST OF LEDGER GROUPS",
                Placeholder = "Select Under Group...",
                AllowCreate = true,
                CreateButtonText = "New Group",
                AllowClear = false
            });

            if (_ledgerId.HasValue)
            {
                _existingLedger = await _ledgerService.GetLedgerDetailsAsync(_ledgerId.Value);
                if (_existingLedger != null)
                {
                    _txtName.Text = _existingLedger.LedgerName;
                    _cmbGroup.SelectedValue = _existingLedger.GroupId;
                    _numOpeningBalance.Value = _existingLedger.OpeningBalance;
                    _cmbBalanceType.SelectedValue = _existingLedger.OpeningBalanceType;
                    _txtEffectiveFrom.Text = _existingLedger.CreatedAt != default
                        ? FormatEffectiveDate(_existingLedger.CreatedAt)
                        : FormatEffectiveDate(DateTime.Today);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_txtEffectiveFrom.Text))
                {
                    _txtEffectiveFrom.Text = FormatEffectiveDate(DateTime.Today);
                }
                if (_initialGroupId.HasValue)
                {
                    _cmbGroup.SelectedValue = _initialGroupId.Value;
                }
            }
        }
        catch (Exception ex)
        {
            MoneyFlowMessageBox.Show($"Failed to load ledger data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task OnSaveAsync()
    {
        var name = _txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MoneyFlowMessageBox.Show("Please enter a ledger name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtName.Focus();
            return;
        }

        var groupId = (int?)_cmbGroup.SelectedValue ?? 0;
        if (groupId <= 0)
        {
            MoneyFlowMessageBox.Show("Please select an under group.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _cmbGroup.Focus();
            return;
        }

        var effDateStr = _txtEffectiveFrom.Text.Trim();
        string[] dateFormats = { "dd-MM-yyyy", "d-M-yyyy", "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" };
        if (string.IsNullOrWhiteSpace(effDateStr) ||
            (!DateTime.TryParseExact(effDateStr, dateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out _) &&
             !DateTime.TryParse(effDateStr, out _)))
        {
            MoneyFlowMessageBox.Show("Please enter a valid Effective From date (e.g. DD-MM-YYYY).", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtEffectiveFrom.Focus();
            return;
        }

        var balanceType = (BalanceType)(_cmbBalanceType.SelectedValue ?? BalanceType.Debit);

        try
        {
            _btnSave.Enabled = false;

            if (_ledgerId.HasValue)
            {
                var updateDto = new LedgerUpdateDto
                {
                    LedgerId = _ledgerId.Value,
                    GroupId = groupId,
                    LedgerName = name,
                    OpeningBalance = _numOpeningBalance.Value,
                    OpeningBalanceType = balanceType,
                    Address = _existingLedger?.Address ?? string.Empty,
                    State = _existingLedger?.State ?? string.Empty,
                    Phone = _existingLedger?.Phone ?? string.Empty,
                    Email = _existingLedger?.Email ?? string.Empty,
                    PAN = _existingLedger?.PAN ?? string.Empty,
                    BankName = _existingLedger?.BankName ?? string.Empty,
                    BankAccountNumber = _existingLedger?.BankAccountNumber ?? string.Empty,
                    IFSC = _existingLedger?.IFSC ?? string.Empty,
                    CreditLimit = _existingLedger?.CreditLimit ?? 0m,
                    CreditDays = _existingLedger?.CreditDays ?? 0,
                    IsActive = _existingLedger?.IsActive ?? true
                };

                await _ledgerService.UpdateLedgerAsync(updateDto);
                LastSavedLedgerId = _ledgerId.Value;
            }
            else
            {
                var createDto = new LedgerCreateDto
                {
                    GroupId = groupId,
                    LedgerName = name,
                    OpeningBalance = _numOpeningBalance.Value,
                    OpeningBalanceType = balanceType
                };

                var created = await _ledgerService.CreateLedgerAsync(_companyId, createDto);
                LastSavedLedgerId = created.LedgerId;
            }

            HasSavedAny = true;
            LedgerSaved?.Invoke();

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MoneyFlowMessageBox.Show($"Failed to save ledger: {ex.Message}", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _btnSave.Enabled = true;
        }
    }

    private void OnCreateGroupRequested()
    {
        using var groupForm = new GroupCreateEditForm(_groupService, _companyId);
        if (groupForm.ShowDialog(this) == DialogResult.OK)
        {
            _ = OnFormLoadAsync();
        }
    }

    private void ShowSuccessStatus(string message)
    {
        if (_lblStatusBanner == null) return;
        _lblStatusBanner.Text = message;
        _lblStatusBanner.Visible = true;

        _statusBannerTimer?.Stop();
        _statusBannerTimer?.Dispose();
        _statusBannerTimer = new System.Windows.Forms.Timer { Interval = 4000 };
        _statusBannerTimer.Tick += (s, e) =>
        {
            _statusBannerTimer?.Stop();
            if (!_lblStatusBanner.IsDisposed)
            {
                _lblStatusBanner.Visible = false;
            }
        };
        _statusBannerTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _statusBannerTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
