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
<<<<<<< Updated upstream
using MoneyFlow.Data.Storage;
=======
using MoneyFlow.Desktop.Controls.Dropdown;
using MoneyFlow.Desktop.Controls.Dropdown.Models;
using MoneyFlow.Desktop.Styling;
>>>>>>> Stashed changes

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Full-Screen / Dedicated ERP Company Creation Workspace.
/// Structured strictly according to the reference layout (Image 3):
/// - Top Navy ERP Title Bar & Sub-bar with Company Data Path
/// - Section 1: Corporate Identity & Physical Domicile (SEC-A)
/// - Section 2: Financial Year Periodicity (SEC-B)
/// - Section 3: Statutory & Compliance Registry (SEC-C)
/// - Section 4: Monetary Units & Currency (SEC-D)
/// - Mandatory Company Guard Notice
/// - Keyboard-first navigation (Tab, Enter, F4/Ctrl+S, F6, Esc, Alt+D)
/// - Transactional Save and Save & Open handling.
/// </summary>
public class CompanyCreateEditForm : Form
{
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;

    private readonly ICompanyService _companyService;
<<<<<<< Updated upstream
    private readonly SystemConfiguration? _systemConfig;
=======
    private readonly ICompanyContext? _companyContext;
>>>>>>> Stashed changes
    private readonly int? _companyIdToEdit;

    public bool IsSaved { get; private set; }
<<<<<<< Updated upstream
    private readonly string? _initialDataPath;

    public CompanyCreateEditForm(ICompanyService companyService, SystemConfiguration? systemConfig = null, int? companyIdToEdit = null, string? initialDataPath = null)
    {
        _companyService = companyService;
        _systemConfig = systemConfig;
        _companyIdToEdit = companyIdToEdit;
        _initialDataPath = initialDataPath;
=======
    public bool IsOpenRequested { get; private set; }
    public Core.Entities.Company? CreatedCompany { get; private set; }

    private bool _isDirty = false;
    private string _baseDataPath = MoneyFlow.Core.Common.CompanyPathConfiguration.ResolveCompaniesPath();
    private string _generatedEntityCode = "010006";

    // UI Controls — Left Column (SEC-A)
    private Label lblDirectoryPreview = null!;
    private TextBox txtCompanyName = null!;
    private TextBox txtMailingName = null!;
    private TextBox txtAddressLine1 = null!;
    private TextBox txtAddressLine2 = null!;
    private TextBox txtAddressLine3 = null!;
    private MoneyFlowTallyDropdown cmbCountry = null!;
    private MoneyFlowTallyDropdown cmbState = null!;
    private Label lblStateCode = null!;
    private TextBox txtPincode = null!;
    private TextBox txtTelephone = null!;
    private TextBox txtMobile = null!;
    private TextBox txtFax = null!;
    private TextBox txtEmail = null!;
    private TextBox txtWebsite = null!;

    // UI Controls — Right Column (SEC-B, SEC-C, SEC-D)
    private DateTimePicker dtpFYBeginning = null!;
    private DateTimePicker dtpBooksBeginning = null!;
    private Label lblFYWindow = null!;
    private TextBox txtGSTIN = null!;
    private TextBox txtPAN = null!;
    private TextBox txtCIN = null!;
    private Label lblEntityCodeDisplay = null!;
    private TextBox txtCurrencySymbol = null!;
    private TextBox txtFormalName = null!;
    private TextBox txtPrecision = null!;
    private TextBox txtSubUnit = null!;

    // Action Buttons & Responsive Containers
    private Button btnCancel = null!;
    private Button btnDiscard = null!;
    private Button btnSave = null!;
    private Button btnSaveAndOpen = null!;
    private Label lblGuardNotice = null!;
    private Panel pnlOuter = null!;
    private Panel pnlHeader = null!;
    private Label lblHdrTitle = null!;
    private Label lblHdrModule = null!;
    private Label btnMaxRestoreTop = null!;
    private Label btnCloseTop = null!;
    private Panel pnlSubBar = null!;
    private Label lblPathVal = null!;
    private Button btnSelectDrive = null!;
    private Panel pnlStatusStrip = null!;
    private Label lblStatusLeft = null!;
    private Label lblStatusRight = null!;
    private Panel pnlActionRail = null!;
    private Label lblKeyboardNav = null!;
    private Panel pnlNotice = null!;
    private Label lblEngineStatus = null!;
    private Panel pnlMainContent = null!;

    // Indian States and official GST Codes
    private static readonly Dictionary<string, string> IndianStates = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Jammu and Kashmir", "01" },
        { "Himachal Pradesh", "02" },
        { "Punjab", "03" },
        { "Chandigarh", "04" },
        { "Uttarakhand", "05" },
        { "Haryana", "06" },
        { "Delhi", "07" },
        { "Rajasthan", "08" },
        { "Uttar Pradesh", "09" },
        { "Bihar", "10" },
        { "Sikkim", "11" },
        { "Arunachal Pradesh", "12" },
        { "Nagaland", "13" },
        { "Manipur", "14" },
        { "Mizoram", "15" },
        { "Tripura", "16" },
        { "Meghalaya", "17" },
        { "Assam", "18" },
        { "West Bengal", "19" },
        { "Jharkhand", "20" },
        { "Odisha", "21" },
        { "Chhattisgarh", "22" },
        { "Madhya Pradesh", "23" },
        { "Gujarat", "24" },
        { "Dadra & Nagar Haveli and Daman & Diu", "26" },
        { "Maharashtra", "27" },
        { "Karnataka", "29" },
        { "Goa", "30" },
        { "Lakshadweep", "31" },
        { "Kerala", "32" },
        { "Tamil Nadu", "33" },
        { "Puducherry", "34" },
        { "Andaman & Nicobar Islands", "35" },
        { "Telangana", "36" },
        { "Andhra Pradesh", "37" },
        { "Ladakh", "38" },
        { "Other Territory", "97" }
    };

    public CompanyCreateEditForm(
        ICompanyService companyService,
        ICompanyContext? companyContext,
        int? companyIdToEdit,
        ICompanyStorageEngine? storageEngine)
    {
        _companyService = companyService ?? throw new ArgumentNullException(nameof(companyService));
        _companyContext = companyContext;
        _companyIdToEdit = companyIdToEdit;
        _baseDataPath = storageEngine?.GetCompaniesRootDirectory() ?? MoneyFlow.Core.Common.CompanyPathConfiguration.ResolveCompaniesPath();

>>>>>>> Stashed changes
        InitializeComponent();
        GenerateNextEntityCodeAsync();

        if (_companyIdToEdit.HasValue)
        {
            LoadExistingCompanyAsync(_companyIdToEdit.Value);
        }
    }

