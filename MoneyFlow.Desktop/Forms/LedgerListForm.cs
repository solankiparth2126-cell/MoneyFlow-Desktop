using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
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
/// Full-screen keyboard-first Ledger Master for MoneyFlow.
/// Follows the Group Master design language:
/// 1. Solid Navy #1B365D header with title and ledger counter.
/// 2. Streamlined search bar with placeholder cue banner, Clear button, and collapsible right sidebar toggle.
/// 3. DataGridView with ALL-CAPS navy headers, alternating row styling, text-based STATUS, and no native scrollbars.
/// 4. Integrated footer with record counter and smooth ▲/▼ scroll indicators.
/// 5. Right-side collapsible actions panel for CREATE, ALTER, DELETE, SEARCH, and CLOSE.
/// </summary>
public class LedgerListForm : Form, IBackNavigable
{
    private readonly ILedgerService _ledgerService;
    private readonly IGroupService _groupService;
    private readonly ICompanyContext _companyContext;

    // View Containers
    private Panel pnlListContainer = null!;
    private Panel pnlCreationContainer = null!;

    // UI Elements
    private Label _lblTitle = null!;
    private Label _lblRecordCount = null!;
    private Label _lblGridFooter = null!;
    private TextBox _txtSearch = null!;
    private Button _btnClear = null!;
    private Guna2DataGridView _dgvLedgers = null!;
    private LedgerMasterRightActionPanel _pnlRightActions = null!;
    private Panel _pnlActionsGap = null!;
    private Panel _pnlWorkspace = null!;
    private Button _btnScrollUp = null!;
    private Button _btnScrollDown = null!;
    private Button _btnToggleActions = null!;
    private bool _isActionsPanelCollapsed;

    public LedgerMasterRightActionPanel RightActionPanel => _pnlRightActions;
    public Button ButtonCreate => _pnlRightActions.ButtonCreate;
    public Button ButtonAlter => _pnlRightActions.ButtonAlter;
    public Button ButtonDelete => _pnlRightActions.ButtonDelete;
    public Button ButtonSearch => _pnlRightActions.ButtonSearch;
    public Button ButtonFind => _pnlRightActions.ButtonSearch;
    public Button ButtonClose => _pnlRightActions.ButtonClose;
    public Button ButtonToggleActions => _btnToggleActions;
    public bool IsActionsPanelCollapsed => _isActionsPanelCollapsed;
    public void ToggleActionsPanel() => SetActionsPanelCollapsed(!_isActionsPanelCollapsed);
    public Guna2DataGridView Grid => _dgvLedgers;
    public Label TitleLabel => _lblTitle;
    public Label FooterLabel => _lblGridFooter;
    public Panel ListContainer => pnlListContainer;
    public Panel CreationContainer => pnlCreationContainer;

