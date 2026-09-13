using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;
using Guna.UI2.WinForms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

public class BankReconciliationForm : Form
{
    private readonly IBankReconciliationService _brsService;
    private readonly ILedgerService _ledgerService;
    private readonly ICompanyContext _companyContext;

    // State
    private List<LedgerSummaryDto> _bankLedgers = new();
    private BankReconciliationReportDto? _currentReport;

    // UI Controls
    private ComboBox cmbBankLedgers = null!;
    private DateTimePicker dtpFromDate = null!;
    private DateTimePicker dtpToDate = null!;
    private Button btnRefresh = null!;
    private Guna2DataGridView dgvTransactions = null!;

    // Summary labels
    private Label lblBookBalance = null!;
    private Label lblIssuedNotPresented = null!;
    private Label lblDepositedNotCleared = null!;
    private Label lblBankBalance = null!;

    // Action buttons
    private Button btnSave = null!;
    private Button btnSetToday = null!;
    private Button btnExportCsv = null!;

    public BankReconciliationForm(
        IBankReconciliationService brsService,
        ILedgerService ledgerService,
        ICompanyContext companyContext)
    {
        _brsService = brsService;
        _ledgerService = ledgerService;
        _companyContext = companyContext;

        InitializeComponent();
        LoadBankLedgersAsync();
    }

    private void InitializeComponent()
    {
        Text = "MoneyFlow Prime — Bank Reconciliation Statement (BRS)";
        Size = new Size(1180, 720);
        MinimumSize = new Size(980, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(240, 246, 252);
        Font = ExecLedgerTheme.UIRegular9;
        KeyPreview = true;

        // 1. Top Ribbon
        var pnlTopRibbon = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = ExecLedgerTheme.PrimaryNavy
        };
        var lblTitle = new Label
        {
            Text = "Bank Reconciliation Statement (BRS)",
            Font = ExecLedgerTheme.UIBold11,
            ForeColor = Color.White,
            Location = new Point(16, 6),
            AutoSize = true
        };
        pnlTopRibbon.Controls.Add(lblTitle);
        Controls.Add(pnlTopRibbon);

        // 2. Filter Bar
        var pnlFilter = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = Color.FromArgb(228, 238, 246),
            Padding = new Padding(15, 8, 15, 8)
        };

        var lblBank = new Label
        {
            Text = "Bank Account:",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            Location = new Point(15, 14),
            AutoSize = true
        };
        pnlFilter.Controls.Add(lblBank);

        cmbBankLedgers = new ComboBox
        {
            Location = new Point(110, 11),
            Width = 260,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = ExecLedgerTheme.UIRegular9
        };
        cmbBankLedgers.SelectedIndexChanged += async (s, e) => await LoadReconciliationAsync();
        pnlFilter.Controls.Add(cmbBankLedgers);

        var lblFrom = new Label
        {
            Text = "From:",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            Location = new Point(390, 14),
            AutoSize = true
        };
        pnlFilter.Controls.Add(lblFrom);

        dtpFromDate = new DateTimePicker
        {
            Location = new Point(435, 11),
            Width = 125,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MMM-yyyy",
            Value = _companyContext.CurrentFinancialYear?.StartDate ?? new DateTime(DateTime.Today.Year, 4, 1)
        };
        pnlFilter.Controls.Add(dtpFromDate);

        var lblTo = new Label
        {
            Text = "To:",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            Location = new Point(575, 14),
            AutoSize = true
        };
        pnlFilter.Controls.Add(lblTo);

        dtpToDate = new DateTimePicker
        {
            Location = new Point(605, 11),
            Width = 125,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MMM-yyyy",
            Value = DateTime.Today
        };
        pnlFilter.Controls.Add(dtpToDate);

        btnRefresh = new Button
        {
            Text = "Refresh (F5)",
            Location = new Point(745, 10),
            Size = new Size(110, 28),
            BackColor = ExecLedgerTheme.SteelBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnRefresh.FlatAppearance.BorderSize = 0;
        btnRefresh.Click += async (s, e) => await LoadReconciliationAsync();
        pnlFilter.Controls.Add(btnRefresh);

        Controls.Add(pnlFilter);

        // 3. Bottom Summary & Actions Panel
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 125,
            BackColor = Color.FromArgb(228, 238, 246),
            Padding = new Padding(15, 10, 15, 10)
        };
        BuildBottomSummary(pnlBottom);
        Controls.Add(pnlBottom);