    // Overloads for DI / FormFactory / direct invocation
    public CompanyCreateEditForm(ICompanyService companyService)
        : this(companyService, null, null, null)
    {
    }

    public CompanyCreateEditForm(ICompanyService companyService, int? companyIdToEdit)
        : this(companyService, null, companyIdToEdit, null)
    {
    }

    public CompanyCreateEditForm(ICompanyService companyService, ICompanyContext? companyContext)
        : this(companyService, companyContext, null, null)
    {
    }

    private async void GenerateNextEntityCodeAsync()
    {
        try
        {
            var companies = await _companyService.GetAllCompaniesAsync();
            int maxId = 10001; // Start from 010002
            foreach (var c in companies)
            {
                string code = c.CompanyNumber.Replace("COMP-", "").Replace("(", "").Replace(")", "").Trim();
                if (int.TryParse(code, out int n) && n > maxId)
                {
                    maxId = n;
                }
            }

            int next = maxId + 1;
            _generatedEntityCode = $"{next:D6}";
            if (lblEntityCodeDisplay != null)
            {
                lblEntityCodeDisplay.Text = _generatedEntityCode;
            }
            UpdateDirectoryPreview();
        }
        catch
        {
            _generatedEntityCode = "010006";
        }
    }

    private async void LoadExistingCompanyAsync(int id)
    {
        try
        {
            var comp = await _companyService.GetCompanyByIdAsync(id);
            if (comp == null) return;

            txtCompanyName.Text = comp.CompanyName;
            txtMailingName.Text = comp.CompanyName;
            txtAddressLine1.Text = comp.Address;
            cmbState.SelectedText = comp.State ?? "";
            cmbCountry.SelectedText = comp.Country ?? "India";
            txtPAN.Text = comp.PAN;
            txtEmail.Text = comp.Email;
            txtTelephone.Text = comp.Phone;
            dtpFYBeginning.Value = comp.FinancialYearFrom;
            dtpBooksBeginning.Value = comp.BooksBeginningFrom;
            txtCurrencySymbol.Text = comp.Currency;

            _generatedEntityCode = comp.CompanyNumber;
            if (lblEntityCodeDisplay != null) lblEntityCodeDisplay.Text = comp.CompanyNumber;
            UpdateDirectoryPreview();
            _isDirty = false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed loading company: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

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

    public CompanyCreateEditForm(ICompanyService companyService, int? companyIdToEdit)
        : this(companyService, null, companyIdToEdit)
    {
    }

    private void InitializeComponent()
    {
        AutoScaleMode = AutoScaleMode.None;
        Text = _companyIdToEdit.HasValue ? "Alter Company — MoneyFlow Desktop ERP" : "Company Creation — MoneyFlow Desktop ERP";
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(241, 245, 249); // Soft light slate canvas
        Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        KeyPreview = true;
        DoubleBuffered = true;

<<<<<<< Updated upstream
        var appIcon = ExecLedgerIcons.GetAppIcon();
        if (appIcon != null)
        {
            this.Icon = appIcon;
            this.ShowIcon = true;
        }

        // Header Panel
        var headerPanel = new Panel
=======
        ScreenFittingManager.FitDialogToWorkArea(this, 1220, 740, 960, 500);

        // Outer Container (Card with 1px border)
        pnlOuter = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(0)
        };
        Controls.Add(pnlOuter);

        // ═══════════════════════════════════════════════════════════════
        // 1. TOP HEADER (32px Soft Sky Blue matching TallyPrime from Image 2)
        // ═══════════════════════════════════════════════════════════════
        pnlHeader = new Panel
>>>>>>> Stashed changes
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = Color.FromArgb(143, 184, 222) // Exact soft Tally sky blue #8FB8DE from Image 2
        };
<<<<<<< Updated upstream
        var picLogo = new PictureBox
        {
            Image = ExecLedgerIcons.GetAppLogo(28, 28),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(28, 28),
            Location = new Point(18, 16),
            BackColor = Color.Transparent
        };
        headerPanel.Controls.Add(picLogo);

        var lblTitle = new Label
        {
            Text = isEdit ? "Alter Company" : "Company Creation",
            Font = ExecLedgerTheme.UIBold12,
            ForeColor = Color.White,
            Location = new Point(54, 18),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblTitle);
        this.Controls.Add(headerPanel);

        // Form Fields Group
        var grp = new GroupBox
        {
            Text = "Company Details, Security & Data Directory",
            Location = new Point(20, 75),
            Size = new Size(585, 600),
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        int y = 28;
        int spacing = 32;

        string defaultBasePath = _systemConfig?.CompanyDataPath ?? SystemEnvironmentManager.GetDefaultCompanyDataPath();
        string initialDataPath = !string.IsNullOrWhiteSpace(_initialDataPath)
            ? _initialDataPath
            : Path.Combine(defaultBasePath, "Companies");

        // Company Data Path (Tally Style)
        AddLabel(grp, "Company Data Path *:", 20, y);
        txtDataPath = new TextBox
        {
            Location = new Point(180, y - 3),
            Width = 275,
            Text = initialDataPath
        };
        grp.Controls.Add(txtDataPath);

        btnBrowseDataPath = new Button
        {
            Text = "Browse...",
            Location = new Point(460, y - 4),
            Size = new Size(90, 27),
            BackColor = Color.FromArgb(226, 232, 240)
        };
        btnBrowseDataPath.Click += (s, e) =>
        {
            using var fbd = new FolderBrowserDialog();
            fbd.Description = "Select Company Data Directory";
            fbd.UseDescriptionForTitle = true;
            if (System.IO.Directory.Exists(txtDataPath.Text))
            {
                fbd.SelectedPath = txtDataPath.Text;
            }
            else if (System.IO.Directory.Exists(initialDataPath))
            {
                fbd.SelectedPath = initialDataPath;
            }
            if (fbd.ShowDialog(this) == DialogResult.OK)
            {
                txtDataPath.Text = fbd.SelectedPath;
=======
        pnlHeader.MouseDown += (s, e) =>
        {
            if (e.Button == MouseButtons.Left && TopLevel)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
>>>>>>> Stashed changes
            }
        };
        pnlHeader.DoubleClick += (s, e) => { if (TopLevel) ToggleMaximize(); };

        lblHdrTitle = new Label
        {
            Text = _companyIdToEdit.HasValue ? "Company Alteration" : "Company Creation",
            Font = new Font("Segoe UI", 9.75F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42), // Dark Navy / Black matching Image 2
            Location = new Point(14, 6),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblHdrTitle.DoubleClick += (s, e) => { if (TopLevel) ToggleMaximize(); };
        pnlHeader.Controls.Add(lblHdrTitle);

        lblHdrModule = new Label
        {
            Text = "",
            Visible = false
        };
        pnlHeader.Controls.Add(lblHdrModule);

        btnMaxRestoreTop = new Label
        {
            Text = "🗖",
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(1160, 5),
            Size = new Size(26, 22),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            Visible = false
        };
        btnMaxRestoreTop.Click += (s, e) => ToggleMaximize();
        pnlHeader.Controls.Add(btnMaxRestoreTop);

        btnCloseTop = new Label
        {
            Text = "✕",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(1190, 5),
            Size = new Size(26, 22),
            Cursor = Cursors.Hand,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        btnCloseTop.MouseEnter += (s, e) => { btnCloseTop.ForeColor = Color.White; btnCloseTop.BackColor = Color.FromArgb(220, 38, 38); };
        btnCloseTop.MouseLeave += (s, e) => { btnCloseTop.ForeColor = Color.FromArgb(15, 23, 42); btnCloseTop.BackColor = Color.Transparent; };
        btnCloseTop.Click += (s, e) => HandleCancelRequest();
        pnlHeader.Controls.Add(btnCloseTop);

        pnlOuter.Controls.Add(pnlHeader);

        // ═══════════════════════════════════════════════════════════════
        // 2. SUB-BAR: DATA PATH (30px #FFFFFF, 1px subtle border bottom)
        // ═══════════════════════════════════════════════════════════════
        pnlSubBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Color.White
        };
        pnlSubBar.Paint += (s, e) =>
        {
            using var p = new Pen(Color.FromArgb(226, 232, 240));
            e.Graphics.DrawLine(p, 0, 29, pnlSubBar.Width, 29);
        };

        var lblPathPrompt = new Label
        {
            Text = "Company Data Path        :",
            Font = new Font("Segoe UI", 8.75F, FontStyle.Regular),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(14, 6),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlSubBar.Controls.Add(lblPathPrompt);

        lblPathVal = new Label
        {
            Text = _baseDataPath,
            Font = new Font("Segoe UI", 8.75F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(180, 6),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlSubBar.Controls.Add(lblPathVal);

        btnSelectDrive = new Button
        {
            Text = "📁 Select from Drive",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(2, 132, 199),
            BackColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(145, 22),
            Location = new Point(1040, 4),
            Cursor = Cursors.Hand
        };
        btnSelectDrive.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnSelectDrive.Click += (s, e) => BrowseDataDirectory();
        pnlSubBar.Controls.Add(btnSelectDrive);

        pnlOuter.Controls.Add(pnlSubBar);

        // ═══════════════════════════════════════════════════════════════
        // 3. BOTTOM STATUS STRIP (Hidden for clean Tally appearance)
        // ═══════════════════════════════════════════════════════════════
        pnlStatusStrip = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 0,
            Visible = false
        };
        lblStatusLeft = new Label { Visible = false };
        lblStatusRight = new Label { Visible = false };
        pnlStatusStrip.Controls.Add(lblStatusLeft);
        pnlStatusStrip.Controls.Add(lblStatusRight);
        pnlOuter.Controls.Add(pnlStatusStrip);

        // ═══════════════════════════════════════════════════════════════
        // 4. BOTTOM ACTION RAIL (48px #F1F5F9)
        // ═══════════════════════════════════════════════════════════════
        pnlActionRail = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            BackColor = Color.FromArgb(241, 245, 249)
        };
        pnlActionRail.Paint += (s, e) =>
        {
            using var p = new Pen(Color.FromArgb(203, 213, 225));
            e.Graphics.DrawLine(p, 0, 0, pnlActionRail.Width, 0);
        };

        lblKeyboardNav = new Label
        {
            Text = "Esc: Back  •  Ctrl+A: Accept  •  Tab: Next  •  Enter: Next Field",
            Font = new Font("Segoe UI", 8.2F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
<<<<<<< Updated upstream
            Location = new Point(345, y),
            AutoSize = true
        };
        grp.Controls.Add(lblNumHint);

        y += spacing;
        AddLabel(grp, "Address:", 20, y);
        txtAddress = AddTextBox(grp, 180, y, 370);

        y += spacing;
        AddLabel(grp, "State:", 20, y);
        cmbState = AddComboBox(grp, 180, y, 370, IndianStates, "Gujarat");

        y += spacing;
        AddLabel(grp, "Country:", 20, y);
        cmbCountry = AddComboBox(grp, 180, y, 370, Countries, _systemConfig?.Country ?? "India");
        cmbCountry.SelectedIndexChanged += (s, e) =>
        {
            if (cmbCountry.Text.Trim().Equals("India", StringComparison.OrdinalIgnoreCase))
            {
                cmbState.Items.Clear();
                cmbState.Items.AddRange(IndianStates);
            }
        };

        y += spacing;
        AddLabel(grp, "PAN:", 20, y);
        txtPAN = AddTextBox(grp, 180, y, 370);

        y += spacing;
        AddLabel(grp, "Email:", 20, y);
        txtEmail = AddTextBox(grp, 180, y, 370);

        y += spacing;
        AddLabel(grp, "Phone:", 20, y);
        txtPhone = AddTextBox(grp, 180, y, 370);

        y += spacing;
        AddLabel(grp, "Financial Year From:", 20, y);
        dtpFYFrom = new DateTimePicker
        {
            Location = new Point(180, y - 3),
            Width = 370,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MMM-yyyy",
            Value = new DateTime(2026, 4, 1),
            Enabled = !isEdit
        };
        grp.Controls.Add(dtpFYFrom);

        y += spacing;
        AddLabel(grp, "Books Beginning From:", 20, y);
        dtpBooksFrom = new DateTimePicker
        {
            Location = new Point(180, y - 3),
            Width = 370,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MMM-yyyy",
            Value = new DateTime(2026, 4, 1),
            Enabled = !isEdit
        };
        grp.Controls.Add(dtpBooksFrom);

        y += spacing;
        AddLabel(grp, "Base Currency:", 20, y);
        txtCurrency = AddTextBox(grp, 180, y, 100);
        txtCurrency.Text = _systemConfig?.CurrencySymbol ?? "₹";

        // Tally Vault Password
        y += spacing;
        AddLabel(grp, "Vault Password:", 20, y);
        txtVaultPassword = new TextBox
        {
            Location = new Point(180, y - 3),
            Width = 160,
            PasswordChar = '●',
            UseSystemPasswordChar = true
        };
        grp.Controls.Add(txtVaultPassword);

        var lblRepeat = new Label
        {
            Text = "Repeat:",
            Location = new Point(350, y),
            AutoSize = true
        };
        grp.Controls.Add(lblRepeat);

        txtConfirmPassword = new TextBox
        {
            Location = new Point(400, y - 3),
            Width = 150,
            PasswordChar = '●',
            UseSystemPasswordChar = true
        };
        grp.Controls.Add(txtConfirmPassword);

        // Auto-backup on exit
        y += spacing;
        chkAutoBackupOnExit = new CheckBox
        {
            Text = "Auto-Backup company data to company folder upon application exit",
            Location = new Point(180, y),
=======
            Location = new Point(14, 15),
>>>>>>> Stashed changes
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlActionRail.Controls.Add(lblKeyboardNav);

        // Action Buttons on Right
        btnCancel = CreateActionButton("[Esc] Back", 690, 8, 95, 32, false);
        btnCancel.Click += (s, e) => HandleCancelRequest();
        pnlActionRail.Controls.Add(btnCancel);

        btnDiscard = CreateActionButton("[Alt+D] Discard", 792, 8, 105, 32, false);
        btnDiscard.Click += (s, e) => ResetFormFields();
        pnlActionRail.Controls.Add(btnDiscard);

        btnSave = CreateActionButton("[Ctrl+A] Accept", 904, 8, 150, 32, true);
        btnSave.Click += (s, e) => ExecuteSave(openAfterSave: false);
        pnlActionRail.Controls.Add(btnSave);

        btnSaveAndOpen = new Button
        {
            Text = "✓ Save & Open Company [F6]",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(11, 30, 51), // Deep Navy
            FlatStyle = FlatStyle.Flat,
            Size = new Size(185, 32),
            Location = new Point(1060, 8),
            Cursor = Cursors.Hand
        };
        btnSaveAndOpen.FlatAppearance.BorderSize = 0;
        btnSaveAndOpen.Click += (s, e) => ExecuteSave(openAfterSave: true);
        pnlActionRail.Controls.Add(btnSaveAndOpen);

        pnlOuter.Controls.Add(pnlActionRail);

        // ═══════════════════════════════════════════════════════════════
        // 5. MANDATORY COMPANY GUARD NOTICE (Hidden for clean Tally appearance)
        // ═══════════════════════════════════════════════════════════════
        pnlNotice = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 0,
            Visible = false
        };
        lblGuardNotice = new Label { Visible = false };
        lblEngineStatus = new Label { Visible = false };
        pnlNotice.Controls.Add(lblGuardNotice);
        pnlNotice.Controls.Add(lblEngineStatus);
        pnlOuter.Controls.Add(pnlNotice);

        // ═══════════════════════════════════════════════════════════════
        // 6. MAIN WORKSPACE (Clean Tally Two-Column Canvas)
        // ═══════════════════════════════════════════════════════════════
        int bottomDividerY = 412;
        pnlMainContent = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(16, 12, 16, 12),
            AutoScroll = true,
            AutoScrollMinSize = new Size(1100, 700)
        };
        pnlMainContent.Paint += (s, e) =>
        {
            // Subtle horizontal divider line separating Company info from Base Currency
            using var p = new Pen(Color.FromArgb(226, 232, 240), 1f);
            int lineW = Math.Max(pnlMainContent.Width - 32, 1060);
            e.Graphics.DrawLine(p, 16, bottomDividerY, lineW, bottomDividerY);
        };
        pnlOuter.Controls.Add(pnlMainContent);

        // ─────────────────────────────────────────────────────────────
        // LEFT COLUMN: Corporate Identity & Address & Contact
        // ─────────────────────────────────────────────────────────────
        int leftX = 16;
        int colonLeftX = 180;
        int inputLeftX = 195;
        int inputWidth = 360;
        int curY = 16;

        // Hidden / maintained directory preview
        lblDirectoryPreview = new Label
        {
            Text = Path.Combine(_baseDataPath, $"{_generatedEntityCode}_NewEntity"),
            Visible = false
        };
        pnlMainContent.Controls.Add(lblDirectoryPreview);

        // 1. Company Name
        var lblNameTitle = CreateFieldLabel("Company Name", leftX, curY);
        var lblNameColon = CreateFieldColon(colonLeftX, curY);
        txtCompanyName = CreateERPTextBox(inputLeftX, curY, inputWidth, 24);
        txtCompanyName.Text = "";
        txtCompanyName.TextChanged += (s, e) =>
        {
            _isDirty = true;
            if (string.IsNullOrWhiteSpace(txtMailingName.Text) || txtMailingName.Text == txtCompanyName.Text.Substring(0, Math.Max(0, txtCompanyName.Text.Length - 1)))
            {
                txtMailingName.Text = txtCompanyName.Text;
            }
            UpdateDirectoryPreview();
        };
        pnlMainContent.Controls.Add(lblNameTitle);
        pnlMainContent.Controls.Add(lblNameColon);
        pnlMainContent.Controls.Add(txtCompanyName);
        curY += 28;

        // 2. Mailing Name
        var lblMailTitle = CreateFieldLabel("Mailing Name", leftX, curY);
        var lblMailColon = CreateFieldColon(colonLeftX, curY);
        txtMailingName = CreateERPTextBox(inputLeftX, curY, inputWidth, 24);
        txtMailingName.Text = "";
        pnlMainContent.Controls.Add(lblMailTitle);
        pnlMainContent.Controls.Add(lblMailColon);
        pnlMainContent.Controls.Add(txtMailingName);
        curY += 28;

        // 3. Address (3 continuous rows)
        var lblAddrTitle = CreateFieldLabel("Address", leftX, curY);
        var lblAddrColon = CreateFieldColon(colonLeftX, curY);
        txtAddressLine1 = CreateERPTextBox(inputLeftX, curY, inputWidth, 24);
        txtAddressLine1.Text = "";
        pnlMainContent.Controls.Add(lblAddrTitle);
        pnlMainContent.Controls.Add(lblAddrColon);
        pnlMainContent.Controls.Add(txtAddressLine1);
        curY += 26;

        txtAddressLine2 = CreateERPTextBox(inputLeftX, curY, inputWidth, 24);
        txtAddressLine2.Text = "";
        pnlMainContent.Controls.Add(txtAddressLine2);
        curY += 26;

        txtAddressLine3 = CreateERPTextBox(inputLeftX, curY, inputWidth, 24);
        txtAddressLine3.Text = "";
        pnlMainContent.Controls.Add(txtAddressLine3);
        curY += 36; // Elegant gap before State matching Image 2

        // 4. State (before Country as in Tally)
        var lblStateTitle = CreateFieldLabel("State", leftX, curY);
        var lblStateColon = CreateFieldColon(colonLeftX, curY);
        cmbState = new MoneyFlowTallyDropdown
        {
            Location = new Point(inputLeftX, curY),
            Size = new Size(inputWidth, 26),
            Configuration = DropdownConfiguration.ForState()
        };

        var stateList = new List<DropdownItemModel>
        {
            DropdownItemModel.CreateSpecial("♦ Not Applicable", "Not Applicable", "Global")
        };
        foreach (var state in IndianStates.OrderBy(k => k.Key))
        {
            stateList.Add(new DropdownItemModel(state.Key, state.Key, state.Value, $"GST Code {state.Value}"));
        }
        cmbState.SetItems(stateList, "Not Applicable");

        lblStateCode = new Label
        {
            Text = "",
            Visible = false
        };

        cmbState.SelectedItemChanged += (s, e) =>
        {
            _isDirty = true;
            string sel = cmbState.SelectedText;
            if (IndianStates.TryGetValue(sel, out var code))
            {
                lblStateCode.Text = $"CODE: {code}";
            }
            else
            {
                lblStateCode.Text = "";
            }
        };

        pnlMainContent.Controls.Add(lblStateTitle);
        pnlMainContent.Controls.Add(lblStateColon);
        pnlMainContent.Controls.Add(cmbState);
        pnlMainContent.Controls.Add(lblStateCode);
        curY += 28;

        // 5. Country
        var lblCountryTitle = CreateFieldLabel("Country", leftX, curY);
        var lblCountryColon = CreateFieldColon(colonLeftX, curY);
        cmbCountry = new MoneyFlowTallyDropdown
        {
            Location = new Point(inputLeftX, curY),
            Size = new Size(inputWidth, 26),
            Configuration = DropdownConfiguration.ForCountry()
        };

        var countryList = new List<DropdownItemModel>
        {
            DropdownItemModel.CreateSpecial("♦ Not Applicable", "Not Applicable", "Global"),
            new DropdownItemModel("Bangladesh", "Bangladesh", "BGD (+880)", "Global"),
            new DropdownItemModel("Bhutan", "Bhutan", "BTN (+975)", "Global"),
            new DropdownItemModel("Botswana", "Botswana", "BWA (+267)", "Global"),
            new DropdownItemModel("Egypt", "Egypt", "EGY (+20)", "Global"),
            new DropdownItemModel("Ghana", "Ghana", "GHA (+233)", "Global"),
            new DropdownItemModel("Hong Kong", "Hong Kong", "HKG (+852)", "Global"),
            new DropdownItemModel("India", "India", "IND (+91)", "Global"),
            new DropdownItemModel("Indonesia", "Indonesia", "IDN (+62)", "Global"),
            new DropdownItemModel("Kenya", "Kenya", "KEN (+254)", "Global"),
            new DropdownItemModel("Kingdom of Bahrain", "Kingdom of Bahrain", "BHR (+973)", "Global"),
            new DropdownItemModel("Kuwait", "Kuwait", "KWT (+965)", "Global"),
            new DropdownItemModel("Liberia", "Liberia", "LBR (+231)", "Global"),
            new DropdownItemModel("Malaysia", "Malaysia", "MYS (+60)", "Global"),
            new DropdownItemModel("Nepal", "Nepal", "NPL (+977)", "Global"),
            new DropdownItemModel("Nigeria", "Nigeria", "NGA (+234)", "Global"),
            new DropdownItemModel("Philippines", "Philippines", "PHL (+63)", "Global"),
            new DropdownItemModel("Qatar", "Qatar", "QAT (+974)", "Global"),
            new DropdownItemModel("Saudi Arabia", "Saudi Arabia", "SAU (+966)", "Global"),
            new DropdownItemModel("Singapore", "Singapore", "SGP (+65)", "Global"),
            new DropdownItemModel("United Arab Emirates", "United Arab Emirates", "ARE (+971)", "Global"),
            new DropdownItemModel("United Kingdom", "United Kingdom", "GBR (+44)", "Global"),
            new DropdownItemModel("United States", "United States", "USA (+1)", "Global")
        };
        cmbCountry.SetItems(countryList, "India");
        cmbCountry.SelectedItemChanged += (s, e) => _isDirty = true;

        pnlMainContent.Controls.Add(lblCountryTitle);
        pnlMainContent.Controls.Add(lblCountryColon);
        pnlMainContent.Controls.Add(cmbCountry);
        curY += 28;

        // 6. Pincode
        var lblPinTitle = CreateFieldLabel("Pincode", leftX, curY);
        var lblPinColon = CreateFieldColon(colonLeftX, curY);
        txtPincode = CreateERPTextBox(inputLeftX, curY, 160, 24);
        txtPincode.Text = "";
        pnlMainContent.Controls.Add(lblPinTitle);
        pnlMainContent.Controls.Add(lblPinColon);
        pnlMainContent.Controls.Add(txtPincode);
        curY += 28;

        // 7. Telephone
        var lblTelTitle = CreateFieldLabel("Telephone", leftX, curY);
        var lblTelColon = CreateFieldColon(colonLeftX, curY);
        txtTelephone = CreateERPTextBox(inputLeftX, curY, 200, 24);
        txtTelephone.Text = "";
        pnlMainContent.Controls.Add(lblTelTitle);
        pnlMainContent.Controls.Add(lblTelColon);
        pnlMainContent.Controls.Add(txtTelephone);
        curY += 28;

        // 8. Mobile (with "+91 - " prefix matching Image 2)
        var lblMobTitle = CreateFieldLabel("Mobile", leftX, curY);
        var lblMobColon = CreateFieldColon(colonLeftX, curY);
        var lblMobPrefix = new Label
        {
            Text = "+91  -",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(inputLeftX, curY + 2),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        txtMobile = CreateERPTextBox(inputLeftX + 45, curY, 160, 24);
        txtMobile.Text = "";
        pnlMainContent.Controls.Add(lblMobTitle);
        pnlMainContent.Controls.Add(lblMobColon);
        pnlMainContent.Controls.Add(lblMobPrefix);
        pnlMainContent.Controls.Add(txtMobile);
        curY += 28;

        // 9. Fax
        var lblFaxTitle = CreateFieldLabel("Fax", leftX, curY);
        var lblFaxColon = CreateFieldColon(colonLeftX, curY);
        txtFax = CreateERPTextBox(inputLeftX, curY, 200, 24);
        txtFax.Text = "";
        pnlMainContent.Controls.Add(lblFaxTitle);
        pnlMainContent.Controls.Add(lblFaxColon);
        pnlMainContent.Controls.Add(txtFax);
        curY += 28;

        // 10. E-mail
        var lblMailLbl = CreateFieldLabel("E-mail", leftX, curY);
        var lblMailCol = CreateFieldColon(colonLeftX, curY);
        txtEmail = CreateERPTextBox(inputLeftX, curY, 260, 24);
        txtEmail.Text = "";
        pnlMainContent.Controls.Add(lblMailLbl);
        pnlMainContent.Controls.Add(lblMailCol);
        pnlMainContent.Controls.Add(txtEmail);
        curY += 28;

        // 11. Website
        var lblWebTitle = CreateFieldLabel("Website", leftX, curY);
        var lblWebColon = CreateFieldColon(colonLeftX, curY);
        txtWebsite = CreateERPTextBox(inputLeftX, curY, 260, 24);
        txtWebsite.Text = "";
        pnlMainContent.Controls.Add(lblWebTitle);
        pnlMainContent.Controls.Add(lblWebColon);
        pnlMainContent.Controls.Add(txtWebsite);

        // ─────────────────────────────────────────────────────────────
        // RIGHT COLUMN: Financial Year & Dates & Statutory Details
        // ─────────────────────────────────────────────────────────────
        int rightX = 580;
        int rightColonX = 810;
        int rightInputX = 825;
        int rY = 16;

        // Financial year beginning from
        var lblFYBeg = CreateFieldLabel("Financial year beginning from", rightX, rY);
        var lblFYBegCol = CreateFieldColon(rightColonX, rY);
        dtpFYBeginning = new DateTimePicker
        {
            Location = new Point(rightInputX, rY),
            Size = new Size(120, 24),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "d-MMM-yy",
            Value = new DateTime(2026, 4, 1)
        };
        dtpFYBeginning.ValueChanged += (s, e) => UpdateFYAccountingWindow();
        pnlMainContent.Controls.Add(lblFYBeg);
        pnlMainContent.Controls.Add(lblFYBegCol);
        pnlMainContent.Controls.Add(dtpFYBeginning);
        rY += 28;

        // Books beginning from
        var lblBooksBeg = CreateFieldLabel("Books beginning from", rightX, rY);
        var lblBooksBegCol = CreateFieldColon(rightColonX, rY);
        dtpBooksBeginning = new DateTimePicker
        {
            Location = new Point(rightInputX, rY),
            Size = new Size(120, 24),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "d-MMM-yy",
            Value = new DateTime(2026, 4, 1)
        };
        dtpBooksBeginning.ValueChanged += (s, e) => UpdateFYAccountingWindow();
        pnlMainContent.Controls.Add(lblBooksBeg);
        pnlMainContent.Controls.Add(lblBooksBegCol);
        pnlMainContent.Controls.Add(dtpBooksBeginning);
        rY += 38;

        // Maintained hidden/subtle FY window
        lblFYWindow = new Label { Text = "01-Apr-2026 to 31-Mar-2027", Visible = false };
        pnlMainContent.Controls.Add(lblFYWindow);

        // Statutory Fields (Cleanly placed without ugly banners)
        var lblGST = CreateFieldLabel("GSTIN / UIN", rightX, rY);
        var lblGSTCol = CreateFieldColon(rightColonX, rY);
        txtGSTIN = CreateERPTextBox(rightInputX, rY, 240, 24);
        txtGSTIN.Text = "";
        txtGSTIN.TextChanged += (s, e) =>
        {
            _isDirty = true;
            string gstin = txtGSTIN.Text.Trim().ToUpperInvariant();
            if (gstin.Length >= 12 && string.IsNullOrWhiteSpace(txtPAN.Text))
            {
                txtPAN.Text = gstin.Substring(2, 10);
            }
        };
        pnlMainContent.Controls.Add(lblGST);
        pnlMainContent.Controls.Add(lblGSTCol);
        pnlMainContent.Controls.Add(txtGSTIN);
        rY += 28;

        var lblPAN = CreateFieldLabel("PAN / Income Tax No.", rightX, rY);
        var lblPANCol = CreateFieldColon(rightColonX, rY);
        txtPAN = CreateERPTextBox(rightInputX, rY, 240, 24);
        txtPAN.Text = "";
        pnlMainContent.Controls.Add(lblPAN);
        pnlMainContent.Controls.Add(lblPANCol);
        pnlMainContent.Controls.Add(txtPAN);
        rY += 28;

        var lblCIN = CreateFieldLabel("Corporate ID (CIN)", rightX, rY);
        var lblCINCol = CreateFieldColon(rightColonX, rY);
        txtCIN = CreateERPTextBox(rightInputX, rY, 240, 24);
        txtCIN.Text = "";
        pnlMainContent.Controls.Add(lblCIN);
        pnlMainContent.Controls.Add(lblCINCol);
        pnlMainContent.Controls.Add(txtCIN);
        rY += 28;

        lblEntityCodeDisplay = new Label
        {
            Text = _generatedEntityCode,
            Visible = false
        };
        pnlMainContent.Controls.Add(lblEntityCodeDisplay);

        // ─────────────────────────────────────────────────────────────
        // BOTTOM SECTION: Base Currency Symbol & Formal Name
        // ─────────────────────────────────────────────────────────────
        int bY = bottomDividerY + 14;

        var lblSym = CreateFieldLabel("Base Currency symbol", leftX, bY);
        var lblSymCol = CreateFieldColon(colonLeftX, bY);
        txtCurrencySymbol = CreateERPTextBox(inputLeftX, bY, 60, 24);
        txtCurrencySymbol.Text = "₹";
        pnlMainContent.Controls.Add(lblSym);
        pnlMainContent.Controls.Add(lblSymCol);
        pnlMainContent.Controls.Add(txtCurrencySymbol);
        bY += 28;

        var lblFormal = CreateFieldLabel("Formal name", leftX, bY);
        var lblFormalCol = CreateFieldColon(colonLeftX, bY);
        txtFormalName = CreateERPTextBox(inputLeftX, bY, 140, 24);
        txtFormalName.Text = "INR";
        pnlMainContent.Controls.Add(lblFormal);
        pnlMainContent.Controls.Add(lblFormalCol);
        pnlMainContent.Controls.Add(txtFormalName);

        // Precision & SubUnit maintained in background for DTO
        txtPrecision = new TextBox { Text = "2", Visible = false };
        txtSubUnit = new TextBox { Text = "Paise", Visible = false };
        pnlMainContent.Controls.Add(txtPrecision);
        pnlMainContent.Controls.Add(txtSubUnit);

        // Setup tab navigation order & focus highlighting
        SetupFocusHighlighting(pnlMainContent);

        // Initial responsive positioning
        LayoutHeader();
        LayoutSubBar();
        LayoutNotice();
        LayoutActionRail();
        LayoutStatusStrip();

        // Keyboard Shortcuts
        KeyDown += OnFormKeyDown;
        _isDirty = false;
    }

    private void UpdateDirectoryPreview()
    {
        if (lblDirectoryPreview == null) return;
        string safeName = SanitizeDirectoryName(txtCompanyName?.Text ?? "NewEntity");
        string dirPath = Path.Combine(_baseDataPath, $"{_generatedEntityCode}_{safeName}");
        lblDirectoryPreview.Text = dirPath;

        if (lblGuardNotice != null)
        {
            lblGuardNotice.Text = $"ⓘ Mandatory Company Guard: The ledger runtime requires an active company context. Clicking Save & Open [F6] provisions {dirPath}\\ and launches the live operational desk.";
        }
    }

    private void UpdateFYAccountingWindow()
    {
        if (dtpFYBeginning == null || lblFYWindow == null) return;
        var start = dtpFYBeginning.Value;
        var end = start.AddYears(1).AddDays(-1);
        lblFYWindow.Text = $"{start:dd-MMM-yyyy} to {end:dd-MMM-yyyy}";
    }

    private static string SanitizeDirectoryName(string raw)
    {
        string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
        string cleaned = new string(raw.Where(ch => !invalid.Contains(ch)).ToArray()).Trim();
        cleaned = cleaned.Replace(" ", "");
        return string.IsNullOrWhiteSpace(cleaned) ? "NewEntity" : cleaned;
    }

    private void BrowseDataDirectory()
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "Select Base Company Storage Directory",
            SelectedPath = Directory.Exists(_baseDataPath) ? _baseDataPath : Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            ShowNewFolderButton = true
        };

        if (fbd.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
        {
            _baseDataPath = MoneyFlow.Core.Common.CompanyPathConfiguration.EnsureTrailingSeparator(fbd.SelectedPath);
            MoneyFlow.Core.Common.CompanyPathConfiguration.ConfiguredCompaniesPath = _baseDataPath;
            UpdateDirectoryPreview();
        }
    }

    private void ResetFormFields()
    {
        if (_isDirty)
        {
            var res = MessageBox.Show(this, "Discard changes and reset form?", "Reset Form", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res != DialogResult.Yes) return;
        }

        txtCompanyName.Text = "";
        txtMailingName.Text = "";
        txtAddressLine1.Text = "";
        txtAddressLine2.Text = "";
        txtAddressLine3.Text = "";
        txtPincode.Text = "";
        txtTelephone.Text = "";
        txtMobile.Text = "";
        txtFax.Text = "";
        txtEmail.Text = "";
        txtWebsite.Text = "";
        txtGSTIN.Text = "";
        txtPAN.Text = "";
        txtCIN.Text = "";
        _isDirty = false;
        txtCompanyName.Focus();
    }

    private void HandleCancelRequest()
    {
        if (_isDirty)
        {
            var res = MessageBox.Show(this, "Discard company creation?", "Confirm Discard", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res != DialogResult.Yes)
            {
                return;
            }
        }

        DialogResult = DialogResult.Cancel;
        Close();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_isDirty && !IsSaved)
        {
            var res = MessageBox.Show(this, "Discard company creation?", "Confirm Discard", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }

        base.OnFormClosing(e);
    }

    private async void ExecuteSave(bool openAfterSave)
    {
        // 1. Validation
        string compName = txtCompanyName.Text.Trim();
        if (string.IsNullOrWhiteSpace(compName) || compName.Length < 2)
        {
            MessageBox.Show(this, "Company Name is required (minimum 2 characters).", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtCompanyName.Focus();
            return;
        }

        string fullAddress = $"{txtAddressLine1.Text.Trim()}\n{txtAddressLine2.Text.Trim()}\n{txtAddressLine3.Text.Trim()}".Trim();

        if (dtpBooksBeginning.Value < dtpFYBeginning.Value)
        {
            MessageBox.Show(this, "Books Beginning date cannot be earlier than Financial Year Beginning.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            dtpBooksBeginning.Focus();
            return;
        }

        string state = cmbState.SelectedText;
        if (state == "♦ Not Applicable" || state == "◆ Not Applicable" || state == "Not Applicable") state = "";

        // Check duplicate company name
        try
        {
            var existing = await _companyService.GetAllCompaniesAsync();
            if (existing.Any(c => c.CompanyName.Equals(compName, StringComparison.OrdinalIgnoreCase) && (!_companyIdToEdit.HasValue || c.CompanyId != _companyIdToEdit.Value)))
            {
                MessageBox.Show(this, "A company with this name already exists.", "Duplicate Company", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtCompanyName.Focus();
                return;
            }
        }
        catch { }

        // 2. Perform Save
        try
        {
            btnSave.Enabled = false;
            btnSaveAndOpen.Enabled = false;
            Cursor = Cursors.WaitCursor;

            string country = cmbCountry.SelectedText;
            if (string.IsNullOrWhiteSpace(country) || country == "♦ Not Applicable" || country == "◆ Not Applicable") country = "India";

            var dto = new CompanyCreateDto
            {
                CompanyName = compName,
                CompanyNumber = _generatedEntityCode,
                Address = fullAddress,
                State = state,
                Country = country,
                PAN = txtPAN.Text.Trim(),
                Email = txtEmail.Text.Trim(),
                Phone = string.IsNullOrWhiteSpace(txtMobile.Text) ? txtTelephone.Text.Trim() : txtMobile.Text.Trim(),
                FinancialYearFrom = dtpFYBeginning.Value.Date,
                BooksBeginningFrom = dtpBooksBeginning.Value.Date,
                Currency = string.IsNullOrWhiteSpace(txtCurrencySymbol.Text) ? "₹" : txtCurrencySymbol.Text.Trim(),
                AutoBackupOnExit = true
            };

            var company = await _companyService.CreateCompanyAsync(dto);
            CreatedCompany = company;
            IsSaved = true;

            if (openAfterSave)
            {
                // Save & Open: ensure company is active in context
                await _companyService.OpenCompanyAsync(company.CompanyId);
                IsOpenRequested = true;
            }

            _isDirty = false;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Unable to create company: {ex.Message}", "Company Creation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnSave.Enabled = true;
            btnSaveAndOpen.Enabled = true;
            Cursor = Cursors.Default;
        }
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F6)
        {
            e.Handled = true;
            ExecuteSave(openAfterSave: true);
        }
        else if ((e.Control && e.KeyCode == Keys.A) || e.KeyCode == Keys.F4 || (e.Control && e.KeyCode == Keys.S))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            ExecuteSave(openAfterSave: false);
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            HandleCancelRequest();
        }
        else if (e.Alt && e.KeyCode == Keys.D)
        {
            e.Handled = true;
            ResetFormFields();
        }
        else if (e.KeyCode == Keys.F12)
        {
            e.Handled = true;
            BrowseDataDirectory();
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // UI BUILDER HELPERS FOR RETRO/ERP STYLING
    // ═══════════════════════════════════════════════════════════════

    private static Panel CreateSectionHeader(string title, string badge, int x, int y, int w)
    {
        var pnl = new Panel
        {
            Location = new Point(x, y),
            Size = new Size(w, 22),
            BackColor = Color.Transparent
        };
        pnl.Paint += (s, e) =>
        {
            using var p = new Pen(Color.FromArgb(203, 213, 225));
            e.Graphics.DrawLine(p, 0, 20, pnl.Width, 20);
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8.2F, FontStyle.Bold),
            ForeColor = Color.FromArgb(17, 36, 58), // Deep Navy
            Location = new Point(0, 2),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnl.Controls.Add(lblTitle);

        var lblBadge = new Label
        {
            Text = badge,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(w - 45, 2),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            BackColor = Color.Transparent
        };
        pnl.Controls.Add(lblBadge);

        return pnl;
    }

    private static Label CreateFieldLabel(string text, int x, int y, bool required = false)
    {
        return new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 8.8F, required ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = required ? Color.FromArgb(15, 23, 42) : Color.FromArgb(71, 85, 105),
            Location = new Point(x, y + 2),
            AutoSize = true,
            BackColor = Color.Transparent
        };
    }

    private static Label CreateFieldColon(int x, int y)
    {
        return new Label
        {
            Text = ":",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(x, y + 2),
            AutoSize = true,
            BackColor = Color.Transparent
        };
    }

    private TextBox CreateERPTextBox(int x, int y, int w, int h)
    {
        var tb = new TextBox
        {
            Location = new Point(x, y),
            Size = new Size(w, h),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(15, 23, 42),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        tb.TextChanged += (s, e) => _isDirty = true;
        return tb;
    }

    private static Button CreateActionButton(string text, int x, int y, int w, int h, bool isPrimary)
    {
        var btn = new Button
        {
            Text = text,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = isPrimary ? Color.White : Color.FromArgb(15, 23, 42),
            BackColor = isPrimary ? Color.FromArgb(11, 30, 51) : Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(w, h),
            Location = new Point(x, y),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = isPrimary ? Color.FromArgb(11, 30, 51) : Color.FromArgb(203, 213, 225);
        return btn;
    }

    private void LayoutHeader()
    {
        if (pnlHeader == null || btnCloseTop == null) return;
        btnCloseTop.Location = new Point(pnlHeader.Width - 30, 5);
        if (btnMaxRestoreTop != null)
        {
            btnMaxRestoreTop.Visible = TopLevel;
            btnMaxRestoreTop.Location = new Point(btnCloseTop.Left - 28, 5);
        }
        if (lblHdrModule != null)
        {
            lblHdrModule.Visible = false;
        }
    }

    private void LayoutSubBar()
    {
        if (pnlSubBar == null || btnSelectDrive == null || lblPathVal == null) return;
        btnSelectDrive.Location = new Point(pnlSubBar.Width - btnSelectDrive.Width - 14, 4);
        lblPathVal.MaximumSize = new Size(Math.Max(100, btnSelectDrive.Left - lblPathVal.Left - 10), 20);
        lblPathVal.AutoEllipsis = true;
    }

    private void LayoutNotice()
    {
        if (pnlNotice == null || lblEngineStatus == null || lblGuardNotice == null) return;
        lblEngineStatus.Location = new Point(pnlNotice.Width - lblEngineStatus.Width - 14, 6);
        lblGuardNotice.MaximumSize = new Size(Math.Max(100, lblEngineStatus.Left - lblGuardNotice.Left - 10), 20);
        lblGuardNotice.AutoEllipsis = true;
    }

    private void LayoutStatusStrip()
    {
        if (pnlStatusStrip == null || lblStatusRight == null || lblStatusLeft == null) return;
        lblStatusRight.Location = new Point(pnlStatusStrip.Width - lblStatusRight.Width - 14, 3);
        lblStatusLeft.MaximumSize = new Size(Math.Max(100, lblStatusRight.Left - lblStatusLeft.Left - 10), 18);
        lblStatusLeft.AutoEllipsis = true;
    }

    private void LayoutActionRail()
    {
        if (pnlActionRail == null || btnSaveAndOpen == null || btnSave == null || btnDiscard == null || btnCancel == null || lblKeyboardNav == null) return;
        int right = pnlActionRail.ClientSize.Width - 14;
        btnSaveAndOpen.Location = new Point(right - btnSaveAndOpen.Width, 8);
        btnSave.Location = new Point(btnSaveAndOpen.Left - 8 - btnSave.Width, 8);
        btnDiscard.Location = new Point(btnSave.Left - 8 - btnDiscard.Width, 8);
        btnCancel.Location = new Point(btnDiscard.Left - 8 - btnCancel.Width, 8);

        lblKeyboardNav.Location = new Point(14, 15);
        lblKeyboardNav.Visible = (btnCancel.Left > lblKeyboardNav.Right + 10);
    }

    private void ToggleMaximize()
    {
        WindowState = (WindowState == FormWindowState.Maximized) ? FormWindowState.Normal : FormWindowState.Maximized;
        UpdateMaxRestoreButtonState();
    }

    private void UpdateMaxRestoreButtonState()
    {
        if (btnMaxRestoreTop != null)
        {
            btnMaxRestoreTop.Text = (WindowState == FormWindowState.Maximized) ? "🗗" : "🗖";
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateMaxRestoreButtonState();
        LayoutHeader();
        LayoutSubBar();
        LayoutNotice();
        LayoutActionRail();
        LayoutStatusStrip();
        if (pnlMainContent != null) pnlMainContent.Invalidate();
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == ScreenFittingManager.WM_GETMINMAXINFO)
        {
            ScreenFittingManager.HandleGetMinMaxInfo(m.HWnd, m.LParam, 960, 500);
        }
        base.WndProc(ref m);
    }

    private void SetupFocusHighlighting(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            if (c is TextBox tb)
            {
                tb.Enter += (s, e) =>
                {
                    tb.BackColor = Color.FromArgb(254, 252, 232); // Pale yellow focus #FEFCE8
                };
                tb.Leave += (s, e) =>
                {
                    tb.BackColor = Color.White;
                };
                tb.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        SelectNextControl(tb, true, true, true, true);
                    }
                };
            }
            else if (c is ComboBox cb)
            {
                cb.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter)
                    {
                        e.SuppressKeyPress = true;
                        SelectNextControl(cb, true, true, true, true);
                    }
                };
            }
            else if (c.HasChildren)
            {
                SetupFocusHighlighting(c);
            }
        }
    }
}

