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
/// Executive Ledger Desktop — Journal Voucher View (F7).
/// Embedded view hosted inside the unified VoucherShell.
/// </summary>
public class JournalVoucherView : UserControl, IVoucherView
{
    private readonly IVoucherHost _host;
    private readonly IAccountingService _accountingService;
    private readonly ILedgerService _ledgerService;

    private VoucherType? _journalVoucherType;
    private IReadOnlyList<LedgerSummaryDto> _allLedgers = new List<LedgerSummaryDto>();

    private Label _lblVoucherTag = null!;
    private Label _lblVoucherNumber = null!;
    private DateTimePicker _dtpVoucherDate = null!;
    private TextBox _txtRefNo = null!;
    private Guna2DataGridView _dgvEntries = null!;
    private TextBox _txtNarration = null!;
    private Label _lblTotalDebit = null!;
    private Label _lblTotalCredit = null!;
    private Label _lblDifference = null!;
    private Label _lblBalanceStatus = null!;
    private Guna2Button _btnSave = null!;
    private Guna2Button _btnSaveAndNew = new();
    private Guna2Button _btnNew = null!;
    private Guna2Button _btnPrint = null!;
    private Guna2Button _btnCancel = null!;
    private bool _isInitializing;

    public VoucherTypeEnum VoucherType => VoucherTypeEnum.Journal;
    public string VoucherTitle => "Journal Voucher (F7)";
    public string ActiveVoucherKey => "Journal";
    public Control ContentControl => this;

    public JournalVoucherView(
        IVoucherHost host,
        IAccountingService accountingService,
        ILedgerService ledgerService)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _accountingService = accountingService ?? throw new ArgumentNullException(nameof(accountingService));
        _ledgerService = ledgerService ?? throw new ArgumentNullException(nameof(ledgerService));

