using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
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

public class CashBankBookForm : Form
{
    private readonly IAccountingService _accountingService;
    private readonly ICompanyContext _companyContext;

    private CashBankBookType _currentBookType = CashBankBookType.CashBook;
    private IReadOnlyList<LedgerSummaryDto> _availableLedgers = new List<LedgerSummaryDto>();
    private CashBankBookReportDto? _currentReport;

    // UI Controls - Filter / Mode Bar
    private RadioButton _rbCashBook = null!;
    private RadioButton _rbBankBook = null!;
    private ComboBox _cmbAccount = null!;
    private DateTimePicker _dtpFromDate = null!;
    private DateTimePicker _dtpToDate = null!;
    private TextBox _txtSearch = null!;
    private Button _btnRefresh = null!;

    // UI Controls - Account Context Card
    private Label _lblAccountInfo = null!;
    private Label _lblOpeningBalance = null!;

    // UI Controls - Data Grid
    private Guna2DataGridView _dgvEntries = null!;

    // UI Controls - Footer
    private Label _lblTxnCount = null!;
    private Label _lblTotalDebit = null!;
    private Label _lblTotalCredit = null!;
    private Label _lblClosingBalance = null!;
    private Label _lblValidationBadge = null!;

    // UI Controls - Buttons
    private Button _btnDrillDown = null!;
    private Button _btnExportCsv = null!;
    private Button _btnPrint = null!;
    private Button _btnClose = null!;

    public CashBankBookForm(
        IAccountingService accountingService,
        ICompanyContext companyContext)
    {
        _accountingService = accountingService;
        _companyContext = companyContext;

        InitializeComponent();
    }

    public void SetInitialBookType(CashBankBookType bookType)
    {
        _currentBookType = bookType;
        if (bookType == CashBankBookType.BankBook)
        {
            _rbBankBook.Checked = true;
        }
        else
        {
            _rbCashBook.Checked = true;
        }
    }

