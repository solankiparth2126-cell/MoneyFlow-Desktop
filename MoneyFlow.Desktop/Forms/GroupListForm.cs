using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Tally-style Master Chart of Accounts (Groups & Ledgers).
/// Features a real database-driven hierarchy tree displaying Groups, Sub-Groups, and Ledgers,
/// distinct icons and visual hierarchy, keyboard navigation, and full Master CRUD operations.
/// </summary>
public class GroupListForm : Form
{
    private readonly IGroupService _groupService;
    private readonly IAccountingHierarchyService _hierarchyService;
    private readonly ILedgerService _ledgerService;
    private readonly ICompanyContext _companyContext;

    private TreeView tvAccounts = null!;
    private Guna2DataGridView dgvDetails = null!;
    private TextBox txtSearch = null!;
    private Button btnCreateGroup = null!;
    private Button btnCreateLedger = null!;
    private Button btnAlter = null!;
    private Button btnDelete = null!;
    private Button btnExpandAll = null!;
    private Button btnCollapseAll = null!;
    private Button btnClose = null!;
    private Label lblSelectionInfo = null!;

    private List<AccountHierarchyNodeDto> _rootNodes = new();

    public GroupListForm(
        IGroupService groupService,
        IAccountingHierarchyService hierarchyService,
        ILedgerService ledgerService,
        ICompanyContext companyContext)
    {
        _groupService = groupService;
        _hierarchyService = hierarchyService;
        _ledgerService = ledgerService;
        _companyContext = companyContext;

        InitializeComponent();
        LoadAccountHierarchyAsync();
    }

    private void InitializeComponent()
    {
        Text = "MoneyFlow Desktop — Chart of Accounts (Groups & Ledgers)";
        Size = new Size(1060, 680);
        MinimumSize = new Size(960, 580);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = true;
        BackColor = Color.FromArgb(245, 247, 250);
        Font = ExecLedgerTheme.UIRegular9;
        KeyPreview = true;

        // Header Panel
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = ExecLedgerTheme.PrimaryNavy
        };
        var lblTitle = new Label
        {
            Text = $"Chart of Accounts (Groups & Ledgers) — {_companyContext.CurrentCompany?.CompanyName}",
            Font = ExecLedgerTheme.UIBold11,
            ForeColor = Color.White,
            Location = new Point(18, 16),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblTitle);
        Controls.Add(headerPanel);

