using System;
using System.Drawing;
using System.Windows.Forms;
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
        ILedgerService ledgerService)
    {
        _context = context;
        _databaseSetupService = databaseSetupService;
        _companyService = companyService;
        _companyContext = companyContext;
        _fyService = fyService;
        _groupService = groupService;
        _ledgerService = ledgerService;

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
        menuMasters.DropDownItems.Add("Stock Items (Phase 24)", null, (s, e) => ShowNotImplemented("Stock Items (Phase 24)"));
        menuMasters.DropDownItems.Add("Units of Measure (Phase 24)", null, (s, e) => ShowNotImplemented("Units of Measure (Phase 24)"));

        var menuTransactions = new ToolStripMenuItem("&Transactions");
        menuTransactions.DropDownItems.Add("F4 - Contra", null, (s, e) => ShowNotImplemented("Contra Voucher (Phase 10)"));
        menuTransactions.DropDownItems.Add("F5 - Payment", null, (s, e) => ShowNotImplemented("Payment Voucher (Phase 8)"));
        menuTransactions.DropDownItems.Add("F6 - Receipt", null, (s, e) => ShowNotImplemented("Receipt Voucher (Phase 9)"));
        menuTransactions.DropDownItems.Add("F7 - Journal", null, (s, e) => ShowNotImplemented("Journal Voucher (Phase 11)"));
        menuTransactions.DropDownItems.Add("F8 - Sales", null, (s, e) => ShowNotImplemented("Sales Voucher (Phase 12)"));
        menuTransactions.DropDownItems.Add("F9 - Purchase", null, (s, e) => ShowNotImplemented("Purchase Voucher (Phase 13)"));

        var menuReports = new ToolStripMenuItem("&Reports");
        menuReports.DropDownItems.Add("Day Book (Phase 16)", null, (s, e) => ShowNotImplemented("Day Book Report (Phase 16)"));
        menuReports.DropDownItems.Add("Ledger Statement (Phase 17)", null, (s, e) => ShowNotImplemented("Ledger Report (Phase 17)"));
        menuReports.DropDownItems.Add("Trial Balance (Phase 18)", null, (s, e) => ShowNotImplemented("Trial Balance (Phase 18)"));
        menuReports.DropDownItems.Add("Profit & Loss (Phase 19)", null, (s, e) => ShowNotImplemented("Profit & Loss (Phase 19)"));
        menuReports.DropDownItems.Add("Balance Sheet (Phase 20)", null, (s, e) => ShowNotImplemented("Balance Sheet (Phase 20)"));

        var menuUtilities = new ToolStripMenuItem("&Utilities");
        menuUtilities.DropDownItems.Add("Backup Database (Phase 28)", null, (s, e) => ShowNotImplemented("Database Backup (Phase 28)"));
        menuUtilities.DropDownItems.Add("Restore Database (Phase 28)", null, (s, e) => ShowNotImplemented("Database Restore (Phase 28)"));
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
        toolStrip.Items.Add(new ToolStripButton("F2: Period / FY", null, (s, e) => OpenFinancialYearList()));
        toolStrip.Items.Add(new ToolStripButton("F3: Company", null, (s, e) => OpenCompanyList()));
        toolStrip.Items.Add(new ToolStripButton("F4: Contra", null, (s, e) => ShowNotImplemented("Contra (Phase 10)")));
        toolStrip.Items.Add(new ToolStripButton("F5: Payment", null, (s, e) => ShowNotImplemented("Payment (Phase 8)")));
        toolStrip.Items.Add(new ToolStripButton("F6: Receipt", null, (s, e) => ShowNotImplemented("Receipt (Phase 9)")));
        toolStrip.Items.Add(new ToolStripButton("F7: Journal", null, (s, e) => ShowNotImplemented("Journal (Phase 11)")));
        toolStrip.Items.Add(new ToolStripButton("F8: Sales", null, (s, e) => ShowNotImplemented("Sales (Phase 12)")));
        toolStrip.Items.Add(new ToolStripButton("F9: Purchase", null, (s, e) => ShowNotImplemented("Purchase (Phase 13)")));
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
            "  Company Info (Select / Create / Alter)",
            "  ---------------------------------",
            "  Groups (Chart of Accounts)",
            "  Ledgers",
            "  Inventory Info (Stock & Units)",
            "  Accounting Vouchers",
            "  ---------------------------------",
            "  Day Book",
            "  Trial Balance",
            "  Profit & Loss A/c",
            "  Balance Sheet",
            "  ---------------------------------",
            "  Utilities & Backup",
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

        if (selected.Contains("Company Info"))
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
                ShowNotImplemented("Contra Voucher (F4 - Phase 10)");
                break;
            case Keys.F5:
                ShowNotImplemented("Payment Voucher (F5 - Phase 8)");
                break;
            case Keys.F6:
                ShowNotImplemented("Receipt Voucher (F6 - Phase 9)");
                break;
            case Keys.F7:
                ShowNotImplemented("Journal Voucher (F7 - Phase 11)");
                break;
            case Keys.F8:
                ShowNotImplemented("Sales Voucher (F8 - Phase 12)");
                break;
            case Keys.F9:
                ShowNotImplemented("Purchase Voucher (F9 - Phase 13)");
                break;
        }
    }
}
