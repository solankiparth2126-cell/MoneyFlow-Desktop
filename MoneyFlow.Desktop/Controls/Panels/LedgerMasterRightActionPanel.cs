using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Controls.Panels;

/// <summary>
/// Dedicated vertical action panel docked to the right of the Ledger Master grid.
/// Matches Group Master styling (compact full-height light-blue #E0EDFD accounting sidebar with subtle #CBD5E1 left separator)
/// and two-line action buttons with GDI+ vector icons and Corporate Navy #1B365D colors:
/// CREATE (Alt+C), ALTER (Alt+A), DELETE (Del), SEARCH (Alt+S), and CLOSE (Esc).
/// </summary>
public class LedgerMasterRightActionPanel : Panel
{
    public event EventHandler? CreateClicked;
    public event EventHandler? AlterClicked;
    public event EventHandler? DeleteClicked;
    public event EventHandler? SearchClicked;
    public event EventHandler? FindClicked;
    public event EventHandler? CloseClicked;
    public event EventHandler? CollapseToggled;

    private readonly Label lblHeader;
    private readonly ActionToggleButton btnToggle;
    private readonly ToolTip toolTip;
    private readonly ActionNavButton btnCreate;
    private readonly ActionNavButton btnAlter;
    private readonly ActionNavButton btnDelete;
    private readonly ActionNavButton btnSearch;
    private readonly ActionNavButton btnClose;

    private bool _isCollapsed = true;
    private int _expandedWidth = 200;

    public Button ButtonCreate => btnCreate;
    public Button ButtonAlter => btnAlter;
    public Button ButtonDelete => btnDelete;
    public Button ButtonSearch => btnSearch;
    public Button ButtonFind => btnSearch;
    public Button ButtonClose => btnClose;
    public Button ButtonToggle => btnToggle;
    public Button ToggleButton => btnToggle;
    public ActionToggleButton ActionToggleButton => btnToggle;

    public bool IsCollapsed
    {
        get => _isCollapsed;
        set => SetCollapsed(value);
    }

    public int ExpandedWidth
    {
        get => _expandedWidth;
        set
        {
            _expandedWidth = value;
            if (!_isCollapsed) Width = _expandedWidth;
        }
    }

    public void TriggerCreate() => CreateClicked?.Invoke(this, EventArgs.Empty);
    public void TriggerAlter()
    {
        if (btnAlter.Enabled) AlterClicked?.Invoke(this, EventArgs.Empty);
    }

    public void TriggerDelete()
    {
        if (btnDelete.Enabled) DeleteClicked?.Invoke(this, EventArgs.Empty);
    }

    public void TriggerSearch()
    {
        SearchClicked?.Invoke(this, EventArgs.Empty);
        FindClicked?.Invoke(this, EventArgs.Empty);
    }

    public void TriggerFind() => TriggerSearch();
    public void TriggerClose() => CloseClicked?.Invoke(this, EventArgs.Empty);

    public void ToggleCollapse() => SetCollapsed(!_isCollapsed);

    public void SetCollapsed(bool collapse)
    {
        _isCollapsed = collapse;
        Visible = !collapse;

        btnToggle.IsCollapsed = _isCollapsed;
        toolTip.SetToolTip(btnToggle, _isCollapsed ? "Expand Actions" : "Collapse Actions");
        btnToggle.AccessibleName = _isCollapsed ? "Expand Actions" : "Collapse Actions";

        CollapseToggled?.Invoke(this, EventArgs.Empty);
    }

    public void SetRowSelectedActionsEnabled(bool enabled)
    {
        btnAlter.Enabled = enabled;
        btnDelete.Enabled = enabled;
    }

