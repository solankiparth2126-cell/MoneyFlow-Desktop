using System;
using System.Drawing;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;
using MoneyFlow.Services;

namespace MoneyFlow.Desktop.Forms;

public class MainForm : Form
{
    private readonly AppDbContext _context;
    private readonly IDatabaseSetupService _databaseSetupService;
    private readonly ICompanyService _companyService;
    private readonly ICompanyContext _companyContext;
    private readonly IFinancialYearService _fyService;
    private readonly IGroupService _groupService;
    private readonly ILedgerService _ledgerService;
    private readonly IAccountingService _accountingService;
    private readonly IInventoryService _inventoryService;
    private readonly ISearchService _searchService;
    private readonly IDashboardService _dashboardService;
    private readonly IImportExportService _importExportService;
    private readonly IBackupRestoreService _backupRestoreService;

    // Controls
    private MenuStrip menuStrip = null!;
    private ToolStrip toolStrip = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel lblStatusCompany = null!;
    private ToolStripStatusLabel lblStatusFY = null!;
    private ToolStripStatusLabel lblStatusDatabase = null!;

    // Gateway navigation panel
    private Panel gatewayPanel = null!;
    private ListBox lstGatewayMenu = null!;
    private Label lblCurrentCompany = null!;
    private Label lblCurrentFY = null!;

    public MainForm(
        AppDbContext context,
        IDatabaseSetupService databaseSetupService,
        ICompanyService companyService,
        ICompanyContext companyContext,
        IFinancialYearService fyService,
        IGroupService groupService,
        ILedgerService ledgerService,
        IAccountingService accountingService,
        IInventoryService inventoryService,
        ISearchService searchService,
        IDashboardService dashboardService,
        IImportExportService importExportService,
        IBackupRestoreService backupRestoreService)
    {
        _context = context;
        _databaseSetupService = databaseSetupService;
        _companyService = companyService;
        _companyContext = companyContext;
        _fyService = fyService;
        _groupService = groupService;
        _ledgerService = ledgerService;
        _accountingService = accountingService;
        _inventoryService = inventoryService;
        _searchService = searchService;
        _dashboardService = dashboardService;
        _importExportService = importExportService;
        _backupRestoreService = backupRestoreService;

        InitializeComponent();

        _companyContext.OnCompanyChanged += UpdateCompanyContextUI;
        UpdateCompanyContextUI();
    }

    private void InitializeComponent()
    {
        this.Text = "MoneyFlow Desktop ERP — Gateway of Accounting";
        this.Size = new Size(1024, 680);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.KeyPreview = true;
        this.BackColor = Color.FromArgb(240, 243, 246);
        this.Font = new Font("Segoe UI", 9.5F);

        // 1. MenuStrip
        menuStrip = new MenuStrip
        {
            BackColor = Color.FromArgb(24, 43, 73),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5F)
        };

        var menuCompany = new ToolStripMenuItem("&Company");
        menuCompany.DropDownItems.Add("Select Company (F3)", null, (s, e) => OpenCompanyList());
        menuCompany.DropDownItems.Add("Create Company", null, (s, e) => OpenCreateCompany());
        menuCompany.DropDownItems.Add("Alter Company", null, (s, e) => OpenAlterCompany());
        menuCompany.DropDownItems.Add("Change Financial Year (F2)", null, (s, e) => OpenFinancialYearList());
        menuCompany.DropDownItems.Add("Close Company", null, (s, e) => CloseActiveCompany());
        menuCompany.DropDownItems.Add(new ToolStripSeparator());
        menuCompany.DropDownItems.Add("E&xit (Esc)", null, (s, e) => Application.Exit());

        var menuMasters = new ToolStripMenuItem("&Masters");
        menuMasters.DropDownItems.Add("&Groups (Chart of Accounts)", null, (s, e) => OpenGroupList());
        menuMasters.DropDownItems.Add("&Ledgers", null, (s, e) => OpenLedgerList());
        menuMasters.DropDownItems.Add("&Stock Items", null, (s, e) => OpenStockItemList());
        menuMasters.DropDownItems.Add("&Units of Measure", null, (s, e) => OpenUnitList());

