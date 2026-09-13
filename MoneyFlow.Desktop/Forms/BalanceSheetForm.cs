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

public class BalanceSheetForm : Form
{
    private readonly IAccountingService _accountingService;
    private readonly ILedgerService _ledgerService;
    private readonly ICompanyContext _companyContext;

    private BalanceSheetDto? _currentStatement;

    // UI Controls
    private DateTimePicker _dtpAsOfDate = null!;
    private TextBox _txtSearch = null!;
    private Button _btnRefresh = null!;
    private Guna2DataGridView _dgvLiabilities = null!;
    private Guna2DataGridView _dgvAssets = null!;
    private Label _lblStatusBadge = null!;
    private Label _lblLiabilitiesTotal = null!;
    private Label _lblAssetsTotal = null!;
    private Button _btnPrint = null!;
    private Button _btnExportCsv = null!;
    private Button _btnClose = null!;

    public BalanceSheetForm(
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
        Text = "Balance Sheet Statement (Financial Position) — No GST";
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
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Two-column T-format grids
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));  // Balanced status footer
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Action buttons

        // 1. Filter Bar
        var pnlFilters = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = ExecLedgerTheme.SecondarySurface,
            Padding = new Padding(10, 8, 10, 8)
        };
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // "As of Date (F2):"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // DTP
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // "Search (F3):"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220)); // Search box
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Spacer
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // Refresh button

        pnlFilters.Controls.Add(new Label { Text = "As of Date (F2):", AutoSize = true, Anchor = AnchorStyles.Left, Font = ExecLedgerTheme.UIBold9 }, 0, 0);
        _dtpAsOfDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 130 };
        pnlFilters.Controls.Add(_dtpAsOfDate, 1, 0);

        pnlFilters.Controls.Add(new Label { Text = "Search (F3):", AutoSize = true, Anchor = AnchorStyles.Left, Font = ExecLedgerTheme.UIBold9 }, 2, 0);
        _txtSearch = new TextBox { Width = 210, PlaceholderText = "Filter by Account or Group..." };
        _txtSearch.TextChanged += (s, e) => ApplySearchFilter();
        pnlFilters.Controls.Add(_txtSearch, 3, 0);

        _btnRefresh = new Button { Text = "Refresh (F5)", Width = 100, Height = 30, BackColor = ExecLedgerTheme.PrimaryNavy, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        _btnRefresh.Click += async (s, e) => await LoadBalanceSheetAsync();
        pnlFilters.Controls.Add(_btnRefresh, 5, 0);

        mainLayout.Controls.Add(pnlFilters, 0, 0);

        // 2. Grids Split Panel
        var pnlGrids = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        pnlGrids.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        pnlGrids.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        // Left Side: Capital & Liabilities
        var pnlLeft = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 5, 0) };
        var lblLeftHeader = new Label
        {
            Text = "CAPITAL & LIABILITIES (CREDIT)",
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = ExecLedgerTheme.ApplicationCanvas,
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5, 0, 0, 0)
        };
        _dgvLiabilities = CreateSideGrid();
        pnlLeft.Controls.Add(_dgvLiabilities);
        pnlLeft.Controls.Add(lblLeftHeader);
        pnlGrids.Controls.Add(pnlLeft, 0, 0);

        // Right Side: Property & Assets
        var pnlRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5, 0, 0, 0) };
        var lblRightHeader = new Label
        {
            Text = "PROPERTY & ASSETS (DEBIT)",
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = ExecLedgerTheme.ApplicationCanvas,
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(5, 0, 0, 0)
        };
        _dgvAssets = CreateSideGrid();
        pnlRight.Controls.Add(_dgvAssets);
        pnlRight.Controls.Add(lblRightHeader);
        pnlGrids.Controls.Add(pnlRight, 1, 0);

        mainLayout.Controls.Add(pnlGrids, 0, 1);

        // 3. Balance Footer Summary Panel
        var pnlFooter = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.FromArgb(250, 252, 255),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            Padding = new Padding(5)
        };
        pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));

        _lblLiabilitiesTotal = new Label { Text = "Total Liabilities: ₹0.00", AutoSize = true, Anchor = AnchorStyles.Left, Font = ExecLedgerTheme.UIBold10, ForeColor = ExecLedgerTheme.PrimaryNavy };
        _lblStatusBadge = new Label { Text = "[ ✔ ] BALANCED (Diff: ₹0.00)", AutoSize = true, Anchor = AnchorStyles.None, Font = ExecLedgerTheme.UIBold11, ForeColor = Color.DarkGreen };
        _lblAssetsTotal = new Label { Text = "Total Assets: ₹0.00", AutoSize = true, Anchor = AnchorStyles.Right, Font = ExecLedgerTheme.UIBold10, ForeColor = ExecLedgerTheme.PrimaryNavy };

        pnlFooter.Controls.Add(_lblLiabilitiesTotal, 0, 0);
        pnlFooter.Controls.Add(_lblStatusBadge, 1, 0);
        pnlFooter.Controls.Add(_lblAssetsTotal, 2, 0);
        mainLayout.Controls.Add(pnlFooter, 0, 2);

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
        _btnPrint.Click += (s, e) => MessageBox.Show(this, "Balance Sheet Statement ready for printing / preview.", "Print Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);

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
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            RowHeadersVisible = false,
            EnableHeadersVisualStyles = false,
            ColumnHeadersHeight = 32
        };

        dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(240, 244, 250);
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = ExecLedgerTheme.PrimaryNavy;
        dgv.ColumnHeadersDefaultCellStyle.Font = ExecLedgerTheme.UIBold9;

        dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Particulars",
            HeaderText = "Particulars",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Amount",
            HeaderText = "Amount (₹)",
            Width = 160,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" },
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        dgv.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "LedgerId",
            HeaderText = "LedgerId",
            Visible = false
        });

        dgv.CellDoubleClick += (s, e) => DrillDownLedger(dgv);
        dgv.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                DrillDownLedger(dgv);
                e.Handled = true;
            }
        };

        return dgv;
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

        await LoadBalanceSheetAsync();
    }

    private async Task LoadBalanceSheetAsync()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
            return;

        try
        {
            Cursor = Cursors.WaitCursor;
            _btnRefresh.Enabled = false;

            _currentStatement = await _accountingService.GetBalanceSheetAsync(
                _companyContext.CurrentCompany.CompanyId,
                _dtpAsOfDate.Value);

            PopulateGrids();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error calculating Balance Sheet: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnRefresh.Enabled = true;
            Cursor = Cursors.Default;
        }
    }

    private void PopulateGrids()
    {
        if (_currentStatement == null) return;

        var searchText = _txtSearch.Text.Trim();

        _dgvLiabilities.Rows.Clear();
        _dgvAssets.Rows.Clear();

        // 1. Fill Liabilities
        foreach (var grp in _currentStatement.Liabilities)
        {
            var matchingLines = string.IsNullOrEmpty(searchText)
                ? grp.Lines
                : grp.Lines.Where(l => l.LedgerName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                                       grp.GroupName.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matchingLines.Count == 0 && !string.IsNullOrEmpty(searchText) && !grp.GroupName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                continue;

            // Group Header Row
            int headerIdx = _dgvLiabilities.Rows.Add($"  ▶ {grp.GroupName.ToUpperInvariant()}", grp.TotalAmount, null);
            FormatHeaderRow(_dgvLiabilities.Rows[headerIdx]);

            // Child Ledger Rows
            foreach (var line in matchingLines)
            {
                int rowIdx = _dgvLiabilities.Rows.Add($"      {line.LedgerName}", line.Amount, line.LedgerId);
                _dgvLiabilities.Rows[rowIdx].DefaultCellStyle.ForeColor = Color.FromArgb(40, 40, 40);
            }
        }

        // Add Profit & Loss Account row to Liabilities if Net Profit
        if (_currentStatement.HasNetProfit && _currentStatement.NetProfit > 0)
        {
            int pnlHeader = _dgvLiabilities.Rows.Add("  ▶ PROFIT & LOSS ACCOUNT (SURPLUS)", _currentStatement.NetProfit, null);
            FormatHeaderRow(_dgvLiabilities.Rows[pnlHeader]);
            int pnlLine = _dgvLiabilities.Rows.Add("      Current Period Net Profit", _currentStatement.NetProfit, null);
            _dgvLiabilities.Rows[pnlLine].DefaultCellStyle.ForeColor = Color.DarkGreen;
        }

        // 2. Fill Assets
        foreach (var grp in _currentStatement.Assets)
        {
            var matchingLines = string.IsNullOrEmpty(searchText)
                ? grp.Lines
                : grp.Lines.Where(l => l.LedgerName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                                       grp.GroupName.Contains(searchText, StringComparison.OrdinalIgnoreCase)).ToList();

            if (matchingLines.Count == 0 && !string.IsNullOrEmpty(searchText) && !grp.GroupName.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                continue;

            // Group Header Row
            int headerIdx = _dgvAssets.Rows.Add($"  ▶ {grp.GroupName.ToUpperInvariant()}", grp.TotalAmount, null);
            FormatHeaderRow(_dgvAssets.Rows[headerIdx]);

            // Child Ledger Rows
            foreach (var line in matchingLines)
            {
                int rowIdx = _dgvAssets.Rows.Add($"      {line.LedgerName}", line.Amount, line.LedgerId);
                _dgvAssets.Rows[rowIdx].DefaultCellStyle.ForeColor = Color.FromArgb(40, 40, 40);
            }
        }

        // Add Profit & Loss Account row to Assets if Net Loss
        if (!_currentStatement.HasNetProfit && _currentStatement.NetLoss > 0)
        {
            int pnlHeader = _dgvAssets.Rows.Add("  ▶ PROFIT & LOSS ACCOUNT (DEFICIT)", _currentStatement.NetLoss, null);
            FormatHeaderRow(_dgvAssets.Rows[pnlHeader]);
            int pnlLine = _dgvAssets.Rows.Add("      Current Period Net Loss", _currentStatement.NetLoss, null);
            _dgvAssets.Rows[pnlLine].DefaultCellStyle.ForeColor = Color.Crimson;
        }

        // 3. Update Balanced Footer Status
        decimal totalLiabilities = _currentStatement.TotalLiabilitiesSide;
        decimal totalAssets = _currentStatement.TotalAssetsSide;
        decimal diff = _currentStatement.Difference;

        _lblLiabilitiesTotal.Text = $"Total Liabilities: ₹{totalLiabilities:N2}";
        _lblAssetsTotal.Text = $"Total Assets: ₹{totalAssets:N2}";

        if (_currentStatement.IsBalanced)
        {
            _lblStatusBadge.Text = "[ ✔ ] BALANCED (Diff: ₹0.00)";
            _lblStatusBadge.ForeColor = Color.DarkGreen;
        }
        else
        {
            _lblStatusBadge.Text = $"[ ⚠ ] UNBALANCED (Diff: ₹{diff:N2})";
            _lblStatusBadge.ForeColor = Color.Crimson;
        }
    }

    private void FormatHeaderRow(DataGridViewRow row)
    {
        row.DefaultCellStyle.BackColor = Color.FromArgb(246, 249, 252);
        row.DefaultCellStyle.Font = ExecLedgerTheme.UIBold9;
        row.DefaultCellStyle.ForeColor = ExecLedgerTheme.PrimaryNavy;
    }

    private void ApplySearchFilter()
    {
        PopulateGrids();
    }

    private void DrillDownLedger(DataGridView dgv)
    {
        if (dgv.CurrentRow == null) return;

        var val = dgv.CurrentRow.Cells["LedgerId"].Value;
        if (val == null || !int.TryParse(val.ToString(), out int ledgerId) || ledgerId <= 0)
            return;

        using var statementForm = new LedgerStatementForm(_accountingService, _ledgerService, _companyContext);
        statementForm.ShowDialog(this);
    }

    private void ExportToCsv()
    {
        if (_currentStatement == null)
        {
            MessageBox.Show(this, "No data available to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"BalanceSheet_{_companyContext.CurrentCompany?.CompanyName}_{_dtpAsOfDate.Value:yyyyMMdd}.csv"
        };

        if (sfd.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"\"Balance Sheet Statement — {_companyContext.CurrentCompany?.CompanyName}\"");
            sb.AppendLine($"\"As of Date:\",\"{_dtpAsOfDate.Value:dd-MMM-yyyy}\"");
            sb.AppendLine();
            sb.AppendLine("\"LIABILITIES & CAPITAL\",\"AMOUNT (INR)\",\"\",\"ASSETS & PROPERTIES\",\"AMOUNT (INR)\"");

            int maxRows = Math.Max(_dgvLiabilities.Rows.Count, _dgvAssets.Rows.Count);

            for (int i = 0; i < maxRows; i++)
            {
                string liabPart = i < _dgvLiabilities.Rows.Count ? _dgvLiabilities.Rows[i].Cells["Particulars"].Value?.ToString()?.Trim() ?? "" : "";
                string liabAmt = i < _dgvLiabilities.Rows.Count ? _dgvLiabilities.Rows[i].Cells["Amount"].Value?.ToString() ?? "" : "";

                string assetPart = i < _dgvAssets.Rows.Count ? _dgvAssets.Rows[i].Cells["Particulars"].Value?.ToString()?.Trim() ?? "" : "";
                string assetAmt = i < _dgvAssets.Rows.Count ? _dgvAssets.Rows[i].Cells["Amount"].Value?.ToString() ?? "" : "";

                sb.AppendLine($"\"{EscapeCsv(liabPart)}\",\"{liabAmt}\",,\"\"{EscapeCsv(assetPart)}\",\"{assetAmt}\"");
            }

            sb.AppendLine();
            sb.AppendLine($"\"TOTAL LIABILITIES\",\"{_currentStatement.TotalLiabilitiesSide:N2}\",,\"TOTAL ASSETS\",\"{_currentStatement.TotalAssetsSide:N2}\"");
            sb.AppendLine($"\"BALANCE STATUS\",\"{(_currentStatement.IsBalanced ? "BALANCED" : "UNBALANCED")}\",,\"DIFFERENCE\",\"{_currentStatement.Difference:N2}\"");

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show(this, "Balance Sheet exported to CSV successfully.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                _ = LoadBalanceSheetAsync();
                break;
            case Keys.P when e.Control:
                _btnPrint.PerformClick();
                break;
        }
    }
}
