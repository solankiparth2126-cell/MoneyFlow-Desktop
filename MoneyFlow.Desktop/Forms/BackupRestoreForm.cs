using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Forms;

public class BackupRestoreForm : Form
{
    private readonly IBackupRestoreService _backupRestoreService;
    private readonly ICompanyService _companyService;
    private readonly ICompanyContext _companyContext;

    // Controls
    private TabControl _tabMain = null!;

    // Tab 1: Create Backup
    private ComboBox _cmbBackupCompany = null!;
    private TextBox _txtBackupDirectory = null!;
    private Button _btnBrowseBackupDir = null!;
    private RadioButton _rbFormatMfb = null!;
    private RadioButton _rbFormatBak = null!;
    private TextBox _txtBackupComment = null!;
    private Button _btnCreateBackup = null!;
    private RichTextBox _rtbBackupLog = null!;

    // Tab 2: Restore
    private TextBox _txtRestoreFilePath = null!;
    private Button _btnBrowseRestoreFile = null!;
    private Button _btnInspectFile = null!;
    private GroupBox _grpManifest = null!;
    private Label _lblManifestCompany = null!;
    private Label _lblManifestFY = null!;
    private Label _lblManifestDate = null!;
    private Label _lblManifestCounts = null!;
    private Label _lblManifestChecksum = null!;
    private RadioButton _rbRestoreAsNew = null!;
    private RadioButton _rbRestoreOverwrite = null!;
    private TextBox _txtNewCompanyName = null!;
    private ComboBox _cmbTargetCompany = null!;
    private Label _lblOverwriteWarning = null!;
    private Button _btnExecuteRestore = null!;
    private RichTextBox _rtbRestoreLog = null!;

    // Tab 3: History
    private TextBox _txtHistoryDir = null!;
    private Button _btnRefreshHistory = null!;
    private Button _btnOpenInExplorer = null!;
    private DataGridView _dgvHistory = null!;
    private Button _btnHistoryVerify = null!;
    private Button _btnHistoryRestore = null!;

    private BackupManifestDto? _currentManifest;

    public BackupRestoreForm(
        IBackupRestoreService backupRestoreService,
        ICompanyService companyService,
        ICompanyContext companyContext)
    {
        _backupRestoreService = backupRestoreService;
        _companyService = companyService;
        _companyContext = companyContext;

        InitializeComponent();
        _ = LoadCompaniesAsync();
        _ = LoadHistoryAsync();
    }

    private void InitializeComponent()
    {
        Text = "Backup & Restore Manager — MoneyFlow Desktop Accounting";
        Size = new Size(1000, 720);
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = Color.FromArgb(244, 246, 249);
        KeyPreview = true;

        // Header Strip
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = Color.FromArgb(18, 52, 86),
            Padding = new Padding(15, 12, 15, 10)
        };

        var lblTitle = new Label
        {
            Text = "LOCAL BACKUP & RESTORE SYSTEM",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(15, 14)
        };

        var btnClose = new Button
        {
            Text = "Close (Esc)",
            Width = 85,
            Height = 28,
            BackColor = Color.FromArgb(192, 57, 43),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(885, 13)
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, e) => Close();

        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(btnClose);

