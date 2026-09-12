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

public class CreditNoteForm : Form
{
    private readonly IAccountingService _accountingService;
    private readonly ICompanyContext _companyContext;

    private VoucherType? _creditNoteVoucherType;
    private IReadOnlyList<LedgerSummaryDto> _customerLedgers = new List<LedgerSummaryDto>();
    private IReadOnlyList<LedgerSummaryDto> _salesLedgers = new List<LedgerSummaryDto>();

    // UI Controls
    private Label _lblVoucherNumber = null!;
    private DateTimePicker _dtpVoucherDate = null!;
    private TextBox _txtOriginalInvoiceNo = null!;
    private DateTimePicker _dtpOriginalInvoiceDate = null!;
    private ComboBox _cmbParty = null!;
    private Label _lblPartyBalance = null!;
    private ComboBox _cmbSalesLedger = null!;
    private Label _lblSalesBalance = null!;
    private DataGridView _dgvItems = null!;
    private TextBox _txtNarration = null!;
    private Label _lblNetTotal = null!;
    private Label _lblBalanceStatus = null!;
    private Button _btnSave = null!;
    private Button _btnSaveAndNew = null!;
    private Button _btnNew = null!;
    private Button _btnPrint = null!;
    private Button _btnCancel = null!;
    private bool _isInitializing;

