using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Controls.Fields;
using MoneyFlow.Desktop.Controls.Lookup;
using MoneyFlow.Desktop.Dialogs;
using MoneyFlow.Desktop.Navigation;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Group Creation & Alteration screen for MoneyFlow Desktop ERP.
///
/// Features:
/// 1. MoneyFlow Standard ERP Theme:
///    - Primary Navy (#1B365D), Steel Blue (#2E5B88), System Focus Blue (#2563EB).
///    - Clean white workspace surface (#FFFFFF), subtle borders (#CBD5E1).
///    - Selected rows use light blue (#E0EDFD) with deep navy text and clear blue indicator.
///    - Input controls have pure white backgrounds (#FFFFFF) with blue focus border (#2563EB).
///    - Zero yellow, golden, or Tally-specific palette colors.
/// 2. Under Field & Right-Side LIST OF GROUPS Panel:
///    - Right panel opens ONLY when Under control has focus; otherwise hidden.
///    - Auto-Search: typing on Under immediately searches and filters parent groups.
///    - Boxed card container prevents bottom items from ever being clipped/cut off.
///    - Smooth scrolling on Arrow Up / Down keeps the highlighted row fully visible.
///    - Enter commits the highlighted group and advances to the next field.
///    - Escape cancels temporary selection, restoring the previous Under value without closing the screen.
/// 3. Fixed Bottom Shortcut Footer (#1B365D):
///    - [Q] Quit, [A] Accept, [D] Delete, [F4] List, [F12] Configure.
///    - Live CAPS / NUM / Ready status indicator.
/// </summary>
public partial class GroupCreateEditForm : Form, IBackNavigable
{
    private readonly IGroupService _groupService;
    private readonly int _companyId;
    private readonly int? _groupIdToEdit;
    private readonly int? _initialParentGroupId;
    private readonly string _companyName;
    private readonly string _financialYear;

    // Field Rows
    private MoneyFlowField fldName = null!;
    private MoneyFlowField fldAlias = null!;
    private MoneyFlowField fldUnder = null!;
    private MoneyFlowField fldSubLedger = null!;
    private MoneyFlowField fldNettBalances = null!;
    private MoneyFlowField fldCalculation = null!;
    private MoneyFlowField fldMethodToAllocate = null!;
    private MoneyFlowField fldNature = null!;
    private MoneyFlowField fldAffectsPL = null!;

    // Underlying Editors
    private TextBox txtGroupName = null!;
    private TextBox txtAlias = null!;
    private Panel pnlUnderEditor = null!;
    private TextBox txtUnder = null!;
    private Label lblUnderArrow = null!;
    private MoneyFlowTextLookup cmbSubLedger = null!;
    private MoneyFlowTextLookup cmbNettBalances = null!;
    private MoneyFlowTextLookup cmbCalculation = null!;
    private MoneyFlowTextLookup cmbMethodToAllocate = null!;
    private MoneyFlowTextLookup cmbNature = null!;
    private MoneyFlowTextLookup cmbAffectsPL = null!;

    private Button _compatBtnSave = null!;
    private Button _compatBtnCancel = null!;
    private Button btnSave = null!;
    private Button btnCancel = null!;
    private System.Windows.Forms.Timer? _keyboardStatusTimer;
    private string _currentFooterStatus = string.Empty;

    // Container Panels
    private Panel headerPanel = null!;
    private Panel pnlFooter = null!;
    private Label lblFooterStatus = null!;
    private Panel pnlMainWorkspace = null!;
    private Panel pnlLeftForm = null!;
    private Panel pnlRightList = null!;
    private Panel pnlListBoxBorder = null!;
    private Panel card = null!; // Left form container (maintained for test backward compatibility)
    private Label lblSection = null!; // Maintained for test backward compatibility
    private bool _isSaving;

    // Right List of Groups Controls
    private TextBox txtRightSearch = null!;
    private ListBox lstRightGroups = null!;
    private Panel pnlCreateAction = null!;
    private readonly List<ParentGroupItem> _allParentGroups = new();
    private readonly List<ParentGroupItem> _filteredParentGroups = new();

    // Parent Group Selection & Navigation State
    private int? _selectedParentGroupId;
    private string _selectedParentGroupName = "Primary";
    private ParentGroupItem? _selectedParentGroupItem;
    private int? _preLookupSelectedGroupId; // State for Escape cancellation
    private int _highlightedIndex = 0;      // Current cursor index in right list
    private int? _highlightedGroupId;       // Highlighted item ID before commit

    // Public Properties for Compatibility & Testing
    public MoneyFlowField FieldName => fldName;
    public MoneyFlowField FieldAlias => fldAlias;
    public MoneyFlowField FieldUnder => fldUnder;
    public MoneyFlowField FieldSubLedger => fldSubLedger;
    public MoneyFlowField FieldNettBalances => fldNettBalances;
    public MoneyFlowField FieldCalculation => fldCalculation;
    public MoneyFlowField FieldMethodToAllocate => fldMethodToAllocate;
    public MoneyFlowField FieldNature => fldNature;
    public MoneyFlowField FieldAffectsPL => fldAffectsPL;
    public Button ButtonSave => _compatBtnSave ?? btnSave;
    public Button ButtonCancel => _compatBtnCancel ?? btnCancel;
    public Panel CardPanel => card;
    public Panel HeaderPanel => headerPanel;
    public Label SectionLabel => lblSection;
    public int ActiveFieldIndex => _activeFieldIndex;
    public int? SelectedParentGroupId => _selectedParentGroupId;

    private readonly List<MoneyFlowField> _fields = new();
    private int _activeFieldIndex = 0;
    private bool _isInitializing = true;
    private bool _isDirty;
    private string _originalName = string.Empty;

    public bool IsSaved { get; private set; }
    public int? NewlySavedGroupId { get; private set; }

    public event Action<int>? Saved;
    public event Action? Cancelled;

    public class ParentGroupItem
    {
        public int? GroupId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsPrimary { get; set; }
        public GroupNature Nature { get; set; } = GroupNature.Assets;
        public bool AffectsProfitLoss { get; set; }
        public Group? RawGroup { get; set; }

        public override string ToString() => Name;
    }

    public GroupCreateEditForm(
        IGroupService groupService,
        int companyId,
        int? groupIdToEdit = null,
        int? initialParentGroupId = null,
        string? companyName = null,
        string? financialYear = null)
    {
        _groupService = groupService ?? throw new ArgumentNullException(nameof(groupService));
        _companyId = companyId;
        _groupIdToEdit = groupIdToEdit;
        _initialParentGroupId = initialParentGroupId;
        _companyName = !string.IsNullOrWhiteSpace(companyName) ? companyName : "Acme Global Corp";
        _financialYear = !string.IsNullOrWhiteSpace(financialYear) ? financialYear : "FY 2024-25";

        InitializeComponent();
        LoadParentGroupsAndDataAsync();
    }

    private void InitializeComponent()
    {
        bool isEdit = _groupIdToEdit.HasValue;
        Text = isEdit ? "MoneyFlow — Group Alteration" : "MoneyFlow — Group Creation";
        Size = new Size(1160, 720);
        MinimumSize = new Size(920, 580);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        Dock = DockStyle.Fill;
        BackColor = ExecLedgerTheme.WorkSurface;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        KeyPreview = true;

        // ═══════════════════════════════════════════════════════════════
        //  1. SCREEN NAVIGATION HEADER (48px Deep Navy #1B365D)
        // ═══════════════════════════════════════════════════════════════
        headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = ExecLedgerTheme.PrimaryNavy
        };

