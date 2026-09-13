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

using Guna.UI2.WinForms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

public class JournalVoucherForm : Form
{
    private readonly IAccountingService _accountingService;
    private readonly ILedgerService _ledgerService;
    private readonly ICompanyContext _companyContext;

    private VoucherType? _journalVoucherType;
    private IReadOnlyList<LedgerSummaryDto> _allLedgers = new List<LedgerSummaryDto>();

    // UI Controls
    private Label _lblVoucherNumber = null!;
    private DateTimePicker _dtpVoucherDate = null!;
    private TextBox _txtRefNo = null!;
    private Guna2DataGridView _dgvEntries = null!;
    private TextBox _txtNarration = null!;
    private Label _lblTotalDebit = null!;
    private Label _lblTotalCredit = null!;
    private Label _lblDifference = null!;
    private Label _lblBalanceStatus = null!;
    private Button _btnSave = null!;
    private Button _btnSaveAndNew = null!;
    private Button _btnNew = null!;
    private Button _btnPrint = null!;
    private Button _btnCancel = null!;
    private bool _isInitializing;

    public JournalVoucherForm(
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
        Text = "Journal Voucher (F7) — Adjustment & Transfer Entries";
        Size = new Size(980, 680);
        StartPosition = FormStartPosition.CenterParent;
        Font = ExecLedgerTheme.UIRegular9;
        KeyPreview = true;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(15)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 75));  // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));  // Narration & Summary
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Buttons

        // 1. Header Panel
        var pnlHeader = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1,
            BackColor = ExecLedgerTheme.SecondarySurface,
            Padding = new Padding(10)
        };
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        pnlHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));

        pnlHeader.Controls.Add(new Label { Text = "Voucher No:", AutoSize = true, Anchor = AnchorStyles.Left, Font = ExecLedgerTheme.UIBold10 }, 0, 0);
        _lblVoucherNumber = new Label { Text = "JRN-00001", AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = ExecLedgerTheme.PrimaryNavy, Font = ExecLedgerTheme.UIBold10 };
        pnlHeader.Controls.Add(_lblVoucherNumber, 1, 0);

        pnlHeader.Controls.Add(new Label { Text = "Date:", AutoSize = true, Anchor = AnchorStyles.Left }, 2, 0);
        _dtpVoucherDate = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Width = 135 };
        pnlHeader.Controls.Add(_dtpVoucherDate, 3, 0);

        pnlHeader.Controls.Add(new Label { Text = "Ref No:", AutoSize = true, Anchor = AnchorStyles.Left }, 4, 0);
        _txtRefNo = new TextBox { Width = 140, Font = ExecLedgerTheme.UIRegular9 };
        pnlHeader.Controls.Add(_txtRefNo, 5, 0);

        // 2. DataGridView for Line Items (Dual Dr / Cr entries)
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
        _dgvEntries.CellValueChanged += async (s, e) => await OnGridCellValueChangedAsync(e.RowIndex, e.ColumnIndex);
        _dgvEntries.RowsRemoved += (s, e) => RecalculateTotals();

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

        pnlSummary.Controls.Add(new Label { Text = "Voucher Narration:", AutoSize = true }, 0, 0);

        var pnlTotals = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };
        _lblTotalCredit = new Label { Text = "Credit: ₹0.00", Font = ExecLedgerTheme.UIBold10, ForeColor = ExecLedgerTheme.PrimaryNavy, AutoSize = true, Margin = new Padding(15, 0, 0, 0) };
        _lblTotalDebit = new Label { Text = "Debit: ₹0.00", Font = ExecLedgerTheme.UIBold10, ForeColor = ExecLedgerTheme.PrimaryNavy, AutoSize = true };
        pnlTotals.Controls.Add(_lblTotalCredit);
        pnlTotals.Controls.Add(_lblTotalDebit);
        pnlSummary.Controls.Add(pnlTotals, 1, 0);

        _txtNarration = new TextBox { Dock = DockStyle.Fill, Font = ExecLedgerTheme.UIRegular9, Multiline = true, Height = 40 };
        pnlSummary.Controls.Add(_txtNarration, 0, 1);
        pnlSummary.SetRowSpan(_txtNarration, 2);

        var pnlStatus = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true
        };
        _lblDifference = new Label { Text = "Diff: ₹0.00", Font = ExecLedgerTheme.UIBold10, ForeColor = Color.DimGray, AutoSize = true, Margin = new Padding(15, 0, 0, 0) };
        _lblBalanceStatus = new Label { Text = "Enter entries", Font = ExecLedgerTheme.UIBold10, ForeColor = Color.DarkOrange, AutoSize = true };
        pnlStatus.Controls.Add(_lblDifference);
        pnlStatus.Controls.Add(_lblBalanceStatus);
        pnlSummary.Controls.Add(pnlStatus, 1, 1);

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
            BackColor = ExecLedgerTheme.PrimaryNavy,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Height = 34,
            Width = 130,
            Font = ExecLedgerTheme.UIBold9,
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
            Font = ExecLedgerTheme.UIBold9,
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
        _isInitializing = true;
        try
        {
            UseWaitCursor = true;

            var company = _companyContext.CurrentCompany;
            var fy = _companyContext.CurrentFinancialYear;

            if (company == null || fy == null)
            {
                MessageBox.Show("No active company or financial year selected.", "Executive Ledger", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Close();
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
            UseWaitCursor = false;
        }
    }

    private async Task RefreshVoucherNumberPreviewAsync()
    {
        if (_journalVoucherType == null || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
            return;

        var nextNumber = await _accountingService.GetNextVoucherNumberPreviewAsync(
            _companyContext.CurrentCompany.CompanyId,
            _journalVoucherType.VoucherTypeId,
            _companyContext.CurrentFinancialYear.FinancialYearId);

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
            var company = _companyContext.CurrentCompany;
            var fy = _companyContext.CurrentFinancialYear;

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

            UseWaitCursor = true;
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
                Close();
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
            UseWaitCursor = false;
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