    private List<LedgerSummaryDto> _allLedgers = new();
    private bool _isCreationViewActive;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);
    private const int EM_SETCUEBANNER = 0x1501;

    public LedgerListForm(
        ILedgerService ledgerService,
        IGroupService groupService,
        ICompanyContext companyContext)
    {
        _ledgerService = ledgerService ?? throw new ArgumentNullException(nameof(ledgerService));
        _groupService = groupService ?? throw new ArgumentNullException(nameof(groupService));
        _companyContext = companyContext ?? throw new ArgumentNullException(nameof(companyContext));

        InitializeComponent();
        _ = LoadLedgersFromDatabaseAsync();
    }

    private void InitializeComponent()
    {
        Text = "MoneyFlow Desktop — Ledger Master";
        Size = new Size(1100, 700);
        MinimumSize = new Size(880, 520);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(240, 242, 245);
        Font = ExecLedgerTheme.UIRegular9;
        KeyPreview = true;

        // Container 1: Full-Screen Creation Host (reserved for future in-place workflows)
        pnlCreationContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Visible = false
        };
        Controls.Add(pnlCreationContainer);

        // Container 2: Ledger Master List
        pnlListContainer = new Panel
        {
            Dock = DockStyle.Fill,
            Visible = true,
            BackColor = Color.FromArgb(240, 242, 245), // #F0F2F5
            Padding = new Padding(16, 16, 16, 16)
        };

        // ── 1. Top Header Card (Solid Navy #1B365D, Height 54px) ──
        var headerCard = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            BackColor = ExecLedgerTheme.PrimaryNavy // #1B365D
        };

        var compName = _companyContext.CurrentCompany?.CompanyName ?? "";
        _lblTitle = new Label
        {
            Text = $"LEDGER MASTER — {compName}",
            Font = new Font("Segoe UI", 13.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(18, 15),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        headerCard.Controls.Add(_lblTitle);

        _lblRecordCount = new Label
        {
            Text = "0 Ledgers",
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(203, 213, 225),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        headerCard.Controls.Add(_lblRecordCount);

        void UpdateRecordCountPos()
        {
            if (headerCard.Width > 0 && _lblRecordCount != null)
            {
                _lblRecordCount.Location = new Point(headerCard.Width - _lblRecordCount.Width - 20, 18);
            }
        }
        headerCard.Resize += (s, e) => UpdateRecordCountPos();
        _lblRecordCount.TextChanged += (s, e) => UpdateRecordCountPos();
        headerCard.HandleCreated += (s, e) => UpdateRecordCountPos();

        // Header bottom gap (12px)
        var pnlHeaderGap = new Panel
        {
            Dock = DockStyle.Top,
            Height = 12,
            BackColor = Color.Transparent
        };

        // ── 2. Workspace Panel ──
        _pnlWorkspace = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };

        // ── Right Action Panel (Dock = Right, Width = 200px, Light Blue #E0EDFD) ──
        _pnlRightActions = new LedgerMasterRightActionPanel
        {
            Dock = DockStyle.Right,
            Width = 200
        };
        _pnlRightActions.CreateClicked += async (s, e) => await OnCreateAsync();
        _pnlRightActions.AlterClicked += async (s, e) => await OnAlterAsync();
        _pnlRightActions.DeleteClicked += async (s, e) => await OnDeleteAsync();
        _pnlRightActions.SearchClicked += (s, e) => FocusSearchBox();
        _pnlRightActions.CloseClicked += (s, e) =>
        {
            if (HandleBackNavigation()) return;
            MoneyFlowEscController.HandleEsc(
                ActiveControl,
                this,
                closeAction: () => Close());
        };
        _pnlRightActions.CollapseToggled += (s, e) =>
        {
            EnsureSelectedRowVisible();
            UpdateScrollIndicators();
        };
        _pnlRightActions.SetRowSelectedActionsEnabled(false);

        // Gap between Main Grid Area and Right Action Panel (12px)
        _pnlActionsGap = new Panel
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
            Name = "SearchPanel",
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.White
        };

        var lblSearch = new Label
        {
            Name = "lblSearch",
            Text = "&Search:",
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(27, 54, 93),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        lblSearch.Click += (s, e) => FocusSearchBox();
        searchPanel.Controls.Add(lblSearch);

        _txtSearch = new TextBox
        {
            Name = "txtSearch",
            Font = new Font("Segoe UI", 9.75F, FontStyle.Regular),
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            PlaceholderText = "Type to search ledger name, alias, group, nature..."
        };

        void SelectAllSearchText()
        {
            if (_txtSearch.IsHandleCreated)
            {
                BeginInvoke(() =>
                {
                    if (_txtSearch.TextLength > 0)
                    {
                        _txtSearch.SelectAll();
                    }
                });
            }
            else
            {
                _txtSearch.SelectAll();
            }
        }

        _txtSearch.Click += (s, e) => SelectAllSearchText();
        _txtSearch.MouseUp += (s, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                SelectAllSearchText();
            }
        };
        _txtSearch.Enter += (s, e) => SelectAllSearchText();
        _txtSearch.GotFocus += (s, e) => searchPanel.Invalidate();
        _txtSearch.LostFocus += (s, e) => searchPanel.Invalidate();

        _txtSearch.TextChanged += async (s, e) => await OnSearchChangedAsync();
        _txtSearch.KeyDown += (s, e) =>
        {
            if (e.Alt && (e.KeyCode == Keys.S || e.KeyCode == Keys.F))
            {
                SelectAllSearchText();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Down && _dgvLedgers.Rows.Count > 0)
            {
                _dgvLedgers.Focus();
                e.Handled = true;
            }
        };
        searchPanel.Controls.Add(_txtSearch);

        _btnClear = new Button
        {
            Name = "btnClear",
            Text = "Clear",
            Size = new Size(62, 28),
            BackColor = Color.FromArgb(241, 245, 249),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Cursor = Cursors.Hand
        };
        _btnClear.FlatAppearance.BorderSize = 1;
        _btnClear.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnClear.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 232, 240);
        _btnClear.Click += (s, e) =>
        {
            _txtSearch.Clear();
            FocusSearchBox();
        };
        searchPanel.Controls.Add(_btnClear);

        // Sidebar toggle button on far right of SearchPanel
        _btnToggleActions = _pnlRightActions.ToggleButton;
        _btnToggleActions.Size = new Size(28, 28);
        _btnToggleActions.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _btnToggleActions.Click += (s, e) => ToggleActionsPanel();
        searchPanel.Controls.Add(_btnToggleActions);

        const int searchBoxPadX = 8;
        const int searchBoxPadY = 5;

        searchPanel.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Outer 1px subtle panel boundary
            using var panelPen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawRectangle(panelPen, 0, 0, searchPanel.Width - 1, searchPanel.Height - 1);

            // Modern input box border around _txtSearch
            var boxRect = new Rectangle(
                _txtSearch.Left - searchBoxPadX,
                _txtSearch.Top - searchBoxPadY,
                _txtSearch.Width + (searchBoxPadX * 2),
                _txtSearch.Height + (searchBoxPadY * 2));

            using var fillBrush = new SolidBrush(Color.White);
            e.Graphics.FillRectangle(fillBrush, boxRect);

            bool isFocused = _txtSearch.Focused;
            Color boxBorderColor = isFocused ? Color.FromArgb(27, 54, 93) : Color.FromArgb(203, 213, 225);
            using var boxPen = new Pen(boxBorderColor, isFocused ? 1.5f : 1f);
            e.Graphics.DrawRectangle(boxPen, boxRect);
        };

        searchPanel.MouseDown += (s, e) =>
        {
            var boxRect = new Rectangle(
                _txtSearch.Left - searchBoxPadX,
                _txtSearch.Top - searchBoxPadY,
                _txtSearch.Width + (searchBoxPadX * 2),
                _txtSearch.Height + (searchBoxPadY * 2));

            if (boxRect.Contains(e.Location))
            {
                _txtSearch.Focus();
                SelectAllSearchText();
            }
        };

        void LayoutSearchPanel()
        {
            if (searchPanel.Width <= 0 || searchPanel.Height <= 0) return;

            lblSearch.Location = new Point(14, (searchPanel.Height - lblSearch.PreferredHeight) / 2);

            int txtLeft = lblSearch.Right + 12 + searchBoxPadX;
            int txtTop = (searchPanel.Height - _txtSearch.PreferredHeight) / 2;

            int rightBoundary = searchPanel.Width - 16;
            if (_btnToggleActions != null && _btnToggleActions.Visible)
            {
                rightBoundary = _btnToggleActions.Left - 12;
            }

            int availableWidth = rightBoundary - txtLeft - searchBoxPadX - 8 - _btnClear.Width - 16;
            int searchWidth = Math.Clamp(availableWidth, 180, 480);

            _txtSearch.Location = new Point(txtLeft, txtTop);
            _txtSearch.Width = searchWidth;

            int boxRight = _txtSearch.Right + searchBoxPadX;
            _btnClear.Location = new Point(boxRight + 8, (searchPanel.Height - _btnClear.Height) / 2);

            if (_btnToggleActions != null)
            {
                _btnToggleActions.Location = new Point(
                    searchPanel.Width - _btnToggleActions.Width - 8,
                    (searchPanel.Height - _btnToggleActions.Height) / 2);
            }

            searchPanel.Invalidate();
        }

        searchPanel.Resize += (s, e) => LayoutSearchPanel();
        searchPanel.HandleCreated += (s, e) => LayoutSearchPanel();

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

        // ── DataGridView (Ledger Master Table matching Group Master Styling) ──
        _dgvLedgers = new Guna2DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            ShowCellToolTips = false,
            ScrollBars = ScrollBars.None,
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

        // Column definitions with ALL CAPS headers
        _dgvLedgers.Columns.Clear();

        var colLedgerId = new DataGridViewTextBoxColumn
        {
            Name = "LedgerId",
            HeaderText = "ID",
            Visible = false
        };
        _dgvLedgers.Columns.Add(colLedgerId);

        var colLedgerName = new DataGridViewTextBoxColumn
        {
            Name = "LedgerName",
            HeaderText = "LEDGER NAME",
            FillWeight = 34,
            MinimumWidth = 140
        };
        _dgvLedgers.Columns.Add(colLedgerName);

        var colUnder = new DataGridViewTextBoxColumn
        {
            Name = "UnderGroup",
            HeaderText = "UNDER",
            FillWeight = 26,
            MinimumWidth = 150
        };
        _dgvLedgers.Columns.Add(colUnder);

        var colNature = new DataGridViewTextBoxColumn
        {
            Name = "Nature",
            HeaderText = "NATURE",
            FillWeight = 16,
            MinimumWidth = 100
        };
        _dgvLedgers.Columns.Add(colNature);

        var colOpeningBalance = new DataGridViewTextBoxColumn
        {
            Name = "OpeningBalance",
            HeaderText = "OPENING BALANCE",
            FillWeight = 14,
            MinimumWidth = 110
        };
        _dgvLedgers.Columns.Add(colOpeningBalance);

        var colStatus = new DataGridViewTextBoxColumn
        {
            Name = "Status",
            HeaderText = "STATUS",
            FillWeight = 10,
            MinimumWidth = 70
        };
        _dgvLedgers.Columns.Add(colStatus);

        // Accounting Table Styling matching exact MoneyFlow Group Master specifications
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
        _dgvLedgers.ColumnHeadersDefaultCellStyle = headerStyle;
        _dgvLedgers.ThemeStyle.HeaderStyle.BackColor = Color.FromArgb(27, 54, 93);
        _dgvLedgers.ThemeStyle.HeaderStyle.ForeColor = Color.White;
        _dgvLedgers.ThemeStyle.HeaderStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        _dgvLedgers.ThemeStyle.HeaderStyle.Height = 36;

        colOpeningBalance.HeaderCell.Style = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(27, 54, 93),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleRight,
            SelectionBackColor = Color.FromArgb(27, 54, 93),
            SelectionForeColor = Color.White,
            Padding = new Padding(8, 0, 12, 0)
        };

        var rowStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            SelectionBackColor = Color.FromArgb(224, 237, 253), // #E0EDFD
            SelectionForeColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(8, 0, 8, 0)
        };
        _dgvLedgers.DefaultCellStyle = rowStyle;
        _dgvLedgers.ThemeStyle.RowsStyle.BackColor = Color.White;
        _dgvLedgers.ThemeStyle.RowsStyle.ForeColor = Color.FromArgb(15, 23, 42);
        _dgvLedgers.ThemeStyle.RowsStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
        _dgvLedgers.ThemeStyle.RowsStyle.SelectionBackColor = Color.FromArgb(224, 237, 253);
        _dgvLedgers.ThemeStyle.RowsStyle.SelectionForeColor = Color.FromArgb(15, 23, 42);

        _dgvLedgers.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            SelectionBackColor = Color.FromArgb(224, 237, 253),
            SelectionForeColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(8, 0, 8, 0)
        };

        _dgvLedgers.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var col = _dgvLedgers.Columns[e.ColumnIndex];
            if (col.Name == "OpeningBalance" && e.CellStyle != null)
            {
                e.CellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                e.CellStyle.Padding = new Padding(8, 0, 12, 0);
            }
        };

        _dgvLedgers.DoubleClick += async (s, e) => { if (ButtonAlter.Enabled) await OnAlterAsync(); };
        _dgvLedgers.KeyDown += OnGridKeyDown;
        _dgvLedgers.SelectionChanged += (s, e) =>
        {
            UpdateActionButtonsEnabledState();
            EnsureSelectedRowVisible();
        };
        _dgvLedgers.Resize += (s, e) => EnsureSelectedRowVisible();
        _dgvLedgers.MouseWheel += (s, e) =>
        {
            if (e.Delta < 0)
                ScrollDownOneRow();
            else if (e.Delta > 0)
                ScrollUpOneRow();
        };
        _dgvLedgers.Scroll += (s, e) => UpdateScrollIndicators();

        // ── Grid Footer Bar with Down/Up Scroll Arrows ──
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

        _btnScrollUp = new Button
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
        _btnScrollUp.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnScrollUp.Click += (s, e) => ScrollUpOneRow();

        _btnScrollDown = new Button
        {
            Text = "▼",
            Size = new Size(28, 24),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(240, 242, 245),
            ForeColor = Color.FromArgb(27, 54, 93),
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnScrollDown.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        _btnScrollDown.Click += (s, e) => ScrollDownOneRow();

        pnlFooterRight.Controls.Add(_btnScrollUp);
        pnlFooterRight.Controls.Add(_btnScrollDown);

        _lblGridFooter = new Label
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            ForeColor = Color.FromArgb(46, 91, 136), // #2E5B88
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0)
        };

        pnlGridFooter.Controls.Add(pnlFooterRight);
        pnlGridFooter.Controls.Add(_lblGridFooter);
        pnlFooterRight.SendToBack();
        _lblGridFooter.BringToFront();

        // Assembly inside GridCard: Footer docks Bottom first, Grid docks Fill above it
        gridCard.Controls.Add(pnlGridFooter);
        gridCard.Controls.Add(_dgvLedgers);
        pnlGridFooter.SendToBack();
        _dgvLedgers.BringToFront();

        // Assembly inside MainArea: SearchPanel docks Top first, Gap docks Top second, GridCard docks Fill
        pnlMainArea.Controls.Add(searchPanel);
        pnlMainArea.Controls.Add(pnlSearchGridGap);
        pnlMainArea.Controls.Add(gridCard);
        searchPanel.SendToBack();
        pnlSearchGridGap.SendToBack();
        gridCard.BringToFront();

        // Assembly inside Workspace: Actions docks Right first, Gap docks Right second, MainArea docks Fill
        _pnlWorkspace.Controls.Add(_pnlRightActions);
        _pnlWorkspace.Controls.Add(_pnlActionsGap);
        _pnlWorkspace.Controls.Add(pnlMainArea);
        _pnlRightActions.SendToBack();
        _pnlActionsGap.SendToBack();
        pnlMainArea.BringToFront();

        // Assembly inside ListContainer: Header docks Top first, Gap docks Top second, Workspace docks Fill
        pnlListContainer.Controls.Add(headerCard);
        pnlListContainer.Controls.Add(pnlHeaderGap);
        pnlListContainer.Controls.Add(_pnlWorkspace);
        headerCard.SendToBack();
        pnlHeaderGap.SendToBack();
        _pnlWorkspace.BringToFront();

        Controls.Add(pnlListContainer);

        // Window Shortcuts
        KeyDown += OnFormKeyDown;

        // Default to collapsed state matching Reference Image 1
        SetActionsPanelCollapsed(true);

        Shown += (s, e) =>
        {
            FocusFirstRowInGrid();
            UpdateActionButtonsEnabledState();
        };
    }

    /// <summary>
    /// Collapses or expands the right-side Actions panel, hiding it completely when collapsed
    /// and allowing the Ledger Master table and search panel to occupy 100% of the available workspace.
    /// </summary>
    public void SetActionsPanelCollapsed(bool collapsed)
    {
        _isActionsPanelCollapsed = collapsed;

        _pnlWorkspace.SuspendLayout();
        _pnlRightActions.Visible = !collapsed;
        _pnlActionsGap.Visible = !collapsed;
        _pnlWorkspace.ResumeLayout(true);

        _pnlRightActions.SetCollapsed(collapsed);

        EnsureSelectedRowVisible();
        UpdateScrollIndicators();
    }

    /// <summary>
    /// Places keyboard focus on the first row of the grid with the first row selected.
    /// Default focus behavior when Ledger Master is opened.
    /// </summary>
    public void FocusFirstRowInGrid()
    {
        if (_dgvLedgers.Rows.Count > 0)
        {
            _dgvLedgers.ClearSelection();
            _dgvLedgers.Rows[0].Selected = true;
            if (_dgvLedgers.Columns.Contains("LedgerName"))
            {
                _dgvLedgers.CurrentCell = _dgvLedgers.Rows[0].Cells["LedgerName"];
            }
        }
        if (_dgvLedgers.CanFocus)
        {
            _dgvLedgers.Focus();
        }
    }

    /// <summary>
    /// Enables or disables ALTER and DELETE action buttons depending on whether a row is currently selected in the grid.
    /// </summary>
    public void UpdateActionButtonsEnabledState()
    {
        bool hasSelection = _dgvLedgers.SelectedRows.Count > 0 && _dgvLedgers.SelectedRows[0].Index >= 0;
        _pnlRightActions.SetRowSelectedActionsEnabled(hasSelection);
    }

    /// <summary>
    /// Moves keyboard focus to the search textbox and selects existing search text for immediate typing.
    /// </summary>
    public void FocusSearchBox()
    {
        if (_txtSearch.CanFocus)
        {
            _txtSearch.Focus();
            if (_txtSearch.IsHandleCreated)
            {
                BeginInvoke(() =>
                {
                    if (_txtSearch.TextLength > 0)
                        _txtSearch.SelectAll();
                });
            }
            else
            {
                _txtSearch.SelectAll();
            }
        }
    }

    /// <summary>
    /// Loads all ledgers for the current company from the database.
    /// </summary>
    public async Task LoadLedgersFromDatabaseAsync(int? selectLedgerId = null)
    {
        if (_companyContext.CurrentCompany == null) return;
        int companyId = _companyContext.CurrentCompany.CompanyId;
        _lblTitle.Text = $"LEDGER MASTER — {_companyContext.CurrentCompany.CompanyName}";

        try
        {
            var ledgers = await _ledgerService.GetLedgersByCompanyAsync(companyId, null, null);
            _allLedgers = ledgers.OrderBy(l => l.LedgerName).ToList();

            ApplyFilterAndBind(selectLedgerId);
        }
        catch (Exception ex)
        {
            MoneyFlowMessageBox.Show($"Failed to load ledger master data: {ex.Message}", "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public Task LoadLedgersAsync() => LoadLedgersFromDatabaseAsync();

    private void ApplyFilterAndBind(int? selectLedgerId = null)
    {
        string term = _txtSearch.Text.Trim();

        var filtered = _allLedgers.AsEnumerable();
        if (!string.IsNullOrEmpty(term))
        {
            filtered = filtered.Where(l =>
                l.LedgerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(l.DisplayGroup) && l.DisplayGroup.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(l.GroupName) && l.GroupName.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                l.GroupNature.ToString().Contains(term, StringComparison.OrdinalIgnoreCase) ||
                l.FormattedOpeningBalance.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var list = filtered.OrderBy(l => l.LedgerName).ToList();

        _dgvLedgers.Rows.Clear();
        int rowIndexToSelect = -1;

        for (int i = 0; i < list.Count; i++)
        {
            var l = list[i];
            string under = string.IsNullOrEmpty(l.DisplayGroup) ? (string.IsNullOrEmpty(l.GroupName) ? "Primary" : l.GroupName) : l.DisplayGroup;
            string nature = l.GroupNature.ToString();
            string openingBalance = l.FormattedOpeningBalance;
            string status = l.IsActive ? "Active" : "Inactive";

            int r = _dgvLedgers.Rows.Add(l.LedgerId, l.LedgerName, under, nature, openingBalance, status);

            if (selectLedgerId.HasValue && l.LedgerId == selectLedgerId.Value)
            {
                rowIndexToSelect = r;
            }
        }

        _lblRecordCount.Text = $"{list.Count} Ledgers";

        if (rowIndexToSelect >= 0 && rowIndexToSelect < _dgvLedgers.Rows.Count)
        {
            _dgvLedgers.Rows[rowIndexToSelect].Selected = true;
            _dgvLedgers.CurrentCell = _dgvLedgers.Rows[rowIndexToSelect].Cells["LedgerName"];
        }
        else if (_dgvLedgers.Rows.Count > 0)
        {
            _dgvLedgers.Rows[0].Selected = true;
            _dgvLedgers.CurrentCell = _dgvLedgers.Rows[0].Cells["LedgerName"];
        }

        UpdateActionButtonsEnabledState();
        EnsureSelectedRowVisible();
        UpdateScrollIndicators();

        if (ActiveControl == null || ActiveControl == this || ActiveControl == _dgvLedgers)
        {
            FocusFirstRowInGrid();
        }
    }

    private async Task OnSearchChangedAsync()
    {
        await Task.Yield();
        ApplyFilterAndBind();
    }

    public async Task OnCreateAsync(int? initialGroupId = null)
    {
        if (_companyContext.CurrentCompany == null) return;

        using var dialog = new LedgerCreateEditForm(_ledgerService, _groupService, _companyContext.CurrentCompany.CompanyId, null, initialGroupId);
        dialog.LedgerSaved += () => _ = LoadLedgersFromDatabaseAsync(selectLedgerId: dialog.LastSavedLedgerId);
        dialog.ShowDialog(this);
        await LoadLedgersFromDatabaseAsync(selectLedgerId: dialog.LastSavedLedgerId);
    }

    public async Task OnAlterAsync()
    {
        if (_companyContext.CurrentCompany == null || _dgvLedgers.SelectedRows.Count == 0) return;
        int ledgerId = Convert.ToInt32(_dgvLedgers.SelectedRows[0].Cells["LedgerId"].Value);

        using var dialog = new LedgerCreateEditForm(_ledgerService, _groupService, _companyContext.CurrentCompany.CompanyId, ledgerId);
        dialog.LedgerSaved += () => _ = LoadLedgersFromDatabaseAsync(selectLedgerId: dialog.LastSavedLedgerId ?? ledgerId);
        dialog.ShowDialog(this);
        await LoadLedgersFromDatabaseAsync(selectLedgerId: dialog.LastSavedLedgerId ?? ledgerId);
    }

    public async Task OnDeleteAsync()
    {
        if (_companyContext.CurrentCompany == null || _dgvLedgers.SelectedRows.Count == 0) return;

        int ledgerId = Convert.ToInt32(_dgvLedgers.SelectedRows[0].Cells["LedgerId"].Value);
        string name = _dgvLedgers.SelectedRows[0].Cells["LedgerName"].Value?.ToString() ?? "Ledger";

        var confirm = MoneyFlowMessageBox.Show(
            $"Delete selected ledger '{name}'?\n\nThis will remove the ledger from the chart of accounts.",
            "Confirm Delete Ledger",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        try
        {
            bool success = await _ledgerService.DeleteLedgerAsync(ledgerId);
            if (success)
            {
                await LoadLedgersFromDatabaseAsync();
            }
        }
        catch (Exception ex)
        {
            MoneyFlowMessageBox.Show($"Cannot delete ledger: {ex.Message}", "Delete Prevented", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        }
    }

    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            if (ButtonAlter.Enabled)
            {
                e.Handled = true;
                _ = OnAlterAsync();
            }
        }
        else if (e.KeyCode == Keys.Delete)
        {
            if (ButtonDelete.Enabled)
            {
                e.Handled = true;
                _ = OnDeleteAsync();
            }
        }
        else if (e.KeyCode == Keys.C && !e.Control && !e.Alt)
        {
            e.Handled = true;
            _ = OnCreateAsync();
        }
        else if (e.KeyCode == Keys.A && !e.Control && !e.Alt)
        {
            if (ButtonAlter.Enabled)
            {
                e.Handled = true;
                _ = OnAlterAsync();
            }
        }
        else if (e.KeyCode == Keys.Down)
        {
            e.Handled = true;
            ScrollDownOneRow();
        }
        else if (e.KeyCode == Keys.Up)
        {
            if (_dgvLedgers.SelectedRows.Count > 0 && _dgvLedgers.SelectedRows[0].Index == 0)
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
            int step = Math.Max(1, _dgvLedgers.DisplayedRowCount(false));
            int cur = _dgvLedgers.SelectedRows.Count > 0 ? _dgvLedgers.SelectedRows[0].Index : 0;
            int target = Math.Min(_dgvLedgers.Rows.Count - 1, cur + step);
            if (target < _dgvLedgers.Rows.Count)
            {
                _dgvLedgers.Rows[target].Selected = true;
                _dgvLedgers.CurrentCell = _dgvLedgers.Rows[target].Cells["LedgerName"];
                EnsureSelectedRowVisible();
            }
        }
        else if (e.KeyCode == Keys.PageUp)
        {
            e.Handled = true;
            int step = Math.Max(1, _dgvLedgers.DisplayedRowCount(false));
            int cur = _dgvLedgers.SelectedRows.Count > 0 ? _dgvLedgers.SelectedRows[0].Index : 0;
            int target = Math.Max(0, cur - step);
            if (target >= 0 && _dgvLedgers.Rows.Count > 0)
            {
                _dgvLedgers.Rows[target].Selected = true;
                _dgvLedgers.CurrentCell = _dgvLedgers.Rows[target].Cells["LedgerName"];
                EnsureSelectedRowVisible();
            }
        }
        else if (e.Alt && (e.KeyCode == Keys.S || e.KeyCode == Keys.F))
        {
            e.Handled = true;
            FocusSearchBox();
        }
    }

    public void ScrollDownOneRow()
    {
        if (_dgvLedgers.Rows.Count == 0) return;
        int currentIndex = _dgvLedgers.SelectedRows.Count > 0 ? _dgvLedgers.SelectedRows[0].Index : 0;
        if (currentIndex < _dgvLedgers.Rows.Count - 1)
        {
            int nextIndex = currentIndex + 1;
            _dgvLedgers.Rows[nextIndex].Selected = true;
            _dgvLedgers.CurrentCell = _dgvLedgers.Rows[nextIndex].Cells["LedgerName"];
        }
        EnsureSelectedRowVisible();
        if (_dgvLedgers.CanFocus) _dgvLedgers.Focus();
    }

    public void ScrollUpOneRow()
    {
        if (_dgvLedgers.Rows.Count == 0) return;
        int currentIndex = _dgvLedgers.SelectedRows.Count > 0 ? _dgvLedgers.SelectedRows[0].Index : 0;
        if (currentIndex > 0)
        {
            int prevIndex = currentIndex - 1;
            _dgvLedgers.Rows[prevIndex].Selected = true;
            _dgvLedgers.CurrentCell = _dgvLedgers.Rows[prevIndex].Cells["LedgerName"];
        }
        EnsureSelectedRowVisible();
        if (_dgvLedgers.CanFocus) _dgvLedgers.Focus();
    }

    public void EnsureSelectedRowVisible()
    {
        if (_dgvLedgers.Rows.Count == 0)
        {
            UpdateScrollIndicators();
            return;
        }

        int selIndex = _dgvLedgers.SelectedRows.Count > 0 ? _dgvLedgers.SelectedRows[0].Index : 0;
        int displayedCount = _dgvLedgers.DisplayedRowCount(true);
        if (displayedCount <= 0) displayedCount = _dgvLedgers.DisplayedRowCount(false);

        if (displayedCount > 0)
        {
            int first = _dgvLedgers.FirstDisplayedScrollingRowIndex;
            if (first < 0) first = 0;

            if (selIndex < first)
            {
                _dgvLedgers.FirstDisplayedScrollingRowIndex = selIndex;
            }
            else if (selIndex >= first + displayedCount)
            {
                int targetFirst = Math.Max(0, selIndex - displayedCount + 1);
                if (targetFirst < _dgvLedgers.Rows.Count)
                {
                    _dgvLedgers.FirstDisplayedScrollingRowIndex = targetFirst;
                }
            }
        }

        UpdateScrollIndicators();
    }

    public void UpdateScrollIndicators()
    {
        if (_btnScrollUp == null || _btnScrollDown == null || _lblGridFooter == null) return;

        int total = _allLedgers.Count;
        int count = _dgvLedgers.Rows.Count;

        if (count == 0)
        {
            _btnScrollUp.Visible = false;
            _btnScrollDown.Visible = false;
            _lblGridFooter.Text = $"Total Ledgers: {total}     |     Showing: 0–0 of {total}";
            return;
        }

        int first = _dgvLedgers.FirstDisplayedScrollingRowIndex;
        if (first < 0) first = 0;
        int displayedCount = _dgvLedgers.DisplayedRowCount(false);

        if (displayedCount == 0)
        {
            _btnScrollUp.Visible = first > 0;
            _btnScrollDown.Visible = count > 15;
            _lblGridFooter.Text = $"Total Ledgers: {total}     |     Showing: 1–{count} of {total}";
            return;
        }

        _btnScrollUp.Visible = first > 0;
        _btnScrollDown.Visible = (first + displayedCount) < count;

        int start = first + 1;
        int end = Math.Min(first + displayedCount, count);
        _lblGridFooter.Text = $"Total Ledgers: {total}     |     Showing: {start}–{end} of {total}";
    }

    public bool HandleBackNavigation()
    {
        if (_isCreationViewActive)
        {
            _isCreationViewActive = false;
            pnlCreationContainer.Visible = false;
            pnlListContainer.Visible = true;
            pnlListContainer.BringToFront();
            if (_dgvLedgers.CanFocus) _dgvLedgers.Focus();
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
                closeAction: () => Close());
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (pnlCreationContainer.Visible) return;

        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            HandleBackNavigation();
            return;
        }

        if (e.Alt && e.KeyCode == Keys.C)
        {
            e.Handled = true;
            _ = OnCreateAsync();
            return;
        }

        if (e.Alt && e.KeyCode == Keys.A)
        {
            if (ButtonAlter.Enabled)
            {
                e.Handled = true;
                _ = OnAlterAsync();
            }
            return;
        }

        if ((e.Alt && e.KeyCode == Keys.D) || e.KeyCode == Keys.Delete)
        {
            if (ButtonDelete.Enabled)
            {
                e.Handled = true;
                _ = OnDeleteAsync();
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