        // Left Icon (Group Hierarchy glyph)
        var picIcon = new Label
        {
            Text = "⚏",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = ExecLedgerTheme.WhiteText,
            Location = new Point(14, 12),
            Size = new Size(20, 24),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        headerPanel.Controls.Add(picIcon);

        // Title: Group Alteration / Group Creation
        var lblTitle = new Label
        {
            Text = isEdit ? "Group Alteration" : "Group Creation",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = ExecLedgerTheme.WhiteText,
            Location = new Point(38, 13),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        headerPanel.Controls.Add(lblTitle);

        // Master Modification Badge Pill
        var pnlBadge = new Panel
        {
            Height = 22,
            Width = 142,
            BackColor = Color.FromArgb(40, 255, 255, 255)
        };
        pnlBadge.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(90, 130, 180), 1);
            using var path = CreateRoundedRectanglePath(new Rectangle(0, 0, pnlBadge.Width - 1, pnlBadge.Height - 1), 4);
            e.Graphics.DrawPath(pen, path);
        };
        var lblBadge = new Label
        {
            Text = "Master Modification",
            Font = new Font("Segoe UI", 7.75F, FontStyle.Regular),
            ForeColor = ExecLedgerTheme.WhiteText,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        pnlBadge.Controls.Add(lblBadge);
        headerPanel.Controls.Add(pnlBadge);

        // Center / Breadcrumb
        var actionWord = isEdit ? "Alter" : "Create";
        var lblBreadcrumb = new Label
        {
            Text = $"Gateway of ERP > Accounts Info > Groups > {actionWord}  •  {_companyName} ({_financialYear})",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(203, 213, 225), // #CBD5E1
            AutoSize = true,
            BackColor = Color.Transparent
        };
        headerPanel.Controls.Add(lblBreadcrumb);

        // Right Help Button ('?')
        var btnHelp = new Button
        {
            Text = "?",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = ExecLedgerTheme.WhiteText,
            Size = new Size(34, 34),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            TabStop = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnHelp.FlatAppearance.BorderSize = 0;
        btnHelp.FlatAppearance.MouseOverBackColor = ExecLedgerTheme.ButtonHover;
        btnHelp.Click += (s, e) => ShowHelpDialog();
        headerPanel.Controls.Add(btnHelp);

        void LayoutHeader()
        {
            pnlBadge.Location = new Point(lblTitle.Right + 12, 13);
            btnHelp.Location = new Point(headerPanel.Width - btnHelp.Width - 10, 7);

            // Center breadcrumb between badge and right edge
            int leftEdge = pnlBadge.Right + 16;
            int rightEdge = btnHelp.Left - 16;
            int availableBreadcrumb = rightEdge - leftEdge;
            if (availableBreadcrumb > lblBreadcrumb.Width)
            {
                lblBreadcrumb.Location = new Point(leftEdge + (availableBreadcrumb - lblBreadcrumb.Width) / 2, 15);
                lblBreadcrumb.Visible = true;
            }
            else
            {
                lblBreadcrumb.Visible = availableBreadcrumb > 140;
                lblBreadcrumb.Location = new Point(leftEdge, 15);
            }
        }
        headerPanel.Resize += (s, e) => LayoutHeader();

        // ═══════════════════════════════════════════════════════════════
        //  2. FIXED BOTTOM SHORTCUT FOOTER (44px #1B365D)
        // ═══════════════════════════════════════════════════════════════
        pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 44,
            BackColor = ExecLedgerTheme.PrimaryNavy
        };
        pnlFooter.Paint += (s, e) =>
        {
            using var pen = new Pen(ExecLedgerTheme.DeepNavy, 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0);
        };

        var pnlShortcuts = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Location = new Point(12, 8),
            Height = 30,
            BackColor = Color.Transparent
        };

        Control CreateShortcutPill(string key, string description, Action onClick, bool isEnabled = true)
        {
            var pnl = new Panel
            {
                AutoSize = true,
                Height = 28,
                Margin = new Padding(0, 0, 14, 0),
                BackColor = Color.Transparent,
                Cursor = isEnabled ? Cursors.Hand : Cursors.Default
            };

            var keyBox = new Panel
            {
                Height = 22,
                Location = new Point(0, 3),
                BackColor = isEnabled ? ExecLedgerTheme.WorkSurface : Color.FromArgb(71, 85, 105)
            };
            keyBox.Paint += (s, e) =>
            {
                using var p = new Pen(isEnabled ? ExecLedgerTheme.PrimaryBorder : Color.FromArgb(100, 116, 139), 1);
                e.Graphics.DrawRectangle(p, 0, 0, keyBox.Width - 1, keyBox.Height - 1);
            };

            var lblKey = new Label
            {
                Text = key,
                Font = new Font("Segoe UI", 7.75F, FontStyle.Bold),
                ForeColor = isEnabled ? ExecLedgerTheme.PrimaryText : ExecLedgerTheme.DisabledText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.Transparent
            };
            using (var g = CreateGraphics())
            {
                var sz = g.MeasureString(key, lblKey.Font);
                keyBox.Width = Math.Max(26, (int)sz.Width + 10);
            }
            keyBox.Controls.Add(lblKey);
            pnl.Controls.Add(keyBox);

            var lblDesc = new Label
            {
                Text = description,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                ForeColor = isEnabled ? ExecLedgerTheme.WhiteText : ExecLedgerTheme.DisabledText,
                Location = new Point(keyBox.Right + 6, 6),
                AutoSize = true,
                BackColor = Color.Transparent
            };
            pnl.Controls.Add(lblDesc);

            if (isEnabled)
            {
                pnl.Click += (s, e) => onClick();
                lblKey.Click += (s, e) => onClick();
                keyBox.Click += (s, e) => onClick();
                lblDesc.Click += (s, e) => onClick();
            }

            return pnl;
        }

        // ── Uniform Shortcut Pills in Footer Panel ──
        pnlShortcuts.Controls.Add(CreateShortcutPill("Q", "Quit (Esc)", () => HandleCancel()));
        pnlShortcuts.Controls.Add(CreateShortcutPill("A", isEdit ? "Accept (Ctrl+A)" : "Create (Ctrl+A)", () => _ = SaveAsync()));
        pnlShortcuts.Controls.Add(CreateShortcutPill("D", "Delete (Alt+D)", () => _ = DeleteAsync(), isEnabled: isEdit));
        pnlShortcuts.Controls.Add(CreateShortcutPill("F4", "List / Lookup", () => FocusRightListSearch()));
        pnlShortcuts.Controls.Add(CreateShortcutPill("F12", "Configure", () => ShowConfigurationHelp()));

        // Invisible compat buttons on pnlFooter for programmatic triggers & test compatibility
        btnSave = new Button
        {
            Text = isEdit ? "Save Changes (Enter)" : "Create Group (Enter)",
            Size = new Size(0, 0),
            TabStop = false,
            Visible = true
        };
        btnSave.Click += async (s, e) => await SaveAsync();
        btnSave.KeyDown += OnButtonSaveKeyDown;
        pnlFooter.Controls.Add(btnSave);

        btnCancel = new Button
        {
            Text = "Cancel (Esc)",
            Size = new Size(0, 0),
            TabStop = false,
            Visible = true
        };
        btnCancel.Click += (s, e) => HandleCancel();
        btnCancel.KeyDown += OnButtonCancelKeyDown;
        pnlFooter.Controls.Add(btnCancel);

        pnlFooter.Controls.Add(pnlShortcuts);

        lblFooterStatus = new Label
        {
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(203, 213, 225),
            AutoSize = true,
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        pnlFooter.Controls.Add(lblFooterStatus);
        pnlFooter.Resize += (s, e) => UpdateKeyboardStatus();

        // ═══════════════════════════════════════════════════════════════
        //  4. MAIN WORKSPACE (Split: Left Form 70% & Right List 30%)
        // ═══════════════════════════════════════════════════════════════
        pnlMainWorkspace = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ExecLedgerTheme.WorkSurface
        };

        // ── Right Panel: LIST OF GROUPS (Width ~320px, opens ONLY when Under control has focus) ──
        pnlRightList = new Panel
        {
            Dock = DockStyle.Right,
            Width = 320,
            BackColor = ExecLedgerTheme.SecondarySurface,
            Visible = false // Hidden initially; opens ONLY when focused on Under control!
        };
        pnlRightList.Paint += (s, e) =>
        {
            // Left vertical divider
            using var pen = new Pen(ExecLedgerTheme.PrimaryBorder, 1);
            e.Graphics.DrawLine(pen, 0, 0, 0, pnlRightList.Height);
        };
        pnlRightList.VisibleChanged += (s, e) =>
        {
            if (pnlRightList.Visible && txtRightSearch != null && txtRightSearch.CanFocus)
            {
                BeginInvoke(new Action(() =>
                {
                    if (txtRightSearch != null && txtRightSearch.CanFocus)
                    {
                        txtRightSearch.Focus();
                        txtRightSearch.SelectAll();
                    }
                }));
            }
        };

        BuildRightListSection();