        InitializeComponent();
    }

    public void FocusDefault()
    {
        if (_dgvEntries != null && _dgvEntries.Rows.Count > 0)
        {
            _dgvEntries.Focus();
            _dgvEntries.CurrentCell = _dgvEntries.Rows[0].Cells["ColLedger"];
        }
        else
        {
            _txtRefNo?.Focus();
        }
    }

    public void FocusDate() => _dtpVoucherDate?.Focus();

    public bool HasUnsavedChanges()
    {
        if (!string.IsNullOrWhiteSpace(_txtRefNo?.Text)) return true;
        if (!string.IsNullOrWhiteSpace(_txtNarration?.Text)) return true;
        if (_dgvEntries == null) return false;

        foreach (DataGridViewRow row in _dgvEntries.Rows)
        {
            if (row.IsNewRow) continue;
            if (row.Cells["ColLedger"]?.Value != null) return true;
            if (decimal.TryParse(row.Cells["ColDebit"]?.Value?.ToString(), out var dr) && dr > 0) return true;
            if (decimal.TryParse(row.Cells["ColCredit"]?.Value?.ToString(), out var cr) && cr > 0) return true;
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
        workLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // Ref No / Guidance row
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
            Text = "JOURNAL",
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
            Text = "JRN-00001",
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

        // --- Row 1: Ref No & Mode Guidance ---
        var pnlRefRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 2, 0, 2)
        };

        var lblRefTag = new Label
        {
            Text = "Reference / Bill No :",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.PrimaryText,
            AutoSize = true,
            Margin = new Padding(0, 5, 6, 0)
        };

        _txtRefNo = new TextBox
        {
            Width = 160,
            Font = ExecLedgerTheme.UIRegular9,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 2, 20, 0)
        };

        var lblGuidance = new Label
        {
            Text = "Adjustment & Transfer Entries (Double-Entry Debit & Credit)",
            Font = ExecLedgerTheme.UIRegular8,
            ForeColor = ExecLedgerTheme.SecondaryText,
            AutoSize = true,
            Margin = new Padding(0, 6, 0, 0)
        };

        pnlRefRow.Controls.Add(lblRefTag);
        pnlRefRow.Controls.Add(_txtRefNo);
        pnlRefRow.Controls.Add(lblGuidance);

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

        var colType = new DataGridViewComboBoxColumn
        {
            HeaderText = "Dr/Cr",
            Name = "ColType",
            Width = 70,
            FlatStyle = FlatStyle.Flat
        };
        colType.Items.AddRange("Dr", "Cr");

        var colLedger = new DataGridViewComboBoxColumn
        {
            HeaderText = "Particulars (Ledger A/c)",
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

        var colDebit = new DataGridViewTextBoxColumn
        {
            HeaderText = "Debit (₹)",
            Name = "ColDebit",
            Width = 120,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        };

        var colCredit = new DataGridViewTextBoxColumn
        {
            HeaderText = "Credit (₹)",
            Name = "ColCredit",
            Width = 120,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        };

        var colNarration = new DataGridViewTextBoxColumn
        {
            HeaderText = "Line Narration",
            Name = "ColNarration",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };

        _dgvEntries.Columns.AddRange(colType, colLedger, colBalance, colDebit, colCredit, colNarration);
        _dgvEntries.EditMode = DataGridViewEditMode.EditOnEnter;
        _dgvEntries.CellValueChanged += async (s, e) => await OnGridCellValueChangedAsync(e.RowIndex, e.ColumnIndex);
        _dgvEntries.RowsRemoved += (s, e) => RecalculateTotals();

        _dgvEntries.CellEnter += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == _dgvEntries.Columns["ColLedger"].Index)
            {
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

        _lblTotalCredit = new Label { Text = "Credit: ₹0.00", Font = ExecLedgerTheme.UIBold10, ForeColor = ExecLedgerTheme.PrimaryNavy, AutoSize = true, Margin = new Padding(12, 4, 0, 0) };
        _lblTotalDebit = new Label { Text = "Debit: ₹0.00", Font = ExecLedgerTheme.UIBold10, ForeColor = ExecLedgerTheme.PrimaryNavy, AutoSize = true, Margin = new Padding(12, 4, 0, 0) };
        _lblDifference = new Label { Text = "Diff: ₹0.00", Font = ExecLedgerTheme.UIBold10, ForeColor = Color.DimGray, AutoSize = true, Margin = new Padding(12, 4, 0, 0) };
        _lblBalanceStatus = new Label { Text = "Enter entries", Font = ExecLedgerTheme.UIBold9, ForeColor = Color.DarkOrange, AutoSize = true, Margin = new Padding(0, 4, 10, 0) };

        pnlTotals.Controls.Add(_lblTotalCredit);
        pnlTotals.Controls.Add(_lblTotalDebit);
        pnlTotals.Controls.Add(_lblDifference);
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
        workLayout.Controls.Add(pnlRefRow, 0, 1);
        workLayout.Controls.Add(_dgvEntries, 0, 2);
        workLayout.Controls.Add(pnlSummary, 0, 3);
        workLayout.Controls.Add(pnlBottomRibbon, 0, 4);

        Controls.Add(workLayout);

        _host.FlyoutPanel.LedgerSelected += OnFlyoutLedgerSelected;
    }

    public void OnFlyoutLedgerSelected(LedgerSummaryDto? ledger)
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
                    var typeVal = _dgvEntries.Rows[rowIdx].Cells["ColType"].Value?.ToString();
                    if (string.Equals(typeVal, "Cr", StringComparison.OrdinalIgnoreCase))
                    {
                        _dgvEntries.CurrentCell = _dgvEntries.Rows[rowIdx].Cells["ColCredit"];
                    }
                    else
                    {
                        _dgvEntries.CurrentCell = _dgvEntries.Rows[rowIdx].Cells["ColDebit"];
                    }
                }
            }
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

            // Set FY date range
            _dtpVoucherDate.MinDate = fy.StartDate;
            _dtpVoucherDate.MaxDate = fy.EndDate;
            _dtpVoucherDate.Value = DateTime.Today >= fy.StartDate && DateTime.Today <= fy.EndDate ? DateTime.Today : fy.StartDate;

            // Load Journal Voucher Type
            _journalVoucherType = await _accountingService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Journal);

            // Preview voucher number
            await RefreshVoucherNumberPreviewAsync();

            // Load all active ledgers for company
            _allLedgers = await _ledgerService.GetLedgersByCompanyAsync(company.CompanyId);

            // Populate Grid's Particulars Column
            var colLedger = (DataGridViewComboBoxColumn)_dgvEntries.Columns["ColLedger"];
            colLedger.DisplayMember = "LedgerName";
            colLedger.ValueMember = "LedgerId";
            colLedger.DataSource = _allLedgers.ToList();

            // Pre-add 2 initial rows: Row 0 as Dr, Row 1 as Cr
            _dgvEntries.Rows.Clear();
            var row0Idx = _dgvEntries.Rows.Add();
            _dgvEntries.Rows[row0Idx].Cells["ColType"].Value = "Dr";

            var row1Idx = _dgvEntries.Rows.Add();
            _dgvEntries.Rows[row1Idx].Cells["ColType"].Value = "Cr";

            RecalculateTotals();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to initialize Journal Voucher: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private async Task RefreshVoucherNumberPreviewAsync()
    {
        if (_journalVoucherType == null || _host.CompanyContext.CurrentCompany == null || _host.CompanyContext.CurrentFinancialYear == null)
            return;

        var nextNumber = await _accountingService.GetNextVoucherNumberPreviewAsync(
            _host.CompanyContext.CurrentCompany.CompanyId,
            _journalVoucherType.VoucherTypeId,
            _host.CompanyContext.CurrentFinancialYear.FinancialYearId);

        _lblVoucherNumber.Text = nextNumber;
    }

    private async Task OnGridCellValueChangedAsync(int rowIndex, int columnIndex)
    {
        if (_isInitializing) return;
        if (rowIndex < 0 || rowIndex >= _dgvEntries.Rows.Count) return;

        var row = _dgvEntries.Rows[rowIndex];

        // 1. If Ledger changed, update balance
        if (columnIndex == _dgvEntries.Columns["ColLedger"].Index)
        {
            if (row.Cells["ColLedger"].Value is int ledgerId && _host.CompanyContext.CurrentCompany != null)
            {
                var balance = await _accountingService.GetLedgerBalanceAsync(_host.CompanyContext.CurrentCompany.CompanyId, ledgerId);
                row.Cells["ColBalance"].Value = balance.ClosingBalanceDisplay;
            }
            else
            {
                row.Cells["ColBalance"].Value = string.Empty;
            }
        }

        // 2. Mutual exclusivity of Debit and Credit
        if (columnIndex == _dgvEntries.Columns["ColDebit"].Index)
        {
            if (decimal.TryParse(row.Cells["ColDebit"].Value?.ToString(), out var drAmt) && drAmt > 0)
            {
                row.Cells["ColCredit"].Value = null;
                row.Cells["ColType"].Value = "Dr";
            }
        }
        else if (columnIndex == _dgvEntries.Columns["ColCredit"].Index)
        {
            if (decimal.TryParse(row.Cells["ColCredit"].Value?.ToString(), out var crAmt) && crAmt > 0)
            {
                row.Cells["ColDebit"].Value = null;
                row.Cells["ColType"].Value = "Cr";
            }
        }
        else if (columnIndex == _dgvEntries.Columns["ColType"].Index)
        {
            var typeVal = row.Cells["ColType"].Value?.ToString();
            if (typeVal == "Dr" && decimal.TryParse(row.Cells["ColCredit"].Value?.ToString(), out var crVal) && crVal > 0)
            {
                row.Cells["ColDebit"].Value = crVal;
                row.Cells["ColCredit"].Value = null;
            }
            else if (typeVal == "Cr" && decimal.TryParse(row.Cells["ColDebit"].Value?.ToString(), out var drVal) && drVal > 0)
            {
                row.Cells["ColCredit"].Value = drVal;
                row.Cells["ColDebit"].Value = null;
            }
        }

        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        decimal totalDebit = 0;
        decimal totalCredit = 0;

        foreach (DataGridViewRow row in _dgvEntries.Rows)
        {
            if (row.IsNewRow) continue;

            if (decimal.TryParse(row.Cells["ColDebit"].Value?.ToString(), out var dr) && dr > 0)
            {
                totalDebit += dr;
            }

            if (decimal.TryParse(row.Cells["ColCredit"].Value?.ToString(), out var cr) && cr > 0)
            {
                totalCredit += cr;
            }
        }

        decimal difference = Math.Abs(totalDebit - totalCredit);

        _lblTotalDebit.Text = $"Debit: ₹{totalDebit:N2}";
        _lblTotalCredit.Text = $"Credit: ₹{totalCredit:N2}";
        _lblDifference.Text = $"Diff: ₹{difference:N2}";

        if (totalDebit > 0 && totalCredit > 0 && difference == 0)
        {
            _lblBalanceStatus.Text = "BALANCED";
            _lblBalanceStatus.ForeColor = Color.FromArgb(16, 185, 129);
            _lblDifference.ForeColor = Color.FromArgb(16, 185, 129);
            _btnSave.Enabled = true;
            _btnSaveAndNew.Enabled = true;
        }
        else if (totalDebit == 0 && totalCredit == 0)
        {
            _lblBalanceStatus.Text = "Enter entries";
            _lblBalanceStatus.ForeColor = Color.DarkOrange;
            _lblDifference.ForeColor = Color.DimGray;
            _btnSave.Enabled = false;
            _btnSaveAndNew.Enabled = false;
        }
        else
        {
            _lblBalanceStatus.Text = "NOT BALANCED";
            _lblBalanceStatus.ForeColor = Color.DarkRed;
            _lblDifference.ForeColor = Color.DarkRed;
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

            if (_journalVoucherType == null)
            {
                MessageBox.Show("Journal voucher type not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var entries = new List<VoucherEntryDto>();

            for (int i = 0; i < _dgvEntries.Rows.Count; i++)
            {
                var row = _dgvEntries.Rows[i];
                if (row.IsNewRow) continue;

                if (row.Cells["ColLedger"].Value is not int ledgerId || ledgerId <= 0)
                {
                    continue;
                }

                decimal debit = 0;
                decimal credit = 0;

                if (decimal.TryParse(row.Cells["ColDebit"].Value?.ToString(), out var dr)) debit = dr;
                if (decimal.TryParse(row.Cells["ColCredit"].Value?.ToString(), out var cr)) credit = cr;

                if (debit <= 0 && credit <= 0)
                {
                    continue;
                }

                if (debit > 0 && credit > 0)
                {
                    MessageBox.Show($"Row {i + 1}: An entry cannot have both Debit and Credit amounts.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var lineNarration = row.Cells["ColNarration"].Value?.ToString()?.Trim() ?? string.Empty;

                entries.Add(new VoucherEntryDto
                {
                    LedgerId = ledgerId,
                    Debit = debit,
                    Credit = credit,
                    Narration = lineNarration
                });
            }

            if (entries.Count < 2)
            {
                MessageBox.Show("A journal voucher requires at least two accounting entries (at least one Debit and one Credit).", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            decimal totalDebit = entries.Sum(e => e.Debit);
            decimal totalCredit = entries.Sum(e => e.Credit);

            if (totalDebit != totalCredit || totalDebit <= 0)
            {
                MessageBox.Show($"Voucher is not balanced.\nTotal Debit: ₹{totalDebit:N2}\nTotal Credit: ₹{totalCredit:N2}\nDifference: ₹{Math.Abs(totalDebit - totalCredit):N2}", "Balance Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var voucherDto = new VoucherCreateDto
            {
                FinancialYearId = fy.FinancialYearId,
                VoucherTypeId = _journalVoucherType.VoucherTypeId,
                VoucherDate = _dtpVoucherDate.Value.Date,
                ReferenceNumber = _txtRefNo.Text.Trim(),
                Narration = _txtNarration.Text.Trim(),
                Entries = entries
            };

            _btnSave.Enabled = false;
            _btnSaveAndNew.Enabled = false;

            var savedVoucher = await _accountingService.SaveVoucherAsync(company.CompanyId, voucherDto);

            MessageBox.Show(
                $"Journal Voucher '{savedVoucher.VoucherNumber}' of ₹{totalDebit:N2} saved successfully.",
                "Journal Voucher Saved",
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
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save Journal Voucher:\n{ex.Message}", "Save Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        var row0Idx = _dgvEntries.Rows.Add();
        _dgvEntries.Rows[row0Idx].Cells["ColType"].Value = "Dr";

        var row1Idx = _dgvEntries.Rows.Add();
        _dgvEntries.Rows[row1Idx].Cells["ColType"].Value = "Cr";

        _txtRefNo.Clear();
        _txtNarration.Clear();
        RecalculateTotals();
    }
}
