using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Forms;

public class DashboardForm : Form
{
    private readonly IDashboardService _dashboardService;
    private readonly IAccountingService _accountingService;
    private readonly ICompanyContext _companyContext;

    private DashboardDto? _currentDashboard;

    // Header Controls
    private Label _lblCompanyTitle = null!;
    private Label _lblFyBadge = null!;
    private DateTimePicker _dtpAsOfDate = null!;
    private Button _btnRefresh = null!;
    private Button _btnPrint = null!;
    private Button _btnExport = null!;
    private Button _btnClose = null!;

    // KPI Cards
    private Panel _cardLiquidity = null!;
    private Label _lblLiquidTotal = null!;
    private Label _lblCashBal = null!;
    private Label _lblBankBal = null!;

    private Panel _cardWorkingCapital = null!;
    private Label _lblWorkingCapital = null!;
    private Label _lblReceivables = null!;
    private Label _lblPayables = null!;

    private Panel _cardProfitability = null!;
    private Label _lblNetProfit = null!;
    private Label _lblSales = null!;
    private Label _lblPurchases = null!;

    private Panel _cardInventory = null!;
    private Label _lblStockValuation = null!;
    private Label _lblStockCount = null!;

    // Content Tabs & Grids
    private TabControl _tabDetails = null!;
    private DataGridView _dgvTrends = null!;
    private DataGridView _dgvDebtors = null!;
    private DataGridView _dgvCreditors = null!;
    private DataGridView _dgvRecentVouchers = null!;

    // Navigation Action Handler
    public Action<string>? OnNavigateRequested { get; set; }

    public DashboardForm(
        IDashboardService dashboardService,
        IAccountingService accountingService,
        ICompanyContext companyContext)
    {
        _dashboardService = dashboardService;
        _accountingService = accountingService;
        _companyContext = companyContext;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Executive Dashboard — MoneyFlow Accounting";
        Size = new Size(1180, 750);
        MinimumSize = new Size(1000, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = Color.FromArgb(244, 246, 249);
        KeyPreview = true;

        // 1. Top Header Bar
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(18, 52, 86),
            Padding = new Padding(15, 10, 15, 10)
        };

        _lblCompanyTitle = new Label
        {
            Text = "MoneyFlow Executive Dashboard",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(15, 12)
        };

        _lblFyBadge = new Label
        {
            Text = "Financial Year: 2026-27",
            ForeColor = Color.FromArgb(176, 206, 238),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            AutoSize = true,
            Location = new Point(16, 38)
        };

        var lblAsOf = new Label
        {
            Text = "As of Date (F2):",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(540, 24)
        };

        _dtpAsOfDate = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MMM-yyyy",
            Width = 120,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(645, 20)
        };
        _dtpAsOfDate.ValueChanged += async (s, e) => await LoadDashboardDataAsync();

        _btnRefresh = new Button
        {
            Text = "Refresh (F5)",
            Width = 95,
            Height = 28,
            BackColor = Color.FromArgb(41, 128, 185),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(780, 20)
        };
        _btnRefresh.FlatAppearance.BorderSize = 0;
        _btnRefresh.Click += async (s, e) => await LoadDashboardDataAsync();

        _btnPrint = new Button
        {
            Text = "Print (Ctrl+P)",
            Width = 95,
            Height = 28,
            BackColor = Color.FromArgb(52, 73, 94),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(885, 20)
        };
        _btnPrint.FlatAppearance.BorderSize = 0;
        _btnPrint.Click += (s, e) => PrintDashboard();

        _btnExport = new Button
        {
            Text = "Export CSV",
            Width = 85,
            Height = 28,
            BackColor = Color.FromArgb(39, 174, 96),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(988, 20)
        };
        _btnExport.FlatAppearance.BorderSize = 0;
        _btnExport.Click += (s, e) => ExportCsv();

