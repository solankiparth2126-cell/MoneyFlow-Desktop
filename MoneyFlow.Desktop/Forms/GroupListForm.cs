using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Forms;

public class GroupListForm : Form
{
    private readonly IGroupService _groupService;
    private readonly ICompanyContext _companyContext;

    private TreeView tvGroups = null!;
    private DataGridView dgvGroups = null!;
    private TextBox txtSearch = null!;
    private Button btnCreate = null!;
    private Button btnAlter = null!;
    private Button btnDelete = null!;
    private Button btnClose = null!;

    public GroupListForm(IGroupService groupService, ICompanyContext companyContext)
    {
        _groupService = groupService;
        _companyContext = companyContext;

        InitializeComponent();
        LoadGroupDataAsync();
    }

    private void InitializeComponent()
    {
        this.Text = "MoneyFlow Desktop — Chart of Accounts (Groups)";
        this.Size = new Size(960, 600);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.FromArgb(245, 247, 250);
        this.Font = new Font("Segoe UI", 9.5F);

        // Header Panel
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = Color.FromArgb(24, 43, 73)
        };
        var lblTitle = new Label
        {
            Text = $"Account Groups (Chart of Accounts) — {_companyContext.CurrentCompany?.CompanyName}",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(18, 15),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblTitle);
        this.Controls.Add(headerPanel);