        // 4. Data Grid
        dgvTransactions = new Guna2DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            Font = ExecLedgerTheme.UIRegular9,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            ColumnHeadersHeight = 28,
            EnableHeadersVisualStyles = false,
            SelectionMode = DataGridViewSelectionMode.CellSelect
        };

        dgvTransactions.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(216, 236, 248);
        dgvTransactions.ColumnHeadersDefaultCellStyle.ForeColor = ExecLedgerTheme.PrimaryNavy;
        dgvTransactions.ColumnHeadersDefaultCellStyle.Font = ExecLedgerTheme.UIBold9;

        // Columns
        dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "EntryId", Visible = false });
        dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date", Width = 100, ReadOnly = true });
        dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "Particulars", HeaderText = "Particulars", Width = 310, ReadOnly = true });
        dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "VchType", HeaderText = "Vch Type", Width = 110, ReadOnly = true });
        dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "VchNo", HeaderText = "Vch No.", Width = 110, ReadOnly = true });
        dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn { Name = "InstrumentNo", HeaderText = "Instrument / Chq No.", Width = 150 });
        dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Debit",
            HeaderText = "Debit (Deposits)",
            Width = 130,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        });
        dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Credit",
            HeaderText = "Credit (Payments)",
            Width = 130,
            ReadOnly = true,
            DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" }
        });
        dgvTransactions.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "BankDate",
            HeaderText = "Bank Date (DD-MMM-YYYY)",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
            DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(255, 252, 230) } // Warm yellow editable cell
        });

        dgvTransactions.CellValueChanged += (s, e) => RecalculateLiveSummary();
        Controls.Add(dgvTransactions);
        Controls.SetChildIndex(dgvTransactions, 1);

        KeyDown += OnKeyDown;
    }

    private void BuildBottomSummary(Panel pnl)
    {
        // Reconciliation Summary Box (Left)
        int x = 16;
        int y = 8;
        int lineSpacing = 22;

        lblBookBalance = new Label
        {
            Text = "Balance as per Company Books: ₹0.00",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            Location = new Point(x, y),
            AutoSize = true
        };
        pnl.Controls.Add(lblBookBalance);

        y += lineSpacing;
        lblIssuedNotPresented = new Label
        {
            Text = "Add: Cheques issued but not presented: ₹0.00",
            Font = ExecLedgerTheme.UIRegular9,
            ForeColor = Color.FromArgb(0, 110, 60),
            Location = new Point(x, y),
            AutoSize = true
        };
        pnl.Controls.Add(lblIssuedNotPresented);

        y += lineSpacing;
        lblDepositedNotCleared = new Label
        {
            Text = "Less: Cheques deposited but not cleared: ₹0.00",
            Font = ExecLedgerTheme.UIRegular9,
            ForeColor = Color.FromArgb(180, 20, 20),
            Location = new Point(x, y),
            AutoSize = true
        };
        pnl.Controls.Add(lblDepositedNotCleared);

        y += lineSpacing;
        lblBankBalance = new Label
        {
            Text = "Balance as per Bank: ₹0.00",
            Font = ExecLedgerTheme.UIBold10,
            ForeColor = ExecLedgerTheme.SteelBlue,
            Location = new Point(x, y),
            AutoSize = true
        };
        pnl.Controls.Add(lblBankBalance);

        // Action Buttons (Right)
        btnSave = new Button
        {
            Text = "Save Changes (Ctrl+A)",
            Location = new Point(pnl.Width - 450, 45),
            Size = new Size(160, 34),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            Font = ExecLedgerTheme.UIBold9,
            FlatStyle = FlatStyle.Flat
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += async (s, e) => await SaveChangesAsync();
        pnl.Controls.Add(btnSave);

        btnSetToday = new Button
        {
            Text = "Clear with Today (Space)",
            Location = new Point(pnl.Width - 280, 45),
            Size = new Size(160, 34),
            BackColor = ExecLedgerTheme.SteelBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        btnSetToday.FlatAppearance.BorderSize = 0;
        btnSetToday.Click += (s, e) => SetCurrentRowBankDate(DateTime.Today);
        pnl.Controls.Add(btnSetToday);

        btnExportCsv = new Button
        {
            Text = "Export BRS",
            Location = new Point(pnl.Width - 110, 45),
            Size = new Size(95, 34),
            BackColor = Color.FromArgb(226, 232, 240),
            FlatStyle = FlatStyle.Flat
        };
        btnExportCsv.FlatAppearance.BorderSize = 0;
        btnExportCsv.Click += (s, e) => ExportBrsCsv();
        pnl.Controls.Add(btnExportCsv);

        pnl.Resize += (s, e) =>
        {
            btnSave.Location = new Point(pnl.Width - 440, 45);
            btnSetToday.Location = new Point(pnl.Width - 270, 45);
            btnExportCsv.Location = new Point(pnl.Width - 100, 45);
        };
    }

    private async void LoadBankLedgersAsync()
    {
        if (!_companyContext.IsCompanyOpen) return;
        int compId = _companyContext.CurrentCompany!.CompanyId;

        var ledgers = await _ledgerService.GetLedgersByCompanyAsync(compId);
        // Find ledgers in "Bank Accounts" or "Bank OCC" or "Bank OD"
        _bankLedgers = ledgers
            .Where(l => l.GroupName.Contains("Bank", StringComparison.OrdinalIgnoreCase) || l.LedgerName.Contains("Bank", StringComparison.OrdinalIgnoreCase))
            .ToList();

        cmbBankLedgers.Items.Clear();
        foreach (var l in _bankLedgers)
        {
            cmbBankLedgers.Items.Add(new ComboBoxItem(l.LedgerId, l.LedgerName));
        }

        if (cmbBankLedgers.Items.Count > 0)
        {
            cmbBankLedgers.SelectedIndex = 0;
        }
        else
        {
            MessageBox.Show("No Bank Ledgers found in the active company. Please create a Bank Account ledger first.", "BRS", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async Task LoadReconciliationAsync()
    {
        if (cmbBankLedgers.SelectedItem is not ComboBoxItem item || !_companyContext.IsCompanyOpen) return;

        try
        {
            int bankId = item.Id;
            int compId = _companyContext.CurrentCompany!.CompanyId;

            _currentReport = await _brsService.GetBankReconciliationDataAsync(
                compId,
                bankId,
                dtpFromDate.Value,
                dtpToDate.Value);

            dgvTransactions.Rows.Clear();
            foreach (var tx in _currentReport.Transactions)
            {
                dgvTransactions.Rows.Add(
                    tx.VoucherEntryId,
                    tx.VoucherDate.ToString("dd-MMM-yyyy"),
                    tx.Particulars,
                    tx.VoucherTypeName,
                    tx.VoucherNumber,
                    tx.InstrumentNumber,
                    tx.Debit > 0 ? (object)tx.Debit : "",
                    tx.Credit > 0 ? (object)tx.Credit : "",
                    tx.BankDate?.ToString("dd-MMM-yyyy") ?? ""
                );
            }

            RecalculateLiveSummary();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load reconciliation data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void RecalculateLiveSummary()
    {
        if (_currentReport == null) return;

        decimal bookBalance = _currentReport.BalanceAsPerCompanyBooks;
        decimal chequesIssuedNotPresented = 0m;
        decimal chequesDepositedNotCleared = 0m;

        foreach (DataGridViewRow row in dgvTransactions.Rows)
        {
            if (row.IsNewRow) continue;

            string bankDateStr = row.Cells["BankDate"].Value?.ToString()?.Trim() ?? "";
            bool isCleared = DateTime.TryParse(bankDateStr, out var bankDt) && bankDt <= dtpToDate.Value.Date;

            if (!isCleared)
            {
                if (decimal.TryParse(row.Cells["Debit"].Value?.ToString(), out decimal debit) && debit > 0)
                {
                    chequesDepositedNotCleared += debit;
                }
                if (decimal.TryParse(row.Cells["Credit"].Value?.ToString(), out decimal credit) && credit > 0)
                {
                    chequesIssuedNotPresented += credit;
                }
            }
        }

        decimal netAdjustment = chequesIssuedNotPresented - chequesDepositedNotCleared;
        decimal bankBalance = bookBalance + netAdjustment;

        string drCrBook = bookBalance >= 0 ? "Dr" : "Cr";
        string drCrBank = bankBalance >= 0 ? "Dr" : "Cr";

        lblBookBalance.Text = $"Balance as per Company Books: ₹{Math.Abs(bookBalance):N2} {drCrBook}";
        lblIssuedNotPresented.Text = $"Add: Cheques issued but not presented: ₹{chequesIssuedNotPresented:N2}";
        lblDepositedNotCleared.Text = $"Less: Cheques deposited but not cleared: ₹{chequesDepositedNotCleared:N2}";
        lblBankBalance.Text = $"Balance as per Bank: ₹{Math.Abs(bankBalance):N2} {drCrBank}";
    }

    private void SetCurrentRowBankDate(DateTime date)
    {
        if (dgvTransactions.CurrentRow == null || dgvTransactions.CurrentRow.IsNewRow) return;

        var cell = dgvTransactions.CurrentRow.Cells["BankDate"];
        string current = cell.Value?.ToString()?.Trim() ?? "";

        // Toggle: if already set, clear it. If empty, set it.
        if (!string.IsNullOrEmpty(current))
        {
            cell.Value = "";
        }
        else
        {
            cell.Value = date.ToString("dd-MMM-yyyy");
        }
        RecalculateLiveSummary();
    }

    private async Task SaveChangesAsync()
    {
        if (!_companyContext.IsCompanyOpen) return;
        int compId = _companyContext.CurrentCompany!.CompanyId;

        var updates = new List<BankClearanceUpdateDto>();
        foreach (DataGridViewRow row in dgvTransactions.Rows)
        {
            if (row.IsNewRow) continue;
            int entryId = Convert.ToInt32(row.Cells["EntryId"].Value);
            string instrumentNo = row.Cells["InstrumentNo"].Value?.ToString() ?? "";
            string bankDateStr = row.Cells["BankDate"].Value?.ToString()?.Trim() ?? "";

            DateTime? bankDate = null;
            if (DateTime.TryParse(bankDateStr, out var dt))
            {
                bankDate = dt;
            }

            updates.Add(new BankClearanceUpdateDto
            {
                VoucherEntryId = entryId,
                BankDate = bankDate,
                InstrumentNumber = instrumentNo
            });
        }

        try
        {
            btnSave.Enabled = false;
            await _brsService.UpdateBankClearanceAsync(compId, updates);
            MessageBox.Show("Bank reconciliation changes saved successfully.", "BRS Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await LoadReconciliationAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save reconciliation: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnSave.Enabled = true;
        }
    }

    private void ExportBrsCsv()
    {
        if (_currentReport == null || dgvTransactions.Rows.Count == 0)
        {
            MessageBox.Show("No data available to export.", "BRS", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "CSV File (*.csv)|*.csv",
            FileName = $"BRS_{_currentReport.BankLedgerName}_{dtpToDate.Value:yyyyMMdd}.csv"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Bank Reconciliation Statement for {_currentReport.BankLedgerName}");
            sb.AppendLine($"As on: {dtpToDate.Value:dd-MMM-yyyy}");
            sb.AppendLine();
            sb.AppendLine("Date,Particulars,Voucher Type,Voucher No,Instrument No,Debit,Credit,Bank Date");

            foreach (DataGridViewRow row in dgvTransactions.Rows)
            {
                if (row.IsNewRow) continue;
                sb.AppendLine($"\"{row.Cells["Date"].Value}\",\"{row.Cells["Particulars"].Value}\",\"{row.Cells["VchType"].Value}\",\"{row.Cells["VchNo"].Value}\",\"{row.Cells["InstrumentNo"].Value}\",{row.Cells["Debit"].Value},{row.Cells["Credit"].Value},\"{row.Cells["BankDate"].Value}\"");
            }

            sb.AppendLine();
            sb.AppendLine($"\"{lblBookBalance.Text}\"");
            sb.AppendLine($"\"{lblIssuedNotPresented.Text}\"");
            sb.AppendLine($"\"{lblDepositedNotCleared.Text}\"");
            sb.AppendLine($"\"{lblBankBalance.Text}\"");

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show("BRS exported successfully.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F5)
        {
            e.Handled = true;
            _ = LoadReconciliationAsync();
        }
        else if (e.Control && e.KeyCode == Keys.A)
        {
            e.Handled = true;
            _ = SaveChangesAsync();
        }
        else if (e.KeyCode == Keys.Space && dgvTransactions.Focused)
        {
            e.Handled = true;
            SetCurrentRowBankDate(DateTime.Today);
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private record ComboBoxItem(int Id, string Text)
    {
        public override string ToString() => Text;
    }
}