        _btnClose = new Button
        {
            Text = "Close (Esc)",
            Width = 80,
            Height = 28,
            BackColor = Color.FromArgb(192, 57, 43),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(1080, 20)
        };
        _btnClose.FlatAppearance.BorderSize = 0;
        _btnClose.Click += (s, e) => Close();

        pnlHeader.Controls.AddRange(new Control[] {
            _lblCompanyTitle, _lblFyBadge, lblAsOf, _dtpAsOfDate,
            _btnRefresh, _btnPrint, _btnExport, _btnClose
        });

        // 2. KPI Cards Panel
        var pnlKpis = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 115,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(15, 10, 15, 5),
            BackColor = Color.FromArgb(244, 246, 249)
        };
        pnlKpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        pnlKpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        pnlKpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        pnlKpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));

        // Card 1: Liquid Funds
        _cardLiquidity = CreateCardPanel(
            "LIQUID FUNDS (CASH & BANK)",
            Color.FromArgb(41, 128, 185),
            out _lblLiquidTotal,
            out _lblCashBal,
            out _lblBankBal,
            "Click for Cash / Bank Book",
            () => OnNavigateRequested?.Invoke("CashBankBook"));

        // Card 2: Working Capital
        _cardWorkingCapital = CreateCardPanel(
            "WORKING CAPITAL (DEBTORS / CREDITORS)",
            Color.FromArgb(142, 68, 173),
            out _lblWorkingCapital,
            out _lblReceivables,
            out _lblPayables,
            "Click for Outstanding Analysis",
            () => OnNavigateRequested?.Invoke("Outstanding"));

        // Card 3: Profitability
        _cardProfitability = CreateCardPanel(
            "PROFITABILITY (FYTD NET)",
            Color.FromArgb(39, 174, 96),
            out _lblNetProfit,
            out _lblSales,
            out _lblPurchases,
            "Click for Profit & Loss Account",
            () => OnNavigateRequested?.Invoke("ProfitLoss"));

        // Card 4: Inventory
        _cardInventory = CreateCardPanel(
            "CLOSING STOCK VALUATION",
            Color.FromArgb(211, 84, 0),
            out _lblStockValuation,
            out _lblStockCount,
            out var dummySub,
            "Click for Stock Summary",
            () => OnNavigateRequested?.Invoke("StockSummary"));
        dummySub.Visible = false;

        pnlKpis.Controls.Add(_cardLiquidity, 0, 0);
        pnlKpis.Controls.Add(_cardWorkingCapital, 1, 0);
        pnlKpis.Controls.Add(_cardProfitability, 2, 0);
        pnlKpis.Controls.Add(_cardInventory, 3, 0);

        // 3. Quick Action Buttons Bar
        var pnlQuickActions = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            BackColor = Color.FromArgb(235, 240, 245),
            Padding = new Padding(15, 7, 15, 7)
        };

        var flowActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoScroll = true
        };

        void AddQuickBtn(string text, Color bg, Action act)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 30,
                BackColor = bg,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Margin = new Padding(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.Click += (s, e) => act();
            flowActions.Controls.Add(btn);
        }

        AddQuickBtn("+ Payment (F5)", Color.FromArgb(18, 52, 86), () => OnNavigateRequested?.Invoke("Payment"));
        AddQuickBtn("+ Receipt (F6)", Color.FromArgb(18, 52, 86), () => OnNavigateRequested?.Invoke("Receipt"));
        AddQuickBtn("+ Sales (F8)", Color.FromArgb(18, 52, 86), () => OnNavigateRequested?.Invoke("Sales"));
        AddQuickBtn("+ Purchase (F9)", Color.FromArgb(18, 52, 86), () => OnNavigateRequested?.Invoke("Purchase"));
        AddQuickBtn("Day Book", Color.FromArgb(52, 73, 94), () => OnNavigateRequested?.Invoke("DayBook"));
        AddQuickBtn("Trial Balance", Color.FromArgb(52, 73, 94), () => OnNavigateRequested?.Invoke("TrialBalance"));
        AddQuickBtn("Profit & Loss", Color.FromArgb(52, 73, 94), () => OnNavigateRequested?.Invoke("ProfitLoss"));
        AddQuickBtn("Balance Sheet", Color.FromArgb(52, 73, 94), () => OnNavigateRequested?.Invoke("BalanceSheet"));
        AddQuickBtn("Stock Summary", Color.FromArgb(52, 73, 94), () => OnNavigateRequested?.Invoke("StockSummary"));
        AddQuickBtn("Go To (Alt+G)", Color.FromArgb(41, 128, 185), () => OnNavigateRequested?.Invoke("GoTo"));

        pnlQuickActions.Controls.Add(flowActions);

        // 4. Central Tab Control
        _tabDetails = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point),
            Padding = new Point(12, 6)
        };

        // Tab 1: Monthly Trends
        var tabTrends = new TabPage("  Monthly Financial Activity & Trends  ");
        _dgvTrends = CreateStandardGrid();
        _dgvTrends.Columns.Add("Month", "Month");
        _dgvTrends.Columns.Add("Sales", "Sales (₹)");
        _dgvTrends.Columns.Add("Purchases", "Purchases (₹)");
        _dgvTrends.Columns.Add("Inflows", "Inflows / Receipts (₹)");
        _dgvTrends.Columns.Add("Outflows", "Outflows / Payments (₹)");

        _dgvTrends.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _dgvTrends.Columns[2].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _dgvTrends.Columns[3].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _dgvTrends.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        tabTrends.Controls.Add(_dgvTrends);

        // Tab 2: Top Parties
        var tabParties = new TabPage("  Top Debtors & Top Creditors  ");
        var splitParties = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 560
        };

        // Left: Debtors
        var pnlDebtors = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var lblDebtorsHeader = new Label
        {
            Text = "TOP SUNDRY DEBTORS (RECEIVABLES)",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(41, 128, 185),
            Dock = DockStyle.Top,
            Height = 25
        };
        _dgvDebtors = CreateStandardGrid();
        _dgvDebtors.Columns.Add("Party", "Customer / Debtor");
        _dgvDebtors.Columns.Add("Balance", "Outstanding (₹)");
        _dgvDebtors.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        pnlDebtors.Controls.Add(_dgvDebtors);
        pnlDebtors.Controls.Add(lblDebtorsHeader);
        splitParties.Panel1.Controls.Add(pnlDebtors);

        // Right: Creditors
        var pnlCreditors = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        var lblCreditorsHeader = new Label
        {
            Text = "TOP SUNDRY CREDITORS (PAYABLES)",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(142, 68, 173),
            Dock = DockStyle.Top,
            Height = 25
        };
        _dgvCreditors = CreateStandardGrid();
        _dgvCreditors.Columns.Add("Party", "Supplier / Creditor");
        _dgvCreditors.Columns.Add("Balance", "Outstanding (₹)");
        _dgvCreditors.Columns[1].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        pnlCreditors.Controls.Add(_dgvCreditors);
        pnlCreditors.Controls.Add(lblCreditorsHeader);
        splitParties.Panel2.Controls.Add(pnlCreditors);

        tabParties.Controls.Add(splitParties);

        // Tab 3: Recent Transactions
        var tabRecent = new TabPage("  Recent Transactions (Latest 10)  ");
        _dgvRecentVouchers = CreateStandardGrid();
        _dgvRecentVouchers.Columns.Add("Date", "Date");
        _dgvRecentVouchers.Columns.Add("VoucherNo", "Voucher No");
        _dgvRecentVouchers.Columns.Add("Type", "Voucher Type");
        _dgvRecentVouchers.Columns.Add("Particulars", "Particulars");
        _dgvRecentVouchers.Columns.Add("Amount", "Amount (₹)");
        _dgvRecentVouchers.Columns.Add("Narration", "Narration");

        _dgvRecentVouchers.Columns[4].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        _dgvRecentVouchers.CellDoubleClick += (s, e) => DrillDownSelectedVoucher();
        _dgvRecentVouchers.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                DrillDownSelectedVoucher();
            }
        };

        tabRecent.Controls.Add(_dgvRecentVouchers);

        _tabDetails.TabPages.AddRange(new TabPage[] { tabTrends, tabParties, tabRecent });

        var pnlContent = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15, 5, 15, 5),
            BackColor = Color.FromArgb(244, 246, 249)
        };
        pnlContent.Controls.Add(_tabDetails);

        Controls.Add(pnlContent);
        Controls.Add(pnlQuickActions);
        Controls.Add(pnlKpis);
        Controls.Add(pnlHeader);

        Load += async (s, e) =>
        {
            _dtpAsOfDate.Value = DateTime.Today;
            await LoadDashboardDataAsync();
        };
    }

    private Panel CreateCardPanel(string title, Color accentColor, out Label lblPrimary, out Label lblSub1, out Label lblSub2, string hint, Action? onClick = null)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(5),
            Padding = new Padding(10, 8, 10, 8),
            Cursor = Cursors.Hand
        };

        var pnlStripe = new Panel
        {
            Dock = DockStyle.Left,
            Width = 5,
            BackColor = accentColor
        };

        var lblTitle = new Label
        {
            Text = title,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(120, 140, 160),
            Dock = DockStyle.Top,
            Height = 18
        };

        lblPrimary = new Label
        {
            Text = "₹0.00",
            Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 30, 30),
            Dock = DockStyle.Top,
            Height = 28
        };

        lblSub1 = new Label
        {
            Text = "Cash: ₹0.00",
            Font = new Font("Segoe UI", 8.2F, FontStyle.Regular),
            ForeColor = Color.FromArgb(80, 90, 100),
            Dock = DockStyle.Top,
            Height = 16
        };

        lblSub2 = new Label
        {
            Text = "Bank: ₹0.00",
            Font = new Font("Segoe UI", 8.2F, FontStyle.Regular),
            ForeColor = Color.FromArgb(80, 90, 100),
            Dock = DockStyle.Top,
            Height = 16
        };

        var lblHint = new Label
        {
            Text = hint,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Italic),
            ForeColor = accentColor,
            Dock = DockStyle.Bottom,
            Height = 14,
            TextAlign = ContentAlignment.MiddleRight
        };

        card.Controls.AddRange(new Control[] { lblHint, lblSub2, lblSub1, lblPrimary, lblTitle, pnlStripe });

        // Forward click events from children
        void WireClick(Control c)
        {
            c.Cursor = Cursors.Hand;
            if (onClick != null)
            {
                c.Click += (s, e) => onClick();
            }
            foreach (Control child in c.Controls) WireClick(child);
        }
        WireClick(card);

        return card;
    }

    private DataGridView CreateStandardGrid()
    {
        var dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false
        };

        dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 240, 245);
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(40, 40, 40);
        dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        dgv.ColumnHeadersHeight = 30;

        dgv.DefaultCellStyle.Font = new Font("Segoe UI", 9F);
        dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(209, 232, 255);
        dgv.DefaultCellStyle.SelectionForeColor = Color.Black;
        dgv.RowTemplate.Height = 26;

        return dgv;
    }

    public async Task LoadDashboardDataAsync()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            MessageBox.Show("Please select an active company first.", "No Active Company", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Cursor = Cursors.WaitCursor;
        try
        {
            int companyId = _companyContext.CurrentCompany.CompanyId;
            var data = await _dashboardService.GetDashboardDataAsync(companyId, _dtpAsOfDate.Value.Date);
            _currentDashboard = data;

            _lblCompanyTitle.Text = $"{data.CompanyName} — Executive Dashboard";
            _lblFyBadge.Text = $"Financial Year: {data.FinancialYearLabel} | As of: {data.AsOfDate:dd-MMM-yyyy}";

            // 1. Update KPI Cards
            _lblLiquidTotal.Text = $"₹{data.TotalLiquidFunds:N2}";
            _lblCashBal.Text = $"Cash: ₹{data.CashBalance:N2}";
            _lblBankBal.Text = $"Bank: ₹{data.BankBalance:N2}";

            _lblWorkingCapital.Text = $"₹{data.NetWorkingCapital:N2}";
            _lblReceivables.Text = $"Debtors (Rec): ₹{data.TotalReceivables:N2}";
            _lblPayables.Text = $"Creditors (Pay): ₹{data.TotalPayables:N2}";

            _lblNetProfit.Text = $"₹{data.NetProfitOrLoss:N2}";
            _lblNetProfit.ForeColor = data.NetProfitOrLoss >= 0 ? Color.FromArgb(39, 174, 96) : Color.FromArgb(192, 57, 43);
            _lblSales.Text = $"Sales: ₹{data.TotalSales:N2}";
            _lblPurchases.Text = $"Purchases: ₹{data.TotalPurchases:N2}";

            _lblStockValuation.Text = $"₹{data.TotalStockValuation:N2}";
            _lblStockCount.Text = $"Active Stock Items: {data.TotalStockItemsCount}";

            // 2. Populate Monthly Trends
            _dgvTrends.Rows.Clear();
            foreach (var m in data.MonthlyTrends)
            {
                _dgvTrends.Rows.Add(
                    m.MonthLabel,
                    m.SalesAmount > 0 ? $"₹{m.SalesAmount:N2}" : "—",
                    m.PurchaseAmount > 0 ? $"₹{m.PurchaseAmount:N2}" : "—",
                    m.InflowsAmount > 0 ? $"₹{m.InflowsAmount:N2}" : "—",
                    m.OutflowsAmount > 0 ? $"₹{m.OutflowsAmount:N2}" : "—"
                );
            }

            // 3. Populate Top Debtors
            _dgvDebtors.Rows.Clear();
            foreach (var d in data.TopDebtors)
            {
                _dgvDebtors.Rows.Add(d.PartyName, $"₹{d.Balance:N2} {d.BalanceType}");
            }
            if (data.TopDebtors.Count == 0)
            {
                _dgvDebtors.Rows.Add("No outstanding debtors", "—");
            }

            // 4. Populate Top Creditors
            _dgvCreditors.Rows.Clear();
            foreach (var c in data.TopCreditors)
            {
                _dgvCreditors.Rows.Add(c.PartyName, $"₹{c.Balance:N2} {c.BalanceType}");
            }
            if (data.TopCreditors.Count == 0)
            {
                _dgvCreditors.Rows.Add("No outstanding creditors", "—");
            }

            // 5. Populate Recent Vouchers
            _dgvRecentVouchers.Rows.Clear();
            foreach (var v in data.RecentVouchers)
            {
                int rowIdx = _dgvRecentVouchers.Rows.Add(
                    v.VoucherDate.ToString("dd-MMM-yyyy"),
                    v.VoucherNumber,
                    v.VoucherTypeName,
                    v.Particulars,
                    $"₹{v.Amount:N2}",
                    v.Narration
                );
                _dgvRecentVouchers.Rows[rowIdx].Tag = v.VoucherId;
            }
            if (data.RecentVouchers.Count == 0)
            {
                _dgvRecentVouchers.Rows.Add("—", "—", "—", "No recent vouchers found", "—", "—");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading dashboard data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private async void DrillDownSelectedVoucher()
    {
        if (_dgvRecentVouchers.SelectedRows.Count == 0) return;
        var tag = _dgvRecentVouchers.SelectedRows[0].Tag;
        if (tag is int voucherId)
        {
            var voucher = await _accountingService.GetVoucherByIdAsync(voucherId);
            if (voucher != null)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Voucher No: {voucher.VoucherNumber}");
                sb.AppendLine($"Voucher Type: {voucher.VoucherType?.Name ?? "Voucher"}");
                sb.AppendLine($"Date: {voucher.VoucherDate:dd-MMM-yyyy}");
                sb.AppendLine($"Reference: {voucher.ReferenceNumber}");
                sb.AppendLine($"Narration: {voucher.Narration}");
                sb.AppendLine(new string('-', 50));
                sb.AppendLine("ENTRIES:");
                foreach (var entry in voucher.VoucherEntries)
                {
                    string drCr = entry.Debit > 0 ? $"Dr ₹{entry.Debit:N2}" : $"Cr ₹{entry.Credit:N2}";
                    sb.AppendLine($"  • {entry.Ledger?.LedgerName ?? "Account",-25} {drCr}");
                }
                MessageBox.Show(sb.ToString(), $"Voucher Audit Detail — {voucher.VoucherNumber}", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    private void ExportCsv()
    {
        if (_currentDashboard == null)
        {
            MessageBox.Show("No dashboard data available to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"Dashboard_{_currentDashboard.CompanyName}_{_currentDashboard.AsOfDate:yyyyMMdd}.csv"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"\"{_currentDashboard.CompanyName}\",\"Executive Dashboard Summary\"");
                sb.AppendLine($"\"Financial Year\",\"{_currentDashboard.FinancialYearLabel}\"");
                sb.AppendLine($"\"As of Date\",\"{_currentDashboard.AsOfDate:dd-MMM-yyyy}\"");
                sb.AppendLine();
                sb.AppendLine("\"KEY PERFORMANCE INDICATORS\",");
                sb.AppendLine($"\"Liquid Funds (Cash & Bank)\",\"{_currentDashboard.TotalLiquidFunds:F2}\"");
                sb.AppendLine($"\"Cash Balance\",\"{_currentDashboard.CashBalance:F2}\"");
                sb.AppendLine($"\"Bank Balance\",\"{_currentDashboard.BankBalance:F2}\"");
                sb.AppendLine($"\"Sundry Debtors (Receivables)\",\"{_currentDashboard.TotalReceivables:F2}\"");
                sb.AppendLine($"\"Sundry Creditors (Payables)\",\"{_currentDashboard.TotalPayables:F2}\"");
                sb.AppendLine($"\"Net Working Capital\",\"{_currentDashboard.NetWorkingCapital:F2}\"");
                sb.AppendLine($"\"Total Sales (FYTD)\",\"{_currentDashboard.TotalSales:F2}\"");
                sb.AppendLine($"\"Total Purchases (FYTD)\",\"{_currentDashboard.TotalPurchases:F2}\"");
                sb.AppendLine($"\"Net Profit / Loss (FYTD)\",\"{_currentDashboard.NetProfitOrLoss:F2}\"");
                sb.AppendLine($"\"Closing Stock Valuation\",\"{_currentDashboard.TotalStockValuation:F2}\"");
                sb.AppendLine();

                sb.AppendLine("\"MONTHLY TRENDS\",,,,");
                sb.AppendLine("\"Month\",\"Sales (₹)\",\"Purchases (₹)\",\"Inflows (₹)\",\"Outflows (₹)\"");
                foreach (var m in _currentDashboard.MonthlyTrends)
                {
                    sb.AppendLine($"\"{m.MonthLabel}\",\"{m.SalesAmount:F2}\",\"{m.PurchaseAmount:F2}\",\"{m.InflowsAmount:F2}\",\"{m.OutflowsAmount:F2}\"");
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show("Dashboard summary successfully exported to CSV.", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void PrintDashboard()
    {
        if (_currentDashboard == null)
        {
            MessageBox.Show("No dashboard data to print.", "Print", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var pd = new PrintDocument();
        pd.PrintPage += (s, ev) =>
        {
            var g = ev.Graphics!;
            float y = 50;
            var fontTitle = new Font("Segoe UI", 14F, FontStyle.Bold);
            var fontHeader = new Font("Segoe UI", 10F, FontStyle.Bold);
            var fontNormal = new Font("Segoe UI", 9F, FontStyle.Regular);
            var brush = Brushes.Black;

            g.DrawString(_currentDashboard.CompanyName, fontTitle, brush, 50, y);
            y += 24;
            g.DrawString($"EXECUTIVE DASHBOARD SUMMARY — AS OF {_currentDashboard.AsOfDate:dd-MMM-yyyy} (FY: {_currentDashboard.FinancialYearLabel})", fontHeader, brush, 50, y);
            y += 30;

            g.DrawLine(Pens.Black, 50, y, 750, y);
            y += 15;

            g.DrawString("FINANCIAL KPI SUMMARY", fontHeader, Brushes.DarkBlue, 50, y);
            y += 20;

            void DrawKpiLine(string label, string val)
            {
                g.DrawString(label, fontNormal, brush, 70, y);
                g.DrawString(val, fontNormal, brush, 400, y);
                y += 18;
            }

            DrawKpiLine("Total Liquid Funds (Cash & Bank):", $"₹{_currentDashboard.TotalLiquidFunds:N2}");
            DrawKpiLine("  • Cash in Hand:", $"₹{_currentDashboard.CashBalance:N2}");
            DrawKpiLine("  • Bank Accounts:", $"₹{_currentDashboard.BankBalance:N2}");
            DrawKpiLine("Sundry Debtors (Total Receivables):", $"₹{_currentDashboard.TotalReceivables:N2}");
            DrawKpiLine("Sundry Creditors (Total Payables):", $"₹{_currentDashboard.TotalPayables:N2}");
            DrawKpiLine("Net Working Position:", $"₹{_currentDashboard.NetWorkingCapital:N2}");
            DrawKpiLine("Total Sales (Year-to-Date):", $"₹{_currentDashboard.TotalSales:N2}");
            DrawKpiLine("Total Purchases (Year-to-Date):", $"₹{_currentDashboard.TotalPurchases:N2}");
            DrawKpiLine("Net Profit / Loss (Year-to-Date):", $"₹{_currentDashboard.NetProfitOrLoss:N2}");
            DrawKpiLine("Closing Stock Valuation:", $"₹{_currentDashboard.TotalStockValuation:N2} ({_currentDashboard.TotalStockItemsCount} items)");

            y += 15;
            g.DrawLine(Pens.Black, 50, y, 750, y);
            y += 15;

            g.DrawString("MONTHLY ACTIVITY TRENDS", fontHeader, Brushes.DarkBlue, 50, y);
            y += 20;

            g.DrawString("Month", fontHeader, brush, 70, y);
            g.DrawString("Sales (₹)", fontHeader, brush, 220, y);
            g.DrawString("Purchases (₹)", fontHeader, brush, 350, y);
            g.DrawString("Inflows (₹)", fontHeader, brush, 480, y);
            g.DrawString("Outflows (₹)", fontHeader, brush, 610, y);
            y += 20;

            foreach (var m in _currentDashboard.MonthlyTrends.Take(12))
            {
                g.DrawString(m.MonthLabel, fontNormal, brush, 70, y);
                g.DrawString($"₹{m.SalesAmount:N2}", fontNormal, brush, 220, y);
                g.DrawString($"₹{m.PurchaseAmount:N2}", fontNormal, brush, 350, y);
                g.DrawString($"₹{m.InflowsAmount:N2}", fontNormal, brush, 480, y);
                g.DrawString($"₹{m.OutflowsAmount:N2}", fontNormal, brush, 610, y);
                y += 18;
            }

            ev.HasMorePages = false;
        };

        using var dlg = new PrintPreviewDialog
        {
            Document = pd,
            Width = 900,
            Height = 700
        };
        dlg.ShowDialog(this);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }
        if (keyData == Keys.F2)
        {
            _dtpAsOfDate.Focus();
            return true;
        }
        if (keyData == Keys.F5)
        {
            _ = LoadDashboardDataAsync();
            return true;
        }
        if (keyData == (Keys.Control | Keys.P))
        {
            PrintDashboard();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
