using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;

using Guna.UI2.WinForms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

public class OutstandingReportForm : Form
{
    private readonly IAccountingService _accountingService;
    private readonly ILedgerService _ledgerService;
    private readonly ICompanyContext _companyContext;

    private OutstandingReportDto? _currentReport;
    private bool _isReceivables = true;

    // UI Controls
    private RadioButton _rbReceivables = null!;
    private RadioButton _rbPayables = null!;
    private DateTimePicker _dtpAsOfDate = null!;
    private TextBox _txtSearch = null!;
    private Button _btnRefresh = null!;
    private Guna2DataGridView _dgvOutstanding = null!;
    private Label _lblSummaryLeft = null!;
    private Label _lblSummaryRight = null!;
    private Button _btnPrint = null!;
    private Button _btnExportCsv = null!;
    private Button _btnClose = null!;

    public OutstandingReportForm(
        IAccountingService accountingService,
        ILedgerService ledgerService,
        ICompanyContext companyContext)
    {
        _accountingService = accountingService;
        _ledgerService = ledgerService;
        _companyContext = companyContext;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Outstanding Analysis & Aging Register (Receivables / Payables) — No GST";
        Size = new Size(1250, 780);
        StartPosition = FormStartPosition.CenterParent;
        Font = ExecLedgerTheme.UIRegular9;
        KeyPreview = true;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(15)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // Filter & Toggle toolbar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Aging DataGridView
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));  // Audit Summary Footer
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Action Buttons

        // 1. Filter Toolbar
        var pnlFilters = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 1,
            BackColor = ExecLedgerTheme.SecondarySurface,
            Padding = new Padding(10, 8, 10, 8)
        };
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); // Radio Receivables
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); // Radio Payables
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // "As of Date (F2):"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // DTP
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // "Search (F3):"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200)); // Search box
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Spacer
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // Refresh button

        _rbReceivables = new RadioButton
        {
            Text = "Receivables (Debtors)",
            Checked = true,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.PrimaryNavy
        };
        _rbReceivables.CheckedChanged += async (s, e) =>
        {
            if (_rbReceivables.Checked)
            {
                _isReceivables = true;
                await LoadReportAsync();
            }
        };

        _rbPayables = new RadioButton
        {
            Text = "Payables (Creditors)",
            Checked = false,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.PrimaryNavy
        };
        _rbPayables.CheckedChanged += async (s, e) =>
        {
            if (_rbPayables.Checked)
            {
                _isReceivables = false;
                await LoadReportAsync();
            }
        };

        pnlFilters.Controls.Add(_rbReceivables, 0, 0);
        pnlFilters.Controls.Add(_rbPayables, 1, 0);

        pnlFilters.Controls.Add(new Label { Text = "As of Date (F2):", AutoSize = true, Anchor = AnchorStyles.Left, Font = ExecLedgerTheme.UIBold9 }, 2, 0);
        _dtpAsOfDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 120 };
        pnlFilters.Controls.Add(_dtpAsOfDate, 3, 0);

        pnlFilters.Controls.Add(new Label { Text = "Search (F3):", AutoSize = true, Anchor = AnchorStyles.Left, Font = ExecLedgerTheme.UIBold9 }, 4, 0);
        _txtSearch = new TextBox { Width = 190, PlaceholderText = "Filter by party name..." };
        _txtSearch.TextChanged += (s, e) => ApplySearchFilter();
        pnlFilters.Controls.Add(_txtSearch, 5, 0);

        _btnRefresh = new Button { Text = "Refresh (F5)", Width = 100, Height = 30, BackColor = ExecLedgerTheme.PrimaryNavy, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        _btnRefresh.Click += async (s, e) => await LoadReportAsync();
        pnlFilters.Controls.Add(_btnRefresh, 7, 0);

        mainLayout.Controls.Add(pnlFilters, 0, 0);

        // 2. DataGridView
        _dgvOutstanding = new Guna2DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            RowHeadersVisible = false,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 36
        };

        _dgvOutstanding.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 244, 250);
        _dgvOutstanding.ColumnHeadersDefaultCellStyle.ForeColor = ExecLedgerTheme.PrimaryNavy;
        _dgvOutstanding.ColumnHeadersDefaultCellStyle.Font = ExecLedgerTheme.UIBold9;

        _dgvOutstanding.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColParty",
            HeaderText = "Particulars (Party Name)",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 30,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        _dgvOutstanding.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColGroup",
            HeaderText = "Parent Group",
            Width = 160,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        _dgvOutstanding.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColTotal",
            HeaderText = "Total Outstanding (₹)",
            Width = 170,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", Font = ExecLedgerTheme.UIBold9 },
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        _dgvOutstanding.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Col0To30",
            HeaderText = "0-30 Days (₹)",
            Width = 140,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", ForeColor = Color.DarkGreen },
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        _dgvOutstanding.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Col31To60",
            HeaderText = "31-60 Days (₹)",
            Width = 140,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", ForeColor = Color.DarkGoldenrod },
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        _dgvOutstanding.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Col61To90",
            HeaderText = "61-90 Days (₹)",
            Width = 140,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", ForeColor = Color.DarkOrange },
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        _dgvOutstanding.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColOver90",
            HeaderText = ">90 Days (₹)",
            Width = 140,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", ForeColor = Color.Crimson, Font = ExecLedgerTheme.UIBold9 },
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        _dgvOutstanding.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColLedgerId",
            HeaderText = "LedgerId",
            Visible = false
        });

        _dgvOutstanding.CellDoubleClick += (s, e) => DrillDownLedger();
        _dgvOutstanding.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                DrillDownLedger();
                e.Handled = true;
            }
        };

        mainLayout.Controls.Add(_dgvOutstanding, 0, 1);

        // 3. Summary Footer
        var pnlSummary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.FromArgb(250, 252, 255),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            Padding = new Padding(8)
        };
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        _lblSummaryLeft = new Label
        {
            Text = "Total Parties: 0 | Total Outstanding: ₹0.00",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = ExecLedgerTheme.UIBold10,
            ForeColor = ExecLedgerTheme.PrimaryNavy
        };

        _lblSummaryRight = new Label
        {
            Text = "0-30: ₹0.00 | 31-60: ₹0.00 | 61-90: ₹0.00 | >90: ₹0.00",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = Color.FromArgb(50, 50, 50)
        };

        pnlSummary.Controls.Add(_lblSummaryLeft, 0, 0);
        pnlSummary.Controls.Add(_lblSummaryRight, 1, 0);

        mainLayout.Controls.Add(pnlSummary, 0, 2);

        // 4. Action Buttons
        var pnlActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(5)
        };

        _btnClose = new Button { Text = "Close (Esc)", Width = 110, Height = 35 };
        _btnClose.Click += (s, e) => Close();

        _btnExportCsv = new Button { Text = "Export CSV", Width = 120, Height = 35 };
        _btnExportCsv.Click += (s, e) => ExportToCsv();

        _btnPrint = new Button { Text = "Print (Ctrl+P)", Width = 110, Height = 35, BackColor = Color.FromArgb(230, 240, 252) };
        _btnPrint.Click += (s, e) => MessageBox.Show(this, "Outstanding Aging Report ready for printing / preview.", "Print Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);

        pnlActions.Controls.Add(_btnClose);
        pnlActions.Controls.Add(_btnExportCsv);
        pnlActions.Controls.Add(_btnPrint);

        mainLayout.Controls.Add(pnlActions, 0, 3);

        Controls.Add(mainLayout);

        KeyDown += OnFormKeyDown;
        Load += async (s, e) => await OnFormLoadAsync();
    }

    private async Task OnFormLoadAsync()
    {
        if (_companyContext.CurrentFinancialYear != null)
        {
            var fy = _companyContext.CurrentFinancialYear;
            _dtpAsOfDate.MinDate = fy.StartDate;
            _dtpAsOfDate.MaxDate = fy.EndDate;

            var today = DateTime.Today;
            if (today >= fy.StartDate && today <= fy.EndDate)
                _dtpAsOfDate.Value = today;
            else
                _dtpAsOfDate.Value = fy.EndDate;
        }
        else
        {
            _dtpAsOfDate.Value = DateTime.Today;
        }

        await LoadReportAsync();
    }

    private async Task LoadReportAsync()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
            return;

        try
        {
            Cursor = Cursors.WaitCursor;
            _btnRefresh.Enabled = false;

            _currentReport = await _accountingService.GetOutstandingReportAsync(
                _companyContext.CurrentCompany.CompanyId,
                _dtpAsOfDate.Value,
                _isReceivables);

            PopulateGrid();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error calculating Outstanding Aging: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnRefresh.Enabled = true;
            Cursor = Cursors.Default;
        }
    }

    private void PopulateGrid()
    {
        if (_currentReport == null) return;

        var searchText = _txtSearch.Text.Trim();
        _dgvOutstanding.Rows.Clear();

        var filteredParties = string.IsNullOrEmpty(searchText)
            ? _currentReport.Parties
            : _currentReport.Parties.Where(p => p.PartyName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                                                p.GroupName.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var p in filteredParties)
        {
            _dgvOutstanding.Rows.Add(
                p.PartyName,
                p.GroupName,
                p.TotalOutstanding,
                p.Aging.Days0To30,
                p.Aging.Days31To60,
                p.Aging.Days61To90,
                p.Aging.DaysOver90,
                p.LedgerId);
        }

        string mode = _isReceivables ? "Receivables (Debtors)" : "Payables (Creditors)";
        _lblSummaryLeft.Text = $"{mode}: {filteredParties.Count} Parties | Total: ₹{filteredParties.Sum(p => p.TotalOutstanding):N2}";
        _lblSummaryRight.Text = $"0-30d: ₹{filteredParties.Sum(p => p.Aging.Days0To30):N2}  |  31-60d: ₹{filteredParties.Sum(p => p.Aging.Days31To60):N2}  |  61-90d: ₹{filteredParties.Sum(p => p.Aging.Days61To90):N2}  |  >90d: ₹{filteredParties.Sum(p => p.Aging.DaysOver90):N2}";
    }

    private void ApplySearchFilter()
    {
        PopulateGrid();
    }

    private void DrillDownLedger()
    {
        if (_dgvOutstanding.CurrentRow == null) return;

        var val = _dgvOutstanding.CurrentRow.Cells["ColLedgerId"].Value;
        if (val == null || !int.TryParse(val.ToString(), out int ledgerId) || ledgerId <= 0)
            return;

        using var statementForm = new LedgerStatementForm(_accountingService, _ledgerService, _companyContext);
        statementForm.ShowDialog(this);
    }

    private void ExportToCsv()
    {
        if (_currentReport == null)
        {
            MessageBox.Show(this, "No data available to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string mode = _isReceivables ? "Receivables" : "Payables";
        using var sfd = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"Outstanding_{mode}_{_companyContext.CurrentCompany?.CompanyName}_{_dtpAsOfDate.Value:yyyyMMdd}.csv"
        };

        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"\"Outstanding {mode} Aging Analysis — {_companyContext.CurrentCompany?.CompanyName}\"");
            sb.AppendLine($"\"As of Date:\",\"{_dtpAsOfDate.Value:dd-MMM-yyyy}\"");
            sb.AppendLine();
            sb.AppendLine("\"PARTY NAME\",\"GROUP\",\"TOTAL OUTSTANDING (INR)\",\"0-30 DAYS (INR)\",\"31-60 DAYS (INR)\",\"61-90 DAYS (INR)\",\">90 DAYS (INR)\"");

            foreach (DataGridViewRow row in _dgvOutstanding.Rows)
            {
                string party = row.Cells["ColParty"].Value?.ToString() ?? "";
                string group = row.Cells["ColGroup"].Value?.ToString() ?? "";
                string total = row.Cells["ColTotal"].Value?.ToString() ?? "";
                string d0_30 = row.Cells["Col0To30"].Value?.ToString() ?? "";
                string d31_60 = row.Cells["Col31To60"].Value?.ToString() ?? "";
                string d61_90 = row.Cells["Col61To90"].Value?.ToString() ?? "";
                string dOver90 = row.Cells["ColOver90"].Value?.ToString() ?? "";

                sb.AppendLine($"\"{EscapeCsv(party)}\",\"{EscapeCsv(group)}\",\"{total}\",\"{d0_30}\",\"{d31_60}\",\"{d61_90}\",\"{dOver90}\"");
            }

            sb.AppendLine();
            sb.AppendLine($"\"TOTALS\",,\"{_currentReport.TotalOutstandingAmount:N2}\",\"{_currentReport.TotalDays0To30:N2}\",\"{_currentReport.TotalDays31To60:N2}\",\"{_currentReport.TotalDays61To90:N2}\",\"{_currentReport.TotalDaysOver90:N2}\"");

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show(this, "Outstanding Aging Report exported to CSV successfully.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error exporting CSV: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string EscapeCsv(string val)
    {
        return val.Replace("\"", "\"\"");
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Escape:
                Close();
                break;
            case Keys.F2:
                _dtpAsOfDate.Focus();
                break;
            case Keys.F3:
                _txtSearch.Focus();
                _txtSearch.SelectAll();
                break;
            case Keys.F5:
                _ = LoadReportAsync();
                break;
            case Keys.P when e.Control:
                _btnPrint.PerformClick();
                break;
        }
    }
}
