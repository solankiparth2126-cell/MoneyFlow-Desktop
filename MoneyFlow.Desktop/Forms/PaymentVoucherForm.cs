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
using MoneyFlow.Desktop.Controls;
using MoneyFlow.Desktop.Navigation;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Executive Ledger Desktop — Payment Voucher Entry (F5).
/// Navy header, accounting grid with Guna2DataGridView, balance indicator,
/// compact action bar, and keyboard-driven workflow.
/// </summary>
public class PaymentVoucherForm : Form
{
    private readonly IAccountingService _accountingService;
    private readonly ILedgerService _ledgerService;
    private readonly ICompanyContext _companyContext;
    private readonly INavigationService? _navigationService;

    private VoucherType? _paymentVoucherType;
    private IReadOnlyList<LedgerSummaryDto> _cashBankLedgers = new List<LedgerSummaryDto>();
    private IReadOnlyList<LedgerSummaryDto> _allLedgers = new List<LedgerSummaryDto>();

    // UI Controls
    private TallyTopHeaderBar _topHeaderBar = null!;
    private TallySideActionBar _sideActionBar = null!;
    private TallyLedgerFlyoutPanel _flyoutPanel = null!;

    private Label _lblVoucherTag = null!;
    private Label _lblVoucherNumber = null!;
    private DateTimePicker _dtpVoucherDate = null!;
    private ComboBox _cmbAccount = null!;
    private Label _lblAccountBalance = null!;
    private Guna2DataGridView _dgvEntries = null!;
    private TextBox _txtNarration = null!;
    private Label _lblTotalAmount = null!;
    private Panel _pnlBalanceBar = null!;
    private Label _lblBalanceStatus = null!;

    private Guna2Button _btnAccept = null!;
    private Guna2Button _btnClear = null!;
    private Guna2Button _btnPrint = null!;
    private Guna2Button _btnQuit = null!;

    private bool _isInitializing;
    private bool _isAccountActive;

    public PaymentVoucherForm(
        IAccountingService accountingService,
        ILedgerService ledgerService,
        ICompanyContext companyContext,
        INavigationService? navigationService = null)
    {
        _accountingService = accountingService;
        _ledgerService = ledgerService;
        _companyContext = companyContext;
        _navigationService = navigationService;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        ExecLedgerStyler.ApplyChildForm(this, "Payment Voucher (F5)");
        MinimumSize = new Size(950, 600);
        KeyPreview = true;

        // 1. Top Header Bar
        _topHeaderBar = new TallyTopHeaderBar();
        _topHeaderBar.SetSubtitle("Payment Voucher");
        _topHeaderBar.SetCompany(_companyContext.CurrentCompany?.CompanyName ?? "Executive Ledger");
        _topHeaderBar.CloseRequested += () => Close();
        _topHeaderBar.CompanyMenuRequested += () => _navigationService?.OpenCompanyList(this);

        // 2. Right Side Action Bar
        _sideActionBar = new TallySideActionBar();
        _sideActionBar.SetActiveVoucher("Payment");
        _sideActionBar.DateClicked += () => _dtpVoucherDate.Focus();
        _sideActionBar.CompanyClicked += () => _navigationService?.OpenCompanyList(this);
        _sideActionBar.ContraClicked += () => { Close(); _navigationService?.OpenContraVoucher(); };
        _sideActionBar.ReceiptClicked += () => { Close(); _navigationService?.OpenReceiptVoucher(); };
        _sideActionBar.JournalClicked += () => { Close(); _navigationService?.OpenJournalVoucher(); };
        _sideActionBar.SalesClicked += () => { Close(); _navigationService?.OpenSalesVoucher(); };
        _sideActionBar.PurchaseClicked += () => { Close(); _navigationService?.OpenPurchaseVoucher(); };

        // 3. Right Flyout
        _flyoutPanel = new TallyLedgerFlyoutPanel
        {
            Dock = DockStyle.Right,
            Visible = false
        };
        _flyoutPanel.LedgerSelected += OnFlyoutLedgerSelected;
        _flyoutPanel.CreateRequested += () => _navigationService?.OpenLedgerList(this);

        // 4. Center Work Area
        var pnlCenterWork = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ExecLedgerTheme.WorkSurface,
            Padding = new Padding(12, 8, 12, 4)
        };

