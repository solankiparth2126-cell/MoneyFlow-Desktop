using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Dialogs;

/// <summary>
/// First-Time Environment Setup and Regional Defaults Configuration Dialog.
/// Features:
/// - Dark Navy header with rocket badge.
/// - Descriptive subtitle explaining ledger engine initialization.
/// - Parameter cards for Country (India ₹ INR), Accounting Terminology (India / SAARC GST Compliant),
///   and Company Data Path with Browse support.
/// - Full keyboard navigation:
///     * 'A' or Enter or Alt+A -> Accept configuration & proceed
///     * 'C' or Alt+C         -> Configure / change data directory
///     * Esc                  -> Exit
/// </summary>
public class ApplicationStartupDialog : Form
{
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    [DllImport("gdi32.dll", EntryPoint = "CreateRoundRectRgn")]
    private static extern IntPtr CreateRoundRectRgn(
        int nLeftRect,
        int nTopRect,
        int nRightRect,
        int nBottomRect,
        int nWidthEllipse,
        int nHeightEllipse
    );

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;

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

    private static readonly string SetupMarkerPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "MyERP",
        "System",
        ".setup_completed"
    );

    public static string DefaultDataDirectory => MoneyFlow.Core.Common.CompanyPathConfiguration.ResolveDataRootPath();

    public string SelectedDataPath { get; private set; } = DefaultDataDirectory;
    public string SelectedCountry { get; private set; } = "India";
    public string SelectedCurrency { get; private set; } = "INR";
    public string SelectedTerminology { get; private set; } = "India / SAARC";

    private Guna2TextBox txtDataPath = null!;
    private Guna2Button btnBrowse = null!;
    private Guna2Button btnConfigure = null!;
    private Guna2Button btnAccept = null!;
    private bool _isConfirmingExit;

    public ApplicationStartupDialog(string? initialDataPath = null)
    {
        if (!string.IsNullOrWhiteSpace(initialDataPath))
        {
            SelectedDataPath = initialDataPath;
        }

        InitializeComponent();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int preference = DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }
        catch { }
        UpdateWindowRegion();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateWindowRegion();
    }

    private void UpdateWindowRegion()
    {
        if (IsHandleCreated && Width > 0 && Height > 0)
        {
            Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 10, 10));
        }
    }

    /// <summary>
    /// Determines whether the first-time setup wizard should be displayed.
    /// </summary>
    public static bool IsFirstTimeSetupRequired()
    {
        return !File.Exists(SetupMarkerPath);
    }

    /// <summary>
    /// Marks the first-time setup as completed by writing the system marker file.
    /// </summary>
    public static void MarkSetupCompleted(string? dataPath = null)
    {
        try
        {
            string resolvedPath = dataPath ?? DefaultDataDirectory;
            MoneyFlow.Core.Common.CompanyPathConfiguration.SetConfiguredPaths(resolvedPath, resolvedPath);

            string dir = Path.GetDirectoryName(SetupMarkerPath)!;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string content = $"SetupCompletedUtc={DateTime.UtcNow:O}\nDataPath={resolvedPath}\n";
            File.WriteAllText(SetupMarkerPath, content);
        }
        catch
        {
            // Silently allow marker creation to avoid blocking startup in restricted environments
        }
    }

    private void InitializeComponent()
    {
        AutoScaleMode = AutoScaleMode.None;
        Text = "Application Startup — MoneyFlow Desktop ERP";
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(17, 36, 58); // Outer frame border color (Deep Navy #11243A)
        Padding = new Padding(1); // 1px uniform perimeter border around all 4 sides!
        KeyPreview = true;
        DoubleBuffered = true;

        ScreenFittingManager.FitDialogToWorkArea(this, 740, 360, 640, 320);

        // Main Card Container (Pure White)
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(0)
        };
        Controls.Add(card);

        // ═══════════════════════════════════════════════════════════════
        // 1. HEADER PANEL (Deep Navy #11243A, 66px height)
        // ═══════════════════════════════════════════════════════════════
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 66,
            BackColor = Color.FromArgb(17, 36, 58)
        };

        // Window dragging support on header
        void DragHeader(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }
        pnlHeader.MouseDown += DragHeader;

        // Header Title Label
        var lblTitle = new Label
        {
            Text = "Application Startup",
            Font = new Font("Segoe UI", 12.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(70, 13),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblTitle.MouseDown += DragHeader;
        pnlHeader.Controls.Add(lblTitle);

        // Header Subtitle Label
        var lblSubtitle = new Label
        {
            Text = "Initial Environment Configuration  •  Regional Defaults",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184), // Slate-400 #94A3B8
            Location = new Point(71, 38),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblSubtitle.MouseDown += DragHeader;
        pnlHeader.Controls.Add(lblSubtitle);

        // Native Anti-Aliased Header Painting (Rocket badge and bottom divider)
        pnlHeader.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            // 1. Rocket Icon Badge Background (40x40 at x=20, y=13)
            int badgeX = 20, badgeY = 13, badgeW = 40, badgeH = 40;
            using (var badgePath = CreateRoundedRectanglePath(new Rectangle(badgeX, badgeY, badgeW, badgeH), 8))
            {
                using var bgBrush = new SolidBrush(Color.FromArgb(24, 48, 76));
                using var borderPen = new Pen(Color.FromArgb(38, 70, 105), 1f);
                e.Graphics.FillPath(bgBrush, badgePath);
                e.Graphics.DrawPath(borderPen, badgePath);
            }
            DrawRocketVector(e.Graphics, badgeX, badgeY);

            // 2. Subtle bottom separator line
            using var sepPen = new Pen(Color.FromArgb(30, 58, 90), 1f);
            e.Graphics.DrawLine(sepPen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        // Close Button (✕) on Header
        var btnClose = new Button
        {
            Text = "✕",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(180, 200, 220),
            Size = new Size(34, 34),
            Location = new Point(694, 16),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 29, 72); // Crimson red on hover
        btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(190, 18, 60);
        btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.White;
        btnClose.MouseLeave += (s, e) => btnClose.ForeColor = Color.FromArgb(180, 200, 220);
        btnClose.Click += (s, e) => PromptExitConfirmation();
        pnlHeader.Controls.Add(btnClose);

        card.Controls.Add(pnlHeader);

        // ═══════════════════════════════════════════════════════════════
        // 2. INSTRUCTION / DESCRIPTION TEXT (White background)
        // ═══════════════════════════════════════════════════════════════
        var pnlDescription = new Panel
        {
            Location = new Point(20, 78),
            Size = new Size(700, 36),
            BackColor = Color.Transparent
        };

        var lblDescription = new Label
        {
            Text = "MoneyFlow will initialize the ledger engine with the parameters below. Select Configure to adjust localization directories, or click Accept to continue.",
            Font = new Font("Segoe UI", 8.8F, FontStyle.Regular),
            ForeColor = Color.FromArgb(51, 65, 85), // Slate-700
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        pnlDescription.Controls.Add(lblDescription);
        card.Controls.Add(pnlDescription);

        // ═══════════════════════════════════════════════════════════════
        // 3. THREE PARAMETER CARDS (Rows with 8px radius, #F8FAFC)
        // ═══════════════════════════════════════════════════════════════
        int rowY = 120;
        int rowHeight = 44;
        int rowGap = 8;
        int rowWidth = 700;
        int rowX = 20;

        // --- ROW 1: Country ---
        var pnlCountryRow = CreateParameterCard(rowX, rowY, rowWidth, rowHeight);
        pnlCountryRow.Paint += (s, e) => DrawRowIcon(e.Graphics, RowIconType.Globe);

        var lblCountryTitle = CreateRowLabel("Country", 50);
        var lblCountryColon = CreateRowColon(220);
        var lblCountryValue = CreateRowValue("India", 240);

        // Pill: ₹ INR
        var pillCurrency = new Guna2Panel
        {
            Size = new Size(54, 22),
            Location = new Point(295, 11),
            FillColor = Color.FromArgb(254, 243, 199), // Amber-100
            BorderColor = Color.FromArgb(253, 230, 138), // Amber-200
            BorderThickness = 1,
            BorderRadius = 4
        };
        var lblCurrency = new Label
        {
            Text = "₹ INR",
            Font = new Font("Segoe UI", 7.8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(180, 83, 9), // Amber-700
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        pillCurrency.Controls.Add(lblCurrency);

        pnlCountryRow.Controls.Add(lblCountryTitle);
        pnlCountryRow.Controls.Add(lblCountryColon);
        pnlCountryRow.Controls.Add(lblCountryValue);
        pnlCountryRow.Controls.Add(pillCurrency);
        card.Controls.Add(pnlCountryRow);

        // --- ROW 2: Accounting Terminology ---
        rowY += rowHeight + rowGap;
        var pnlTermRow = CreateParameterCard(rowX, rowY, rowWidth, rowHeight);
        pnlTermRow.Paint += (s, e) => DrawRowIcon(e.Graphics, RowIconType.Document);

        var lblTermTitle = CreateRowLabel("Accounting Terminology", 50);
        var lblTermColon = CreateRowColon(220);
        var lblTermValue = CreateRowValue("India / SAARC", 240);

        // Pill: GST Compliant
        var pillGst = new Guna2Panel
        {
            Size = new Size(84, 22),
            Location = new Point(345, 11),
            FillColor = Color.FromArgb(224, 242, 254), // Sky-100
            BorderColor = Color.FromArgb(186, 230, 253), // Sky-200
            BorderThickness = 1,
            BorderRadius = 4
        };
        var lblGst = new Label
        {
            Text = "GST Compliant",
            Font = new Font("Segoe UI", 7.8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(2, 132, 199), // Sky-600
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        pillGst.Controls.Add(lblGst);

        pnlTermRow.Controls.Add(lblTermTitle);
        pnlTermRow.Controls.Add(lblTermColon);
        pnlTermRow.Controls.Add(lblTermValue);
        pnlTermRow.Controls.Add(pillGst);
        card.Controls.Add(pnlTermRow);

        // --- ROW 3: Company Data Path ---
        rowY += rowHeight + rowGap;
        var pnlPathRow = CreateParameterCard(rowX, rowY, rowWidth, rowHeight);
        pnlPathRow.Paint += (s, e) => DrawRowIcon(e.Graphics, RowIconType.Folder);

        var lblPathTitle = CreateRowLabel("Company Data Path", 50);
        var lblPathColon = CreateRowColon(220);

        // Path Box Container
        var pnlPathBox = new Guna2Panel
        {
            Size = new Size(430, 30),
            Location = new Point(240, 7),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(203, 213, 225),
            BorderThickness = 1,
            BorderRadius = 4
        };

        txtDataPath = new Guna2TextBox
        {
            Location = new Point(4, 2),
            Size = new Size(335, 26),
            BorderThickness = 0,
            FillColor = Color.White,
            Font = new Font("Consolas", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(30, 41, 59),
            Text = SelectedDataPath,
            ReadOnly = true
        };
        pnlPathBox.Controls.Add(txtDataPath);

        btnBrowse = new Guna2Button
        {
            Text = "Browse",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(2, 132, 199),
            FillColor = Color.Transparent,
            HoverState = { FillColor = Color.FromArgb(240, 249, 255) },
            Size = new Size(82, 26),
            Location = new Point(344, 2),
            Cursor = Cursors.Hand,
            BorderRadius = 3
        };
        btnBrowse.Click += (s, e) => ChooseDataPath();
        pnlPathBox.Controls.Add(btnBrowse);

        pnlPathRow.Controls.Add(lblPathTitle);
        pnlPathRow.Controls.Add(lblPathColon);
        pnlPathRow.Controls.Add(pnlPathBox);
        card.Controls.Add(pnlPathRow);

        // ═══════════════════════════════════════════════════════════════
        // 4. FOOTER BAR (Info hint on left, Configure & Accept on right)
        // ═══════════════════════════════════════════════════════════════
        int footerY = 286;

        // Left Navigation Hint
        var lblFooterHint = new Label
        {
            Text = "ⓘ Use keyboard shortcuts or mouse to navigate",
            Font = new Font("Segoe UI", 8.2F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(22, footerY + 8),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        card.Controls.Add(lblFooterHint);

        // Button: C: Configure
        btnConfigure = new Guna2Button
        {
            Text = "C: Configure",
            Font = new Font("Segoe UI", 8.8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(2, 132, 199), // Sky-600
            FillColor = Color.White,
            BorderColor = Color.FromArgb(2, 132, 199),
            BorderThickness = 1,
            BorderRadius = 4,
            HoverState = { FillColor = Color.FromArgb(240, 249, 255), BorderColor = Color.FromArgb(3, 105, 161) },
            Size = new Size(130, 36),
            Location = new Point(450, footerY),
            Cursor = Cursors.Hand
        };
        btnConfigure.Click += (s, e) => ChooseDataPath();
        card.Controls.Add(btnConfigure);

        // Button: ✓ A: Accept
        btnAccept = new Guna2Button
        {
            Text = "✓  A: Accept",
            Font = new Font("Segoe UI", 8.8F, FontStyle.Bold),
            ForeColor = Color.White,
            FillColor = Color.FromArgb(11, 30, 51), // Deep Navy
            HoverState = { FillColor = Color.FromArgb(19, 46, 74) },
            BorderRadius = 4,
            Size = new Size(130, 36),
            Location = new Point(590, footerY),
            Cursor = Cursors.Hand
        };
        btnAccept.Click += (s, e) => AcceptAndContinue();
        card.Controls.Add(btnAccept);

        // Keyboard Shortcut Handling
        KeyDown += OnDialogKeyDown;
    }

    private void OnDialogKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.A || (e.Alt && e.KeyCode == Keys.A) || e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            AcceptAndContinue();
        }
        else if (e.KeyCode == Keys.C || (e.Alt && e.KeyCode == Keys.C))
        {
            e.Handled = true;
            ChooseDataPath();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            PromptExitConfirmation();
        }
    }

    private void PromptExitConfirmation()
    {
        if (_isConfirmingExit) return;
        _isConfirmingExit = true;
        try
        {
            bool confirmed = QuitConfirmationDialog.ShowQuitDialog(this);
            if (confirmed)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }
        finally
        {
            _isConfirmingExit = false;
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (DialogResult != DialogResult.OK && e.CloseReason == CloseReason.UserClosing && !_isConfirmingExit)
        {
            _isConfirmingExit = true;
            bool confirmed = false;
            try
            {
                confirmed = QuitConfirmationDialog.ShowQuitDialog(this);
            }
            finally
            {
                _isConfirmingExit = false;
            }

            if (!confirmed)
            {
                e.Cancel = true;
                return;
            }
        }

        base.OnFormClosing(e);
    }

    private void ChooseDataPath()
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "Select Company Data Directory for MoneyFlow Desktop",
            SelectedPath = Directory.Exists(SelectedDataPath) ? SelectedDataPath : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            ShowNewFolderButton = true
        };

        if (fbd.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
        {
            SelectedDataPath = fbd.SelectedPath;
            txtDataPath.Text = SelectedDataPath;
        }
    }

    private void AcceptAndContinue()
    {
        try
        {
            // Ensure target data directory exists
            if (!Directory.Exists(SelectedDataPath))
            {
                Directory.CreateDirectory(SelectedDataPath);
            }

            // Mark setup completed
            MarkSetupCompleted(SelectedDataPath);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MoneyFlowMessageBox.Show(this, $"Failed creating selected data path: {ex.Message}", "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // UI HELPER METHODS FOR CRISP VECTOR RENDERING
    // ═══════════════════════════════════════════════════════════════

    private static Guna2Panel CreateParameterCard(int x, int y, int w, int h)
    {
        return new Guna2Panel
        {
            Location = new Point(x, y),
            Size = new Size(w, h),
            FillColor = Color.FromArgb(248, 250, 252), // Slate-50 #F8FAFC
            BorderColor = Color.FromArgb(226, 232, 240), // Slate-200 #E2E8F0
            BorderThickness = 1,
            BorderRadius = 6,
            BackColor = Color.Transparent
        };
    }

    private static Label CreateRowLabel(string text, int x)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(51, 65, 85), // Slate-700
            Location = new Point(x, 12),
            AutoSize = true,
            BackColor = Color.Transparent
        };
    }

    private static Label CreateRowColon(int x)
    {
        return new Label
        {
            Text = ":",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(x, 12),
            AutoSize = true,
            BackColor = Color.Transparent
        };
    }

    private static Label CreateRowValue(string text, int x)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42), // Slate-900
            Location = new Point(x, 12),
            AutoSize = true,
            BackColor = Color.Transparent
        };
    }

    private enum RowIconType
    {
        Globe,
        Document,
        Folder
    }

    private static void DrawRowIcon(Graphics g, RowIconType type)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(2, 132, 199), 1.6f); // Sky-600 #0284C7
        using var brush = new SolidBrush(Color.FromArgb(2, 132, 199));

        switch (type)
        {
            case RowIconType.Globe:
                // Globe circle
                g.DrawEllipse(pen, 18, 13, 18, 18);
                // Equator line
                g.DrawLine(pen, 18, 22, 36, 22);
                // Meridian curve
                g.DrawEllipse(pen, 22, 13, 10, 18);
                break;

            case RowIconType.Document:
                // Document shape with folded corner
                var docPts = new[]
                {
                    new Point(19, 13),
                    new Point(30, 13),
                    new Point(34, 17),
                    new Point(34, 31),
                    new Point(19, 31)
                };
                g.DrawPolygon(pen, docPts);
                // Inner lines
                g.DrawLine(pen, 23, 20, 30, 20);
                g.DrawLine(pen, 23, 24, 30, 24);
                break;

            case RowIconType.Folder:
                // Folder tab & body
                var tabPts = new[]
                {
                    new Point(18, 18),
                    new Point(18, 15),
                    new Point(24, 15),
                    new Point(26, 17),
                    new Point(35, 17),
                    new Point(35, 18)
                };
                g.DrawLines(pen, tabPts);
                // Folder body
                g.DrawRectangle(pen, 18, 18, 18, 13);
                break;
        }
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
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

    private static void DrawRocketVector(Graphics g, int ox, int oy)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var bodyBrush = new SolidBrush(Color.FromArgb(56, 189, 248)); // Sky Blue #38BDF8

        // Rocket Body
        var pts = new[]
        {
            new PointF(ox + 24, oy + 10),
            new PointF(ox + 29, oy + 15),
            new PointF(ox + 21, oy + 24),
            new PointF(ox + 16, oy + 19)
        };
        g.FillPolygon(bodyBrush, pts);

        // Left Fin
        var leftFin = new[] { new PointF(ox + 16, oy + 19), new PointF(ox + 11, oy + 23), new PointF(ox + 17, oy + 25) };
        g.FillPolygon(bodyBrush, leftFin);

        // Right Fin
        var rightFin = new[] { new PointF(ox + 21, oy + 24), new PointF(ox + 23, oy + 30), new PointF(ox + 27, oy + 24) };
        g.FillPolygon(bodyBrush, rightFin);

        // Window Circle
        using var winBrush = new SolidBrush(Color.FromArgb(17, 36, 58));
        g.FillEllipse(winBrush, ox + 19, oy + 16, 5, 5);

        // Flame exhaust
        using var flameBrush = new SolidBrush(Color.FromArgb(251, 146, 60)); // Orange #FB923C
        var flame = new[] { new PointF(ox + 16, oy + 25), new PointF(ox + 13, oy + 28), new PointF(ox + 19, oy + 28) };
        g.FillPolygon(flameBrush, flame);
    }
}