    private void InitializeComponent()
    {
        Text = "Cash & Bank Books Register";
        Size = new Size(1220, 780);
        StartPosition = FormStartPosition.CenterParent;
        Font = ExecLedgerTheme.UIRegular9;
        KeyPreview = true;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(15)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));  // Mode & Filter bar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Header info card
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));  // Footer Summary
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Action Buttons

        // 1. Mode & Filter Bar
        var pnlFilters = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 10,
            RowCount = 1,
            BackColor = ExecLedgerTheme.SecondarySurface,
            Padding = new Padding(10, 8, 10, 8)
        };
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115)); // Radio Cash Book
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115)); // Radio Bank Book
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));  // "Account:"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));   // Combo Account
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));  // "From (F2):"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125)); // DTP From
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 40));  // "To:"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 125)); // DTP To
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));   // Filter search
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // Refresh Button

        _rbCashBook = new RadioButton
        {
            Text = "Cash Book",
            Checked = true,
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = Color.FromArgb(24, 43, 73),
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };
        _rbCashBook.CheckedChanged += async (s, e) =>
        {
            if (_rbCashBook.Checked)
            {
                _currentBookType = CashBankBookType.CashBook;
                Text = "Cash Book — Cash-in-Hand Register";
                await ReloadAccountDropdownAsync();
            }
        };
        pnlFilters.Controls.Add(_rbCashBook, 0, 0);

        _rbBankBook = new RadioButton
        {
            Text = "Bank Book",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = Color.FromArgb(24, 43, 73),
            AutoSize = true,
            Anchor = AnchorStyles.Left
        };
        _rbBankBook.CheckedChanged += async (s, e) =>
        {
            if (_rbBankBook.Checked)
            {
                _currentBookType = CashBankBookType.BankBook;
                Text = "Bank Book — Bank Accounts Register";
                await ReloadAccountDropdownAsync();
            }
        };
        pnlFilters.Controls.Add(_rbBankBook, 1, 0);

        pnlFilters.Controls.Add(new Label
        {
            Text = "Account:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = ExecLedgerTheme.UIBold9
        }, 2, 0);

        _cmbAccount = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Dock = DockStyle.Fill
        };
        _cmbAccount.SelectedIndexChanged += async (s, e) => await LoadReportDataAsync();
        pnlFilters.Controls.Add(_cmbAccount, 3, 0);

        pnlFilters.Controls.Add(new Label
        {
            Text = "From (F2):",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = ExecLedgerTheme.UIBold9
        }, 4, 0);

        _dtpFromDate = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MMM-yyyy",
            Width = 120
        };
        pnlFilters.Controls.Add(_dtpFromDate, 5, 0);

        pnlFilters.Controls.Add(new Label
        {
            Text = "To:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Font = ExecLedgerTheme.UIBold9
        }, 6, 0);

        _dtpToDate = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MMM-yyyy",
            Width = 120
        };
        pnlFilters.Controls.Add(_dtpToDate, 7, 0);

        _txtSearch = new TextBox
        {
            PlaceholderText = "Search voucher, account (F3)...",
            Dock = DockStyle.Fill
        };
        _txtSearch.TextChanged += (s, e) => RenderReport();
        pnlFilters.Controls.Add(_txtSearch, 8, 0);

        _btnRefresh = new Button
        {
            Text = "Refresh (F5)",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(220, 230, 242)
        };
        _btnRefresh.Click += async (s, e) => await LoadReportDataAsync();
        pnlFilters.Controls.Add(_btnRefresh, 9, 0);

        mainLayout.Controls.Add(pnlFilters, 0, 0);

        // 2. Account Context Card
        var pnlCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(238, 244, 250),
            Padding = new Padding(12, 10, 12, 10)
        };

        _lblAccountInfo = new Label
        {
            Text = "Book: Cash Book | Account: All Cash Accounts",
            Font = ExecLedgerTheme.UIBold10,
            ForeColor = Color.FromArgb(24, 43, 73),
            AutoSize = true,
            Dock = DockStyle.Left
        };

        _lblOpeningBalance = new Label
        {
            Text = "Opening Balance: ₹0.00 Dr",
            Font = ExecLedgerTheme.UIBold10,
            ForeColor = Color.FromArgb(37, 99, 235),
            AutoSize = true,
            Dock = DockStyle.Right
        };

        pnlCard.Controls.Add(_lblAccountInfo);
        pnlCard.Controls.Add(_lblOpeningBalance);
        mainLayout.Controls.Add(pnlCard, 0, 1);

        // 3. High-Density DataGridView
        _dgvEntries = new Guna2DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.Fixed3D,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            AllowUserToResizeRows = false
        };

        SetupDataGridColumns();
        _dgvEntries.DoubleClick += (s, e) => DrillDownVoucher();
        _dgvEntries.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                DrillDownVoucher();
                e.Handled = true;
            }
        };
        mainLayout.Controls.Add(_dgvEntries, 0, 2);

        // 4. Footer Summary Panel
        var pnlFooter = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            BackColor = ExecLedgerTheme.SecondarySurface,
            Padding = new Padding(10, 8, 10, 8)
        };
        pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18));
        pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22));
        pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 23));
        pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));

        _lblTxnCount = new Label
        {
            Text = "Transactions: 0",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = Color.FromArgb(70, 80, 95),
            Anchor = AnchorStyles.Left,
            AutoSize = true
        };

        _lblTotalDebit = new Label
        {
            Text = "Total Receipts: ₹0.00",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = Color.FromArgb(16, 125, 65), // Green
            Anchor = AnchorStyles.Left,
            AutoSize = true
        };

        _lblTotalCredit = new Label
        {
            Text = "Total Payments: ₹0.00",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = Color.FromArgb(180, 40, 40), // Red
            Anchor = AnchorStyles.Left,
            AutoSize = true
        };

        _lblClosingBalance = new Label
        {
            Text = "Closing: ₹0.00 Dr",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = Color.FromArgb(24, 43, 73),
            Anchor = AnchorStyles.Left,
            AutoSize = true
        };

        _lblValidationBadge = new Label
        {
            Text = "[ ✔ ] RECONCILED",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = Color.FromArgb(16, 125, 65),
            Anchor = AnchorStyles.Right,
            AutoSize = true
        };

        pnlFooter.Controls.Add(_lblTxnCount, 0, 0);
        pnlFooter.Controls.Add(_lblTotalDebit, 1, 0);
        pnlFooter.Controls.Add(_lblTotalCredit, 2, 0);
        pnlFooter.Controls.Add(_lblClosingBalance, 3, 0);
        pnlFooter.Controls.Add(_lblValidationBadge, 4, 0);
        mainLayout.Controls.Add(pnlFooter, 0, 3);

        // 5. Action Buttons Panel
        var pnlActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 8, 0, 0)
        };

        _btnClose = new Button
        {
            Text = "Close (Esc)",
            Size = new Size(110, 35),
            BackColor = Color.FromArgb(235, 238, 242)
        };
        _btnClose.Click += (s, e) => Close();

        _btnPrint = new Button
        {
            Text = "Print (Ctrl+P)",
            Size = new Size(120, 35),
            BackColor = Color.FromArgb(220, 235, 252)
        };
        _btnPrint.Click += (s, e) => PrintStatement();

        _btnExportCsv = new Button
        {
            Text = "Export CSV",
            Size = new Size(110, 35),
            BackColor = Color.FromArgb(220, 245, 230)
        };
        _btnExportCsv.Click += (s, e) => ExportToCsv();

        _btnDrillDown = new Button
        {
            Text = "View Voucher (Enter)",
            Size = new Size(155, 35),
            BackColor = Color.FromArgb(24, 43, 73),
            ForeColor = Color.White
        };
        _btnDrillDown.Click += (s, e) => DrillDownVoucher();

        pnlActions.Controls.Add(_btnClose);
        pnlActions.Controls.Add(_btnPrint);
        pnlActions.Controls.Add(_btnExportCsv);
        pnlActions.Controls.Add(_btnDrillDown);
        mainLayout.Controls.Add(pnlActions, 0, 4);

        Controls.Add(mainLayout);

        // Hotkeys
        KeyDown += CashBankBookForm_KeyDown;
        Load += async (s, e) => await InitializeFormAsync();
    }

    private void SetupDataGridColumns()
    {
        _dgvEntries.Columns.Clear();

        _dgvEntries.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColDate",
            HeaderText = "Date",
            Width = 100,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });

        _dgvEntries.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColVchType",
            HeaderText = "Type",
            Width = 90
        });

        _dgvEntries.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColVchNo",
            HeaderText = "Voucher No",
            Width = 115
        });

        _dgvEntries.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColRefNo",
            HeaderText = "Ref / Inst #",
            Width = 100
        });

        _dgvEntries.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColParticulars",
            HeaderText = "Particulars (Opposing Account)",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 35
        });

        _dgvEntries.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColAccount",
            HeaderText = "Account",
            Width = 120
        });

        _dgvEntries.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColDebit",
            HeaderText = "Receipts / Deposits (₹ Dr)",
            Width = 145,
            DefaultCellStyle = {
                Alignment = DataGridViewContentAlignment.MiddleRight,
                Format = "N2",
                ForeColor = Color.FromArgb(16, 125, 65)
            }
        });

        _dgvEntries.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColCredit",
            HeaderText = "Payments / Withdrawals (₹ Cr)",
            Width = 145,
            DefaultCellStyle = {
                Alignment = DataGridViewContentAlignment.MiddleRight,
                Format = "N2",
                ForeColor = Color.FromArgb(180, 40, 40)
            }
        });

        _dgvEntries.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColRunningBalance",
            HeaderText = "Running Balance (₹)",
            Width = 150,
            DefaultCellStyle = {
                Alignment = DataGridViewContentAlignment.MiddleRight,
                Font = ExecLedgerTheme.UIBold9,
                ForeColor = Color.FromArgb(24, 43, 73)
            }
        });

        _dgvEntries.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColNarration",
            HeaderText = "Narration",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            FillWeight = 25
        });
    }

    private async Task InitializeFormAsync()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            MessageBox.Show(this, "Please open a company first.", "No Company", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Close();
            return;
        }

        var fy = _companyContext.CurrentFinancialYear;
        if (fy != null)
        {
            _dtpFromDate.Value = fy.StartDate;
            _dtpToDate.Value = fy.EndDate > DateTime.Today ? DateTime.Today : fy.EndDate;
        }
        else
        {
            _dtpFromDate.Value = new DateTime(DateTime.Today.Year, 4, 1);
            _dtpToDate.Value = DateTime.Today;
        }

        await ReloadAccountDropdownAsync();
    }

    private async Task ReloadAccountDropdownAsync()
    {
        var company = _companyContext.CurrentCompany;
        if (company == null) return;

        try
        {
            UseWaitCursor = true;

            _availableLedgers = _currentBookType == CashBankBookType.CashBook
                ? await _accountingService.GetCashLedgersAsync(company.CompanyId)
                : await _accountingService.GetBankLedgersAsync(company.CompanyId);

            var items = new List<AccountComboItem>();
            string allLabel = _currentBookType == CashBankBookType.CashBook
                ? "[ All Cash Accounts ]"
                : "[ All Bank Accounts ]";

            items.Add(new AccountComboItem { LedgerId = null, DisplayName = allLabel });

            foreach (var l in _availableLedgers.OrderBy(l => l.LedgerName))
            {
                items.Add(new AccountComboItem { LedgerId = l.LedgerId, DisplayName = l.LedgerName });
            }

            _cmbAccount.DataSource = null;
            _cmbAccount.DisplayMember = "DisplayName";
            _cmbAccount.ValueMember = "LedgerId";
            _cmbAccount.DataSource = items;
            _cmbAccount.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error loading accounts: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task LoadReportDataAsync()
    {
        var company = _companyContext.CurrentCompany;
        if (company == null || _cmbAccount.SelectedItem is not AccountComboItem selectedItem) return;

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

            _currentReport = await _accountingService.GetCashBankBookAsync(
                company.CompanyId,
                selectedItem.LedgerId,
                _currentBookType,
                from,
                to);

            RenderReport();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to load book statement: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void RenderReport()
    {
        _dgvEntries.Rows.Clear();
        if (_currentReport == null) return;

        string bookName = _currentReport.BookType == CashBankBookType.CashBook ? "Cash Book" : "Bank Book";
        _lblAccountInfo.Text = $"Book: {bookName} | Account: {_currentReport.SelectedLedgerName}";
        _lblOpeningBalance.Text = $"Opening Balance (as of {_currentReport.FromDate:dd-MMM-yyyy}): {_currentReport.FormattedOpeningBalance}";

        // Render Opening Balance Row
        var opIdx = _dgvEntries.Rows.Add();
        var opRow = _dgvEntries.Rows[opIdx];
        opRow.DefaultCellStyle.BackColor = ExecLedgerTheme.SecondarySurface;
        opRow.DefaultCellStyle.Font = ExecLedgerTheme.UIRegular8;
        opRow.Cells["ColDate"].Value = _currentReport.FromDate.ToString("dd-MMM-yyyy");
        opRow.Cells["ColParticulars"].Value = "** Opening Balance **";
        opRow.Cells["ColRunningBalance"].Value = _currentReport.FormattedOpeningBalance;

        var filter = _txtSearch.Text.Trim().ToLowerInvariant();

        var filteredLines = string.IsNullOrWhiteSpace(filter)
            ? _currentReport.Lines
            : _currentReport.Lines.Where(l =>
                l.VoucherNumber.ToLowerInvariant().Contains(filter) ||
                l.Particulars.ToLowerInvariant().Contains(filter) ||
                l.ReferenceNumber.ToLowerInvariant().Contains(filter) ||
                l.AccountName.ToLowerInvariant().Contains(filter) ||
                l.Narration.ToLowerInvariant().Contains(filter)).ToList();

        foreach (var line in filteredLines)
        {
            var idx = _dgvEntries.Rows.Add();
            var row = _dgvEntries.Rows[idx];
            row.Tag = line;

            row.Cells["ColDate"].Value = line.Date.ToString("dd-MMM-yyyy");
            row.Cells["ColVchType"].Value = line.VoucherTypeName;
            row.Cells["ColVchNo"].Value = line.VoucherNumber;
            row.Cells["ColRefNo"].Value = line.ReferenceNumber;
            row.Cells["ColParticulars"].Value = line.Particulars;
            row.Cells["ColAccount"].Value = line.AccountName;
            row.Cells["ColDebit"].Value = line.Debit > 0 ? line.Debit : (object)string.Empty;
            row.Cells["ColCredit"].Value = line.Credit > 0 ? line.Credit : (object)string.Empty;

            var runningTypeStr = line.RunningType == BalanceType.Debit ? "Dr" : "Cr";
            row.Cells["ColRunningBalance"].Value = $"₹{line.RunningBalance:N2} {runningTypeStr}";
            row.Cells["ColNarration"].Value = line.Narration;
        }

        // Summary Labels
        string debitTitle = _currentReport.BookType == CashBankBookType.CashBook ? "Total Receipts" : "Total Deposits";
        string creditTitle = _currentReport.BookType == CashBankBookType.CashBook ? "Total Payments" : "Total Withdrawals";

        _lblTxnCount.Text = $"Transactions: {_currentReport.Lines.Count}" +
            (filteredLines.Count != _currentReport.Lines.Count ? $" (Shown: {filteredLines.Count})" : "");

        _lblTotalDebit.Text = $"{debitTitle}: ₹{_currentReport.TotalDebit:N2}";
        _lblTotalCredit.Text = $"{creditTitle}: ₹{_currentReport.TotalCredit:N2}";
        _lblClosingBalance.Text = $"Closing: {_currentReport.FormattedClosingBalance}";

        // Mathematical identity verification
        decimal openingNet = _currentReport.OpeningType == BalanceType.Debit
            ? _currentReport.OpeningBalance
            : -_currentReport.OpeningBalance;
        decimal closingNet = _currentReport.ClosingType == BalanceType.Debit
            ? _currentReport.ClosingBalance
            : -_currentReport.ClosingBalance;
        decimal expectedClosing = openingNet + _currentReport.TotalDebit - _currentReport.TotalCredit;

        if (Math.Round(closingNet, 2) == Math.Round(expectedClosing, 2))
        {
            _lblValidationBadge.Text = "[ ✔ ] RECONCILED";
            _lblValidationBadge.ForeColor = Color.FromArgb(16, 125, 65);
        }
        else
        {
            _lblValidationBadge.Text = $"[ ⚠ ] DIFF: ₹{Math.Abs(closingNet - expectedClosing):N2}";
            _lblValidationBadge.ForeColor = Color.FromArgb(200, 30, 30);
        }
    }

    private void DrillDownVoucher()
    {
        if (_dgvEntries.CurrentRow?.Tag is not CashBankBookLineDto line) return;

        var sb = new StringBuilder();
        sb.AppendLine($"Voucher No: {line.VoucherNumber}");
        sb.AppendLine($"Voucher Type: {line.VoucherTypeName}");
        sb.AppendLine($"Date: {line.Date:dd-MMM-yyyy}");
        if (!string.IsNullOrWhiteSpace(line.ReferenceNumber))
            sb.AppendLine($"Ref / Cheque No: {line.ReferenceNumber}");
        sb.AppendLine($"Account: {line.AccountName}");
        sb.AppendLine($"Opposing Particulars: {line.Particulars}");
        if (line.Debit > 0)
            sb.AppendLine($"Receipt / Deposit (Dr): ₹{line.Debit:N2}");
        if (line.Credit > 0)
            sb.AppendLine($"Payment / Withdrawal (Cr): ₹{line.Credit:N2}");
        sb.AppendLine($"Running Balance: ₹{line.RunningBalance:N2} {(line.RunningType == BalanceType.Debit ? "Dr" : "Cr")}");
        sb.AppendLine($"Narration: {line.Narration}");

        MessageBox.Show(this, sb.ToString(), $"Voucher Audit Detail — {line.VoucherNumber}", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportToCsv()
    {
        if (_currentReport == null || _currentReport.Lines.Count == 0)
        {
            MessageBox.Show(this, "No transactions to export.", "Empty Register", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        string prefix = _currentReport.BookType == CashBankBookType.CashBook ? "CashBook" : "BankBook";
        using var sfd = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv",
            FileName = $"{prefix}_{_currentReport.SelectedLedgerName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"\"{_currentReport.BookType}: {_currentReport.SelectedLedgerName}\"");
            sb.AppendLine($"\"Period: {_currentReport.FromDate:dd-MMM-yyyy} to {_currentReport.ToDate:dd-MMM-yyyy}\"");
            sb.AppendLine($"\"Opening Balance: {_currentReport.OpeningBalance:F2} {_currentReport.OpeningType}\"");
            sb.AppendLine();
            sb.AppendLine("Date,VoucherType,VoucherNo,ReferenceNo,Particulars,Account,Debit,Credit,RunningBalance,Narration");

            foreach (var line in _currentReport.Lines)
            {
                var runType = line.RunningType == BalanceType.Debit ? "Dr" : "Cr";
                sb.AppendLine($"\"{line.Date:dd-MMM-yyyy}\",\"{line.VoucherTypeName}\",\"{line.VoucherNumber}\",\"{EscapeCsv(line.ReferenceNumber)}\",\"{EscapeCsv(line.Particulars)}\",\"{EscapeCsv(line.AccountName)}\",{line.Debit:F2},{line.Credit:F2},\"{line.RunningBalance:F2} {runType}\",\"{EscapeCsv(line.Narration)}\"");
            }

            sb.AppendLine();
            sb.AppendLine($",,,,Total Debits:,{_currentReport.TotalDebit:F2},Total Credits:,{_currentReport.TotalCredit:F2}");
            sb.AppendLine($",,,,Closing Balance:,{_currentReport.ClosingBalance:F2} {_currentReport.ClosingType}");

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show(this, $"Exported {_currentReport.Lines.Count} entries successfully to:\n{sfd.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void PrintStatement()
    {
        if (_currentReport == null || _currentReport.Lines.Count == 0)
        {
            MessageBox.Show(this, "No transactions to print.", "Empty Register", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            using var printDoc = new PrintDocument();
            printDoc.DocumentName = $"{_currentReport.BookType} - {_currentReport.SelectedLedgerName}";
            int currentLineIndex = 0;

            printDoc.PrintPage += (s, e) =>
            {
                var g = e.Graphics!;
                float y = 50;
                var fontHeader = new Font("Segoe UI", 13F, FontStyle.Bold);
                var fontSubHeader = ExecLedgerTheme.UIBold9;
                var fontBody = ExecLedgerTheme.UIRegular8;
                var fontItalic = ExecLedgerTheme.UIRegular8;

                // Title
                string companyName = _companyContext.CurrentCompany?.CompanyName ?? "MoneyFlow Desktop Accounting";
                g.DrawString(companyName, fontHeader, Brushes.Black, 50, y);
                y += 25;

                string bookTitle = _currentReport.BookType == CashBankBookType.CashBook ? "CASH BOOK" : "BANK BOOK";
                g.DrawString($"{bookTitle} — {_currentReport.SelectedLedgerName.ToUpper()}", fontSubHeader, Brushes.Black, 50, y);
                y += 20;

                g.DrawString($"For the Period: {_currentReport.FromDate:dd-MMM-yyyy} to {_currentReport.ToDate:dd-MMM-yyyy} | Opening: {_currentReport.FormattedOpeningBalance}", fontBody, Brushes.DarkSlateGray, 50, y);
                y += 25;

                g.DrawLine(Pens.Black, 50, y, e.MarginBounds.Right, y);
                y += 5;

                // Table Header
                g.DrawString("Date", fontSubHeader, Brushes.Black, 50, y);
                g.DrawString("Type", fontSubHeader, Brushes.Black, 135, y);
                g.DrawString("Vch No", fontSubHeader, Brushes.Black, 200, y);
                g.DrawString("Particulars", fontSubHeader, Brushes.Black, 290, y);
                g.DrawString("Receipt/Dep (Dr)", fontSubHeader, Brushes.Black, 490, y);
                g.DrawString("Pmt/Wdl (Cr)", fontSubHeader, Brushes.Black, 590, y);
                g.DrawString("Balance", fontSubHeader, Brushes.Black, 680, y);
                y += 20;

                g.DrawLine(Pens.Black, 50, y, e.MarginBounds.Right, y);
                y += 8;

                while (currentLineIndex < _currentReport.Lines.Count)
                {
                    if (y > e.MarginBounds.Bottom - 40)
                    {
                        e.HasMorePages = true;
                        return;
                    }

                    var line = _currentReport.Lines[currentLineIndex];
                    g.DrawString(line.Date.ToString("dd-MMM-yyyy"), fontBody, Brushes.Black, 50, y);
                    g.DrawString(line.VoucherTypeName, fontBody, Brushes.Black, 135, y);
                    g.DrawString(line.VoucherNumber, fontBody, Brushes.Black, 200, y);

                    string part = line.Particulars.Length > 28 ? line.Particulars.Substring(0, 25) + "..." : line.Particulars;
                    g.DrawString(part, fontBody, Brushes.Black, 290, y);

                    if (line.Debit > 0)
                        g.DrawString(line.Debit.ToString("N2"), fontBody, Brushes.DarkGreen, 490, y);
                    if (line.Credit > 0)
                        g.DrawString(line.Credit.ToString("N2"), fontBody, Brushes.DarkRed, 590, y);

                    var runType = line.RunningType == BalanceType.Debit ? "Dr" : "Cr";
                    g.DrawString($"{line.RunningBalance:N2} {runType}", fontBody, Brushes.Black, 680, y);

                    y += 18;
                    currentLineIndex++;
                }

                // Summary footer
                y += 10;
                g.DrawLine(Pens.Black, 50, y, e.MarginBounds.Right, y);
                y += 5;
                g.DrawString($"Receipts: ₹{_currentReport.TotalDebit:N2} | Payments: ₹{_currentReport.TotalCredit:N2} | Closing: {_currentReport.FormattedClosingBalance}", fontSubHeader, Brushes.Black, 50, y);

                e.HasMorePages = false;
            };

            using var preview = new PrintPreviewDialog
            {
                Document = printDoc,
                Width = 950,
                Height = 700,
                StartPosition = FormStartPosition.CenterParent
            };
            preview.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Print Preview failed: {ex.Message}", "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string EscapeCsv(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;
        return field.Replace("\"", "\"\"");
    }

    private void CashBankBookForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F2)
        {
            _dtpFromDate.Focus();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F3)
        {
            _txtSearch.Focus();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F4)
        {
            _cmbAccount.Focus();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F5)
        {
            _ = LoadReportDataAsync();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.P)
        {
            PrintStatement();
            e.Handled = true;
        }
    }

    private class AccountComboItem
    {
        public int? LedgerId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}
