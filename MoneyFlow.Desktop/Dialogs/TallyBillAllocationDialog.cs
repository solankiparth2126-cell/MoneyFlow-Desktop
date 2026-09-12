using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Enums;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Dialogs;

public class TallyBillAllocationDialog : Form
{
    private readonly decimal _targetAmount;
    private readonly string _ledgerName;
    private readonly IReadOnlyList<PendingBillDto> _pendingBills;
    private readonly List<BillAllocationCreateDto> _initialAllocations;

    private DataGridView dgvAllocations = null!;
    private Label lblHeaderInfo = null!;
    private Label lblTargetAmount = null!;
    private Label lblAllocatedAmount = null!;
    private Label lblRemainingAmount = null!;
    private Button btnAccept = null!;
    private Button btnCancel = null!;

    public List<BillAllocationCreateDto> Allocations { get; private set; } = new();

    public TallyBillAllocationDialog(
        string ledgerName,
        decimal targetAmount,
        IReadOnlyList<PendingBillDto> pendingBills,
        List<BillAllocationCreateDto>? existingAllocations = null)
    {
        _ledgerName = ledgerName;
        _targetAmount = Math.Abs(targetAmount);
        _pendingBills = pendingBills ?? new List<PendingBillDto>();
        _initialAllocations = existingAllocations ?? new List<BillAllocationCreateDto>();

        InitializeComponent();
        PopulateInitialRows();
    }

    private void InitializeComponent()
    {
        Text = $"Bill-wise Details — {_ledgerName}";
        Size = new Size(680, 420);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(240, 246, 252);
        Font = new Font("Segoe UI", 9.5F);
        KeyPreview = true;

        // Header Panel (Tally Deep Blue)
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(0, 56, 101)
        };
        lblHeaderInfo = new Label
        {
            Text = $"Bill-wise Details for: {_ledgerName}",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(14, 11),
            AutoSize = true
        };
        pnlHeader.Controls.Add(lblHeaderInfo);
        Controls.Add(pnlHeader);

