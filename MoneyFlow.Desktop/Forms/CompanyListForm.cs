using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Forms;

public class CompanyListForm : Form
{
    private readonly ICompanyService _companyService;
    private readonly ICompanyContext _companyContext;

    private DataGridView dgvCompanies = null!;
    private Button btnSelect = null!;
    private Button btnCreate = null!;
    private Button btnAlter = null!;
    private Button btnDelete = null!;
    private Button btnClose = null!;

    public bool CompanySelected { get; private set; }

    public CompanyListForm(ICompanyService companyService, ICompanyContext companyContext)
    {
        _companyService = companyService;
        _companyContext = companyContext;
        InitializeComponent();
        LoadCompaniesAsync();
    }

    private void InitializeComponent()
    {
        this.Text = "MoneyFlow Desktop — Companies";
        this.Size = new Size(800, 500);
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
            Text = "Select or Manage Company",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 16),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblTitle);
        this.Controls.Add(headerPanel);

        // DataGridView
        dgvCompanies = new DataGridView
        {
            Location = new Point(20, 75),
            Size = new Size(745, 320),
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.Fixed3D,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        dgvCompanies.Columns.Add("CompanyId", "ID");
        dgvCompanies.Columns["CompanyId"]!.Visible = false;
        dgvCompanies.Columns.Add("CompanyName", "Company Name");
        dgvCompanies.Columns.Add("State", "State");
        dgvCompanies.Columns.Add("FinancialYear", "Financial Year");
        dgvCompanies.Columns.Add("Currency", "Currency");
        dgvCompanies.Columns.Add("Status", "Status");

        dgvCompanies.Columns["CompanyName"]!.FillWeight = 40;
        dgvCompanies.Columns["State"]!.FillWeight = 20;
        dgvCompanies.Columns["FinancialYear"]!.FillWeight = 20;
        dgvCompanies.Columns["Currency"]!.FillWeight = 10;
        dgvCompanies.Columns["Status"]!.FillWeight = 10;

        dgvCompanies.DoubleClick += async (s, e) => await SelectCompanyAsync();
        dgvCompanies.KeyDown += async (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                await SelectCompanyAsync();
            }
        };

        this.Controls.Add(dgvCompanies);

        // Bottom Action Buttons Panel
        var btnPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            BackColor = Color.FromArgb(235, 238, 242)
        };

        btnSelect = new Button
        {
            Text = "&Select Company (Enter)",
            Location = new Point(20, 15),
            Size = new Size(165, 34),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        btnSelect.Click += async (s, e) => await SelectCompanyAsync();

        btnCreate = new Button
        {
            Text = "&Create (Alt+C)",
            Location = new Point(195, 15),
            Size = new Size(130, 34),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White
        };
        btnCreate.Click += (s, e) => CreateCompany();

        btnAlter = new Button
        {
            Text = "&Alter (Alt+A)",
            Location = new Point(335, 15),
            Size = new Size(130, 34),
            BackColor = Color.FromArgb(245, 158, 11),
            ForeColor = Color.White
        };
        btnAlter.Click += (s, e) => AlterCompany();

        btnDelete = new Button
        {
            Text = "&Delete (Alt+D)",
            Location = new Point(475, 15),
            Size = new Size(130, 34),
            BackColor = Color.FromArgb(239, 68, 68),
            ForeColor = Color.White
        };
        btnDelete.Click += async (s, e) => await DeleteCompanyAsync();

        btnClose = new Button
        {
            Text = "Close (Esc)",
            Location = new Point(635, 15),
            Size = new Size(130, 34),
            BackColor = Color.FromArgb(226, 232, 240)
        };
        btnClose.Click += (s, e) => this.Close();

        btnPanel.Controls.Add(btnSelect);
        btnPanel.Controls.Add(btnCreate);
        btnPanel.Controls.Add(btnAlter);
        btnPanel.Controls.Add(btnDelete);
        btnPanel.Controls.Add(btnClose);
        this.Controls.Add(btnPanel);

        this.CancelButton = btnClose;
    }

    private async void LoadCompaniesAsync()
    {
        dgvCompanies.Rows.Clear();
        var companies = await _companyService.GetAllCompaniesAsync();

        foreach (var c in companies)
        {
            string fy = $"{c.FinancialYearFrom:dd-MMM-yyyy}";
            string status = c.IsActive ? "Active" : "Inactive";
            if (_companyContext.CurrentCompany?.CompanyId == c.CompanyId)
            {
                status = "OPEN";
            }

            dgvCompanies.Rows.Add(c.CompanyId, c.CompanyName, c.State, fy, c.Currency, status);
        }
    }

    private int? GetSelectedCompanyId()
    {
        if (dgvCompanies.SelectedRows.Count == 0) return null;
        var row = dgvCompanies.SelectedRows[0];
        return Convert.ToInt32(row.Cells["CompanyId"].Value);
    }

    private async Task SelectCompanyAsync()
    {
        int? id = GetSelectedCompanyId();
        if (!id.HasValue)
        {
            MessageBox.Show("Please select a company from the list.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        bool opened = await _companyService.OpenCompanyAsync(id.Value);
        if (opened)
        {
            CompanySelected = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        else
        {
            MessageBox.Show("Unable to open the selected company.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CreateCompany()
    {
        using var createForm = new CompanyCreateEditForm(_companyService);
        if (createForm.ShowDialog(this) == DialogResult.OK)
        {
            LoadCompaniesAsync();
            CompanySelected = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    private void AlterCompany()
    {
        int? id = GetSelectedCompanyId();
        if (!id.HasValue)
        {
            MessageBox.Show("Please select a company to alter.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var editForm = new CompanyCreateEditForm(_companyService, id.Value);
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            LoadCompaniesAsync();
        }
    }

    private async Task DeleteCompanyAsync()
    {
        int? id = GetSelectedCompanyId();
        if (!id.HasValue)
        {
            MessageBox.Show("Please select a company to delete.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            "Are you sure you want to mark this company as inactive?",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm == DialogResult.Yes)
        {
            await _companyService.DeleteCompanyAsync(id.Value);
            LoadCompaniesAsync();
        }
    }
}
