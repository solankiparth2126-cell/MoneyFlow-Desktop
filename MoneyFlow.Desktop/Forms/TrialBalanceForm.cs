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

public class TrialBalanceForm : Form
{
    private readonly IAccountingService _accountingService;
    private readonly ILedgerService _ledgerService;
    private readonly ICompanyContext _companyContext;

    private TrialBalanceDto? _currentReport;
    private bool _isDetailedView = true; // true = Ledger-wise (Detailed), false = Group-wise (Condensed)

    // UI Controls
    private DateTimePicker _dtpFromDate = null!;
    private DateTimePicker _dtpToDate = null!;
    private TextBox _txtSearch = null!;
    private Button _btnToggleView = null!;
    private Button _btnRefresh = null!;
    private Guna2DataGridView _dgvTrialBalance = null!;
    private Label _lblOpeningTotals = null!;
    private Label _lblPeriodTotals = null!;
    private Label _lblClosingTotals = null!;
    private Label _lblBalanceStatus = null!;
    private Button _btnPrint = null!;
    private Button _btnExportCsv = null!;
    private Button _btnClose = null!;

    public TrialBalanceForm(
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
        Text = "Trial Balance — Double-Entry Verification Statement";
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
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));  // Filter bar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));  // Summary Totals
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Actions

        // 1. Filter Bar
        var pnlFilters = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 9,
            RowCount = 1,
            BackColor = ExecLedgerTheme.SecondarySurface,
            Padding = new Padding(10, 8, 10, 8)
        };
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // "From (F2):"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // DTP From
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));  // "To:"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // DTP To
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); // Toggle View (F1)
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));  // "Search:"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Search Box
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // Refresh Button

        pnlFilters.Controls.Add(new Label { Text = "From (F2):", AutoSize = true, Anchor = AnchorStyles.Left, Font = ExecLedgerTheme.UIBold9 }, 0, 0);
        _dtpFromDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 120 };
        pnlFilters.Controls.Add(_dtpFromDate, 1, 0);

        pnlFilters.Controls.Add(new Label { Text = "To:", AutoSize = true, Anchor = AnchorStyles.Left, Font = ExecLedgerTheme.UIBold9 }, 2, 0);
        _dtpToDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 120 };
        pnlFilters.Controls.Add(_dtpToDate, 3, 0);

        _btnToggleView = new Button { Text = "View: Detailed (F1)", Width = 155, Height = 32, BackColor = Color.FromArgb(235, 243, 255), Font = ExecLedgerTheme.UIBold9 };
        _btnToggleView.Click += (s, e) => ToggleViewMode();
        pnlFilters.Controls.Add(_btnToggleView, 4, 0);

        pnlFilters.Controls.Add(new Label { Text = "Filter:", AutoSize = true, Anchor = AnchorStyles.Left }, 5, 0);
        _txtSearch = new TextBox { Width = 180, Dock = DockStyle.Fill };
        _txtSearch.TextChanged += (s, e) => RenderReportRows();
        pnlFilters.Controls.Add(_txtSearch, 6, 0);

        _btnRefresh = new Button { Text = "Refresh (F5)", Width = 95, Height = 30, BackColor = Color.FromArgb(230, 240, 252) };
        _btnRefresh.Click += async (s, e) => await LoadTrialBalanceDataAsync();
        pnlFilters.Controls.Add(_btnRefresh, 7, 0);

        mainLayout.Controls.Add(pnlFilters, 0, 0);

        // 2. DataGridView
        _dgvTrialBalance = new Guna2DataGridView
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
        _dgvTrialBalance.ColumnHeadersDefaultCellStyle.BackColor = ExecLedgerTheme.ApplicationCanvas;
        _dgvTrialBalance.ColumnHeadersDefaultCellStyle.Font = ExecLedgerTheme.UIBold9;
        _dgvTrialBalance.EnableHeadersVisualStyles = false;
        _dgvTrialBalance.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 252, 255);

        ConfigureGridColumns();
        _dgvTrialBalance.CellDoubleClick += (s, e) => DrillDownLedger();

        mainLayout.Controls.Add(_dgvTrialBalance, 0, 1);

        // 3. Totals & Balance Status Panel
        var pnlTotals = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.FromArgb(250, 252, 255),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            Padding = new Padding(5)
        };
        pnlTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); // Balance Status
        pnlTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); // Opening Totals
        pnlTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); // Period Totals
        pnlTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); // Closing Totals

        _lblBalanceStatus = new Label { Text = "[ ✔ ] BALANCED (Diff: ₹0.00)", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.DarkGreen, Font = ExecLedgerTheme.UIBold10 };
        _lblOpeningTotals = new Label { Text = "Opening: Dr ₹0.00 | Cr ₹0.00", AutoSize = true, Anchor = AnchorStyles.Right, Font = ExecLedgerTheme.UIBold9 };
        _lblPeriodTotals = new Label { Text = "Period: Dr ₹0.00 | Cr ₹0.00", AutoSize = true, Anchor = AnchorStyles.Right, Font = ExecLedgerTheme.UIBold9 };
        _lblClosingTotals = new Label { Text = "Closing: Dr ₹0.00 | Cr ₹0.00", AutoSize = true, Anchor = AnchorStyles.Right, ForeColor = ExecLedgerTheme.PrimaryNavy, Font = ExecLedgerTheme.UIBold9 };

        pnlTotals.Controls.Add(_lblBalanceStatus, 0, 0);
        pnlTotals.Controls.Add(_lblOpeningTotals, 1, 0);
        pnlTotals.Controls.Add(_lblPeriodTotals, 2, 0);
        pnlTotals.Controls.Add(_lblClosingTotals, 3, 0);

        mainLayout.Controls.Add(pnlTotals, 0, 2);

        // 4. Action Buttons Panel
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
        _btnPrint.Click += (s, e) => MessageBox.Show(this, "Trial Balance Report ready for printing / preview.", "Print Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);

        pnlActions.Controls.Add(_btnClose);
        pnlActions.Controls.Add(_btnExportCsv);
        pnlActions.Controls.Add(_btnPrint);

        mainLayout.Controls.Add(pnlActions, 0, 3);

        Controls.Add(mainLayout);

        KeyDown += OnFormKeyDown;
        Load += async (s, e) => await OnFormLoadAsync();
    }

    private void ConfigureGridColumns()
    {
        _dgvTrialBalance.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColParticulars",
            HeaderText = "Particulars (Ledger / Group)",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        _dgvTrialBalance.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColGroup",
            HeaderText = "Group / Nature",
            Width = 160
        });

        _dgvTrialBalance.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColOpDr",
            HeaderText = "Op. Debit (₹)",
            Width = 115,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        });

        _dgvTrialBalance.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColOpCr",
            HeaderText = "Op. Credit (₹)",
            Width = 115,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        });

        _dgvTrialBalance.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColTxnDr",
            HeaderText = "Txn Debit (₹)",
            Width = 115,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        });

        _dgvTrialBalance.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColTxnCr",
            HeaderText = "Txn Credit (₹)",
            Width = 115,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        });

        _dgvTrialBalance.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColClDr",
            HeaderText = "Closing Debit (₹)",
            Width = 130,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", Font = ExecLedgerTheme.UIBold9, ForeColor = ExecLedgerTheme.PrimaryNavy }
        });

        _dgvTrialBalance.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColClCr",
            HeaderText = "Closing Credit (₹)",
            Width = 130,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", Font = ExecLedgerTheme.UIBold9, ForeColor = ExecLedgerTheme.PrimaryNavy }
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
                Text = $"Trial Balance — {company.CompanyName} (FY: {fy.YearName})";
                _dtpFromDate.MinDate = fy.StartDate;
                _dtpFromDate.MaxDate = fy.EndDate;
                _dtpToDate.MinDate = fy.StartDate;
                _dtpToDate.MaxDate = fy.EndDate;

                _dtpFromDate.Value = fy.StartDate;
                _dtpToDate.Value = DateTime.Today >= fy.StartDate && DateTime.Today <= fy.EndDate
                    ? DateTime.Today
                    : fy.EndDate;
            }

            _dtpFromDate.ValueChanged += async (s, e) => await LoadTrialBalanceDataAsync();
            _dtpToDate.ValueChanged += async (s, e) => await LoadTrialBalanceDataAsync();

            await LoadTrialBalanceDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error initializing Trial Balance: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadTrialBalanceDataAsync()
    {
        var company = _companyContext.CurrentCompany;
        if (company == null) return;

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

            _currentReport = await _accountingService.GetTrialBalanceAsync(company.CompanyId, from, to);
            RenderReportRows();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to load trial balance: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ToggleViewMode()
    {
        _isDetailedView = !_isDetailedView;
        _btnToggleView.Text = _isDetailedView ? "View: Detailed (F1)" : "View: Condensed (F1)";
        _btnToggleView.BackColor = _isDetailedView ? Color.FromArgb(235, 243, 255) : Color.FromArgb(255, 243, 235);
        RenderReportRows();
    }

    private void RenderReportRows()
    {
        _dgvTrialBalance.Rows.Clear();
        if (_currentReport == null) return;

        var term = _txtSearch.Text.Trim().ToLowerInvariant();

        if (_isDetailedView)
        {
            // Detailed (Ledger-wise) Mode
            var items = _currentReport.Items;
            if (!string.IsNullOrWhiteSpace(term))
            {
                items = items.Where(i =>
                    i.LedgerName.ToLowerInvariant().Contains(term) ||
                    i.GroupName.ToLowerInvariant().Contains(term) ||
                    i.GroupNature.ToString().ToLowerInvariant().Contains(term)
                ).ToList();
            }

            foreach (var item in items)
            {
                var idx = _dgvTrialBalance.Rows.Add();
                var row = _dgvTrialBalance.Rows[idx];
                row.Tag = item.LedgerId;

                row.Cells["ColParticulars"].Value = $"  {item.LedgerName}";
                row.Cells["ColGroup"].Value = $"{item.GroupName} ({item.GroupNature})";
                row.Cells["ColOpDr"].Value = item.OpeningDebit > 0 ? item.OpeningDebit : (object)string.Empty;
                row.Cells["ColOpCr"].Value = item.OpeningCredit > 0 ? item.OpeningCredit : (object)string.Empty;
                row.Cells["ColTxnDr"].Value = item.PeriodDebit > 0 ? item.PeriodDebit : (object)string.Empty;
                row.Cells["ColTxnCr"].Value = item.PeriodCredit > 0 ? item.PeriodCredit : (object)string.Empty;
                row.Cells["ColClDr"].Value = item.ClosingDebit > 0 ? item.ClosingDebit : (object)string.Empty;
                row.Cells["ColClCr"].Value = item.ClosingCredit > 0 ? item.ClosingCredit : (object)string.Empty;
            }
        }
        else
        {
            // Condensed (Group-wise) Mode
            var groups = _currentReport.Items
                .GroupBy(i => new { i.GroupId, i.GroupName, i.GroupNature })
                .Select(g => new
                {
                    g.Key.GroupId,
                    g.Key.GroupName,
                    g.Key.GroupNature,
                    OpDr = g.Sum(x => x.OpeningDebit),
                    OpCr = g.Sum(x => x.OpeningCredit),
                    TxnDr = g.Sum(x => x.PeriodDebit),
                    TxnCr = g.Sum(x => x.PeriodCredit),
                    ClDr = g.Sum(x => x.ClosingDebit),
                    ClCr = g.Sum(x => x.ClosingCredit)
                })
                .OrderBy(g => g.GroupName);

            foreach (var g in groups)
            {
                if (!string.IsNullOrWhiteSpace(term) &&
                    !g.GroupName.ToLowerInvariant().Contains(term) &&
                    !g.GroupNature.ToString().ToLowerInvariant().Contains(term))
                {
                    continue;
                }

                var idx = _dgvTrialBalance.Rows.Add();
                var row = _dgvTrialBalance.Rows[idx];
                row.DefaultCellStyle.Font = ExecLedgerTheme.UIBold9;
                row.DefaultCellStyle.BackColor = ExecLedgerTheme.SecondarySurface;

                row.Cells["ColParticulars"].Value = $"[Group] {g.GroupName}";
                row.Cells["ColGroup"].Value = g.GroupNature.ToString();
                row.Cells["ColOpDr"].Value = g.OpDr > 0 ? g.OpDr : (object)string.Empty;
                row.Cells["ColOpCr"].Value = g.OpCr > 0 ? g.OpCr : (object)string.Empty;
                row.Cells["ColTxnDr"].Value = g.TxnDr > 0 ? g.TxnDr : (object)string.Empty;
                row.Cells["ColTxnCr"].Value = g.TxnCr > 0 ? g.TxnCr : (object)string.Empty;
                row.Cells["ColClDr"].Value = g.ClDr > 0 ? g.ClDr : (object)string.Empty;
                row.Cells["ColClCr"].Value = g.ClCr > 0 ? g.ClCr : (object)string.Empty;
            }
        }

        // Summary Totals
        _lblOpeningTotals.Text = $"Opening: Dr ₹{_currentReport.TotalOpeningDebit:N2} | Cr ₹{_currentReport.TotalOpeningCredit:N2}";
        _lblPeriodTotals.Text = $"Period: Dr ₹{_currentReport.TotalPeriodDebit:N2} | Cr ₹{_currentReport.TotalPeriodCredit:N2}";
        _lblClosingTotals.Text = $"Closing: Dr ₹{_currentReport.TotalClosingDebit:N2} | Cr ₹{_currentReport.TotalClosingCredit:N2}";

        if (_currentReport.IsBalanced)
        {
            _lblBalanceStatus.Text = "[ ✔ ] BALANCED (Diff: ₹0.00)";
            _lblBalanceStatus.ForeColor = Color.DarkGreen;
        }
        else
        {
            _lblBalanceStatus.Text = $"[ ✕ ] OUT OF BALANCE (Diff: ₹{_currentReport.Difference:N2})";
            _lblBalanceStatus.ForeColor = Color.Red;
        }
    }

    private void DrillDownLedger()
    {
        if (!_isDetailedView)
        {
            // Switch to detailed view if clicked on a group
            ToggleViewMode();
            return;
        }

        if (_dgvTrialBalance.CurrentRow?.Tag is not int ledgerId) return;

        using var statementForm = new LedgerStatementForm(_accountingService, _ledgerService, _companyContext);
        statementForm.ShowDialog(this);
    }

    private void ExportToCsv()
    {
        if (_currentReport == null || _currentReport.Items.Count == 0)
        {
            MessageBox.Show(this, "No trial balance records to export.", "Empty", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv",
            FileName = $"TrialBalance_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Trial Balance Statement: From {_currentReport.FromDate:dd-MMM-yyyy} To {_currentReport.ToDate:dd-MMM-yyyy}");
            sb.AppendLine($"Mode: {(_isDetailedView ? "Detailed (Ledger-wise)" : "Condensed (Group-wise)")}");
            sb.AppendLine();
            sb.AppendLine("Particulars,Group,OpeningDebit,OpeningCredit,PeriodDebit,PeriodCredit,ClosingDebit,ClosingCredit");

            foreach (var item in _currentReport.Items)
            {
                sb.AppendLine($"\"{EscapeCsv(item.LedgerName)}\",\"{EscapeCsv(item.GroupName)}\",{item.OpeningDebit:F2},{item.OpeningCredit:F2},{item.PeriodDebit:F2},{item.PeriodCredit:F2},{item.ClosingDebit:F2},{item.ClosingCredit:F2}");
            }

            sb.AppendLine();
            sb.AppendLine($"Total Opening:,,{_currentReport.TotalOpeningDebit:F2},{_currentReport.TotalOpeningCredit:F2}");
            sb.AppendLine($"Total Period:,,,,{_currentReport.TotalPeriodDebit:F2},{_currentReport.TotalPeriodCredit:F2}");
            sb.AppendLine($"Total Closing:,,,,,,{_currentReport.TotalClosingDebit:F2},{_currentReport.TotalClosingCredit:F2}");
            sb.AppendLine($"Balanced: {_currentReport.IsBalanced} (Difference: {_currentReport.Difference:F2})");

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show(this, $"Exported {_currentReport.Items.Count} ledger rows successfully to:\n{sfd.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        else if (e.KeyCode == Keys.F1)
        {
            ToggleViewMode();
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
        else if (e.KeyCode == Keys.Enter && _dgvTrialBalance.Focused)
        {
            DrillDownLedger();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.P)
        {
            _btnPrint.PerformClick();
            e.Handled = true;
        }
    }
}