        // Bottom Summary Panel
        var pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 90,
            BackColor = Color.FromArgb(228, 238, 246),
            Padding = new Padding(12)
        };

        lblTargetAmount = new Label
        {
            Text = $"Target Amount: ₹{_targetAmount:N2}",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 56, 101),
            Location = new Point(16, 10),
            AutoSize = true
        };
        pnlFooter.Controls.Add(lblTargetAmount);

        lblAllocatedAmount = new Label
        {
            Text = "Allocated: ₹0.00",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 120, 60),
            Location = new Point(230, 10),
            AutoSize = true
        };
        pnlFooter.Controls.Add(lblAllocatedAmount);

        lblRemainingAmount = new Label
        {
            Text = $"Remaining: ₹{_targetAmount:N2}",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(180, 20, 20),
            Location = new Point(440, 10),
            AutoSize = true
        };
        pnlFooter.Controls.Add(lblRemainingAmount);

        btnAccept = new Button
        {
            Text = "Accept (Ctrl+A)",
            Location = new Point(420, 45),
            Size = new Size(130, 32),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat
        };
        btnAccept.FlatAppearance.BorderSize = 0;
        btnAccept.Click += (s, e) => SaveAndClose();
        pnlFooter.Controls.Add(btnAccept);

        btnCancel = new Button
        {
            Text = "Cancel (Esc)",
            Location = new Point(560, 45),
            Size = new Size(95, 32),
            BackColor = Color.FromArgb(226, 232, 240),
            FlatStyle = FlatStyle.Flat
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
        pnlFooter.Controls.Add(btnCancel);

        Controls.Add(pnlFooter);

        // Grid (Tally Style)
        dgvAllocations = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AllowUserToResizeRows = false,
            MultiSelect = false,
            Font = new Font("Segoe UI", 9.5F),
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 30,
            EnableHeadersVisualStyles = false
        };

        dgvAllocations.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(216, 236, 248);
        dgvAllocations.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(0, 56, 101);
        dgvAllocations.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

        // Columns
        var colType = new DataGridViewComboBoxColumn
        {
            Name = "BillType",
            HeaderText = "Type of Ref",
            Width = 120,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
        };
        colType.Items.AddRange("Agst Ref", "New Ref", "Advance", "On Account");

        var colName = new DataGridViewTextBoxColumn
        {
            Name = "BillName",
            HeaderText = "Name / Bill No",
            Width = 180
        };

        var colDays = new DataGridViewTextBoxColumn
        {
            Name = "CreditDays",
            HeaderText = "Cr. Days",
            Width = 80
        };

        var colDueDate = new DataGridViewTextBoxColumn
        {
            Name = "DueDate",
            HeaderText = "Due Date",
            Width = 110
        };

        var colAmount = new DataGridViewTextBoxColumn
        {
            Name = "Amount",
            HeaderText = "Amount (₹)",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        };

        dgvAllocations.Columns.AddRange(colType, colName, colDays, colDueDate, colAmount);
        dgvAllocations.CellValueChanged += (s, e) => RecalculateTotals();
        dgvAllocations.RowsRemoved += (s, e) => RecalculateTotals();
        dgvAllocations.CellEndEdit += OnCellEndEdit;

        Controls.Add(dgvAllocations);
        Controls.SetChildIndex(dgvAllocations, 1);

        KeyDown += (s, e) =>
        {
            if (e.Control && e.KeyCode == Keys.A)
            {
                e.Handled = true;
                SaveAndClose();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                DialogResult = DialogResult.Cancel;
                Close();
            }
        };
    }

    private void PopulateInitialRows()
    {
        dgvAllocations.Rows.Clear();

        if (_initialAllocations.Count > 0)
        {
            foreach (var alloc in _initialAllocations)
            {
                string typeStr = alloc.BillType switch
                {
                    BillType.AgstRef => "Agst Ref",
                    BillType.Advance => "Advance",
                    BillType.OnAccount => "On Account",
                    _ => "New Ref"
                };

                dgvAllocations.Rows.Add(
                    typeStr,
                    alloc.BillName,
                    alloc.CreditDays?.ToString() ?? "",
                    alloc.DueDate?.ToString("dd-MMM-yyyy") ?? "",
                    alloc.Amount
                );
            }
        }
        else
        {
            // Auto-populate first line
            string defaultType = _pendingBills.Count > 0 ? "Agst Ref" : "New Ref";
            string defaultBill = _pendingBills.Count > 0 ? _pendingBills[0].BillName : "";
            decimal defaultAmt = _pendingBills.Count > 0 ? Math.Min(_targetAmount, _pendingBills[0].PendingAmount) : _targetAmount;
            string defaultDue = _pendingBills.Count > 0 ? _pendingBills[0].DueDate?.ToString("dd-MMM-yyyy") ?? "" : "";

            dgvAllocations.Rows.Add(defaultType, defaultBill, "", defaultDue, defaultAmt);
        }

        RecalculateTotals();
    }

    private void OnCellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var row = dgvAllocations.Rows[e.RowIndex];

        // If user selects "Agst Ref" and types or leaves bill name, help them pick pending bill
        if (e.ColumnIndex == 0) // BillType
        {
            string type = row.Cells["BillType"].Value?.ToString() ?? "";
            if (type == "Agst Ref" && _pendingBills.Count > 0 && string.IsNullOrWhiteSpace(row.Cells["BillName"].Value?.ToString()))
            {
                row.Cells["BillName"].Value = _pendingBills[0].BillName;
                row.Cells["Amount"].Value = Math.Min(_targetAmount, _pendingBills[0].PendingAmount);
                if (_pendingBills[0].DueDate.HasValue)
                {
                    row.Cells["DueDate"].Value = _pendingBills[0].DueDate.Value.ToString("dd-MMM-yyyy");
                }
            }
            else if (type == "On Account")
            {
                row.Cells["BillName"].Value = "On Account";
                row.Cells["Amount"].Value = GetRemainingToAllocate();
            }
        }
        else if (e.ColumnIndex == 2) // CreditDays
        {
            if (int.TryParse(row.Cells["CreditDays"].Value?.ToString(), out int days) && days > 0)
            {
                row.Cells["DueDate"].Value = DateTime.Today.AddDays(days).ToString("dd-MMM-yyyy");
            }
        }

        RecalculateTotals();
    }

    private decimal GetRemainingToAllocate()
    {
        decimal allocated = 0m;
        foreach (DataGridViewRow row in dgvAllocations.Rows)
        {
            if (row.IsNewRow) continue;
            if (decimal.TryParse(row.Cells["Amount"].Value?.ToString(), out decimal val))
            {
                allocated += val;
            }
        }
        return Math.Max(0, _targetAmount - allocated);
    }

    private void RecalculateTotals()
    {
        decimal allocated = 0m;
        foreach (DataGridViewRow row in dgvAllocations.Rows)
        {
            if (row.IsNewRow) continue;
            if (decimal.TryParse(row.Cells["Amount"].Value?.ToString(), out decimal val))
            {
                allocated += val;
            }
        }

        decimal remaining = _targetAmount - allocated;

        lblAllocatedAmount.Text = $"Allocated: ₹{allocated:N2}";
        lblRemainingAmount.Text = $"Remaining: ₹{remaining:N2}";
        lblRemainingAmount.ForeColor = Math.Abs(remaining) < 0.01m ? Color.FromArgb(0, 120, 60) : Color.FromArgb(180, 20, 20);
    }

    private void SaveAndClose()
    {
        Allocations.Clear();
        foreach (DataGridViewRow row in dgvAllocations.Rows)
        {
            if (row.IsNewRow) continue;

            string typeStr = row.Cells["BillType"].Value?.ToString() ?? "New Ref";
            string billName = row.Cells["BillName"].Value?.ToString()?.Trim() ?? string.Empty;
            decimal.TryParse(row.Cells["Amount"].Value?.ToString(), out decimal amount);

            if (amount <= 0) continue;

            DateTime? dueDate = null;
            if (DateTime.TryParse(row.Cells["DueDate"].Value?.ToString(), out var dt))
            {
                dueDate = dt;
            }

            int? creditDays = null;
            if (int.TryParse(row.Cells["CreditDays"].Value?.ToString(), out int days))
            {
                creditDays = days;
            }

            BillType billType = typeStr switch
            {
                "Agst Ref" => BillType.AgstRef,
                "Advance" => BillType.Advance,
                "On Account" => BillType.OnAccount,
                _ => BillType.NewRef
            };

            Allocations.Add(new BillAllocationCreateDto
            {
                BillType = billType,
                BillName = string.IsNullOrWhiteSpace(billName) ? "BILL" : billName,
                DueDate = dueDate,
                CreditDays = creditDays,
                Amount = amount
            });
        }

        if (Allocations.Count == 0)
        {
            // Default on account
            Allocations.Add(new BillAllocationCreateDto
            {
                BillType = BillType.OnAccount,
                BillName = "On Account",
                Amount = _targetAmount
            });
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
