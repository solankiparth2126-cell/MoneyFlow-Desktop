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

public class DayBookForm : Form
{
    private readonly IAccountingService _accountingService;
    private readonly ICompanyContext _companyContext;

    private List<DayBookItemDto> _allItems = new();
    private List<DayBookItemDto> _filteredItems = new();

    // UI Controls
    private DateTimePicker _dtpFromDate = null!;
    private DateTimePicker _dtpToDate = null!;
    private ComboBox _cmbVoucherType = null!;
    private TextBox _txtSearch = null!;
    private Button _btnRefresh = null!;
    private DataGridView _dgvDayBook = null!;
    private Label _lblCount = null!;
    private Label _lblTotalDebit = null!;
    private Label _lblTotalCredit = null!;
    private Label _lblBalanceStatus = null!;
    private Button _btnPrint = null!;
    private Button _btnExportCsv = null!;
    private Button _btnClose = null!;

    public DayBookForm(
        IAccountingService accountingService,
        ICompanyContext companyContext)
    {
        _accountingService = accountingService;
        _companyContext = companyContext;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Day Book — Chronological Transaction Audit";
        Size = new Size(1180, 750);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5F);
        KeyPreview = true;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(15)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));  // Filter & Search bar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));  // Totals Summary
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Buttons

        // 1. Top Filters Panel
        var pnlFilters = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 9,
            RowCount = 1,
            BackColor = Color.FromArgb(245, 248, 252),
            Padding = new Padding(10, 8, 10, 8)
        };
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // "From Date:"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // DTP From
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));  // "To Date:"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); // DTP To
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60));  // "Type:"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160)); // Combo Type
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));  // "Search:"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); // Search Box
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // Refresh Button

        pnlFilters.Controls.Add(new Label { Text = "From (F2):", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 0);
        _dtpFromDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 120 };
        pnlFilters.Controls.Add(_dtpFromDate, 1, 0);

        pnlFilters.Controls.Add(new Label { Text = "To:", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 2, 0);
        _dtpToDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 120 };
        pnlFilters.Controls.Add(_dtpToDate, 3, 0);

        pnlFilters.Controls.Add(new Label { Text = "Type:", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 4, 0);
        _cmbVoucherType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        PopulateVoucherTypeCombo();
        _cmbVoucherType.SelectedIndexChanged += async (s, e) => await LoadDayBookDataAsync();
        pnlFilters.Controls.Add(_cmbVoucherType, 5, 0);

        pnlFilters.Controls.Add(new Label { Text = "Filter:", AutoSize = true, Anchor = AnchorStyles.Left }, 6, 0);
        _txtSearch = new TextBox { Width = 180, Dock = DockStyle.Fill };
        _txtSearch.TextChanged += (s, e) => ApplySearchFilter();
        pnlFilters.Controls.Add(_txtSearch, 7, 0);

        _btnRefresh = new Button { Text = "Refresh (F5)", Width = 95, Height = 30, BackColor = Color.FromArgb(230, 240, 252) };
        _btnRefresh.Click += async (s, e) => await LoadDayBookDataAsync();
        pnlFilters.Controls.Add(_btnRefresh, 8, 0);

        mainLayout.Controls.Add(pnlFilters, 0, 0);

        // 2. DataGridView
        _dgvDayBook = new DataGridView
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
        _dgvDayBook.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 240, 248);
        _dgvDayBook.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        _dgvDayBook.EnableHeadersVisualStyles = false;
        _dgvDayBook.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 251, 255);

        ConfigureGridColumns();
        _dgvDayBook.CellDoubleClick += (s, e) => DrillDownVoucher();

        mainLayout.Controls.Add(_dgvDayBook, 0, 1);

        // 3. Totals Summary Panel
        var pnlTotals = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.FromArgb(250, 252, 255),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
            Padding = new Padding(5)
        };
        pnlTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        pnlTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        pnlTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        pnlTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        _lblCount = new Label { Text = "Transactions: 0", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        _lblBalanceStatus = new Label { Text = "Balanced: ₹0.00", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.DarkGreen, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        _lblTotalDebit = new Label { Text = "Total Debit: ₹0.00", AutoSize = true, Anchor = AnchorStyles.Right, ForeColor = Color.FromArgb(0, 51, 102), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        _lblTotalCredit = new Label { Text = "Total Credit: ₹0.00", AutoSize = true, Anchor = AnchorStyles.Right, ForeColor = Color.FromArgb(0, 51, 102), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };

        pnlTotals.Controls.Add(_lblCount, 0, 0);
        pnlTotals.Controls.Add(_lblBalanceStatus, 1, 0);
        pnlTotals.Controls.Add(_lblTotalDebit, 2, 0);
        pnlTotals.Controls.Add(_lblTotalCredit, 3, 0);

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
        _btnPrint.Click += (s, e) => MessageBox.Show(this, "Day Book Report ready for printing / preview.", "Print Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);

        pnlActions.Controls.Add(_btnClose);
        pnlActions.Controls.Add(_btnExportCsv);
        pnlActions.Controls.Add(_btnPrint);

        mainLayout.Controls.Add(pnlActions, 0, 3);

        Controls.Add(mainLayout);

        KeyDown += OnFormKeyDown;
        Load += async (s, e) => await OnFormLoadAsync();
    }

    private void PopulateVoucherTypeCombo()
    {
        _cmbVoucherType.Items.Clear();
        _cmbVoucherType.Items.Add("All Vouchers");
        _cmbVoucherType.Items.Add("Payment (F5)");
        _cmbVoucherType.Items.Add("Receipt (F6)");
        _cmbVoucherType.Items.Add("Contra (F4)");
        _cmbVoucherType.Items.Add("Journal (F7)");
        _cmbVoucherType.Items.Add("Sales (F8)");
        _cmbVoucherType.Items.Add("Purchase (F9)");
        _cmbVoucherType.Items.Add("Debit Note (Ctrl+F9)");
        _cmbVoucherType.Items.Add("Credit Note (Ctrl+F8)");
        _cmbVoucherType.SelectedIndex = 0;
    }

    private void ConfigureGridColumns()
    {
        _dgvDayBook.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColDate",
            HeaderText = "Date",
            Width = 110,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });

        _dgvDayBook.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColType",
            HeaderText = "Type",
            Width = 120
        });

        _dgvDayBook.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColVoucherNo",
            HeaderText = "Voucher No",
            Width = 130
        });

        _dgvDayBook.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColRefNo",
            HeaderText = "Ref No",
            Width = 110
        });

        _dgvDayBook.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColParticulars",
            HeaderText = "Particulars",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        _dgvDayBook.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColDebit",
            HeaderText = "Debit (₹)",
            Width = 135,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", ForeColor = Color.FromArgb(0, 51, 102) }
        });

        _dgvDayBook.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColCredit",
            HeaderText = "Credit (₹)",
            Width = 135,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", ForeColor = Color.FromArgb(0, 51, 102) }
        });

        _dgvDayBook.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColNarration",
            HeaderText = "Narration",
            Width = 220
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
                Text = $"Day Book — {company.CompanyName} (FY: {fy.YearName})";
                _dtpFromDate.MinDate = fy.StartDate;
                _dtpFromDate.MaxDate = fy.EndDate;
                _dtpToDate.MinDate = fy.StartDate;
                _dtpToDate.MaxDate = fy.EndDate;

                var initialDate = DateTime.Today >= fy.StartDate && DateTime.Today <= fy.EndDate
                    ? DateTime.Today
                    : fy.StartDate;

                _dtpFromDate.Value = initialDate;
                _dtpToDate.Value = initialDate;
            }

            _dtpFromDate.ValueChanged += async (s, e) => await LoadDayBookDataAsync();
            _dtpToDate.ValueChanged += async (s, e) => await LoadDayBookDataAsync();

            await LoadDayBookDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error loading Day Book: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private VoucherTypeEnum? GetSelectedVoucherTypeFilter()
    {
        return _cmbVoucherType.SelectedIndex switch
        {
            1 => VoucherTypeEnum.Payment,
            2 => VoucherTypeEnum.Receipt,
            3 => VoucherTypeEnum.Contra,
            4 => VoucherTypeEnum.Journal,
            5 => VoucherTypeEnum.Sales,
            6 => VoucherTypeEnum.Purchase,
            7 => VoucherTypeEnum.DebitNote,
            8 => VoucherTypeEnum.CreditNote,
            _ => null
        };
    }

    private async Task LoadDayBookDataAsync()
    {
        var company = _companyContext.CurrentCompany;
        if (company == null) return;

        try
        {
            UseWaitCursor = true;
            var filterType = GetSelectedVoucherTypeFilter();
            var from = _dtpFromDate.Value.Date;
            var to = _dtpToDate.Value.Date;

            if (from > to)
            {
                _dtpToDate.Value = from;
                to = from;
            }

            var report = await _accountingService.GetDayBookAsync(company.CompanyId, from, to, filterType);
            _allItems = report.Items.ToList();
            ApplySearchFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to load transactions: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void ApplySearchFilter()
    {
        var term = _txtSearch.Text.Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(term))
        {
            _filteredItems = _allItems.ToList();
        }
        else
        {
            _filteredItems = _allItems.Where(item =>
                item.VoucherNumber.ToLowerInvariant().Contains(term) ||
                item.VoucherTypeName.ToLowerInvariant().Contains(term) ||
                item.Particulars.ToLowerInvariant().Contains(term) ||
                item.ReferenceNumber.ToLowerInvariant().Contains(term) ||
                item.Narration.ToLowerInvariant().Contains(term) ||
                item.DebitAmount.ToString().Contains(term) ||
                item.CreditAmount.ToString().Contains(term)
            ).ToList();
        }

        RenderGridRows();
    }

    private void RenderGridRows()
    {
        _dgvDayBook.Rows.Clear();

        decimal sumDebit = 0;
        decimal sumCredit = 0;

        foreach (var item in _filteredItems)
        {
            var idx = _dgvDayBook.Rows.Add();
            var row = _dgvDayBook.Rows[idx];
            row.Tag = item;

            row.Cells["ColDate"].Value = item.Date.ToString("dd-MMM-yyyy");
            row.Cells["ColType"].Value = item.VoucherTypeName;
            row.Cells["ColVoucherNo"].Value = item.VoucherNumber;
            row.Cells["ColRefNo"].Value = item.ReferenceNumber;
            row.Cells["ColParticulars"].Value = item.Particulars;
            row.Cells["ColDebit"].Value = item.DebitAmount > 0 ? item.DebitAmount : (object)string.Empty;
            row.Cells["ColCredit"].Value = item.CreditAmount > 0 ? item.CreditAmount : (object)string.Empty;
            row.Cells["ColNarration"].Value = item.Narration;

            sumDebit += item.DebitAmount;
            sumCredit += item.CreditAmount;
        }

        _lblCount.Text = $"Transactions: {_filteredItems.Count}";
        _lblTotalDebit.Text = $"Total Debit: ₹{sumDebit:N2}";
        _lblTotalCredit.Text = $"Total Credit: ₹{sumCredit:N2}";

        var diff = Math.Abs(sumDebit - sumCredit);
        if (diff == 0)
        {
            _lblBalanceStatus.Text = "[ ✔ ] BALANCED (Diff: ₹0.00)";
            _lblBalanceStatus.ForeColor = Color.DarkGreen;
        }
        else
        {
            _lblBalanceStatus.Text = $"[ ✕ ] OUT OF BALANCE (Diff: ₹{diff:N2})";
            _lblBalanceStatus.ForeColor = Color.Red;
        }
    }

    private void DrillDownVoucher()
    {
        if (_dgvDayBook.CurrentRow?.Tag is not DayBookItemDto item) return;

        var sb = new StringBuilder();
        sb.AppendLine($"Voucher No: {item.VoucherNumber}");
        sb.AppendLine($"Type: {item.VoucherTypeName}");
        sb.AppendLine($"Date: {item.Date:dd-MMM-yyyy}");
        sb.AppendLine($"Ref No: {item.ReferenceNumber}");
        sb.AppendLine($"Amount: ₹{item.DebitAmount:N2}");
        sb.AppendLine($"Particulars: {item.Particulars}");
        sb.AppendLine($"Narration: {item.Narration}");

        MessageBox.Show(this, sb.ToString(), $"Voucher Audit Detail — {item.VoucherNumber}", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportToCsv()
    {
        if (_filteredItems.Count == 0)
        {
            MessageBox.Show(this, "No transaction records to export.", "Empty", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv",
            FileName = $"DayBook_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Date,Type,VoucherNo,ReferenceNo,Particulars,Debit,Credit,Narration");

            foreach (var item in _filteredItems)
            {
                sb.AppendLine($"\"{item.Date:dd-MMM-yyyy}\",\"{item.VoucherTypeName}\",\"{item.VoucherNumber}\",\"{item.ReferenceNumber}\",\"{EscapeCsv(item.Particulars)}\",{item.DebitAmount:F2},{item.CreditAmount:F2},\"{EscapeCsv(item.Narration)}\"");
            }

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show(this, $"Exported {_filteredItems.Count} transactions successfully to:\n{sfd.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            _cmbVoucherType.Focus();
            _cmbVoucherType.DroppedDown = true;
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter && _dgvDayBook.Focused)
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
