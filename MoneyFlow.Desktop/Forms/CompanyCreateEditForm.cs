using System;
using System.Drawing;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data.Storage;

namespace MoneyFlow.Desktop.Forms;

public class CompanyCreateEditForm : Form
{
    private readonly ICompanyService _companyService;
    private readonly SystemConfiguration? _systemConfig;
    private readonly int? _companyIdToEdit;

    // UI Controls
    private TextBox txtCompanyName = null!;
    private TextBox txtAddress = null!;
    private ComboBox cmbState = null!;
    private ComboBox cmbCountry = null!;
    private TextBox txtPAN = null!;
    private TextBox txtEmail = null!;
    private TextBox txtPhone = null!;
    private DateTimePicker dtpFYFrom = null!;
    private DateTimePicker dtpBooksFrom = null!;
    private TextBox txtCurrency = null!;
    private TextBox txtCompanyNumber = null!;
    private TextBox txtDataPath = null!;
    private Button btnBrowseDataPath = null!;
    private TextBox txtVaultPassword = null!;
    private TextBox txtConfirmPassword = null!;
    private CheckBox chkAutoBackupOnExit = null!;
    private CheckBox chkDefaultLedgers = null!;
    private Button btnSave = null!;
    private Button btnCancel = null!;

    private static readonly string[] Countries = new[]
    {
        "India",
        "Australia",
        "Canada",
        "Germany",
        "France",
        "Japan",
        "Nepal",
        "Singapore",
        "United Arab Emirates",
        "United Kingdom",
        "United States",
        "Other"
    };

    private static readonly string[] IndianStates = new[]
    {
        "Andaman and Nicobar Islands",
        "Andhra Pradesh",
        "Arunachal Pradesh",
        "Assam",
        "Bihar",
        "Chandigarh",
        "Chhattisgarh",
        "Dadra and Nagar Haveli and Daman and Diu",
        "Delhi",
        "Goa",
        "Gujarat",
        "Haryana",
        "Himachal Pradesh",
        "Jammu and Kashmir",
        "Jharkhand",
        "Karnataka",
        "Kerala",
        "Ladakh",
        "Lakshadweep",
        "Madhya Pradesh",
        "Maharashtra",
        "Manipur",
        "Meghalaya",
        "Mizoram",
        "Nagaland",
        "Odisha",
        "Puducherry",
        "Punjab",
        "Rajasthan",
        "Sikkim",
        "Tamil Nadu",
        "Telangana",
        "Tripura",
        "Uttar Pradesh",
        "Uttarakhand",
        "West Bengal"
    };

    public bool IsSaved { get; private set; }
    private readonly string? _initialDataPath;

    public CompanyCreateEditForm(ICompanyService companyService, SystemConfiguration? systemConfig = null, int? companyIdToEdit = null, string? initialDataPath = null)
    {
        _companyService = companyService;
        _systemConfig = systemConfig;
        _companyIdToEdit = companyIdToEdit;
        _initialDataPath = initialDataPath;
        InitializeComponent();
        if (_companyIdToEdit.HasValue)
        {
            LoadCompanyDataAsync(_companyIdToEdit.Value);
        }
    }

    public CompanyCreateEditForm(ICompanyService companyService, int? companyIdToEdit)
        : this(companyService, null, companyIdToEdit)
    {
    }

