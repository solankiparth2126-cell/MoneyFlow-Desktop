using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Forms;

public class ContraVoucherForm : Form
{
    private readonly IAccountingService _accountingService;
    private readonly ILedgerService _ledgerService;
    private readonly ICompanyContext _companyContext;

    private VoucherType? _contraVoucherType;
    private IReadOnlyList<LedgerSummaryDto> _cashBankLedgers = new List<LedgerSummaryDto>();

    // UI Controls
    private Label _lblVoucherNumber = null!;
    private DateTimePicker _dtpVoucherDate = null!;
    private ComboBox _cmbDestinationAccount = null!;
    private Label _lblDestinationBalance = null!;
    private DataGridView _dgvEntries = null!;
    private TextBox _txtNarration = null!;
    private Label _lblTotalAmount = null!;
    private Label _lblBalanceStatus = null!;
    private Button _btnSave = null!;
    private Button _btnSaveAndNew = null!;
    private Button _btnNew = null!;
    private Button _btnPrint = null!;
    private Button _btnCancel = null!;

    public ContraVoucherForm(
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
        Text = "Contra Voucher (F4) — Cash & Bank Transfer";
        Size = new Size(920, 650);
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
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 115)); // Header + Helper
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));  // Narration & Summary
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Buttons

        // 1. Header Panel
        var pnlHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 3,
            BackColor = Color.FromArgb(245, 248, 252),
            Padding = new Padding(10)
        };
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        // Row 0: Voucher No & Date
        pnlHeader.Controls.Add(new Label { Text = "Voucher No:", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 0);
        _lblVoucherNumber = new Label { Text = "CTR-00001", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.FromArgb(0, 51, 102), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        pnlHeader.Controls.Add(_lblVoucherNumber, 1, 0);

        pnlHeader.Controls.Add(new Label { Text = "Date:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
        _dtpVoucherDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 140 };
        pnlHeader.Controls.Add(_dtpVoucherDate, 3, 0);

        // Row 1: Destination Account (Debit: Cash/Bank Receiving Funds)
        pnlHeader.Controls.Add(new Label { Text = "Destination Account (Dr):", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 1);
        var pnlAccount = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _cmbDestinationAccount = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, Font = new Font("Segoe UI", 9.5F) };
        _cmbDestinationAccount.SelectedIndexChanged += async (s, e) => await OnDestinationAccountSelectedAsync();
        _lblDestinationBalance = new Label { Text = "Cur Bal: ₹0.00", AutoSize = true, ForeColor = Color.FromArgb(0, 100, 0), Margin = new Padding(10, 5, 0, 0) };
        pnlAccount.Controls.Add(_cmbDestinationAccount);
        pnlAccount.Controls.Add(_lblDestinationBalance);
        pnlHeader.Controls.Add(pnlAccount, 1, 1);
        pnlHeader.SetColumnSpan(pnlAccount, 3);

        // Row 2: Quick Transfer Templates
        var pnlTemplates = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(0, 4, 0, 0) };
        var lblTemplate = new Label { Text = "Quick Mode:", AutoSize = true, ForeColor = Color.FromArgb(100, 110, 120), Font = new Font("Segoe UI", 8.5F, FontStyle.Bold), Margin = new Padding(0, 3, 8, 0) };
        pnlTemplates.Controls.Add(lblTemplate);

        var btnDeposit = new Button { Text = "Cash Deposit to Bank", AutoSize = true, Height = 25, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(230, 242, 255), Font = new Font("Segoe UI", 8.5F) };
        btnDeposit.Click += (s, e) => ApplyTransferTemplate(isDeposit: true);
        pnlTemplates.Controls.Add(btnDeposit);

        var btnWithdraw = new Button { Text = "Cash Withdrawal from Bank", AutoSize = true, Height = 25, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(230, 242, 255), Font = new Font("Segoe UI", 8.5F), Margin = new Padding(8, 0, 0, 0) };
        btnWithdraw.Click += (s, e) => ApplyTransferTemplate(isDeposit: false);
        pnlTemplates.Controls.Add(btnWithdraw);

        pnlHeader.Controls.Add(pnlTemplates, 1, 2);
        pnlHeader.SetColumnSpan(pnlTemplates, 3);

        // 2. DataGridView for Line Items (Credit entries - Source of funds)
        _dgvEntries = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToResizeRows = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            GridColor = Color.FromArgb(230, 230, 230),
            RowHeadersWidth = 30,
            RowTemplate = { Height = 28 }
        };

        var colLedger = new DataGridViewComboBoxColumn
        {
            HeaderText = "Particulars (Source Cr A/c)",
            Name = "ColLedger",
            Width = 300,
            FlatStyle = FlatStyle.Flat
        };

        var colBalance = new DataGridViewTextBoxColumn
        {
            HeaderText = "Current Balance",
            Name = "ColBalance",
            Width = 140,
            ReadOnly = true,
            DefaultCellStyle = { ForeColor = Color.DimGray }
        };

        var colAmount = new DataGridViewTextBoxColumn
        {
            HeaderText = "Amount (₹)",
            Name = "ColAmount",
            Width = 130,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        };

        var colMode = new DataGridViewTextBoxColumn
        {
            HeaderText = "Transfer Mode / Instrument",
            Name = "ColMode",
            Width = 180
        };

        var colNarration = new DataGridViewTextBoxColumn
        {
            HeaderText = "Line Narration",
            Name = "ColNarration",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };

        _dgvEntries.Columns.AddRange(colLedger, colBalance, colAmount, colMode, colNarration);
        _dgvEntries.CellValueChanged += async (s, e) => await OnGridCellValueChangedAsync(e.RowIndex, e.ColumnIndex);
        _dgvEntries.RowsRemoved += (s, e) => RecalculateTotals();

        // 3. Narration and Summary Panel
        var pnlSummary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(5)
        };
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        pnlSummary.Controls.Add(new Label { Text = "Voucher Narration:", AutoSize = true }, 0, 0);
        _lblTotalAmount = new Label
        {
            Text = "Total Amount: ₹0.00",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 51, 102),
            Anchor = AnchorStyles.Right,
            AutoSize = true
        };
        pnlSummary.Controls.Add(_lblTotalAmount, 1, 0);

        _txtNarration = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F) };
        pnlSummary.Controls.Add(_txtNarration, 0, 1);

        _lblBalanceStatus = new Label
        {
            Text = "Voucher Balanced",
            ForeColor = Color.FromArgb(16, 185, 129),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Anchor = AnchorStyles.Right,
            AutoSize = true
        };
        pnlSummary.Controls.Add(_lblBalanceStatus, 1, 1);

        // 4. Action Buttons Panel
        var pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 8, 0, 0)
        };

        _btnSave = new Button
        {
            Text = "Save (Ctrl+A)",
            BackColor = Color.FromArgb(0, 51, 102),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Height = 34,
            Width = 130,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Margin = new Padding(0, 0, 8, 0)
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += async (s, e) => await SaveVoucherAsync(closeAfterSave: true);

        _btnSaveAndNew = new Button
        {
            Text = "Save & New (Alt+S)",
            BackColor = Color.FromArgb(24, 43, 73),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Height = 34,
            Width = 140,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Margin = new Padding(0, 0, 8, 0)
        };
        _btnSaveAndNew.FlatAppearance.BorderSize = 0;
        _btnSaveAndNew.Click += async (s, e) => await SaveVoucherAsync(closeAfterSave: false);

        _btnNew = new Button
        {
            Text = "Clear (Alt+N)",
            Height = 34,
            Width = 100,
            Margin = new Padding(0, 0, 8, 0)
        };
        _btnNew.Click += (s, e) => ResetForm();

        _btnPrint = new Button
        {
            Text = "Print (Ctrl+P)",
            Height = 34,
            Width = 100,
            Margin = new Padding(0, 0, 8, 0)
        };
        _btnPrint.Click += (s, e) => MessageBox.Show("Voucher print preview will be configured with RDLC reports in Phase 33.", "Print Voucher", MessageBoxButtons.OK, MessageBoxIcon.Information);

        _btnCancel = new Button
        {
            Text = "Cancel (Esc)",
            Height = 34,
            Width = 100
        };
        _btnCancel.Click += (s, e) => Close();

        pnlButtons.Controls.AddRange(new Control[] { _btnSave, _btnSaveAndNew, _btnNew, _btnPrint, _btnCancel });

        mainLayout.Controls.Add(pnlHeader, 0, 0);
        mainLayout.Controls.Add(_dgvEntries, 0, 1);
        mainLayout.Controls.Add(pnlSummary, 0, 2);
        mainLayout.Controls.Add(pnlButtons, 0, 3);

        Controls.Add(mainLayout);

        Load += async (s, e) => await InitializeFormDataAsync();
    }

    private async Task InitializeFormDataAsync()
    {
        try
        {
            UseWaitCursor = true;

            var company = _companyContext.CurrentCompany;
            var fy = _companyContext.CurrentFinancialYear;

            if (company == null || fy == null)
            {
                MessageBox.Show("No active company or financial year selected.", "MoneyFlow", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
                return;
            }

            // Set FY date range on picker
            _dtpVoucherDate.MinDate = fy.StartDate;
            _dtpVoucherDate.MaxDate = fy.EndDate;
            _dtpVoucherDate.Value = DateTime.Today >= fy.StartDate && DateTime.Today <= fy.EndDate ? DateTime.Today : fy.StartDate;

            // Load Contra Voucher Type
            _contraVoucherType = await _accountingService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Contra);

            // Preview voucher number
            await RefreshVoucherNumberPreviewAsync();

            // Load Cash and Bank Ledgers for both Destination and Source
            _cashBankLedgers = await _accountingService.GetCashAndBankLedgersAsync(company.CompanyId);

            // Populate Destination Account ComboBox
            _cmbDestinationAccount.DisplayMember = "LedgerName";
            _cmbDestinationAccount.ValueMember = "LedgerId";
            _cmbDestinationAccount.DataSource = _cashBankLedgers.ToList();

            // Populate Grid's Particulars (Source Account) Column
            var colLedger = (DataGridViewComboBoxColumn)_dgvEntries.Columns["ColLedger"];
            colLedger.DisplayMember = "LedgerName";
            colLedger.ValueMember = "LedgerId";
            colLedger.DataSource = _cashBankLedgers.ToList();

            // Pre-add empty row
            if (_dgvEntries.Rows.Count == 0)
            {
                _dgvEntries.Rows.Add();
            }

            RecalculateTotals();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize Contra Voucher: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task RefreshVoucherNumberPreviewAsync()
    {
        if (_contraVoucherType == null || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
            return;

        var nextNumber = await _accountingService.GetNextVoucherNumberPreviewAsync(
            _companyContext.CurrentCompany.CompanyId,
            _contraVoucherType.VoucherTypeId,
            _companyContext.CurrentFinancialYear.FinancialYearId);

        _lblVoucherNumber.Text = nextNumber;
    }

    private async Task OnDestinationAccountSelectedAsync()
    {
        if (_cmbDestinationAccount.SelectedItem is LedgerSummaryDto selected && _companyContext.CurrentCompany != null)
        {
            var balance = await _accountingService.GetLedgerBalanceAsync(_companyContext.CurrentCompany.CompanyId, selected.LedgerId);
            _lblDestinationBalance.Text = $"Cur Bal: {balance.ClosingBalanceDisplay}";
            _lblDestinationBalance.ForeColor = balance.ClosingBalance >= 0 ? Color.FromArgb(0, 100, 0) : Color.DarkRed;
        }
        else
        {
            _lblDestinationBalance.Text = "Cur Bal: ₹0.00";
        }
    }

    private void ApplyTransferTemplate(bool isDeposit)
    {
        if (_cashBankLedgers.Count == 0) return;

        var cashLedger = _cashBankLedgers.FirstOrDefault(l => l.GroupName.Contains("Cash", StringComparison.OrdinalIgnoreCase) || l.LedgerName.Contains("Cash", StringComparison.OrdinalIgnoreCase));
        var bankLedger = _cashBankLedgers.FirstOrDefault(l => l.GroupName.Contains("Bank", StringComparison.OrdinalIgnoreCase) || l.LedgerName.Contains("Bank", StringComparison.OrdinalIgnoreCase));

        if (isDeposit)
        {
            // Cash -> Bank: Bank is Destination (Dr), Cash is Source (Cr)
            if (bankLedger != null)
            {
                _cmbDestinationAccount.SelectedValue = bankLedger.LedgerId;
            }
            if (cashLedger != null && _dgvEntries.Rows.Count > 0)
            {
                _dgvEntries.Rows[0].Cells["ColLedger"].Value = cashLedger.LedgerId;
                _dgvEntries.Rows[0].Cells["ColMode"].Value = "Cash Deposit";
            }
            _txtNarration.Text = "Being cash deposited into bank.";
        }
        else
        {
            // Bank -> Cash: Cash is Destination (Dr), Bank is Source (Cr)
            if (cashLedger != null)
            {
                _cmbDestinationAccount.SelectedValue = cashLedger.LedgerId;
            }
            if (bankLedger != null && _dgvEntries.Rows.Count > 0)
            {
                _dgvEntries.Rows[0].Cells["ColLedger"].Value = bankLedger.LedgerId;
                _dgvEntries.Rows[0].Cells["ColMode"].Value = "Cash Withdrawal";
            }
            _txtNarration.Text = "Being cash withdrawn from bank for office use.";
        }
    }

    private async Task OnGridCellValueChangedAsync(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || rowIndex >= _dgvEntries.Rows.Count) return;

        var row = _dgvEntries.Rows[rowIndex];

        // If Ledger changed, update balance
        if (columnIndex == _dgvEntries.Columns["ColLedger"].Index)
        {
            if (row.Cells["ColLedger"].Value is int ledgerId && _companyContext.CurrentCompany != null)
            {
                var balance = await _accountingService.GetLedgerBalanceAsync(_companyContext.CurrentCompany.CompanyId, ledgerId);
                row.Cells["ColBalance"].Value = balance.ClosingBalanceDisplay;
            }
            else
            {
                row.Cells["ColBalance"].Value = string.Empty;
            }
        }

        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        decimal totalAmount = 0;

        foreach (DataGridViewRow row in _dgvEntries.Rows)
        {
            if (row.IsNewRow) continue;

            if (decimal.TryParse(row.Cells["ColAmount"].Value?.ToString(), out var amt) && amt > 0)
            {
                totalAmount += amt;
            }
        }

        _lblTotalAmount.Text = $"Total Amount: ₹{totalAmount:N2}";

        if (totalAmount > 0 && _cmbDestinationAccount.SelectedIndex >= 0)
        {
            _lblBalanceStatus.Text = "Voucher Balanced (Dr = Cr)";
            _lblBalanceStatus.ForeColor = Color.FromArgb(16, 185, 129);
            _btnSave.Enabled = true;
            _btnSaveAndNew.Enabled = true;
        }
        else
        {
            _lblBalanceStatus.Text = totalAmount <= 0 ? "Enter credit amount" : "Select destination account";
            _lblBalanceStatus.ForeColor = Color.DarkOrange;
            _btnSave.Enabled = false;
            _btnSaveAndNew.Enabled = false;
        }
    }

    private async Task SaveVoucherAsync(bool closeAfterSave)
    {
        try
        {
            var company = _companyContext.CurrentCompany;
            var fy = _companyContext.CurrentFinancialYear;

            if (company == null || fy == null)
            {
                MessageBox.Show("Active company or financial year is missing.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_contraVoucherType == null)
            {
                MessageBox.Show("Contra voucher type not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_cmbDestinationAccount.SelectedValue is not int destinationLedgerId || destinationLedgerId <= 0)
            {
                MessageBox.Show("Please select a receiving destination Cash/Bank account.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _cmbDestinationAccount.Focus();
                return;
            }

            // Gather line entries (Credit entries)
            var entries = new List<VoucherEntryDto>();
            decimal totalCredit = 0;

            for (int i = 0; i < _dgvEntries.Rows.Count; i++)
            {
                var row = _dgvEntries.Rows[i];
                if (row.IsNewRow) continue;

                if (row.Cells["ColLedger"].Value is not int sourceLedgerId || sourceLedgerId <= 0)
                {
                    continue;
                }

                if (sourceLedgerId == destinationLedgerId)
                {
                    MessageBox.Show($"Row {i + 1}: Destination and Source accounts cannot be the same ledger.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (!decimal.TryParse(row.Cells["ColAmount"].Value?.ToString(), out var amt) || amt <= 0)
                {
                    MessageBox.Show($"Row {i + 1}: Please enter a valid positive credit amount.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var mode = row.Cells["ColMode"].Value?.ToString()?.Trim() ?? string.Empty;
                var lineNarration = row.Cells["ColNarration"].Value?.ToString()?.Trim() ?? string.Empty;
                var combinedNarration = !string.IsNullOrEmpty(mode) 
                    ? (!string.IsNullOrEmpty(lineNarration) ? $"[{mode}] {lineNarration}" : $"[{mode}]")
                    : lineNarration;

                entries.Add(new VoucherEntryDto
                {
                    LedgerId = sourceLedgerId,
                    Debit = 0,
                    Credit = amt,
                    Narration = combinedNarration
                });

                totalCredit += amt;
            }

            if (entries.Count == 0 || totalCredit <= 0)
            {
                MessageBox.Show("Please enter at least one source line item with an amount.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Destination account gets Debited for total amount transferred
            entries.Insert(0, new VoucherEntryDto
            {
                LedgerId = destinationLedgerId,
                Debit = totalCredit,
                Credit = 0,
                Narration = _txtNarration.Text.Trim()
            });

            var voucherDto = new VoucherCreateDto
            {
                FinancialYearId = fy.FinancialYearId,
                VoucherTypeId = _contraVoucherType.VoucherTypeId,
                VoucherDate = _dtpVoucherDate.Value.Date,
                ReferenceNumber = string.Empty,
                Narration = _txtNarration.Text.Trim(),
                Entries = entries
            };

            UseWaitCursor = true;
            _btnSave.Enabled = false;
            _btnSaveAndNew.Enabled = false;

            var savedVoucher = await _accountingService.SaveVoucherAsync(company.CompanyId, voucherDto);

            MessageBox.Show(
                $"Contra Voucher '{savedVoucher.VoucherNumber}' of ₹{totalCredit:N2} saved successfully.",
                "Contra Voucher Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            if (closeAfterSave)
            {
                Close();
            }
            else
            {
                ResetForm();
                await RefreshVoucherNumberPreviewAsync();
                await OnDestinationAccountSelectedAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save Contra Voucher:\n{ex.Message}", "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
            _btnSave.Enabled = true;
            _btnSaveAndNew.Enabled = true;
        }
    }

    private void ResetForm()
    {
        _dgvEntries.Rows.Clear();
        _dgvEntries.Rows.Add();
        _txtNarration.Clear();
        RecalculateTotals();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.A))
        {
            _btnSave.PerformClick();
            return true;
        }
        if (keyData == (Keys.Alt | Keys.S))
        {
            _btnSaveAndNew.PerformClick();
            return true;
        }
        if (keyData == (Keys.Alt | Keys.N))
        {
            _btnNew.PerformClick();
            return true;
        }
        if (keyData == (Keys.Control | Keys.P))
        {
            _btnPrint.PerformClick();
            return true;
        }
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }
}
