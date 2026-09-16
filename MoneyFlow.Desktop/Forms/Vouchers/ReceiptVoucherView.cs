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
/// Executive Ledger Desktop — Receipt Voucher View (F6).
/// Embedded view hosted inside the unified VoucherShell.
/// </summary>
public class ReceiptVoucherView : UserControl, IVoucherView
{
    private readonly IVoucherHost _host;
    private readonly IAccountingService _accountingService;
    private readonly ILedgerService _ledgerService;

    private VoucherType? _receiptVoucherType;
    private IReadOnlyList<LedgerSummaryDto> _cashBankLedgers = new List<LedgerSummaryDto>();
    private IReadOnlyList<LedgerSummaryDto> _allLedgers = new List<LedgerSummaryDto>();

    private Label _lblVoucherTag = null!;
    private Label _lblVoucherNumber = null!;
    private DateTimePicker _dtpVoucherDate = null!;
    private ComboBox _cmbAccount = null!;
    private Label _lblAccountBalance = null!;
    private Guna2DataGridView _dgvEntries = null!;
    private TextBox _txtNarration = null!;
    private Label _lblTotalAmount = null!;
    private Label _lblBalanceStatus = null!;

    private Guna2Button _btnAccept = null!;
    private Guna2Button _btnClear = null!;
    private Guna2Button _btnPrint = null!;
    private Guna2Button _btnQuit = null!;

    private bool _isInitializing;
    private bool _isAccountActive;

    public VoucherTypeEnum VoucherType => VoucherTypeEnum.Receipt;
    public string VoucherTitle => "Receipt Voucher (F6)";
    public string ActiveVoucherKey => "Receipt";
    public Control ContentControl => this;

    public ReceiptVoucherView(
        IVoucherHost host,
        IAccountingService accountingService,
        ILedgerService ledgerService)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _accountingService = accountingService ?? throw new ArgumentNullException(nameof(accountingService));
        _ledgerService = ledgerService ?? throw new ArgumentNullException(nameof(ledgerService));

