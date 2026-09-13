using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Controls;

/// <summary>
/// Executive Ledger voucher entry control — reusable accounting grid with
/// header fields, entry grid (Guna2DataGridView), totals footer, and balance state indicator.
/// Used by all 8 voucher entry forms.
/// </summary>
public class VoucherEntryControl : UserControl
{
    // Header controls
    private ComboBox cmbVoucherType = null!;
    private TextBox txtVoucherNumber = null!;
    private DateTimePicker dtpVoucherDate = null!;
    private TextBox txtReferenceNumber = null!;
    private TextBox txtNarration = null!;

    // Grid
    private Guna2DataGridView dgvEntries = null!;

    // Footer controls
    private Label lblTotalDebit = null!;
    private Label lblTotalCredit = null!;
    private Label lblDifference = null!;
    private Panel pnlBalanceIndicator = null!;
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
        this.Font = ExecLedgerTheme.UIRegular9;
        this.Dock = DockStyle.Fill;
        this.BackColor = ExecLedgerTheme.ApplicationCanvas;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(8)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));   // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));   // Totals Footer
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));   // Balance Indicator

        // ═══════════════════════════════════════════════════════
        //  1. HEADER — Compact voucher info panel
        // ═══════════════════════════════════════════════════════
        var pnlHeader = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            FillColor = ExecLedgerTheme.WorkSurface,
            BorderColor = ExecLedgerTheme.PrimaryBorder,
            BorderThickness = 1,
            BorderRadius = ExecLedgerTheme.BorderRadius,
            Padding = new Padding(8, 6, 8, 6)
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 2,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));  // Type label
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));   // Type combo
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75));  // No label
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15));   // No text
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55));  // Date label
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));   // Date picker
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 55));  // Ref label
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));   // Ref text

        // Row 0: Type, Number, Date, Reference
        var lblType = CreateFieldLabel("Type:");
        cmbVoucherType = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = ExecLedgerTheme.UIRegular9,
            FlatStyle = FlatStyle.Flat,
            Margin = new Padding(2)
        };

        var lblNo = CreateFieldLabel("Voucher No:");
        txtVoucherNumber = new TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = ExecLedgerTheme.InputReadOnlyBg,
            Font = ExecLedgerTheme.MonoRegular9,
            ForeColor = ExecLedgerTheme.PrimaryText,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(2)
        };

        var lblDate = CreateFieldLabel("Date:");
        dtpVoucherDate = new DateTimePicker
        {
            Dock = DockStyle.Fill,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MMM-yyyy",
            Font = ExecLedgerTheme.MonoRegular9,
            Margin = new Padding(2)
        };

        var lblRef = CreateFieldLabel("Ref No:");
        txtReferenceNumber = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = ExecLedgerTheme.UIRegular9,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(2)
        };

        headerLayout.Controls.Add(lblType, 0, 0);
        headerLayout.Controls.Add(cmbVoucherType, 1, 0);
        headerLayout.Controls.Add(lblNo, 2, 0);
        headerLayout.Controls.Add(txtVoucherNumber, 3, 0);
        headerLayout.Controls.Add(lblDate, 4, 0);
        headerLayout.Controls.Add(dtpVoucherDate, 5, 0);
        headerLayout.Controls.Add(lblRef, 6, 0);
        headerLayout.Controls.Add(txtReferenceNumber, 7, 0);

        // Row 1: Narration (spans full width)
        var lblNarr = CreateFieldLabel("Narration:");
        txtNarration = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = ExecLedgerTheme.UIRegular9,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(2)
        };

        headerLayout.Controls.Add(lblNarr, 0, 1);
        headerLayout.SetColumnSpan(txtNarration, 7);
        headerLayout.Controls.Add(txtNarration, 1, 1);

        pnlHeader.Controls.Add(headerLayout);
        mainLayout.Controls.Add(pnlHeader, 0, 0);

        // ═══════════════════════════════════════════════════════
        //  2. GRID — Guna2DataGridView with accounting style
        // ═══════════════════════════════════════════════════════
        var pnlGrid = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 4) };
        dgvEntries = new Guna2DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = true,
            AllowUserToDeleteRows = true,
            EditMode = DataGridViewEditMode.EditOnEnter
        };
        ExecLedgerStyler.StyleGrid(dgvEntries, false);
        dgvEntries.AllowUserToAddRows = true;
        dgvEntries.AllowUserToDeleteRows = true;
        dgvEntries.ReadOnly = false;
        dgvEntries.SelectionMode = DataGridViewSelectionMode.CellSelect;

        SetupGridColumns();

        dgvEntries.CellValueChanged += (s, e) => RecalculateTotals();
        dgvEntries.RowsRemoved += (s, e) => RecalculateTotals();
        dgvEntries.KeyDown += DgvEntries_KeyDown;

        // Active cell focus border styling
        dgvEntries.CellPainting += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                e.Paint(e.CellBounds, DataGridViewPaintParts.All);

                if (dgvEntries.CurrentCell != null &&
                    e.RowIndex == dgvEntries.CurrentCell.RowIndex &&
                    e.ColumnIndex == dgvEntries.CurrentCell.ColumnIndex)
                {
                    using var pen = new Pen(ExecLedgerTheme.SystemFocusBlue, 2);
                    var rect = e.CellBounds;
                    rect.Inflate(-1, -1);
                    e.Graphics.DrawRectangle(pen, rect);
                }

                e.Handled = true;
            }
        };

        pnlGrid.Controls.Add(dgvEntries);
        mainLayout.Controls.Add(pnlGrid, 0, 1);

        // ═══════════════════════════════════════════════════════
        //  3. TOTALS FOOTER — Double-rule accounting footer
        // ═══════════════════════════════════════════════════════
        var pnlFooter = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            FillColor = ExecLedgerTheme.ApplicationCanvas,
            BorderColor = ExecLedgerTheme.PrimaryBorder,
            BorderThickness = 1,
            BorderRadius = ExecLedgerTheme.BorderRadius,
            Padding = new Padding(8, 0, 8, 0)
        };

        // Double-rule top border
        pnlFooter.Paint += (s, e) =>
        {
            using var pen = new Pen(ExecLedgerTheme.PrimaryBorder, 1);
            e.Graphics.DrawLine(pen, 4, 1, pnlFooter.Width - 4, 1);
            e.Graphics.DrawLine(pen, 4, 4, pnlFooter.Width - 4, 4);
        };

        var footerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 2,
            Padding = new Padding(4, 8, 4, 4)
        };
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
        footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        lblTotalDebit = new Label
        {
            Text = "TOTAL DEBIT    0.00",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = ExecLedgerTheme.MonoBold9,
            ForeColor = ExecLedgerTheme.PrimaryText
        };
        lblTotalCredit = new Label
        {
            Text = "TOTAL CREDIT   0.00",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = ExecLedgerTheme.MonoBold9,
            ForeColor = ExecLedgerTheme.PrimaryText
        };
        lblDifference = new Label
        {
            Text = "VARIANCE       0.00",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = ExecLedgerTheme.MonoBold9,
            ForeColor = ExecLedgerTheme.SecondaryText
        };

        lblBalanceBadge = new Label
        {
            Text = "▲ NOT BALANCED",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = ExecLedgerTheme.UIBold9,
            BackColor = ExecLedgerTheme.ErrorBg,
            ForeColor = ExecLedgerTheme.ErrorRed
        };

        footerLayout.Controls.Add(lblTotalDebit, 0, 0);
        footerLayout.Controls.Add(lblTotalCredit, 1, 0);
        footerLayout.Controls.Add(lblDifference, 2, 0);
        footerLayout.Controls.Add(lblBalanceBadge, 3, 0);

        pnlFooter.Controls.Add(footerLayout);
        mainLayout.Controls.Add(pnlFooter, 0, 2);

        // ═══════════════════════════════════════════════════════
        //  4. BALANCE INDICATOR — Full-width bar
        // ═══════════════════════════════════════════════════════
        pnlBalanceIndicator = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ExecLedgerTheme.ErrorBg,
            Padding = new Padding(8, 0, 8, 0)
        };
        pnlBalanceIndicator.Paint += (s, e) =>
        {
            using var pen = new Pen(IsBalanced ? ExecLedgerTheme.SuccessBorder : ExecLedgerTheme.ErrorBorder, 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlBalanceIndicator.Width, 0);
        };

        var lblIndicator = new Label
        {
            Text = "▲ OUT OF BALANCE — Debit and Credit totals must match before posting",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = ExecLedgerTheme.UIBold8,
            ForeColor = ExecLedgerTheme.ErrorRed
        };
        pnlBalanceIndicator.Controls.Add(lblIndicator);
        mainLayout.Controls.Add(pnlBalanceIndicator, 0, 3);

        this.Controls.Add(mainLayout);
        this.ResumeLayout(false);
    }

    private Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = ExecLedgerTheme.UIBold8,
            ForeColor = ExecLedgerTheme.SecondaryText,
            Margin = new Padding(2)
        };
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
            Width = 150,
            DefaultCellStyle = new DataGridViewCellStyle
            {
                Alignment = DataGridViewContentAlignment.MiddleRight,
                Format = "N2",
                Font = ExecLedgerTheme.MonoRegular9,
                Padding = new Padding(4, 0, 8, 0)
            }
        };

        var colCredit = new DataGridViewTextBoxColumn
        {
            Name = "colCredit",
            HeaderText = "Credit (Cr)",
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
            Name = "colNarration",
            HeaderText = "Line Narration",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            DefaultCellStyle = new DataGridViewCellStyle { Font = ExecLedgerTheme.UIRegular9 }
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

        lblTotalDebit.Text = $"TOTAL DEBIT    {ExecLedgerTheme.FormatCurrency(TotalDebit)}";
        lblTotalCredit.Text = $"TOTAL CREDIT   {ExecLedgerTheme.FormatCurrency(TotalCredit)}";

        var (varText, varColor) = ExecLedgerTheme.FormatBalance(TotalDebit - TotalCredit);
        lblDifference.Text = $"VARIANCE       {varText}";
        lblDifference.ForeColor = varColor;

        if (IsBalanced)
        {
            lblBalanceBadge.Text = "● BALANCED";
            lblBalanceBadge.BackColor = ExecLedgerTheme.SuccessBg;
            lblBalanceBadge.ForeColor = ExecLedgerTheme.SuccessGreen;

            pnlBalanceIndicator.BackColor = ExecLedgerTheme.SuccessBg;
            if (pnlBalanceIndicator.Controls.Count > 0 && pnlBalanceIndicator.Controls[0] is Label lbl)
            {
                lbl.Text = "● BALANCED — Ready to post";
                lbl.ForeColor = ExecLedgerTheme.SuccessGreen;
            }
        }
        else
        {
            lblBalanceBadge.Text = "▲ NOT BALANCED";
            lblBalanceBadge.BackColor = ExecLedgerTheme.ErrorBg;
            lblBalanceBadge.ForeColor = ExecLedgerTheme.ErrorRed;

            pnlBalanceIndicator.BackColor = ExecLedgerTheme.ErrorBg;
            if (pnlBalanceIndicator.Controls.Count > 0 && pnlBalanceIndicator.Controls[0] is Label lbl)
            {
                lbl.Text = $"▲ OUT OF BALANCE — Variance: {ExecLedgerTheme.FormatCurrency(Difference)}";
                lbl.ForeColor = ExecLedgerTheme.ErrorRed;
            }
        }

        pnlBalanceIndicator.Invalidate();
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