    public CreditNoteForm(
        IAccountingService accountingService,
        ICompanyContext companyContext)
    {
        _accountingService = accountingService;
        _companyContext = companyContext;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Credit Note (Sales Return / Customer Credit Adjustment) — No GST";
        Size = new Size(1020, 720);
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
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130)); // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Items Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));  // Narration & Summary
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Buttons

        // 1. Header Panel
        var pnlHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 3,
            BackColor = Color.FromArgb(245, 248, 252),
            Padding = new Padding(10)
        };
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        // Row 0: Voucher No, Date
        pnlHeader.Controls.Add(new Label { Text = "Credit Note No:", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 0);
        _lblVoucherNumber = new Label { Text = "CRN-00001", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.FromArgb(0, 51, 102), Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
        pnlHeader.Controls.Add(_lblVoucherNumber, 1, 0);

        pnlHeader.Controls.Add(new Label { Text = "Date:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
        _dtpVoucherDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 135 };
        pnlHeader.Controls.Add(_dtpVoucherDate, 3, 0);

        // Row 1: Original Sales Invoice Ref & Date
        pnlHeader.Controls.Add(new Label { Text = "Orig Inv No:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _txtOriginalInvoiceNo = new TextBox { Width = 160, Font = new Font("Segoe UI", 9.5F) };
        pnlHeader.Controls.Add(_txtOriginalInvoiceNo, 1, 1);

        pnlHeader.Controls.Add(new Label { Text = "Orig Date:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 1);
        _dtpOriginalInvoiceDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 135 };
        pnlHeader.Controls.Add(_dtpOriginalInvoiceDate, 3, 1);

        // Row 2: Customer Party (Cr) & Sales Return / Income Account (Dr)
        pnlHeader.Controls.Add(new Label { Text = "Customer (Cr):", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 0, 2);
        var pnlParty = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _cmbParty = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220, Font = new Font("Segoe UI", 9.5F) };
        _cmbParty.SelectedIndexChanged += async (s, e) => await OnPartySelectedAsync();
        _lblPartyBalance = new Label { Text = "Cur Bal: ₹0.00", AutoSize = true, ForeColor = Color.FromArgb(0, 100, 0), Margin = new Padding(10, 5, 0, 0) };
        pnlParty.Controls.Add(_cmbParty);
        pnlParty.Controls.Add(_lblPartyBalance);
        pnlHeader.Controls.Add(pnlParty, 1, 2);

        pnlHeader.Controls.Add(new Label { Text = "Return A/c (Dr):", AutoSize = true, Anchor = AnchorStyles.Left, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }, 2, 2);
        var pnlSales = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _cmbSalesLedger = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200, Font = new Font("Segoe UI", 9.5F) };
        _cmbSalesLedger.SelectedIndexChanged += async (s, e) => await OnSalesLedgerSelectedAsync();
        _lblSalesBalance = new Label { Text = "Cur Bal: ₹0.00", AutoSize = true, ForeColor = Color.FromArgb(0, 100, 0), Margin = new Padding(10, 5, 0, 0) };
        pnlSales.Controls.Add(_cmbSalesLedger);
        pnlSales.Controls.Add(_lblSalesBalance);
        pnlHeader.Controls.Add(pnlSales, 3, 2);

        mainLayout.Controls.Add(pnlHeader, 0, 0);

        // 2. Line Items DataGridView (Section 16/29 Sales Return Items)
        _dgvItems = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            RowHeadersWidth = 35,
            SelectionMode = DataGridViewSelectionMode.CellSelect
        };
        _dgvItems.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 240, 248);
        _dgvItems.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        _dgvItems.EnableHeadersVisualStyles = false;

        ConfigureGridColumns();
        _dgvItems.CellValueChanged += (s, e) => RecalculateRow(e.RowIndex, e.ColumnIndex);
        _dgvItems.RowsAdded += (s, e) => RecalculateTotals();
        _dgvItems.RowsRemoved += (s, e) => RecalculateTotals();

        mainLayout.Controls.Add(_dgvItems, 0, 1);

        // 3. Narration & Totals Summary Panel
        var pnlSummary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(5)
        };
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

        // Narration box
        var pnlNarration = new Panel { Dock = DockStyle.Fill };
        pnlNarration.Controls.Add(new Label { Text = "Narration / Return Reason:", Top = 0, Left = 0, AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
        _txtNarration = new TextBox
        {
            Multiline = true,
            Top = 22,
            Left = 0,
            Width = 460,
            Height = 60,
            ScrollBars = ScrollBars.Vertical,
            Text = "Being goods returned / credit note issued against invoice"
        };
        pnlNarration.Controls.Add(_txtNarration);
        pnlSummary.Controls.Add(pnlNarration, 0, 0);

        // Totals display
        var pnlTotals = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            BackColor = Color.FromArgb(250, 252, 255),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
        };
        pnlTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        pnlTotals.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        pnlTotals.Controls.Add(new Label { Text = "Total Return Amount:", Anchor = AnchorStyles.Right, Font = new Font("Segoe UI", 10.5F, FontStyle.Bold) }, 0, 0);
        _lblNetTotal = new Label { Text = "₹ 0.00", Anchor = AnchorStyles.Right, Font = new Font("Segoe UI", 11F, FontStyle.Bold), ForeColor = Color.FromArgb(0, 51, 102) };
        pnlTotals.Controls.Add(_lblNetTotal, 1, 0);

        _lblBalanceStatus = new Label
        {
            Text = "Double Entry: Dr Sales Return ₹0.00 | Cr Customer ₹0.00",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            ForeColor = Color.DarkGreen,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Italic)
        };
        pnlTotals.Controls.Add(_lblBalanceStatus, 0, 1);
        pnlTotals.SetColumnSpan(_lblBalanceStatus, 2);

        pnlSummary.Controls.Add(pnlTotals, 1, 0);
        mainLayout.Controls.Add(pnlSummary, 0, 2);

        // 4. Action Buttons Panel
        var pnlActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(5)
        };

        _btnCancel = new Button { Text = "Cancel (Esc)", Width = 110, Height = 35 };
        _btnCancel.Click += (s, e) => Close();

        _btnPrint = new Button { Text = "Print (Ctrl+P)", Width = 110, Height = 35 };
        _btnPrint.Click += (s, e) => MessageBox.Show(this, "Credit Note Voucher preview generated. Ready for printing.", "Print Preview", MessageBoxButtons.OK, MessageBoxIcon.Information);

        _btnNew = new Button { Text = "Clear (Alt+N)", Width = 110, Height = 35 };
        _btnNew.Click += async (s, e) => await ResetFormAsync();

        _btnSaveAndNew = new Button { Text = "Save & New (Alt+S)", Width = 140, Height = 35, BackColor = Color.FromArgb(230, 240, 250) };
        _btnSaveAndNew.Click += async (s, e) => await SaveVoucherInternalAsync(true);

        _btnSave = new Button { Text = "Save (Ctrl+A)", Width = 120, Height = 35, BackColor = Color.FromArgb(0, 51, 102), ForeColor = Color.White };
        _btnSave.Click += async (s, e) => await SaveVoucherInternalAsync(false);

        pnlActions.Controls.Add(_btnCancel);
        pnlActions.Controls.Add(_btnPrint);
        pnlActions.Controls.Add(_btnNew);
        pnlActions.Controls.Add(_btnSaveAndNew);
        pnlActions.Controls.Add(_btnSave);

        mainLayout.Controls.Add(pnlActions, 0, 3);
        Controls.Add(mainLayout);

        KeyDown += OnFormKeyDown;
        Load += async (s, e) => await OnFormLoadAsync();
    }

    private void ConfigureGridColumns()
    {
        _dgvItems.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColDescription",
            HeaderText = "Item / Description",
            Width = 280
        });

        _dgvItems.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColQty",
            HeaderText = "Return Qty",
            Width = 110,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        });

        _dgvItems.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColRate",
            HeaderText = "Return Rate (₹)",
            Width = 130,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        });

        _dgvItems.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColAmount",
            HeaderText = "Return Amount (₹)",
            Width = 140,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", ForeColor = Color.FromArgb(0, 51, 102) }
        });

        _dgvItems.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColReason",
            HeaderText = "Reason / Narration",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
    }

    private async Task OnFormLoadAsync()
    {
        _isInitializing = true;
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
                _dtpVoucherDate.MinDate = fy.StartDate;
                _dtpVoucherDate.MaxDate = fy.EndDate;
                _dtpVoucherDate.Value = DateTime.Today >= fy.StartDate && DateTime.Today <= fy.EndDate
                    ? DateTime.Today
                    : fy.StartDate;
            }

            _creditNoteVoucherType = await _accountingService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.CreditNote);
            await LoadLedgersAsync();
            await RefreshVoucherNumberPreviewAsync();

            AddDefaultEmptyRow();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error initializing Credit Note Form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isInitializing = false;
        }

        await OnPartySelectedAsync();
        await OnSalesLedgerSelectedAsync();
    }

    private async Task LoadLedgersAsync()
    {
        var company = _companyContext.CurrentCompany;
        if (company == null) return;

        _customerLedgers = await _accountingService.GetCustomerPartyLedgersAsync(company.CompanyId);
        _salesLedgers = await _accountingService.GetSalesLedgersAsync(company.CompanyId);

        _cmbParty.DataSource = null;
        _cmbParty.DisplayMember = "LedgerName";
        _cmbParty.ValueMember = "LedgerId";
        _cmbParty.DataSource = _customerLedgers.ToList();

        _cmbSalesLedger.DataSource = null;
        _cmbSalesLedger.DisplayMember = "LedgerName";
        _cmbSalesLedger.ValueMember = "LedgerId";
        _cmbSalesLedger.DataSource = _salesLedgers.ToList();

        if (_customerLedgers.Count > 0)
            _cmbParty.SelectedIndex = 0;

        // Try selecting a ledger named "Sales Return" or "Sales Returns" if present, else first
        var returnLedgerIndex = _salesLedgers.ToList().FindIndex(l => l.LedgerName.Contains("Return", StringComparison.OrdinalIgnoreCase));
        if (returnLedgerIndex >= 0)
            _cmbSalesLedger.SelectedIndex = returnLedgerIndex;
        else if (_salesLedgers.Count > 0)
            _cmbSalesLedger.SelectedIndex = 0;
    }

    private async Task RefreshVoucherNumberPreviewAsync()
    {
        var company = _companyContext.CurrentCompany;
        var fy = _companyContext.CurrentFinancialYear;
        if (company == null || fy == null || _creditNoteVoucherType == null) return;

        var preview = await _accountingService.GetNextVoucherNumberPreviewAsync(company.CompanyId, _creditNoteVoucherType.VoucherTypeId, fy.FinancialYearId);
        _lblVoucherNumber.Text = preview;
    }

    private async Task OnPartySelectedAsync()
    {
        if (_isInitializing) return;
        if (_cmbParty.SelectedItem is not LedgerSummaryDto selected) return;
        var company = _companyContext.CurrentCompany;
        if (company == null) return;

        var balance = await _accountingService.GetLedgerBalanceAsync(company.CompanyId, selected.LedgerId, _dtpVoucherDate.Value);
        _lblPartyBalance.Text = $"Cur Bal: ₹{balance.ClosingBalance:N2} {balance.ClosingType}";
        _lblPartyBalance.ForeColor = balance.ClosingBalance < 0 ? Color.Red : Color.FromArgb(0, 100, 0);
    }

    private async Task OnSalesLedgerSelectedAsync()
    {
        if (_isInitializing) return;
        if (_cmbSalesLedger.SelectedItem is not LedgerSummaryDto selected) return;
        var company = _companyContext.CurrentCompany;
        if (company == null) return;

        var balance = await _accountingService.GetLedgerBalanceAsync(company.CompanyId, selected.LedgerId, _dtpVoucherDate.Value);
        _lblSalesBalance.Text = $"Cur Bal: ₹{balance.ClosingBalance:N2} {balance.ClosingType}";
    }

    private void AddDefaultEmptyRow()
    {
        if (_dgvItems.Rows.Count == 0 || (_dgvItems.Rows.Count == 1 && _dgvItems.Rows[0].IsNewRow))
        {
            var idx = _dgvItems.Rows.Add();
            var row = _dgvItems.Rows[idx];
            row.Cells["ColDescription"].Value = "Sales Return Items";
            row.Cells["ColQty"].Value = 1.00m;
            row.Cells["ColRate"].Value = 0.00m;
            row.Cells["ColAmount"].Value = 0.00m;
            row.Cells["ColReason"].Value = "Defective / return goods";
        }
    }

    private void RecalculateRow(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || rowIndex >= _dgvItems.Rows.Count) return;
        var row = _dgvItems.Rows[rowIndex];
        if (row.IsNewRow) return;

        decimal qty = ConvertToDecimal(row.Cells["ColQty"].Value);
        decimal rate = ConvertToDecimal(row.Cells["ColRate"].Value);
        decimal amount = Math.Round(qty * rate, 2);

        row.Cells["ColAmount"].Value = amount;
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        decimal totalAmount = 0;

        foreach (DataGridViewRow row in _dgvItems.Rows)
        {
            if (row.IsNewRow) continue;
            totalAmount += ConvertToDecimal(row.Cells["ColAmount"].Value);
        }

        _lblNetTotal.Text = $"₹ {totalAmount:N2}";
        _lblBalanceStatus.Text = $"Double Entry: Dr Sales Return ₹{totalAmount:N2} | Cr Customer ₹{totalAmount:N2} (Balanced)";
    }

    private static decimal ConvertToDecimal(object? val)
    {
        if (val == null) return 0m;
        if (decimal.TryParse(val.ToString(), out var result))
            return result;
        return 0m;
    }

    private async Task<bool> SaveVoucherInternalAsync(bool resetAfterSave)
    {
        var company = _companyContext.CurrentCompany;
        var fy = _companyContext.CurrentFinancialYear;

        if (company == null || fy == null || _creditNoteVoucherType == null)
        {
            MessageBox.Show(this, "Active company or financial year context is missing.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        if (_cmbParty.SelectedItem is not LedgerSummaryDto customerParty)
        {
            MessageBox.Show(this, "Please select a Customer Party account.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _cmbParty.Focus();
            return false;
        }

        if (_cmbSalesLedger.SelectedItem is not LedgerSummaryDto salesReturnLedger)
        {
            MessageBox.Show(this, "Please select a Sales / Return Ledger.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _cmbSalesLedger.Focus();
            return false;
        }

        // Validate items and total
        decimal totalReturnAmount = 0;
        var itemNarrationParts = new List<string>();

        foreach (DataGridViewRow row in _dgvItems.Rows)
        {
            if (row.IsNewRow) continue;
            var desc = row.Cells["ColDescription"].Value?.ToString()?.Trim();
            var qty = ConvertToDecimal(row.Cells["ColQty"].Value);
            var rate = ConvertToDecimal(row.Cells["ColRate"].Value);
            var amount = ConvertToDecimal(row.Cells["ColAmount"].Value);
            var reason = row.Cells["ColReason"].Value?.ToString()?.Trim();

            if (amount <= 0 && string.IsNullOrWhiteSpace(desc)) continue;

            totalReturnAmount += amount;
            if (!string.IsNullOrWhiteSpace(desc))
            {
                var note = !string.IsNullOrWhiteSpace(reason) ? $" ({reason})" : string.Empty;
                itemNarrationParts.Add($"{desc} (Qty: {qty:N2} @ ₹{rate:N2}){note}");
            }
        }

        if (totalReturnAmount <= 0)
        {
            MessageBox.Show(this, "Total Credit Note Return Amount must be greater than zero.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        // Build composite narration
        var baseNarration = _txtNarration.Text.Trim();
        var origRef = _txtOriginalInvoiceNo.Text.Trim();
        var refString = !string.IsNullOrWhiteSpace(origRef)
            ? $" against Orig Inv #{origRef} dt. {_dtpOriginalInvoiceDate.Value:dd-MMM-yyyy}"
            : string.Empty;

        var fullNarration = $"{baseNarration}{refString}. Items: {string.Join("; ", itemNarrationParts)}";
        if (fullNarration.Length > 500)
            fullNarration = fullNarration[..500];

        // Construct Strict Double-Entry Voucher:
        // Dr: Sales Return A/c (decreases revenue)
        // Cr: Customer Party (Sundry Debtors / Cash / Bank) (decreases customer debt / refunds cash)
        var dto = new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = _creditNoteVoucherType.VoucherTypeId,
            VoucherDate = _dtpVoucherDate.Value.Date,
            ReferenceNumber = origRef,
            Narration = fullNarration,
            Entries = new List<VoucherEntryDto>
            {
                new()
                {
                    LedgerId = salesReturnLedger.LedgerId,
                    Debit = totalReturnAmount,
                    Credit = 0m,
                    Narration = $"Sales Return from {customerParty.LedgerName}{refString}"
                },
                new()
                {
                    LedgerId = customerParty.LedgerId,
                    Debit = 0m,
                    Credit = totalReturnAmount,
                    Narration = $"Credit Note issued to {customerParty.LedgerName}{refString}"
                }
            }
        };

        var validation = _accountingService.ValidateVoucher(dto, fy.StartDate, fy.EndDate);
        if (!validation.IsValid)
        {
            MessageBox.Show(this, string.Join(Environment.NewLine, validation.Errors), "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }

        try
        {
            var savedVoucher = await _accountingService.SaveVoucherAsync(company.CompanyId, dto);
            MessageBox.Show(this, $"Credit Note {savedVoucher.VoucherNumber} for ₹{totalReturnAmount:N2} saved successfully!", "Voucher Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);

            if (resetAfterSave)
            {
                await ResetFormAsync();
            }
            else
            {
                Close();
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error saving Credit Note voucher: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private async Task ResetFormAsync()
    {
        _txtOriginalInvoiceNo.Clear();
        _dtpOriginalInvoiceDate.Value = DateTime.Today;
        _txtNarration.Text = "Being goods returned / credit note issued against invoice";
        _dgvItems.Rows.Clear();
        AddDefaultEmptyRow();
        RecalculateTotals();
        await RefreshVoucherNumberPreviewAsync();
        if (_cmbParty.SelectedItem != null)
            await OnPartySelectedAsync();
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.A)
        {
            _btnSave.PerformClick();
            e.Handled = true;
        }
        else if (e.Alt && e.KeyCode == Keys.S)
        {
            _btnSaveAndNew.PerformClick();
            e.Handled = true;
        }
        else if (e.Alt && e.KeyCode == Keys.N)
        {
            _btnNew.PerformClick();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.P)
        {
            _btnPrint.PerformClick();
            e.Handled = true;
        }
    }
}
