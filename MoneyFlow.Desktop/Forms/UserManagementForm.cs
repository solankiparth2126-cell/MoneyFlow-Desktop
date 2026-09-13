using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;
using Guna.UI2.WinForms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Forms;

public class UserManagementForm : Form
{
    private readonly ISecurityService _securityService;
    private readonly IUserContext _userContext;
    private readonly IAuditService _auditService;
    private readonly ICompanyContext _companyContext;

    // Controls
    private TabControl _tabMain = null!;

    // Tab 1: Users
    private Guna2DataGridView _dgvUsers = null!;
    private Button _btnAddUser = null!;
    private Button _btnEditUser = null!;
    private Button _btnToggleActive = null!;
    private Button _btnResetPassword = null!;
    private Button _btnRefreshUsers = null!;

    // Tab 2: Roles & Permissions
    private ComboBox _cmbRoles = null!;
    private Label _lblRoleDescription = null!;
    private TreeView _tvPermissions = null!;
    private Button _btnSavePermissions = null!;
    private Button _btnSelectAllPerms = null!;
    private Button _btnClearPerms = null!;

    // Tab 3: Audit Trail
    private DateTimePicker _dtpAuditFrom = null!;
    private DateTimePicker _dtpAuditTo = null!;
    private ComboBox _cmbAuditModule = null!;
    private Button _btnFilterAudit = null!;
    private Button _btnExportAuditCsv = null!;
    private Guna2DataGridView _dgvAudit = null!;

    private List<RoleDto> _cachedRoles = new();

    public UserManagementForm(
        ISecurityService securityService,
        IUserContext userContext,
        IAuditService auditService,
        ICompanyContext companyContext)
    {
        _securityService = securityService;
        _userContext = userContext;
        _auditService = auditService;
        _companyContext = companyContext;

        InitializeComponent();
        _ = LoadUsersAsync();
        _ = LoadRolesAndPermissionsAsync();
        _ = LoadAuditLogsAsync();
    }

    private void InitializeComponent()
    {
        Text = "User Management & Security Permissions — MoneyFlow Desktop Accounting";
        Size = new Size(1020, 700);
        MinimumSize = new Size(900, 580);
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
            Text = "USER MANAGEMENT & SECURITY PERMISSIONS",
            ForeColor = Color.White,
            Font = ExecLedgerTheme.UIBold12,
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
            Location = new Point(905, 13)
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, e) => Close();

        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(btnClose);