        var menuTransactions = new ToolStripMenuItem("&Transactions");
        menuTransactions.DropDownItems.Add("F4 - &Contra", null, (s, e) => OpenContraVoucher());
        menuTransactions.DropDownItems.Add("F5 - &Payment", null, (s, e) => OpenPaymentVoucher());
        menuTransactions.DropDownItems.Add("F6 - &Receipt", null, (s, e) => OpenReceiptVoucher());
        menuTransactions.DropDownItems.Add("F7 - &Journal", null, (s, e) => OpenJournalVoucher());
        menuTransactions.DropDownItems.Add("F8 - &Sales", null, (s, e) => OpenSalesVoucher());
        menuTransactions.DropDownItems.Add("F9 - &Purchase", null, (s, e) => OpenPurchaseVoucher());
        menuTransactions.DropDownItems.Add("&Debit Note (Ctrl+F9)", null, (s, e) => OpenDebitNote());
        menuTransactions.DropDownItems.Add("&Credit Note (Ctrl+F8)", null, (s, e) => OpenCreditNote());

        var menuReports = new ToolStripMenuItem("&Reports");
        menuReports.DropDownItems.Add("&Dashboard", null, (s, e) => OpenDashboard());
        menuReports.DropDownItems.Add(new ToolStripSeparator());
        menuReports.DropDownItems.Add("&Day Book", null, (s, e) => OpenDayBook());
        menuReports.DropDownItems.Add("&Ledger Statement", null, (s, e) => OpenLedgerStatement());
        menuReports.DropDownItems.Add("&Trial Balance", null, (s, e) => OpenTrialBalance());
        menuReports.DropDownItems.Add("&Profit & Loss", null, (s, e) => OpenProfitLoss());
        menuReports.DropDownItems.Add("&Balance Sheet", null, (s, e) => OpenBalanceSheet());
        menuReports.DropDownItems.Add("&Cash / Bank Book", null, (s, e) => OpenCashBankBook());
        menuReports.DropDownItems.Add("&Outstanding Analysis", null, (s, e) => OpenOutstandingReport());
        menuReports.DropDownItems.Add("&Stock Summary", null, (s, e) => OpenStockSummary());

        var menuUtilities = new ToolStripMenuItem("&Utilities");
        menuUtilities.DropDownItems.Add("&Global Search (Alt+G)", null, (s, e) => OpenGlobalSearch());
        menuUtilities.DropDownItems.Add("&Import / Export Data...", null, (s, e) => OpenImportExport());
        menuUtilities.DropDownItems.Add(new ToolStripSeparator());
        menuUtilities.DropDownItems.Add("&Backup & Restore System (F10)...", null, (s, e) => OpenBackupRestore());
        menuUtilities.DropDownItems.Add(new ToolStripSeparator());
        menuUtilities.DropDownItems.Add("Connection Diagnostics", null, (s, e) => OpenDatabaseDiagnostics());

        menuStrip.Items.AddRange(new ToolStripItem[] {
            menuCompany,
            menuMasters,
            menuTransactions,
            menuReports,
            menuUtilities
        });
        this.MainMenuStrip = menuStrip;
        this.Controls.Add(menuStrip);

