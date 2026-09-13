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

public class ProfitLossForm : Form
{
    private readonly IAccountingService _accountingService;
    private readonly ILedgerService _ledgerService;
    private readonly ICompanyContext _companyContext;

    private ProfitLossStatementDto? _currentStatement;

    // UI Controls
    private DateTimePicker _dtpFromDate = null!;
    private DateTimePicker _dtpToDate = null!;
    private Button _btnRefresh = null!;
    private Guna2DataGridView _dgvExpenses = null!;
    private Guna2DataGridView _dgvIncomes = null!;
    private Label _lblGrossResult = null!;
    private Label _lblNetResult = null!;
    private Button _btnPrint = null!;
    private Button _btnExportCsv = null!;
    private Button _btnClose = null!;

    public ProfitLossForm(
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
        Text = "Profit & Loss Statement (Trading & Income Account) — No GST";
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
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));  // Filter bar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Two-column T-format grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));  // Results Summary
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Buttons

        // 1. Filter Bar
        var pnlFilters = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = ExecLedgerTheme.SecondarySurface,
            Padding = new Padding(10, 8, 10, 8)
        };
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // "From (F2):"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // DTP From
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));  // "To:"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // DTP To
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Space
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // Refresh Button

        pnlFilters.Controls.Add(new Label { Text = "From (F2):", AutoSize = true, Anchor = AnchorStyles.Left, Font = ExecLedgerTheme.UIBold9 }, 0, 0);
        _dtpFromDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 120 };
        pnlFilters.Controls.Add(_dtpFromDate, 1, 0);

        pnlFilters.Controls.Add(new Label { Text = "To:", AutoSize = true, Anchor = AnchorStyles.Left, Font = ExecLedgerTheme.UIBold9 }, 2, 0);
        _dtpToDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 120 };
        pnlFilters.Controls.Add(_dtpToDate, 3, 0);

        _btnRefresh = new Button { Text = "Refresh (F5)", Width = 95, Height = 30, BackColor = Color.FromArgb(230, 240, 252) };
        _btnRefresh.Click += async (s, e) => await LoadProfitLossDataAsync();
        pnlFilters.Controls.Add(_btnRefresh, 5, 0);

        mainLayout.Controls.Add(pnlFilters, 0, 0);

        // 2. Two-Column T-Format Panel (Left = Expenses / Losses, Right = Incomes / Gains)
        var pnlGrids = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        pnlGrids.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        pnlGrids.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        // Left Panel (Expenses)
        var pnlLeft = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 5, 0) };
        var lblLeftHeader = new Label
        {
            Text = "EXPENSES & LOSSES (DEBIT)",
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = ExecLedgerTheme.ApplicationCanvas,
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5, 0, 0, 0)
        };
        _dgvExpenses = CreateSideGrid();
        pnlLeft.Controls.Add(_dgvExpenses);
        pnlLeft.Controls.Add(lblLeftHeader);
        pnlGrids.Controls.Add(pnlLeft, 0, 0);

        // Right Panel (Incomes)
        var pnlRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5, 0, 0, 0) };
        var lblRightHeader = new Label
        {
            Text = "INCOMES & GAINS (CREDIT)",
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = ExecLedgerTheme.ApplicationCanvas,
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5, 0, 0, 0)
        };
        _dgvIncomes = CreateSideGrid();
        pnlRight.Controls.Add(_dgvIncomes);
        pnlRight.Controls.Add(lblRightHeader);
        pnlGrids.Controls.Add(pnlRight, 1, 0);

        mainLayout.Controls.Add(pnlGrids, 0, 1);

        // 3. Results Summary Panel
        var pnlResults = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.FromArgb(250, 252, 255),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            Padding = new Padding(5)
        };
        pnlResults.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        pnlResults.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _lblGrossResult = new Label { Text = "Gross Profit: ₹0.00", AutoSize = true, Anchor = AnchorStyles.Left, Font = ExecLedgerTheme.UIBold10, ForeColor = Color.DarkGreen };
        _lblNetResult = new Label { Text = "Net Profit: ₹0.00", AutoSize = true, Anchor = AnchorStyles.Right, Font = ExecLedgerTheme.UIBold11, ForeColor = ExecLedgerTheme.PrimaryNavy };

        pnlResults.Controls.Add(_lblGrossResult, 0, 0);
        pnlResults.Controls.Add(_lblNetResult, 1, 0);
        mainLayout.Controls.Add(pnlResults, 0, 2);

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
        _btnPrint.Click += (s, e) => MessageBox.Show(this, "Profit & Loss Statement ready for printing / preview.", "Print Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);

        pnlActions.Controls.Add(_btnClose);
        pnlActions.Controls.Add(_btnExportCsv);
        pnlActions.Controls.Add(_btnPrint);

        mainLayout.Controls.Add(pnlActions, 0, 3);

        Controls.Add(mainLayout);

        KeyDown += OnFormKeyDown;
        Load += async (s, e) => await OnFormLoadAsync();
    }

    private Guna2DataGridView CreateSideGrid()
    {
        var dgv = new Guna2DataGridView
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
        dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 244, 250);
        dgv.ColumnHeadersDefaultCellStyle.Font = ExecLedgerTheme.UIBold9;
        dgv.EnableHeadersVisualStyles = false;

        dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColParticulars",
            HeaderText = "Particulars",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColAmount",
            HeaderText = "Amount (₹)",
            Width = 160,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        });

        dgv.CellDoubleClick += (s, e) => DrillDownGrid(dgv);
        return dgv;
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
                Text = $"Profit & Loss Statement — {company.CompanyName} (FY: {fy.YearName})";
                _dtpFromDate.MinDate = fy.StartDate;
                _dtpFromDate.MaxDate = fy.EndDate;
                _dtpToDate.MinDate = fy.StartDate;
                _dtpToDate.MaxDate = fy.EndDate;

                _dtpFromDate.Value = fy.StartDate;
                _dtpToDate.Value = DateTime.Today >= fy.StartDate && DateTime.Today <= fy.EndDate
                    ? DateTime.Today
                    : fy.EndDate;
            }

            _dtpFromDate.ValueChanged += async (s, e) => await LoadProfitLossDataAsync();
            _dtpToDate.ValueChanged += async (s, e) => await LoadProfitLossDataAsync();

            await LoadProfitLossDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error loading Profit & Loss Statement: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadProfitLossDataAsync()
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

            _currentStatement = await _accountingService.GetProfitAndLossAsync(company.CompanyId, from, to);
            RenderStatement();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to load Profit & Loss Statement: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void RenderStatement()
    {
        _dgvExpenses.Rows.Clear();
        _dgvIncomes.Rows.Clear();
        if (_currentStatement == null) return;

        // 1. Populate Left Side (Expenses)
        // Part 1: Trading Expenses
        AddSectionHeader(_dgvExpenses, "TRADING EXPENSES (DIRECT COSTS)");
        foreach (var cat in _currentStatement.TradingExpenses)
        {
            AddCategoryRow(_dgvExpenses, cat.CategoryName, cat.TotalAmount);
            foreach (var line in cat.Lines)
            {
                AddLedgerRow(_dgvExpenses, line.LedgerName, line.Amount, line.LedgerId);
            }
        }

        if (_currentStatement.HasGrossProfit)
        {
            AddResultRow(_dgvExpenses, "Gross Profit c/d (Carried Down)", _currentStatement.GrossProfit, true);
        }

        AddSubtotalRow(_dgvExpenses, "Total Trading Account", _currentStatement.GrandTradingTotal);

        // Part 2: Operating & Indirect Expenses
        AddSectionHeader(_dgvExpenses, "INDIRECT EXPENSES / OVERHEADS");
        if (!_currentStatement.HasGrossProfit)
        {
            AddResultRow(_dgvExpenses, "Gross Loss b/d (Brought Down)", _currentStatement.GrossLoss, false);
        }

        foreach (var cat in _currentStatement.IndirectExpenses)
        {
            AddCategoryRow(_dgvExpenses, cat.CategoryName, cat.TotalAmount);
            foreach (var line in cat.Lines)
            {
                AddLedgerRow(_dgvExpenses, line.LedgerName, line.Amount, line.LedgerId);
            }
        }

        if (_currentStatement.HasNetProfit)
        {
            AddResultRow(_dgvExpenses, "Net Profit (Transferred to Capital)", _currentStatement.NetProfit, true);
        }

        AddGrandTotalRow(_dgvExpenses, "Total (Debit Side)", _currentStatement.GrandPLTotal);

        // 2. Populate Right Side (Incomes)
        // Part 1: Trading Revenues
        AddSectionHeader(_dgvIncomes, "TRADING REVENUES (DIRECT SALES)");
        foreach (var cat in _currentStatement.TradingRevenues)
        {
            AddCategoryRow(_dgvIncomes, cat.CategoryName, cat.TotalAmount);
            foreach (var line in cat.Lines)
            {
                AddLedgerRow(_dgvIncomes, line.LedgerName, line.Amount, line.LedgerId);
            }
        }

        if (!_currentStatement.HasGrossProfit)
        {
            AddResultRow(_dgvIncomes, "Gross Loss c/d (Carried Down)", _currentStatement.GrossLoss, false);
        }

        AddSubtotalRow(_dgvIncomes, "Total Trading Account", _currentStatement.GrandTradingTotal);

        // Part 2: Indirect Incomes
        AddSectionHeader(_dgvIncomes, "INDIRECT INCOMES / GAINS");
        if (_currentStatement.HasGrossProfit)
        {
            AddResultRow(_dgvIncomes, "Gross Profit b/d (Brought Down)", _currentStatement.GrossProfit, true);
        }

        foreach (var cat in _currentStatement.IndirectIncomes)
        {
            AddCategoryRow(_dgvIncomes, cat.CategoryName, cat.TotalAmount);
            foreach (var line in cat.Lines)
            {
                AddLedgerRow(_dgvIncomes, line.LedgerName, line.Amount, line.LedgerId);
            }
        }

        if (!_currentStatement.HasNetProfit)
        {
            AddResultRow(_dgvIncomes, "Net Loss", _currentStatement.NetLoss, false);
        }

        AddGrandTotalRow(_dgvIncomes, "Total (Credit Side)", _currentStatement.GrandPLTotal);

        // 3. Update Result Badges
        if (_currentStatement.HasGrossProfit)
        {
            _lblGrossResult.Text = $"Gross Profit: ₹{_currentStatement.GrossProfit:N2}";
            _lblGrossResult.ForeColor = Color.DarkGreen;
        }
        else
        {
            _lblGrossResult.Text = $"Gross Loss: ₹{_currentStatement.GrossLoss:N2}";
            _lblGrossResult.ForeColor = Color.Red;
        }

        if (_currentStatement.HasNetProfit)
        {
            _lblNetResult.Text = $"Net Profit: ₹{_currentStatement.NetProfit:N2}";
            _lblNetResult.ForeColor = Color.DarkGreen;
        }
        else
        {
            _lblNetResult.Text = $"Net Loss: ₹{_currentStatement.NetLoss:N2}";
            _lblNetResult.ForeColor = Color.Red;
        }
    }

    private static void AddSectionHeader(DataGridView dgv, string title)
    {
        var idx = dgv.Rows.Add();
        var row = dgv.Rows[idx];
        row.DefaultCellStyle.BackColor = Color.FromArgb(242, 246, 252);
        row.DefaultCellStyle.Font = ExecLedgerTheme.UIBold9;
        row.Cells["ColParticulars"].Value = title;
    }

    private static void AddCategoryRow(DataGridView dgv, string categoryName, decimal amount)
    {
        var idx = dgv.Rows.Add();
        var row = dgv.Rows[idx];
        row.DefaultCellStyle.Font = ExecLedgerTheme.UIBold9;
        row.Cells["ColParticulars"].Value = $"  • {categoryName}";
        row.Cells["ColAmount"].Value = amount;
    }

    private static void AddLedgerRow(DataGridView dgv, string ledgerName, decimal amount, int ledgerId)
    {
        var idx = dgv.Rows.Add();
        var row = dgv.Rows[idx];
        row.Tag = ledgerId;
        row.Cells["ColParticulars"].Value = $"      {ledgerName}";
        row.Cells["ColAmount"].Value = amount;
    }

    private static void AddResultRow(DataGridView dgv, string title, decimal amount, bool isProfit)
    {
        var idx = dgv.Rows.Add();
        var row = dgv.Rows[idx];
        row.DefaultCellStyle.Font = ExecLedgerTheme.UIBold9;
        row.DefaultCellStyle.ForeColor = isProfit ? Color.DarkGreen : Color.DarkRed;
        row.Cells["ColParticulars"].Value = $"  ★ {title}";
        row.Cells["ColAmount"].Value = amount;
    }

    private static void AddSubtotalRow(DataGridView dgv, string title, decimal amount)
    {
        var idx = dgv.Rows.Add();
        var row = dgv.Rows[idx];
        row.DefaultCellStyle.BackColor = Color.FromArgb(235, 242, 250);
        row.DefaultCellStyle.Font = ExecLedgerTheme.UIBold9;
        row.Cells["ColParticulars"].Value = $"--- {title} ---";
        row.Cells["ColAmount"].Value = amount;
    }

    private static void AddGrandTotalRow(DataGridView dgv, string title, decimal amount)
    {
        var idx = dgv.Rows.Add();
        var row = dgv.Rows[idx];
        row.DefaultCellStyle.BackColor = Color.FromArgb(220, 235, 252);
        row.DefaultCellStyle.Font = ExecLedgerTheme.UIBold10;
        row.Cells["ColParticulars"].Value = $"TOTAL: {title}";
        row.Cells["ColAmount"].Value = amount;
    }

    private void DrillDownGrid(DataGridView dgv)
    {
        if (dgv.CurrentRow?.Tag is not int ledgerId) return;

        using var statementForm = new LedgerStatementForm(_accountingService, _ledgerService, _companyContext);
        statementForm.ShowDialog(this);
    }

    private void ExportToCsv()
    {
        if (_currentStatement == null)
        {
            MessageBox.Show(this, "No statement data to export.", "Empty", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv",
            FileName = $"ProfitLoss_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Profit & Loss Statement: From {_currentStatement.FromDate:dd-MMM-yyyy} To {_currentStatement.ToDate:dd-MMM-yyyy}");
            sb.AppendLine();
            sb.AppendLine("EXPENSES,Amount (₹),INCOMES,Amount (₹)");

            int maxRows = Math.Max(_dgvExpenses.Rows.Count, _dgvIncomes.Rows.Count);
            for (int i = 0; i < maxRows; i++)
            {
                string expDesc = i < _dgvExpenses.Rows.Count ? _dgvExpenses.Rows[i].Cells["ColParticulars"].Value?.ToString() ?? "" : "";
                string expAmt = i < _dgvExpenses.Rows.Count ? _dgvExpenses.Rows[i].Cells["ColAmount"].Value?.ToString() ?? "" : "";
                string incDesc = i < _dgvIncomes.Rows.Count ? _dgvIncomes.Rows[i].Cells["ColParticulars"].Value?.ToString() ?? "" : "";
                string incAmt = i < _dgvIncomes.Rows.Count ? _dgvIncomes.Rows[i].Cells["ColAmount"].Value?.ToString() ?? "" : "";

                sb.AppendLine($"\"{EscapeCsv(expDesc)}\",{expAmt},\"{EscapeCsv(incDesc)}\",{incAmt}");
            }

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show(this, $"Exported Profit & Loss Statement successfully to:\n{sfd.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        else if (e.Control && e.KeyCode == Keys.P)
        {
            _btnPrint.PerformClick();
            e.Handled = true;
        }
    }
}
