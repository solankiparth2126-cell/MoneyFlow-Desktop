using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;
using Guna.UI2.WinForms;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Forms;

public class FinancialYearListForm : Form
{
    private readonly IFinancialYearService _fyService;
    private readonly ICompanyContext _companyContext;

    private Guna2DataGridView dgvYears = null!;
    private Button btnSelect = null!;
    private Button btnCreate = null!;
    private Button btnCloseFY = null!;
    private Button btnCancel = null!;

    public bool YearChanged { get; private set; }

    public FinancialYearListForm(IFinancialYearService fyService, ICompanyContext companyContext)
    {
        _fyService = fyService;
        _companyContext = companyContext;
        InitializeComponent();
        LoadYearsAsync();
    }

    private void InitializeComponent()
    {
        this.Text = "MoneyFlow Desktop — Financial Years";
        this.Size = new Size(680, 420);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.FromArgb(245, 247, 250);
        this.Font = ExecLedgerTheme.UIRegular9;

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = Color.FromArgb(24, 43, 73)
        };
        var lblTitle = new Label
        {
            Text = $"Financial Years — {_companyContext.CurrentCompany?.CompanyName}",
            Font = ExecLedgerTheme.UIBold11,
            ForeColor = Color.White,
            Location = new Point(18, 15),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblTitle);
        this.Controls.Add(headerPanel);

        dgvYears = new Guna2DataGridView
        {
            Location = new Point(20, 70),
            Size = new Size(625, 240),
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

        dgvYears.Columns.Add("Id", "ID");
        dgvYears.Columns["Id"]!.Visible = false;
        dgvYears.Columns.Add("YearName", "Financial Year");
        dgvYears.Columns.Add("StartDate", "Start Date");
        dgvYears.Columns.Add("EndDate", "End Date");
        dgvYears.Columns.Add("Status", "Status");

        dgvYears.Columns["YearName"]!.FillWeight = 25;
        dgvYears.Columns["StartDate"]!.FillWeight = 25;
        dgvYears.Columns["EndDate"]!.FillWeight = 25;
        dgvYears.Columns["Status"]!.FillWeight = 25;

        dgvYears.DoubleClick += async (s, e) => await SelectYearAsync();
        dgvYears.KeyDown += async (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                await SelectYearAsync();
            }
        };

        this.Controls.Add(dgvYears);

        var btnPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.FromArgb(235, 238, 242)
        };

        btnSelect = new Button
        {
            Text = "&Select FY (Enter)",
            Location = new Point(20, 14),
            Size = new Size(150, 32),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            Font = ExecLedgerTheme.UIBold9
        };
        btnSelect.Click += async (s, e) => await SelectYearAsync();

        btnCreate = new Button
        {
            Text = "&New FY (Alt+C)",
            Location = new Point(180, 14),
            Size = new Size(140, 32),
            BackColor = Color.FromArgb(37, 99, 235),
            ForeColor = Color.White
        };
        btnCreate.Click += (s, e) => CreateFinancialYear();

        btnCloseFY = new Button
        {
            Text = "&Lock/Close FY",
            Location = new Point(330, 14),
            Size = new Size(140, 32),
            BackColor = Color.FromArgb(239, 68, 68),
            ForeColor = Color.White
        };
        btnCloseFY.Click += async (s, e) => await CloseSelectedFYAsync();

        btnCancel = new Button
        {
            Text = "Close (Esc)",
            Location = new Point(505, 14),
            Size = new Size(140, 32),
            BackColor = Color.FromArgb(226, 232, 240)
        };
        btnCancel.Click += (s, e) => this.Close();

        btnPanel.Controls.Add(btnSelect);
        btnPanel.Controls.Add(btnCreate);
        btnPanel.Controls.Add(btnCloseFY);
        btnPanel.Controls.Add(btnCancel);
        this.Controls.Add(btnPanel);

        this.CancelButton = btnCancel;
    }

    private async void LoadYearsAsync()
    {
        if (_companyContext.CurrentCompany == null) return;

        dgvYears.Rows.Clear();
        var years = await _fyService.GetFinancialYearsByCompanyAsync(_companyContext.CurrentCompany.CompanyId);

        foreach (var y in years)
        {
            string status = y.IsClosed ? "Closed / Locked" : (y.IsActive ? "ACTIVE" : "Open");
            int rowIndex = dgvYears.Rows.Add(
                y.FinancialYearId,
                y.YearName,
                y.StartDate.ToString("dd-MMM-yyyy"),
                y.EndDate.ToString("dd-MMM-yyyy"),
                status);

            if (y.IsActive)
            {
                dgvYears.Rows[rowIndex].DefaultCellStyle.BackColor = Color.FromArgb(240, 253, 244);
                dgvYears.Rows[rowIndex].DefaultCellStyle.Font = ExecLedgerTheme.UIBold9;
            }
        }
    }

    private int? GetSelectedFYId()
    {
        if (dgvYears.SelectedRows.Count == 0) return null;
        return Convert.ToInt32(dgvYears.SelectedRows[0].Cells["Id"].Value);
    }

    private async Task SelectYearAsync()
    {
        int? id = GetSelectedFYId();
        if (!id.HasValue) return;

        try
        {
            bool success = await _fyService.SetActiveFinancialYearAsync(id.Value);
            if (success)
            {
                YearChanged = true;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Cannot Set Active FY", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CreateFinancialYear()
    {
        if (_companyContext.CurrentCompany == null) return;

        // Suggest the next financial year starting day after the latest existing FY end date
        var currentFY = _companyContext.CurrentFinancialYear;
        DateTime? suggested = currentFY != null ? currentFY.EndDate.AddDays(1) : (DateTime?)null;

        using var createForm = new FinancialYearCreateForm(_fyService, _companyContext.CurrentCompany.CompanyId, suggested);
        if (createForm.ShowDialog(this) == DialogResult.OK)
        {
            LoadYearsAsync();
        }
    }

    private async Task CloseSelectedFYAsync()
    {
        int? id = GetSelectedFYId();
        if (!id.HasValue) return;

        var confirm = MessageBox.Show(
            "Closing a Financial Year locks all accounting postings in that period.\n\nAre you sure you want to close this Financial Year?",
            "Confirm Lock Financial Year",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm == DialogResult.Yes)
        {
            await _fyService.CloseFinancialYearAsync(id.Value);
            LoadYearsAsync();
        }
    }
}
