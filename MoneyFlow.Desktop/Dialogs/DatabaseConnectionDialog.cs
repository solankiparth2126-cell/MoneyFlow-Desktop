using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Data.SqlClient;
using MoneyFlow.Services;

namespace MoneyFlow.Desktop.Dialogs;

public class DatabaseConnectionDialog : Form
{
    private readonly IDatabaseSetupService _databaseSetupService;
    private ComboBox cmbServerInstance = null!;
    private TextBox txtDatabase = null!;
    private CheckBox chkWindowsAuth = null!;
    private TextBox txtUsername = null!;
    private TextBox txtPassword = null!;
    private Button btnTest = null!;
    private Button btnConnect = null!;
    private Button btnExit = null!;
    private Label lblStatus = null!;
    private TextBox txtDiagnostics = null!;

    public bool ConnectionEstablished { get; private set; }
    public string SelectedConnectionString { get; private set; } = string.Empty;

    public DatabaseConnectionDialog(IDatabaseSetupService databaseSetupService)
    {
        _databaseSetupService = databaseSetupService;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "MoneyFlow Desktop — Database Configuration & Diagnostics";
        this.Size = new Size(580, 520);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.FromArgb(245, 247, 250);
        this.Font = new Font("Segoe UI", 9.5F);

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.FromArgb(24, 43, 73)
        };

