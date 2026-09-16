using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms.Vouchers;

/// <summary>
/// Executive Ledger Desktop — Sales Voucher View (F8).
/// Embedded view hosted inside the unified VoucherShell.
/// </summary>
public class SalesVoucherView : UserControl, IVoucherView
{
    private readonly IVoucherHost _host;
    private readonly IAccountingService _accountingService;

    private VoucherType? _salesVoucherType;
    private IReadOnlyList<LedgerSummaryDto> _partyLedgers = new List<LedgerSummaryDto>();
    private IReadOnlyList<LedgerSummaryDto> _salesLedgers = new List<LedgerSummaryDto>();

    private Label _lblVoucherTag = null!;
    private Label _lblVoucherNumber = null!;
    private DateTimePicker _dtpVoucherDate = null!;
    private TextBox _txtRefNo = null!;
    private ComboBox _cmbParty = null!;
    private Label _lblPartyBalance = null!;
    private ComboBox _cmbSalesLedger = null!;
    private Label _lblSalesBalance = null!;
    private Guna2DataGridView _dgvItems = null!;
    private TextBox _txtNarration = null!;
    private Label _lblSubtotal = null!;
    private Label _lblDiscount = null!;
    private Label _lblNetTotal = null!;
    private Label _lblBalanceStatus = null!;
    private Guna2Button _btnSave = null!;
    private Guna2Button _btnSaveAndNew = new();
    private Guna2Button _btnNew = null!;
    private Guna2Button _btnPrint = null!;
    private Guna2Button _btnCancel = null!;
    private bool _isInitializing;

    private enum ActiveSalesSelector { None, Party, SalesLedger }
    private ActiveSalesSelector _activeSelector = ActiveSalesSelector.None;

    public VoucherTypeEnum VoucherType => VoucherTypeEnum.Sales;
    public string VoucherTitle => "Sales Voucher (F8)";
    public string ActiveVoucherKey => "Sales";
    public Control ContentControl => this;

    public SalesVoucherView(
        IVoucherHost host,
        IAccountingService accountingService)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _accountingService = accountingService ?? throw new ArgumentNullException(nameof(accountingService));