        // Search & Filter Panel
        var searchPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 46,
            BackColor = Color.FromArgb(235, 238, 242)
        };
        var lblSearch = new Label { Text = "Search Chart of Accounts:", Location = new Point(18, 14), AutoSize = true, Font = ExecLedgerTheme.UIBold9 };
        txtSearch = new TextBox { Location = new Point(190, 11), Width = 340, Font = ExecLedgerTheme.UIRegular10 };
        txtSearch.TextChanged += async (s, e) => await OnSearchChangedAsync();

        var btnClear = new Button { Text = "Clear", Location = new Point(540, 10), Size = new Size(70, 26), BackColor = Color.White };
        btnClear.Click += (s, e) => txtSearch.Clear();

        btnExpandAll = new Button { Text = "Expand All", Location = new Point(625, 10), Size = new Size(85, 26), BackColor = Color.White };
        btnExpandAll.Click += (s, e) => tvAccounts.ExpandAll();

        btnCollapseAll = new Button { Text = "Collapse All", Location = new Point(715, 10), Size = new Size(85, 26), BackColor = Color.White };
        btnCollapseAll.Click += (s, e) => tvAccounts.CollapseAll();

        searchPanel.Controls.Add(lblSearch);
        searchPanel.Controls.Add(txtSearch);
        searchPanel.Controls.Add(btnClear);
        searchPanel.Controls.Add(btnExpandAll);
        searchPanel.Controls.Add(btnCollapseAll);
        Controls.Add(searchPanel);

        // Main Split Container: Left TreeView (Chart of Accounts), Right Grid
        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 420,
            BorderStyle = BorderStyle.None
        };

        // Left: TreeView
        var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 4, 8) };
        var lblTreeHeader = new Label
        {
            Text = "Hierarchical Structure (Groups & Ledgers)",
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = Color.FromArgb(226, 232, 240),
            Font = ExecLedgerTheme.UIBold9,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };
        tvAccounts = new TreeView
        {
            Dock = DockStyle.Fill,
            Font = ExecLedgerTheme.UIRegular9,
            HideSelection = false,
            ShowPlusMinus = true,
            ShowLines = true,
            ShowRootLines = true,
            ItemHeight = 22,
            BorderStyle = BorderStyle.FixedSingle
        };
        tvAccounts.AfterSelect += (s, e) => OnTreeNodeSelected();
        tvAccounts.KeyDown += OnTreeKeyDown;
        tvAccounts.NodeMouseDoubleClick += (s, e) => AlterSelectedNode();

        leftPanel.Controls.Add(tvAccounts);
        leftPanel.Controls.Add(lblTreeHeader);
        splitContainer.Panel1.Controls.Add(leftPanel);

        // Right: Details Panel / Grid
        var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 8, 12, 8) };
        lblSelectionInfo = new Label
        {
            Text = "Child Accounts & Properties",
            Dock = DockStyle.Top,
            Height = 28,
            BackColor = Color.FromArgb(226, 232, 240),
            Font = ExecLedgerTheme.UIBold9,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 0, 0)
        };
        dgvDetails = new Guna2DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowTemplate = { Height = 26 }
        };

        dgvDetails.Columns.Add("Id", "ID");
        dgvDetails.Columns["Id"]!.Visible = false;
        dgvDetails.Columns.Add("Type", "Type");
        dgvDetails.Columns.Add("Name", "Name");
        dgvDetails.Columns.Add("HierarchyPath", "Full Path / Under");
        dgvDetails.Columns.Add("Nature", "Nature");
        dgvDetails.Columns.Add("Balance", "Opening Balance");

        dgvDetails.Columns["Type"]!.FillWeight = 12;
        dgvDetails.Columns["Name"]!.FillWeight = 30;
        dgvDetails.Columns["HierarchyPath"]!.FillWeight = 32;
        dgvDetails.Columns["Nature"]!.FillWeight = 13;
        dgvDetails.Columns["Balance"]!.FillWeight = 13;

        dgvDetails.DoubleClick += (s, e) => AlterSelectedGridRow();
        dgvDetails.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                AlterSelectedGridRow();
            }
        };

        rightPanel.Controls.Add(dgvDetails);
        rightPanel.Controls.Add(lblSelectionInfo);
        splitContainer.Panel2.Controls.Add(rightPanel);

        Controls.Add(splitContainer);

        // Bottom Action Buttons Panel
        var btnPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = Color.FromArgb(235, 238, 242),
            Padding = new Padding(12, 10, 12, 10)
        };

        btnCreateGroup = new Button
        {
            Text = "&Create Group (Alt+C)",
            Location = new Point(12, 11),
            Size = new Size(155, 34),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            Font = ExecLedgerTheme.UIBold9
        };
        btnCreateGroup.Click += (s, e) => CreateGroup();

        btnCreateLedger = new Button
        {
            Text = "Create &Ledger (Alt+L)",
            Location = new Point(175, 11),
            Size = new Size(160, 34),
            BackColor = Color.FromArgb(14, 165, 233),
            ForeColor = Color.White,
            Font = ExecLedgerTheme.UIBold9
        };
        btnCreateLedger.Click += (s, e) => CreateLedgerUnderSelectedGroup();

        btnAlter = new Button
        {
            Text = "&Alter (Alt+A)",
            Location = new Point(343, 11),
            Size = new Size(120, 34),
            BackColor = Color.FromArgb(245, 158, 11),
            ForeColor = Color.White,
            Font = ExecLedgerTheme.UIBold9
        };
        btnAlter.Click += (s, e) => AlterSelectedNode();

        btnDelete = new Button
        {
            Text = "&Delete (Alt+D)",
            Location = new Point(471, 11),
            Size = new Size(120, 34),
            BackColor = Color.FromArgb(239, 68, 68),
            ForeColor = Color.White,
            Font = ExecLedgerTheme.UIBold9
        };
        btnDelete.Click += async (s, e) => await DeleteSelectedNodeAsync();

        btnClose = new Button
        {
            Text = "Close (Esc)",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(915, 11),
            Size = new Size(125, 34),
            BackColor = Color.FromArgb(226, 232, 240),
            Font = ExecLedgerTheme.UIRegular9
        };
        btnClose.Click += (s, e) => Close();

        btnPanel.Controls.Add(btnCreateGroup);
        btnPanel.Controls.Add(btnCreateLedger);
        btnPanel.Controls.Add(btnAlter);
        btnPanel.Controls.Add(btnDelete);
        btnPanel.Controls.Add(btnClose);
        Controls.Add(btnPanel);

        CancelButton = btnClose;
    }

    private async void LoadAccountHierarchyAsync()
    {
        if (_companyContext.CurrentCompany == null) return;
        int companyId = _companyContext.CurrentCompany.CompanyId;

        tvAccounts.BeginUpdate();
        tvAccounts.Nodes.Clear();

        _rootNodes = (await _hierarchyService.GetAccountTreeAsync(companyId, includeLedgers: true)).ToList();
        foreach (var root in _rootNodes)
        {
            tvAccounts.Nodes.Add(BuildTreeNode(root));
        }

        tvAccounts.ExpandAll();
        if (tvAccounts.Nodes.Count > 0)
        {
            tvAccounts.SelectedNode = tvAccounts.Nodes[0];
        }

        tvAccounts.EndUpdate();

        PopulateGridFromSelection();
    }

    private TreeNode BuildTreeNode(AccountHierarchyNodeDto dto)
    {
        string icon = dto.IsGroup ? "📁" : "📄";
        string label = dto.IsGroup ? $"{icon} {dto.Name} ({dto.Nature})" : $"{icon} {dto.Name}";
        var node = new TreeNode(label)
        {
            Tag = dto,
            NodeFont = dto.IsGroup ? ExecLedgerTheme.UIBold9 : ExecLedgerTheme.UIRegular9,
            ForeColor = dto.IsGroup ? ExecLedgerTheme.PrimaryNavy : ExecLedgerTheme.PrimaryText
        };

        foreach (var child in dto.Children)
        {
            node.Nodes.Add(BuildTreeNode(child));
        }

        return node;
    }

    private void OnTreeNodeSelected()
    {
        PopulateGridFromSelection();
    }

    private void PopulateGridFromSelection()
    {
        dgvDetails.Rows.Clear();
        if (tvAccounts.SelectedNode?.Tag is not AccountHierarchyNodeDto selected) return;

        if (selected.IsGroup)
        {
            lblSelectionInfo.Text = $"Group: {selected.Path} — ({selected.SubGroupsCount} Sub-Groups, {selected.LedgersCount} Ledgers)";
            foreach (var child in selected.Children)
            {
                string typeLabel = child.IsGroup ? "[Group]" : "[Ledger]";
                string balanceStr = child.IsGroup
                    ? "—"
                    : $"₹{child.Balance:N2} {(child.BalanceType == Core.Enums.BalanceType.Debit ? "Dr" : "Cr")}";

                dgvDetails.Rows.Add(
                    child.Id,
                    typeLabel,
                    child.Name,
                    child.Path,
                    child.Nature.ToString(),
                    balanceStr);
            }
        }
        else
        {
            lblSelectionInfo.Text = $"Ledger: {selected.Path}";
            string balanceStr = $"₹{selected.Balance:N2} {(selected.BalanceType == Core.Enums.BalanceType.Debit ? "Dr" : "Cr")}";
            dgvDetails.Rows.Add(
                selected.Id,
                "[Ledger]",
                selected.Name,
                selected.Path,
                selected.Nature.ToString(),
                balanceStr);
        }
    }

    private async Task OnSearchChangedAsync()
    {
        string term = txtSearch.Text.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            PopulateGridFromSelection();
            return;
        }

        if (_companyContext.CurrentCompany == null) return;
        int companyId = _companyContext.CurrentCompany.CompanyId;

        dgvDetails.Rows.Clear();
        lblSelectionInfo.Text = $"Search results for '{term}' in Chart of Accounts";

        var matches = await _hierarchyService.SearchLedgersAsync(companyId, term);
        foreach (var m in matches)
        {
            string balanceStr = $"₹{m.CurrentBalance:N2} {(m.CurrentBalanceType == Core.Enums.BalanceType.Debit ? "Dr" : "Cr")}";
            dgvDetails.Rows.Add(
                m.LedgerId,
                "[Ledger]",
                m.LedgerName,
                m.FullHierarchyPath,
                m.GroupNature.ToString(),
                balanceStr);
        }
    }

    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            AlterSelectedNode();
        }
        else if (e.KeyCode == Keys.Delete)
        {
            e.Handled = true;
            _ = DeleteSelectedNodeAsync();
        }
    }

    private void CreateGroup()
    {
        if (_companyContext.CurrentCompany == null) return;

        int? parentId = null;
        if (tvAccounts.SelectedNode?.Tag is AccountHierarchyNodeDto sel && sel.IsGroup)
        {
            parentId = sel.Id;
        }

        using var form = new GroupCreateEditForm(_groupService, _companyContext.CurrentCompany.CompanyId, null, parentId);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            LoadAccountHierarchyAsync();
        }
    }

    private void CreateLedgerUnderSelectedGroup()
    {
        if (_companyContext.CurrentCompany == null) return;

        int? groupId = null;
        if (tvAccounts.SelectedNode?.Tag is AccountHierarchyNodeDto sel)
        {
            groupId = sel.IsGroup ? sel.Id : sel.ParentGroupId;
        }

        using var form = new LedgerCreateEditForm(_ledgerService, _groupService, _companyContext.CurrentCompany.CompanyId, null, groupId);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            LoadAccountHierarchyAsync();
        }
    }

    private void AlterSelectedNode()
    {
        if (_companyContext.CurrentCompany == null) return;

        if (tvAccounts.SelectedNode?.Tag is AccountHierarchyNodeDto sel)
        {
            if (sel.IsGroup)
            {
                using var form = new GroupCreateEditForm(_groupService, _companyContext.CurrentCompany.CompanyId, sel.Id);
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    LoadAccountHierarchyAsync();
                }
            }
            else
            {
                using var form = new LedgerCreateEditForm(_ledgerService, _groupService, _companyContext.CurrentCompany.CompanyId, sel.Id);
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    LoadAccountHierarchyAsync();
                }
            }
        }
    }

    private void AlterSelectedGridRow()
    {
        if (_companyContext.CurrentCompany == null || dgvDetails.SelectedRows.Count == 0) return;

        var row = dgvDetails.SelectedRows[0];
        int id = Convert.ToInt32(row.Cells["Id"].Value);
        string type = row.Cells["Type"].Value?.ToString() ?? string.Empty;

        if (type.Contains("Group"))
        {
            using var form = new GroupCreateEditForm(_groupService, _companyContext.CurrentCompany.CompanyId, id);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                LoadAccountHierarchyAsync();
            }
        }
        else
        {
            using var form = new LedgerCreateEditForm(_ledgerService, _groupService, _companyContext.CurrentCompany.CompanyId, id);
            if (form.ShowDialog(this) == DialogResult.OK)
            {
                LoadAccountHierarchyAsync();
            }
        }
    }

    private async Task DeleteSelectedNodeAsync()
    {
        if (tvAccounts.SelectedNode?.Tag is not AccountHierarchyNodeDto sel)
        {
            MessageBox.Show("Please select a group or ledger to delete.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (sel.IsGroup)
        {
            var confirm = MessageBox.Show(
                $"Are you sure you want to delete the Group '{sel.Name}'?",
                "Confirm Delete Group",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm == DialogResult.Yes)
            {
                try
                {
                    await _groupService.DeleteGroupAsync(sel.Id);
                    MessageBox.Show("Group deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadAccountHierarchyAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Cannot Delete Group", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        else
        {
            var confirm = MessageBox.Show(
                $"Are you sure you want to delete the Ledger '{sel.Name}'?",
                "Confirm Delete Ledger",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm == DialogResult.Yes)
            {
                try
                {
                    await _ledgerService.DeleteLedgerAsync(sel.Id);
                    MessageBox.Show("Ledger deleted successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    LoadAccountHierarchyAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Cannot Delete Ledger", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
