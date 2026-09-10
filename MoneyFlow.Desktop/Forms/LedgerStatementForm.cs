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

namespace MoneyFlow.Desktop.Forms;

public class LedgerStatementForm : Form
{
    private readonly IAccountingService _accountingService;
    private readonly ILedgerService _ledgerService;
    private readonly ICompanyContext _companyContext;

    private IReadOnlyList<LedgerSummaryDto> _allLedgers = new List<LedgerSummaryDto>();
    private LedgerStatementDto? _currentStatement;

    // UI Controls
    private ComboBox _cmbLedger = null!;
    private DateTimePicker _dtpFromDate = null!;
    private DateTimePicker _dtpToDate = null!;
    private Button _btnRefresh = null!;
    private Label _lblLedgerInfo = null!;
    private Label _lblOpeningBalance = null!;
    private DataGridView _dgvStatement = null!;
    private Label _lblTotalDebit = null!;
    private Label _lblTotalCredit = null!;
    private Label _lblClosingBalance = null!;
    private Button _btnPrint = null!;
    private Button _btnExportCsv = null!;
    private Button _btnClose = null!;

    public LedgerStatementForm(
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
        Text = "Ledger Statement / Account Extract";
        Size = new Size(1180, 750);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5F);
        KeyPreview = true;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(15)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Filter bar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Header info card
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));  // Footer Summary
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Action Buttons

        // 1. Filter Bar
        var pnlFilters = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 1,
            BackColor = Color.FromArgb(245, 248, 252),
            Padding = new Padding(10, 8, 10, 8)
        };
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105)); // "Select Ledger:"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));   // Combo Ledger
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // "From (F2):"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // DTP From
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));  // "To:"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // DTP To
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // Refresh Button

        pnlFilters.Controls.Add(new Label { Text = "Ledger (F4):", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 0);
        _cmbLedger = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        _cmbLedger.SelectedIndexChanged += async (s, e) => await LoadStatementDataAsync();
        pnlFilters.Controls.Add(_cmbLedger, 1, 0);

        pnlFilters.Controls.Add(new Label { Text = "From (F2):", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 2, 0);
        _dtpFromDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 120 };
        pnlFilters.Controls.Add(_dtpFromDate, 3, 0);

        pnlFilters.Controls.Add(new Label { Text = "To:", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 4, 0);
        _dtpToDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 120 };
        pnlFilters.Controls.Add(_dtpToDate, 5, 0);

        _btnRefresh = new Button { Text = "Refresh (F5)", Width = 100, Height = 30, BackColor = Color.FromArgb(230, 240, 252) };
        _btnRefresh.Click += async (s, e) => await LoadStatementDataAsync();
        pnlFilters.Controls.Add(_btnRefresh, 6, 0);

        mainLayout.Controls.Add(pnlFilters, 0, 0);

        // 2. Ledger Info & Opening Balance Card
        var pnlInfoCard = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.FromArgb(238, 243, 250),
            Padding = new Padding(10, 5, 10, 5)
        };
        pnlInfoCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        pnlInfoCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        _lblLedgerInfo = new Label { Text = "Ledger: [Select a ledger] | Under Group: -", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(0, 51, 102) };
        _lblOpeningBalance = new Label { Text = "Opening Balance: ₹0.00 Dr", AutoSize = true, Anchor = AnchorStyles.Right, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.DarkSlateBlue };

        pnlInfoCard.Controls.Add(_lblLedgerInfo, 0, 0);
        pnlInfoCard.Controls.Add(_lblOpeningBalance, 1, 0);
        mainLayout.Controls.Add(pnlInfoCard, 0, 1);

        // 3. DataGridView
        _dgvStatement = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false
        };
        _dgvStatement.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 240, 248);
        _dgvStatement.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        _dgvStatement.EnableHeadersVisualStyles = false;
        _dgvStatement.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 251, 255);

        ConfigureGridColumns();
        _dgvStatement.CellDoubleClick += (s, e) => DrillDownVoucher();

        mainLayout.Controls.Add(_dgvStatement, 0, 2);

        // 4. Footer Totals & Closing Balance Panel
        var pnlTotals = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.FromArgb(250, 252, 255),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            Padding = new Padding(5)
        };
        pnlTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        pnlTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        pnlTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        _lblTotalDebit = new Label { Text = "Total Debit: ₹0.00", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.FromArgb(0, 51, 102), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _lblTotalCredit = new Label { Text = "Total Credit: ₹0.00", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.FromArgb(0, 51, 102), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _lblClosingBalance = new Label { Text = "Closing Balance: ₹0.00 Dr", AutoSize = true, Anchor = AnchorStyles.Right, ForeColor = Color.DarkGreen, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) };

        pnlTotals.Controls.Add(_lblTotalDebit, 0, 0);
        pnlTotals.Controls.Add(_lblTotalCredit, 1, 0);
        pnlTotals.Controls.Add(_lblClosingBalance, 2, 0);

        mainLayout.Controls.Add(pnlTotals, 0, 3);

        // 5. Action Buttons Panel
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
        _btnPrint.Click += (s, e) => MessageBox.Show(this, "Ledger Statement ready for printing / preview.", "Print Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);

        pnlActions.Controls.Add(_btnClose);
        pnlActions.Controls.Add(_btnExportCsv);
        pnlActions.Controls.Add(_btnPrint);

        mainLayout.Controls.Add(pnlActions, 0, 4);

        Controls.Add(mainLayout);

        KeyDown += OnFormKeyDown;
        Load += async (s, e) => await OnFormLoadAsync();
    }

    private void ConfigureGridColumns()
    {
        _dgvStatement.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColDate",
            HeaderText = "Date",
            Width = 110,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });

        _dgvStatement.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColParticulars",
            HeaderText = "Particulars (Opposing Account)",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        _dgvStatement.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColVchType",
            HeaderText = "Vch Type",
            Width = 110
        });

        _dgvStatement.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColVchNo",
            HeaderText = "Voucher No",
            Width = 120
        });

        _dgvStatement.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColDebit",
            HeaderText = "Debit (₹)",
            Width = 130,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", ForeColor = Color.FromArgb(0, 51, 102) }
        });

        _dgvStatement.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColCredit",
            HeaderText = "Credit (₹)",
            Width = 130,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", ForeColor = Color.FromArgb(0, 51, 102) }
        });

        _dgvStatement.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColRunningBalance",
            HeaderText = "Balance (₹)",
            Width = 150,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }
        });

        _dgvStatement.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColNarration",
            HeaderText = "Narration",
            Width = 200
        });
    }

    private async Task OnFormLoadAsync()
    {
        try
        {
            var company = _companyContext.CurrentCompany;
            if (company == null)
            {
                MessageBox.Show(this, "Please select an active company first.", "No Company", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
                return;
            }

            var fy = _companyContext.CurrentFinancialYear;
            if (fy != null)
            {
                _dtpFromDate.MinDate = fy.StartDate;
                _dtpFromDate.MaxDate = fy.EndDate;
                _dtpToDate.MinDate = fy.StartDate;
                _dtpToDate.MaxDate = fy.EndDate;

                _dtpFromDate.Value = fy.StartDate;
                _dtpToDate.Value = DateTime.Today >= fy.StartDate && DateTime.Today <= fy.EndDate
                    ? DateTime.Today
                    : fy.EndDate;
            }

            _dtpFromDate.ValueChanged += async (s, e) => await LoadStatementDataAsync();
            _dtpToDate.ValueChanged += async (s, e) => await LoadStatementDataAsync();

            await LoadLedgersComboAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error initializing Ledger Statement: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadLedgersComboAsync()
    {
        var company = _companyContext.CurrentCompany;
        if (company == null) return;

        _allLedgers = await _ledgerService.GetLedgersByCompanyAsync(company.CompanyId);

        _cmbLedger.DataSource = null;
        _cmbLedger.DisplayMember = "LedgerName";
        _cmbLedger.ValueMember = "LedgerId";
        _cmbLedger.DataSource = _allLedgers.OrderBy(l => l.LedgerName).ToList();

        if (_allLedgers.Count > 0)
        {
            _cmbLedger.SelectedIndex = 0;
        }
    }

    private async Task LoadStatementDataAsync()
    {
        var company = _companyContext.CurrentCompany;
        if (company == null || _cmbLedger.SelectedItem is not LedgerSummaryDto selectedLedger) return;

        try
        {
            UseWaitCursor = true;
            var from = _dtpFromDate.Value.Date;
            var to = _dtpToDate.Value.Date;

            if (from > to)
            {
                _dtpToDate.Value = from;
                to = from;
            }

            _currentStatement = await _accountingService.GetLedgerStatementAsync(company.CompanyId, selectedLedger.LedgerId, from, to);
            RenderStatement();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to load ledger statement: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void RenderStatement()
    {
        _dgvStatement.Rows.Clear();
        if (_currentStatement == null) return;

        _lblLedgerInfo.Text = $"Ledger: {_currentStatement.LedgerName} | Under Group: {_currentStatement.GroupName}";

        var opTypeStr = _currentStatement.OpeningType == BalanceType.Debit ? "Dr" : "Cr";
        _lblOpeningBalance.Text = $"Opening Balance (as of {_currentStatement.FromDate:dd-MMM-yyyy}): ₹{_currentStatement.OpeningBalance:N2} {opTypeStr}";

        // Render Opening Balance as first row
        var opIdx = _dgvStatement.Rows.Add();
        var opRow = _dgvStatement.Rows[opIdx];
        opRow.DefaultCellStyle.BackColor = Color.FromArgb(245, 248, 252);
        opRow.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Italic);
        opRow.Cells["ColDate"].Value = _currentStatement.FromDate.ToString("dd-MMM-yyyy");
        opRow.Cells["ColParticulars"].Value = "** Opening Balance **";
        opRow.Cells["ColRunningBalance"].Value = $"₹{_currentStatement.OpeningBalance:N2} {opTypeStr}";

        // Render Transaction Rows
        foreach (var line in _currentStatement.Lines)
        {
            var idx = _dgvStatement.Rows.Add();
            var row = _dgvStatement.Rows[idx];
            row.Tag = line;

            row.Cells["ColDate"].Value = line.Date.ToString("dd-MMM-yyyy");
            row.Cells["ColParticulars"].Value = line.Particulars;
            row.Cells["ColVchType"].Value = line.VoucherTypeName;
            row.Cells["ColVchNo"].Value = line.VoucherNumber;
            row.Cells["ColDebit"].Value = line.Debit > 0 ? line.Debit : (object)string.Empty;
            row.Cells["ColCredit"].Value = line.Credit > 0 ? line.Credit : (object)string.Empty;

            var runningTypeStr = line.RunningType == BalanceType.Debit ? "Dr" : "Cr";
            row.Cells["ColRunningBalance"].Value = $"₹{line.RunningBalance:N2} {runningTypeStr}";
            row.Cells["ColNarration"].Value = line.Narration;
        }

        // Summary Totals
        _lblTotalDebit.Text = $"Total Debit: ₹{_currentStatement.TotalDebit:N2}";
        _lblTotalCredit.Text = $"Total Credit: ₹{_currentStatement.TotalCredit:N2}";

        var clTypeStr = _currentStatement.ClosingType == BalanceType.Debit ? "Dr" : "Cr";
        _lblClosingBalance.Text = $"Closing Balance (as of {_currentStatement.ToDate:dd-MMM-yyyy}): ₹{_currentStatement.ClosingBalance:N2} {clTypeStr}";
    }

    private void DrillDownVoucher()
    {
        if (_dgvStatement.CurrentRow?.Tag is not LedgerStatementLineDto line) return;

        var sb = new StringBuilder();
        sb.AppendLine($"Voucher No: {line.VoucherNumber}");
        sb.AppendLine($"Type: {line.VoucherTypeName}");
        sb.AppendLine($"Date: {line.Date:dd-MMM-yyyy}");
        sb.AppendLine($"Opposing Account: {line.Particulars}");
        if (line.Debit > 0) sb.AppendLine($"Debit Amount: ₹{line.Debit:N2}");
        if (line.Credit > 0) sb.AppendLine($"Credit Amount: ₹{line.Credit:N2}");
        sb.AppendLine($"Running Balance: ₹{line.RunningBalance:N2} {(line.RunningType == BalanceType.Debit ? "Dr" : "Cr")}");
        sb.AppendLine($"Narration: {line.Narration}");

        MessageBox.Show(this, sb.ToString(), $"Transaction Detail — {line.VoucherNumber}", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportToCsv()
    {
        if (_currentStatement == null || _currentStatement.Lines.Count == 0)
        {
            MessageBox.Show(this, "No ledger entries to export.", "Empty", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv",
            FileName = $"Ledger_{_currentStatement.LedgerName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Ledger Statement: {_currentStatement.LedgerName} (Under Group: {_currentStatement.GroupName})");
            sb.AppendLine($"Period: {_currentStatement.FromDate:dd-MMM-yyyy} to {_currentStatement.ToDate:dd-MMM-yyyy}");
            sb.AppendLine($"Opening Balance: {_currentStatement.OpeningBalance:F2} {_currentStatement.OpeningType}");
            sb.AppendLine();
            sb.AppendLine("Date,Particulars,VoucherType,VoucherNo,Debit,Credit,RunningBalance,Narration");

            foreach (var line in _currentStatement.Lines)
            {
                var runType = line.RunningType == BalanceType.Debit ? "Dr" : "Cr";
                sb.AppendLine($"\"{line.Date:dd-MMM-yyyy}\",\"{EscapeCsv(line.Particulars)}\",\"{line.VoucherTypeName}\",\"{line.VoucherNumber}\",{line.Debit:F2},{line.Credit:F2},\"{line.RunningBalance:F2} {runType}\",\"{EscapeCsv(line.Narration)}\"");
            }

            sb.AppendLine();
            sb.AppendLine($",Total Debits:,{_currentStatement.TotalDebit:F2},Total Credits:,{_currentStatement.TotalCredit:F2}");
            sb.AppendLine($",Closing Balance:,{_currentStatement.ClosingBalance:F2} {_currentStatement.ClosingType}");

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show(this, $"Exported {_currentStatement.Lines.Count} entries successfully to:\n{sfd.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private static string EscapeCsv(string text) => text.Replace("\"", "\"\"");

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F5)
        {
            _btnRefresh.PerformClick();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F2 && !e.Alt)
        {
            _dtpFromDate.Focus();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F4)
        {
            _cmbLedger.Focus();
            _cmbLedger.DroppedDown = true;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter && _dgvStatement.Focused)
        {
            DrillDownVoucher();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.P)
        {
            _btnPrint.PerformClick();
            e.Handled = true;
        }
    }
}
