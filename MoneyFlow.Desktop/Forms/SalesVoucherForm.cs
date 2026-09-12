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

public class SalesVoucherForm : Form
{
    private readonly IAccountingService _accountingService;
    private readonly ICompanyContext _companyContext;

    private VoucherType? _salesVoucherType;
    private IReadOnlyList<LedgerSummaryDto> _partyLedgers = new List<LedgerSummaryDto>();
    private IReadOnlyList<LedgerSummaryDto> _salesLedgers = new List<LedgerSummaryDto>();

    // UI Controls
    private Label _lblVoucherNumber = null!;
    private DateTimePicker _dtpVoucherDate = null!;
    private TextBox _txtRefNo = null!;
    private ComboBox _cmbParty = null!;
    private Label _lblPartyBalance = null!;
    private ComboBox _cmbSalesLedger = null!;
    private Label _lblSalesBalance = null!;
    private DataGridView _dgvItems = null!;
    private TextBox _txtNarration = null!;
    private Label _lblSubtotal = null!;
    private Label _lblDiscount = null!;
    private Label _lblNetTotal = null!;
    private Label _lblBalanceStatus = null!;
    private Button _btnSave = null!;
    private Button _btnSaveAndNew = null!;
    private Button _btnNew = null!;
    private Button _btnPrint = null!;
    private Button _btnCancel = null!;
    private bool _isInitializing;

