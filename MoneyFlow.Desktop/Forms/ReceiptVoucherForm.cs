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

public class ReceiptVoucherForm : Form
{
    private readonly IAccountingService _accountingService;
    private readonly ILedgerService _ledgerService;
    private readonly ICompanyContext _companyContext;

    private VoucherType? _receiptVoucherType;
    private IReadOnlyList<LedgerSummaryDto> _cashBankLedgers = new List<LedgerSummaryDto>();
    private IReadOnlyList<LedgerSummaryDto> _allLedgers = new List<LedgerSummaryDto>();

    // UI Controls
    private Label _lblVoucherNumber = null!;
    private DateTimePicker _dtpVoucherDate = null!;
    private ComboBox _cmbAccount = null!;
    private Label _lblAccountBalance = null!;
    private DataGridView _dgvEntries = null!;
    private TextBox _txtNarration = null!;
    private Label _lblTotalAmount = null!;
    private Label _lblBalanceStatus = null!;
    private Button _btnSave = null!;
    private Button _btnSaveAndNew = null!;
    private Button _btnNew = null!;
    private Button _btnPrint = null!;
    private Button _btnCancel = null!;

    public ReceiptVoucherForm(
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
        Text = "Receipt Voucher (F6)";
        Size = new Size(880, 640);
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
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));  // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));  // Narration & Summary
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Buttons

        // 1. Header Panel
        var pnlHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            BackColor = Color.FromArgb(245, 248, 252),
            Padding = new Padding(10)
        };
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        // Row 0: Voucher No & Date
        pnlHeader.Controls.Add(new Label { Text = "Voucher No:", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 0);
        _lblVoucherNumber = new Label { Text = "RCT-00001", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.FromArgb(0, 51, 102), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        pnlHeader.Controls.Add(_lblVoucherNumber, 1, 0);

        pnlHeader.Controls.Add(new Label { Text = "Date:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
        _dtpVoucherDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 140 };
        pnlHeader.Controls.Add(_dtpVoucherDate, 3, 0);

        // Row 1: Receiving Account (Debit: Cash/Bank)
        pnlHeader.Controls.Add(new Label { Text = "Account (Dr):", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 1);
        var pnlAccount = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _cmbAccount = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 230, Font = new Font("Segoe UI", 9.5F) };
        _cmbAccount.SelectedIndexChanged += async (s, e) => await OnAccountSelectedAsync();
        _lblAccountBalance = new Label { Text = "Cur Bal: ₹0.00", AutoSize = true, ForeColor = Color.FromArgb(0, 100, 0), Margin = new Padding(10, 5, 0, 0) };
        pnlAccount.Controls.Add(_cmbAccount);
        pnlAccount.Controls.Add(_lblAccountBalance);
        pnlHeader.Controls.Add(pnlAccount, 1, 1);
        pnlHeader.SetColumnSpan(pnlAccount, 3);

        // 2. DataGridView for Line Items (Credit entries)
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
            HeaderText = "Particulars (Credit A/c)",
            Name = "ColLedger",
            Width = 350,
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

        var colNarration = new DataGridViewTextBoxColumn
        {
            HeaderText = "Line Narration",
            Name = "ColNarration",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };

        _dgvEntries.Columns.AddRange(colLedger, colBalance, colAmount, colNarration);
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

        pnlSummary.Controls.Add(new Label { Text = "Narration:", AutoSize = true }, 0, 0);
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
            Size = new Size(130, 34),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += async (s, e) => await OnSaveVoucherAsync(closeOnSuccess: true);

        _btnSaveAndNew = new Button
        {
            Text = "Save & New (Alt+S)",
            BackColor = Color.FromArgb(235, 243, 250),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(150, 34),
            Font = new Font("Segoe UI", 9.5F)
        };
        _btnSaveAndNew.FlatAppearance.BorderColor = Color.FromArgb(0, 51, 102);
        _btnSaveAndNew.Click += async (s, e) => await OnSaveVoucherAsync(closeOnSuccess: false);

        _btnNew = new Button
        {
            Text = "Clear (Alt+N)",
            Size = new Size(100, 34),
            Font = new Font("Segoe UI", 9.5F)
        };
        _btnNew.Click += (s, e) => ResetForm();

        _btnPrint = new Button
        {
            Text = "Print (Ctrl+P)",
            Size = new Size(110, 34),
            Font = new Font("Segoe UI", 9.5F)
        };
        _btnPrint.Click += (s, e) => MessageBox.Show("Voucher printing will be available in Phase 20 / Phase 33 reporting module.", "Print Voucher", MessageBoxButtons.OK, MessageBoxIcon.Information);

        _btnCancel = new Button
        {
            Text = "Cancel (Esc)",
            Size = new Size(100, 34),
            Font = new Font("Segoe UI", 9.5F)
        };
        _btnCancel.Click += (s, e) => Close();

        pnlButtons.Controls.Add(_btnSave);
        pnlButtons.Controls.Add(_btnSaveAndNew);
        pnlButtons.Controls.Add(_btnNew);
        pnlButtons.Controls.Add(_btnPrint);
        pnlButtons.Controls.Add(_btnCancel);

        mainLayout.Controls.Add(pnlHeader, 0, 0);
        mainLayout.Controls.Add(_dgvEntries, 0, 1);
        mainLayout.Controls.Add(pnlSummary, 0, 2);
        mainLayout.Controls.Add(pnlButtons, 0, 3);

        Controls.Add(mainLayout);

        KeyDown += async (s, e) =>
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                e.Handled = true;
                await OnSaveVoucherAsync(closeOnSuccess: true);
            }
            else if (e.Alt && e.KeyCode == Keys.S)
            {
                e.Handled = true;
                await OnSaveVoucherAsync(closeOnSuccess: false);
            }
            else if (e.Alt && e.KeyCode == Keys.N)
            {
                e.Handled = true;
                ResetForm();
            }
            else if (e.Control && e.KeyCode == Keys.P)
            {
                e.Handled = true;
                _btnPrint.PerformClick();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                Close();
            }
        };

        Load += async (s, e) => await OnFormLoadAsync();
    }

    private async Task OnFormLoadAsync()
    {
        if (_companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select an active company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Close();
            return;
        }

        try
        {
            var companyId = _companyContext.CurrentCompany.CompanyId;
            var fy = _companyContext.CurrentFinancialYear;

            _dtpVoucherDate.Value = DateTime.Today >= fy.StartDate && DateTime.Today <= fy.EndDate
                ? DateTime.Today
                : fy.StartDate;

            _receiptVoucherType = await _accountingService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Receipt);

            _cashBankLedgers = await _accountingService.GetCashAndBankLedgersAsync(companyId);
            _cmbAccount.DisplayMember = "DisplayName";
            _cmbAccount.ValueMember = "LedgerId";

            var accountItems = _cashBankLedgers.Select(l => new
            {
                LedgerId = l.LedgerId,
                DisplayName = $"{l.LedgerName} ({l.GroupName})"
            }).ToList();

            _cmbAccount.DataSource = accountItems;

            _allLedgers = await _ledgerService.GetLedgersByCompanyAsync(companyId);
            var colLedger = (DataGridViewComboBoxColumn)_dgvEntries.Columns["ColLedger"];
            colLedger.DisplayMember = "DisplayName";
            colLedger.ValueMember = "LedgerId";

            var particularsItems = _allLedgers.Select(l => new
            {
                LedgerId = l.LedgerId,
                DisplayName = $"{l.LedgerName} ({l.GroupName})"
            }).ToList();

            colLedger.DataSource = particularsItems;

            await UpdateVoucherNumberPreviewAsync();
            await OnAccountSelectedAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize receipt voucher: {ex.Message}", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task UpdateVoucherNumberPreviewAsync()
    {
        if (_companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null || _receiptVoucherType == null) return;

        try
        {
            var preview = await _accountingService.GetNextVoucherNumberPreviewAsync(
                _companyContext.CurrentCompany.CompanyId,
                _receiptVoucherType.VoucherTypeId,
                _companyContext.CurrentFinancialYear.FinancialYearId);

            _lblVoucherNumber.Text = preview;
        }
        catch
        {
            _lblVoucherNumber.Text = "RCT-00001";
        }
    }

    private async Task OnAccountSelectedAsync()
    {
        if (_companyContext.CurrentCompany == null || _cmbAccount.SelectedValue == null) return;

        if (_cmbAccount.SelectedValue is int ledgerId)
        {
            try
            {
                var bal = await _accountingService.GetLedgerBalanceAsync(_companyContext.CurrentCompany.CompanyId, ledgerId);
                _lblAccountBalance.Text = $"Cur Bal: {bal.FormattedClosingBalance}";
            }
            catch
            {
                _lblAccountBalance.Text = "Cur Bal: ₹0.00";
            }
        }
    }

    private async Task OnGridCellValueChangedAsync(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || rowIndex >= _dgvEntries.Rows.Count) return;

        var row = _dgvEntries.Rows[rowIndex];

        if (columnIndex == _dgvEntries.Columns["ColLedger"].Index)
        {
            var cellValue = row.Cells["ColLedger"].Value;
            if (cellValue is int ledgerId && _companyContext.CurrentCompany != null)
            {
                try
                {
                    var bal = await _accountingService.GetLedgerBalanceAsync(_companyContext.CurrentCompany.CompanyId, ledgerId);
                    row.Cells["ColBalance"].Value = bal.FormattedClosingBalance;
                }
                catch
                {
                    row.Cells["ColBalance"].Value = "₹0.00";
                }
            }
        }

        if (columnIndex == _dgvEntries.Columns["ColAmount"].Index)
        {
            RecalculateTotals();
        }
    }

    private void RecalculateTotals()
    {
        decimal totalCredit = 0m;

        foreach (DataGridViewRow row in _dgvEntries.Rows)
        {
            if (row.IsNewRow) continue;

            var val = row.Cells["ColAmount"].Value;
            if (val != null && decimal.TryParse(val.ToString(), out var amt) && amt > 0)
            {
                totalCredit += amt;
            }
        }

        _lblTotalAmount.Text = $"Total Amount: ₹{totalCredit:N2}";

        if (totalCredit > 0)
        {
            _lblBalanceStatus.Text = "Voucher Balanced (Dr = Cr)";
            _lblBalanceStatus.ForeColor = Color.FromArgb(16, 185, 129);
            _btnSave.Enabled = true;
            _btnSaveAndNew.Enabled = true;
        }
        else
        {
            _lblBalanceStatus.Text = "Enter amounts to balance voucher";
            _lblBalanceStatus.ForeColor = Color.FromArgb(220, 38, 38);
        }
    }

    private async Task OnSaveVoucherAsync(bool closeOnSuccess)
    {
        if (_companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null || _receiptVoucherType == null)
        {
            MessageBox.Show("Company and Financial Year must be active.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_cmbAccount.SelectedValue == null || !(_cmbAccount.SelectedValue is int debitLedgerId))
        {
            MessageBox.Show("Please select a valid Cash/Bank receiving account.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _cmbAccount.Focus();
            return;
        }

        // Collect Credit line entries from DataGridView
        var entries = new List<VoucherEntryDto>();
        decimal totalCredit = 0m;

        foreach (DataGridViewRow row in _dgvEntries.Rows)
        {
            if (row.IsNewRow) continue;

            var ledgerVal = row.Cells["ColLedger"].Value;
            var amountVal = row.Cells["ColAmount"].Value;
            var narrationVal = row.Cells["ColNarration"].Value?.ToString() ?? string.Empty;

            if (ledgerVal is int creditLedgerId && amountVal != null && decimal.TryParse(amountVal.ToString(), out var amount) && amount > 0)
            {
                entries.Add(new VoucherEntryDto
                {
                    LedgerId = creditLedgerId,
                    Debit = 0m,
                    Credit = amount,
                    Narration = narrationVal
                });
                totalCredit += amount;
            }
        }

        if (entries.Count == 0 || totalCredit <= 0)
        {
            MessageBox.Show("Please enter at least one credit line item with an amount greater than 0.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Add matching Debit entry for the receiving Cash/Bank account
        entries.Add(new VoucherEntryDto
        {
            LedgerId = debitLedgerId,
            Debit = totalCredit,
            Credit = 0m,
            Narration = _txtNarration.Text.Trim()
        });

        var createDto = new VoucherCreateDto
        {
            FinancialYearId = _companyContext.CurrentFinancialYear.FinancialYearId,
            VoucherTypeId = _receiptVoucherType.VoucherTypeId,
            VoucherDate = _dtpVoucherDate.Value,
            Narration = _txtNarration.Text.Trim(),
            Entries = entries
        };

        try
        {
            _btnSave.Enabled = false;
            _btnSaveAndNew.Enabled = false;

            var saved = await _accountingService.SaveVoucherAsync(_companyContext.CurrentCompany.CompanyId, createDto);

            MessageBox.Show(
                $"Receipt voucher {saved.VoucherNumber} of ₹{totalCredit:N2} saved successfully!",
                "Voucher Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            if (closeOnSuccess)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                ResetForm();
                await UpdateVoucherNumberPreviewAsync();
                await OnAccountSelectedAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save voucher: {ex.Message}", "Accounting Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnSave.Enabled = true;
            _btnSaveAndNew.Enabled = true;
        }
    }

    private void ResetForm()
    {
        _dgvEntries.Rows.Clear();
        _txtNarration.Clear();
        _lblTotalAmount.Text = "Total Amount: ₹0.00";
        _lblBalanceStatus.Text = "Enter amounts to balance voucher";
        _lblBalanceStatus.ForeColor = Color.FromArgb(220, 38, 38);
    }
}
