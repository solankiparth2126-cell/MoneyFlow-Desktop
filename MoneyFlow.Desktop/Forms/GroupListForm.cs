using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Controls.Panels;
using MoneyFlow.Desktop.Navigation;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Full-screen keyboard-first Group Master for MoneyFlow.
/// Follows Tally-style workflow:
/// 1. Group Master List loads first with all database groups (sorted GroupId DESC, newest at top).
/// 2. Clicking Create or Alter replaces the list full-screen with Group Creation form.
/// 3. Save / ESC returns directly to Group Master List, refreshes DB, and highlights the newest group.
/// </summary>
public class GroupListForm : Form, IBackNavigable
{
    private readonly IGroupService _groupService;
    private readonly IAccountingHierarchyService _hierarchyService;
    private readonly ILedgerService _ledgerService;
    private readonly ICompanyContext _companyContext;

    // Outer View Switcher Containers
    private Panel pnlListContainer = null!;
    private Panel pnlCreationContainer = null!;

    // Group Master List UI
    private TextBox txtSearch = null!;
    private Guna2DataGridView dgvGroups = null!;
    private GroupMasterRightActionPanel pnlRightActions = null!;
    private Label lblTitle = null!;
    private Label lblRecordCount = null!;
    private Label lblGridFooter = null!;
    private Button btnScrollUp = null!;
    private Button btnScrollDown = null!;

    public GroupMasterRightActionPanel RightActionPanel => pnlRightActions;
    public Button ButtonCreate => pnlRightActions.ButtonCreate;
    public Button ButtonAlter => pnlRightActions.ButtonAlter;
    public Button ButtonDelete => pnlRightActions.ButtonDelete;
    public Button ButtonSearch => pnlRightActions.ButtonSearch;
    public Button ButtonFind => pnlRightActions.ButtonSearch;
    public Button ButtonClose => pnlRightActions.ButtonClose;
    public Guna2DataGridView Grid => dgvGroups;
    public Label TitleLabel => lblTitle;
    public Label FooterLabel => lblGridFooter;

    private List<GroupSummaryDto> _allGroups = new();
    private GroupCreateEditForm? _activeCreationForm;
    private bool _isCreationViewActive;

