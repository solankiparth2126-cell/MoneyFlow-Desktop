using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Controls;

public class VoucherEntryControl : UserControl
{
    // Header controls
    private ComboBox cmbVoucherType = null!;
    private TextBox txtVoucherNumber = null!;
    private DateTimePicker dtpVoucherDate = null!;
    private TextBox txtReferenceNumber = null!;
    private TextBox txtNarration = null!;

    // Grid
    private DataGridView dgvEntries = null!;

    // Footer controls
    private Label lblTotalDebit = null!;
    private Label lblTotalCredit = null!;
    private Label lblDifference = null!;
    private Label lblBalanceBadge = null!;

    // Data source cache
    private List<LedgerSummaryDto> _availableLedgers = new();
    private List<VoucherType> _availableVoucherTypes = new();

    public event EventHandler? BalanceStatusChanged;
    public event EventHandler? SaveRequested;

    public decimal TotalDebit { get; private set; }
    public decimal TotalCredit { get; private set; }
    public decimal Difference => Math.Abs(TotalDebit - TotalCredit);
    public bool IsBalanced => TotalDebit == TotalCredit && TotalDebit > 0m;

    public VoucherEntryControl()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.Font = new Font("Segoe UI", 9.5F);
        this.Dock = DockStyle.Fill;
        this.BackColor = ThemeManager.Colors.WindowBg;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));  // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 85));   // Footer

        // 1. HEADER GROUPBOX
        var grpHeader = new GroupBox
        {
            Text = "Voucher Details (Header)",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = ThemeManager.Colors.HeaderBg
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 2,
            Padding = new Padding(8, 4, 8, 4)
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100)); // Type Label
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));  // Type Combo
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));  // No Label
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));  // No Text
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // Date Label
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));  // Date Picker

        // Row 1
        var lblType = new Label { Text = "Voucher Type:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F) };
        cmbVoucherType = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5F) };
        
        var lblNo = new Label { Text = "Voucher No:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F) };
        txtVoucherNumber = new TextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = Color.FromArgb(245, 245, 245), Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };

        var lblDate = new Label { Text = "Date (F2):", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F) };
        dtpVoucherDate = new DateTimePicker { Dock = DockStyle.Fill, Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MM-yyyy", Font = new Font("Segoe UI", 9.5F) };

        headerLayout.Controls.Add(lblType, 0, 0);
        headerLayout.Controls.Add(cmbVoucherType, 1, 0);
        headerLayout.Controls.Add(lblNo, 2, 0);
        headerLayout.Controls.Add(txtVoucherNumber, 3, 0);
        headerLayout.Controls.Add(lblDate, 4, 0);
        headerLayout.Controls.Add(dtpVoucherDate, 5, 0);

        // Row 2: Ref No & Narration
        var lblRef = new Label { Text = "Ref / Invoice:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F) };
        txtReferenceNumber = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F) };

        var lblNarr = new Label { Text = "Narration:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F) };
        txtNarration = new TextBox { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F) };

        headerLayout.Controls.Add(lblRef, 0, 1);
        headerLayout.Controls.Add(txtReferenceNumber, 1, 1);
        headerLayout.Controls.Add(lblNarr, 2, 1);
        headerLayout.SetColumnSpan(txtNarration, 3);
        headerLayout.Controls.Add(txtNarration, 3, 1);

        grpHeader.Controls.Add(headerLayout);
        mainLayout.Controls.Add(grpHeader, 0, 0);

        // 2. GRID PANEL
        var pnlGrid = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 5, 0, 5) };
        dgvEntries = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            EditMode = DataGridViewEditMode.EditOnEnter
        };
        ThemeManager.StyleGrid(dgvEntries);

        SetupGridColumns();

        dgvEntries.CellValueChanged += (s, e) => RecalculateTotals();
        dgvEntries.RowsRemoved += (s, e) => RecalculateTotals();
        dgvEntries.KeyDown += DgvEntries_KeyDown;

        pnlGrid.Controls.Add(dgvEntries);
        mainLayout.Controls.Add(pnlGrid, 0, 1);

        // 3. FOOTER PANEL
        var grpFooter = new GroupBox
        {
            Text = "Summary & Balance Status",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = ThemeManager.Colors.HeaderBg
        };

        var footerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            Padding = new Padding(10, 5, 10, 5)
        };
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); // Debit
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); // Credit
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); // Diff
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); // Badge
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10));

        lblTotalDebit = new Label { Text = "Total Debit: ₹ 0.00", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(13, 110, 253) };
        lblTotalCredit = new Label { Text = "Total Credit: ₹ 0.00", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(102, 16, 242) };
        lblDifference = new Label { Text = "Difference: ₹ 0.00", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10F, FontStyle.Bold), ForeColor = Color.FromArgb(108, 117, 125) };

        lblBalanceBadge = new Label
        {
            Text = "NOT BALANCED",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            BackColor = ThemeManager.Colors.DangerBg,
            ForeColor = ThemeManager.Colors.DangerFg,
            BorderStyle = BorderStyle.FixedSingle
        };

        footerLayout.Controls.Add(lblTotalDebit, 0, 0);
        footerLayout.Controls.Add(lblTotalCredit, 1, 0);
        footerLayout.Controls.Add(lblDifference, 2, 0);
        footerLayout.Controls.Add(lblBalanceBadge, 3, 0);

        grpFooter.Controls.Add(footerLayout);
        mainLayout.Controls.Add(grpFooter, 0, 2);

        this.Controls.Add(mainLayout);
        this.ResumeLayout(false);
    }

    private void SetupGridColumns()
    {
        dgvEntries.Columns.Clear();

        var colLedger = new DataGridViewComboBoxColumn
        {
            Name = "colLedger",
            HeaderText = "Particulars (Ledger Account)",
            Width = 320,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
            FlatStyle = FlatStyle.Flat
        };

        var colDebit = new DataGridViewTextBoxColumn
        {
            Name = "colDebit",
            HeaderText = "Debit (Dr)",
            Width = 140,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        };

        var colCredit = new DataGridViewTextBoxColumn
        {
            Name = "colCredit",
            HeaderText = "Credit (Cr)",
            Width = 140,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        };

        var colNarration = new DataGridViewTextBoxColumn
        {
            Name = "colNarration",
            HeaderText = "Item Narration",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };

        dgvEntries.Columns.AddRange(colLedger, colDebit, colCredit, colNarration);
    }

    public void SetLedgers(IEnumerable<LedgerSummaryDto> ledgers)
    {
        _availableLedgers = ledgers.Where(l => l.IsActive).OrderBy(l => l.LedgerName).ToList();
        var colLedger = (DataGridViewComboBoxColumn)dgvEntries.Columns["colLedger"];
        colLedger.DataSource = _availableLedgers;
        colLedger.DisplayMember = "LedgerName";
        colLedger.ValueMember = "LedgerId";
    }

    public void SetVoucherTypes(IEnumerable<VoucherType> types, int? defaultTypeId = null)
    {
        _availableVoucherTypes = types.Where(t => t.IsActive).ToList();
        cmbVoucherType.DataSource = _availableVoucherTypes;
        cmbVoucherType.DisplayMember = "Name";
        cmbVoucherType.ValueMember = "VoucherTypeId";

        if (defaultTypeId.HasValue)
        {
            cmbVoucherType.SelectedValue = defaultTypeId.Value;
        }
    }

    public void SetVoucherNumber(string voucherNumber)
    {
        txtVoucherNumber.Text = voucherNumber;
    }

    public void SetVoucherDate(DateTime date)
    {
        dtpVoucherDate.Value = date;
    }

    public void RecalculateTotals()
    {
        decimal dr = 0m;
        decimal cr = 0m;

        foreach (DataGridViewRow row in dgvEntries.Rows)
        {
            if (row.IsNewRow) continue;

            if (row.Cells["colDebit"].Value != null &&
                decimal.TryParse(row.Cells["colDebit"].Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal debitVal))
            {
                dr += debitVal;
            }

            if (row.Cells["colCredit"].Value != null &&
                decimal.TryParse(row.Cells["colCredit"].Value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal creditVal))
            {
                cr += creditVal;
            }
        }

        TotalDebit = dr;
        TotalCredit = cr;

        lblTotalDebit.Text = $"Total Debit: ₹ {TotalDebit:N2}";
        lblTotalCredit.Text = $"Total Credit: ₹ {TotalCredit:N2}";
        lblDifference.Text = $"Difference: ₹ {Difference:N2}";

        if (IsBalanced)
        {
            lblBalanceBadge.Text = "BALANCED";
            lblBalanceBadge.BackColor = ThemeManager.Colors.SuccessBg;
            lblBalanceBadge.ForeColor = ThemeManager.Colors.SuccessFg;
        }
        else
        {
            lblBalanceBadge.Text = "NOT BALANCED";
            lblBalanceBadge.BackColor = ThemeManager.Colors.DangerBg;
            lblBalanceBadge.ForeColor = ThemeManager.Colors.DangerFg;
        }

        BalanceStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public VoucherCreateDto? GetVoucherData(int financialYearId)
    {
        if (cmbVoucherType.SelectedValue == null) return null;

        var dto = new VoucherCreateDto
        {
            FinancialYearId = financialYearId,
            VoucherTypeId = (int)cmbVoucherType.SelectedValue,
            VoucherDate = dtpVoucherDate.Value.Date,
            ReferenceNumber = txtReferenceNumber.Text.Trim(),
            Narration = txtNarration.Text.Trim(),
            Entries = new List<VoucherEntryDto>()
        };

        foreach (DataGridViewRow row in dgvEntries.Rows)
        {
            if (row.IsNewRow) continue;
            if (row.Cells["colLedger"].Value == null) continue;

            int ledgerId = Convert.ToInt32(row.Cells["colLedger"].Value);
            decimal.TryParse(row.Cells["colDebit"].Value?.ToString() ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal debit);
            decimal.TryParse(row.Cells["colCredit"].Value?.ToString() ?? "0", NumberStyles.Any, CultureInfo.InvariantCulture, out decimal credit);
            string rowNarr = row.Cells["colNarration"].Value?.ToString() ?? string.Empty;

            if (debit > 0m || credit > 0m)
            {
                dto.Entries.Add(new VoucherEntryDto
                {
                    LedgerId = ledgerId,
                    Debit = debit,
                    Credit = credit,
                    Narration = rowNarr
                });
            }
        }

        return dto;
    }

    public void Clear()
    {
        txtReferenceNumber.Clear();
        txtNarration.Clear();
        dgvEntries.Rows.Clear();
        RecalculateTotals();
    }

    private void DgvEntries_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Control && e.KeyCode == Keys.S)
        {
            SaveRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }
}