        var workLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = ExecLedgerTheme.WorkSurface
        };
        workLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));   // Voucher badge & date
        workLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));   // Account & Balance
        workLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // Particulars Grid
        workLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));   // Narration & Total
        workLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));   // Action Bar

        // ── Row 0: Voucher Type Tag & Number & Date ──
        var pnlVoucherHeader = new Panel
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0)
        };

        _lblVoucherTag = new Label
        {
            Text = "PAYMENT",
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
            Text = "—",
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
            Location = new Point(560, 2)
        };

        var lblDateTag = new Label
        {
            Text = "Date:",
            Font = ExecLedgerTheme.UIRegular8,
            ForeColor = ExecLedgerTheme.SecondaryText,
            AutoSize = true,
            Margin = new Padding(0, 5, 4, 0)
        };

        _dtpVoucherDate = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MMM-yyyy",
            Width = 120,
            Font = ExecLedgerTheme.MonoRegular9
        };

        pnlDate.Controls.Add(lblDateTag);
        pnlDate.Controls.Add(_dtpVoucherDate);

        pnlVoucherHeader.Controls.Add(_lblVoucherTag);
        pnlVoucherHeader.Controls.Add(lblNoPrefix);
        pnlVoucherHeader.Controls.Add(_lblVoucherNumber);
        pnlVoucherHeader.Controls.Add(pnlDate);

        // ── Row 1: Account & Balance ──
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

        _cmbAccount = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 280,
            Font = ExecLedgerTheme.UIRegular9,
            Location = new Point(72, 1),
            FlatStyle = FlatStyle.Flat
        };
        _cmbAccount.SelectedIndexChanged += async (s, e) => await OnAccountSelectedAsync();
        _cmbAccount.Enter += (s, e) =>
        {
            _isAccountActive = true;
            _cmbAccount.BackColor = ExecLedgerTheme.PrimarySelection;
            _flyoutPanel.Visible = true;
            _flyoutPanel.SetTitle("List of Ledger Accounts");
            _flyoutPanel.LoadLedgers(_cashBankLedgers, includeEndOfList: false);
        };
        _cmbAccount.Leave += (s, e) =>
        {
            _cmbAccount.BackColor = ExecLedgerTheme.InputBg;
        };

        var lblBalanceTitle = new Label
        {
            Text = "Current balance :",
            Font = ExecLedgerTheme.UIRegular8,
            ForeColor = ExecLedgerTheme.SecondaryText,
            Location = new Point(0, 26),
            AutoSize = true
        };

        _lblAccountBalance = new Label
        {
            Text = "0.00",
            Font = ExecLedgerTheme.MonoRegular9,
            ForeColor = ExecLedgerTheme.SuccessGreen,
            Location = new Point(100, 25),
            AutoSize = true
        };

        pnlAccountRow.Controls.Add(lblAccountPrompt);
        pnlAccountRow.Controls.Add(lblColon);
        pnlAccountRow.Controls.Add(_cmbAccount);
        pnlAccountRow.Controls.Add(lblBalanceTitle);
        pnlAccountRow.Controls.Add(_lblAccountBalance);

        // ── Row 2: Particulars Grid (Guna2DataGridView) ──
        _dgvEntries = new Guna2DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToResizeRows = false
        };
        ExecLedgerStyler.StyleGrid(_dgvEntries, false);
        _dgvEntries.AllowUserToAddRows = true;
        _dgvEntries.AllowUserToDeleteRows = true;
        _dgvEntries.ReadOnly = false;
        _dgvEntries.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _dgvEntries.EditMode = DataGridViewEditMode.EditOnEnter;
        _dgvEntries.RowTemplate.Height = ExecLedgerTheme.StandardGridRow;

        // Active cell focus border
        _dgvEntries.CellPainting += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.All);
                if (_dgvEntries.CurrentCell != null &&
                    e.RowIndex == _dgvEntries.CurrentCell.RowIndex &&
                    e.ColumnIndex == _dgvEntries.CurrentCell.ColumnIndex)
                {
                    using var pen = new Pen(ExecLedgerTheme.SystemFocusBlue, 2);
                    var rect = e.CellBounds;
                    rect.Inflate(-1, -1);
                    e.Graphics.DrawRectangle(pen, rect);
                }
                e.Handled = true;
            }
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
            DefaultCellStyle = new DataGridViewCellStyle
            {
                ForeColor = ExecLedgerTheme.SecondaryText,
                Font = ExecLedgerTheme.MonoRegular9,
                Alignment = DataGridViewContentAlignment.MiddleRight,
                Padding = new Padding(4, 0, 8, 0)
            }
        };

        var colAmount = new DataGridViewTextBoxColumn
        {
            HeaderText = "Amount",
            Name = "ColAmount",
            Width = 150,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleRight,
                Format = "N2",
                Font = ExecLedgerTheme.MonoRegular9,
                Padding = new Padding(4, 0, 8, 0)
            }
        };

        var colNarration = new DataGridViewTextBoxColumn
        {
            HeaderText = "Line Narration",
            Name = "ColNarration",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            DefaultCellStyle = new DataGridViewCellStyle { Font = ExecLedgerTheme.UIRegular9 }
        };

        _dgvEntries.Columns.AddRange(colLedger, colBalance, colAmount, colNarration);
        _dgvEntries.CellValueChanged += async (s, e) => await OnGridCellValueChangedAsync(e.RowIndex, e.ColumnIndex);
        _dgvEntries.RowsRemoved += (s, e) => RecalculateTotals();

        _dgvEntries.CellEnter += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == _dgvEntries.Columns["ColLedger"].Index)
            {
                _isAccountActive = false;
                _flyoutPanel.Visible = true;
                _flyoutPanel.SetTitle("List of Ledger Accounts");
                _flyoutPanel.LoadLedgers(_allLedgers, includeEndOfList: true);
            }
        };

        // ── Row 3: Narration & Total Summary ──
        var pnlSummary = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0, 4, 0, 0)
        };
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 68));
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        pnlSummary.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        var lblNarration = new Label
        {
            Text = "Narration:",
            Font = ExecLedgerTheme.UIBold8,
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
            Text = "0.00",
            Font = ExecLedgerTheme.MonoBold10,
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            AutoSize = true,
            Margin = new Padding(0, 4, 0, 0)
        };

        _lblBalanceStatus = new Label
        {
            Text = "Enter amounts",
            ForeColor = ExecLedgerTheme.SecondaryText,
            Font = ExecLedgerTheme.UIBold8,
            AutoSize = true,
            Margin = new Padding(0, 6, 12, 0)
        };

        pnlTotals.Controls.Add(_lblTotalAmount);
        pnlTotals.Controls.Add(_lblBalanceStatus);

        pnlSummary.Controls.Add(lblNarration, 0, 0);
        pnlSummary.Controls.Add(_txtNarration, 1, 0);
        pnlSummary.Controls.Add(pnlTotals, 2, 0);

        // ── Row 4: Bottom Action Bar ──
        _pnlBalanceBar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ExecLedgerTheme.ApplicationCanvas,
            Margin = new Padding(0, 4, 0, 0)
        };
        _pnlBalanceBar.Paint += (s, e) =>
        {
            using var pen = new Pen(ExecLedgerTheme.PrimaryBorder, 1);
            e.Graphics.DrawLine(pen, 0, 0, _pnlBalanceBar.Width, 0);
        };

        var flowRibbon = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(4, 4, 4, 4)
        };

        _btnQuit = CreateActionButton("Esc: Quit", false, () => Close());
        _btnAccept = CreateActionButton("Ctrl+A: Accept", true, async () => await OnSaveVoucherAsync(closeOnSuccess: true));
        _btnClear = CreateActionButton("Clear", false, () => ResetForm());
        _btnPrint = CreateActionButton("Ctrl+P: Print", false, () => MessageBox.Show("Voucher print preview ready.", "Executive Ledger Print", MessageBoxButtons.OK, MessageBoxIcon.Information));

        flowRibbon.Controls.AddRange(new Control[] { _btnQuit, _btnAccept, _btnClear, _btnPrint });
        _pnlBalanceBar.Controls.Add(flowRibbon);

        // Assemble Work Area
        workLayout.Controls.Add(pnlVoucherHeader, 0, 0);
        workLayout.Controls.Add(pnlAccountRow, 0, 1);
        workLayout.Controls.Add(_dgvEntries, 0, 2);
        workLayout.Controls.Add(pnlSummary, 0, 3);
        workLayout.Controls.Add(_pnlBalanceBar, 0, 4);

        pnlCenterWork.Controls.Add(workLayout);

        // Assemble Form
        Controls.Add(pnlCenterWork);
        Controls.Add(_flyoutPanel);
        Controls.Add(_sideActionBar);
        Controls.Add(_topHeaderBar);

        // Keyboard navigation
        KeyDown += async (s, e) =>
        {
            if (e.Control && e.KeyCode == Keys.A) { e.Handled = true; await OnSaveVoucherAsync(true); }
            else if (e.Alt && e.KeyCode == Keys.S) { e.Handled = true; await OnSaveVoucherAsync(false); }
            else if (e.Alt && e.KeyCode == Keys.N) { e.Handled = true; ResetForm(); }
            else if (e.Control && e.KeyCode == Keys.P) { e.Handled = true; _btnPrint.PerformClick(); }
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                if (_flyoutPanel.Visible) _flyoutPanel.Visible = false;
                else Close();
            }
            else if (e.KeyCode == Keys.F2) { e.Handled = true; _dtpVoucherDate.Focus(); }
            else if (e.KeyCode == Keys.F4) { e.Handled = true; Close(); _navigationService?.OpenContraVoucher(); }
            else if (e.KeyCode == Keys.F6) { e.Handled = true; Close(); _navigationService?.OpenReceiptVoucher(); }
            else if (e.KeyCode == Keys.F7) { e.Handled = true; Close(); _navigationService?.OpenJournalVoucher(); }
            else if (e.KeyCode == Keys.F8) { e.Handled = true; Close(); _navigationService?.OpenSalesVoucher(); }
            else if (e.KeyCode == Keys.F9) { e.Handled = true; Close(); _navigationService?.OpenPurchaseVoucher(); }
        };

        Load += async (s, e) => await OnFormLoadAsync();
    }

    private Guna2Button CreateActionButton(string text, bool isPrimary, Action onClick)
    {
        var btn = new Guna2Button
        {
            Text = text,
            AutoSize = false,
            Size = new Size(TextRenderer.MeasureText(text, ExecLedgerTheme.UIRegular8).Width + 24, ExecLedgerTheme.ToolbarButtonHeight),
            BorderRadius = ExecLedgerTheme.BorderRadius,
            Font = isPrimary ? ExecLedgerTheme.UIBold8 : ExecLedgerTheme.UIRegular8,
            Margin = new Padding(2, 0, 4, 0),
            Cursor = Cursors.Hand
        };

        if (isPrimary)
        {
            btn.FillColor = ExecLedgerTheme.PrimaryNavy;
            btn.ForeColor = ExecLedgerTheme.WhiteText;
            btn.BorderColor = ExecLedgerTheme.DeepNavy;
            btn.BorderThickness = 1;
            btn.HoverState.FillColor = ExecLedgerTheme.ButtonHover;
            btn.HoverState.ForeColor = ExecLedgerTheme.WhiteText;
        }
        else
        {
            btn.FillColor = ExecLedgerTheme.WorkSurface;
            btn.ForeColor = ExecLedgerTheme.PrimaryText;
            btn.BorderColor = ExecLedgerTheme.PrimaryBorder;
            btn.BorderThickness = 1;
            btn.HoverState.FillColor = ExecLedgerTheme.MenuHover;
        }

        btn.Click += (s, e) => onClick();
        return btn;
    }

    private void OnFlyoutLedgerSelected(LedgerSummaryDto? ledger)
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

    private async Task OnFormLoadAsync()
    {
        if (_companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select an active company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Close();
            return;
        }

        _isInitializing = true;
        try
        {
            var companyId = _companyContext.CurrentCompany.CompanyId;
            var fy = _companyContext.CurrentFinancialYear;

            _topHeaderBar.SetCompany(_companyContext.CurrentCompany.CompanyName);

            _dtpVoucherDate.Value = DateTime.Today >= fy.StartDate && DateTime.Today <= fy.EndDate
                ? DateTime.Today
                : fy.StartDate;

            _paymentVoucherType = await _accountingService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

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
            MessageBox.Show($"Failed to initialize payment voucher: {ex.Message}", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isInitializing = false;
        }

        await OnAccountSelectedAsync();
    }

    private async Task UpdateVoucherNumberPreviewAsync()
    {
        if (_companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null || _paymentVoucherType == null) return;

        try
        {
            var preview = await _accountingService.GetNextVoucherNumberPreviewAsync(
                _companyContext.CurrentCompany.CompanyId,
                _paymentVoucherType.VoucherTypeId,
                _companyContext.CurrentFinancialYear.FinancialYearId);

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
        if (_companyContext.CurrentCompany == null || _cmbAccount.SelectedValue == null) return;

        if (_cmbAccount.SelectedValue is int ledgerId)
        {
            try
            {
                var bal = await _accountingService.GetLedgerBalanceAsync(_companyContext.CurrentCompany.CompanyId, ledgerId);
                _lblAccountBalance.Text = $"{bal.FormattedClosingBalance}";
                _lblAccountBalance.ForeColor = bal.ClosingBalance >= 0 ? ExecLedgerTheme.SuccessGreen : ExecLedgerTheme.ErrorRed;
            }
            catch
            {
                _lblAccountBalance.Text = "0.00";
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
            if (cellValue is int ledgerId && _companyContext.CurrentCompany != null)
            {
                try
                {
                    var bal = await _accountingService.GetLedgerBalanceAsync(_companyContext.CurrentCompany.CompanyId, ledgerId);
                    row.Cells["ColBalance"].Value = bal.FormattedClosingBalance;
                }
                catch
                {
                    row.Cells["ColBalance"].Value = "0.00";
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
        decimal totalDebit = 0m;

        foreach (DataGridViewRow row in _dgvEntries.Rows)
        {
            if (row.IsNewRow) continue;

            var val = row.Cells["ColAmount"].Value;
            if (val != null && decimal.TryParse(val.ToString(), out var amt) && amt > 0)
            {
                totalDebit += amt;
            }
        }

        _lblTotalAmount.Text = ExecLedgerTheme.FormatCurrency(totalDebit);

        if (totalDebit > 0)
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
        if (_companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null || _paymentVoucherType == null)
        {
            MessageBox.Show("Company and Financial Year must be active.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_cmbAccount.SelectedValue == null || !(_cmbAccount.SelectedValue is int creditLedgerId))
        {
            MessageBox.Show("Please select a valid Cash/Bank paying account.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _cmbAccount.Focus();
            return;
        }

        var entries = new List<VoucherEntryDto>();
        decimal totalDebit = 0m;

        foreach (DataGridViewRow row in _dgvEntries.Rows)
        {
            if (row.IsNewRow) continue;

            var ledgerVal = row.Cells["ColLedger"].Value;
            var amountVal = row.Cells["ColAmount"].Value;
            var narrationVal = row.Cells["ColNarration"].Value?.ToString() ?? string.Empty;

            if (ledgerVal is int debitLedgerId && amountVal != null && decimal.TryParse(amountVal.ToString(), out var amount) && amount > 0)
            {
                var entry = new VoucherEntryDto
                {
                    LedgerId = debitLedgerId,
                    Debit = amount,
                    Credit = 0m,
                    Narration = narrationVal
                };

                // Attach Bill-wise allocation (Agst Ref)
                entry.BillAllocations.Add(new BillAllocationCreateDto
                {
                    LedgerId = debitLedgerId,
                    BillType = BillType.AgstRef,
                    BillName = $"PMT-{DateTime.Today:yyyyMMdd}",
                    Amount = amount
                });

                entries.Add(entry);
                totalDebit += amount;
            }
        }

        if (entries.Count == 0 || totalDebit <= 0)
        {
            MessageBox.Show("Please enter at least one debit line item with an amount greater than 0.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Add matching Credit entry for the paying Cash/Bank account
        entries.Add(new VoucherEntryDto
        {
            LedgerId = creditLedgerId,
            Debit = 0m,
            Credit = totalDebit,
            Narration = _txtNarration.Text.Trim()
        });

        var createDto = new VoucherCreateDto
        {
            FinancialYearId = _companyContext.CurrentFinancialYear.FinancialYearId,
            VoucherTypeId = _paymentVoucherType.VoucherTypeId,
            VoucherDate = _dtpVoucherDate.Value,
            Narration = _txtNarration.Text.Trim(),
            Entries = entries
        };

        try
        {
            _btnAccept.Enabled = false;

            var saved = await _accountingService.SaveVoucherAsync(_companyContext.CurrentCompany.CompanyId, createDto);

            MessageBox.Show(
                $"Payment voucher {saved.VoucherNumber} of {totalDebit:N2} saved successfully.",
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