    public bool IsCreationViewActive => _isCreationViewActive;
    public Panel ListContainer => pnlListContainer;
    public Panel CreationContainer => pnlCreationContainer;
    public GroupCreateEditForm? ActiveCreationForm => _activeCreationForm;

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string lParam);
    private const int EM_SETCUEBANNER = 0x1501;

    public GroupListForm(
        IGroupService groupService,
        IAccountingHierarchyService hierarchyService,
        ILedgerService ledgerService,
        ICompanyContext companyContext)
    {
        _groupService = groupService ?? throw new ArgumentNullException(nameof(groupService));
        _hierarchyService = hierarchyService;
        _ledgerService = ledgerService;
        _companyContext = companyContext ?? throw new ArgumentNullException(nameof(companyContext));

        InitializeComponent();
        _ = LoadGroupsFromDatabaseAsync();
    }

    private void InitializeComponent()
    {
        Text = "MoneyFlow Desktop — Group Master";
        Size = new Size(1100, 700);
        MinimumSize = new Size(880, 520);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(240, 242, 245);
        Font = ExecLedgerTheme.UIRegular9;
        KeyPreview = true;

        // Container 1: Full-Screen Group Creation Host (hidden initially)
        pnlCreationContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(pnlCreationContainer);

        // Container 2: Group Master List
        pnlListContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Visible = true,
            BackColor = Color.FromArgb(240, 242, 245), // #F0F2F5
            Padding = new Padding(16, 16, 16, 16)      // 16px clean outer margin on all sides
        };

        // ── 1. Top Header Card (Solid Navy #1B365D, Height 54px, Clean Square Accounting Layout) ──
        var headerCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            BackColor = ExecLedgerTheme.PrimaryNavy // #1B365D
        };

        var compName = _companyContext.CurrentCompany?.CompanyName ?? "";
        lblTitle = new Label
        {
            Text = $"GROUP MASTER — {compName}",
            Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(18, 15),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        headerCard.Controls.Add(lblTitle);

        lblRecordCount = new Label
        {
            Text = "0 Groups",
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(203, 213, 225),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(headerCard.Width - 140, 18),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        headerCard.Controls.Add(lblRecordCount);

        // Header bottom gap (12px)
        var pnlHeaderGap = new Panel
        {
            Dock = DockStyle.Top,
            Height = 12,
            BackColor = Color.Transparent
        };

        // ── 2. Workspace Panel (Holds Main Content Card and Actions Card) ──
        var pnlWorkspace = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        // ── Right Action Panel (Dock = Right, Width = 200px, Light Blue Reference Image 1) ──
        pnlRightActions = new GroupMasterRightActionPanel
        {
            Dock = DockStyle.Right,
            Width = 200
        };
        pnlRightActions.CreateClicked += (s, e) => OpenCreateGroupView();
        pnlRightActions.AlterClicked += (s, e) => AlterSelectedGroup();
        pnlRightActions.DeleteClicked += async (s, e) => await DeleteSelectedGroupAsync();
        pnlRightActions.SearchClicked += (s, e) => FocusSearchBox();
        pnlRightActions.CloseClicked += (s, e) =>
        {
            if (HandleBackNavigation()) return;
            MoneyFlowEscController.HandleEsc(
                ActiveControl,
                this,
                closeAction: () => Close());
        };
        pnlRightActions.SetRowSelectedActionsEnabled(false);

        // Gap between Main Grid Area and Right Action Panel (12px)
        var pnlActionsGap = new Panel
        {
            Dock = DockStyle.Right,
            Width = 12,
            BackColor = Color.Transparent
        };

        // ── Main Content Area (Contains Search, Gap, and GridCard) ──
        var pnlMainArea = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        // ── Search Panel (Top of Main Area) ──
        var searchPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.White,
            Padding = new Padding(8, 6, 8, 6)
        };
        searchPanel.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(203, 213, 225), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, searchPanel.Width - 1, searchPanel.Height - 1);
        };

        var lblSearch = new Label
        {
            Text = "Search:",
            Location = new Point(12, 11),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42)
        };
        searchPanel.Controls.Add(lblSearch);

        txtSearch = new TextBox
        {
            Location = new Point(72, 7),
            Width = 360,
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            BorderStyle = BorderStyle.FixedSingle,
            ForeColor = Color.FromArgb(15, 23, 42)
        };
        txtSearch.TextChanged += async (s, e) => await OnSearchChangedAsync();
        txtSearch.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Down && dgvGroups.Rows.Count > 0)
            {
                dgvGroups.Focus();
                e.Handled = true;
            }
        };
        txtSearch.HandleCreated += (s, e) =>
        {
            SendMessage(txtSearch.Handle, EM_SETCUEBANNER, 1, "Type to search group name, alias, nature...");
        };
        searchPanel.Controls.Add(txtSearch);

        var btnClear = new Button
        {
            Text = "Clear",
            Location = new Point(440, 7),
            Size = new Size(64, 28),
            BackColor = Color.FromArgb(240, 242, 245),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Cursor = Cursors.Hand
        };
        btnClear.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnClear.Click += (s, e) =>
        {
            txtSearch.Clear();
            txtSearch.Focus();
        };
        searchPanel.Controls.Add(btnClear);        

        searchPanel.Resize += (s, e) =>
        {
            int maxSearchW = Math.Max(180, Math.Min(420, searchPanel.Width - 320));
            txtSearch.Width = maxSearchW;
            btnClear.Location = new Point(txtSearch.Right + 8, 7);
        };

        // Gap between SearchPanel and GridCard (10px)
        var pnlSearchGridGap = new Panel
        {
            Dock = DockStyle.Top,
            Height = 10,
            BackColor = Color.Transparent
        };

        // ── Grid Card (White container cleanly wrapping Grid and Footer inside a 1px border) ──
        var gridCard = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(1)
        };
        gridCard.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(203, 213, 225), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, gridCard.Width - 1, gridCard.Height - 1);
        };

        // ── DataGridView (Group Master Accounting Table) ──
        dgvGroups = new Guna2DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            ShowCellToolTips = false, // Clean accounting style: no popup tooltips over cells or headers
            ScrollBars = ScrollBars.None, // Clean accounting style: no visible vertical/horizontal scrollbar
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            GridColor = Color.FromArgb(226, 232, 240),
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AllowUserToResizeColumns = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            ColumnHeadersVisible = true,
            ColumnHeadersHeight = 36,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            EnableHeadersVisualStyles = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowTemplate = { Height = 30 }
        };

        // Column definitions with exact uppercase titles & proportional FillWeights (Section 7)
        dgvGroups.Columns.Clear();

        var colGroupId = new DataGridViewTextBoxColumn
        {
            Name = "GroupId",
            HeaderText = "Group ID",
            Visible = false
        };
        dgvGroups.Columns.Add(colGroupId);

        var colGroupName = new DataGridViewTextBoxColumn
        {
            Name = "GroupName",
            HeaderText = "GROUP NAME",
            FillWeight = 42,
            MinimumWidth = 160
        };
        dgvGroups.Columns.Add(colGroupName);

        var colUnder = new DataGridViewTextBoxColumn
        {
            Name = "UnderGroup",
            HeaderText = "UNDER",
            FillWeight = 23,
            MinimumWidth = 100
        };
        dgvGroups.Columns.Add(colUnder);

        var colNature = new DataGridViewTextBoxColumn
        {
            Name = "Nature",
            HeaderText = "NATURE",
            FillWeight = 23,
            MinimumWidth = 100
        };
        dgvGroups.Columns.Add(colNature);

        var colStatus = new DataGridViewTextBoxColumn
        {
            Name = "Status",
            HeaderText = "STATUS",
            FillWeight = 12,
            MinimumWidth = 80
        };
        dgvGroups.Columns.Add(colStatus);

        // Accounting Table Styling matching exact MoneyFlow specifications
        var headerStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(27, 54, 93), // Solid Navy #1B365D
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            SelectionBackColor = Color.FromArgb(27, 54, 93),
            SelectionForeColor = Color.White,
            Padding = new Padding(8, 0, 8, 0)
        };
        dgvGroups.ColumnHeadersDefaultCellStyle = headerStyle;
        dgvGroups.ThemeStyle.HeaderStyle.BackColor = Color.FromArgb(27, 54, 93);
        dgvGroups.ThemeStyle.HeaderStyle.ForeColor = Color.White;
        dgvGroups.ThemeStyle.HeaderStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        dgvGroups.ThemeStyle.HeaderStyle.Height = 36;

        var rowStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            SelectionBackColor = Color.FromArgb(224, 237, 253), // #E0EDFD
            SelectionForeColor = Color.FromArgb(15, 23, 42), // #0F172A
            Padding = new Padding(8, 0, 8, 0)
        };
        dgvGroups.DefaultCellStyle = rowStyle;
        dgvGroups.ThemeStyle.RowsStyle.BackColor = Color.White;
        dgvGroups.ThemeStyle.RowsStyle.ForeColor = Color.FromArgb(15, 23, 42);
        dgvGroups.ThemeStyle.RowsStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        dgvGroups.ThemeStyle.RowsStyle.SelectionBackColor = Color.FromArgb(224, 237, 253);
        dgvGroups.ThemeStyle.RowsStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);

        dgvGroups.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            SelectionBackColor = Color.FromArgb(224, 237, 253),
            SelectionForeColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(8, 0, 8, 0)
        };

        dgvGroups.DoubleClick += (s, e) => { if (ButtonAlter.Enabled) AlterSelectedGroup(); };
        dgvGroups.KeyDown += OnGridKeyDown;
        dgvGroups.SelectionChanged += (s, e) =>
        {
            UpdateActionButtonsEnabledState();
            EnsureSelectedRowVisible();
        };
        dgvGroups.Resize += (s, e) => EnsureSelectedRowVisible();
        dgvGroups.MouseWheel += (s, e) =>
        {
            if (e.Delta < 0)
                ScrollDownOneRow();
            else if (e.Delta > 0)
                ScrollUpOneRow();
        };
        dgvGroups.Scroll += (s, e) => UpdateScrollIndicators();

        // ── Grid Footer Bar with Down/Up Scroll Arrows (Section 19 & 38) ──
        var pnlGridFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 34,
            BackColor = Color.White
        };
        pnlGridFooter.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlGridFooter.Width, 0);
        };

        var pnlFooterRight = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 5, 8, 0)
        };

        btnScrollUp = new Button
        {
            Text = "▲",
            Size = new Size(28, 24),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(240, 242, 245),
            ForeColor = Color.FromArgb(27, 54, 93),
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Visible = false
        };
        btnScrollUp.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnScrollUp.Click += (s, e) => ScrollUpOneRow();

        btnScrollDown = new Button
        {
            Text = "▼",
            Size = new Size(28, 24),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(240, 242, 245),
            ForeColor = Color.FromArgb(27, 54, 93),
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnScrollDown.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnScrollDown.Click += (s, e) => ScrollDownOneRow();

        pnlFooterRight.Controls.Add(btnScrollUp);
        pnlFooterRight.Controls.Add(btnScrollDown);

        lblGridFooter = new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(46, 91, 136), // #2E5B88
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0)
        };

        pnlGridFooter.Controls.Add(pnlFooterRight);
        pnlGridFooter.Controls.Add(lblGridFooter);
        pnlFooterRight.SendToBack();
        lblGridFooter.BringToFront();

        // Assembly inside GridCard: Footer docks Bottom first, Grid docks Fill above it
        gridCard.Controls.Add(pnlGridFooter);
        gridCard.Controls.Add(dgvGroups);
        pnlGridFooter.SendToBack();
        dgvGroups.BringToFront();

        // Assembly inside MainArea: SearchPanel docks Top first, Gap docks Top second, GridCard docks Fill
        pnlMainArea.Controls.Add(searchPanel);
        pnlMainArea.Controls.Add(pnlSearchGridGap);
        pnlMainArea.Controls.Add(gridCard);
        searchPanel.SendToBack();
        pnlSearchGridGap.SendToBack();
        gridCard.BringToFront();

        // Assembly inside Workspace: Actions docks Right first, Gap docks Right second, MainArea docks Fill
        pnlWorkspace.Controls.Add(pnlRightActions);
        pnlWorkspace.Controls.Add(pnlActionsGap);
        pnlWorkspace.Controls.Add(pnlMainArea);
        pnlRightActions.SendToBack();
        pnlActionsGap.SendToBack();
        pnlMainArea.BringToFront();

        // Assembly inside ListContainer: Header docks Top first, Gap docks Top second, Workspace docks Fill
        pnlListContainer.Controls.Add(headerCard);
        pnlListContainer.Controls.Add(pnlHeaderGap);
        pnlListContainer.Controls.Add(pnlWorkspace);
        headerCard.SendToBack();
        pnlHeaderGap.SendToBack();
        pnlWorkspace.BringToFront();

        Controls.Add(pnlListContainer);

        // Window Shortcuts
        KeyDown += OnFormKeyDown;

        Shown += (s, e) =>
        {
            FocusFirstRowInGrid();
            UpdateActionButtonsEnabledState();
        };
    }

    /// <summary>
    /// Places keyboard focus on the first row of the grid with the first row selected.
    /// Default focus behavior when Group Master is opened.
    /// </summary>
    public void FocusFirstRowInGrid()
    {
        if (dgvGroups.Rows.Count > 0)
        {
            dgvGroups.ClearSelection();
            dgvGroups.Rows[0].Selected = true;
            if (dgvGroups.Columns.Contains("GroupName"))
            {
                dgvGroups.CurrentCell = dgvGroups.Rows[0].Cells["GroupName"];
            }
        }
        if (dgvGroups.CanFocus)
        {
            dgvGroups.Focus();
        }
    }

    /// <summary>
    /// Enables or disables ALTER and DELETE action buttons depending on whether a row is currently selected in the grid.
    /// </summary>
    public void UpdateActionButtonsEnabledState()
    {
        bool hasSelection = dgvGroups.SelectedRows.Count > 0 && dgvGroups.SelectedRows[0].Index >= 0;
        pnlRightActions.SetRowSelectedActionsEnabled(hasSelection);
    }

    /// <summary>
    /// Moves keyboard focus to the search textbox and selects existing search text for immediate typing.
    /// Triggered by clicking FIND or pressing Alt+F anywhere in Group Master.
    /// </summary>
    public void FocusSearchBox()
    {
        if (txtSearch.CanFocus)
        {
            txtSearch.Focus();
            txtSearch.SelectAll();
        }
    }

    /// <summary>
    /// Loads all groups for the current company from the database.
    /// Strictly orders by GroupId DESC (authoritative newest-first order).
    /// </summary>
    public async Task LoadGroupsFromDatabaseAsync(int? selectGroupId = null)
    {
        if (_companyContext.CurrentCompany == null) return;
        int companyId = _companyContext.CurrentCompany.CompanyId;
        lblTitle.Text = $"GROUP MASTER — {_companyContext.CurrentCompany.CompanyName}";

        try
        {
            // Database is the SINGLE SOURCE OF TRUTH
            var groups = await _groupService.GetGroupsByCompanyAsync(companyId, null);

            // Default Order: GroupId DESC (Newest record MUST appear at the TOP)
            _allGroups = groups.OrderByDescending(g => g.GroupId).ToList();

            ApplyFilterAndBind(selectGroupId);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load group master data: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyFilterAndBind(int? selectGroupId = null)
    {
        string term = txtSearch.Text.Trim();

        var filtered = _allGroups.AsEnumerable();
        if (!string.IsNullOrEmpty(term))
        {
            filtered = filtered.Where(g =>
                g.GroupName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (g.ParentGroupName != null && g.ParentGroupName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                g.NatureDisplay.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        // Preserve newest-first order
        var list = filtered.OrderByDescending(g => g.GroupId).ToList();

        dgvGroups.Rows.Clear();
        int rowIndexToSelect = -1;

        for (int i = 0; i < list.Count; i++)
        {
            var g = list[i];
            string under = string.IsNullOrEmpty(g.ParentGroupName) ? "Primary" : g.ParentGroupName;
            string status = "Active";

            int r = dgvGroups.Rows.Add(g.GroupId, g.GroupName, under, g.NatureDisplay, status);

            if (selectGroupId.HasValue && g.GroupId == selectGroupId.Value)
            {
                rowIndexToSelect = r;
            }
        }

        lblRecordCount.Text = $"{list.Count} Groups";

        if (rowIndexToSelect >= 0 && rowIndexToSelect < dgvGroups.Rows.Count)
        {
            dgvGroups.Rows[rowIndexToSelect].Selected = true;
            dgvGroups.CurrentCell = dgvGroups.Rows[rowIndexToSelect].Cells["GroupName"];
        }
        else if (dgvGroups.Rows.Count > 0)
        {
            // Select first (newest) row by default
            dgvGroups.Rows[0].Selected = true;
            dgvGroups.CurrentCell = dgvGroups.Rows[0].Cells["GroupName"];
        }

        UpdateActionButtonsEnabledState();
        EnsureSelectedRowVisible();
        UpdateScrollIndicators();

        if (ActiveControl == null || ActiveControl == this || ActiveControl == dgvGroups)
        {
            FocusFirstRowInGrid();
        }
    }

    private async Task OnSearchChangedAsync()
    {
        await Task.Yield();
        ApplyFilterAndBind();
    }

    /// <summary>
    /// Switches workspace full-screen from Group Master List to Group Creation.
    /// The list disappears completely; Group Creation occupies 100% of DynamicContentPanel.
    /// </summary>
    public void OpenCreateGroupView(int? parentGroupId = null)
    {
        if (_companyContext.CurrentCompany == null) return;

        pnlListContainer.Visible = false;
        _isCreationViewActive = true;

        _activeCreationForm?.Dispose();
        pnlCreationContainer.Controls.Clear();

        _activeCreationForm = new GroupCreateEditForm(_groupService, _companyContext.CurrentCompany.CompanyId, null, parentGroupId)
        {
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            Dock = DockStyle.Fill
        };

        _activeCreationForm.Saved += async (newGroupId) =>
        {
            CloseCreationView();
            await LoadGroupsFromDatabaseAsync(selectGroupId: newGroupId);
        };

        _activeCreationForm.Cancelled += async () =>
        {
            CloseCreationView();
            await LoadGroupsFromDatabaseAsync();
        };

        pnlCreationContainer.Controls.Add(_activeCreationForm);
        pnlCreationContainer.Visible = true;
        _activeCreationForm.Show();
        _activeCreationForm.Focus();
    }

    /// <summary>
    /// Switches workspace full-screen from Group Master List to Group Alteration in EDIT mode.
    /// </summary>
    public void OpenAlterGroupView(int groupId)
    {
        if (_companyContext.CurrentCompany == null) return;

        pnlListContainer.Visible = false;
        _isCreationViewActive = true;

        _activeCreationForm?.Dispose();
        pnlCreationContainer.Controls.Clear();

        _activeCreationForm = new GroupCreateEditForm(_groupService, _companyContext.CurrentCompany.CompanyId, groupId)
        {
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            Dock = DockStyle.Fill
        };

        _activeCreationForm.Saved += async (savedGroupId) =>
        {
            CloseCreationView();
            await LoadGroupsFromDatabaseAsync(selectGroupId: savedGroupId);
        };

        _activeCreationForm.Cancelled += async () =>
        {
            CloseCreationView();
            await LoadGroupsFromDatabaseAsync();
        };

        pnlCreationContainer.Controls.Add(_activeCreationForm);
        pnlCreationContainer.Visible = true;
        _activeCreationForm.Show();
        _activeCreationForm.Focus();
    }

    private void CloseCreationView()
    {
        _isCreationViewActive = false;
        if (_activeCreationForm != null)
        {
            pnlCreationContainer.Controls.Remove(_activeCreationForm);
            _activeCreationForm.Dispose();
            _activeCreationForm = null;
        }

        pnlCreationContainer.Visible = false;
        pnlListContainer.Visible = true;
        pnlListContainer.BringToFront();

        if (dgvGroups.CanFocus)
        {
            dgvGroups.Focus();
        }
    }

    private void AlterSelectedGroup()
    {
        if (!ButtonAlter.Enabled || dgvGroups.SelectedRows.Count == 0) return;
        int groupId = Convert.ToInt32(dgvGroups.SelectedRows[0].Cells["GroupId"].Value);
        OpenAlterGroupView(groupId);
    }

    private async Task DeleteSelectedGroupAsync()
    {
        if (!ButtonDelete.Enabled || dgvGroups.SelectedRows.Count == 0 || _companyContext.CurrentCompany == null) return;

        int groupId = Convert.ToInt32(dgvGroups.SelectedRows[0].Cells["GroupId"].Value);
        string name = dgvGroups.SelectedRows[0].Cells["GroupName"].Value?.ToString() ?? "Group";

        var confirm = MessageBox.Show(
            $"Delete selected group '{name}'?\n\nThis will remove the group from the chart of accounts.",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm == DialogResult.Yes)
        {
            try
            {
                bool deleted = await _groupService.DeleteGroupAsync(groupId);
                if (deleted)
                {
                    await LoadGroupsFromDatabaseAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot delete group: {ex.Message}", "Delete Prevented", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
    }

    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            if (ButtonAlter.Enabled)
            {
                e.Handled = true;
                AlterSelectedGroup();
            }
        }
        else if (e.KeyCode == Keys.Delete)
        {
            if (ButtonDelete.Enabled)
            {
                e.Handled = true;
                _ = DeleteSelectedGroupAsync();
            }
        }
        else if (e.KeyCode == Keys.C && !e.Control && !e.Alt)
        {
            e.Handled = true;
            OpenCreateGroupView();
        }
        else if (e.KeyCode == Keys.A && !e.Control && !e.Alt)
        {
            if (ButtonAlter.Enabled)
            {
                e.Handled = true;
                AlterSelectedGroup();
            }
        }
        else if (e.KeyCode == Keys.Down)
        {
            e.Handled = true;
            ScrollDownOneRow();
        }
        else if (e.KeyCode == Keys.Up)
        {
            if (dgvGroups.SelectedRows.Count > 0 && dgvGroups.SelectedRows[0].Index == 0)
            {
                e.Handled = true;
                FocusSearchBox();
            }
            else
            {
                e.Handled = true;
                ScrollUpOneRow();
            }
        }
        else if (e.KeyCode == Keys.PageDown)
        {
            e.Handled = true;
            int step = Math.Max(1, dgvGroups.DisplayedRowCount(false));
            int cur = dgvGroups.SelectedRows.Count > 0 ? dgvGroups.SelectedRows[0].Index : 0;
            int target = Math.Min(dgvGroups.Rows.Count - 1, cur + step);
            if (target < dgvGroups.Rows.Count)
            {
                dgvGroups.Rows[target].Selected = true;
                dgvGroups.CurrentCell = dgvGroups.Rows[target].Cells["GroupName"];
                EnsureSelectedRowVisible();
            }
        }
        else if (e.KeyCode == Keys.PageUp)
        {
            e.Handled = true;
            int step = Math.Max(1, dgvGroups.DisplayedRowCount(false));
            int cur = dgvGroups.SelectedRows.Count > 0 ? dgvGroups.SelectedRows[0].Index : 0;
            int target = Math.Max(0, cur - step);
            if (target >= 0 && dgvGroups.Rows.Count > 0)
            {
                dgvGroups.Rows[target].Selected = true;
                dgvGroups.CurrentCell = dgvGroups.Rows[target].Cells["GroupName"];
                EnsureSelectedRowVisible();
            }
        }
        else if (e.Alt && e.KeyCode == Keys.F)
        {
            e.Handled = true;
            FocusSearchBox();
        }
    }

    /// <summary>
    /// Moves selection down by one row and auto-scrolls the grid viewport to keep the selected row in view.
    /// </summary>
    public void ScrollDownOneRow()
    {
        if (dgvGroups.Rows.Count == 0) return;
        int currentIndex = dgvGroups.SelectedRows.Count > 0 ? dgvGroups.SelectedRows[0].Index : 0;
        if (currentIndex < dgvGroups.Rows.Count - 1)
        {
            int nextIndex = currentIndex + 1;
            dgvGroups.Rows[nextIndex].Selected = true;
            dgvGroups.CurrentCell = dgvGroups.Rows[nextIndex].Cells["GroupName"];
        }
        EnsureSelectedRowVisible();
        if (dgvGroups.CanFocus) dgvGroups.Focus();
    }

    /// <summary>
    /// Moves selection up by one row and auto-scrolls the grid viewport to keep the selected row in view.
    /// </summary>
    public void ScrollUpOneRow()
    {
        if (dgvGroups.Rows.Count == 0) return;
        int currentIndex = dgvGroups.SelectedRows.Count > 0 ? dgvGroups.SelectedRows[0].Index : 0;
        if (currentIndex > 0)
        {
            int prevIndex = currentIndex - 1;
            dgvGroups.Rows[prevIndex].Selected = true;
            dgvGroups.CurrentCell = dgvGroups.Rows[prevIndex].Cells["GroupName"];
        }
        EnsureSelectedRowVisible();
        if (dgvGroups.CanFocus) dgvGroups.Focus();
    }

    /// <summary>
    /// Automatically adjusts FirstDisplayedScrollingRowIndex so the currently selected row is completely visible in the viewport.
    /// Works without needing native scrollbars.
    /// </summary>
    public void EnsureSelectedRowVisible()
    {
        if (dgvGroups.Rows.Count == 0)
        {
            UpdateScrollIndicators();
            return;
        }

        int selIndex = dgvGroups.SelectedRows.Count > 0 ? dgvGroups.SelectedRows[0].Index : 0;
        int displayedCount = dgvGroups.DisplayedRowCount(false);

        if (displayedCount > 0)
        {
            int first = dgvGroups.FirstDisplayedScrollingRowIndex;
            if (first < 0) first = 0;

            if (selIndex < first)
            {
                dgvGroups.FirstDisplayedScrollingRowIndex = selIndex;
            }
            else if (selIndex >= first + displayedCount)
            {
                int targetFirst = Math.Max(0, selIndex - displayedCount + 1);
                if (targetFirst < dgvGroups.Rows.Count)
                {
                    dgvGroups.FirstDisplayedScrollingRowIndex = targetFirst;
                }
            }
        }

        UpdateScrollIndicators();
    }

    /// <summary>
    /// Updates the visibility of the footer down (▼) and up (▲) scroll indicator buttons based on viewport position
    /// and dynamically displays the current row range in the footer.
    /// </summary>
    public void UpdateScrollIndicators()
    {
        if (btnScrollUp == null || btnScrollDown == null || lblGridFooter == null) return;

        int total = _allGroups.Count;
        int count = dgvGroups.Rows.Count;

        if (count == 0)
        {
            btnScrollUp.Visible = false;
            btnScrollDown.Visible = false;
            lblGridFooter.Text = $"Total Groups: {total}     |     Showing: 0–0 of {total}";
            return;
        }

        int first = dgvGroups.FirstDisplayedScrollingRowIndex;
        if (first < 0) first = 0;
        int displayedCount = dgvGroups.DisplayedRowCount(false);

        if (displayedCount == 0)
        {
            btnScrollUp.Visible = first > 0;
            btnScrollDown.Visible = count > 15;
            lblGridFooter.Text = $"Total Groups: {total}     |     Showing: 1–{count} of {total}";
            return;
        }

        btnScrollUp.Visible = first > 0;
        btnScrollDown.Visible = (first + displayedCount) < count;

        int start = first + 1;
        int end = Math.Min(first + displayedCount, count);
        lblGridFooter.Text = $"Total Groups: {total}     |     Showing: {start}–{end} of {total}";
    }

    public bool HandleBackNavigation()
    {
        if (_isCreationViewActive)
        {
            if (_activeCreationForm != null)
            {
                _activeCreationForm.HandleCancel();
            }
            else
            {
                CloseCreationView();
            }
            return true;
        }

        return false;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Alt | Keys.S) || keyData == (Keys.Alt | Keys.F))
        {
            FocusSearchBox();
            return true;
        }

        if (keyData == Keys.Escape)
        {
            if (HandleBackNavigation())
            {
                return true;
            }

            return MoneyFlowEscController.HandleEsc(
                ActiveControl,
                this,
                closeAction: () =>
                {
                    Close();
                });
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        // When in creation view, let creation view handle keys
        if (pnlCreationContainer.Visible) return;

        // Group Master List Shortcuts
        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            HandleBackNavigation();
            return;
        }

        if (e.Alt && e.KeyCode == Keys.C)
        {
            e.Handled = true;
            OpenCreateGroupView();
            return;
        }

        if (e.Alt && e.KeyCode == Keys.A)
        {
            if (ButtonAlter.Enabled)
            {
                e.Handled = true;
                AlterSelectedGroup();
            }
            return;
        }

        if ((e.Alt && e.KeyCode == Keys.D) || e.KeyCode == Keys.Delete)
        {
            if (ButtonDelete.Enabled)
            {
                e.Handled = true;
                _ = DeleteSelectedGroupAsync();
            }
            return;
        }

        if (e.Alt && (e.KeyCode == Keys.S || e.KeyCode == Keys.F))
        {
            e.Handled = true;
            FocusSearchBox();
            return;
        }
    }
}