        InitializeComponent();
    }

    public void FocusDefault() => _cmbParty?.Focus();
    public void FocusDate() => _dtpVoucherDate?.Focus();

    public bool HasUnsavedChanges()
    {
        if (!string.IsNullOrWhiteSpace(_txtRefNo?.Text)) return true;
        if (!string.IsNullOrWhiteSpace(_txtNarration?.Text)) return true;
        if (_dgvItems == null) return false;

        foreach (DataGridViewRow row in _dgvItems.Rows)
        {
            if (row.IsNewRow) continue;
            var desc = row.Cells["ColDesc"]?.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(desc)) return true;
            if (decimal.TryParse(row.Cells["ColAmount"]?.Value?.ToString(), out var amt) && amt > 0) return true;
            if (decimal.TryParse(row.Cells["ColRate"]?.Value?.ToString(), out var rate) && rate > 0) return true;
        }

        return false;
    }

    private void InitializeComponent()
    {
        Dock = DockStyle.Fill;
        BackColor = ExecLedgerTheme.WorkSurface;

        var workLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = ExecLedgerTheme.WorkSurface
        };
        workLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // Voucher badge & date
        workLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); // Account & Balance
        workLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Particulars Table
        workLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Narration & Total
        workLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Action Bar

        // --- Row 0: Voucher Type Tag & Voucher Number & Date ---
        var pnlVoucherHeader = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };

        _lblVoucherTag = new Label
        {
            Text = "SALES",
            BackColor = ExecLedgerTheme.PrimaryNavy,
            ForeColor = ExecLedgerTheme.WhiteText,
            Font = ExecLedgerTheme.UIBold8,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(80, 24),
            Location = new Point(0, 4)
        };

        var lblNoPrefix = new Label
        {
            Text = "No.",
            Font = ExecLedgerTheme.UIRegular9,
            ForeColor = ExecLedgerTheme.SecondaryText,
            AutoSize = true,
            Location = new Point(88, 7)
        };

        _lblVoucherNumber = new Label
        {
            Text = "SLS-00001",
            Font = ExecLedgerTheme.MonoBold9,
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            AutoSize = true,
            Location = new Point(112, 7)
        };

        var lblRef = new Label
        {
            Text = "Ref / Inv No:",
            Font = ExecLedgerTheme.UIRegular9,
            ForeColor = ExecLedgerTheme.SecondaryText,
            AutoSize = true,
            Location = new Point(220, 7)
        };

        _txtRefNo = new TextBox
        {
            Width = 140,
            Font = ExecLedgerTheme.UIRegular9,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(300, 5)
        };

        var pnlDate = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Location = new Point(640, 2)
        };

        var lblDateTag = new Label
        {
            Text = "Date:",
            Font = ExecLedgerTheme.UIRegular9,
            ForeColor = ExecLedgerTheme.SecondaryText,
            AutoSize = true,
            Margin = new Padding(0, 4, 4, 0)
        };

        _dtpVoucherDate = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MMM-yyyy",
            Width = 120,
            Font = ExecLedgerTheme.UIRegular8
        };

        pnlDate.Controls.Add(lblDateTag);
        pnlDate.Controls.Add(_dtpVoucherDate);

        pnlVoucherHeader.Controls.Add(_lblVoucherTag);
        pnlVoucherHeader.Controls.Add(lblNoPrefix);
        pnlVoucherHeader.Controls.Add(_lblVoucherNumber);
        pnlVoucherHeader.Controls.Add(lblRef);
        pnlVoucherHeader.Controls.Add(_txtRefNo);
        pnlVoucherHeader.Controls.Add(pnlDate);

        // --- Row 1: Party A/c (Dr) & Sales Ledger (Cr) ---
        var pnlAccountRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 2, 0, 2)
        };

        var lblPartyTag = new Label
        {
            Text = "Party A/c (Dr) :",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.PrimaryText,
            AutoSize = true,
            Margin = new Padding(0, 6, 6, 0)
        };

        _cmbParty = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 220,
            Font = ExecLedgerTheme.UIRegular9,
            Margin = new Padding(0, 3, 6, 0)
        };
        _cmbParty.SelectedIndexChanged += async (s, e) => await OnPartySelectedAsync();
        _cmbParty.Enter += (s, e) =>
        {
            _activeSelector = ActiveSalesSelector.Party;
            _cmbParty.BackColor = ExecLedgerTheme.PrimarySelection;
            _host.FlyoutPanel.Visible = true;
            _host.FlyoutPanel.SetTitle("List of Party / Customer Accounts");
            _host.FlyoutPanel.LoadLedgers(_partyLedgers, includeEndOfList: false);
        };
        _cmbParty.Leave += (s, e) =>
        {
            _cmbParty.BackColor = Color.White;
        };

        _lblPartyBalance = new Label
        {
            Text = "Cur Bal: ₹0.00",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            AutoSize = true,
            Margin = new Padding(0, 6, 24, 0)
        };

        var lblSalesTag = new Label
        {
            Text = "Sales Ledger :",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.PrimaryText,
            AutoSize = true,
            Margin = new Padding(0, 6, 6, 0)
        };

        _cmbSalesLedger = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 200,
            Font = ExecLedgerTheme.UIRegular9,
            Margin = new Padding(0, 3, 6, 0)
        };
        _cmbSalesLedger.SelectedIndexChanged += async (s, e) => await OnSalesLedgerSelectedAsync();
        _cmbSalesLedger.Enter += (s, e) =>
        {
            _activeSelector = ActiveSalesSelector.SalesLedger;
            _cmbSalesLedger.BackColor = ExecLedgerTheme.PrimarySelection;
            _host.FlyoutPanel.Visible = true;
            _host.FlyoutPanel.SetTitle("List of Sales Accounts");
            _host.FlyoutPanel.LoadLedgers(_salesLedgers, includeEndOfList: false);
        };
        _cmbSalesLedger.Leave += (s, e) =>
        {
            _cmbSalesLedger.BackColor = Color.White;
        };

        _lblSalesBalance = new Label
        {
            Text = "Cur Bal: ₹0.00",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0)
        };

        pnlAccountRow.Controls.Add(lblPartyTag);
        pnlAccountRow.Controls.Add(_cmbParty);
        pnlAccountRow.Controls.Add(_lblPartyBalance);
        pnlAccountRow.Controls.Add(lblSalesTag);
        pnlAccountRow.Controls.Add(_cmbSalesLedger);
        pnlAccountRow.Controls.Add(_lblSalesBalance);

        // --- Row 2: DataGridView for Line Items ---
        _dgvItems = new Guna2DataGridView
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

        _dgvItems.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = ExecLedgerTheme.ApplicationCanvas,
            ForeColor = ExecLedgerTheme.PrimaryText,
            Font = ExecLedgerTheme.UIBold9,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0)
        };
        _dgvItems.ColumnHeadersHeight = 26;
        _dgvItems.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

        _dgvItems.DefaultCellStyle = new DataGridViewCellStyle
        {
            Font = ExecLedgerTheme.UIRegular9,
            SelectionBackColor = ExecLedgerTheme.PrimarySelection,
            SelectionForeColor = Color.Black,
            Padding = new Padding(4, 0, 4, 0)
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
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2", Font = ExecLedgerTheme.UIBold10 }
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

        // --- Row 3: Narration and Total Summary ---
        var pnlSummary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 2, 0, 0)
        };
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));

        var lblNarration = new Label
        {
            Text = "Narration:",
            Font = ExecLedgerTheme.UIRegular9,
            ForeColor = ExecLedgerTheme.SecondaryText,
            Anchor = AnchorStyles.Left,
            AutoSize = true
        };

        _txtNarration = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = ExecLedgerTheme.UIRegular9,
            BorderStyle = BorderStyle.FixedSingle
        };

        var pnlTotals = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };

        _lblNetTotal = new Label { Text = "Total: ₹0.00", Font = ExecLedgerTheme.UIBold11, ForeColor = ExecLedgerTheme.PrimaryNavy, AutoSize = true, Margin = new Padding(12, 4, 0, 0) };
        _lblDiscount = new Label { Text = "Discount: ₹0.00", Font = ExecLedgerTheme.UIBold10, ForeColor = Color.DarkRed, AutoSize = true, Margin = new Padding(12, 4, 0, 0) };
        _lblSubtotal = new Label { Text = "Subtotal: ₹0.00", Font = ExecLedgerTheme.UIRegular9, ForeColor = Color.DimGray, AutoSize = true, Margin = new Padding(12, 4, 0, 0) };
        _lblBalanceStatus = new Label { Text = "Enter line items", Font = ExecLedgerTheme.UIBold9, ForeColor = Color.DarkOrange, AutoSize = true, Margin = new Padding(0, 4, 10, 0) };

        pnlTotals.Controls.Add(_lblNetTotal);
        pnlTotals.Controls.Add(_lblDiscount);
        pnlTotals.Controls.Add(_lblSubtotal);
        pnlTotals.Controls.Add(_lblBalanceStatus);

        pnlSummary.Controls.Add(lblNarration, 0, 0);
        pnlSummary.Controls.Add(_txtNarration, 1, 0);
        pnlSummary.Controls.Add(pnlTotals, 2, 0);

        // --- Row 4: Action Ribbon ---
        var pnlBottomRibbon = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ExecLedgerTheme.ApplicationCanvas,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 4, 0, 0)
        };

        var flowRibbon = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(4, 2, 4, 2)
        };

        _btnCancel = _host.CreateActionButton("Esc: Quit", false, () => _host.CloseHost());
        _btnSave = _host.CreateActionButton("Ctrl+A: Accept", true, async () => await SaveVoucherAsync(closeAfterSave: true));
        _btnNew = _host.CreateActionButton("Clear", false, () => ResetForm());
        _btnPrint = _host.CreateActionButton("Ctrl+P: Print", false, () => MessageBox.Show("Invoice print preview ready.", "Executive Ledger Print", MessageBoxButtons.OK, MessageBoxIcon.Information));

        flowRibbon.Controls.AddRange(new Control[] { _btnCancel, _btnSave, _btnNew, _btnPrint });

        pnlBottomRibbon.Controls.Add(flowRibbon);

        // Assemble Work Area Layout
        workLayout.Controls.Add(pnlVoucherHeader, 0, 0);
        workLayout.Controls.Add(pnlAccountRow, 0, 1);
        workLayout.Controls.Add(_dgvItems, 0, 2);
        workLayout.Controls.Add(pnlSummary, 0, 3);
        workLayout.Controls.Add(pnlBottomRibbon, 0, 4);

        Controls.Add(workLayout);

        _host.FlyoutPanel.LedgerSelected += OnFlyoutLedgerSelected;
    }

    public void OnFlyoutLedgerSelected(LedgerSummaryDto? ledger)
    {
        if (_activeSelector == ActiveSalesSelector.Party)
        {
            if (ledger != null)
                _cmbParty.SelectedValue = ledger.LedgerId;
            _cmbSalesLedger.Focus();
        }
        else if (_activeSelector == ActiveSalesSelector.SalesLedger)
        {
            if (ledger != null)
                _cmbSalesLedger.SelectedValue = ledger.LedgerId;
            _dgvItems.Focus();
        }
    }

    public async Task InitializeAsync()
    {
        _isInitializing = true;
        try
        {
            var company = _host.CompanyContext.CurrentCompany;
            var fy = _host.CompanyContext.CurrentFinancialYear;

            if (company == null || fy == null)
            {
                MessageBox.Show("No active company or financial year selected.", "Executive Ledger", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _host.CloseHost();
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
        }

        await OnPartySelectedAsync();
        await OnSalesLedgerSelectedAsync();
    }

    private async Task RefreshVoucherNumberPreviewAsync()
    {
        if (_salesVoucherType == null || _host.CompanyContext.CurrentCompany == null || _host.CompanyContext.CurrentFinancialYear == null)
            return;

        var nextNumber = await _accountingService.GetNextVoucherNumberPreviewAsync(
            _host.CompanyContext.CurrentCompany.CompanyId,
            _salesVoucherType.VoucherTypeId,
            _host.CompanyContext.CurrentFinancialYear.FinancialYearId);

        _lblVoucherNumber.Text = nextNumber;
    }

    private async Task OnPartySelectedAsync()
    {
        if (_isInitializing) return;
        if (_cmbParty.SelectedItem is LedgerSummaryDto selected && _host.CompanyContext.CurrentCompany != null)
        {
            var balance = await _accountingService.GetLedgerBalanceAsync(_host.CompanyContext.CurrentCompany.CompanyId, selected.LedgerId);
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
        if (_cmbSalesLedger.SelectedItem is LedgerSummaryDto selected && _host.CompanyContext.CurrentCompany != null)
        {
            var balance = await _accountingService.GetLedgerBalanceAsync(_host.CompanyContext.CurrentCompany.CompanyId, selected.LedgerId);
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
            var company = _host.CompanyContext.CurrentCompany;
            var fy = _host.CompanyContext.CurrentFinancialYear;

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
                _host.CloseHost();
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
}