        // Search Bar Panel
        var searchPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 45,
            BackColor = Color.FromArgb(235, 238, 242)
        };
        var lblSearch = new Label { Text = "Search Groups:", Location = new Point(20, 13), AutoSize = true };
        txtSearch = new TextBox { Location = new Point(125, 10), Width = 300 };
        txtSearch.TextChanged += async (s, e) => await SearchGroupsAsync();

        var btnClear = new Button { Text = "Clear", Location = new Point(435, 9), Size = new Size(70, 26) };
        btnClear.Click += (s, e) => txtSearch.Clear();

        searchPanel.Controls.Add(lblSearch);
        searchPanel.Controls.Add(txtSearch);
        searchPanel.Controls.Add(btnClear);
        this.Controls.Add(searchPanel);

        // Main Split Container: Left TreeView, Right DataGridView
        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 340,
            BorderStyle = BorderStyle.Fixed3D
        };

        // Left: TreeView
        var leftPanel = new Panel { Dock = DockStyle.Fill };
        var lblTreeHeader = new Label
        {
            Text = "Hierarchy Tree",
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = Color.FromArgb(240, 243, 246),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };
        tvGroups = new TreeView
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5F),
            HideSelection = false
        };
        tvGroups.AfterSelect += (s, e) => OnTreeNodeSelected();
        leftPanel.Controls.Add(tvGroups);
        leftPanel.Controls.Add(lblTreeHeader);
        splitContainer.Panel1.Controls.Add(leftPanel);

        // Right: DataGridView
        var rightPanel = new Panel { Dock = DockStyle.Fill };
        dgvGroups = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        dgvGroups.Columns.Add("GroupId", "ID");
        dgvGroups.Columns["GroupId"]!.Visible = false;
        dgvGroups.Columns.Add("GroupName", "Group Name");
        dgvGroups.Columns.Add("ParentGroupName", "Under (Parent)");
        dgvGroups.Columns.Add("Nature", "Nature");
        dgvGroups.Columns.Add("SubGroupsCount", "Sub Groups");
        dgvGroups.Columns.Add("LedgersCount", "Ledgers");

        dgvGroups.Columns["GroupName"]!.FillWeight = 30;
        dgvGroups.Columns["ParentGroupName"]!.FillWeight = 25;
        dgvGroups.Columns["Nature"]!.FillWeight = 20;
        dgvGroups.Columns["SubGroupsCount"]!.FillWeight = 12;
        dgvGroups.Columns["LedgersCount"]!.FillWeight = 13;

        dgvGroups.DoubleClick += (s, e) => AlterSelectedGroup();
        dgvGroups.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                AlterSelectedGroup();
            }
        };

        rightPanel.Controls.Add(dgvGroups);
        splitContainer.Panel2.Controls.Add(rightPanel);

        this.Controls.Add(splitContainer);

        // Bottom Action Buttons Panel
        var btnPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            BackColor = Color.FromArgb(235, 238, 242)
        };

        btnCreate = new Button
        {
            Text = "&Create Group (Alt+C)",
            Location = new Point(20, 14),
            Size = new Size(160, 32),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold)
        };
        btnCreate.Click += (s, e) => CreateGroup();

        btnAlter = new Button
        {
            Text = "&Alter (Alt+A)",
            Location = new Point(190, 14),
            Size = new Size(130, 32),
            BackColor = Color.FromArgb(245, 158, 11),
            ForeColor = Color.White
        };
        btnAlter.Click += (s, e) => AlterSelectedGroup();

        btnDelete = new Button
        {
            Text = "&Delete (Alt+D)",
            Location = new Point(330, 14),
            Size = new Size(130, 32),
            BackColor = Color.FromArgb(239, 68, 68),
            ForeColor = Color.White
        };
        btnDelete.Click += async (s, e) => await DeleteSelectedGroupAsync();

        btnClose = new Button
        {
            Text = "Close (Esc)",
            Location = new Point(790, 14),
            Size = new Size(140, 32),
            BackColor = Color.FromArgb(226, 232, 240)
        };
        btnClose.Click += (s, e) => this.Close();

        btnPanel.Controls.Add(btnCreate);
        btnPanel.Controls.Add(btnAlter);
        btnPanel.Controls.Add(btnDelete);
        btnPanel.Controls.Add(btnClose);
        this.Controls.Add(btnPanel);

        this.CancelButton = btnClose;
    }

    private async void LoadGroupDataAsync()
    {
        if (_companyContext.CurrentCompany == null) return;
        int companyId = _companyContext.CurrentCompany.CompanyId;

        // 1. Load Tree
        tvGroups.Nodes.Clear();
        var treeNodes = await _groupService.GetGroupTreeAsync(companyId);
        foreach (var root in treeNodes)
        {
            tvGroups.Nodes.Add(CreateTreeNode(root));
        }
        tvGroups.ExpandAll();

        // 2. Load Grid
        await SearchGroupsAsync();
    }

    private TreeNode CreateTreeNode(GroupTreeNodeDto dto)
    {
        var node = new TreeNode($"{dto.GroupName} ({dto.Nature})")
        {
            Tag = dto.GroupId
        };

        foreach (var child in dto.Children)
        {
            node.Nodes.Add(CreateTreeNode(child));
        }

        return node;
    }

    private async Task SearchGroupsAsync()
    {
        if (_companyContext.CurrentCompany == null) return;
        dgvGroups.Rows.Clear();

        var groups = await _groupService.GetGroupsByCompanyAsync(_companyContext.CurrentCompany.CompanyId, txtSearch.Text);
        foreach (var g in groups)
        {
            dgvGroups.Rows.Add(
                g.GroupId,
                g.GroupName,
                g.ParentGroupName ?? "[Primary]",
                g.NatureDisplay,
                g.SubGroupsCount,
                g.LedgersCount);
        }
    }

    private void OnTreeNodeSelected()
    {
        if (tvGroups.SelectedNode?.Tag is int groupId)
        {
            // Select row in grid
            foreach (DataGridViewRow row in dgvGroups.Rows)
            {
                if (Convert.ToInt32(row.Cells["GroupId"].Value) == groupId)
                {
                    row.Selected = true;
                    dgvGroups.FirstDisplayedScrollingRowIndex = row.Index;
                    break;
                }
            }
        }
    }

    private int? GetSelectedGroupId()
    {
        if (dgvGroups.SelectedRows.Count > 0)
        {
            return Convert.ToInt32(dgvGroups.SelectedRows[0].Cells["GroupId"].Value);
        }
        if (tvGroups.SelectedNode?.Tag is int treeId)
        {
            return treeId;
        }
        return null;
    }

    private void CreateGroup()
    {
        if (_companyContext.CurrentCompany == null) return;

        using var form = new GroupCreateEditForm(_groupService, _companyContext.CurrentCompany.CompanyId);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            LoadGroupDataAsync();
        }
    }

    private void AlterSelectedGroup()
    {
        if (_companyContext.CurrentCompany == null) return;
        int? id = GetSelectedGroupId();
        if (!id.HasValue)
        {
            MessageBox.Show("Please select a group to alter.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new GroupCreateEditForm(_groupService, _companyContext.CurrentCompany.CompanyId, id.Value);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            LoadGroupDataAsync();
        }
    }

    private async Task DeleteSelectedGroupAsync()
    {
        int? id = GetSelectedGroupId();
        if (!id.HasValue)
        {
            MessageBox.Show("Please select a group to delete.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            "Are you sure you want to delete this Account Group?",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm == DialogResult.Yes)
        {
            try
            {
                await _groupService.DeleteGroupAsync(id.Value);
                MessageBox.Show("Group deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                LoadGroupDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Cannot Delete Group", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
