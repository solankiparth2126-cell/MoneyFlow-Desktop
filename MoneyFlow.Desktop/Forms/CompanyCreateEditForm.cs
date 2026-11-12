using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data.Storage;
using MoneyFlow.Desktop.Controls.Lookup;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Redesigned landscape modal popup for creating or altering a company.
/// Visually mirrors the Select Company screen (Reference Image 2) with:
/// - 1080px wide landscape layout to match Select Company proportions
/// - Strictly NO scrollbars (AutoScroll = false, fits naturally within ~715px height)
/// - Deep Navy (#0B2945) header bar with MoneyFlow ERP branding
/// - Removal of Company Data Path UI (inherited internally from Select Company)
/// - Universal MoneyFlow Common Dropdown (MoneyFlowTextLookup) for State, Country, and Currency
/// - Keyboard-first Enter navigation: Enter moves to next control, Shift+Enter moves to previous
/// - Dropdown auto-open on focus and auto-close on selection/deactivation
/// </summary>
public partial class CompanyCreateEditForm : Form
{
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, int wParam, int lParam);

    // Visual Palette strictly matching Select Company (Reference Image 2)
    private static readonly Color PrimaryNavy = Color.FromArgb(11, 41, 69);     // #0B2945
    private static readonly Color CanvasBg = Color.FromArgb(241, 245, 249);      // #F1F5F9
    private static readonly Color CardBg = Color.White;                          // #FFFFFF
    private static readonly Color PrimaryText = Color.FromArgb(11, 41, 69);     // #0B2945
    private static readonly Color SecondaryText = Color.FromArgb(100, 116, 139); // #64748B
    private static readonly Color BorderColor = Color.FromArgb(212, 225, 239);   // #D4E1EF
    private static readonly Color InputFocus = Color.FromArgb(57, 133, 255);     // #3985FF
    private static readonly Color HoverBg = Color.FromArgb(237, 245, 255);       // #EDF5FF
    private static readonly Color PlaceholderColor = Color.FromArgb(148, 163, 184); // #94A3B8
    private static readonly Color DividerColor = Color.FromArgb(226, 234, 243);  // #E2EAF3
    private static readonly Color ErrorRed = Color.FromArgb(220, 38, 38);        // #DC2626

    private readonly ICompanyService _companyService;
    private readonly SystemConfiguration? _systemConfig;
    private readonly int? _companyIdToEdit;
    private readonly string? _initialDataPath;
    private string _effectiveDataPath = string.Empty;

    // Structural Containers
    private Panel pnlHeader = null!;
    private Panel pnlFooter = null!;
    private Panel pnlMainBody = null!;

    // Section Cards
    private Guna2Panel cardCompanyInfo = null!;
    private Guna2Panel cardFinancial = null!;
    private Guna2Panel cardSecurity = null!;

    // Header Controls
    private Label lblTitle = null!;
    private Label lblTitleSeparator = null!;
    private Label lblSubtitle = null!;
    private Guna2Button btnCloseHeader = null!;

    // Section A: Company Information Controls
    private Guna2TextBox txtCompanyName = null!;
    private Label lblCompanyNameError = null!;
    private Guna2TextBox txtCompanyNumber = null!;
    private Guna2TextBox txtAddress = null!;
    private MoneyFlowTextLookup lookupState = null!;
    private MoneyFlowTextLookup lookupCountry = null!;
    private Guna2TextBox txtPhone = null!;
    private Guna2TextBox txtEmail = null!;
    private Label lblEmailError = null!;
    private Guna2TextBox txtPAN = null!;
    private Label lblPANError = null!;

    // Section B: Financial Configuration Controls
    private DateTimePicker dtpFYFrom = null!;
    private DateTimePicker dtpBooksFrom = null!;
    private Label lblDateError = null!;
    private MoneyFlowTextLookup lookupCurrency = null!;

    // Section C: Security & Backup Controls
    private Guna2TextBox txtVaultPassword = null!;
    private Guna2TextBox txtConfirmPassword = null!;
    private Guna2Button btnToggleVaultPass = null!;
    private Guna2Button btnToggleConfirmPass = null!;
    private Label lblPasswordError = null!;
    private Guna2CheckBox chkAutoBackupOnExit = null!;
    private Guna2CheckBox chkDefaultLedgers = null!;

    // Footer Controls
    private Guna2Button btnSave = null!;
    private Guna2Button btnCancel = null!;

    // State
    public bool IsSaved { get; private set; }
    private bool _isSaving;
    private bool _userDragged;

    // Master Data Models for Common Dropdown
    public record StateItem(string Name, string Code);
    public record CountryItem(string Name, string Code);
    public record CurrencyItem(string Description, string Code, string Symbol);

    private static readonly List<StateItem> MasterStates = new()
    {
        new("Andaman and Nicobar Islands", "AN"),
        new("Andhra Pradesh", "AP"),
        new("Arunachal Pradesh", "AR"),
        new("Assam", "AS"),
        new("Bihar", "BR"),
        new("Chandigarh", "CH"),
        new("Chhattisgarh", "CG"),
        new("Dadra and Nagar Haveli and Daman and Diu", "DH"),
        new("Delhi", "DL"),
        new("Goa", "GA"),
        new("Gujarat", "GJ"),
        new("Haryana", "HR"),
        new("Himachal Pradesh", "HP"),
        new("Jammu and Kashmir", "JK"),
        new("Jharkhand", "JH"),
        new("Karnataka", "KA"),
        new("Kerala", "KL"),
        new("Ladakh", "LA"),
        new("Lakshadweep", "LD"),
        new("Madhya Pradesh", "MP"),
        new("Maharashtra", "MH"),
        new("Manipur", "MN"),
        new("Meghalaya", "ML"),
        new("Mizoram", "MZ"),
        new("Nagaland", "NL"),
        new("Odisha", "OR"),
        new("Puducherry", "PY"),
        new("Punjab", "PB"),
        new("Rajasthan", "RJ"),
        new("Sikkim", "SK"),
        new("Tamil Nadu", "TN"),
        new("Telangana", "TS"),
        new("Tripura", "TR"),
        new("Uttar Pradesh", "UP"),
        new("Uttarakhand", "UK"),
        new("West Bengal", "WB")
    };

    private static readonly List<CountryItem> MasterCountries = new()
    {
        new("India", "IN"),
        new("United States", "US"),
        new("United Kingdom", "GB"),
        new("United Arab Emirates", "AE"),
        new("Singapore", "SG"),
        new("Canada", "CA"),
        new("Australia", "AU"),
        new("Germany", "DE"),
        new("Japan", "JP"),
        new("Saudi Arabia", "SA")
    };

    private static readonly List<CurrencyItem> MasterCurrencies = new()
    {
        new("Indian Rupee", "INR", "₹"),
        new("US Dollar", "USD", "$"),
        new("Euro", "EUR", "€"),
        new("British Pound", "GBP", "£"),
        new("UAE Dirham", "AED", "د.إ"),
        new("Singapore Dollar", "SGD", "S$")
    };

    public CompanyCreateEditForm(
        ICompanyService companyService,
        SystemConfiguration? systemConfig = null,
        int? companyIdToEdit = null,
        string? initialDataPath = null)
    {
        _companyService = companyService ?? throw new ArgumentNullException(nameof(companyService));
        _systemConfig = systemConfig;
        _companyIdToEdit = companyIdToEdit;
        _initialDataPath = initialDataPath;

        ResolveEffectiveDataPath();

        bool isEdit = _companyIdToEdit.HasValue;

        this.Text = isEdit ? "MoneyFlow Desktop — Alter Company" : "MoneyFlow Desktop — Create Company";
        this.Width = 1080; // Landscape orientation: ~1050-1150px
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.None;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = CanvasBg;
        this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        this.KeyPreview = true;
        this.DoubleBuffered = true;

        var appIcon = ExecLedgerIcons.GetAppIcon();
        if (appIcon != null)
        {
            this.Icon = appIcon;
            this.ShowIcon = true;
        }

        // Soft modal elevation shadow
        _ = new Guna2ShadowForm
        {
            TargetForm = this,
            ShadowColor = Color.FromArgb(15, 23, 42)
        };

        // 1. Build Header Section (Pinned top, 56px)
        BuildHeaderSection(isEdit);

        // 2. Build Footer Section (Pinned bottom, 56px)
        BuildFooterSection(isEdit);

        // 3. Build Landscape Body Section (Strictly NO SCROLLBARS)
        BuildBodySection(isEdit);

        // 4. Wire Keyboard Enter Navigation & Dropdown Focus Handling
        SetupEnterNavigation(isEdit);

        // Assemble controls
        this.Controls.Add(pnlMainBody);
        this.Controls.Add(pnlFooter);
        this.Controls.Add(pnlHeader);

        pnlMainBody.BringToFront();

        // Calculate natural content-based height to fit perfectly without scrollbars
        UpdateDynamicHeight();

        this.Shown += (s, e) =>
        {
            UpdateDynamicHeight();
            txtCompanyName.Focus();
        };

        this.Resize += (s, e) => LayoutCards();
    }

    // ═══════════════════════════════════════════════════════════════
    //  HEADER SECTION (56px Deep Navy #0B2945)
    // ═══════════════════════════════════════════════════════════════
    private void BuildHeaderSection(bool isEdit)
    {
        pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 56,
            BackColor = PrimaryNavy
        };
        pnlHeader.MouseDown += Header_MouseDown;

        var picLogo = new PictureBox
        {
            Image = ExecLedgerIcons.GetAppLogo(30, 30),
            Size = new Size(30, 30),
            Location = new Point(20, 13),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        picLogo.MouseDown += Header_MouseDown;
        pnlHeader.Controls.Add(picLogo);

        lblTitle = new Label
        {
            Text = isEdit ? "Alter Company" : "Create Company",
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(60, 15),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblTitle.MouseDown += Header_MouseDown;
        pnlHeader.Controls.Add(lblTitle);

        lblTitleSeparator = new Label
        {
            Text = "|",
            Font = new Font("Segoe UI", 11F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(lblTitle.Right + 8, 17),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblTitleSeparator.MouseDown += Header_MouseDown;
        pnlHeader.Controls.Add(lblTitleSeparator);

        lblSubtitle = new Label
        {
            Text = "MoneyFlow ERP",
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = Color.FromArgb(203, 213, 225),
            Location = new Point(lblTitleSeparator.Right + 8, 18),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblSubtitle.MouseDown += Header_MouseDown;
        pnlHeader.Controls.Add(lblSubtitle);

        btnCloseHeader = new Guna2Button
        {
            Text = "✕",
            Size = new Size(50, 56),
            Font = new Font("Segoe UI", 11.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(226, 232, 240),
            FillColor = Color.Transparent,
            BorderThickness = 0,
            BorderRadius = 0,
            Cursor = Cursors.Hand,
            HoverState = { FillColor = Color.FromArgb(220, 38, 38), ForeColor = Color.White }
        };
        btnCloseHeader.Click += (s, e) => this.Close();
        pnlHeader.Controls.Add(btnCloseHeader);

        pnlHeader.Resize += (s, e) =>
        {
            btnCloseHeader.Location = new Point(pnlHeader.Width - btnCloseHeader.Width, 0);
            lblTitleSeparator.Location = new Point(lblTitle.Right + 8, 17);
            lblSubtitle.Location = new Point(lblTitleSeparator.Right + 8, 18);
        };
    }

    private void Header_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _userDragged = true;
            ReleaseCapture();
            SendMessage(Handle, 0x0112, 0xF010 + 2, 0);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  FOOTER ACTION AREA (56px Pinned Bottom)
    // ═══════════════════════════════════════════════════════════════
    private void BuildFooterSection(bool isEdit)
    {
        pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = CanvasBg
        };

        var lineDivider = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = DividerColor
        };
        pnlFooter.Controls.Add(lineDivider);

        // Cancel Button (Secondary)
        btnCancel = new Guna2Button
        {
            Text = "Cancel (Esc)",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = PrimaryNavy,
            FillColor = Color.White,
            BorderColor = BorderColor,
            BorderThickness = 1,
            BorderRadius = 6,
            Size = new Size(120, 38),
            Cursor = Cursors.Hand,
            HoverState = { FillColor = HoverBg, BorderColor = Color.FromArgb(148, 163, 184) }
        };
        btnCancel.Click += (s, e) => this.Close();
        pnlFooter.Controls.Add(btnCancel);

        // Create / Save Button (Primary Navy)
        btnSave = new Guna2Button
        {
            Text = isEdit ? "Save Changes (Enter)" : "Create Company (Enter)",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.White,
            FillColor = PrimaryNavy,
            BorderThickness = 0,
            BorderRadius = 6,
            Size = new Size(195, 38),
            Cursor = Cursors.Hand,
            HoverState = { FillColor = Color.FromArgb(22, 62, 102) }
        };
        btnSave.Click += async (s, e) => await SaveAsync();
        pnlFooter.Controls.Add(btnSave);

        pnlFooter.Resize += (s, e) =>
        {
            int y = (pnlFooter.Height - 38) / 2;
            btnCancel.Location = new Point(pnlFooter.Width - 20 - btnCancel.Width, y);
            btnSave.Location = new Point(btnCancel.Left - 12 - btnSave.Width, y);
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  LANDSCAPE BODY (Strictly No Scrollbars)
    // ═══════════════════════════════════════════════════════════════
    private void BuildBodySection(bool isEdit)
    {
        pnlMainBody = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = CanvasBg,
            AutoScroll = false, // Strictly NO scrollbars!
            Padding = new Padding(20, 8, 20, 8)
        };

        cardCompanyInfo = CreateCardPanel();
        cardFinancial = CreateCardPanel();
        cardSecurity = CreateCardPanel();

        pnlMainBody.Controls.Add(cardCompanyInfo);
        pnlMainBody.Controls.Add(cardFinancial);
        pnlMainBody.Controls.Add(cardSecurity);

        BuildCompanyInfoCard();
        BuildFinancialCard(isEdit);
        BuildSecurityCard(isEdit);

        LayoutCards();
    }

    private Guna2Panel CreateCardPanel()
    {
        return new Guna2Panel
        {
            FillColor = CardBg,
            BorderColor = BorderColor,
            BorderThickness = 1,
            BorderRadius = 8,
            BackColor = Color.Transparent
        };
    }

    private void AddSectionHeader(Guna2Panel card, string title)
    {
        var lbl = new Label
        {
            Text = title,
            UseMnemonic = false,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = PrimaryNavy,
            Location = new Point(18, 10),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        card.Controls.Add(lbl);

        var divider = new Panel
        {
            Location = new Point(18, 30),
            Size = new Size(card.Width - 36, 1),
            BackColor = DividerColor,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        card.Controls.Add(divider);
    }

    // ─────────────────────────────────────────────────────────────
    //  SECTION A: COMPANY INFORMATION (Landscape Balanced Layout)
    // ─────────────────────────────────────────────────────────────
    private void BuildCompanyInfoCard()
    {
        cardCompanyInfo.Controls.Clear();
        AddSectionHeader(cardCompanyInfo, "🏢   COMPANY INFORMATION");

        int cardW = 1040;
        int usableW = cardW - 36; // 1004px
        int colW = (usableW - 18) / 2; // 493px
        int col1X = 18;
        int col2X = col1X + colW + 18; // 529px

        int y = 40;

        // Row 1: Company Name (Left) | Company Number / Entity Code (Right)
        AddLabel(cardCompanyInfo, "Company Name *", col1X, y, isRequired: true);
        AddLabel(cardCompanyInfo, "Company Number / Entity Code", col2X, y);
        y += 20;

        txtCompanyName = CreateInput(col1X, y, colW, placeholder: "Enter official registered company name");
        txtCompanyName.TabIndex = 1;
        txtCompanyName.TextChanged += (s, e) =>
        {
            if (lblCompanyNameError.Visible && !string.IsNullOrWhiteSpace(txtCompanyName.Text))
            {
                lblCompanyNameError.Visible = false;
                txtCompanyName.BorderColor = BorderColor;
            }
        };
        cardCompanyInfo.Controls.Add(txtCompanyName);

        txtCompanyNumber = CreateInput(col2X, y, colW, placeholder: "Auto-generated if blank (e.g. 010002)");
        txtCompanyNumber.TabIndex = 2;
        cardCompanyInfo.Controls.Add(txtCompanyNumber);
        y += 40;

        lblCompanyNameError = CreateErrorLabel(col1X, y - 4, colW);
        cardCompanyInfo.Controls.Add(lblCompanyNameError);

        // Row 2: Registered Address (Full width spanning both columns)
        AddLabel(cardCompanyInfo, "Registered Address", col1X, y);
        y += 20;

        txtAddress = new Guna2TextBox
        {
            Location = new Point(col1X, y),
            Size = new Size(usableW, 46),
            Multiline = true,
            AcceptsReturn = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = PrimaryText,
            FillColor = Color.White,
            BorderColor = BorderColor,
            BorderThickness = 1,
            BorderRadius = 6,
            PlaceholderText = "Door/Building No., Street, Area, City, Pincode...",
            PlaceholderForeColor = PlaceholderColor,
            TabIndex = 3
        };
        txtAddress.FocusedState.BorderColor = InputFocus;
        txtAddress.HoverState.BorderColor = Color.FromArgb(148, 163, 184);
        cardCompanyInfo.Controls.Add(txtAddress);
        y += 52;

        // Row 3: State (Left, Common Dropdown) | Country (Right, Common Dropdown)
        AddLabel(cardCompanyInfo, "State", col1X, y);
        AddLabel(cardCompanyInfo, "Country", col2X, y);
        y += 20;

        // Common Dropdown: State
        lookupState = CreateCommonDropdown(col1X, y, colW, "SELECT STATE", "Select State...");
        var stateProvider = new ListLookupProvider<StateItem>(
            MasterStates,
            s => s.Name,
            s => s.Name,
            s => s.Code
        );
        lookupState.SetProvider(stateProvider, new LookupConfig
        {
            Title = "SELECT STATE",
            Placeholder = "Select State...",
            DescriptionColumnHeader = "Description",
            CodeColumnHeader = "Code",
            ShowCodeColumn = true,
            DefaultWidth = 460,
            DefaultHeight = 360,
            AllowClear = false
        });
        lookupState.Text = "Gujarat";
        lookupState.TabIndex = 4;
        cardCompanyInfo.Controls.Add(lookupState);

        // Common Dropdown: Country
        lookupCountry = CreateCommonDropdown(col2X, y, colW, "SELECT COUNTRY", "Select Country...");
        var countryProvider = new ListLookupProvider<CountryItem>(
            MasterCountries,
            c => c.Name,
            c => c.Name,
            c => c.Code
        );
        lookupCountry.SetProvider(countryProvider, new LookupConfig
        {
            Title = "SELECT COUNTRY",
            Placeholder = "Select Country...",
            DescriptionColumnHeader = "Description",
            CodeColumnHeader = "Code",
            ShowCodeColumn = true,
            DefaultWidth = 460,
            DefaultHeight = 360,
            AllowClear = false
        });
        lookupCountry.Text = _systemConfig?.Country ?? "India";
        lookupCountry.TabIndex = 5;
        lookupCountry.SelectedValueChanged += (s, e) =>
        {
            if (lookupCountry.Text.Trim().Equals("India", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(lookupState.Text))
                {
                    lookupState.Text = "Gujarat";
                }
            }
        };
        cardCompanyInfo.Controls.Add(lookupCountry);
        y += 42;

        // Row 4: Phone / Mobile (Left) | Email Address (Right)
        AddLabel(cardCompanyInfo, "Phone / Mobile", col1X, y);
        AddLabel(cardCompanyInfo, "Email Address", col2X, y);
        y += 20;

        txtPhone = CreateInput(col1X, y, colW, placeholder: "e.g. +91 98765 43210");
        txtPhone.TabIndex = 6;
        cardCompanyInfo.Controls.Add(txtPhone);

        txtEmail = CreateInput(col2X, y, colW, placeholder: "e.g. accounts@company.com");
        txtEmail.TabIndex = 7;
        txtEmail.TextChanged += (s, e) =>
        {
            if (lblEmailError.Visible)
            {
                lblEmailError.Visible = false;
                txtEmail.BorderColor = BorderColor;
            }
        };
        cardCompanyInfo.Controls.Add(txtEmail);
        y += 40;

        lblEmailError = CreateErrorLabel(col2X, y - 4, colW);
        cardCompanyInfo.Controls.Add(lblEmailError);

        // Row 5: PAN (Left)
        AddLabel(cardCompanyInfo, "PAN", col1X, y);
        y += 20;

        txtPAN = CreateInput(col1X, y, colW, placeholder: "e.g. ABCDE1234F");
        txtPAN.TabIndex = 8;
        txtPAN.TextChanged += (s, e) =>
        {
            if (lblPANError.Visible)
            {
                lblPANError.Visible = false;
                txtPAN.BorderColor = BorderColor;
            }
        };
        cardCompanyInfo.Controls.Add(txtPAN);
        y += 40;

        lblPANError = CreateErrorLabel(col1X, y - 4, colW);
        cardCompanyInfo.Controls.Add(lblPANError);
        y += 6;

        cardCompanyInfo.Height = y;
    }

    // ─────────────────────────────────────────────────────────────
    //  SECTION B: FINANCIAL CONFIGURATION (Horizontal Grid Layout)
    // ─────────────────────────────────────────────────────────────
    private void BuildFinancialCard(bool isEdit)
    {
        cardFinancial.Controls.Clear();
        AddSectionHeader(cardFinancial, "📅   FINANCIAL CONFIGURATION");

        int cardW = 1040;
        int usableW = cardW - 36; // 1004px
        int colW = (usableW - 32) / 3; // 324px
        int col1X = 18;
        int col2X = col1X + colW + 16; // 358px
        int col3X = col2X + colW + 16; // 698px

        int y = 40;

        // All 3 Controls on ONE Single Horizontal Row
        AddLabel(cardFinancial, "Financial Year From *", col1X, y, isRequired: true);
        AddLabel(cardFinancial, "Books Beginning From *", col2X, y, isRequired: true);
        AddLabel(cardFinancial, "Base Currency Symbol", col3X, y);
        y += 20;

        // 1. Financial Year From (Left)
        var pnlFYHost = CreateDateHost(col1X, y, colW, out dtpFYFrom, isEdit);
        dtpFYFrom.TabIndex = 9;
        cardFinancial.Controls.Add(pnlFYHost);

        // 2. Books Beginning From (Center)
        var pnlBooksHost = CreateDateHost(col2X, y, colW, out dtpBooksFrom, isEdit);
        dtpBooksFrom.TabIndex = 10;
        cardFinancial.Controls.Add(pnlBooksHost);

        // 3. Base Currency Symbol (Right, Common Dropdown)
        lookupCurrency = CreateCommonDropdown(col3X, y, colW, "SELECT CURRENCY", "Select Currency...");
        var currencyProvider = new ListLookupProvider<CurrencyItem>(
            MasterCurrencies,
            c => c.Symbol,
            c => $"{c.Description} ({c.Symbol})",
            c => c.Code
        );
        lookupCurrency.SetProvider(currencyProvider, new LookupConfig
        {
            Title = "SELECT CURRENCY",
            Placeholder = "Select Currency...",
            DescriptionColumnHeader = "Description",
            CodeColumnHeader = "Code",
            ShowCodeColumn = true,
            DefaultWidth = 460,
            DefaultHeight = 340,
            AllowClear = false
        });
        lookupCurrency.Text = _systemConfig?.CurrencySymbol ?? "₹";
        lookupCurrency.TabIndex = 11;
        cardFinancial.Controls.Add(lookupCurrency);
        y += 40;

        lblDateError = CreateErrorLabel(col1X, y - 4, usableW);
        cardFinancial.Controls.Add(lblDateError);
        y += 6;

        cardFinancial.Height = y;
    }

    // ─────────────────────────────────────────────────────────────
    //  SECTION C: SECURITY & BACKUP (Horizontal Layout)
    // ─────────────────────────────────────────────────────────────
    private void BuildSecurityCard(bool isEdit)
    {
        cardSecurity.Controls.Clear();
        AddSectionHeader(cardSecurity, "🔒   SECURITY & BACKUP");

        int cardW = 1040;
        int usableW = cardW - 36;
        int colW = (usableW - 18) / 2;
        int col1X = 18;
        int col2X = col1X + colW + 18;

        int y = 40;

        // Row 1: Vault Password (Left) | Repeat Vault Password (Right)
        AddLabel(cardSecurity, "Vault Password", col1X, y);
        AddLabel(cardSecurity, "Repeat Vault Password", col2X, y);
        y += 20;

        txtVaultPassword = CreateInput(col1X, y, colW, placeholder: "Optional password protection");
        txtVaultPassword.UseSystemPasswordChar = true;
        txtVaultPassword.TabIndex = 12;

        btnToggleVaultPass = CreatePasswordEyeButton(txtVaultPassword);
        cardSecurity.Controls.Add(btnToggleVaultPass);
        cardSecurity.Controls.Add(txtVaultPassword);

        txtConfirmPassword = CreateInput(col2X, y, colW, placeholder: "Confirm password");
        txtConfirmPassword.UseSystemPasswordChar = true;
        txtConfirmPassword.TabIndex = 13;

        btnToggleConfirmPass = CreatePasswordEyeButton(txtConfirmPassword);
        cardSecurity.Controls.Add(btnToggleConfirmPass);
        cardSecurity.Controls.Add(txtConfirmPassword);

        txtConfirmPassword.TextChanged += (s, e) =>
        {
            if (lblPasswordError.Visible && txtVaultPassword.Text == txtConfirmPassword.Text)
            {
                lblPasswordError.Visible = false;
                txtConfirmPassword.BorderColor = BorderColor;
            }
        };
        y += 40;

        lblPasswordError = CreateErrorLabel(col1X, y - 4, usableW);
        cardSecurity.Controls.Add(lblPasswordError);

        // Row 2: Auto-Backup Checkbox (Left) | Create Default Ledgers Checkbox (Right)
        chkAutoBackupOnExit = new Guna2CheckBox
        {
            Text = "Auto-Backup company data to company folder upon application exit",
            Location = new Point(col1X, y),
            Size = new Size(colW, 24),
            Checked = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = PrimaryText,
            Cursor = Cursors.Hand,
            TabIndex = 14
        };
        chkAutoBackupOnExit.CheckedState.FillColor = PrimaryNavy;
        chkAutoBackupOnExit.CheckedState.BorderColor = PrimaryNavy;
        chkAutoBackupOnExit.CheckedState.BorderRadius = 3;
        chkAutoBackupOnExit.UncheckedState.FillColor = Color.White;
        chkAutoBackupOnExit.UncheckedState.BorderColor = BorderColor;
        chkAutoBackupOnExit.UncheckedState.BorderRadius = 3;
        cardSecurity.Controls.Add(chkAutoBackupOnExit);

        chkDefaultLedgers = new Guna2CheckBox
        {
            Text = "Create default accounting ledgers (Cash, P&L, Sales, Purchase, etc.)",
            Location = new Point(col2X, y),
            Size = new Size(colW, 24),
            Checked = true,
            Visible = !isEdit,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = PrimaryText,
            Cursor = Cursors.Hand,
            TabIndex = 15
        };
        chkDefaultLedgers.CheckedState.FillColor = PrimaryNavy;
        chkDefaultLedgers.CheckedState.BorderColor = PrimaryNavy;
        chkDefaultLedgers.CheckedState.BorderRadius = 3;
        chkDefaultLedgers.UncheckedState.FillColor = Color.White;
        chkDefaultLedgers.UncheckedState.BorderColor = BorderColor;
        chkDefaultLedgers.UncheckedState.BorderRadius = 3;
        cardSecurity.Controls.Add(chkDefaultLedgers);

        y += 30;

        cardSecurity.Height = y;
    }

    // ─────────────────────────────────────────────────────────────
    //  CONTROL BUILDERS & STYLING
    // ─────────────────────────────────────────────────────────────
    private void AddLabel(Guna2Panel card, string text, int x, int y, bool isRequired = false)
    {
        var lbl = new Label
        {
            Text = text,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = isRequired ? PrimaryNavy : Color.FromArgb(30, 41, 59),
            Location = new Point(x, y),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        card.Controls.Add(lbl);
    }

    private Guna2TextBox CreateInput(int x, int y, int width, string placeholder = "")
    {
        var tb = new Guna2TextBox
        {
            Location = new Point(x, y),
            Size = new Size(width, 36),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = PrimaryText,
            FillColor = Color.White,
            BorderColor = BorderColor,
            BorderThickness = 1,
            BorderRadius = 6,
            PlaceholderText = placeholder,
            PlaceholderForeColor = PlaceholderColor
        };
        tb.FocusedState.BorderColor = InputFocus;
        tb.HoverState.BorderColor = Color.FromArgb(148, 163, 184);
        return tb;
    }

    /// <summary>
    /// Reusable MoneyFlow Common Dropdown component with dropdown arrow and exact styling.
    /// AutoOpenOnFocus is enabled to provide instant keyboard entry.
    /// </summary>
    private MoneyFlowTextLookup CreateCommonDropdown(int x, int y, int width, string title, string placeholder)
    {
        var lookup = new MoneyFlowTextLookup
        {
            Location = new Point(x, y),
            Size = new Size(width, 36),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            BorderColor = BorderColor,
            FocusBorderColor = InputFocus,
            BorderRadius = 6,
            ShowDropdownArrow = true,
            AutoOpenOnFocus = true,
            PlaceholderText = placeholder
        };
        return lookup;
    }

    private Guna2Panel CreateDateHost(int x, int y, int width, out DateTimePicker dtp, bool isEdit)
    {
        var host = new Guna2Panel
        {
            Location = new Point(x, y),
            Size = new Size(width, 36),
            BorderColor = BorderColor,
            BorderThickness = 1,
            BorderRadius = 6,
            FillColor = Color.White,
            BackColor = Color.Transparent
        };

        DateTime now = DateTime.Now;
        int fyStartYear = now.Month >= 4 ? now.Year : now.Year - 1;
        DateTime defaultFY = new DateTime(fyStartYear, 4, 1);

        dtp = new DateTimePicker
        {
            Location = new Point(4, 5),
            Width = width - 8,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MMM-yyyy",
            Value = defaultFY,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            Enabled = !isEdit
        };
        host.Controls.Add(dtp);
        return host;
    }

    private Guna2Button CreatePasswordEyeButton(Guna2TextBox targetInput)
    {
        var btn = new Guna2Button
        {
            Text = "👁",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = SecondaryText,
            FillColor = Color.Transparent,
            BorderThickness = 0,
            BorderRadius = 4,
            Size = new Size(30, 28),
            Cursor = Cursors.Hand,
            HoverState = { FillColor = HoverBg }
        };
        btn.Location = new Point(targetInput.Right - 32, targetInput.Top + 4);
        btn.Click += (s, e) =>
        {
            targetInput.UseSystemPasswordChar = !targetInput.UseSystemPasswordChar;
            btn.Text = targetInput.UseSystemPasswordChar ? "👁" : "🔒";
            btn.ForeColor = targetInput.UseSystemPasswordChar ? SecondaryText : InputFocus;
        };
        btn.BringToFront();
        return btn;
    }

    private Label CreateErrorLabel(int x, int y, int width)
    {
        return new Label
        {
            Location = new Point(x, y),
            Size = new Size(width, 16),
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            ForeColor = ErrorRed,
            Visible = false,
            BackColor = Color.Transparent
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  LANDSCAPE LAYOUT & NO SCROLLBARS GUARANTEE
    // ═══════════════════════════════════════════════════════════════
    private void LayoutCards()
    {
        int cardW = Math.Max(700, pnlMainBody.ClientSize.Width - 40);

        cardCompanyInfo.Location = new Point(20, 8);
        cardCompanyInfo.Width = cardW;

        cardFinancial.Location = new Point(20, cardCompanyInfo.Bottom + 10);
        cardFinancial.Width = cardW;

        cardSecurity.Location = new Point(20, cardFinancial.Bottom + 10);
        cardSecurity.Width = cardW;

        // Position eye toggle buttons on their inputs
        if (btnToggleVaultPass != null && txtVaultPassword != null)
        {
            btnToggleVaultPass.Location = new Point(txtVaultPassword.Right - 32, txtVaultPassword.Top + 4);
            btnToggleVaultPass.BringToFront();
        }
        if (btnToggleConfirmPass != null && txtConfirmPassword != null)
        {
            btnToggleConfirmPass.Location = new Point(txtConfirmPassword.Right - 32, txtConfirmPassword.Top + 4);
            btnToggleConfirmPass.BringToFront();
        }
    }

    /// <summary>
    /// Content-driven dynamic height calculation without scrollbars.
    /// In landscape orientation, the entire form fits naturally on desktop screens.
    /// </summary>
    public void UpdateDynamicHeight()
    {
        LayoutCards();

        int contentHeight = cardSecurity.Bottom + 12;
        int idealTotalHeight = pnlHeader.Height + contentHeight + pnlFooter.Height + 2;

        this.Height = idealTotalHeight;
        pnlMainBody.AutoScroll = false; // Strictly NO scrollbars!

        Screen screen = (this.Owner != null ? Screen.FromControl(this.Owner) : Screen.FromControl(this))
                        ?? Screen.PrimaryScreen
                        ?? Screen.AllScreens[0];

        // Center window on screen if user has not manually dragged it
        if (!_userDragged)
        {
            int top = screen.WorkingArea.Top + Math.Max(10, (screen.WorkingArea.Height - this.Height) / 2);
            int left = screen.WorkingArea.Left + Math.Max(10, (screen.WorkingArea.Width - this.Width) / 2);
            this.Location = new Point(left, top);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(BorderColor, 1);
        e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
    }

    // ═══════════════════════════════════════════════════════════════
    //  KEYBOARD SHORTCUTS & USER EXPERIENCE (ENTER NAVIGATION)
    // ═══════════════════════════════════════════════════════════════
    private readonly List<Control> _flowControls = new();

    private void SetupEnterNavigation(bool isEdit)
    {
        _flowControls.Clear();
        _flowControls.Add(txtCompanyName);
        _flowControls.Add(txtCompanyNumber);
        _flowControls.Add(txtAddress);
        _flowControls.Add(lookupState);
        _flowControls.Add(lookupCountry);
        _flowControls.Add(txtPhone);
        _flowControls.Add(txtEmail);
        _flowControls.Add(txtPAN);
        _flowControls.Add(dtpFYFrom);
        _flowControls.Add(dtpBooksFrom);
        _flowControls.Add(lookupCurrency);
        _flowControls.Add(txtVaultPassword);
        _flowControls.Add(txtConfirmPassword);
        _flowControls.Add(chkAutoBackupOnExit);
        if (!isEdit)
        {
            _flowControls.Add(chkDefaultLedgers);
        }
        _flowControls.Add(btnSave);

        for (int i = 0; i < _flowControls.Count; i++)
        {
            int idx = i;
            var ctrl = _flowControls[i];

            if (ctrl is Guna2TextBox gtb)
            {
                if (gtb == txtAddress)
                {
                    AttachAddressKeyEvents(gtb, () => MoveToNext(idx), () => MoveToPrevious(idx));
                }
                else
                {
                    AttachTextBoxKeyEvents(gtb, () => MoveToNext(idx), () => MoveToPrevious(idx));
                }
            }
            else if (ctrl is MoneyFlowTextLookup lookup)
            {
                lookup.SelectionCommitted += (s, e) =>
                {
                    BeginInvoke(new Action(() => MoveToNext(idx)));
                };
                lookup.EditorKeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter && !e.Alt && !e.Control)
                    {
                        if (e.Shift)
                        {
                            e.Handled = true;
                            e.SuppressKeyPress = true;
                            BeginInvoke(new Action(() => MoveToPrevious(idx)));
                        }
                    }
                };
            }
            else if (ctrl is DateTimePicker dtp)
            {
                dtp.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter && !e.Alt && !e.Control)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        if (e.Shift) MoveToPrevious(idx);
                        else MoveToNext(idx);
                    }
                };
            }
            else if (ctrl is Guna2CheckBox cb)
            {
                cb.KeyDown += (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter && !e.Alt && !e.Control)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        if (e.Shift) MoveToPrevious(idx);
                        else MoveToNext(idx);
                    }
                };
            }
            else if (ctrl is Guna2Button btn && btn == btnSave)
            {
                btn.KeyDown += async (s, e) =>
                {
                    if (e.KeyCode == Keys.Enter && !e.Alt && !e.Control && !e.Shift)
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        await SaveAsync();
                    }
                    else if ((e.KeyCode == Keys.Enter && e.Shift) || (e.KeyCode == Keys.Tab && e.Shift))
                    {
                        e.Handled = true;
                        e.SuppressKeyPress = true;
                        MoveToPrevious(idx);
                    }
                };
            }
        }
    }

    private void AttachTextBoxKeyEvents(Guna2TextBox tb, Action onEnter, Action onShiftEnter)
    {
        KeyEventHandler handler = (s, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Alt && !e.Control)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (e.Shift) onShiftEnter();
                else onEnter();
            }
        };

        tb.KeyDown += handler;
        foreach (Control c in tb.Controls)
        {
            c.KeyDown += handler;
        }
    }

    private void AttachAddressKeyEvents(Guna2TextBox tb, Action onEnter, Action onShiftEnter)
    {
        KeyEventHandler handler = (s, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Alt)
            {
                if (e.Shift || e.Control)
                {
                    // Allow newline inside multiline address when Shift/Ctrl held
                    return;
                }

                // Plain Enter jumps to next control
                e.Handled = true;
                e.SuppressKeyPress = true;
                onEnter();
            }
        };

        tb.KeyDown += handler;
        foreach (Control c in tb.Controls)
        {
            c.KeyDown += handler;
        }
    }

    public void MoveToNext(int currentIndex)
    {
        int next = currentIndex + 1;
        while (next < _flowControls.Count && (!_flowControls[next].Enabled || !_flowControls[next].Visible))
        {
            next++;
        }

        if (next < _flowControls.Count)
        {
            FocusControl(_flowControls[next]);
        }
        else
        {
            FocusControl(btnSave);
        }
    }

    public void MoveToPrevious(int currentIndex)
    {
        int prev = currentIndex - 1;
        while (prev >= 0 && (!_flowControls[prev].Enabled || !_flowControls[prev].Visible))
        {
            prev--;
        }

        if (prev >= 0)
        {
            FocusControl(_flowControls[prev]);
        }
    }

    private void FocusControl(Control ctrl)
    {
        CloseAllLookupPopups(ctrl as MoneyFlowTextLookup);

        if (ctrl is MoneyFlowTextLookup lookup)
        {
            lookup.Focus();
        }
        else if (ctrl is Guna2TextBox gtb)
        {
            gtb.Focus();
            gtb.SelectAll();
        }
        else
        {
            ctrl.Focus();
        }
    }

    private void CloseAllLookupPopups(MoneyFlowTextLookup? except = null)
    {
        if (lookupState != null && lookupState != except && lookupState.IsPopupOpen)
        {
            lookupState.ClosePopup();
        }
        if (lookupCountry != null && lookupCountry != except && lookupCountry.IsPopupOpen)
        {
            lookupCountry.ClosePopup();
        }
        if (lookupCurrency != null && lookupCurrency != except && lookupCurrency.IsPopupOpen)
        {
            lookupCurrency.ClosePopup();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            if (lookupState != null && lookupState.IsPopupOpen)
            {
                lookupState.ClosePopup();
                return true;
            }
            if (lookupCountry != null && lookupCountry.IsPopupOpen)
            {
                lookupCountry.ClosePopup();
                return true;
            }
            if (lookupCurrency != null && lookupCurrency.IsPopupOpen)
            {
                lookupCurrency.ClosePopup();
                return true;
            }

            this.Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ═══════════════════════════════════════════════════════════════
    //  DATA BINDING & PERSISTENCE
    // ═══════════════════════════════════════════════════════════════
    private async void LoadCompanyDataAsync(int companyId)
    {
        try
        {
            var company = await _companyService.GetCompanyByIdAsync(companyId);
            if (company != null)
            {
                txtCompanyName.Text = company.CompanyName;
                txtCompanyNumber.Text = company.CompanyNumber;
                txtAddress.Text = company.Address;
                lookupState.Text = company.State;
                lookupCountry.Text = company.Country;
                txtPAN.Text = company.PAN;
                txtEmail.Text = company.Email;
                txtPhone.Text = company.Phone;
                dtpFYFrom.Value = company.FinancialYearFrom;
                dtpBooksFrom.Value = company.BooksBeginningFrom;
                lookupCurrency.Text = company.Currency;
                chkAutoBackupOnExit.Checked = company.AutoBackupOnExit;

                if (company.IsPasswordProtected)
                {
                    txtVaultPassword.PlaceholderText = "(Password set — leave blank to keep)";
                    txtConfirmPassword.PlaceholderText = "(Leave blank to keep)";
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to load company details:\n{ex.Message}", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task SaveAsync()
    {
        if (_isSaving) return;

        // Reset inline error messages
        lblCompanyNameError.Visible = false;
        lblDateError.Visible = false;
        lblPANError.Visible = false;
        lblEmailError.Visible = false;
        lblPasswordError.Visible = false;

        txtCompanyName.BorderColor = BorderColor;
        txtPAN.BorderColor = BorderColor;
        txtEmail.BorderColor = BorderColor;
        txtConfirmPassword.BorderColor = BorderColor;

        string name = txtCompanyName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            lblCompanyNameError.Text = "Company Name is required.";
            lblCompanyNameError.Visible = true;
            txtCompanyName.BorderColor = ErrorRed;
            txtCompanyName.Focus();
            return;
        }

        if (dtpBooksFrom.Value.Date < dtpFYFrom.Value.Date)
        {
            lblDateError.Text = "Books Beginning date cannot be earlier than Financial Year beginning date.";
            lblDateError.Visible = true;
            dtpBooksFrom.Focus();
            return;
        }

        string pan = txtPAN.Text.Trim();
        if (!string.IsNullOrEmpty(pan) && !System.Text.RegularExpressions.Regex.IsMatch(pan, @"^[A-Z]{5}[0-9]{4}[A-Z]{1}$"))
        {
            lblPANError.Text = "Invalid PAN format. Must be 10 characters (e.g. ABCDE1234F).";
            lblPANError.Visible = true;
            txtPAN.BorderColor = ErrorRed;
            txtPAN.Focus();
            return;
        }

        string email = txtEmail.Text.Trim();
        if (!string.IsNullOrEmpty(email) && !email.Contains("@"))
        {
            lblEmailError.Text = "Invalid Email address format.";
            lblEmailError.Visible = true;
            txtEmail.BorderColor = ErrorRed;
            txtEmail.Focus();
            return;
        }

        string vaultPass = txtVaultPassword.Text.Trim();
        string confirmPass = txtConfirmPassword.Text.Trim();
        if (!string.IsNullOrEmpty(vaultPass) && vaultPass != confirmPass)
        {
            lblPasswordError.Text = "Passwords do not match. Please verify your Vault Password.";
            lblPasswordError.Visible = true;
            txtConfirmPassword.BorderColor = ErrorRed;
            txtConfirmPassword.Focus();
            return;
        }

        string originalSaveText = btnSave.Text;
        try
        {
            _isSaving = true;
            btnSave.Enabled = false;
            btnCancel.Enabled = false;
            btnSave.Text = "Saving...";

            string currencyText = lookupCurrency.Text.Trim();

            if (_companyIdToEdit.HasValue)
            {
                var updateDto = new CompanyUpdateDto
                {
                    CompanyId = _companyIdToEdit.Value,
                    CompanyName = name,
                    CompanyNumber = txtCompanyNumber.Text.Trim(),
                    Address = txtAddress.Text.Trim(),
                    State = lookupState.Text.Trim(),
                    Country = lookupCountry.Text.Trim(),
                    PAN = pan,
                    Email = email,
                    Phone = txtPhone.Text.Trim(),
                    Currency = string.IsNullOrWhiteSpace(currencyText) ? "₹" : currencyText,
                    NewVaultPassword = string.IsNullOrWhiteSpace(vaultPass) ? null : vaultPass,
                    AutoBackupOnExit = chkAutoBackupOnExit.Checked,
                    DataDirectory = _effectiveDataPath
                };

                await _companyService.UpdateCompanyAsync(updateDto);
                if (this.Visible)
                {
                    MessageBox.Show(this, "Company details updated successfully.", "Company Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                var createDto = new CompanyCreateDto
                {
                    CompanyName = name,
                    CompanyNumber = string.IsNullOrWhiteSpace(txtCompanyNumber.Text) ? null : txtCompanyNumber.Text.Trim(),
                    DataDirectory = _effectiveDataPath,
                    Address = txtAddress.Text.Trim(),
                    State = lookupState.Text.Trim(),
                    Country = lookupCountry.Text.Trim(),
                    PAN = pan,
                    Email = email,
                    Phone = txtPhone.Text.Trim(),
                    FinancialYearFrom = dtpFYFrom.Value,
                    BooksBeginningFrom = dtpBooksFrom.Value,
                    Currency = string.IsNullOrWhiteSpace(currencyText) ? "₹" : currencyText,
                    CreateDefaultLedgers = chkDefaultLedgers.Checked,
                    VaultPassword = string.IsNullOrWhiteSpace(vaultPass) ? null : vaultPass,
                    AutoBackupOnExit = chkAutoBackupOnExit.Checked
                };

                await _companyService.CreateCompanyAsync(createDto);
                if (this.Visible)
                {
                    MessageBox.Show(this, $"Company '{name}' created successfully with Chart of Accounts.", "Company Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }

            IsSaved = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException != null ? $"{ex.Message}\n\nDetails: {ex.InnerException.Message}" : ex.Message;
            MessageBox.Show(this, $"Failed to save company:\n{msg}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isSaving = false;
            btnSave.Enabled = true;
            btnCancel.Enabled = true;
            btnSave.Text = originalSaveText;
        }
    }

    private void ResolveEffectiveDataPath()
    {
        if (!string.IsNullOrWhiteSpace(_initialDataPath))
        {
            _effectiveDataPath = _initialDataPath;
            return;
        }

        string candidate = _systemConfig?.CompanyDataPath
            ?? Environment.GetEnvironmentVariable("MONEYFLOW_DATA_PATH")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "MoneyFlow", "Data", "Companies");

        _effectiveDataPath = candidate;
    }
}
