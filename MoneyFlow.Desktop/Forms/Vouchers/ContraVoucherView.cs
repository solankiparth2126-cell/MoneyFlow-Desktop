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
/// Executive Ledger Desktop — Contra Voucher View (F4).
/// Embedded view hosted inside the unified VoucherShell.
/// </summary>
public class ContraVoucherView : UserControl, IVoucherView
{
    private readonly IVoucherHost _host;
    private readonly IAccountingService _accountingService;
    private readonly ILedgerService _ledgerService;

    private VoucherType? _contraVoucherType;
    private IReadOnlyList<LedgerSummaryDto> _cashBankLedgers = new List<LedgerSummaryDto>();

    private Label _lblVoucherTag = null!;
    private Label _lblVoucherNumber = null!;
    private DateTimePicker _dtpVoucherDate = null!;
    private ComboBox _cmbDestinationAccount = null!;
    private Label _lblDestinationBalance = null!;
    private Guna2DataGridView _dgvEntries = null!;
    private TextBox _txtNarration = null!;
    private Label _lblTotalAmount = null!;
    private Label _lblBalanceStatus = null!;

    private Guna2Button _btnSave = null!;
    private Guna2Button _btnSaveAndNew = new();
    private Guna2Button _btnNew = null!;
    private Guna2Button _btnPrint = null!;
    private Guna2Button _btnCancel = null!;

    private bool _isInitializing;
    private bool _isDestinationActive;

    public VoucherTypeEnum VoucherType => VoucherTypeEnum.Contra;
    public string VoucherTitle => "Contra Voucher (F4)";
    public string ActiveVoucherKey => "Contra";
    public Control ContentControl => this;

    public ContraVoucherView(
        IVoucherHost host,
        IAccountingService accountingService,
        ILedgerService ledgerService)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _accountingService = accountingService ?? throw new ArgumentNullException(nameof(accountingService));
        _ledgerService = ledgerService ?? throw new ArgumentNullException(nameof(ledgerService));