        // TabControl
        _tabMain = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = ExecLedgerTheme.UIRegular9
        };

        var tabUsers = new TabPage("  User Accounts  ") { BackColor = Color.FromArgb(248, 249, 251), Padding = new Padding(12) };
        var tabRoles = new TabPage("  Roles & Permission Matrix  ") { BackColor = Color.FromArgb(248, 249, 251), Padding = new Padding(12) };
        var tabAudit = new TabPage("  Audit Trail Register  ") { BackColor = Color.FromArgb(248, 249, 251), Padding = new Padding(12) };

        InitializeUsersTab(tabUsers);
        InitializeRolesTab(tabRoles);
        InitializeAuditTab(tabAudit);

        _tabMain.TabPages.Add(tabUsers);
        _tabMain.TabPages.Add(tabRoles);
        _tabMain.TabPages.Add(tabAudit);

        Controls.Add(_tabMain);
        Controls.Add(pnlHeader);

        KeyDown += UserManagementForm_KeyDown;
    }

    private void InitializeUsersTab(TabPage tab)
    {
        var pnl = new Panel { Dock = DockStyle.Fill };

        // Top Action Bar
        var pnlTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 45,
            BackColor = Color.FromArgb(235, 240, 246),
            Padding = new Padding(10, 8, 10, 8)
        };

        _btnAddUser = new Button
        {
            Text = "+ Add New User...",
            Font = ExecLedgerTheme.UIBold9,
            BackColor = Color.FromArgb(39, 174, 96),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(10, 8),
            Width = 140,
            Height = 28
        };
        _btnAddUser.FlatAppearance.BorderSize = 0;
        _btnAddUser.Click += async (s, e) => await ShowAddUserDialogAsync();

        _btnEditUser = new Button
        {
            Text = "Edit User...",
            Font = ExecLedgerTheme.UIRegular9,
            Location = new Point(160, 8),
            Width = 100,
            Height = 28
        };
        _btnEditUser.Click += async (s, e) => await ShowEditUserDialogAsync();

        _btnToggleActive = new Button
        {
            Text = "Activate / Deactivate",
            Font = ExecLedgerTheme.UIRegular9,
            Location = new Point(270, 8),
            Width = 150,
            Height = 28
        };
        _btnToggleActive.Click += async (s, e) => await ToggleSelectedUserStatusAsync();

        _btnResetPassword = new Button
        {
            Text = "Reset Password...",
            Font = ExecLedgerTheme.UIRegular9,
            Location = new Point(430, 8),
            Width = 130,
            Height = 28
        };
        _btnResetPassword.Click += (s, e) => ShowResetPasswordDialog();

        _btnRefreshUsers = new Button
        {
            Text = "Refresh (F5)",
            Font = ExecLedgerTheme.UIRegular9,
            Location = new Point(570, 8),
            Width = 95,
            Height = 28
        };
        _btnRefreshUsers.Click += async (s, e) => await LoadUsersAsync();

        pnlTop.Controls.Add(_btnAddUser);
        pnlTop.Controls.Add(_btnEditUser);
        pnlTop.Controls.Add(_btnToggleActive);
        pnlTop.Controls.Add(_btnResetPassword);
        pnlTop.Controls.Add(_btnRefreshUsers);

        // DataGridView
        _dgvUsers = new Guna2DataGridView
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

        _dgvUsers.Columns.Add("UserId", "User ID");
        _dgvUsers.Columns.Add("Username", "Username");
        _dgvUsers.Columns.Add("FullName", "Full Name");
        _dgvUsers.Columns.Add("Role", "Role");
        _dgvUsers.Columns.Add("Email", "Email");
        _dgvUsers.Columns.Add("Status", "Status");
        _dgvUsers.Columns.Add("LastLogin", "Last Login");
        _dgvUsers.Columns.Add("CreatedAt", "Created Date");

        _dgvUsers.Columns[0].FillWeight = 8;
        _dgvUsers.Columns[1].FillWeight = 15;
        _dgvUsers.Columns[2].FillWeight = 20;
        _dgvUsers.Columns[3].FillWeight = 15;
        _dgvUsers.Columns[4].FillWeight = 18;
        _dgvUsers.Columns[5].FillWeight = 10;
        _dgvUsers.Columns[6].FillWeight = 14;
        _dgvUsers.Columns[7].FillWeight = 12;

        pnl.Controls.Add(_dgvUsers);
        pnl.Controls.Add(pnlTop);
        tab.Controls.Add(pnl);
    }

    private void InitializeRolesTab(TabPage tab)
    {
        var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };

        // Top Selector
        var pnlTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = Color.FromArgb(235, 240, 246),
            Padding = new Padding(12, 10, 12, 10)
        };

        var lblRole = new Label { Text = "Select Role to Configure:", Location = new Point(12, 15), AutoSize = true, Font = ExecLedgerTheme.UIBold9 };
        _cmbRoles = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(190, 12),
            Width = 220,
            Font = ExecLedgerTheme.UIRegular9
        };
        _cmbRoles.SelectedIndexChanged += async (s, e) => await DisplayRolePermissionsAsync();

        _lblRoleDescription = new Label
        {
            Text = "Role Description",
            Location = new Point(430, 15),
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 110, 120),
            Font = ExecLedgerTheme.UIRegular8
        };

        pnlTop.Controls.Add(lblRole);
        pnlTop.Controls.Add(_cmbRoles);
        pnlTop.Controls.Add(_lblRoleDescription);

        // Permissions TreeView
        _tvPermissions = new TreeView
        {
            Dock = DockStyle.Fill,
            CheckBoxes = true,
            Font = ExecLedgerTheme.UIRegular9,
            BorderStyle = BorderStyle.FixedSingle
        };
        _tvPermissions.AfterCheck += (s, e) =>
        {
            if (e.Action != TreeViewAction.Unknown && e.Node != null)
            {
                foreach (TreeNode child in e.Node.Nodes)
                {
                    child.Checked = e.Node.Checked;
                }
            }
        };

        // Bottom Action Bar
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 45,
            BackColor = Color.FromArgb(235, 240, 246),
            Padding = new Padding(10, 8, 10, 8)
        };

        _btnSavePermissions = new Button
        {
            Text = "Save Role Permissions",
            Font = ExecLedgerTheme.UIBold9,
            BackColor = Color.FromArgb(41, 128, 185),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(10, 8),
            Width = 200,
            Height = 28
        };
        _btnSavePermissions.FlatAppearance.BorderSize = 0;
        _btnSavePermissions.Click += async (s, e) => await SaveRolePermissionsAsync();

        _btnSelectAllPerms = new Button
        {
            Text = "Check All",
            Location = new Point(220, 8),
            Width = 90,
            Height = 28
        };
        _btnSelectAllPerms.Click += (s, e) => SetAllTreeChecked(true);

        _btnClearPerms = new Button
        {
            Text = "Uncheck All",
            Location = new Point(320, 8),
            Width = 95,
            Height = 28
        };
        _btnClearPerms.Click += (s, e) => SetAllTreeChecked(false);

        pnlBottom.Controls.Add(_btnSavePermissions);
        pnlBottom.Controls.Add(_btnSelectAllPerms);
        pnlBottom.Controls.Add(_btnClearPerms);

        pnl.Controls.Add(_tvPermissions);
        pnl.Controls.Add(pnlBottom);
        pnl.Controls.Add(pnlTop);

        tab.Controls.Add(pnl);
    }

    private void InitializeAuditTab(TabPage tab)
    {
        var pnl = new Panel { Dock = DockStyle.Fill };

        // Filter Bar
        var pnlFilter = new Panel
        {
            Dock = DockStyle.Top,
            Height = 45,
            BackColor = Color.FromArgb(235, 240, 246),
            Padding = new Padding(10, 8, 10, 8)
        };

        var lblFrom = new Label { Text = "From:", Location = new Point(10, 12), AutoSize = true };
        _dtpAuditFrom = new DateTimePicker { Location = new Point(50, 9), Width = 110, Value = DateTime.Today.AddDays(-30) };

        var lblTo = new Label { Text = "To:", Location = new Point(170, 12), AutoSize = true };
        _dtpAuditTo = new DateTimePicker { Location = new Point(195, 9), Width = 110, Value = DateTime.Today.AddDays(1) };

        var lblMod = new Label { Text = "Module:", Location = new Point(315, 12), AutoSize = true };
        _cmbAuditModule = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(370, 9),
            Width = 110
        };
        _cmbAuditModule.Items.AddRange(new object[] { "All Modules", "Company", "Masters", "Transactions", "Inventory", "Reports", "Utilities", "Security" });
        _cmbAuditModule.SelectedIndex = 0;

        _btnFilterAudit = new Button
        {
            Text = "Apply Filter",
            Font = ExecLedgerTheme.UIBold9,
            Location = new Point(490, 8),
            Width = 95,
            Height = 27
        };
        _btnFilterAudit.Click += async (s, e) => await LoadAuditLogsAsync();

        _btnExportAuditCsv = new Button
        {
            Text = "Export to CSV",
            Location = new Point(595, 8),
            Width = 110,
            Height = 27
        };
        _btnExportAuditCsv.Click += (s, e) => ExportAuditToCsv();

        pnlFilter.Controls.Add(lblFrom);
        pnlFilter.Controls.Add(_dtpAuditFrom);
        pnlFilter.Controls.Add(lblTo);
        pnlFilter.Controls.Add(_dtpAuditTo);
        pnlFilter.Controls.Add(lblMod);
        pnlFilter.Controls.Add(_cmbAuditModule);
        pnlFilter.Controls.Add(_btnFilterAudit);
        pnlFilter.Controls.Add(_btnExportAuditCsv);

        // DataGridView
        _dgvAudit = new Guna2DataGridView
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

        _dgvAudit.Columns.Add("Timestamp", "Timestamp");
        _dgvAudit.Columns.Add("Username", "User");
        _dgvAudit.Columns.Add("Action", "Action");
        _dgvAudit.Columns.Add("Module", "Module");
        _dgvAudit.Columns.Add("RecordId", "Record ID");
        _dgvAudit.Columns.Add("Description", "Description");

        _dgvAudit.Columns[0].FillWeight = 16;
        _dgvAudit.Columns[1].FillWeight = 12;
        _dgvAudit.Columns[2].FillWeight = 12;
        _dgvAudit.Columns[3].FillWeight = 12;
        _dgvAudit.Columns[4].FillWeight = 10;
        _dgvAudit.Columns[5].FillWeight = 38;

        pnl.Controls.Add(_dgvAudit);
        pnl.Controls.Add(pnlFilter);

        tab.Controls.Add(pnl);
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            var users = await _securityService.GetAllUsersAsync();
            _dgvUsers.Rows.Clear();

            foreach (var u in users)
            {
                int rowIdx = _dgvUsers.Rows.Add(
                    u.UserId,
                    u.Username,
                    u.FullName,
                    u.RoleName,
                    u.Email,
                    u.IsActive ? "Active" : "Inactive",
                    u.LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? "Never",
                    u.CreatedAt.ToString("yyyy-MM-dd")
                );

                if (!u.IsActive)
                {
                    _dgvUsers.Rows[rowIdx].DefaultCellStyle.ForeColor = Color.FromArgb(180, 50, 50);
                }

                _dgvUsers.Rows[rowIdx].Tag = u;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed loading users: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadRolesAndPermissionsAsync()
    {
        try
        {
            _cachedRoles = (await _securityService.GetAllRolesAsync()).ToList();
            _cmbRoles.Items.Clear();

            foreach (var r in _cachedRoles)
            {
                _cmbRoles.Items.Add(r.RoleName);
            }

            if (_cmbRoles.Items.Count > 0)
            {
                _cmbRoles.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed loading roles: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task DisplayRolePermissionsAsync()
    {
        string selectedRole = _cmbRoles.SelectedItem?.ToString() ?? string.Empty;
        var role = _cachedRoles.FirstOrDefault(r => r.RoleName == selectedRole);
        if (role == null) return;

        _lblRoleDescription.Text = $"{role.Description} ({role.UsersCount} active users assigned)";

        try
        {
            var permissions = await _securityService.GetAllPermissionsAsync(role.RoleId);

            _tvPermissions.BeginUpdate();
            _tvPermissions.Nodes.Clear();

            var groups = permissions.GroupBy(p => p.Module).OrderBy(g => g.Key);

            foreach (var grp in groups)
            {
                var modNode = new TreeNode(grp.Key) { Tag = grp.Key };
                bool allGranted = true;

                foreach (var p in grp.OrderBy(x => x.PermissionKey))
                {
                    var childNode = new TreeNode($"{p.PermissionKey} — {p.Description}")
                    {
                        Tag = p.PermissionKey,
                        Checked = p.IsGranted
                    };

                    if (!p.IsGranted) allGranted = false;
                    modNode.Nodes.Add(childNode);
                }

                modNode.Checked = allGranted && grp.Any();
                _tvPermissions.Nodes.Add(modNode);
            }

            _tvPermissions.ExpandAll();
            _tvPermissions.EndUpdate();

            // Disable editing permissions on Administrator
            bool isAdmin = string.Equals(selectedRole, "Administrator", StringComparison.OrdinalIgnoreCase);
            _btnSavePermissions.Enabled = !isAdmin;
            _btnSelectAllPerms.Enabled = !isAdmin;
            _btnClearPerms.Enabled = !isAdmin;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed loading permissions: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task SaveRolePermissionsAsync()
    {
        string selectedRole = _cmbRoles.SelectedItem?.ToString() ?? string.Empty;
        var role = _cachedRoles.FirstOrDefault(r => r.RoleName == selectedRole);
        if (role == null) return;

        if (string.Equals(selectedRole, "Administrator", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show("The Administrator role always has full permissions.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var checkedKeys = new List<string>();
        foreach (TreeNode parent in _tvPermissions.Nodes)
        {
            foreach (TreeNode child in parent.Nodes)
            {
                if (child.Checked && child.Tag is string key)
                {
                    checkedKeys.Add(key);
                }
            }
        }

        try
        {
            var dto = new RolePermissionsUpdateDto
            {
                RoleId = role.RoleId,
                PermissionKeys = checkedKeys
            };

            await _securityService.UpdateRolePermissionsAsync(dto);
            MessageBox.Show($"Permissions for role '{role.RoleName}' successfully updated ({checkedKeys.Count} permissions granted).", "Saved", MessageBoxButtons.OK, MessageBoxIcon.Information);

            await LoadAuditLogsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed saving permissions: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetAllTreeChecked(bool check)
    {
        foreach (TreeNode parent in _tvPermissions.Nodes)
        {
            parent.Checked = check;
            foreach (TreeNode child in parent.Nodes)
            {
                child.Checked = check;
            }
        }
    }

    private async Task ShowAddUserDialogAsync()
    {
        using var dlg = new Form
        {
            Text = "Create New User Account",
            Size = new Size(420, 360),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Font = ExecLedgerTheme.UIRegular9
        };

        var lblU = new Label { Text = "Username:", Location = new Point(20, 25), AutoSize = true };
        var txtU = new TextBox { Location = new Point(120, 22), Width = 250 };

        var lblF = new Label { Text = "Full Name:", Location = new Point(20, 65), AutoSize = true };
        var txtF = new TextBox { Location = new Point(120, 62), Width = 250 };

        var lblE = new Label { Text = "Email:", Location = new Point(20, 105), AutoSize = true };
        var txtE = new TextBox { Location = new Point(120, 102), Width = 250 };

        var lblR = new Label { Text = "Role:", Location = new Point(20, 145), AutoSize = true };
        var cmbR = new ComboBox { Location = new Point(120, 142), Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
        foreach (var r in _cachedRoles) cmbR.Items.Add(r.RoleName);
        if (cmbR.Items.Count > 0) cmbR.SelectedIndex = 1; // Default to Accountant

        var lblP = new Label { Text = "Password:", Location = new Point(20, 185), AutoSize = true };
        var txtP = new TextBox { Location = new Point(120, 182), Width = 250, UseSystemPasswordChar = true };

        var chkAct = new CheckBox { Text = "Active Account", Checked = true, Location = new Point(120, 220), AutoSize = true };

        var btnSave = new Button { Text = "Create User", BackColor = Color.FromArgb(39, 174, 96), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(170, 260), Width = 100, Height = 30 };
        btnSave.FlatAppearance.BorderSize = 0;

        var btnCancel = new Button { Text = "Cancel", Location = new Point(280, 260), Width = 90, Height = 30 };
        btnCancel.Click += (s, e) => dlg.Close();

        btnSave.Click += async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txtU.Text) || string.IsNullOrWhiteSpace(txtP.Text))
            {
                MessageBox.Show("Username and Password are required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedRole = _cachedRoles.FirstOrDefault(r => r.RoleName == cmbR.SelectedItem?.ToString());
            if (selectedRole == null) return;

            try
            {
                var dto = new UserCreateDto
                {
                    Username = txtU.Text.Trim(),
                    FullName = txtF.Text.Trim(),
                    Email = txtE.Text.Trim(),
                    RoleId = selectedRole.RoleId,
                    Password = txtP.Text,
                    IsActive = chkAct.Checked
                };

                await _securityService.CreateUserAsync(dto);
                dlg.DialogResult = DialogResult.OK;
                dlg.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create user: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        dlg.Controls.AddRange(new Control[] { lblU, txtU, lblF, txtF, lblE, txtE, lblR, cmbR, lblP, txtP, chkAct, btnSave, btnCancel });

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            await LoadUsersAsync();
            await LoadAuditLogsAsync();
        }
    }

    private async Task ShowEditUserDialogAsync()
    {
        if (_dgvUsers.SelectedRows.Count == 0) return;
        var user = _dgvUsers.SelectedRows[0].Tag as UserSummaryDto;
        if (user == null) return;

        using var dlg = new Form
        {
            Text = $"Edit User — {user.Username}",
            Size = new Size(420, 280),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Font = ExecLedgerTheme.UIRegular9
        };

        var lblU = new Label { Text = "Username:", Location = new Point(20, 25), AutoSize = true };
        var txtU = new TextBox { Location = new Point(120, 22), Width = 250, Text = user.Username, ReadOnly = true, BackColor = Color.FromArgb(240, 240, 240) };

        var lblF = new Label { Text = "Full Name:", Location = new Point(20, 65), AutoSize = true };
        var txtF = new TextBox { Location = new Point(120, 62), Width = 250, Text = user.FullName };

        var lblE = new Label { Text = "Email:", Location = new Point(20, 105), AutoSize = true };
        var txtE = new TextBox { Location = new Point(120, 102), Width = 250, Text = user.Email };

        var lblR = new Label { Text = "Role:", Location = new Point(20, 145), AutoSize = true };
        var cmbR = new ComboBox { Location = new Point(120, 142), Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
        int selIdx = 0;
        for (int i = 0; i < _cachedRoles.Count; i++)
        {
            cmbR.Items.Add(_cachedRoles[i].RoleName);
            if (_cachedRoles[i].RoleId == user.RoleId) selIdx = i;
        }
        if (cmbR.Items.Count > 0) cmbR.SelectedIndex = selIdx;

        var btnSave = new Button { Text = "Save Changes", BackColor = Color.FromArgb(41, 128, 185), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(160, 195), Width = 110, Height = 30 };
        btnSave.FlatAppearance.BorderSize = 0;

        var btnCancel = new Button { Text = "Cancel", Location = new Point(280, 195), Width = 90, Height = 30 };
        btnCancel.Click += (s, e) => dlg.Close();

        btnSave.Click += async (s, e) =>
        {
            var selectedRole = _cachedRoles.FirstOrDefault(r => r.RoleName == cmbR.SelectedItem?.ToString());
            if (selectedRole == null) return;

            try
            {
                var dto = new UserUpdateDto
                {
                    UserId = user.UserId,
                    FullName = txtF.Text.Trim(),
                    Email = txtE.Text.Trim(),
                    RoleId = selectedRole.RoleId,
                    IsActive = user.IsActive
                };

                await _securityService.UpdateUserAsync(dto);
                dlg.DialogResult = DialogResult.OK;
                dlg.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed updating user: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        dlg.Controls.AddRange(new Control[] { lblU, txtU, lblF, txtF, lblE, txtE, lblR, cmbR, btnSave, btnCancel });

        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            await LoadUsersAsync();
            await LoadAuditLogsAsync();
        }
    }

    private async Task ToggleSelectedUserStatusAsync()
    {
        if (_dgvUsers.SelectedRows.Count == 0) return;
        var user = _dgvUsers.SelectedRows[0].Tag as UserSummaryDto;
        if (user == null) return;

        try
        {
            bool toggled = await _securityService.ToggleUserStatusAsync(user.UserId);
            if (toggled)
            {
                await LoadUsersAsync();
                await LoadAuditLogsAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void ShowResetPasswordDialog()
    {
        if (_dgvUsers.SelectedRows.Count == 0) return;
        var user = _dgvUsers.SelectedRows[0].Tag as UserSummaryDto;
        if (user == null) return;

        using var dlg = new Form
        {
            Text = $"Reset Password for '{user.Username}'",
            Size = new Size(380, 200),
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            Font = ExecLedgerTheme.UIRegular9
        };

        var lblP = new Label { Text = "New Password:", Location = new Point(20, 30), AutoSize = true };
        var txtP = new TextBox { Location = new Point(130, 27), Width = 200, UseSystemPasswordChar = true };

        var btnSave = new Button { Text = "Set Password", BackColor = Color.FromArgb(41, 128, 185), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Location = new Point(130, 80), Width = 110, Height = 30 };
        btnSave.FlatAppearance.BorderSize = 0;

        btnSave.Click += async (s, e) =>
        {
            if (string.IsNullOrWhiteSpace(txtP.Text) || txtP.Text.Length < 4)
            {
                MessageBox.Show("Password must be at least 4 characters.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                await _securityService.ResetPasswordAsync(user.UserId, txtP.Text);
                MessageBox.Show($"Password for '{user.Username}' has been reset successfully.", "Password Reset", MessageBoxButtons.OK, MessageBoxIcon.Information);
                dlg.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        dlg.Controls.AddRange(new Control[] { lblP, txtP, btnSave });
        dlg.ShowDialog(this);
    }

    private async Task LoadAuditLogsAsync()
    {
        try
        {
            string? moduleFilter = _cmbAuditModule.SelectedIndex > 0 ? _cmbAuditModule.SelectedItem?.ToString() : null;

            var filter = new AuditLogFilterDto
            {
                FromDate = _dtpAuditFrom.Value.Date,
                ToDate = _dtpAuditTo.Value.Date.AddDays(1).AddTicks(-1),
                Module = moduleFilter,
                MaxRecords = 300
            };

            var logs = await _auditService.GetLogsAsync(filter);
            _dgvAudit.Rows.Clear();

            foreach (var log in logs)
            {
                _dgvAudit.Rows.Add(
                    log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
                    log.Username,
                    log.Action,
                    log.Module,
                    log.RecordId,
                    log.Description
                );
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed loading audit logs: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportAuditToCsv()
    {
        if (_dgvAudit.Rows.Count == 0)
        {
            MessageBox.Show("No audit records to export.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog();
        sfd.Filter = "CSV Files (*.csv)|*.csv";
        sfd.FileName = $"MoneyFlow_AuditTrail_{DateTime.Now:yyyyMMdd_HHmm}.csv";

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("Timestamp,Username,Action,Module,RecordId,Description");

                foreach (DataGridViewRow row in _dgvAudit.Rows)
                {
                    if (row.IsNewRow) continue;
                    var parts = new string[6];
                    for (int i = 0; i < 6; i++)
                    {
                        string val = row.Cells[i].Value?.ToString() ?? string.Empty;
                        if (val.Contains(",") || val.Contains("\""))
                        {
                            val = $"\"{val.Replace("\"", "\"\"")}\"";
                        }
                        parts[i] = val;
                    }
                    sb.AppendLine(string.Join(",", parts));
                }

                File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show($"Audit log exported successfully to:\n{sfd.FileName}", "Export Completed", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void UserManagementForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F5)
        {
            if (_tabMain.SelectedIndex == 0) _ = LoadUsersAsync();
            else if (_tabMain.SelectedIndex == 2) _ = LoadAuditLogsAsync();
            e.Handled = true;
        }
    }
}