    private void InitializeComponent()
    {
        bool isEdit = _companyIdToEdit.HasValue;
        this.Text = isEdit ? "MoneyFlow Desktop — Alter Company" : "MoneyFlow Desktop — Create Company";
        this.Size = new Size(640, 770);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.FromArgb(245, 247, 250);
        this.Font = ExecLedgerTheme.UIRegular9;

        var appIcon = ExecLedgerIcons.GetAppIcon();
        if (appIcon != null)
        {
            this.Icon = appIcon;
            this.ShowIcon = true;
        }

        // Header Panel
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.FromArgb(24, 43, 73)
        };
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
            }
        };
        grp.Controls.Add(btnBrowseDataPath);

        y += spacing;
        AddLabel(grp, "Company Name *:", 20, y);
        txtCompanyName = AddTextBox(grp, 180, y, 370);

        y += spacing;
        AddLabel(grp, "Company Number:", 20, y);
        txtCompanyNumber = AddTextBox(grp, 180, y, 160);
        var lblNumHint = new Label
        {
            Text = "(Leave blank to auto-generate e.g. 010002)",
            Font = ExecLedgerTheme.UIRegular8,
            ForeColor = Color.FromArgb(100, 116, 139),
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
            AutoSize = true,
            Checked = true
        };
        grp.Controls.Add(chkAutoBackupOnExit);

        // Default ledgers
        y += spacing;
        chkDefaultLedgers = new CheckBox
        {
            Text = "Create default accounting ledgers (Cash, P&L, Sales, Purchase...)",
            Location = new Point(180, y),
            AutoSize = true,
            Checked = true,
            Visible = !isEdit
        };
        grp.Controls.Add(chkDefaultLedgers);

        this.Controls.Add(grp);

        // Buttons
        btnSave = new Button
        {
            Text = isEdit ? "Save Changes (Enter)" : "Create Company (Enter)",
            Location = new Point(290, 685),
            Size = new Size(185, 35),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            Font = ExecLedgerTheme.UIBold9
        };
        btnSave.Click += async (s, e) => await SaveAsync();

        btnCancel = new Button
        {
            Text = "Cancel (Esc)",
            Location = new Point(485, 685),
            Size = new Size(120, 35),
            BackColor = Color.FromArgb(226, 232, 240)
        };
        btnCancel.Click += (s, e) => this.Close();

        this.Controls.Add(btnSave);
        this.Controls.Add(btnCancel);

        this.AcceptButton = btnSave;
        this.CancelButton = btnCancel;
    }

    private void AddLabel(GroupBox grp, string text, int x, int y)
    {
        grp.Controls.Add(new Label { Text = text, Location = new Point(x, y), AutoSize = true });
    }

    private TextBox AddTextBox(GroupBox grp, int x, int y, int width)
    {
        var tb = new TextBox { Location = new Point(x, y - 3), Width = width };
        grp.Controls.Add(tb);
        return tb;
    }

    private ComboBox AddComboBox(GroupBox grp, int x, int y, int width, string[] items, string defaultText = "")
    {
        var cb = new ComboBox
        {
            Location = new Point(x, y - 3),
            Width = width,
            DropDownStyle = ComboBoxStyle.DropDown,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems
        };
        cb.Items.AddRange(items);
        if (!string.IsNullOrEmpty(defaultText))
        {
            cb.Text = defaultText;
        }
        grp.Controls.Add(cb);
        return cb;
    }

    private async void LoadCompanyDataAsync(int companyId)
    {
        var company = await _companyService.GetCompanyByIdAsync(companyId);
        if (company != null)
        {
            txtCompanyName.Text = company.CompanyName;
            txtCompanyNumber.Text = company.CompanyNumber;
            if (!string.IsNullOrWhiteSpace(company.DataDirectory))
            {
                txtDataPath.Text = company.DataDirectory;
            }
            txtAddress.Text = company.Address;
            cmbState.Text = company.State;
            cmbCountry.Text = company.Country;
            txtPAN.Text = company.PAN;
            txtEmail.Text = company.Email;
            txtPhone.Text = company.Phone;
            dtpFYFrom.Value = company.FinancialYearFrom;
            dtpBooksFrom.Value = company.BooksBeginningFrom;
            txtCurrency.Text = company.Currency;
            chkAutoBackupOnExit.Checked = company.AutoBackupOnExit;
            if (company.IsPasswordProtected)
            {
                txtVaultPassword.PlaceholderText = "(Password set)";
                txtConfirmPassword.PlaceholderText = "(Leave blank to keep)";
            }
        }
    }

    private async Task SaveAsync()
    {
        string name = txtCompanyName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Please enter a valid Company Name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtCompanyName.Focus();
            return;
        }

        string dataDir = txtDataPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(dataDir))
        {
            MessageBox.Show("Please enter a valid Company Data Path.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtDataPath.Focus();
            return;
        }

        // Validate password confirmation
        if (!string.IsNullOrEmpty(txtVaultPassword.Text) || !string.IsNullOrEmpty(txtConfirmPassword.Text))
        {
            if (txtVaultPassword.Text != txtConfirmPassword.Text)
            {
                MessageBox.Show("Vault password and confirmation password do not match. Please re-enter.", "Password Mismatch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtConfirmPassword.Focus();
                return;
            }
        }

        btnSave.Enabled = false;

        try
        {
            if (_companyIdToEdit.HasValue)
            {
                var updateDto = new CompanyUpdateDto
                {
                    CompanyId = _companyIdToEdit.Value,
                    CompanyName = name,
                    CompanyNumber = txtCompanyNumber.Text.Trim(),
                    DataDirectory = dataDir,
                    Address = txtAddress.Text,
                    State = cmbState.Text.Trim(),
                    Country = cmbCountry.Text.Trim(),
                    PAN = txtPAN.Text,
                    Email = txtEmail.Text,
                    Phone = txtPhone.Text,
                    Currency = txtCurrency.Text,
                    IsActive = true,
                    NewVaultPassword = string.IsNullOrWhiteSpace(txtVaultPassword.Text) ? null : txtVaultPassword.Text,
                    AutoBackupOnExit = chkAutoBackupOnExit.Checked
                };

                await _companyService.UpdateCompanyAsync(updateDto);
                MessageBox.Show("Company details updated successfully.", "Company Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                var createDto = new CompanyCreateDto
                {
                    CompanyName = name,
                    CompanyNumber = txtCompanyNumber.Text.Trim(),
                    DataDirectory = dataDir,
                    Address = txtAddress.Text,
                    State = cmbState.Text.Trim(),
                    Country = cmbCountry.Text.Trim(),
                    PAN = txtPAN.Text,
                    Email = txtEmail.Text,
                    Phone = txtPhone.Text,
                    FinancialYearFrom = dtpFYFrom.Value,
                    BooksBeginningFrom = dtpBooksFrom.Value,
                    Currency = txtCurrency.Text,
                    CreateDefaultLedgers = chkDefaultLedgers.Checked,
                    VaultPassword = string.IsNullOrWhiteSpace(txtVaultPassword.Text) ? null : txtVaultPassword.Text,
                    AutoBackupOnExit = chkAutoBackupOnExit.Checked
                };

                await _companyService.CreateCompanyAsync(createDto);
                MessageBox.Show($"Company '{name}' created successfully with Chart of Accounts at {dataDir}.", "Company Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            IsSaved = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException != null ? $"{ex.Message}\n\nDetails: {ex.InnerException.Message}" : ex.Message;
            MessageBox.Show($"Failed to save company:\n{msg}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            btnSave.Enabled = true;
        }
    }
}
