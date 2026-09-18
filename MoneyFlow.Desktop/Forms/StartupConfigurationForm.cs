using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Data.Storage;
using MoneyFlow.Desktop.Dialogs;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// First-Time Application Startup & Initial Environment Configuration Screen.
/// Clean, compact desktop ERP layout matching professional specification:
/// - Country and Accounting Terminology as clean informational defaults
/// - Company Data Path as the ONLY editable setting with fully visible Browse button
/// - Single primary [ ✓ Accept ] action button in the footer
/// - Multi-layer Close Confirmation Dialog with semi-transparent dark overlay (58% dimming)
/// - Zero database configuration — 100% local file storage (company.data)
/// </summary>
public class StartupConfigurationForm : Form
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
        int nHeightEllipse);

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

    // ═══════════════════════════════════════════════════════════════
    //  CONTROLS & FIELDS
    // ═══════════════════════════════════════════════════════════════

    // Main Card
    private Guna2Panel pnlCard = null!;

    // Header
    private Guna2Panel pnlHeader = null!;
    private Guna2Panel pnlRocketBadge = null!;
    private Label lblTitle = null!;
    private Label lblSubtitle = null!;
    private Guna2Button btnWindowMinimize = null!;
    private Guna2Button btnWindowClose = null!;

    // Close Dialog State
    private bool _isCloseDialogActive = false;
    private bool _setupAccepted = false;

    // Body
    private Panel pnlBody = null!;
    private Label lblErrorMessage = null!;

    // Card 1: Country
    private Guna2Panel pnlCountryCard = null!;
    private Label lblCountryTitle = null!;
    private Label lblCountryColon = null!;
    private Label lblCountryValue = null!;
    private Guna2Panel pnlCurrencyBadge = null!;
    private Label lblCurrencyBadge = null!;

    // Card 2: Accounting Terminology
    private Guna2Panel pnlTerminologyCard = null!;
    private Label lblTermTitle = null!;
    private Label lblTermColon = null!;
    private Label lblTermValue = null!;
    private Guna2Panel pnlComplianceBadge = null!;
    private Label lblComplianceBadge = null!;

    // Card 3: Company Data Path (Editable)
    private Guna2Panel pnlPathCard = null!;
    private Label lblPathTitle = null!;
    private Label lblPathColon = null!;
    private Guna2Panel pnlPathInnerBox = null!;
    private TextBox txtDataPath = null!;
    private Guna2Button btnBrowse = null!;

    // Footer
    private Guna2Panel pnlFooter = null!;
    private Label lblFooterHint = null!;
    private Guna2Button btnAccept = null!;

    public SystemConfiguration Config { get; private set; }

    // ═══════════════════════════════════════════════════════════════
    //  CONSTRUCTOR & INITIALIZATION
    // ═══════════════════════════════════════════════════════════════

    public StartupConfigurationForm(SystemConfiguration? initialConfig = null)
    {
        Config = initialConfig ?? new SystemConfiguration
        {
            CompanyDataPath = SystemEnvironmentManager.GetDefaultCompanyDataPath()
        };

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Application Startup — Initial Environment Configuration";
        Size = new Size(780, 330);
        MinimumSize = new Size(780, 330);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(11, 40, 71); // Form border tone matching executive navy
        KeyPreview = true;
        DoubleBuffered = true;

        var appIcon = ExecLedgerIcons.GetAppIcon();
        if (appIcon != null)
        {
            Icon = appIcon;
            ShowIcon = true;
        }

        // Form elevation shadow
        _ = new Guna2ShadowForm
        {
            TargetForm = this,
            ShadowColor = Color.FromArgb(15, 23, 42),
            BorderRadius = 12
        };

        // Center Card (Elevated container filling form)
        pnlCard = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(203, 213, 225),
            BorderThickness = 1,
            BorderRadius = 12,
            Padding = new Padding(0)
        };
        Controls.Add(pnlCard);

        BuildHeaderSection();
        BuildFooterSection();
        BuildBodySection();

        AcceptButton = btnAccept;
        CancelButton = btnWindowClose;

        ApplyRoundedRegion();
        Resize += (s, e) => ApplyRoundedRegion();

        // Keyboard handler
        KeyDown += StartupConfigurationForm_KeyDown;
    }

    private void ApplyRoundedRegion()
    {
        if (Width > 0 && Height > 0)
        {
            var rgn = CreateRoundRectRgn(0, 0, Width, Height, 14, 14);
            if (rgn != IntPtr.Zero)
            {
                Region = Region.FromHrgn(rgn);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  1. HEADER SECTION (Deep Navy with Rocket & Window Controls)
    // ═══════════════════════════════════════════════════════════════

    private void BuildHeaderSection()
    {
        pnlHeader = new Guna2Panel
        {
            Dock = DockStyle.Top,
            Height = 72,
            FillColor = Color.FromArgb(11, 40, 71), // #0B2847 Executive Deep Navy
            BorderRadius = 12,
            CustomizableEdges = { TopLeft = true, TopRight = true, BottomLeft = false, BottomRight = false }
        };
        pnlCard.Controls.Add(pnlHeader);

        // Allow dragging window by header
        void DragHeader(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }
        pnlHeader.MouseDown += DragHeader;

        // App Logo Badge
        pnlRocketBadge = new Guna2Panel
        {
            Size = new Size(42, 42),
            Location = new Point(20, 15),
            FillColor = Color.FromArgb(15, 53, 92),
            BorderColor = Color.FromArgb(30, 78, 122),
            BorderThickness = 1,
            BorderRadius = 8
        };
        pnlRocketBadge.MouseDown += DragHeader;
        var picAppLogo = new PictureBox
        {
            Image = ExecLedgerIcons.GetAppLogo(30, 30),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(30, 30),
            Location = new Point(6, 6),
            BackColor = Color.Transparent
        };
        picAppLogo.MouseDown += DragHeader;
        pnlRocketBadge.Controls.Add(picAppLogo);
        pnlHeader.Controls.Add(pnlRocketBadge);

        // Title
        lblTitle = new Label
        {
            Text = "Application Startup",
            Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(72, 15),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblTitle.MouseDown += DragHeader;
        pnlHeader.Controls.Add(lblTitle);

        // Subtitle
        lblSubtitle = new Label
        {
            Text = "Initial Environment Configuration & Regional Defaults",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(72, 40),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblSubtitle.MouseDown += DragHeader;
        pnlHeader.Controls.Add(lblSubtitle);

        // Window Minimize Button [—]
        btnWindowMinimize = new Guna2Button
        {
            Text = "—",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            FillColor = Color.Transparent,
            BorderThickness = 0,
            Size = new Size(32, 32),
            Location = new Point(pnlHeader.Width - 76, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        btnWindowMinimize.HoverState.ForeColor = Color.White;
        btnWindowMinimize.Click += (s, e) => WindowState = FormWindowState.Minimized;
        pnlHeader.Controls.Add(btnWindowMinimize);

        // Window Close Button [✕]
        btnWindowClose = new Guna2Button
        {
            Text = "✕",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            FillColor = Color.Transparent,
            BorderThickness = 0,
            Size = new Size(32, 32),
            Location = new Point(pnlHeader.Width - 42, 20),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        btnWindowClose.HoverState.ForeColor = Color.White;
        new ToolTip().SetToolTip(btnWindowClose, "Close [Esc]");
        btnWindowClose.Click += (s, e) => Close();
        pnlHeader.Controls.Add(btnWindowClose);

        pnlHeader.Resize += (s, e) =>
        {
            btnWindowClose.Location = new Point(pnlHeader.Width - 42, 20);
            btnWindowMinimize.Location = new Point(pnlHeader.Width - 76, 20);
        };
    }


    // ═══════════════════════════════════════════════════════════════
    //  2. FOOTER SECTION (Navigation Hint + Accept Button)
    // ═══════════════════════════════════════════════════════════════

    private void BuildFooterSection()
    {
        pnlFooter = new Guna2Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            FillColor = ExecLedgerTheme.SecondarySurface, // #F8FAFC
            BorderColor = ExecLedgerTheme.GridBorder, // #E2E8F0
            CustomizableEdges = { TopLeft = false, TopRight = false, BottomLeft = true, BottomRight = true }
        };
        pnlFooter.Paint += (s, e) =>
        {
            using var pen = new Pen(ExecLedgerTheme.GridBorder, 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0);
        };
        pnlCard.Controls.Add(pnlFooter);

        // Left Navigation Hint with Shortcut Keys
        lblFooterHint = new Label
        {
            Text = "ⓘ  Press [Enter] to Accept  •  [Esc] to Close",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(24, 18),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlFooter.Controls.Add(lblFooterHint);

        // Single Primary Action: [ ✓ Accept [Enter] ]
        btnAccept = new Guna2Button
        {
            Text = "✓   Accept [Enter]",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.White,
            FillColor = Color.FromArgb(11, 40, 71), // Match header Deep Navy
            BorderThickness = 0,
            BorderRadius = 6,
            Size = new Size(150, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        btnAccept.HoverState.FillColor = Color.FromArgb(15, 53, 92);
        btnAccept.Click += BtnAccept_Click;
        pnlFooter.Controls.Add(btnAccept);

        pnlFooter.Resize += (s, e) =>
        {
            btnAccept.Location = new Point(pnlFooter.Width - 174, 10);
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  3. BODY SECTION (Country, Terminology, Company Data Path)
    // ═══════════════════════════════════════════════════════════════

    private void BuildBodySection()
    {
        pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(24, 16, 24, 12)
        };
        pnlCard.Controls.Add(pnlBody);
        pnlBody.BringToFront();

        // ─── Unified Grid Coordinates ─────────────────────────────
        // Column 1: Icon centered around X = 16..40
        // Column 2: Label at X = 52
        // Column 3: Colon at X = 236
        // Column 4: Value / Control at X = 256

        // ─── Card 1: Country (Informational Default) ───────────────
        pnlCountryCard = CreateFieldCard(16, 48);
        pnlCountryCard.Paint += (s, e) => PaintCardIcon(e.Graphics, "Globe");

        lblCountryTitle = new Label
        {
            Text = "Country",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(52, 14),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlCountryCard.Controls.Add(lblCountryTitle);

        lblCountryColon = new Label
        {
            Text = ":",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(236, 14),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlCountryCard.Controls.Add(lblCountryColon);

        lblCountryValue = new Label
        {
            Text = Config.Country,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(256, 14),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlCountryCard.Controls.Add(lblCountryValue);

        pnlCurrencyBadge = new Guna2Panel
        {
            Size = new Size(62, 24),
            Location = new Point(318, 12),
            FillColor = Color.FromArgb(254, 243, 199), // Warm amber background
            BorderColor = Color.FromArgb(253, 230, 138),
            BorderThickness = 1,
            BorderRadius = 4
        };
        lblCurrencyBadge = new Label
        {
            Text = $"{Config.CurrencySymbol} {Config.CurrencyCode}".Trim(),
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(180, 83, 9),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        pnlCurrencyBadge.Controls.Add(lblCurrencyBadge);
        pnlCountryCard.Controls.Add(pnlCurrencyBadge);
        pnlBody.Controls.Add(pnlCountryCard);

        // ─── Card 2: Accounting Terminology (Informational Default) ──
        pnlTerminologyCard = CreateFieldCard(74, 48);
        pnlTerminologyCard.Paint += (s, e) => PaintCardIcon(e.Graphics, "Document");

        lblTermTitle = new Label
        {
            Text = "Accounting Terminology",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(52, 14),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlTerminologyCard.Controls.Add(lblTermTitle);

        lblTermColon = new Label
        {
            Text = ":",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(236, 14),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlTerminologyCard.Controls.Add(lblTermColon);

        lblTermValue = new Label
        {
            Text = Config.AccountingTerminology,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(256, 14),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlTerminologyCard.Controls.Add(lblTermValue);

        pnlComplianceBadge = new Guna2Panel
        {
            Size = new Size(60, 24),
            Location = new Point(372, 12),
            FillColor = Color.FromArgb(224, 242, 254), // Light blue background
            BorderColor = Color.FromArgb(186, 230, 253),
            BorderThickness = 1,
            BorderRadius = 4
        };
        lblComplianceBadge = new Label
        {
            Text = string.IsNullOrWhiteSpace(Config.ComplianceProfile) ? "GST" : Config.ComplianceProfile,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(2, 132, 199),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        pnlComplianceBadge.Controls.Add(lblComplianceBadge);
        pnlTerminologyCard.Controls.Add(pnlComplianceBadge);
        pnlBody.Controls.Add(pnlTerminologyCard);

        // ─── Card 3: Company Data Path (ONLY EDITABLE SETTING) ───────
        pnlPathCard = CreateFieldCard(132, 50);
        pnlPathCard.Paint += (s, e) => PaintCardIcon(e.Graphics, "Folder");

        lblPathTitle = new Label
        {
            Text = "Company Data Path",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(52, 15),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlPathCard.Controls.Add(lblPathTitle);

        lblPathColon = new Label
        {
            Text = ":",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(236, 15),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlPathCard.Controls.Add(lblPathColon);

        // Browse Button
        btnBrowse = new Guna2Button
        {
            Text = "Browse",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(2, 132, 199),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(2, 132, 199),
            BorderThickness = 1,
            BorderRadius = 4,
            Size = new Size(92, 32),
            Location = new Point(pnlPathCard.Width - 106, 9),
            Cursor = Cursors.Hand
        };
        btnBrowse.HoverState.FillColor = Color.FromArgb(240, 249, 255);
        btnBrowse.Click += BtnBrowse_Click;
        pnlPathCard.Controls.Add(btnBrowse);

        // Text box container
        pnlPathInnerBox = new Guna2Panel
        {
            Location = new Point(256, 9),
            Size = new Size(pnlPathCard.Width - 106 - 12 - 256, 32),
            FillColor = Color.FromArgb(248, 250, 252),
            BorderColor = Color.FromArgb(203, 213, 225),
            BorderThickness = 1,
            BorderRadius = 4
        };

        txtDataPath = new TextBox
        {
            Text = Config.CompanyDataPath,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(15, 23, 42),
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(248, 250, 252),
            Location = new Point(8, 6),
            Width = pnlPathInnerBox.Width - 16,
            ReadOnly = false
        };
        txtDataPath.TextChanged += (s, e) =>
        {
            Config.CompanyDataPath = txtDataPath.Text.Trim();
            HideError();
        };
        pnlPathInnerBox.Controls.Add(txtDataPath);
        pnlPathCard.Controls.Add(pnlPathInnerBox);

        pnlBody.Controls.Add(pnlPathCard);

        // Error message banner below controls (hidden by default)
        lblErrorMessage = new Label
        {
            Text = "",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = ExecLedgerTheme.ErrorRed,
            BackColor = ExecLedgerTheme.ErrorBg,
            Location = new Point(24, 186),
            Size = new Size(732, 20),
            Padding = new Padding(6, 2, 6, 2),
            Visible = false
        };
        pnlBody.Controls.Add(lblErrorMessage);

        // Responsive resizing
        pnlBody.Resize += (s, e) =>
        {
            int contentWidth = pnlBody.Width - 48;
            lblErrorMessage.Width = contentWidth;
            pnlCountryCard.Width = contentWidth;
            pnlTerminologyCard.Width = contentWidth;
            pnlPathCard.Width = contentWidth;

            btnBrowse.Location = new Point(contentWidth - 106, 9);
            pnlPathInnerBox.Width = Math.Max(260, btnBrowse.Left - 12 - 256);
            txtDataPath.Width = pnlPathInnerBox.Width - 16;
        };
    }

    private Guna2Panel CreateFieldCard(int top, int height)
    {
        return new Guna2Panel
        {
            Location = new Point(24, top),
            Size = new Size(732, height),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(226, 232, 240),
            BorderThickness = 1,
            BorderRadius = 6
        };
    }

    private void PaintCardIcon(Graphics g, string type)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(Color.FromArgb(2, 132, 199), 1.8f);

        switch (type)
        {
            case "Globe":
                g.DrawEllipse(pen, 18, 14, 20, 20);
                g.DrawEllipse(pen, 24, 14, 8, 20);
                g.DrawLine(pen, 18, 24, 38, 24);
                break;

            case "Document":
                var docPts = new[]
                {
                    new Point(19, 13),
                    new Point(32, 13),
                    new Point(37, 18),
                    new Point(37, 33),
                    new Point(19, 33)
                };
                g.DrawPolygon(pen, docPts);
                g.DrawLine(pen, 23, 21, 33, 21);
                g.DrawLine(pen, 23, 25, 33, 25);
                g.DrawLine(pen, 23, 29, 30, 29);
                break;

            case "Folder":
                var folderPts = new[]
                {
                    new Point(18, 16),
                    new Point(25, 16),
                    new Point(28, 19),
                    new Point(38, 19),
                    new Point(38, 33),
                    new Point(18, 33)
                };
                g.DrawPolygon(pen, folderPts);
                g.DrawLine(pen, 18, 19, 38, 19);
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  EVENT HANDLERS & ACTIONS
    // ═══════════════════════════════════════════════════════════════

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select Company Data Root Directory for MoneyFlow",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = Directory.Exists(txtDataPath.Text.Trim())
                ? txtDataPath.Text.Trim()
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            var selected = dialog.SelectedPath.Trim();
            txtDataPath.Text = selected;
            Config.CompanyDataPath = selected;
            HideError();
        }
    }

    private void BtnAccept_Click(object? sender, EventArgs e)
    {
        var targetPath = txtDataPath.Text.Trim();

        // 1. Validate Company Data Path (local filesystem only, reject SQL/database strings)
        if (!SystemEnvironmentManager.ValidateDataPath(targetPath, out var errorMsg))
        {
            ShowError(errorMsg ?? "The selected company data path is invalid. Please select another location.");
            pnlPathCard.BorderColor = ExecLedgerTheme.ErrorRed;
            return;
        }

        pnlPathCard.BorderColor = Color.FromArgb(226, 232, 240);
        HideError();

        // 2. Initialize environment & atomically commit configuration
        try
        {
            Cursor = Cursors.WaitCursor;
            btnAccept.Enabled = false;
            btnBrowse.Enabled = false;

            Config.CompanyDataPath = targetPath;
            Config.Country = !string.IsNullOrWhiteSpace(Config.Country) ? Config.Country : "India";
            Config.CurrencySymbol = !string.IsNullOrWhiteSpace(Config.CurrencySymbol) ? Config.CurrencySymbol : "₹";
            Config.CurrencyCode = !string.IsNullOrWhiteSpace(Config.CurrencyCode) ? Config.CurrencyCode : "INR";
            Config.AccountingTerminology = !string.IsNullOrWhiteSpace(Config.AccountingTerminology) ? Config.AccountingTerminology : "India / SAARC";
            Config.ComplianceProfile = !string.IsNullOrWhiteSpace(Config.ComplianceProfile) ? Config.ComplianceProfile : "GST";
            Config.FinancialYearCycle = !string.IsNullOrWhiteSpace(Config.FinancialYearCycle) ? Config.FinancialYearCycle : "01-Apr to 31-Mar";
            Config.DecimalPrecision = !string.IsNullOrWhiteSpace(Config.DecimalPrecision) ? Config.DecimalPrecision : "2 Decimals (0.00)";
            Config.DecimalPlaces = Config.DecimalPlaces > 0 ? Config.DecimalPlaces : 2;

            // Initializes System, Companies, Backups, Logs and writes installation.dat + system.config atomically
            SystemEnvironmentManager.InitializeEnvironment(targetPath, Config);

            _setupAccepted = true;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to initialize local environment: {ex.Message}");
        }
        finally
        {
            Cursor = Cursors.Default;
            btnAccept.Enabled = true;
            btnBrowse.Enabled = true;
        }
    }

    private void ShowError(string message)
    {
        lblErrorMessage.Text = $"⚠  {message}";
        lblErrorMessage.Visible = true;
    }

    private void HideError()
    {
        lblErrorMessage.Visible = false;
    }

    // ═══════════════════════════════════════════════════════════════
    //  CLOSE INTERCEPTION & MODAL CONFIRMATION OVERLAY
    // ═══════════════════════════════════════════════════════════════

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // 1. If setup was accepted, allow form to close cleanly
        if (_setupAccepted || DialogResult == DialogResult.OK)
        {
            base.OnFormClosing(e);
            return;
        }

        // 2. Prevent duplicate / cascading confirmation dialogs
        if (_isCloseDialogActive)
        {
            e.Cancel = true;
            return;
        }

        // 3. Show modern MyERP Close Confirmation Dialog with semi-transparent dark backdrop overlay
        var shouldExit = ConfirmClose();
        if (!shouldExit)
        {
            // User selected [ Continue Setup ] -> preserve all state and return focus
            e.Cancel = true;
            txtDataPath.Focus();
            return;
        }

        // User selected [ Exit MyERP ] -> clean exit without completing setup
        DialogResult = DialogResult.Cancel;
        base.OnFormClosing(e);
    }

    private bool ConfirmClose()
    {
        if (_isCloseDialogActive) return false;
        _isCloseDialogActive = true;

        // Create dark semi-transparent modal overlay form covering the entire parent form client area
        using var overlay = new ModalOverlayForm
        {
            FormBorderStyle = FormBorderStyle.None,
            StartPosition = FormStartPosition.Manual,
            Location = PointToScreen(Point.Empty),
            Size = Size,
            BackColor = Color.FromArgb(15, 23, 42), // Slate Deep Navy #0F172A
            Opacity = 0.58, // 58% dimming — clearly dims parent while keeping it recognizable
            ShowInTaskbar = false,
            TopMost = false
        };

        // Clip overlay corners to match parent form radius
        var rgn = CreateRoundRectRgn(0, 0, overlay.Width, overlay.Height, 14, 14);
        if (rgn != IntPtr.Zero)
        {
            overlay.Region = Region.FromHrgn(rgn);
        }

        try
        {
            // Display overlay directly over parent without stealing activation
            overlay.Show(this);

            // Open confirmation dialog centered as a modal on top of parent
            using var dlg = new SetupCloseConfirmationDialog();
            var res = dlg.ShowDialog(this);
            return res == DialogResult.Yes;
        }
        finally
        {
            overlay.Close();
            _isCloseDialogActive = false;
        }
    }

    private sealed class ModalOverlayForm : Form
    {
        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_NOACTIVATE = 0x08000000;
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE;
                return cp;
            }
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_isCloseDialogActive)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        if (keyData == Keys.Enter)
        {
            btnAccept.PerformClick();
            return true;
        }

        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void StartupConfigurationForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            btnAccept.PerformClick();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Alt && e.KeyCode == Keys.F4)
        {
            Close();
            e.Handled = true;
        }
    }
}