    public SalesVoucherForm(
        IAccountingService accountingService,
        ICompanyContext companyContext)
    {
        _accountingService = accountingService;
        _companyContext = companyContext;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Sales Voucher (F8) — Invoice Entry (No GST)";
        Size = new Size(1020, 700);
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
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 110)); // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Items Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));  // Narration & Summary
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Buttons

        // 1. Header Panel
        var pnlHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 2,
            BackColor = Color.FromArgb(245, 248, 252),
            Padding = new Padding(10)
        };
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        // Row 0: Voucher No, Date, Ref No
        pnlHeader.Controls.Add(new Label { Text = "Invoice No:", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 0);
        _lblVoucherNumber = new Label { Text = "SLS-00001", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.FromArgb(0, 51, 102), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        pnlHeader.Controls.Add(_lblVoucherNumber, 1, 0);

        pnlHeader.Controls.Add(new Label { Text = "Date:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
        _dtpVoucherDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 135 };
        pnlHeader.Controls.Add(_dtpVoucherDate, 3, 0);

        pnlHeader.Controls.Add(new Label { Text = "Ref / Inv No:", AutoSize = true, Anchor = AnchorStyles.Left }, 4, 0);
        _txtRefNo = new TextBox { Width = 140, Font = new Font("Segoe UI", 9.5F) };
        pnlHeader.Controls.Add(_txtRefNo, 5, 0);

        // Row 1: Party A/c (Debit) & Sales Ledger (Credit)
        pnlHeader.Controls.Add(new Label { Text = "Party A/c (Dr):", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 1);
        var pnlParty = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _cmbParty = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Font = new Font("Segoe UI", 9.5F) };
        _cmbParty.SelectedIndexChanged += async (s, e) => await OnPartySelectedAsync();
        _lblPartyBalance = new Label { Text = "Cur Bal: ₹0.00", AutoSize = true, ForeColor = Color.FromArgb(0, 100, 0), Margin = new Padding(10, 5, 0, 0) };
        pnlParty.Controls.Add(_cmbParty);
        pnlParty.Controls.Add(_lblPartyBalance);
        pnlHeader.Controls.Add(pnlParty, 1, 1);

        pnlHeader.Controls.Add(new Label { Text = "Sales A/c:", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 2, 1);
        var pnlSales = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _cmbSalesLedger = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Font = new Font("Segoe UI", 9.5F) };
        _cmbSalesLedger.SelectedIndexChanged += async (s, e) => await OnSalesLedgerSelectedAsync();
        _lblSalesBalance = new Label { Text = "Cur Bal: ₹0.00", AutoSize = true, ForeColor = Color.FromArgb(0, 100, 0), Margin = new Padding(10, 5, 0, 0) };
        pnlSales.Controls.Add(_cmbSalesLedger);
        pnlSales.Controls.Add(_lblSalesBalance);
        pnlHeader.Controls.Add(pnlSales, 3, 1);
        pnlHeader.SetColumnSpan(pnlSales, 3);

        // 2. DataGridView for Line Items
        _dgvItems = new DataGridView
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

        var colDesc = new DataGridViewTextBoxColumn
        {
            HeaderText = "Item / Service Description",
            Name = "ColDesc",
            Width = 320
        };

        var colQty = new DataGridViewTextBoxColumn
        {
            HeaderText = "Quantity",
            Name = "ColQty",
            Width = 90,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        };

        var colRate = new DataGridViewTextBoxColumn
        {
            HeaderText = "Rate (₹)",
            Name = "ColRate",
            Width = 110,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        };

        var colGross = new DataGridViewTextBoxColumn
        {
            HeaderText = "Gross (₹)",
            Name = "ColGross",
            Width = 110,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", ForeColor = Color.DimGray }
        };

        var colDiscount = new DataGridViewTextBoxColumn
        {
            HeaderText = "Discount (₹)",
            Name = "ColDiscount",
            Width = 100,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        };

        var colAmount = new DataGridViewTextBoxColumn
        {
            HeaderText = "Amount (₹)",
            Name = "ColAmount",
            Width = 120,
            ReadOnly = true,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }
        };

        var colNarration = new DataGridViewTextBoxColumn
        {
            HeaderText = "Line Narration",
            Name = "ColNarration",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };

        _dgvItems.Columns.AddRange(colDesc, colQty, colRate, colGross, colDiscount, colAmount, colNarration);
        _dgvItems.CellValueChanged += (s, e) => OnGridCellValueChanged(e.RowIndex, e.ColumnIndex);
        _dgvItems.RowsRemoved += (s, e) => RecalculateTotals();

        // 3. Narration and Summary Panel
        var pnlSummary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(5)
        };
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        pnlSummary.Controls.Add(new Label { Text = "Invoice Narration:", AutoSize = true }, 0, 0);

        var pnlTotals = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };
        _lblNetTotal = new Label { Text = "Total: ₹0.00", Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.FromArgb(0, 51, 102), AutoSize = true, Margin = new Padding(15, 0, 0, 0) };
        _lblDiscount = new Label { Text = "Discount: ₹0.00", Font = new Font("Segoe UI", 9.5F, FontStyle.Bold), ForeColor = Color.DarkRed, AutoSize = true, Margin = new Padding(15, 0, 0, 0) };
        _lblSubtotal = new Label { Text = "Subtotal: ₹0.00", Font = new Font("Segoe UI", 9.5F), ForeColor = Color.DimGray, AutoSize = true };
        pnlTotals.Controls.Add(_lblNetTotal);
        pnlTotals.Controls.Add(_lblDiscount);
        pnlTotals.Controls.Add(_lblSubtotal);
        pnlSummary.Controls.Add(pnlTotals, 1, 0);

        _txtNarration = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F), Multiline = true, Height = 40 };
        pnlSummary.Controls.Add(_txtNarration, 0, 1);
        pnlSummary.SetRowSpan(_txtNarration, 2);

        _lblBalanceStatus = new Label
        {
            Text = "Enter line items",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.DarkOrange,
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
        _btnPrint.Click += (s, e) => MessageBox.Show("Invoice print preview will be configured with RDLC reports in Phase 33.", "Print Invoice", MessageBoxButtons.OK, MessageBoxIcon.Information);

        _btnCancel = new Button
        {
            Text = "Cancel (Esc)",
            Height = 34,
            Width = 100
        };
        _btnCancel.Click += (s, e) => Close();

        pnlButtons.Controls.AddRange(new Control[] { _btnSave, _btnSaveAndNew, _btnNew, _btnPrint, _btnCancel });

        mainLayout.Controls.Add(pnlHeader, 0, 0);
        mainLayout.Controls.Add(_dgvItems, 0, 1);
        mainLayout.Controls.Add(pnlSummary, 0, 2);
        mainLayout.Controls.Add(pnlButtons, 0, 3);

        Controls.Add(mainLayout);

        Load += async (s, e) => await InitializeFormDataAsync();
    }

    private async Task InitializeFormDataAsync()
    {
        _isInitializing = true;
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

            _dtpVoucherDate.MinDate = fy.StartDate;
            _dtpVoucherDate.MaxDate = fy.EndDate;
            _dtpVoucherDate.Value = DateTime.Today >= fy.StartDate && DateTime.Today <= fy.EndDate ? DateTime.Today : fy.StartDate;

            _salesVoucherType = await _accountingService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);

            await RefreshVoucherNumberPreviewAsync();

            _partyLedgers = await _accountingService.GetCustomerPartyLedgersAsync(company.CompanyId);
            _cmbParty.DisplayMember = "LedgerName";
            _cmbParty.ValueMember = "LedgerId";
            _cmbParty.DataSource = _partyLedgers.ToList();

            _salesLedgers = await _accountingService.GetSalesLedgersAsync(company.CompanyId);
            _cmbSalesLedger.DisplayMember = "LedgerName";
            _cmbSalesLedger.ValueMember = "LedgerId";
            _cmbSalesLedger.DataSource = _salesLedgers.ToList();

            // Pre-add empty row with default Qty = 1, Discount = 0
            _dgvItems.Rows.Clear();
            var rowIdx = _dgvItems.Rows.Add();
            _dgvItems.Rows[rowIdx].Cells["ColQty"].Value = 1.00m;
            _dgvItems.Rows[rowIdx].Cells["ColDiscount"].Value = 0.00m;

            RecalculateTotals();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize Sales Voucher: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isInitializing = false;
            UseWaitCursor = false;
        }

        await OnPartySelectedAsync();
        await OnSalesLedgerSelectedAsync();
    }

    private async Task RefreshVoucherNumberPreviewAsync()
    {
        if (_salesVoucherType == null || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
            return;

        var nextNumber = await _accountingService.GetNextVoucherNumberPreviewAsync(
            _companyContext.CurrentCompany.CompanyId,
            _salesVoucherType.VoucherTypeId,
            _companyContext.CurrentFinancialYear.FinancialYearId);

        _lblVoucherNumber.Text = nextNumber;
    }

    private async Task OnPartySelectedAsync()
    {
        if (_isInitializing) return;
        if (_cmbParty.SelectedItem is LedgerSummaryDto selected && _companyContext.CurrentCompany != null)
        {
            var balance = await _accountingService.GetLedgerBalanceAsync(_companyContext.CurrentCompany.CompanyId, selected.LedgerId);
            _lblPartyBalance.Text = $"Cur Bal: {balance.ClosingBalanceDisplay}";
            _lblPartyBalance.ForeColor = balance.ClosingBalance >= 0 ? Color.FromArgb(0, 100, 0) : Color.DarkRed;
        }
        else
        {
            _lblPartyBalance.Text = "Cur Bal: ₹0.00";
        }
    }

    private async Task OnSalesLedgerSelectedAsync()
    {
        if (_isInitializing) return;
        if (_cmbSalesLedger.SelectedItem is LedgerSummaryDto selected && _companyContext.CurrentCompany != null)
        {
            var balance = await _accountingService.GetLedgerBalanceAsync(_companyContext.CurrentCompany.CompanyId, selected.LedgerId);
            _lblSalesBalance.Text = $"Cur Bal: {balance.ClosingBalanceDisplay}";
        }
        else
        {
            _lblSalesBalance.Text = "Cur Bal: ₹0.00";
        }
    }

    private void OnGridCellValueChanged(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || rowIndex >= _dgvItems.Rows.Count) return;

        var row = _dgvItems.Rows[rowIndex];

        // Recalculate line totals if Qty, Rate, or Discount changed
        if (columnIndex == _dgvItems.Columns["ColQty"].Index ||
            columnIndex == _dgvItems.Columns["ColRate"].Index ||
            columnIndex == _dgvItems.Columns["ColDiscount"].Index)
        {
            decimal.TryParse(row.Cells["ColQty"].Value?.ToString(), out var qty);
            decimal.TryParse(row.Cells["ColRate"].Value?.ToString(), out var rate);
            decimal.TryParse(row.Cells["ColDiscount"].Value?.ToString(), out var discount);

            decimal gross = qty * rate;
            decimal amount = Math.Max(0, gross - discount);

            row.Cells["ColGross"].Value = gross;
            row.Cells["ColAmount"].Value = amount;

            RecalculateTotals();
        }
    }

    private void RecalculateTotals()
    {
        decimal subtotal = 0;
        decimal totalDiscount = 0;
        decimal netTotal = 0;

        foreach (DataGridViewRow row in _dgvItems.Rows)
        {
            if (row.IsNewRow) continue;

            if (decimal.TryParse(row.Cells["ColGross"].Value?.ToString(), out var gross))
                subtotal += gross;

            if (decimal.TryParse(row.Cells["ColDiscount"].Value?.ToString(), out var disc))
                totalDiscount += disc;

            if (decimal.TryParse(row.Cells["ColAmount"].Value?.ToString(), out var amt))
                netTotal += amt;
        }

        _lblSubtotal.Text = $"Subtotal: ₹{subtotal:N2}";
        _lblDiscount.Text = $"Discount: ₹{totalDiscount:N2}";
        _lblNetTotal.Text = $"Total: ₹{netTotal:N2}";

        if (netTotal > 0 && _cmbParty.SelectedIndex >= 0 && _cmbSalesLedger.SelectedIndex >= 0)
        {
            _lblBalanceStatus.Text = "Invoice Balanced (Dr = Cr)";
            _lblBalanceStatus.ForeColor = Color.FromArgb(16, 185, 129);
            _btnSave.Enabled = true;
            _btnSaveAndNew.Enabled = true;
        }
        else
        {
            _lblBalanceStatus.Text = netTotal <= 0 ? "Enter line items" : "Select Party and Sales accounts";
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

            if (_salesVoucherType == null)
            {
                MessageBox.Show("Sales voucher type not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_cmbParty.SelectedValue is not int partyLedgerId || partyLedgerId <= 0)
            {
                MessageBox.Show("Please select a Customer / Party account to debit.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _cmbParty.Focus();
                return;
            }

            if (_cmbSalesLedger.SelectedValue is not int salesLedgerId || salesLedgerId <= 0)
            {
                MessageBox.Show("Please select a Sales account to credit.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _cmbSalesLedger.Focus();
                return;
            }

            decimal netTotal = 0;
            var lineSummaries = new List<string>();

            for (int i = 0; i < _dgvItems.Rows.Count; i++)
            {
                var row = _dgvItems.Rows[i];
                if (row.IsNewRow) continue;

                var desc = row.Cells["ColDesc"].Value?.ToString()?.Trim();
                decimal.TryParse(row.Cells["ColQty"].Value?.ToString(), out var qty);
                decimal.TryParse(row.Cells["ColRate"].Value?.ToString(), out var rate);
                decimal.TryParse(row.Cells["ColDiscount"].Value?.ToString(), out var disc);
                decimal.TryParse(row.Cells["ColAmount"].Value?.ToString(), out var amt);

                if (amt > 0)
                {
                    netTotal += amt;
                    var itemLabel = !string.IsNullOrEmpty(desc) ? desc : $"Item {i + 1}";
                    lineSummaries.Add($"{itemLabel} (Qty: {qty:N2} @ ₹{rate:N2}{(disc > 0 ? $", Disc: ₹{disc:N2}" : "")})");
                }
            }

            if (netTotal <= 0)
            {
                MessageBox.Show("Please enter at least one line item with a valid amount.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var invoiceNarration = _txtNarration.Text.Trim();
            if (string.IsNullOrEmpty(invoiceNarration) && lineSummaries.Count > 0)
            {
                invoiceNarration = string.Join("; ", lineSummaries);
            }

            // Balanced Double Entry:
            // 1. Debit: Customer Party Account for Net Total
            // 2. Credit: Sales Ledger for Net Total
            var entries = new List<VoucherEntryDto>
            {
                new()
                {
                    LedgerId = partyLedgerId,
                    Debit = netTotal,
                    Credit = 0,
                    Narration = $"Sales to {((LedgerSummaryDto)_cmbParty.SelectedItem!).LedgerName}"
                },
                new()
                {
                    LedgerId = salesLedgerId,
                    Debit = 0,
                    Credit = netTotal,
                    Narration = invoiceNarration
                }
            };

            var voucherDto = new VoucherCreateDto
            {
                FinancialYearId = fy.FinancialYearId,
                VoucherTypeId = _salesVoucherType.VoucherTypeId,
                VoucherDate = _dtpVoucherDate.Value.Date,
                ReferenceNumber = _txtRefNo.Text.Trim(),
                Narration = invoiceNarration,
                Entries = entries
            };

            UseWaitCursor = true;
            _btnSave.Enabled = false;
            _btnSaveAndNew.Enabled = false;

            var savedVoucher = await _accountingService.SaveVoucherAsync(company.CompanyId, voucherDto);

            MessageBox.Show(
                $"Sales Invoice '{savedVoucher.VoucherNumber}' of ₹{netTotal:N2} saved successfully.",
                "Sales Invoice Saved",
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
                await OnPartySelectedAsync();
                await OnSalesLedgerSelectedAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save Sales Invoice:\n{ex.Message}", "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        _dgvItems.Rows.Clear();
        var rowIdx = _dgvItems.Rows.Add();
        _dgvItems.Rows[rowIdx].Cells["ColQty"].Value = 1.00m;
        _dgvItems.Rows[rowIdx].Cells["ColDiscount"].Value = 0.00m;

        _txtRefNo.Clear();
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
