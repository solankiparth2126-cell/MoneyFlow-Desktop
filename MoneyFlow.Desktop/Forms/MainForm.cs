using System;
using System.Drawing;
using System.Windows.Forms;
using MoneyFlow.Data;
using MoneyFlow.Services;

namespace MoneyFlow.Desktop.Forms;

public class MainForm : Form
{
    private readonly AppDbContext _context;
    private readonly IDatabaseSetupService _databaseSetupService;

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

    public MainForm(AppDbContext context, IDatabaseSetupService databaseSetupService)
    {
        _context = context;
        _databaseSetupService = databaseSetupService;
        InitializeComponent();
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
        menuCompany.DropDownItems.Add("Select Company", null, (s, e) => ShowNotImplemented("Company Management (Phase 3)"));
        menuCompany.DropDownItems.Add("Create Company", null, (s, e) => ShowNotImplemented("Create Company (Phase 3)"));
        menuCompany.DropDownItems.Add("Alter Company", null, (s, e) => ShowNotImplemented("Alter Company (Phase 3)"));
        menuCompany.DropDownItems.Add(new ToolStripSeparator());
        menuCompany.DropDownItems.Add("E&xit", null, (s, e) => Application.Exit());

        var menuMasters = new ToolStripMenuItem("&Masters");
        menuMasters.DropDownItems.Add("Groups (Phase 5)", null, (s, e) => ShowNotImplemented("Groups Master (Phase 5)"));
        menuMasters.DropDownItems.Add("Ledgers (Phase 6)", null, (s, e) => ShowNotImplemented("Ledgers Master (Phase 6)"));
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
        toolStrip.Items.Add(new ToolStripButton("F2: Date"));
        toolStrip.Items.Add(new ToolStripButton("F4: Contra"));
        toolStrip.Items.Add(new ToolStripButton("F5: Payment"));
        toolStrip.Items.Add(new ToolStripButton("F6: Receipt"));
        toolStrip.Items.Add(new ToolStripButton("F7: Journal"));
        toolStrip.Items.Add(new ToolStripButton("F8: Sales"));
        toolStrip.Items.Add(new ToolStripButton("F9: Purchase"));
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

        lblStatusCompany = new ToolStripStatusLabel("Company: [None Selected - Phase 3]") { ForeColor = Color.White };
        lblStatusFY = new ToolStripStatusLabel(" | FY: 2026-27") { ForeColor = Color.LightGreen };
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
        // Central Container Panel
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

        var lblCurrentCompany = new Label
        {
            Text = "Current Company:\n(Select or Create Company in Phase 3)",
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(70, 80, 95),
            Location = new Point(20, 40),
            Size = new Size(340, 50)
        };

        var lblCurrentFY = new Label
        {
            Text = "Current Financial Year:\n01-Apr-2026 to 31-Mar-2027",
            Font = new Font("Segoe UI", 10F),
            ForeColor = Color.FromArgb(70, 80, 95),
            Location = new Point(20, 110),
            Size = new Size(340, 50)
        };

        var lblArchitecture = new Label
        {
            Text = "MONEYFLOW DESKTOP ERP\nVersion 1.0 (Phase 1 Foundation)\n\n• Standalone Windows PC\n• C# + .NET 8 + WinForms\n• SQL Server Express + EF Core\n• Pure Double-Entry Accounting\n• Offline / Single-PC Architecture",
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
            "  Company Info (Select / Create)",
            "  ---------------------------------",
            "  Accounts Info (Groups & Ledgers)",
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

    private void HandleGatewaySelection()
    {
        var selected = lstGatewayMenu.SelectedItem?.ToString()?.Trim();
        if (string.IsNullOrEmpty(selected) || selected.StartsWith("-")) return;

        if (selected.Contains("Database Diagnostics"))
        {
            OpenDatabaseDiagnostics();
        }
        else if (selected.Contains("Quit"))
        {
            Application.Exit();
        }
        else
        {
            ShowNotImplemented(selected);
        }
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
                // Tally-style Esc back/quit
                var confirm = MessageBox.Show("Do you want to exit MoneyFlow?", "Quit", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (confirm == DialogResult.Yes) Application.Exit();
                break;
            case Keys.F2:
                ShowNotImplemented("Change Date (F2)");
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