        InitializeComponent();
    }

    public void FocusDefault() => _cmbAccount?.Focus();
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
            Text = "Receipt",
            BackColor = ExecLedgerTheme.PrimaryNavy,
            ForeColor = Color.White,
            Font = ExecLedgerTheme.UIBold10,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(84, 25),
            Location = new Point(0, 2)
        };

        var lblNoPrefix = new Label
        {
            Text = "No.",
            Font = ExecLedgerTheme.UIRegular9,
            ForeColor = ExecLedgerTheme.PrimaryText,
            AutoSize = true,
            Location = new Point(94, 6)
        };

        _lblVoucherNumber = new Label
        {
            Text = "289",
            Font = ExecLedgerTheme.UIBold10,
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            AutoSize = true,
            Location = new Point(122, 6)
        };

        var pnlDate = new FlowLayoutPanel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Location = new Point(560, 2)
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
            Width = 130,
            Font = ExecLedgerTheme.UIRegular9
        };

        pnlDate.Controls.Add(lblDateTag);
        pnlDate.Controls.Add(_dtpVoucherDate);

        pnlVoucherHeader.Controls.Add(_lblVoucherTag);
        pnlVoucherHeader.Controls.Add(lblNoPrefix);
        pnlVoucherHeader.Controls.Add(_lblVoucherNumber);
        pnlVoucherHeader.Controls.Add(pnlDate);

        // --- Row 1: Account Header & Current Balance ---
        var pnlAccountRow = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };

        var lblAccountPrompt = new Label
        {
            Text = "Account",
            Font = ExecLedgerTheme.UIRegular9,
            ForeColor = ExecLedgerTheme.PrimaryText,
            Location = new Point(0, 4),
            AutoSize = true
        };

        var lblColon = new Label
        {
            Text = ":",
            Font = ExecLedgerTheme.UIBold9,
            Location = new Point(88, 4),
            AutoSize = true
        };

        _cmbAccount = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 260,
            Font = ExecLedgerTheme.UIRegular9,
            Location = new Point(100, 1),
            FlatStyle = FlatStyle.Flat
        };
        _cmbAccount.SelectedIndexChanged += async (s, e) => await OnAccountSelectedAsync();
        _cmbAccount.Enter += (s, e) =>
        {
            _isAccountActive = true;
            _cmbAccount.BackColor = ExecLedgerTheme.PrimarySelection;
            _host.FlyoutPanel.Visible = true;
            _host.FlyoutPanel.SetTitle("List of Ledger Accounts");
            _host.FlyoutPanel.LoadLedgers(_cashBankLedgers, includeEndOfList: false);
        };
        _cmbAccount.Leave += (s, e) =>
        {
            _cmbAccount.BackColor = Color.White;
        };

        var lblBalanceTitle = new Label
        {
            Text = "Current balance :",
            Font = ExecLedgerTheme.UIRegular9,
            ForeColor = ExecLedgerTheme.SecondaryText,
            Location = new Point(0, 26),
            AutoSize = true
        };

        _lblAccountBalance = new Label
        {
            Text = "₹0.00",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.SuccessGreen,
            Location = new Point(100, 26),
            AutoSize = true
        };

        pnlAccountRow.Controls.Add(lblAccountPrompt);
        pnlAccountRow.Controls.Add(lblColon);
        pnlAccountRow.Controls.Add(_cmbAccount);
        pnlAccountRow.Controls.Add(lblBalanceTitle);
        pnlAccountRow.Controls.Add(_lblAccountBalance);

        // --- Row 2: DataGridView for Particulars ---
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
            HeaderText = "Particulars",
            Name = "ColLedger",
            Width = 360,
            FlatStyle = FlatStyle.Flat,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing
        };

        var colBalance = new DataGridViewTextBoxColumn
        {
            HeaderText = "Current Balance",
            Name = "ColBalance",
            Width = 140,
            ReadOnly = true,
            DefaultCellStyle = { ForeColor = ExecLedgerTheme.SecondaryText }
        };

        var colAmount = new DataGridViewTextBoxColumn
        {
            HeaderText = "Amount",
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

        _dgvEntries.CellEnter += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == _dgvEntries.Columns["ColLedger"].Index)
            {
                _isAccountActive = false;
                _host.FlyoutPanel.Visible = true;
                _host.FlyoutPanel.SetTitle("List of Ledger Accounts");
                _host.FlyoutPanel.LoadLedgers(_allLedgers, includeEndOfList: true);
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

        _btnQuit = _host.CreateActionButton("Esc: Quit", false, () => _host.CloseHost());
        _btnAccept = _host.CreateActionButton("Ctrl+A: Accept", true, async () => await OnSaveVoucherAsync(closeOnSuccess: true));
        _btnClear = _host.CreateActionButton("Clear", false, () => ResetForm());
        _btnPrint = _host.CreateActionButton("Ctrl+P: Print", false, () => MessageBox.Show("Voucher print preview ready.", "Executive Ledger Print", MessageBoxButtons.OK, MessageBoxIcon.Information));

        flowRibbon.Controls.Add(_btnQuit);
        flowRibbon.Controls.Add(_btnAccept);
        flowRibbon.Controls.Add(_btnClear);
        flowRibbon.Controls.Add(_btnPrint);

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
        if (_isAccountActive)
        {
            if (ledger != null)
                _cmbAccount.SelectedValue = ledger.LedgerId;
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
            MessageBox.Show("Please select an active company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _isInitializing = true;
        try
        {
            var companyId = company.CompanyId;

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

            if (_dgvEntries.Rows.Count == 0)
                _dgvEntries.Rows.Add();

            await UpdateVoucherNumberPreviewAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize receipt voucher: {ex.Message}", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isInitializing = false;
        }

        await OnAccountSelectedAsync();
    }

    private async Task UpdateVoucherNumberPreviewAsync()
    {
        var company = _host.CompanyContext.CurrentCompany;
        var fy = _host.CompanyContext.CurrentFinancialYear;
        if (company == null || fy == null || _receiptVoucherType == null) return;

        try
        {
            var preview = await _accountingService.GetNextVoucherNumberPreviewAsync(
                company.CompanyId,
                _receiptVoucherType.VoucherTypeId,
                fy.FinancialYearId);

            _lblVoucherNumber.Text = preview;
        }
        catch
        {
            _lblVoucherNumber.Text = "—";
        }
    }

    private async Task OnAccountSelectedAsync()
    {
        if (_isInitializing) return;
        var company = _host.CompanyContext.CurrentCompany;
        if (company == null || _cmbAccount.SelectedValue == null) return;

        if (_cmbAccount.SelectedValue is int ledgerId)
        {
            try
            {
                var bal = await _accountingService.GetLedgerBalanceAsync(company.CompanyId, ledgerId);
                _lblAccountBalance.Text = $"{bal.FormattedClosingBalance}";
                _lblAccountBalance.ForeColor = bal.ClosingBalance >= 0 ? ExecLedgerTheme.SuccessGreen : ExecLedgerTheme.ErrorRed;
            }
            catch
            {
                _lblAccountBalance.Text = "₹0.00";
                _lblAccountBalance.ForeColor = ExecLedgerTheme.SecondaryText;
            }
        }
    }

    private async Task OnGridCellValueChangedAsync(int rowIndex, int columnIndex)
    {
        if (_isInitializing) return;
        if (rowIndex < 0 || rowIndex >= _dgvEntries.Rows.Count) return;

        var row = _dgvEntries.Rows[rowIndex];

        if (columnIndex == _dgvEntries.Columns["ColLedger"].Index)
        {
            var cellValue = row.Cells["ColLedger"].Value;
            var company = _host.CompanyContext.CurrentCompany;
            if (cellValue is int ledgerId && company != null)
            {
                try
                {
                    var bal = await _accountingService.GetLedgerBalanceAsync(company.CompanyId, ledgerId);
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

        _lblTotalAmount.Text = ExecLedgerTheme.FormatCurrency(totalCredit);

        if (totalCredit > 0)
        {
            _lblBalanceStatus.Text = "● BALANCED";
            _lblBalanceStatus.ForeColor = ExecLedgerTheme.SuccessGreen;
            _btnAccept.Enabled = true;
        }
        else
        {
            _lblBalanceStatus.Text = "Enter amounts";
            _lblBalanceStatus.ForeColor = ExecLedgerTheme.SecondaryText;
        }
    }

    private async Task OnSaveVoucherAsync(bool closeOnSuccess)
    {
        var company = _host.CompanyContext.CurrentCompany;
        var fy = _host.CompanyContext.CurrentFinancialYear;
        if (company == null || fy == null || _receiptVoucherType == null)
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
                var entry = new VoucherEntryDto
                {
                    LedgerId = creditLedgerId,
                    Debit = 0m,
                    Credit = amount,
                    Narration = narrationVal
                };

                entry.BillAllocations.Add(new BillAllocationCreateDto
                {
                    LedgerId = creditLedgerId,
                    BillType = BillType.AgstRef,
                    BillName = $"RCT-{DateTime.Today:yyyyMMdd}",
                    Amount = amount
                });

                entries.Add(entry);
                totalCredit += amount;
            }
        }

        if (entries.Count == 0 || totalCredit <= 0)
        {
            MessageBox.Show("Please enter at least one credit line item with an amount greater than 0.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        entries.Add(new VoucherEntryDto
        {
            LedgerId = debitLedgerId,
            Debit = totalCredit,
            Credit = 0m,
            Narration = _txtNarration.Text.Trim()
        });

        var createDto = new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = _receiptVoucherType.VoucherTypeId,
            VoucherDate = _dtpVoucherDate.Value,
            Narration = _txtNarration.Text.Trim(),
            Entries = entries
        };

        try
        {
            _btnAccept.Enabled = false;

            var saved = await _accountingService.SaveVoucherAsync(company.CompanyId, createDto);

            MessageBox.Show(
                $"Receipt voucher {saved.VoucherNumber} of {totalCredit:N2} saved successfully.",
                "Voucher Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            if (closeOnSuccess)
            {
                _host.CloseHost();
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
            MessageBox.Show($"Failed to save voucher: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnAccept.Enabled = true;
        }
    }

    private void ResetForm()
    {
        _dgvEntries.Rows.Clear();
        _dgvEntries.Rows.Add();
        _txtNarration.Clear();
        _lblTotalAmount.Text = ExecLedgerTheme.FormatCurrency(0);
        _lblBalanceStatus.Text = "Enter amounts";
        _lblBalanceStatus.ForeColor = ExecLedgerTheme.SecondaryText;
    }
}