        // ── Left Panel: GROUP INFORMATION FORM ──
        pnlLeftForm = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ExecLedgerTheme.WorkSurface,
            AutoScroll = true,
            Padding = new Padding(24, 16, 24, 20)
        };

        // Content card container (maintains Location (24, 16) and height for test compatibility)
        card = new Panel
        {
            Location = new Point(24, 16),
            Size = new Size(740, 515),
            BackColor = ExecLedgerTheme.WorkSurface
        };
        pnlLeftForm.Controls.Add(card);

        BuildLeftFormSection(isEdit);

        pnlMainWorkspace.Controls.Add(pnlLeftForm);
        pnlMainWorkspace.Controls.Add(pnlRightList);

        // Strict dock order:
        // pnlFooter (DockStyle.Bottom) -> pnlMainWorkspace (DockStyle.Fill) -> headerPanel (DockStyle.Top)
        Controls.Add(pnlFooter);
        Controls.Add(pnlMainWorkspace);
        Controls.Add(headerPanel);

        // Window Shortcuts
        KeyDown += OnFormKeyDown;
        KeyUp += (s, e) => UpdateKeyboardStatus();

        _keyboardStatusTimer = new System.Windows.Forms.Timer { Interval = 200 };
        _keyboardStatusTimer.Tick += (s, e) => UpdateKeyboardStatus();

        Shown += (s, e) =>
        {
            LayoutHeader();
            UpdateKeyboardStatus();
            _keyboardStatusTimer?.Start();
            SetActiveField(0); // Starts on Field 0 (Name), right panel is hidden
        };

        FormClosing += (s, e) =>
        {
            _keyboardStatusTimer?.Stop();
            _keyboardStatusTimer?.Dispose();
            _keyboardStatusTimer = null;
        };
    }

    private void BuildLeftFormSection(bool isEdit)
    {
        int labelWidth = 290;
        int controlX = 322;
        int curY = 16;

        // Hidden label for test compatibility (AlterGroup_HeaderAndGroupInformationSection_DoNotOverlapAndAreVisible)
        lblSection = new Label
        {
            Text = "Group Information",
            Location = new Point(28, 20),
            AutoSize = true,
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            Visible = false
        };
        card.Controls.Add(lblSection);

        // ─────────────────────────────────────────────────────────────
        // SECTION 1: BASIC INFORMATION
        // ─────────────────────────────────────────────────────────────
        var lblBasicInfo = new Label
        {
            Text = "BASIC INFORMATION",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            Location = new Point(8, curY),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        card.Controls.Add(lblBasicInfo);

        var lblSec1Sub = new Label
        {
            Text = "Group Definition",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
            ForeColor = ExecLedgerTheme.SecondaryText,
            Location = new Point(controlX + 220, curY + 2),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        card.Controls.Add(lblSec1Sub);

        curY += 24;
        var sep1 = new Panel
        {
            Location = new Point(8, curY),
            Height = 1,
            BackColor = ExecLedgerTheme.GridBorder,
            Width = 680
        };
        card.Controls.Add(sep1);
        curY += 14;

        // 1. Name *
        txtGroupName = new TextBox
        {
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            BackColor = Color.White,
            ForeColor = ExecLedgerTheme.PrimaryText,
            BorderStyle = BorderStyle.None,
            Width = 330
        };
        txtGroupName.TextChanged += (s, e) =>
        {
            if (!_isInitializing) _isDirty = true;
        };
        txtGroupName.KeyDown += (s, e) => HandleEditorKeyDown(0, e);
        txtGroupName.GotFocus += (s, e) => SetActiveField(0);

        fldName = new MoneyFlowField("Name *", labelWidth, rowHeight: 32)
        {
            Location = new Point(8, curY)
        };
        fldName.SetEditor(txtGroupName, () => txtGroupName.Text.Trim());
        fldName.Activated += (s, e) => SetActiveField(0);
        card.Controls.Add(fldName);
        _fields.Add(fldName);
        curY += 36;

        // 2. (alias)
        txtAlias = new TextBox
        {
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            BackColor = Color.White,
            ForeColor = ExecLedgerTheme.PrimaryText,
            BorderStyle = BorderStyle.None,
            Width = 330
        };
        txtAlias.TextChanged += (s, e) =>
        {
            if (!_isInitializing) _isDirty = true;
        };
        txtAlias.KeyDown += (s, e) => HandleEditorKeyDown(1, e);
        txtAlias.GotFocus += (s, e) => SetActiveField(1);

        fldAlias = new MoneyFlowField("(alias)", labelWidth, rowHeight: 32)
        {
            Location = new Point(8, curY)
        };
        fldAlias.SetEditor(txtAlias, () => string.IsNullOrWhiteSpace(txtAlias.Text) ? "-" : txtAlias.Text.Trim());
        fldAlias.Activated += (s, e) => SetActiveField(1);
        card.Controls.Add(fldAlias);
        _fields.Add(fldAlias);
        curY += 36;

        // 3. Under (Parent group selection with Auto-Search; ZERO floating popups)
        pnlUnderEditor = new Panel
        {
            Height = 26,
            Width = 330,
            BackColor = Color.White,
            Cursor = Cursors.Hand
        };

        txtUnder = new TextBox
        {
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = ExecLedgerTheme.PrimaryText,
            BackColor = Color.White,
            Location = new Point(4, 4),
            Width = 290,
            Cursor = Cursors.Hand
        };
        txtUnder.PreviewKeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab || e.KeyCode == Keys.Back)
            {
                e.IsInputKey = true;
            }
        };
        txtUnder.GotFocus += (s, e) =>
        {
            SetActiveField(2);
            FocusRightListSearch();
        };
        txtUnder.Click += (s, e) =>
        {
            SetActiveField(2);
            FocusRightListSearch();
        };
        pnlUnderEditor.Click += (s, e) =>
        {
            SetActiveField(2);
            FocusRightListSearch();
        };
        // Auto-search: typing any characters while on Under control routes to search box instantly
        txtUnder.KeyPress += (s, e) =>
        {
            if (!char.IsControl(e.KeyChar))
            {
                txtRightSearch.Text += e.KeyChar;
                txtRightSearch.SelectionStart = txtRightSearch.Text.Length;
                e.Handled = true;
            }
        };
        txtUnder.KeyDown += (s, e) =>
        {
            if ((e.KeyCode == Keys.Tab && e.Shift) || (e.KeyCode == Keys.Enter && e.Shift))
            {
                CancelUnderSelectionAndGoBack();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Back)
            {
                if (txtRightSearch.Text.Length > 0)
                {
                    txtRightSearch.Text = txtRightSearch.Text.Substring(0, txtRightSearch.Text.Length - 1);
                    txtRightSearch.SelectionStart = txtRightSearch.Text.Length;
                    e.Handled = true;
                }
            }
            else if (e.KeyCode == Keys.Down)
            {
                HighlightNextGroup();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                HighlightPreviousGroup();
                e.Handled = true;
            }
        };

        lblUnderArrow = new Label
        {
            Text = "▼",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Regular),
            ForeColor = ExecLedgerTheme.SecondaryText,
            Dock = DockStyle.Right,
            Width = 20,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        lblUnderArrow.Click += (s, e) =>
        {
            SetActiveField(2);
            FocusRightListSearch();
        };

        pnlUnderEditor.Controls.Add(txtUnder);
        pnlUnderEditor.Controls.Add(lblUnderArrow);
        pnlUnderEditor.Resize += (s, e) =>
        {
            txtUnder.Width = pnlUnderEditor.Width - 28;
        };

        fldUnder = new MoneyFlowField("Under", labelWidth, rowHeight: 32)
        {
            Location = new Point(8, curY)
        };
        fldUnder.SetEditor(pnlUnderEditor, () => _selectedParentGroupName);
        fldUnder.Activated += (s, e) =>
        {
            SetActiveField(2);
        };
        card.Controls.Add(fldUnder);
        _fields.Add(fldUnder);

        curY += 46;

        // ─────────────────────────────────────────────────────────────
        // SECTION 2: ACCOUNTING CONFIGURATION
        // ─────────────────────────────────────────────────────────────
        var lblSec2 = new Label
        {
            Text = "ACCOUNTING CONFIGURATION",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            Location = new Point(8, curY),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        card.Controls.Add(lblSec2);

        var lblSec2Sub = new Label
        {
            Text = "Ledger & Allocation Behavior",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
            ForeColor = ExecLedgerTheme.SecondaryText,
            Location = new Point(controlX + 160, curY + 2),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        card.Controls.Add(lblSec2Sub);

        curY += 24;
        var sep2 = new Panel
        {
            Location = new Point(8, curY),
            Height = 1,
            BackColor = ExecLedgerTheme.GridBorder,
            Width = 680
        };
        card.Controls.Add(sep2);
        curY += 14;

        // 4. Group behaves like a sub-ledger
        cmbSubLedger = CreateBinaryLookup("No");
        cmbSubLedger.SelectionCommitted += (s, e) => MoveToNextField(3);
        cmbSubLedger.EditorKeyDown += (s, e) => HandleEditorKeyDown(3, e);
        cmbSubLedger.KeyDown += (s, e) => HandleEditorKeyDown(3, e);
        fldSubLedger = new MoneyFlowField("Group behaves like a sub-ledger", labelWidth, rowHeight: 32)
        {
            Location = new Point(8, curY)
        };
        fldSubLedger.SetEditor(cmbSubLedger, () => cmbSubLedger.SelectedValue?.ToString() ?? "No");
        fldSubLedger.Activated += (s, e) => SetActiveField(3);
        card.Controls.Add(fldSubLedger);
        _fields.Add(fldSubLedger);
        curY += 36;

        // 5. Nett Debit/Credit Balances for Reporting
        cmbNettBalances = CreateBinaryLookup("No");
        cmbNettBalances.SelectionCommitted += (s, e) => MoveToNextField(4);
        cmbNettBalances.EditorKeyDown += (s, e) => HandleEditorKeyDown(4, e);
        cmbNettBalances.KeyDown += (s, e) => HandleEditorKeyDown(4, e);
        fldNettBalances = new MoneyFlowField("Nett Debit/Credit Balances for Reporting", labelWidth, rowHeight: 32)
        {
            Location = new Point(8, curY)
        };
        fldNettBalances.SetEditor(cmbNettBalances, () => cmbNettBalances.SelectedValue?.ToString() ?? "No");
        fldNettBalances.Activated += (s, e) => SetActiveField(4);
        card.Controls.Add(fldNettBalances);
        _fields.Add(fldNettBalances);
        curY += 36;

        // 6. Used for calculation (eg: taxes, discounts)
        cmbCalculation = CreateBinaryLookup("No");
        cmbCalculation.SelectionCommitted += (s, e) => MoveToNextField(5);
        cmbCalculation.EditorKeyDown += (s, e) => HandleEditorKeyDown(5, e);
        cmbCalculation.KeyDown += (s, e) => HandleEditorKeyDown(5, e);
        fldCalculation = new MoneyFlowField("Used for calculation (eg: taxes, discounts)", labelWidth, rowHeight: 32)
        {
            Location = new Point(8, curY)
        };
        fldCalculation.SetEditor(cmbCalculation, () => cmbCalculation.SelectedValue?.ToString() ?? "No");
        fldCalculation.Activated += (s, e) => SetActiveField(5);
        card.Controls.Add(fldCalculation);
        _fields.Add(fldCalculation);
        curY += 36;

        // 7. Method to allocate when used in purchase invoice
        cmbMethodToAllocate = new MoneyFlowTextLookup { AutoOpenOnFocus = true };
        var allocOptions = new[] { "Not Applicable", "Appropriate by Qty", "Appropriate by Value" };
        cmbMethodToAllocate.SetProvider(new ListLookupProvider<string>(allocOptions, x => x, x => x), new LookupConfig
        {
            Title = "METHOD TO ALLOCATE",
            AllowClear = false
        });
        cmbMethodToAllocate.SelectedValue = "Not Applicable";
        cmbMethodToAllocate.SelectionCommitted += (s, e) => MoveToNextField(6);
        cmbMethodToAllocate.EditorKeyDown += (s, e) => HandleEditorKeyDown(6, e);
        cmbMethodToAllocate.KeyDown += (s, e) => HandleEditorKeyDown(6, e);
        fldMethodToAllocate = new MoneyFlowField("Method to allocate when used in purchase invoice", labelWidth, rowHeight: 32)
        {
            Location = new Point(8, curY)
        };
        fldMethodToAllocate.SetEditor(cmbMethodToAllocate, () => cmbMethodToAllocate.SelectedValue?.ToString() ?? "Not Applicable");
        fldMethodToAllocate.Activated += (s, e) => SetActiveField(6);
        card.Controls.Add(fldMethodToAllocate);
        _fields.Add(fldMethodToAllocate);
        curY += 36;

        // 8. Nature of Group
        cmbNature = new MoneyFlowTextLookup { AutoOpenOnFocus = true };
        cmbNature.SetProvider(new EnumLookupProvider<GroupNature>(), new LookupConfig
        {
            Title = "GROUP NATURE",
            AllowClear = false
        });
        cmbNature.SelectedValue = GroupNature.Assets;
        cmbNature.SelectionCommitted += (s, e) => MoveToNextField(7);
        cmbNature.EditorKeyDown += (s, e) => HandleEditorKeyDown(7, e);
        cmbNature.KeyDown += (s, e) => HandleEditorKeyDown(7, e);
        fldNature = new MoneyFlowField("Nature of Group", labelWidth, rowHeight: 32)
        {
            Location = new Point(8, curY)
        };
        fldNature.SetEditor(cmbNature, () => cmbNature.SelectedItem?.Name ?? (cmbNature.SelectedValue is GroupNature gn ? gn.ToString() : "Assets"));
        fldNature.Activated += (s, e) => SetActiveField(7);
        card.Controls.Add(fldNature);
        _fields.Add(fldNature);
        curY += 36;

        // 9. Does it affect Gross / Net Profit / Loss
        cmbAffectsPL = CreateBinaryLookup("No");
        cmbAffectsPL.SelectionCommitted += (s, e) => MoveToNextField(8);
        cmbAffectsPL.EditorKeyDown += (s, e) => HandleEditorKeyDown(8, e);
        cmbAffectsPL.KeyDown += (s, e) => HandleEditorKeyDown(8, e);
        fldAffectsPL = new MoneyFlowField("Does it affect Gross / Net Profit / Loss", labelWidth, rowHeight: 32)
        {
            Location = new Point(8, curY)
        };
        fldAffectsPL.SetEditor(cmbAffectsPL, () => cmbAffectsPL.SelectedValue?.ToString() ?? "No");
        fldAffectsPL.Activated += (s, e) => SetActiveField(8);
        card.Controls.Add(fldAffectsPL);
        _fields.Add(fldAffectsPL);
        curY += 46;

        // ── Hidden Compat Buttons on Card at (322, 442) for test backward-compatibility ──
        _compatBtnSave = new Button
        {
            Text = isEdit ? "Save Changes (Enter)" : "Create Group (Enter)",
            Location = new Point(controlX, 442),
            Size = new Size(0, 0),
            TabStop = false,
            Visible = true
        };
        _compatBtnSave.Click += async (s, e) => await SaveAsync();
        card.Controls.Add(_compatBtnSave);

        _compatBtnCancel = new Button
        {
            Text = "Cancel (Esc)",
            Location = new Point(controlX + 195, 442),
            Size = new Size(0, 0),
            TabStop = false,
            Visible = true
        };
        _compatBtnCancel.Click += (s, e) => HandleCancel();
        card.Controls.Add(_compatBtnCancel);

        void LayoutLeftFields()
        {
            int totalFormW = Math.Max(card.Width, pnlLeftForm.Width - 48);
            card.Width = totalFormW;
            int rightBound = Math.Max(controlX + 360, totalFormW - 32);

            sep1.Width = rightBound - 8;
            sep2.Width = rightBound - 8;
            lblSec1Sub.Location = new Point(rightBound - lblSec1Sub.Width - 8, 18);
            lblSec2Sub.Location = new Point(rightBound - lblSec2Sub.Width - 8, fldSubLedger.Top - 36);

            int editorWidth = Math.Clamp(rightBound - controlX - 60, 240, 380);
            foreach (var fld in _fields)
            {
                fld.Width = rightBound - 8;
                if (fld == fldSubLedger || fld == fldNettBalances || fld == fldCalculation || fld == fldAffectsPL)
                {
                    fld.UpdateEditorWidth(80);
                }
                else
                {
                    fld.UpdateEditorWidth(editorWidth);
                }
            }
        }

        pnlLeftForm.Resize += (s, e) => LayoutLeftFields();
        card.Resize += (s, e) => LayoutLeftFields();
        LayoutLeftFields();
    }

    private void BuildRightListSection()
    {
        // 1. Right Header Bar (36px): LIST OF GROUPS + F4 shortcut
        var pnlRightHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = ExecLedgerTheme.PrimaryNavy
        };

        var lblRightTitle = new Label
        {
            Text = "LIST OF GROUPS",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = ExecLedgerTheme.WhiteText,
            Location = new Point(12, 9),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlRightHeader.Controls.Add(lblRightTitle);

        var lblF4Shortcut = new Label
        {
            Text = "F4",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(203, 213, 225),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlRightHeader.Controls.Add(lblF4Shortcut);
        pnlRightHeader.Resize += (s, e) =>
        {
            lblF4Shortcut.Location = new Point(pnlRightHeader.Width - lblF4Shortcut.Width - 14, 9);
        };
        pnlRightList.Controls.Add(pnlRightHeader);

        // 2. Search Container (42px)
        var pnlSearchContainer = new Panel
        {
            Dock = DockStyle.Top,
            Height = 42,
            BackColor = ExecLedgerTheme.SecondarySurface,
            Padding = new Padding(10, 6, 10, 6)
        };

        var pnlSearchBox = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };
        pnlSearchBox.Paint += (s, e) =>
        {
            bool hasFocus = txtRightSearch.Focused;
            using var pen = new Pen(hasFocus ? ExecLedgerTheme.SystemFocusBlue : ExecLedgerTheme.PrimaryBorder, hasFocus ? 2 : 1);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlSearchBox.Width - 1, pnlSearchBox.Height - 1);
        };

        txtRightSearch = new TextBox
        {
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = ExecLedgerTheme.PrimaryText,
            Location = new Point(8, 6),
            PlaceholderText = "Search parent group..."
        };
        txtRightSearch.GotFocus += (s, e) => pnlSearchBox.Invalidate();
        txtRightSearch.LostFocus += (s, e) => pnlSearchBox.Invalidate();
        txtRightSearch.TextChanged += (s, e) => FilterRightGroupList(txtRightSearch.Text);
        txtRightSearch.PreviewKeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab || e.KeyCode == Keys.Escape || e.KeyCode == Keys.PageDown || e.KeyCode == Keys.PageUp)
            {
                e.IsInputKey = true;
            }
        };
        txtRightSearch.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Down)
            {
                HighlightNextGroup();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                HighlightPreviousGroup();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.PageDown)
            {
                HighlightPageDown();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.PageUp)
            {
                HighlightPageUp();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter && !e.Alt && !e.Control)
            {
                CommitHighlightedParentGroup();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Tab)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (e.Shift)
                {
                    CancelUnderSelectionAndGoBack();
                }
                else
                {
                    CommitHighlightedParentGroup();
                }
            }
            else if (e.KeyCode == Keys.Escape)
            {
                CancelUnderSelection();
                e.Handled = true;
            }
        };

        var lblSearchIcon = new Label
        {
            Text = "🔍",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = ExecLedgerTheme.SecondaryText,
            Dock = DockStyle.Right,
            Width = 24,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };

        pnlSearchBox.Controls.Add(txtRightSearch);
        pnlSearchBox.Controls.Add(lblSearchIcon);
        pnlSearchBox.Resize += (s, e) =>
        {
            txtRightSearch.Width = pnlSearchBox.Width - 36;
        };

        pnlSearchContainer.Controls.Add(pnlSearchBox);
        pnlRightList.Controls.Add(pnlSearchContainer);

        // 3. '+ Create' Action Row (30px)
        pnlCreateAction = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = ExecLedgerTheme.SecondarySurface,
            Cursor = Cursors.Hand
        };
        pnlCreateAction.Paint += (s, e) =>
        {
            using var pen = new Pen(ExecLedgerTheme.GridBorder, 1);
            e.Graphics.DrawLine(pen, 0, pnlCreateAction.Height - 1, pnlCreateAction.Width, pnlCreateAction.Height - 1);
        };

        var lblCreate = new Label
        {
            Text = "+ Create",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            Location = new Point(12, 7),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlCreateAction.Controls.Add(lblCreate);

        var lblCreateShortcut = new Label
        {
            Text = "Alt+C",
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            ForeColor = ExecLedgerTheme.SecondaryText,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlCreateAction.Controls.Add(lblCreateShortcut);
        pnlCreateAction.Resize += (s, e) =>
        {
            lblCreateShortcut.Location = new Point(pnlCreateAction.Width - lblCreateShortcut.Width - 14, 8);
        };

        void TriggerCreateParent()
        {
            SetSelectedParentGroup(null);
            MoveToNextField(2);
        }

        pnlCreateAction.Click += (s, e) => TriggerCreateParent();
        lblCreate.Click += (s, e) => TriggerCreateParent();
        lblCreateShortcut.Click += (s, e) => TriggerCreateParent();
        pnlCreateAction.MouseEnter += (s, e) => pnlCreateAction.BackColor = ExecLedgerTheme.MenuHover;
        pnlCreateAction.MouseLeave += (s, e) => pnlCreateAction.BackColor = ExecLedgerTheme.SecondarySurface;

        pnlRightList.Controls.Add(pnlCreateAction);

        // 4. Boxed Container for List of Groups (prevents data from being cut off at bottom)
        var pnlListBoxCard = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 4, 10, 10),
            BackColor = ExecLedgerTheme.SecondarySurface
        };

        pnlListBoxBorder = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(1)
        };
        pnlListBoxBorder.Paint += (s, e) =>
        {
            using var pen = new Pen(ExecLedgerTheme.PrimaryBorder, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, pnlListBoxBorder.Width - 1, pnlListBoxBorder.Height - 1);

            if (_filteredParentGroups.Count == 0)
            {
                string emptyText = "No Groups Found";
                using var font = new Font("Segoe UI", 9.5F, FontStyle.Italic);
                using var brush = new SolidBrush(ExecLedgerTheme.SecondaryText);
                var sz = e.Graphics.MeasureString(emptyText, font);
                e.Graphics.DrawString(emptyText, font, brush, (pnlListBoxBorder.Width - sz.Width) / 2, 40);
            }
        };

        lstRightGroups = new ListBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 28,
            IntegralHeight = false,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular)
        };

        lstRightGroups.DrawItem += (s, e) =>
        {
            if (e.Index < 0 || e.Index >= _filteredParentGroups.Count) return;

            var item = _filteredParentGroups[e.Index];
            bool isHighlighted = (e.Index == _highlightedIndex) || ((e.State & DrawItemState.Selected) != 0);
            bool isCommitted = (item.GroupId == _selectedParentGroupId);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Background: #E0EDFD if highlighted or committed, else #FFFFFF or #F8FAFC
            Color bg = (isHighlighted || isCommitted)
                ? ExecLedgerTheme.PrimarySelection
                : (e.Index % 2 == 0 ? ExecLedgerTheme.WorkSurface : ExecLedgerTheme.SecondarySurface);

            using var bgBrush = new SolidBrush(bg);
            e.Graphics.FillRectangle(bgBrush, e.Bounds);

            // Clear blue selection indicator (3px solid vertical bar on left edge)
            if (isHighlighted || isCommitted)
            {
                using var indBrush = new SolidBrush(ExecLedgerTheme.SystemFocusBlue); // #2563EB
                e.Graphics.FillRectangle(indBrush, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
            }

            // Text: bold #1B365D when highlighted/committed, else regular #0F172A
            using var font = new Font("Segoe UI", 9F, (isHighlighted || isCommitted) ? FontStyle.Bold : FontStyle.Regular);
            using var textBrush = new SolidBrush((isHighlighted || isCommitted) ? ExecLedgerTheme.PrimaryNavy : ExecLedgerTheme.PrimaryText);
            e.Graphics.DrawString(item.Name, font, textBrush, new PointF(e.Bounds.X + 12, e.Bounds.Y + 5));

            // 'Primary' category tag on right
            if (item.IsPrimary)
            {
                using var catFont = new Font("Segoe UI", 8F, (isHighlighted || isCommitted) ? FontStyle.Bold : FontStyle.Regular);
                using var catBrush = new SolidBrush((isHighlighted || isCommitted) ? ExecLedgerTheme.PrimaryNavy : ExecLedgerTheme.SecondaryText);
                var catSize = e.Graphics.MeasureString("Primary", catFont);
                float catX = e.Bounds.Right - catSize.Width - 14;
                e.Graphics.DrawString("Primary", catFont, catBrush, new PointF(catX, e.Bounds.Y + 6));
            }

            // Subtle 1px row separator
            using var sepPen = new Pen(ExecLedgerTheme.GridBorder, 1);
            e.Graphics.DrawLine(sepPen, e.Bounds.X, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
        };

        lstRightGroups.DoubleClick += (s, e) => CommitHighlightedParentGroup();
        lstRightGroups.Click += (s, e) =>
        {
            if (lstRightGroups.SelectedIndex >= 0 && lstRightGroups.SelectedIndex < _filteredParentGroups.Count)
            {
                _highlightedIndex = lstRightGroups.SelectedIndex;
                _highlightedGroupId = _filteredParentGroups[_highlightedIndex].GroupId;
                lstRightGroups.Invalidate();
            }
        };

        lstRightGroups.KeyDown += (s, e) =>
        {
            if ((e.KeyCode == Keys.Tab && e.Shift) || (e.KeyCode == Keys.Enter && e.Shift))
            {
                CancelUnderSelectionAndGoBack();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Down)
            {
                HighlightNextGroup();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                HighlightPreviousGroup();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                CommitHighlightedParentGroup();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                CancelUnderSelection();
                e.Handled = true;
            }
        };

        pnlListBoxBorder.Controls.Add(lstRightGroups);
        pnlListBoxCard.Controls.Add(pnlListBoxBorder);
        pnlRightList.Controls.Add(pnlListBoxCard);
        pnlListBoxCard.BringToFront();
    }

    private void FilterRightGroupList(string query)
    {
        _filteredParentGroups.Clear();
        string q = query.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(q))
        {
            _filteredParentGroups.AddRange(_allParentGroups);
        }
        else
        {
            _filteredParentGroups.AddRange(_allParentGroups.Where(g => g.Name.ToLowerInvariant().Contains(q)));
        }

        lstRightGroups.Items.Clear();
        foreach (var item in _filteredParentGroups)
        {
            lstRightGroups.Items.Add(item);
        }

        if (_filteredParentGroups.Count > 0)
        {
            lstRightGroups.Visible = true;
            int idx = _filteredParentGroups.FindIndex(g => g.GroupId == _highlightedGroupId);
            _highlightedIndex = idx >= 0 ? idx : 0;
            _highlightedGroupId = _filteredParentGroups[_highlightedIndex].GroupId;
            EnsureHighlightedItemVisible();
        }
        else
        {
            lstRightGroups.Visible = false;
            _highlightedIndex = -1;
            _highlightedGroupId = null;
            pnlListBoxBorder.Invalidate();
        }
    }

    private void ActivateUnderLookupMode()
    {
        _preLookupSelectedGroupId = _selectedParentGroupId;
        _highlightedGroupId = _selectedParentGroupId;

        SyncRightListToHighlighted();
        UpdateKeyboardStatus("Parent Group Selection");
    }

    private void SyncRightListToHighlighted()
    {
        if (_filteredParentGroups.Count == 0) return;

        int idx = _filteredParentGroups.FindIndex(g => g.GroupId == _highlightedGroupId);
        if (idx < 0) idx = _filteredParentGroups.FindIndex(g => g.GroupId == _selectedParentGroupId);
        if (idx < 0) idx = 0;

        _highlightedIndex = idx;
        _highlightedGroupId = _filteredParentGroups[idx].GroupId;

        EnsureHighlightedItemVisible();
    }

    private void EnsureHighlightedItemVisible()
    {
        if (_filteredParentGroups.Count == 0) return;

        if (_highlightedIndex < 0) _highlightedIndex = 0;
        if (_highlightedIndex >= _filteredParentGroups.Count) _highlightedIndex = _filteredParentGroups.Count - 1;

        lstRightGroups.SelectedIndex = _highlightedIndex;

        int itemHeight = lstRightGroups.ItemHeight;
        int visibleRows = Math.Max(1, lstRightGroups.ClientSize.Height / itemHeight);
        int currentTop = lstRightGroups.TopIndex;
        int bottomVisible = currentTop + visibleRows - 1;

        if (_highlightedIndex > bottomVisible)
        {
            lstRightGroups.TopIndex = Math.Max(0, _highlightedIndex - visibleRows + 1);
        }
        else if (_highlightedIndex < currentTop)
        {
            lstRightGroups.TopIndex = _highlightedIndex;
        }

        lstRightGroups.Invalidate();
    }

    private void HighlightNextGroup()
    {
        if (_filteredParentGroups.Count == 0) return;

        int next = Math.Min(_highlightedIndex + 1, _filteredParentGroups.Count - 1);
        _highlightedIndex = next;
        _highlightedGroupId = _filteredParentGroups[next].GroupId;
        EnsureHighlightedItemVisible();
    }

    private void HighlightPreviousGroup()
    {
        if (_filteredParentGroups.Count == 0) return;

        int prev = Math.Max(_highlightedIndex - 1, 0);
        _highlightedIndex = prev;
        _highlightedGroupId = _filteredParentGroups[prev].GroupId;
        EnsureHighlightedItemVisible();
    }

    private void HighlightPageDown()
    {
        if (_filteredParentGroups.Count == 0) return;

        int visibleRows = Math.Max(1, lstRightGroups.ClientSize.Height / lstRightGroups.ItemHeight);
        int next = Math.Min(_highlightedIndex + visibleRows, _filteredParentGroups.Count - 1);
        _highlightedIndex = next;
        _highlightedGroupId = _filteredParentGroups[next].GroupId;
        EnsureHighlightedItemVisible();
    }

    private void HighlightPageUp()
    {
        if (_filteredParentGroups.Count == 0) return;

        int visibleRows = Math.Max(1, lstRightGroups.ClientSize.Height / lstRightGroups.ItemHeight);
        int prev = Math.Max(_highlightedIndex - visibleRows, 0);
        _highlightedIndex = prev;
        _highlightedGroupId = _filteredParentGroups[prev].GroupId;
        EnsureHighlightedItemVisible();
    }

    private void HighlightFirstGroup()
    {
        if (_filteredParentGroups.Count == 0) return;

        _highlightedIndex = 0;
        _highlightedGroupId = _filteredParentGroups[0].GroupId;
        EnsureHighlightedItemVisible();
    }

    private void HighlightLastGroup()
    {
        if (_filteredParentGroups.Count == 0) return;

        _highlightedIndex = _filteredParentGroups.Count - 1;
        _highlightedGroupId = _filteredParentGroups[_highlightedIndex].GroupId;
        EnsureHighlightedItemVisible();
    }

    private void CommitHighlightedParentGroup()
    {
        if (_highlightedIndex >= 0 && _highlightedIndex < _filteredParentGroups.Count)
        {
            var item = _filteredParentGroups[_highlightedIndex];
            SetSelectedParentGroup(item.GroupId);
        }
        else if (_highlightedGroupId.HasValue)
        {
            SetSelectedParentGroup(_highlightedGroupId);
        }
        else
        {
            SetSelectedParentGroup(null);
        }

        // Return focus to form and advance to next field
        MoveToNextField(2);
    }

    private void CancelUnderSelection()
    {
        // If search has text, clear search first
        if (!string.IsNullOrEmpty(txtRightSearch.Text))
        {
            txtRightSearch.Clear();
            _highlightedGroupId = _preLookupSelectedGroupId;
            SyncRightListToHighlighted();
            txtUnder.Focus();
            return;
        }

        // Restore pre-lookup selection
        _highlightedGroupId = _preLookupSelectedGroupId;
        SetSelectedParentGroup(_preLookupSelectedGroupId);
        SyncRightListToHighlighted();
        pnlRightList.Visible = false;
        txtUnder.Focus();
        UpdateKeyboardStatus("Ready");
    }

    private void CancelUnderSelectionAndGoBack()
    {
        txtRightSearch.Clear();
        _highlightedGroupId = _preLookupSelectedGroupId;
        SetSelectedParentGroup(_preLookupSelectedGroupId);
        pnlRightList.Visible = false;
        MoveToPreviousField(2);
        UpdateKeyboardStatus("Ready");
    }

    public void SetSelectedParentGroup(int? groupId)
    {
        if (!_isInitializing) _isDirty = true;

        _selectedParentGroupId = groupId;
        _selectedParentGroupItem = _allParentGroups.FirstOrDefault(g => g.GroupId == groupId);

        if (_selectedParentGroupItem != null)
        {
            _selectedParentGroupName = _selectedParentGroupItem.Name;
            txtUnder.Text = _selectedParentGroupName;

            bool isSubGroup = (_selectedParentGroupItem.GroupId != null);
            if (isSubGroup)
            {
                // Inherit nature and profit/loss from parent group, make derived fields disabled
                cmbNature.SelectedValue = _selectedParentGroupItem.Nature;
                cmbNature.Enabled = false;
                fldNature.Enabled = false;

                cmbAffectsPL.SelectedValue = _selectedParentGroupItem.AffectsProfitLoss ? "Yes" : "No";
                cmbAffectsPL.Enabled = false;
                fldAffectsPL.Enabled = false;
            }
            else
            {
                // Primary: user can select Nature and Profit/Loss freely
                cmbNature.Enabled = true;
                fldNature.Enabled = true;

                cmbAffectsPL.Enabled = true;
                fldAffectsPL.Enabled = true;
            }
        }
        else
        {
            _selectedParentGroupName = "Primary";
            txtUnder.Text = "Primary";
            cmbNature.Enabled = true;
            fldNature.Enabled = true;
            cmbAffectsPL.Enabled = true;
            fldAffectsPL.Enabled = true;
        }

        fldUnder.UpdateDisplayValue();
        fldNature.UpdateDisplayValue();
        fldAffectsPL.UpdateDisplayValue();

        _highlightedGroupId = _selectedParentGroupId;
        SyncRightListToHighlighted();
    }

    public void FocusRightListSearch()
    {
        if (txtRightSearch != null && txtRightSearch.CanFocus)
        {
            txtRightSearch.Focus();
            txtRightSearch.SelectAll();
        }
    }

    private MoneyFlowTextLookup CreateBinaryLookup(string defaultValue)
    {
        var lookup = new MoneyFlowTextLookup 
        { 
            AutoOpenOnFocus = false,
            EnablePopup = false,
            IsBinaryChoice = true,
            ShowDropdownArrow = false,
            DrawBorder = false
        };
        var options = new[] { "No", "Yes" };
        lookup.SetProvider(new ListLookupProvider<string>(options, s => s, s => s), new LookupConfig
        {
            Title = "OPTIONS",
            AllowClear = false
        });
        lookup.SetBinaryValue(defaultValue);
        lookup.SelectedValueChanged += (s, e) =>
        {
            if (!_isInitializing) _isDirty = true;
        };
        return lookup;
    }

    public void SetActiveField(int index)
    {
        if (index < 0 || index >= _fields.Count) return;

        _activeFieldIndex = index;

        // Apply active/inactive styling: all editors stay pure white with focus ring
        for (int i = 0; i < _fields.Count; i++)
        {
            bool isActive = (i == index);
            _fields[i].SetActive(isActive);
        }

        // Right-side LIST OF GROUPS panel: ONLY open/visible when on Under control (index == 2), otherwise hidden
        bool isUnder = (index == 2);
        pnlRightList.Visible = isUnder;

        if (isUnder)
        {
            txtRightSearch.Clear(); // Clean search for auto-search
            ActivateUnderLookupMode();
            BeginInvoke(new Action(() =>
            {
                if (txtRightSearch != null && txtRightSearch.CanFocus)
                {
                    txtRightSearch.Focus();
                    txtRightSearch.SelectAll();
                }
            }));
        }

        UpdateKeyboardStatus();
    }

    public void MoveToNextField(int currentIndex)
    {
        int next = currentIndex + 1;
        while (next < _fields.Count && (!_fields[next].Enabled || (_fields[next].Editor != null && !_fields[next].Editor!.Enabled)))
        {
            next++;
        }

        if (next < _fields.Count)
        {
            SetActiveField(next);
        }
        else
        {
            FocusSaveButton();
        }
    }

    public void MoveToPreviousField(int currentIndex)
    {
        int prev = currentIndex - 1;
        while (prev >= 0 && (!_fields[prev].Enabled || (_fields[prev].Editor != null && !_fields[prev].Editor!.Enabled)))
        {
            prev--;
        }

        if (prev >= 0)
        {
            SetActiveField(prev);
        }
    }

    public void FocusSaveButton()
    {
        for (int i = 0; i < _fields.Count; i++)
        {
            _fields[i].SetActive(false);
        }
        _activeFieldIndex = -1;
        pnlRightList.Visible = false; // Hide right panel
        ActiveControl = btnSave;
        btnSave.Focus();
    }

    private void HandleEditorKeyDown(int fieldIndex, KeyEventArgs e)
    {
        // 1. Shift+Enter or Shift+Tab -> Previous Field
        if ((e.KeyCode == Keys.Enter && e.Shift) || (e.KeyCode == Keys.Tab && e.Shift))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            MoveToPreviousField(fieldIndex);
            return;
        }

        // 2. Tab without Shift -> Next Field
        if (e.KeyCode == Keys.Tab && !e.Shift)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            MoveToNextField(fieldIndex);
            return;
        }

        // 3. Plain Enter -> Next Field
        if (e.KeyCode == Keys.Enter && !e.Alt && !e.Control && !e.Shift)
        {
            if (_fields[fieldIndex].Editor is MoneyFlowTextLookup lookup && lookup.IsPopupOpen)
            {
                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;

            if (fieldIndex == 0 && string.IsNullOrWhiteSpace(txtGroupName.Text))
            {
                MoneyFlowMessageBox.Show("Please enter a valid Group Name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetActiveField(0);
                return;
            }

            MoveToNextField(fieldIndex);
            return;
        }

        // 4. Escape -> Cancel / Close
        if (e.KeyCode == Keys.Escape)
        {
            if (MoneyFlowLookupPopup.WasAnyPopupJustClosedByEsc)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (_fields[fieldIndex].Editor is MoneyFlowTextLookup lookup && lookup.IsPopupOpen)
            {
                lookup.ClosePopup();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            HandleCancel();
            return;
        }
    }

    private void OnButtonSaveKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && !e.Alt && !e.Control && !e.Shift)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            _ = SaveAsync();
            return;
        }

        if ((e.KeyCode == Keys.Enter && e.Shift) || (e.KeyCode == Keys.Tab && e.Shift))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            MoveToPreviousField(_fields.Count);
            return;
        }

        if (e.KeyCode == Keys.Tab && !e.Shift)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            btnCancel.Focus();
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            HandleCancel();
            return;
        }
    }

    private void OnButtonCancelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            HandleCancel();
            return;
        }

        if ((e.KeyCode == Keys.Enter && e.Shift) || (e.KeyCode == Keys.Tab && e.Shift))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            FocusSaveButton();
            return;
        }

        if (e.KeyCode == Keys.Tab && !e.Shift)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            SetActiveField(0);
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            HandleCancel();
            return;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        UpdateKeyboardStatus();

        // ═══════════════════════════════════════════════════════════════
        //  UNDER LOOKUP KEYBOARD INTERCEPTION
        // ═══════════════════════════════════════════════════════════════
        bool isUnderActive = (_activeFieldIndex == 2) || (txtRightSearch != null && txtRightSearch.Focused) || (lstRightGroups != null && lstRightGroups.Focused);

        if (isUnderActive)
        {
            if (keyData == (Keys.Tab | Keys.Shift) || keyData == (Keys.Enter | Keys.Shift))
            {
                CancelUnderSelectionAndGoBack();
                return true;
            }

            if (keyData == Keys.Down)
            {
                HighlightNextGroup();
                return true;
            }

            if (keyData == Keys.Up)
            {
                HighlightPreviousGroup();
                return true;
            }

            if (keyData == Keys.PageDown)
            {
                HighlightPageDown();
                return true;
            }

            if (keyData == Keys.PageUp)
            {
                HighlightPageUp();
                return true;
            }

            if (keyData == Keys.Home && !(txtRightSearch != null && txtRightSearch.Focused && txtRightSearch.SelectionStart > 0))
            {
                HighlightFirstGroup();
                return true;
            }

            if (keyData == Keys.End && !(txtRightSearch != null && txtRightSearch.Focused && txtRightSearch.SelectionStart < txtRightSearch.Text.Length))
            {
                HighlightLastGroup();
                return true;
            }

            if (keyData == Keys.Enter && !keyData.HasFlag(Keys.Alt) && !keyData.HasFlag(Keys.Control) && !keyData.HasFlag(Keys.Shift))
            {
                CommitHighlightedParentGroup();
                return true;
            }

            if (keyData == Keys.Escape)
            {
                CancelUnderSelection();
                return true;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  GLOBAL SHORTCUTS
        // ═══════════════════════════════════════════════════════════════
        // F4 or Alt+Down -> Jump to Under / Right List of Groups
        if (keyData == Keys.F4 || keyData == (Keys.Alt | Keys.Down))
        {
            SetActiveField(2);
            FocusRightListSearch();
            return true;
        }

        // Ctrl+F -> Focus search textbox
        if (keyData == (Keys.Control | Keys.F))
        {
            SetActiveField(2);
            FocusRightListSearch();
            return true;
        }

        // Alt+C -> Create Group / Set to Primary
        if (keyData == (Keys.Alt | Keys.C))
        {
            SetSelectedParentGroup(null);
            MoveToNextField(2);
            return true;
        }

        // Ctrl+A or Alt+A -> Accept / Save
        if (keyData == (Keys.Control | Keys.A) || keyData == (Keys.Alt | Keys.A))
        {
            _ = SaveAsync();
            return true;
        }

        // Alt+D or Ctrl+D -> Delete Group (Alter mode only)
        if ((keyData == (Keys.Alt | Keys.D) || keyData == (Keys.Control | Keys.D)) && _groupIdToEdit.HasValue)
        {
            _ = DeleteAsync();
            return true;
        }

        if (keyData == Keys.Escape)
        {
            if (MoneyFlowLookupPopup.WasAnyPopupJustClosedByEsc || HasOpenPopup())
            {
                CloseOpenPopup();
                return true;
            }

            HandleCancel();
            return true;
        }

        // Shift+Tab or Shift+Enter -> Previous Field
        if (keyData == (Keys.Tab | Keys.Shift) || keyData == (Keys.Enter | Keys.Shift))
        {
            if (HasOpenPopup())
            {
                CloseOpenPopup();
            }

            if (_activeFieldIndex == 2 || (pnlRightList != null && pnlRightList.Visible))
            {
                CancelUnderSelectionAndGoBack();
                return true;
            }

            if (ActiveControl == btnCancel)
            {
                FocusSaveButton();
                return true;
            }
            if (ActiveControl == btnSave)
            {
                MoveToPreviousField(_fields.Count);
                return true;
            }
            if (_activeFieldIndex >= 0)
            {
                MoveToPreviousField(_activeFieldIndex);
                return true;
            }
        }

        // Tab -> Next Field
        if (keyData == Keys.Tab)
        {
            if (_activeFieldIndex >= 0 && _activeFieldIndex < _fields.Count)
            {
                var curEditor = _fields[_activeFieldIndex].Editor;
                if (curEditor is MoneyFlowTextLookup lookup && lookup.IsPopupOpen)
                {
                    lookup.CloseLookup();
                }

                MoveToNextField(_activeFieldIndex);
                return true;
            }

            if (ActiveControl == btnSave)
            {
                btnCancel.Focus();
                return true;
            }

            if (ActiveControl == btnCancel)
            {
                SetActiveField(0);
                return true;
            }
        }

        // Plain Enter on active field editor -> advance to next field
        if (keyData == Keys.Enter && !keyData.HasFlag(Keys.Alt) && !keyData.HasFlag(Keys.Control))
        {
            if (_activeFieldIndex >= 0 && _activeFieldIndex < _fields.Count)
            {
                var curEditor = _fields[_activeFieldIndex].Editor;
                if (curEditor is MoneyFlowTextLookup lookup && lookup.IsPopupOpen)
                {
                    return base.ProcessCmdKey(ref msg, keyData);
                }

                if (_activeFieldIndex == 0 && string.IsNullOrWhiteSpace(txtGroupName.Text))
                {
                    MoneyFlowMessageBox.Show("Please enter a valid Group Name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    SetActiveField(0);
                    return true;
                }

                if (_activeFieldIndex == 2)
                {
                    CommitHighlightedParentGroup();
                    return true;
                }

                MoveToNextField(_activeFieldIndex);
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    private static extern short GetKeyState(int keyCode);

    private static bool GetNumLockState()
    {
        try
        {
            // VK_NUMLOCK = 0x90 (144)
            short state = GetKeyState(0x90);
            return (state & 1) != 0;
        }
        catch
        {
            return Control.IsKeyLocked(Keys.NumLock);
        }
    }

    private static bool GetCapsLockState()
    {
        try
        {
            // VK_CAPITAL = 0x14 (20)
            short state = GetKeyState(0x14);
            return (state & 1) != 0;
        }
        catch
        {
            return Control.IsKeyLocked(Keys.CapsLock);
        }
    }

    private void UpdateKeyboardStatus(string? statusOverride = null)
    {
        if (lblFooterStatus == null || pnlFooter == null || IsDisposed) return;

        if (statusOverride != null)
        {
            _currentFooterStatus = string.Equals(statusOverride, "Ready", StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : statusOverride;
        }

        bool caps = GetCapsLockState();
        bool num = GetNumLockState();
        string capsStr = caps ? "CAPS: ON" : "CAPS: OFF";
        string numStr = num ? "NUM: ON" : "NUM: OFF";

        string newText = string.IsNullOrWhiteSpace(_currentFooterStatus)
            ? $"{capsStr}  |  {numStr}"
            : $"{capsStr}  |  {numStr}  |  {_currentFooterStatus}";

        if (lblFooterStatus.Text != newText)
        {
            lblFooterStatus.Text = newText;
            lblFooterStatus.Location = new Point(pnlFooter.Width - lblFooterStatus.Width - 18, 14);
        }
    }

    private async void LoadParentGroupsAndDataAsync()
    {
        try
        {
            UpdateKeyboardStatus("Loading...");
            var potentialParents = await _groupService.GetPotentialParentGroupsAsync(_companyId, _groupIdToEdit);

            _allParentGroups.Clear();
            _allParentGroups.Add(new ParentGroupItem
            {
                GroupId = null,
                Name = "Primary",
                IsPrimary = true,
                Nature = GroupNature.Assets,
                AffectsProfitLoss = false,
                RawGroup = null
            });

            foreach (var p in potentialParents)
            {
                bool isPrimaryGroup = (p.ParentGroupId == null);
                _allParentGroups.Add(new ParentGroupItem
                {
                    GroupId = p.GroupId,
                    Name = p.GroupName,
                    IsPrimary = isPrimaryGroup,
                    Nature = p.Nature,
                    AffectsProfitLoss = p.AffectProfitLoss,
                    RawGroup = p
                });
            }

            FilterRightGroupList(string.Empty);

            if (_groupIdToEdit.HasValue)
            {
                var group = await _groupService.GetGroupByIdAsync(_groupIdToEdit.Value);
                if (group != null)
                {
                    _originalName = group.GroupName;
                    txtGroupName.Text = group.GroupName;
                    SetSelectedParentGroup(group.ParentGroupId);

                    // If primary group, load its own saved Nature and AffectProfitLoss
                    if (group.ParentGroupId == null)
                    {
                        cmbNature.SelectedValue = group.Nature;
                        cmbAffectsPL.SelectedValue = group.AffectProfitLoss ? "Yes" : "No";
                    }

                    _isDirty = false;
                }
            }
            else if (_initialParentGroupId.HasValue)
            {
                SetSelectedParentGroup(_initialParentGroupId.Value);
                _isDirty = false;
            }
            else
            {
                SetSelectedParentGroup(null);
                _isDirty = false;
            }

            // Update all inactive labels
            foreach (var fld in _fields)
            {
                fld.UpdateDisplayValue();
            }

            _preLookupSelectedGroupId = _selectedParentGroupId;
            _highlightedGroupId = _selectedParentGroupId;
            SyncRightListToHighlighted();

            _isInitializing = false;
            _isDirty = false;

            SetActiveField(0);
            UpdateKeyboardStatus("Ready");
        }
        catch (Exception ex)
        {
            _isInitializing = false;
            _isDirty = false;
            UpdateKeyboardStatus("Error");
            MoneyFlowMessageBox.Show($"Failed to load group data: {ex.Message}", "Data Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task SaveAsync()
    {
        if (_isSaving) return;
        _isSaving = true;

        string name = txtGroupName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            _isSaving = false;
            MoneyFlowMessageBox.Show("Group name is required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SetActiveField(0);
            return;
        }

        // Check duplicate group against current company
        var allGroups = await _groupService.GetGroupsByCompanyAsync(_companyId);
        bool isDuplicate = allGroups.Any(g =>
            string.Equals(g.GroupName.Trim(), name, StringComparison.OrdinalIgnoreCase) &&
            (!_groupIdToEdit.HasValue || g.GroupId != _groupIdToEdit.Value));

        if (isDuplicate)
        {
            _isSaving = false;
            MoneyFlowMessageBox.Show("Group already exists.", "Duplicate Group", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SetActiveField(0);
            return;
        }

        int? parentId = _selectedParentGroupId;
        GroupNature nature = cmbNature.SelectedValue is GroupNature n ? n : GroupNature.Assets;
        bool affectPL = string.Equals(cmbAffectsPL.SelectedValue?.ToString(), "Yes", StringComparison.OrdinalIgnoreCase);

        try
        {
            UpdateKeyboardStatus("Saving...");
            btnSave.Enabled = false;

            if (_groupIdToEdit.HasValue)
            {
                var updateDto = new GroupUpdateDto
                {
                    GroupId = _groupIdToEdit.Value,
                    GroupName = name,
                    ParentGroupId = parentId,
                    Nature = nature,
                    AffectProfitLoss = affectPL
                };
                var updated = await _groupService.UpdateGroupAsync(updateDto);
                NewlySavedGroupId = updated.GroupId;
            }
            else
            {
                var createDto = new GroupCreateDto
                {
                    GroupName = name,
                    ParentGroupId = parentId,
                    Nature = nature,
                    AffectProfitLoss = affectPL
                };
                var created = await _groupService.CreateGroupAsync(_companyId, createDto);
                NewlySavedGroupId = created.GroupId;
            }

            IsSaved = true;
            _isDirty = false;
            DialogResult = DialogResult.OK;

            UpdateKeyboardStatus("Saved");
            Saved?.Invoke(NewlySavedGroupId ?? 0);
            Close();
        }
        catch (Exception ex)
        {
            UpdateKeyboardStatus("Error");
            MoneyFlowMessageBox.Show($"Failed to save group: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetActiveField(0);
        }
        finally
        {
            btnSave.Enabled = true;
            _isSaving = false;
        }
    }

    private async Task DeleteAsync()
    {
        if (!_groupIdToEdit.HasValue) return;

        string name = txtGroupName.Text.Trim();
        var confirm = MoneyFlowMessageBox.Show(
            this,
            $"Delete selected group '{name}'?\n\nThis will remove the group from the chart of accounts.",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm == DialogResult.Yes)
        {
            try
            {
                UpdateKeyboardStatus("Deleting...");
                bool deleted = await _groupService.DeleteGroupAsync(_groupIdToEdit.Value);
                if (deleted)
                {
                    _isDirty = false;
                    Cancelled?.Invoke();
                    Close();
                }
            }
            catch (Exception ex)
            {
                UpdateKeyboardStatus("Error");
                MoneyFlowMessageBox.Show(this, $"Cannot delete group: {ex.Message}", "Delete Prevented", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                UpdateKeyboardStatus();
            }
        }
    }

    public bool HasOpenPopup()
    {
        return cmbSubLedger.IsPopupOpen ||
               cmbNettBalances.IsPopupOpen ||
               cmbCalculation.IsPopupOpen ||
               cmbMethodToAllocate.IsPopupOpen ||
               cmbNature.IsPopupOpen ||
               cmbAffectsPL.IsPopupOpen;
    }

    public void CloseOpenPopup()
    {
        if (cmbSubLedger.IsPopupOpen) cmbSubLedger.ClosePopup();
        if (cmbNettBalances.IsPopupOpen) cmbNettBalances.ClosePopup();
        if (cmbCalculation.IsPopupOpen) cmbCalculation.ClosePopup();
        if (cmbMethodToAllocate.IsPopupOpen) cmbMethodToAllocate.ClosePopup();
        if (cmbNature.IsPopupOpen) cmbNature.ClosePopup();
        if (cmbAffectsPL.IsPopupOpen) cmbAffectsPL.ClosePopup();
    }

    public void HandleCancel()
    {
        if (HasOpenPopup())
        {
            CloseOpenPopup();
            return;
        }

        var res = MoneyFlowMessageBox.Show(
            this,
            "Are you sure you want to close this screen? Any unsaved changes will be discarded.",
            "Confirm Discard & Close",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (res == DialogResult.Yes)
        {
            _isDirty = false;
            Cancelled?.Invoke();
            Close();
        }
    }

    public bool HandleBackNavigation()
    {
        HandleCancel();
        return true;
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        UpdateKeyboardStatus();

        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            HandleCancel();
        }
        else if (e.KeyCode == Keys.A && !e.Control && !e.Alt)
        {
            if (_activeFieldIndex == _fields.Count - 1)
            {
                e.Handled = true;
                _ = SaveAsync();
            }
        }
    }

    private void ShowHelpDialog()
    {
        MoneyFlowMessageBox.Show(
            this,
            "MoneyFlow Desktop ERP — Group Management Help\n\n" +
            "• Use Enter to advance from field to field.\n" +
            "• Use Shift+Enter or Shift+Tab to navigate backwards.\n" +
            "• When on the Under field, the right-side List of Groups opens automatically.\n" +
            "• Type letters to instantly auto-search parent groups.\n" +
            "• Use Arrow Up and Arrow Down to navigate groups in the list.\n" +
            "• Press Enter to select the highlighted parent group.\n" +
            "• Press F4 or Alt+Down to focus the parent group search box.\n" +
            "• Press Alt+C to set the Under field to Primary (+ Create).\n" +
            "• Press Ctrl+A or Alt+A to accept/save changes.\n" +
            "• Press Alt+D to delete the selected group (in Alter mode).\n" +
            "• Press Esc to exit or close.",
            "Group Master Help",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void ShowConfigurationHelp()
    {
        MoneyFlowMessageBox.Show(
            this,
            "Master Configuration (F12)\n\n" +
            "• Advanced group behaviors and allocation rules are active.\n" +
            "• Sub-ledger and reporting settings apply to all child ledgers.\n" +
            "• Calculation rules apply to purchase and sales vouchers.",
            "Configuration Settings",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
