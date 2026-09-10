using System;
using System.Drawing;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Forms;

public class CompanyCreateEditForm : Form
{
    private readonly ICompanyService _companyService;
    private readonly int? _companyIdToEdit;

    // UI Controls
    private TextBox txtCompanyName = null!;
    private TextBox txtAddress = null!;
    private TextBox txtState = null!;
    private TextBox txtCountry = null!;
    private TextBox txtPAN = null!;
    private TextBox txtEmail = null!;
    private TextBox txtPhone = null!;
    private DateTimePicker dtpFYFrom = null!;
    private DateTimePicker dtpBooksFrom = null!;
    private TextBox txtCurrency = null!;
    private CheckBox chkDefaultLedgers = null!;
    private Button btnSave = null!;
    private Button btnCancel = null!;

    public bool IsSaved { get; private set; }

    public CompanyCreateEditForm(ICompanyService companyService, int? companyIdToEdit = null)
    {
        _companyService = companyService;
        _companyIdToEdit = companyIdToEdit;
        InitializeComponent();
        if (_companyIdToEdit.HasValue)
        {
            LoadCompanyDataAsync(_companyIdToEdit.Value);
        }
    }

    private void InitializeComponent()
    {
        bool isEdit = _companyIdToEdit.HasValue;
        this.Text = isEdit ? "MoneyFlow Desktop — Alter Company" : "MoneyFlow Desktop — Create Company";
        this.Size = new Size(620, 600);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.FromArgb(245, 247, 250);
        this.Font = new Font("Segoe UI", 9.5F);

        // Header Panel
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.FromArgb(24, 43, 73)
        };
        var lblTitle = new Label
        {
            Text = isEdit ? "Alter Company" : "Company Creation",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 16),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblTitle);
        this.Controls.Add(headerPanel);

        // Form Fields Group
        var grp = new GroupBox
        {
            Text = "Company Details",
            Location = new Point(20, 75),
            Size = new Size(560, 415),
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        int y = 28;
        int spacing = 34;

        AddLabel(grp, "Company Name *:", 20, y);
        txtCompanyName = AddTextBox(grp, 180, y, 350);

        y += spacing;
        AddLabel(grp, "Address:", 20, y);
        txtAddress = AddTextBox(grp, 180, y, 350);

        y += spacing;
        AddLabel(grp, "State:", 20, y);
        txtState = AddTextBox(grp, 180, y, 350);

        y += spacing;
        AddLabel(grp, "Country:", 20, y);
        txtCountry = AddTextBox(grp, 180, y, 350);
        txtCountry.Text = "India";

        y += spacing;
        AddLabel(grp, "PAN:", 20, y);
        txtPAN = AddTextBox(grp, 180, y, 350);

        y += spacing;
        AddLabel(grp, "Email:", 20, y);
        txtEmail = AddTextBox(grp, 180, y, 350);

        y += spacing;
        AddLabel(grp, "Phone:", 20, y);
        txtPhone = AddTextBox(grp, 180, y, 350);

        y += spacing;
        AddLabel(grp, "Financial Year From:", 20, y);
        dtpFYFrom = new DateTimePicker
        {
            Location = new Point(180, y - 3),
            Width = 350,
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
            Width = 350,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MMM-yyyy",
            Value = new DateTime(2026, 4, 1),
            Enabled = !isEdit
        };
        grp.Controls.Add(dtpBooksFrom);

        y += spacing;
        AddLabel(grp, "Base Currency:", 20, y);
        txtCurrency = AddTextBox(grp, 180, y, 100);
        txtCurrency.Text = "₹";

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
            Location = new Point(270, 505),
            Size = new Size(180, 35),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
        };
        btnSave.Click += async (s, e) => await SaveAsync();

        btnCancel = new Button
        {
            Text = "Cancel (Esc)",
            Location = new Point(460, 505),
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

    private async void LoadCompanyDataAsync(int companyId)
    {
        var company = await _companyService.GetCompanyByIdAsync(companyId);
        if (company != null)
        {
            txtCompanyName.Text = company.CompanyName;
            txtAddress.Text = company.Address;
            txtState.Text = company.State;
            txtCountry.Text = company.Country;
            txtPAN.Text = company.PAN;
            txtEmail.Text = company.Email;
            txtPhone.Text = company.Phone;
            dtpFYFrom.Value = company.FinancialYearFrom;
            dtpBooksFrom.Value = company.BooksBeginningFrom;
            txtCurrency.Text = company.Currency;
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

        btnSave.Enabled = false;

        try
        {
            if (_companyIdToEdit.HasValue)
            {
                var updateDto = new CompanyUpdateDto
                {
                    CompanyId = _companyIdToEdit.Value,
                    CompanyName = name,
                    Address = txtAddress.Text,
                    State = txtState.Text,
                    Country = txtCountry.Text,
                    PAN = txtPAN.Text,
                    Email = txtEmail.Text,
                    Phone = txtPhone.Text,
                    Currency = txtCurrency.Text,
                    IsActive = true
                };

                await _companyService.UpdateCompanyAsync(updateDto);
                MessageBox.Show("Company details updated successfully.", "Company Updated", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                var createDto = new CompanyCreateDto
                {
                    CompanyName = name,
                    Address = txtAddress.Text,
                    State = txtState.Text,
                    Country = txtCountry.Text,
                    PAN = txtPAN.Text,
                    Email = txtEmail.Text,
                    Phone = txtPhone.Text,
                    FinancialYearFrom = dtpFYFrom.Value,
                    BooksBeginningFrom = dtpBooksFrom.Value,
                    Currency = txtCurrency.Text,
                    CreateDefaultLedgers = chkDefaultLedgers.Checked
                };

                await _companyService.CreateCompanyAsync(createDto);
                MessageBox.Show($"Company '{name}' created successfully with Chart of Accounts.", "Company Created", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            IsSaved = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save company:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            btnSave.Enabled = true;
        }
    }
}