        // Main Tab Control
        _tabMain = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
        };

        var tabCreate = new TabPage("  Create Backup (F10)  ") { BackColor = Color.FromArgb(248, 249, 251), Padding = new Padding(15) };
        var tabRestore = new TabPage("  Restore Wizard  ") { BackColor = Color.FromArgb(248, 249, 251), Padding = new Padding(15) };
        var tabHistory = new TabPage("  Backup Repository & History  ") { BackColor = Color.FromArgb(248, 249, 251), Padding = new Padding(15) };

        InitializeCreateTab(tabCreate);
        InitializeRestoreTab(tabRestore);
        InitializeHistoryTab(tabHistory);

        _tabMain.TabPages.Add(tabCreate);
        _tabMain.TabPages.Add(tabRestore);
        _tabMain.TabPages.Add(tabHistory);

        Controls.Add(_tabMain);
        Controls.Add(pnlHeader);

        KeyDown += BackupRestoreForm_KeyDown;
    }

    private void InitializeCreateTab(TabPage tab)
    {
        var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        var grpConfig = new GroupBox
        {
            Text = "Backup Parameters & Target",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 43, 73),
            Location = new Point(10, 10),
            Size = new Size(930, 290)
        };

        // Company
        var lblComp = new Label { Text = "Source Company:", Font = new Font("Segoe UI", 9F, FontStyle.Regular), Location = new Point(20, 30), AutoSize = true };
        _cmbBackupCompany = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9F),
            Location = new Point(160, 27),
            Width = 400
        };

        // Target Folder
        var lblDir = new Label { Text = "Target Directory:", Font = new Font("Segoe UI", 9F, FontStyle.Regular), Location = new Point(20, 68), AutoSize = true };
        _txtBackupDirectory = new TextBox
        {
            Text = _backupRestoreService.GetDefaultBackupDirectory(),
            Font = new Font("Segoe UI", 9F),
            Location = new Point(160, 65),
            Width = 620
        };
        _btnBrowseBackupDir = new Button
        {
            Text = "Browse...",
            Font = new Font("Segoe UI", 8.5F),
            Location = new Point(790, 64),
            Width = 85,
            Height = 25
        };
        _btnBrowseBackupDir.Click += (s, e) =>
        {
            using var fbd = new FolderBrowserDialog();
            fbd.SelectedPath = _txtBackupDirectory.Text;
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                _txtBackupDirectory.Text = fbd.SelectedPath;
            }
        };

        // Backup Format
        var lblFormat = new Label { Text = "Backup Format:", Font = new Font("Segoe UI", 9F, FontStyle.Regular), Location = new Point(20, 110), AutoSize = true };
        _rbFormatMfb = new RadioButton
        {
            Text = "Portable Company Archive (.mfb) — Recommended",
            Checked = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(160, 108),
            AutoSize = true
        };
        var lblMfbHelp = new Label
        {
            Text = "Packages company masters, accounts, vouchers, items, and SHA-256 checksum into a portable ZIP archive. Restorable on any PC.",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(100, 110, 120),
            Location = new Point(180, 132),
            Size = new Size(720, 20)
        };

        _rbFormatBak = new RadioButton
        {
            Text = "Full Database Backup (.bak) — SQL Server Native",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(160, 158),
            AutoSize = true
        };
        var lblBakHelp = new Label
        {
            Text = "Native SQL Server engine backup containing the entire database instance with all companies. Requires SQL Server relational engine.",
            Font = new Font("Segoe UI", 8F),
            ForeColor = Color.FromArgb(100, 110, 120),
            Location = new Point(180, 182),
            Size = new Size(720, 20)
        };

        // Comment
        var lblComment = new Label { Text = "Notes / Comment:", Font = new Font("Segoe UI", 9F, FontStyle.Regular), Location = new Point(20, 215), AutoSize = true };
        _txtBackupComment = new TextBox
        {
            Font = new Font("Segoe UI", 9F),
            Location = new Point(160, 212),
            Width = 620
        };

        // Create Button
        _btnCreateBackup = new Button
        {
            Text = "Create Backup Now",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            BackColor = Color.FromArgb(39, 174, 96),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(160, 248),
            Width = 200,
            Height = 34
        };
        _btnCreateBackup.FlatAppearance.BorderSize = 0;
        _btnCreateBackup.Click += async (s, e) => await ExecuteCreateBackupAsync();

        grpConfig.Controls.Add(lblComp);
        grpConfig.Controls.Add(_cmbBackupCompany);
        grpConfig.Controls.Add(lblDir);
        grpConfig.Controls.Add(_txtBackupDirectory);
        grpConfig.Controls.Add(_btnBrowseBackupDir);
        grpConfig.Controls.Add(lblFormat);
        grpConfig.Controls.Add(_rbFormatMfb);
        grpConfig.Controls.Add(lblMfbHelp);
        grpConfig.Controls.Add(_rbFormatBak);
        grpConfig.Controls.Add(lblBakHelp);
        grpConfig.Controls.Add(lblComment);
        grpConfig.Controls.Add(_txtBackupComment);
        grpConfig.Controls.Add(_btnCreateBackup);

        // Log / Output Area
        var grpLog = new GroupBox
        {
            Text = "Backup Execution Log & Checksum Verification",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 43, 73),
            Location = new Point(10, 310),
            Size = new Size(930, 280)
        };

        _rtbBackupLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(20, 25, 35),
            ForeColor = Color.FromArgb(220, 230, 242),
            Font = new Font("Consolas", 9.5F),
            BorderStyle = BorderStyle.None
        };
        _rtbBackupLog.AppendText("Ready to create backup. Select company and format, then click 'Create Backup Now'.\n");

        grpLog.Controls.Add(_rtbBackupLog);

        pnl.Controls.Add(grpConfig);
        pnl.Controls.Add(grpLog);
        tab.Controls.Add(pnl);
    }

    private void InitializeRestoreTab(TabPage tab)
    {
        var pnl = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        // File Selection Group
        var grpFile = new GroupBox
        {
            Text = "Select Backup Archive",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 43, 73),
            Location = new Point(10, 10),
            Size = new Size(930, 85)
        };

        var lblFile = new Label { Text = "Backup File Path:", Font = new Font("Segoe UI", 9F, FontStyle.Regular), Location = new Point(20, 32), AutoSize = true };
        _txtRestoreFilePath = new TextBox
        {
            Font = new Font("Segoe UI", 9F),
            Location = new Point(150, 30),
            Width = 580
        };

        _btnBrowseRestoreFile = new Button
        {
            Text = "Browse...",
            Font = new Font("Segoe UI", 8.5F),
            Location = new Point(740, 29),
            Width = 80,
            Height = 25
        };
        _btnBrowseRestoreFile.Click += (s, e) =>
        {
            using var ofd = new OpenFileDialog();
            ofd.Filter = "MoneyFlow Backup Archive (*.mfb)|*.mfb|SQL Server Database Backup (*.bak)|*.bak|All Files (*.*)|*.*";
            ofd.InitialDirectory = _backupRestoreService.GetDefaultBackupDirectory();
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                _txtRestoreFilePath.Text = ofd.FileName;
                _ = InspectBackupFileAsync();
            }
        };

        _btnInspectFile = new Button
        {
            Text = "Inspect",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            BackColor = Color.FromArgb(41, 128, 185),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(830, 29),
            Width = 80,
            Height = 25
        };
        _btnInspectFile.FlatAppearance.BorderSize = 0;
        _btnInspectFile.Click += async (s, e) => await InspectBackupFileAsync();

        grpFile.Controls.Add(lblFile);
        grpFile.Controls.Add(_txtRestoreFilePath);
        grpFile.Controls.Add(_btnBrowseRestoreFile);
        grpFile.Controls.Add(_btnInspectFile);

        // Manifest Card
        _grpManifest = new GroupBox
        {
            Text = "Archive Manifest & Integrity Check",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 43, 73),
            Location = new Point(10, 105),
            Size = new Size(930, 120),
            BackColor = Color.FromArgb(238, 242, 248)
        };

        _lblManifestCompany = new Label { Text = "Company: [No archive inspected]", Font = new Font("Segoe UI", 9F, FontStyle.Bold), Location = new Point(20, 25), AutoSize = true };
        _lblManifestFY = new Label { Text = "Financial Year: —", Font = new Font("Segoe UI", 9F), Location = new Point(20, 50), AutoSize = true };
        _lblManifestDate = new Label { Text = "Backup Date: —", Font = new Font("Segoe UI", 9F), Location = new Point(20, 75), AutoSize = true };

        _lblManifestCounts = new Label { Text = "Contents: Ledgers: 0 | Vouchers: 0 | Stock Items: 0", Font = new Font("Segoe UI", 9F), Location = new Point(360, 25), AutoSize = true };
        _lblManifestChecksum = new Label { Text = "SHA-256 Checksum: [Pending Inspection]", Font = new Font("Segoe UI", 9F), ForeColor = Color.FromArgb(100, 110, 120), Location = new Point(360, 50), AutoSize = true };

        _grpManifest.Controls.Add(_lblManifestCompany);
        _grpManifest.Controls.Add(_lblManifestFY);
        _grpManifest.Controls.Add(_lblManifestDate);
        _grpManifest.Controls.Add(_lblManifestCounts);
        _grpManifest.Controls.Add(_lblManifestChecksum);

        // Destination Options
        var grpDest = new GroupBox
        {
            Text = "Restore Destination Options",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 43, 73),
            Location = new Point(10, 235),
            Size = new Size(930, 160)
        };

        _rbRestoreAsNew = new RadioButton
        {
            Text = "Restore as New Company (Copy - Recommended)",
            Checked = true,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Location = new Point(20, 25),
            AutoSize = true
        };
        _rbRestoreAsNew.CheckedChanged += (s, e) => ToggleRestoreMode();

        var lblNewName = new Label { Text = "New Company Name:", Font = new Font("Segoe UI", 9F), Location = new Point(45, 53), AutoSize = true };
        _txtNewCompanyName = new TextBox
        {
            Font = new Font("Segoe UI", 9F),
            Location = new Point(190, 50),
            Width = 400
        };

        _rbRestoreOverwrite = new RadioButton
        {
            Text = "Overwrite Existing Company (CAUTION: Replaces all accounting data)",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(192, 57, 43),
            Location = new Point(20, 85),
            AutoSize = true
        };
        _rbRestoreOverwrite.CheckedChanged += (s, e) => ToggleRestoreMode();

        var lblTargetComp = new Label { Text = "Target Company:", Font = new Font("Segoe UI", 9F), Location = new Point(45, 113), AutoSize = true };
        _cmbTargetCompany = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 9F),
            Location = new Point(190, 110),
            Width = 400,
            Enabled = false
        };

        _lblOverwriteWarning = new Label
        {
            Text = "WARNING: All existing vouchers, ledgers, and accounts in the chosen target company will be wiped and replaced!",
            Font = new Font("Segoe UI", 8F, FontStyle.Italic),
            ForeColor = Color.FromArgb(192, 57, 43),
            Location = new Point(600, 113),
            AutoSize = true,
            Visible = false
        };

        grpDest.Controls.Add(_rbRestoreAsNew);
        grpDest.Controls.Add(lblNewName);
        grpDest.Controls.Add(_txtNewCompanyName);
        grpDest.Controls.Add(_rbRestoreOverwrite);
        grpDest.Controls.Add(lblTargetComp);
        grpDest.Controls.Add(_cmbTargetCompany);
        grpDest.Controls.Add(_lblOverwriteWarning);

        // Execute Restore Action Button
        _btnExecuteRestore = new Button
        {
            Text = "Restore Backup Now",
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            BackColor = Color.FromArgb(230, 126, 34),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(10, 405),
            Width = 220,
            Height = 35
        };
        _btnExecuteRestore.FlatAppearance.BorderSize = 0;
        _btnExecuteRestore.Click += async (s, e) => await ExecuteRestoreBackupAsync();

        // Restore Log Area
        var grpLog = new GroupBox
        {
            Text = "Restore Operation Status & Log",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 43, 73),
            Location = new Point(10, 450),
            Size = new Size(930, 160)
        };

        _rtbRestoreLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.FromArgb(20, 25, 35),
            ForeColor = Color.FromArgb(220, 230, 242),
            Font = new Font("Consolas", 9.5F),
            BorderStyle = BorderStyle.None
        };
        _rtbRestoreLog.AppendText("Select a .mfb or .bak file, inspect its contents, select destination, and restore.\n");

        grpLog.Controls.Add(_rtbRestoreLog);

        pnl.Controls.Add(grpFile);
        pnl.Controls.Add(_grpManifest);
        pnl.Controls.Add(grpDest);
        pnl.Controls.Add(_btnExecuteRestore);
        pnl.Controls.Add(grpLog);

        tab.Controls.Add(pnl);
    }

    private void InitializeHistoryTab(TabPage tab)
    {
        var pnl = new Panel { Dock = DockStyle.Fill };

        // Top Toolbar
        var pnlTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 45,
            BackColor = Color.FromArgb(235, 240, 245),
            Padding = new Padding(10, 8, 10, 8)
        };

        var lblDir = new Label { Text = "Repository Folder:", Location = new Point(10, 12), AutoSize = true };
        _txtHistoryDir = new TextBox
        {
            Text = _backupRestoreService.GetDefaultBackupDirectory(),
            Location = new Point(130, 9),
            Width = 480
        };

        _btnRefreshHistory = new Button
        {
            Text = "Refresh (F5)",
            Location = new Point(620, 8),
            Width = 90,
            Height = 27
        };
        _btnRefreshHistory.Click += async (s, e) => await LoadHistoryAsync();

        _btnOpenInExplorer = new Button
        {
            Text = "Open in Explorer",
            Location = new Point(720, 8),
            Width = 120,
            Height = 27
        };
        _btnOpenInExplorer.Click += (s, e) =>
        {
            string dir = _txtHistoryDir.Text;
            if (Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
            }
        };

        pnlTop.Controls.Add(lblDir);
        pnlTop.Controls.Add(_txtHistoryDir);
        pnlTop.Controls.Add(_btnRefreshHistory);
        pnlTop.Controls.Add(_btnOpenInExplorer);

        // DataGridView
        _dgvHistory = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        _dgvHistory.Columns.Add("FileName", "File Name");
        _dgvHistory.Columns.Add("Company", "Company Name");
        _dgvHistory.Columns.Add("Type", "Format / Type");
        _dgvHistory.Columns.Add("Size", "File Size");
        _dgvHistory.Columns.Add("Date", "Date Created");
        _dgvHistory.Columns.Add("Status", "Integrity Status");

        _dgvHistory.Columns[0].FillWeight = 25;
        _dgvHistory.Columns[1].FillWeight = 25;
        _dgvHistory.Columns[2].FillWeight = 15;
        _dgvHistory.Columns[3].FillWeight = 10;
        _dgvHistory.Columns[4].FillWeight = 15;
        _dgvHistory.Columns[5].FillWeight = 10;

        // Bottom Action Bar
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 45,
            BackColor = Color.FromArgb(235, 240, 245),
            Padding = new Padding(10, 8, 10, 8)
        };

        _btnHistoryVerify = new Button
        {
            Text = "Verify Selected Backup",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = Color.FromArgb(41, 128, 185),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(10, 8),
            Width = 180,
            Height = 28
        };
        _btnHistoryVerify.FlatAppearance.BorderSize = 0;
        _btnHistoryVerify.Click += async (s, e) => await VerifySelectedHistoryAsync();

        _btnHistoryRestore = new Button
        {
            Text = "Restore Selected Backup...",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            BackColor = Color.FromArgb(230, 126, 34),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(200, 8),
            Width = 190,
            Height = 28
        };
        _btnHistoryRestore.FlatAppearance.BorderSize = 0;
        _btnHistoryRestore.Click += (s, e) => SwitchToRestoreFromHistory();

        pnlBottom.Controls.Add(_btnHistoryVerify);
        pnlBottom.Controls.Add(_btnHistoryRestore);

        pnl.Controls.Add(_dgvHistory);
        pnl.Controls.Add(pnlBottom);
        pnl.Controls.Add(pnlTop);

        tab.Controls.Add(pnl);
    }

    private void ToggleRestoreMode()
    {
        bool isNew = _rbRestoreAsNew.Checked;
        _txtNewCompanyName.Enabled = isNew;
        _cmbTargetCompany.Enabled = !isNew;
        _lblOverwriteWarning.Visible = !isNew;
    }

    private async Task LoadCompaniesAsync()
    {
        try
        {
            var companies = await _companyService.GetAllCompaniesAsync();
            _cmbBackupCompany.Items.Clear();
            _cmbTargetCompany.Items.Clear();

            int selectedIndex = 0;
            for (int i = 0; i < companies.Count; i++)
            {
                var c = companies[i];
                var item = new CompanyComboItem(c.CompanyId, c.CompanyName);
                _cmbBackupCompany.Items.Add(item);
                _cmbTargetCompany.Items.Add(item);

                if (_companyContext.IsCompanyOpen && _companyContext.CurrentCompany?.CompanyId == c.CompanyId)
                {
                    selectedIndex = i;
                }
            }

            if (_cmbBackupCompany.Items.Count > 0)
            {
                _cmbBackupCompany.SelectedIndex = selectedIndex;
                _cmbTargetCompany.SelectedIndex = selectedIndex;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed loading companies: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ExecuteCreateBackupAsync()
    {
        if (_cmbBackupCompany.SelectedItem is not CompanyComboItem selectedCompany)
        {
            MessageBox.Show("Please select a company to backup.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string targetDir = _txtBackupDirectory.Text.Trim();
        if (string.IsNullOrWhiteSpace(targetDir))
        {
            MessageBox.Show("Please specify a target directory.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnCreateBackup.Enabled = false;
        _rtbBackupLog.AppendText($"\n[{DateTime.Now:HH:mm:ss}] Initiating backup for '{selectedCompany.CompanyName}'...\n");

        try
        {
            var options = new BackupCreateOptionsDto
            {
                CompanyId = selectedCompany.CompanyId,
                TargetDirectory = targetDir,
                BackupType = _rbFormatBak.Checked ? BackupType.FullDatabase : BackupType.CompanyArchive,
                Comment = _txtBackupComment.Text.Trim()
            };

            var result = await Task.Run(() => _backupRestoreService.CreateBackupAsync(options));

            _rtbBackupLog.SelectionColor = Color.FromArgb(46, 204, 113);
            _rtbBackupLog.AppendText($"[{DateTime.Now:HH:mm:ss}] SUCCESS: Backup created successfully!\n");
            _rtbBackupLog.SelectionColor = Color.White;
            _rtbBackupLog.AppendText($"• File: {result.FileName}\n");
            _rtbBackupLog.AppendText($"• Path: {result.FullPath}\n");
            _rtbBackupLog.AppendText($"• Size: {result.FileSizeBytes / 1024.0:F2} KB\n");
            _rtbBackupLog.AppendText($"• Format: {result.BackupType}\n");

            MessageBox.Show(this, $"Backup created successfully!\n\nFile: {result.FileName}\nSaved to: {result.FullPath}", "Backup Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);

            await LoadHistoryAsync();
        }
        catch (Exception ex)
        {
            _rtbBackupLog.SelectionColor = Color.FromArgb(231, 76, 60);
            _rtbBackupLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}\n");
            MessageBox.Show(this, $"Backup creation failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnCreateBackup.Enabled = true;
        }
    }

    private async Task InspectBackupFileAsync()
    {
        string path = _txtRestoreFilePath.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show("Please select a valid backup file.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _btnInspectFile.Enabled = false;
        try
        {
            var manifest = await _backupRestoreService.ReadManifestAsync(path);
            if (manifest == null)
            {
                _lblManifestCompany.Text = "Company: [INVALID ARCHIVE / UNKNOWN]";
                _lblManifestCompany.ForeColor = Color.Red;
                _lblManifestChecksum.Text = "Status: Corrupt or unreadable manifest";
                _lblManifestChecksum.ForeColor = Color.Red;
                return;
            }

            _currentManifest = manifest;
            _lblManifestCompany.Text = $"Company: {manifest.CompanyName}";
            _lblManifestCompany.ForeColor = Color.FromArgb(24, 43, 73);
            _lblManifestFY.Text = $"Financial Year: {manifest.FinancialYearLabel}";
            _lblManifestDate.Text = $"Backup Date: {manifest.CreatedAt:dd-MMM-yyyy HH:mm}";
            _lblManifestCounts.Text = $"Contents: Ledgers: {manifest.LedgerCount} | Vouchers: {manifest.VoucherCount} | Stock Items: {manifest.StockItemCount}";

            if (!string.IsNullOrWhiteSpace(manifest.ChecksumSha256))
            {
                _lblManifestChecksum.Text = $"SHA-256 Checksum: {manifest.ChecksumSha256.Substring(0, Math.Min(16, manifest.ChecksumSha256.Length))}... (Integrity Verified)";
                _lblManifestChecksum.ForeColor = Color.FromArgb(39, 174, 96);
            }
            else
            {
                _lblManifestChecksum.Text = $"Checksum: N/A ({manifest.Comment})";
                _lblManifestChecksum.ForeColor = Color.FromArgb(100, 110, 120);
            }

            // Pre-fill suggested new company name
            _txtNewCompanyName.Text = $"{manifest.CompanyName} (Restored)";

            _rtbRestoreLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Inspected archive '{Path.GetFileName(path)}': {manifest.CompanyName}, {manifest.VoucherCount} vouchers.\n");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed inspecting backup: {ex.Message}", "Inspection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnInspectFile.Enabled = true;
        }
    }

    private async Task ExecuteRestoreBackupAsync()
    {
        string path = _txtRestoreFilePath.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            MessageBox.Show("Please select a valid backup file.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        bool isNew = _rbRestoreAsNew.Checked;
        string newName = _txtNewCompanyName.Text.Trim();
        int? targetCompId = null;

        if (isNew)
        {
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Please enter a name for the restored company.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        else
        {
            if (_cmbTargetCompany.SelectedItem is not CompanyComboItem selectedTarget)
            {
                MessageBox.Show("Please select the target company to overwrite.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            targetCompId = selectedTarget.CompanyId;

            var confirmOverwrite = MessageBox.Show(
                $"WARNING: You are about to OVERWRITE all accounting records in '{selectedTarget.CompanyName}'.\n\nAll existing vouchers, ledgers, and transactions in this company will be PERMANENTLY replaced with the backup data.\n\nAre you absolutely sure you want to proceed?",
                "CONFIRM OVERWRITE RESTORE",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirmOverwrite != DialogResult.Yes) return;
        }

        var confirm = MessageBox.Show(
            $"Restore archive '{Path.GetFileName(path)}'?\nDestination: {(isNew ? $"New Company: {newName}" : "Overwrite Selected Company")}",
            "Confirm Restore",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        _btnExecuteRestore.Enabled = false;
        _rtbRestoreLog.AppendText($"\n[{DateTime.Now:HH:mm:ss}] Starting restore operation...\n");

        try
        {
            var options = new RestoreOptionsDto
            {
                BackupFilePath = path,
                RestoreAsNewCompany = isNew,
                NewCompanyName = newName,
                TargetCompanyId = targetCompId
            };

            var result = await Task.Run(() => _backupRestoreService.RestoreBackupAsync(options));

            if (result.Success)
            {
                _rtbRestoreLog.SelectionColor = Color.FromArgb(46, 204, 113);
                _rtbRestoreLog.AppendText($"[{DateTime.Now:HH:mm:ss}] SUCCESS: {result.Message}\n");
                MessageBox.Show(this, result.Message, "Restore Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadCompaniesAsync();
            }
            else
            {
                _rtbRestoreLog.SelectionColor = Color.FromArgb(231, 76, 60);
                _rtbRestoreLog.AppendText($"[{DateTime.Now:HH:mm:ss}] FAILED: {result.Message}\n");
                MessageBox.Show(this, result.Message, "Restore Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _rtbRestoreLog.SelectionColor = Color.FromArgb(231, 76, 60);
            _rtbRestoreLog.AppendText($"[{DateTime.Now:HH:mm:ss}] ERROR: {ex.Message}\n");
            MessageBox.Show(this, $"Error during restoration: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _btnExecuteRestore.Enabled = true;
        }
    }

    private async Task LoadHistoryAsync()
    {
        try
        {
            string dir = _txtHistoryDir.Text.Trim();
            var items = await _backupRestoreService.GetBackupHistoryAsync(dir);

            _dgvHistory.Rows.Clear();
            foreach (var item in items)
            {
                string sizeStr = item.FileSizeBytes > (1024 * 1024)
                    ? $"{item.FileSizeBytes / (1024.0 * 1024.0):F2} MB"
                    : $"{item.FileSizeBytes / 1024.0:F2} KB";

                int rowIdx = _dgvHistory.Rows.Add(
                    item.FileName,
                    item.CompanyName,
                    item.BackupType == BackupType.FullDatabase ? "SQL Server (.bak)" : "Company Archive (.mfb)",
                    sizeStr,
                    item.CreatedAt.ToString("yyyy-MM-dd HH:mm"),
                    item.IsValid ? "Valid" : "Unverified"
                );

                _dgvHistory.Rows[rowIdx].Tag = item;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed reading backup history: {ex.Message}", "History Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task VerifySelectedHistoryAsync()
    {
        if (_dgvHistory.SelectedRows.Count == 0) return;
        var info = _dgvHistory.SelectedRows[0].Tag as BackupFileInfo;
        if (info == null) return;

        var manifest = await _backupRestoreService.ReadManifestAsync(info.FullPath);
        if (manifest != null)
        {
            MessageBox.Show(
                $"Backup File: {info.FileName}\n" +
                $"Company: {manifest.CompanyName}\n" +
                $"Financial Year: {manifest.FinancialYearLabel}\n" +
                $"Ledgers: {manifest.LedgerCount} | Vouchers: {manifest.VoucherCount} | Stock Items: {manifest.StockItemCount}\n" +
                $"Created: {manifest.CreatedAt:dd-MMM-yyyy HH:mm}\n" +
                $"Checksum (SHA-256): {manifest.ChecksumSha256 ?? "N/A"}\n\n" +
                $"Status: ARCHIVE INTEGRITY VERIFIED OK",
                "Integrity Check Result",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show("Backup archive could not be verified or is corrupted.", "Integrity Check Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SwitchToRestoreFromHistory()
    {
        if (_dgvHistory.SelectedRows.Count == 0) return;
        var info = _dgvHistory.SelectedRows[0].Tag as BackupFileInfo;
        if (info == null) return;

        _txtRestoreFilePath.Text = info.FullPath;
        _tabMain.SelectedIndex = 1; // Switch to Restore tab
        _ = InspectBackupFileAsync();
    }

    private void BackupRestoreForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F5 && _tabMain.SelectedIndex == 2)
        {
            _ = LoadHistoryAsync();
            e.Handled = true;
        }
    }

    private record CompanyComboItem(int CompanyId, string CompanyName)
    {
        public override string ToString() => CompanyName;
    }
}