        InitializeComponent();
    }

    public void FocusDefault() => _cmbDestinationAccount?.Focus();
    public void FocusDate() => _dtpVoucherDate?.Focus();

    public bool HasUnsavedChanges()
    {
        if (_dgvEntries == null) return false;
        foreach (DataGridViewRow row in _dgvEntries.Rows)
        {
            if (row.IsNewRow) continue;
            var amtVal = row.Cells["ColAmount"]?.Value;
            if (amtVal != null && decimal.TryParse(amtVal.ToString(), out var amt) && amt > 0)
                return true;
            if (row.Cells["ColLedger"]?.Value != null)
                return true;
        }
        if (!string.IsNullOrWhiteSpace(_txtNarration?.Text))
            return true;
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
        workLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Narration & Total
        workLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); // Action Bar

        // --- Row 0: Voucher Type Tag & Voucher Number & Date ---
        var pnlVoucherHeader = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };

        _lblVoucherTag = new Label
        {
            Text = "CONTRA",
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
            Text = "CTR-00001",
            Font = ExecLedgerTheme.MonoBold9,
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            AutoSize = true,
            Location = new Point(112, 7)
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
        pnlVoucherHeader.Controls.Add(pnlDate);

        // --- Row 1: Destination Account (Dr) & Current Balance ---
        var pnlAccountRow = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };

        var lblAccountPrompt = new Label
        {
            Text = "Account",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.PrimaryText,
            Location = new Point(0, 4),
            AutoSize = true
        };

        var lblColon = new Label
        {
            Text = ":",
            Font = ExecLedgerTheme.UIBold9,
            Location = new Point(60, 4),
            AutoSize = true
        };

        _cmbDestinationAccount = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 280,
            Font = ExecLedgerTheme.UIRegular9,
            Location = new Point(72, 1),
            FlatStyle = FlatStyle.Flat
        };
        _cmbDestinationAccount.SelectedIndexChanged += async (s, e) =>
        {
            if (_isInitializing) return;
            await OnDestinationAccountSelectedAsync();
        };
        _cmbDestinationAccount.Enter += (s, e) =>
        {
            _isDestinationActive = true;
            _cmbDestinationAccount.BackColor = ExecLedgerTheme.PrimarySelection;
            _host.FlyoutPanel.Visible = true;
            _host.FlyoutPanel.SetTitle("List of Bank/Cash Accounts");
            _host.FlyoutPanel.LoadLedgers(_cashBankLedgers, includeEndOfList: false);
        };
        _cmbDestinationAccount.Leave += (s, e) =>
        {
            _cmbDestinationAccount.BackColor = ExecLedgerTheme.InputBg;
        };

        var lblBalanceTitle = new Label
        {
            Text = "Current balance :",
            Font = ExecLedgerTheme.UIRegular8,
            ForeColor = ExecLedgerTheme.SecondaryText,
            Location = new Point(0, 26),
            AutoSize = true
        };

        _lblDestinationBalance = new Label
        {
            Text = "0.00",
            Font = ExecLedgerTheme.MonoRegular9,
            ForeColor = ExecLedgerTheme.SuccessGreen,
            Location = new Point(100, 25),
            AutoSize = true
        };

        pnlAccountRow.Controls.Add(lblAccountPrompt);
        pnlAccountRow.Controls.Add(lblColon);
        pnlAccountRow.Controls.Add(_cmbDestinationAccount);
        pnlAccountRow.Controls.Add(lblBalanceTitle);
        pnlAccountRow.Controls.Add(_lblDestinationBalance);

        // --- Row 2: DataGridView for Line Items ---
        _dgvEntries = new Guna2DataGridView
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

        _dgvEntries.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = ExecLedgerTheme.ApplicationCanvas,
            ForeColor = ExecLedgerTheme.PrimaryText,
            Font = ExecLedgerTheme.UIBold9,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(6, 0, 0, 0)
        };
        _dgvEntries.ColumnHeadersHeight = 26;
        _dgvEntries.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

        _dgvEntries.DefaultCellStyle = new DataGridViewCellStyle
        {
            Font = ExecLedgerTheme.UIRegular9,
            SelectionBackColor = ExecLedgerTheme.PrimarySelection,
            SelectionForeColor = Color.Black,
            Padding = new Padding(4, 0, 4, 0)
        };

        var colLedger = new DataGridViewComboBoxColumn
        {
            HeaderText = "Particulars (Source Cr A/c)",
            Name = "ColLedger",
            Width = 320,
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
        _dgvEntries.EditMode = DataGridViewEditMode.EditOnEnter;
        _dgvEntries.CellValueChanged += async (s, e) =>
        {
            if (_isInitializing) return;
            await OnGridCellValueChangedAsync(e.RowIndex, e.ColumnIndex);
        };
        _dgvEntries.RowsRemoved += (s, e) => RecalculateTotals();

        _dgvEntries.CellEnter += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == _dgvEntries.Columns["ColLedger"].Index)
            {
                _isDestinationActive = false;
                _host.FlyoutPanel.Visible = true;
                _host.FlyoutPanel.SetTitle("List of Bank/Cash Accounts");
                _host.FlyoutPanel.LoadLedgers(_cashBankLedgers, includeEndOfList: true);
            }
        };

        // --- Row 3: Narration and Total Summary ---
        var pnlSummary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 2, 0, 0)
        };
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

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
            FlowDirection = FlowDirection.RightToLeft
        };

        _lblTotalAmount = new Label
        {
            Text = "₹0.00",
            Font = ExecLedgerTheme.UIBold11,
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0)
        };

        _lblBalanceStatus = new Label
        {
            Text = "Voucher Balanced",
            ForeColor = ExecLedgerTheme.SuccessGreen,
            Font = ExecLedgerTheme.UIBold8,
            AutoSize = true,
            Margin = new Padding(0, 4, 12, 0)
        };

        pnlTotals.Controls.Add(_lblTotalAmount);
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
        _btnPrint = _host.CreateActionButton("Ctrl+P: Print", false, () => MessageBox.Show("Voucher print preview ready.", "Executive Ledger Print", MessageBoxButtons.OK, MessageBoxIcon.Information));

        flowRibbon.Controls.AddRange(new Control[] { _btnCancel, _btnSave, _btnNew, _btnPrint });

        pnlBottomRibbon.Controls.Add(flowRibbon);

        // Assemble Work Area Layout
        workLayout.Controls.Add(pnlVoucherHeader, 0, 0);
        workLayout.Controls.Add(pnlAccountRow, 0, 1);
        workLayout.Controls.Add(_dgvEntries, 0, 2);
        workLayout.Controls.Add(pnlSummary, 0, 3);
        workLayout.Controls.Add(pnlBottomRibbon, 0, 4);

        Controls.Add(workLayout);

        _host.FlyoutPanel.LedgerSelected += OnFlyoutLedgerSelected;
    }

    public void OnFlyoutLedgerSelected(LedgerSummaryDto? ledger)
    {
        if (_isDestinationActive)
        {
            if (ledger != null)
                _cmbDestinationAccount.SelectedValue = ledger.LedgerId;
            _dgvEntries.Focus();
        }
        else
        {
            if (_dgvEntries.CurrentCell != null)
            {
                int rowIdx = _dgvEntries.CurrentCell.RowIndex;
                if (rowIdx >= 0 && rowIdx < _dgvEntries.Rows.Count)
                {
                    if (ledger == null)
                    {
                        _txtNarration.Focus();
                    }
                    else
                    {
                        _dgvEntries.Rows[rowIdx].Cells["ColLedger"].Value = ledger.LedgerId;
                        _dgvEntries.CurrentCell = _dgvEntries.Rows[rowIdx].Cells["ColAmount"];
                    }
                }
            }
        }
    }

    public async Task InitializeAsync()
    {
        var company = _host.CompanyContext.CurrentCompany;
        var fy = _host.CompanyContext.CurrentFinancialYear;

        if (company == null || fy == null)
        {
            MessageBox.Show("No active company or financial year selected.", "Executive Ledger", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _isInitializing = true;
        try
        {
            _dtpVoucherDate.MinDate = fy.StartDate;
            _dtpVoucherDate.MaxDate = fy.EndDate;
            _dtpVoucherDate.Value = DateTime.Today >= fy.StartDate && DateTime.Today <= fy.EndDate ? DateTime.Today : fy.StartDate;

            _contraVoucherType = await _accountingService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Contra);

            await RefreshVoucherNumberPreviewAsync();

            _cashBankLedgers = await _accountingService.GetCashAndBankLedgersAsync(company.CompanyId);

            _cmbDestinationAccount.DisplayMember = "LedgerName";
            _cmbDestinationAccount.ValueMember = "LedgerId";
            _cmbDestinationAccount.DataSource = _cashBankLedgers.ToList();

            var colLedger = (DataGridViewComboBoxColumn)_dgvEntries.Columns["ColLedger"];
            colLedger.DisplayMember = "LedgerName";
            colLedger.ValueMember = "LedgerId";
            colLedger.DataSource = _cashBankLedgers.ToList();

            if (_dgvEntries.Rows.Count == 0)
                _dgvEntries.Rows.Add();

            RecalculateTotals();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize Contra Voucher: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isInitializing = false;
        }

        await OnDestinationAccountSelectedAsync();
    }

    private async Task RefreshVoucherNumberPreviewAsync()
    {
        var company = _host.CompanyContext.CurrentCompany;
        var fy = _host.CompanyContext.CurrentFinancialYear;
        if (_contraVoucherType == null || company == null || fy == null) return;

        var nextNumber = await _accountingService.GetNextVoucherNumberPreviewAsync(
            company.CompanyId,
            _contraVoucherType.VoucherTypeId,
            fy.FinancialYearId);

        _lblVoucherNumber.Text = nextNumber;
    }

    private async Task OnDestinationAccountSelectedAsync()
    {
        if (_isInitializing) return;
        var company = _host.CompanyContext.CurrentCompany;
        if (_cmbDestinationAccount.SelectedItem is LedgerSummaryDto selected && company != null)
        {
            var balance = await _accountingService.GetLedgerBalanceAsync(company.CompanyId, selected.LedgerId);
            _lblDestinationBalance.Text = $"Cur Bal: {balance.ClosingBalanceDisplay}";
            _lblDestinationBalance.ForeColor = balance.ClosingBalance >= 0 ? Color.FromArgb(0, 100, 0) : Color.DarkRed;
        }
        else
        {
            _lblDestinationBalance.Text = "Cur Bal: ₹0.00";
        }
    }

    private async Task ApplyTransferTemplateAsync(bool isDeposit)
    {
        if (_cashBankLedgers.Count == 0) return;

        var cashLedger = _cashBankLedgers.FirstOrDefault(l => l.GroupName.Contains("Cash", StringComparison.OrdinalIgnoreCase) || l.LedgerName.Contains("Cash", StringComparison.OrdinalIgnoreCase));
        var bankLedger = _cashBankLedgers.FirstOrDefault(l => l.GroupName.Contains("Bank", StringComparison.OrdinalIgnoreCase) || l.LedgerName.Contains("Bank", StringComparison.OrdinalIgnoreCase));

        _isInitializing = true;
        try
        {
            if (isDeposit)
            {
                if (bankLedger != null) _cmbDestinationAccount.SelectedValue = bankLedger.LedgerId;
                if (cashLedger != null && _dgvEntries.Rows.Count > 0)
                {
                    _dgvEntries.Rows[0].Cells["ColLedger"].Value = cashLedger.LedgerId;
                    _dgvEntries.Rows[0].Cells["ColMode"].Value = "Cash Deposit";
                }
                _txtNarration.Text = "Being cash deposited into bank.";
            }
            else
            {
                if (cashLedger != null) _cmbDestinationAccount.SelectedValue = cashLedger.LedgerId;
                if (bankLedger != null && _dgvEntries.Rows.Count > 0)
                {
                    _dgvEntries.Rows[0].Cells["ColLedger"].Value = bankLedger.LedgerId;
                    _dgvEntries.Rows[0].Cells["ColMode"].Value = "Cash Withdrawal";
                }
                _txtNarration.Text = "Being cash withdrawn from bank for office use.";
            }
        }
        finally
        {
            _isInitializing = false;
        }

        await OnDestinationAccountSelectedAsync();
        if (_dgvEntries.Rows.Count > 0)
        {
            await OnGridCellValueChangedAsync(0, _dgvEntries.Columns["ColLedger"].Index);
        }
    }

    private async Task OnGridCellValueChangedAsync(int rowIndex, int columnIndex)
    {
        if (_isInitializing) return;
        if (rowIndex < 0 || rowIndex >= _dgvEntries.Rows.Count) return;

        var row = _dgvEntries.Rows[rowIndex];

        if (columnIndex == _dgvEntries.Columns["ColLedger"].Index)
        {
            var company = _host.CompanyContext.CurrentCompany;
            if (row.Cells["ColLedger"].Value is int ledgerId && company != null)
            {
                var balance = await _accountingService.GetLedgerBalanceAsync(company.CompanyId, ledgerId);
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
            var company = _host.CompanyContext.CurrentCompany;
            var fy = _host.CompanyContext.CurrentFinancialYear;

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

                if (!decimal.TryParse(row.Cells["ColAmount"].Value?.ToString(), out var amount) || amount <= 0)
                {
                    MessageBox.Show($"Row {i + 1}: Please enter a valid transfer amount greater than 0.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var mode = row.Cells["ColMode"].Value?.ToString() ?? string.Empty;
                var lineNarration = row.Cells["ColNarration"].Value?.ToString() ?? string.Empty;

                entries.Add(new VoucherEntryDto
                {
                    LedgerId = sourceLedgerId,
                    Debit = 0,
                    Credit = amount,
                    Narration = string.IsNullOrWhiteSpace(mode) ? lineNarration : $"[{mode}] {lineNarration}".Trim()
                });

                totalCredit += amount;
            }

            if (entries.Count == 0 || totalCredit <= 0)
            {
                MessageBox.Show("Please enter at least one source (Credit) line entry with amount.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

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
                VoucherDate = _dtpVoucherDate.Value,
                Narration = _txtNarration.Text.Trim(),
                Entries = entries
            };

            _btnSave.Enabled = false;
            _btnSaveAndNew.Enabled = false;

            var createdVoucher = await _accountingService.SaveVoucherAsync(company.CompanyId, voucherDto);

            MessageBox.Show(
                $"Contra Voucher created successfully.\nVoucher No: {createdVoucher.VoucherNumber}\nAmount: ₹{totalCredit:N2}",
                "Success",
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
                await OnDestinationAccountSelectedAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving Contra voucher: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        _dgvEntries.Rows.Add();
        _txtNarration.Clear();
        RecalculateTotals();
    }
}