        var lblHeader = new Label
        {
            Text = "Database Connection & Setup",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 12),
            AutoSize = true
        };

        var lblSubHeader = new Label
        {
            Text = "Configure SQL Server Express instance connection for MoneyFlowDB",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.LightGray,
            Location = new Point(20, 37),
            AutoSize = true
        };

        headerPanel.Controls.Add(lblHeader);
        headerPanel.Controls.Add(lblSubHeader);
        this.Controls.Add(headerPanel);

        var grp = new GroupBox
        {
            Text = "Connection Parameters",
            Location = new Point(20, 75),
            Size = new Size(525, 210),
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        var lblServer = new Label { Text = "SQL Server Instance:", Location = new Point(20, 30), AutoSize = true };
        cmbServerInstance = new ComboBox
        {
            Location = new Point(170, 27),
            Width = 330,
            DropDownStyle = ComboBoxStyle.DropDown
        };
        cmbServerInstance.Items.AddRange(new object[] {
            @".\SQLEXPRESS",
            @"localhost",
            @"(localdb)\mssqllocaldb",
            @".\SQLEXPRESS02",
            @".\SQLEXPRESS03",
            @".\SQLEXPRESS04"
        });
        cmbServerInstance.SelectedIndex = 0;

        var lblDb = new Label { Text = "Database Name:", Location = new Point(20, 65), AutoSize = true };
        txtDatabase = new TextBox
        {
            Location = new Point(170, 62),
            Width = 330,
            Text = "MoneyFlowDB"
        };

        chkWindowsAuth = new CheckBox
        {
            Text = "Use Windows Authentication (Recommended)",
            Location = new Point(170, 95),
            AutoSize = true,
            Checked = true
        };
        chkWindowsAuth.CheckedChanged += (s, e) =>
        {
            txtUsername.Enabled = !chkWindowsAuth.Checked;
            txtPassword.Enabled = !chkWindowsAuth.Checked;
        };

        var lblUser = new Label { Text = "SQL Username:", Location = new Point(20, 130), AutoSize = true };
        txtUsername = new TextBox { Location = new Point(170, 127), Width = 330, Enabled = false };

        var lblPass = new Label { Text = "SQL Password:", Location = new Point(20, 165), AutoSize = true };
        txtPassword = new TextBox { Location = new Point(170, 162), Width = 330, UseSystemPasswordChar = true, Enabled = false };

        grp.Controls.Add(lblServer);
        grp.Controls.Add(cmbServerInstance);
        grp.Controls.Add(lblDb);
        grp.Controls.Add(txtDatabase);
        grp.Controls.Add(chkWindowsAuth);
        grp.Controls.Add(lblUser);
        grp.Controls.Add(txtUsername);
        grp.Controls.Add(lblPass);
        grp.Controls.Add(txtPassword);
        this.Controls.Add(grp);

        lblStatus = new Label
        {
            Text = "Status: Ready to test connection",
            Location = new Point(20, 292),
            AutoSize = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105)
        };
        this.Controls.Add(lblStatus);

        txtDiagnostics = new TextBox
        {
            Location = new Point(20, 315),
            Size = new Size(525, 105),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.White,
            Font = new Font("Consolas", 8.5F)
        };
        this.Controls.Add(txtDiagnostics);

        btnTest = new Button
        {
            Text = "Diagnostics / Test",
            Location = new Point(20, 432),
            Size = new Size(150, 32),
            BackColor = Color.FromArgb(226, 232, 240)
        };
        btnTest.Click += async (s, e) => await RunDiagnosticsAsync();

        btnConnect = new Button
        {
            Text = "Initialize & Continue",
            Location = new Point(275, 432),
            Size = new Size(160, 32),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        btnConnect.Click += async (s, e) => await ConnectAndInitializeAsync();

        btnExit = new Button
        {
            Text = "Exit",
            Location = new Point(445, 432),
            Size = new Size(100, 32),
            BackColor = Color.FromArgb(239, 68, 68),
            ForeColor = Color.White
        };
        btnExit.Click += (s, e) => this.Close();

        this.Controls.Add(btnTest);
        this.Controls.Add(btnConnect);
        this.Controls.Add(btnExit);
    }

    private string BuildConnectionString()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = cmbServerInstance.Text.Trim(),
            InitialCatalog = txtDatabase.Text.Trim(),
            TrustServerCertificate = true
        };

        if (chkWindowsAuth.Checked)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = txtUsername.Text.Trim();
            builder.Password = txtPassword.Text;
        }

        return builder.ConnectionString;
    }

    private async Task RunDiagnosticsAsync()
    {
        btnTest.Enabled = false;
        btnConnect.Enabled = false;
        lblStatus.Text = "Status: Testing connection to SQL Server...";
        lblStatus.ForeColor = Color.FromArgb(37, 99, 235);
        txtDiagnostics.Clear();

        string connStr = BuildConnectionString();
        txtDiagnostics.AppendText($"Attempting connection to: {cmbServerInstance.Text}...\r\n");

        var result = await _databaseSetupService.TestConnectionAsync(connStr);
        if (result.IsSuccess)
        {
            lblStatus.Text = "Status: Connection Successful!";
            lblStatus.ForeColor = Color.FromArgb(16, 185, 129);
            txtDiagnostics.AppendText($"SUCCESS: {result.Message}\r\n");
            txtDiagnostics.AppendText("The SQL Server instance is reachable and ready.\r\n");
        }
        else
        {
            lblStatus.Text = "Status: Connection Failed.";
            lblStatus.ForeColor = Color.FromArgb(239, 68, 68);
            txtDiagnostics.AppendText($"ERROR: {result.Message}\r\n");
            if (result.Exception != null)
            {
                txtDiagnostics.AppendText($"Details: {result.Exception.Message}\r\n");
                txtDiagnostics.AppendText("Tip: Verify that the Windows SQL Server service (e.g. MSSQL$SQLEXPRESS02) is running.\r\n");
            }
        }

        btnTest.Enabled = true;
        btnConnect.Enabled = true;
    }

    private async Task ConnectAndInitializeAsync()
    {
        btnConnect.Enabled = false;
        lblStatus.Text = "Status: Initializing database and seed data...";
        lblStatus.ForeColor = Color.FromArgb(37, 99, 235);

        string connStr = BuildConnectionString();
        _databaseSetupService.SetActiveConnectionString(connStr);

        var result = await _databaseSetupService.InitializeDatabaseAsync();
        if (result.IsSuccess)
        {
            ConnectionEstablished = true;
            SelectedConnectionString = connStr;
            PersistConnectionString(connStr);
            MessageBox.Show(
                "MoneyFlowDB has been verified and system foundation initialized successfully.",
                "Database Ready",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        else
        {
            MessageBox.Show(
                "Database connection failed.\n\nPlease check that SQL Server is running, or test another instance using Diagnostics.",
                "Database Connection Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            lblStatus.Text = "Status: Initialization failed.";
            lblStatus.ForeColor = Color.FromArgb(239, 68, 68);
            txtDiagnostics.AppendText($"FAIL: {result.Message}\r\n");
            btnConnect.Enabled = true;
        }
    }

    private static void PersistConnectionString(string connStr)
    {
        try
        {
            var appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (File.Exists(appSettingsPath))
            {
                var json = File.ReadAllText(appSettingsPath);
                var jObj = System.Text.Json.Nodes.JsonNode.Parse(json);
                if (jObj != null)
                {
                    jObj["ConnectionStrings"] ??= new System.Text.Json.Nodes.JsonObject();
                    jObj["ConnectionStrings"]!["DefaultConnection"] = connStr;
                    File.WriteAllText(appSettingsPath, jObj.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                }
            }
        }
        catch
        {
            // Ignore persistence errors
        }
    }
}