        // 2. ToolStrip (Quick Action & Shortcuts)
        toolStrip = new ToolStrip
        {
            BackColor = Color.FromArgb(234, 240, 246),
            GripStyle = ToolStripGripStyle.Hidden,
            Font = new Font("Segoe UI", 9F)
        };
        toolStrip.Items.Add(new ToolStripLabel("Shortcuts: "));
        toolStrip.Items.Add(new ToolStripButton("Go To (Alt+G)", null, (s, e) => OpenGlobalSearch()) { BackColor = Color.FromArgb(24, 43, 73), ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
        toolStrip.Items.Add(new ToolStripButton("Dashboard", null, (s, e) => OpenDashboard()) { BackColor = Color.FromArgb(41, 128, 185), ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripButton("F2: Period / FY", null, (s, e) => OpenFinancialYearList()));
        toolStrip.Items.Add(new ToolStripButton("F3: Company", null, (s, e) => OpenCompanyList()));
        toolStrip.Items.Add(new ToolStripButton("F4: Contra", null, (s, e) => OpenContraVoucher()));
        toolStrip.Items.Add(new ToolStripButton("F5: Payment", null, (s, e) => OpenPaymentVoucher()));
        toolStrip.Items.Add(new ToolStripButton("F6: Receipt", null, (s, e) => OpenReceiptVoucher()));
        toolStrip.Items.Add(new ToolStripButton("F7: Journal", null, (s, e) => OpenJournalVoucher()));
        toolStrip.Items.Add(new ToolStripButton("F8: Sales", null, (s, e) => OpenSalesVoucher()));
        toolStrip.Items.Add(new ToolStripButton("F9: Purchase", null, (s, e) => OpenPurchaseVoucher()));
        toolStrip.Items.Add(new ToolStripButton("Debit Note (Ctrl+F9)", null, (s, e) => OpenDebitNote()));
        toolStrip.Items.Add(new ToolStripButton("Credit Note (Ctrl+F8)", null, (s, e) => OpenCreditNote()));
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripButton("F10: Backup / Restore", null, (s, e) => OpenBackupRestore()) { BackColor = Color.FromArgb(39, 174, 96), ForeColor = Color.White, Font = new Font("Segoe UI", 9F, FontStyle.Bold) });
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripButton("DB Diagnostics", null, (s, e) => OpenDatabaseDiagnostics()));
        this.Controls.Add(toolStrip);

        // 3. Central Gateway of Accounting Layout
        CreateGatewayLayout();

        // 4. StatusStrip
        statusStrip = new StatusStrip
        {
            BackColor = Color.FromArgb(24, 43, 73),
            ForeColor = Color.White
        };

        lblStatusCompany = new ToolStripStatusLabel("Company: [None Selected]") { ForeColor = Color.White };
        lblStatusFY = new ToolStripStatusLabel(" | FY: Not Selected") { ForeColor = Color.LightGreen };
        lblStatusDatabase = new ToolStripStatusLabel(" | DB: MoneyFlowDB (Connected)") { ForeColor = Color.LightSkyBlue };

        statusStrip.Items.AddRange(new ToolStripItem[] {
            lblStatusCompany,
            lblStatusFY,
            lblStatusDatabase
        });
        this.Controls.Add(statusStrip);

        // 5. Global Keyboard Shortcuts Handler
        this.KeyDown += MainForm_KeyDown;
    }

    private void CreateGatewayLayout()
    {
        var centerContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(30)
        };