    public LedgerMasterRightActionPanel()
    {
        Dock = DockStyle.Right;
        Width = _expandedWidth;
        BackColor = Color.FromArgb(224, 237, 253); // Light Blue #E0EDFD
        Padding = new Padding(10, 12, 10, 12);
        DoubleBuffered = true;

        toolTip = new ToolTip
        {
            ShowAlways = true,
            InitialDelay = 200,
            ReshowDelay = 100,
            AutoPopDelay = 3000
        };

        // Action Panel Header: "ACTIONS" (#1B365D)
        lblHeader = new Label
        {
            Text = "ACTIONS",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(27, 54, 93), // Primary Navy #1B365D
            Location = new Point(10, 10),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        Controls.Add(lblHeader);

        // Modern, compact toggle button (Collapse/Expand Actions)
        btnToggle = new ActionToggleButton
        {
            IsCollapsed = true,
            AccessibleName = "Expand Actions"
        };
        toolTip.SetToolTip(btnToggle, "Expand Actions");

        var btnNavy = Color.FromArgb(27, 54, 93);
        var btnNavyHover = Color.FromArgb(36, 71, 107);

        // 1. CREATE Button
        btnCreate = new ActionNavButton(
            "CREATE",
            "Alt+C",
            "CREATE",
            btnNavy,
            btnNavyHover,
            Color.White)
        {
            TabIndex = 101,
            AccessibleName = "Create Ledger"
        };
        btnCreate.Click += (s, e) => CreateClicked?.Invoke(this, EventArgs.Empty);

        // 2. ALTER Button
        btnAlter = new ActionNavButton(
            "ALTER",
            "Alt+A",
            "ALTER",
            btnNavy,
            btnNavyHover,
            Color.White)
        {
            TabIndex = 102,
            AccessibleName = "Alter Ledger"
        };
        btnAlter.Click += (s, e) => AlterClicked?.Invoke(this, EventArgs.Empty);

        // 3. DELETE Button
        btnDelete = new ActionNavButton(
            "DELETE",
            "Del",
            "DELETE",
            btnNavy,
            btnNavyHover,
            Color.White)
        {
            TabIndex = 103,
            AccessibleName = "Delete Ledger"
        };
        btnDelete.Click += (s, e) => DeleteClicked?.Invoke(this, EventArgs.Empty);

        // 4. SEARCH Button
        btnSearch = new ActionNavButton(
            "SEARCH",
            "Alt+S",
            "SEARCH",
            btnNavy,
            btnNavyHover,
            Color.White)
        {
            TabIndex = 104,
            AccessibleName = "Search Ledger"
        };
        btnSearch.Click += (s, e) => TriggerSearch();

        // 5. CLOSE Button
        btnClose = new ActionNavButton(
            "CLOSE",
            "Esc",
            "CLOSE",
            btnNavy,
            btnNavyHover,
            Color.White)
        {
            TabIndex = 105,
            AccessibleName = "Close Ledger Master"
        };
        btnClose.Click += (s, e) => CloseClicked?.Invoke(this, EventArgs.Empty);

        Controls.Add(btnCreate);
        Controls.Add(btnAlter);
        Controls.Add(btnDelete);
        Controls.Add(btnSearch);
        Controls.Add(btnClose);

        LayoutButtons();
        Resize += (s, e) => LayoutButtons();
    }

    private void LayoutButtons()
    {
        int btnWidth = Math.Max(120, Width - Padding.Horizontal);
        const int btnHeight = 58;
        const int spacing = 8;
        const int closeGap = 20;

        int currentY = Padding.Top + 26;
        int btnX = Padding.Left;

        btnCreate.SetBounds(btnX, currentY, btnWidth, btnHeight);
        currentY += btnHeight + spacing;

        btnAlter.SetBounds(btnX, currentY, btnWidth, btnHeight);
        currentY += btnHeight + spacing;

        btnDelete.SetBounds(btnX, currentY, btnWidth, btnHeight);
        currentY += btnHeight + spacing;

        btnSearch.SetBounds(btnX, currentY, btnWidth, btnHeight);
        currentY += btnHeight + closeGap;

        int closeY = Math.Max(currentY, Height - Padding.Bottom - btnHeight);
        btnClose.SetBounds(btnX, closeY, btnWidth, btnHeight);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (_isCollapsed && e.Button == MouseButtons.Left)
        {
            ToggleCollapse();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;

        using var pen = new Pen(Color.FromArgb(203, 213, 225), 1);
        g.DrawLine(pen, 0, 0, 0, Height);
    }
}