        // Left Information Panel: Company Context
        var contextGroup = new GroupBox
        {
            Text = "CURRENT CONTEXT",
            Location = new Point(40, 30),
            Size = new Size(380, 480),
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 43, 73)
        };

        lblCurrentCompany = new Label
        {
            Text = "Current Company:\n[No Company Open — Select or Create Company]",
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(70, 80, 95),
            Location = new Point(20, 40),
            Size = new Size(340, 60)
        };

        lblCurrentFY = new Label
        {
            Text = "Current Financial Year:\n[None Selected]",
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(70, 80, 95),
            Location = new Point(20, 115),
            Size = new Size(340, 50)
        };

        var lblArchitecture = new Label
        {
            Text = "MONEYFLOW DESKTOP ERP\nVersion 1.0 (Phase 3: Company Management)\n\n• Standalone Windows PC\n• C# + .NET 8 + WinForms\n• SQL Server Express + EF Core\n• Pure Double-Entry Accounting\n• Multi-Company Isolation\n• Zero GST (Pure Accounting)",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(100, 110, 125),
            Location = new Point(20, 200),
            Size = new Size(340, 180)
        };

        contextGroup.Controls.Add(lblCurrentCompany);
        contextGroup.Controls.Add(lblCurrentFY);
        contextGroup.Controls.Add(lblArchitecture);
        centerContainer.Controls.Add(contextGroup);

        // Right Gateway Panel: Gateway of Accounting
        gatewayPanel = new Panel
        {
            Location = new Point(460, 30),
            Size = new Size(480, 480),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White
        };

        var headerGateway = new Panel
        {
            Dock = DockStyle.Top,
            Height = 45,
            BackColor = Color.FromArgb(24, 43, 73)
        };

        var lblGatewayTitle = new Label
        {
            Text = "GATEWAY OF ACCOUNTING",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            Location = new Point(15, 12),
            AutoSize = true
        };
        headerGateway.Controls.Add(lblGatewayTitle);
        gatewayPanel.Controls.Add(headerGateway);

        lstGatewayMenu = new ListBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11F),
            ItemHeight = 28,
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.Normal
        };

        lstGatewayMenu.Items.AddRange(new object[] {
            "  Go To / Search (Alt+G)",
            "  Company Info (Select / Create / Alter)",
            "  ---------------------------------",
            "  Groups (Chart of Accounts)",
            "  Ledgers",
            "  Stock Items",
            "  Units of Measure",
            "  Contra Voucher (F4)",
            "  Payment Voucher (F5)",
            "  Receipt Voucher (F6)",
            "  Journal Voucher (F7)",
            "  Sales Voucher (F8)",
            "  Purchase Voucher (F9)",
            "  Debit Note (Ctrl+F9)",
            "  Credit Note (Ctrl+F8)",
            "  Accounting Vouchers",
            "  ---------------------------------",
            "  Dashboard (Executive Overview)",
            "  Day Book",
            "  Trial Balance",
            "  Profit & Loss A/c",
            "  Balance Sheet",
            "  Cash / Bank Book",
            "  Outstanding Analysis",
            "  Stock Summary",
            "  ---------------------------------",
            "  Import / Export Data",
            "  Backup & Restore (F10)",
            "  Database Diagnostics",
            "  Quit (Esc)"
        });

        lstGatewayMenu.DoubleClick += (s, e) => HandleGatewaySelection();
        lstGatewayMenu.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                HandleGatewaySelection();
                e.Handled = true;
            }
        };

        gatewayPanel.Controls.Add(lstGatewayMenu);
        centerContainer.Controls.Add(gatewayPanel);

        this.Controls.Add(centerContainer);
    }

    private void UpdateCompanyContextUI()
    {
        if (_companyContext.IsCompanyOpen && _companyContext.CurrentCompany != null)
        {
            var company = _companyContext.CurrentCompany;
            var fy = _companyContext.CurrentFinancialYear;

            lblCurrentCompany.Text = $"Current Company:\n{company.CompanyName}\n{company.State}, {company.Country}";
            lblCurrentCompany.ForeColor = Color.FromArgb(16, 185, 129);

            lblCurrentFY.Text = fy != null 
                ? $"Current Financial Year:\n{fy.YearName} ({fy.StartDate:dd-MMM-yyyy} to {fy.EndDate:dd-MMM-yyyy})" 
                : "Current Financial Year:\nNot set";
            lblCurrentFY.ForeColor = Color.FromArgb(37, 99, 235);

            lblStatusCompany.Text = $"Company: {company.CompanyName}";
            lblStatusFY.Text = fy != null ? $" | FY: {fy.YearName}" : " | FY: Not set";
        }
        else
        {
            lblCurrentCompany.Text = "Current Company:\n[No Company Open — Select or Create Company]";
            lblCurrentCompany.ForeColor = Color.FromArgb(70, 80, 95);

            lblCurrentFY.Text = "Current Financial Year:\n[None Selected]";
            lblCurrentFY.ForeColor = Color.FromArgb(70, 80, 95);

            lblStatusCompany.Text = "Company: [None Selected]";
            lblStatusFY.Text = " | FY: Not Selected";
        }
    }

    private void HandleGatewaySelection()
    {
        var selected = lstGatewayMenu.SelectedItem?.ToString()?.Trim();
        if (string.IsNullOrEmpty(selected) || selected.StartsWith("-")) return;

        if (selected.Contains("Go To") || selected.Contains("Search"))
        {
            OpenGlobalSearch();
        }
        else if (selected.Contains("Company Info"))
        {
            OpenCompanyList();
        }
        else if (selected.Contains("Groups"))
        {
            OpenGroupList();
        }
        else if (selected.Contains("Ledgers"))
        {
            OpenLedgerList();
        }
        else if (selected.Contains("Stock Items"))
        {
            OpenStockItemList();
        }
        else if (selected.Contains("Units of Measure"))
        {
            OpenUnitList();
        }
        else if (selected.Contains("Contra"))
        {
            OpenContraVoucher();
        }
        else if (selected.Contains("Payment"))
        {
            OpenPaymentVoucher();
        }
        else if (selected.Contains("Receipt"))
        {
            OpenReceiptVoucher();
        }
        else if (selected.Contains("Journal"))
        {
            OpenJournalVoucher();
        }
        else if (selected.Contains("Sales"))
        {
            OpenSalesVoucher();
        }
        else if (selected.Contains("Purchase"))
        {
            OpenPurchaseVoucher();
        }
        else if (selected.Contains("Debit Note"))
        {
            OpenDebitNote();
        }
        else if (selected.Contains("Credit Note"))
        {
            OpenCreditNote();
        }
        else if (selected.Contains("Dashboard"))
        {
            OpenDashboard();
        }
        else if (selected.Contains("Day Book"))
        {
            OpenDayBook();
        }
        else if (selected.Contains("Ledger Statement"))
        {
            OpenLedgerStatement();
        }
        else if (selected.Contains("Trial Balance"))
        {
            OpenTrialBalance();
        }
        else if (selected.Contains("Profit & Loss"))
        {
            OpenProfitLoss();
        }
        else if (selected.Contains("Balance Sheet"))
        {
            OpenBalanceSheet();
        }
        else if (selected.Contains("Cash / Bank Book") || selected.Contains("Cash Book") || selected.Contains("Bank Book"))
        {
            OpenCashBankBook();
        }
        else if (selected.Contains("Outstanding"))
        {
            OpenOutstandingReport();
        }
        else if (selected.Contains("Stock Summary"))
        {
            OpenStockSummary();
        }
        else if (selected.Contains("Import") || selected.Contains("Export"))
        {
            OpenImportExport();
        }
        else if (selected.Contains("Backup") || selected.Contains("Restore"))
        {
            OpenBackupRestore();
        }
        else if (selected.Contains("Database Diagnostics"))
        {
            OpenDatabaseDiagnostics();
        }
        else if (selected.Contains("Quit"))
        {
            Application.Exit();
        }
        else
        {
            if (!_companyContext.IsCompanyOpen)
            {
                MessageBox.Show("Please select or create a company first.", "Company Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
                OpenCompanyList();
                return;
            }
            ShowNotImplemented(selected);
        }
    }

    private void OpenGroupList()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            MessageBox.Show("Please select or create a company first.", "Company Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var groupForm = new GroupListForm(_groupService, _companyContext);
        groupForm.ShowDialog(this);
    }

    private void OpenLedgerList()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            MessageBox.Show("Please select or create a company first.", "Company Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var ledgerForm = new LedgerListForm(_ledgerService, _groupService, _companyContext);
        ledgerForm.ShowDialog(this);
    }

    private void OpenPaymentVoucher()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select or create a company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var paymentForm = new PaymentVoucherForm(_accountingService, _ledgerService, _companyContext);
        paymentForm.ShowDialog(this);
    }

    private void OpenReceiptVoucher()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select or create a company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var receiptForm = new ReceiptVoucherForm(_accountingService, _ledgerService, _companyContext);
        receiptForm.ShowDialog(this);
    }

    private void OpenContraVoucher()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select or create a company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var contraForm = new ContraVoucherForm(_accountingService, _ledgerService, _companyContext);
        contraForm.ShowDialog(this);
    }

    private void OpenJournalVoucher()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select or create a company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var journalForm = new JournalVoucherForm(_accountingService, _ledgerService, _companyContext);
        journalForm.ShowDialog(this);
    }

    private void OpenSalesVoucher()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select or create a company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var salesForm = new SalesVoucherForm(_accountingService, _companyContext);
        salesForm.ShowDialog(this);
    }

    private void OpenPurchaseVoucher()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select or create a company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var purchaseForm = new PurchaseVoucherForm(_accountingService, _companyContext);
        purchaseForm.ShowDialog(this);
    }

    private void OpenDebitNote()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select or create a company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var debitNoteForm = new DebitNoteForm(_accountingService, _companyContext);
        debitNoteForm.ShowDialog(this);
    }

    private void OpenCreditNote()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select or create a company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var creditNoteForm = new CreditNoteForm(_accountingService, _companyContext);
        creditNoteForm.ShowDialog(this);
    }

    private void OpenDayBook()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select or create a company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var dayBookForm = new DayBookForm(_accountingService, _companyContext);
        dayBookForm.ShowDialog(this);
    }

    private void OpenLedgerStatement()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select or create a company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var statementForm = new LedgerStatementForm(_accountingService, _ledgerService, _companyContext);
        statementForm.ShowDialog(this);
    }

    private void OpenTrialBalance()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select or create a company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var tbForm = new TrialBalanceForm(_accountingService, _ledgerService, _companyContext);
        tbForm.ShowDialog(this);
    }

    private void OpenProfitLoss()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select or create a company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var plForm = new ProfitLossForm(_accountingService, _ledgerService, _companyContext);
        plForm.ShowDialog(this);
    }

    private void OpenBalanceSheet()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select or create a company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var bsForm = new BalanceSheetForm(_accountingService, _ledgerService, _companyContext);
        bsForm.ShowDialog(this);
    }

    private void OpenOutstandingReport()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select or create a company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var outForm = new OutstandingReportForm(_accountingService, _ledgerService, _companyContext);
        outForm.ShowDialog(this);
    }

    private void OpenCashBankBook(CashBankBookType? defaultType = null)
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null || _companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show("Please select or create a company and financial year first.", "Context Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var cashBankForm = new CashBankBookForm(_accountingService, _companyContext);
        if (defaultType.HasValue)
        {
            cashBankForm.SetInitialBookType(defaultType.Value);
        }
        cashBankForm.ShowDialog(this);
    }

    private void OpenUnitList()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            MessageBox.Show("Please select or create a company first.", "Company Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var unitForm = new UnitListForm(_inventoryService, _companyContext);
        unitForm.ShowDialog(this);
    }

    private void OpenStockItemList()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            MessageBox.Show("Please select or create a company first.", "Company Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var stockForm = new StockItemListForm(_inventoryService, _companyContext);
        stockForm.ShowDialog(this);
    }

    private void OpenStockSummary()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            MessageBox.Show("Please select or create a company first.", "Company Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var summaryForm = new StockSummaryForm(_inventoryService, _companyContext);
        summaryForm.ShowDialog(this);
    }

    private void OpenDashboard()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            MessageBox.Show("Please select or create a company first.", "Company Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var dashForm = new DashboardForm(_dashboardService, _accountingService, _companyContext);
        dashForm.OnNavigateRequested = (target) =>
        {
            switch (target)
            {
                case "Payment": OpenPaymentVoucher(); break;
                case "Receipt": OpenReceiptVoucher(); break;
                case "Sales": OpenSalesVoucher(); break;
                case "Purchase": OpenPurchaseVoucher(); break;
                case "DayBook": OpenDayBook(); break;
                case "TrialBalance": OpenTrialBalance(); break;
                case "ProfitLoss": OpenProfitLoss(); break;
                case "BalanceSheet": OpenBalanceSheet(); break;
                case "CashBankBook": OpenCashBankBook(); break;
                case "Outstanding": OpenOutstandingReport(); break;
                case "StockSummary": OpenStockSummary(); break;
                case "GoTo": OpenGlobalSearch(); break;
            }
        };
        dashForm.ShowDialog(this);
    }

    private void OpenImportExport()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            MessageBox.Show("Please select or create a company first.", "Company Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var form = new ImportExportForm(_importExportService, _companyContext);
        form.ShowDialog(this);
    }

    private void OpenBackupRestore()
    {
        using var form = new BackupRestoreForm(_backupRestoreService, _companyService, _companyContext);
        form.ShowDialog(this);
    }

    private void OpenGlobalSearch()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            MessageBox.Show("Please select or create a company first.", "Company Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var searchForm = new GlobalSearchForm(_searchService, _companyContext, (result) =>
        {
            HandleSearchResultNavigation(result);
        });
        searchForm.ShowDialog(this);
    }

    private void HandleSearchResultNavigation(GlobalSearchResultDto result)
    {
        switch (result.Category)
        {
            case GlobalSearchCategory.Navigation:
                switch (result.NavigationTarget)
                {
                    case "Dashboard": OpenDashboard(); break;
                    case "ImportExport": OpenImportExport(); break;
                    case "BackupRestore": OpenBackupRestore(); break;
                    case "DayBook": OpenDayBook(); break;
                    case "TrialBalance": OpenTrialBalance(); break;
                    case "ProfitLoss": OpenProfitLoss(); break;
                    case "BalanceSheet": OpenBalanceSheet(); break;
                    case "CashBankBook": OpenCashBankBook(); break;
                    case "Outstanding": OpenOutstandingReport(); break;
                    case "StockSummary": OpenStockSummary(); break;
                    case "Ledgers": OpenLedgerList(); break;
                    case "Groups": OpenGroupList(); break;
                    case "StockItems": OpenStockItemList(); break;
                    case "Units": OpenUnitList(); break;
                    case "Contra": OpenContraVoucher(); break;
                    case "Payment": OpenPaymentVoucher(); break;
                    case "Receipt": OpenReceiptVoucher(); break;
                    case "Journal": OpenJournalVoucher(); break;
                    case "Sales": OpenSalesVoucher(); break;
                    case "Purchase": OpenPurchaseVoucher(); break;
                    case "DebitNote": OpenDebitNote(); break;
                    case "CreditNote": OpenCreditNote(); break;
                }
                break;

            case GlobalSearchCategory.Ledger:
                if (result.EntityId.HasValue)
                {
                    using var stmt = new LedgerStatementForm(_accountingService, _ledgerService, _companyContext);
                    stmt.ShowDialog(this);
                }
                break;

            case GlobalSearchCategory.StockItem:
                if (result.EntityId.HasValue)
                {
                    using var editItem = new StockItemCreateEditForm(_inventoryService, _companyContext, result.EntityId.Value);
                    editItem.ShowDialog(this);
                }
                break;

            case GlobalSearchCategory.Voucher:
                if (result.EntityId.HasValue)
                {
                    MessageBox.Show(this, $"{result.Title}\n{result.Subtitle}\nAmount: {result.FormattedAmount}", "Voucher Quick View", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                break;
        }
    }

    private void OpenCompanyList()
    {
        using var listForm = new CompanyListForm(_companyService, _companyContext);
        listForm.ShowDialog(this);
    }

    private void OpenCreateCompany()
    {
        using var createForm = new CompanyCreateEditForm(_companyService);
        createForm.ShowDialog(this);
    }

    private void OpenAlterCompany()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            MessageBox.Show("No company is currently open to alter. Please select a company first.", "No Active Company", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var alterForm = new CompanyCreateEditForm(_companyService, _companyContext.CurrentCompany.CompanyId);
        alterForm.ShowDialog(this);
    }

    private void CloseActiveCompany()
    {
        if (!_companyContext.IsCompanyOpen)
        {
            MessageBox.Show("No company is currently open.", "MoneyFlow", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Are you sure you want to close company '{_companyContext.CurrentCompany?.CompanyName}'?",
            "Close Company",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
        {
            _companyService.CloseCompany();
        }
    }

    private void OpenFinancialYearList()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            MessageBox.Show("Please select or create a company first.", "Company Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList();
            return;
        }

        using var fyListForm = new FinancialYearListForm(_fyService, _companyContext);
        fyListForm.ShowDialog(this);
    }

    private void OpenDatabaseDiagnostics()
    {
        using var diag = new Dialogs.DatabaseConnectionDialog(_databaseSetupService);
        diag.ShowDialog(this);
    }

    private void ShowNotImplemented(string feature)
    {
        MessageBox.Show(
            $"{feature} is scheduled in upcoming phases per the Master Prompt.",
            "MoneyFlow Desktop Accounting",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if ((e.Alt && e.KeyCode == Keys.G) || (e.Control && e.KeyCode == Keys.K))
        {
            OpenGlobalSearch();
            e.Handled = true;
            return;
        }

        if (e.Control && e.KeyCode == Keys.F9)
        {
            OpenDebitNote();
            e.Handled = true;
            return;
        }
        if (e.Control && e.KeyCode == Keys.F8)
        {
            OpenCreditNote();
            e.Handled = true;
            return;
        }

        switch (e.KeyCode)
        {
            case Keys.Escape:
                var confirm = MessageBox.Show("Do you want to exit MoneyFlow?", "Quit", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes) Application.Exit();
                break;
            case Keys.F2:
                OpenFinancialYearList();
                break;
            case Keys.F3:
                OpenCompanyList();
                break;
            case Keys.F4:
                OpenContraVoucher();
                break;
            case Keys.F5:
                OpenPaymentVoucher();
                break;
            case Keys.F6:
                OpenReceiptVoucher();
                break;
            case Keys.F7:
                OpenJournalVoucher();
                break;
            case Keys.F8:
                OpenSalesVoucher();
                break;
            case Keys.F9:
                OpenPurchaseVoucher();
                break;
            case Keys.F10:
                OpenBackupRestore();
                break;
        }
    }
}
