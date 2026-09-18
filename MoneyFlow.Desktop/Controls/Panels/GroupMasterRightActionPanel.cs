using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Controls.Panels;

/// <summary>
/// Dedicated vertical action panel docked to the right of the Group Master grid.
/// Matches Reference Image 1 (compact full-height light-blue #E0EDFD accounting sidebar with subtle #CBD5E1 left separator)
/// and Reference Image 2 (two-line action buttons with GDI+ vector icons and action-specific colors):
/// CREATE (Alt+C), ALTER (Alt+A), DELETE (Del), VIEW (Alt+V), FIND (Alt+F), and CLOSE (Esc).
/// </summary>
public class GroupMasterRightActionPanel : Panel
{
    public event EventHandler? CreateClicked;
    public event EventHandler? AlterClicked;
    public event EventHandler? DeleteClicked;
    public event EventHandler? SearchClicked;
    public event EventHandler? FindClicked;
    public event EventHandler? CloseClicked;

    private readonly Label lblHeader;
    private readonly ActionNavButton btnCreate;
    private readonly ActionNavButton btnAlter;
    private readonly ActionNavButton btnDelete;
    private readonly ActionNavButton btnSearch;
    private readonly ActionNavButton btnClose;

    public Button ButtonCreate => btnCreate;
    public Button ButtonAlter => btnAlter;
    public Button ButtonDelete => btnDelete;
    public Button ButtonSearch => btnSearch;
    public Button ButtonFind => btnSearch;
    public Button ButtonClose => btnClose;

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

    public void SetRowSelectedActionsEnabled(bool enabled)
    {
        btnAlter.Enabled = enabled;
        btnDelete.Enabled = enabled;
    }

    public GroupMasterRightActionPanel()
    {
        Dock = DockStyle.Right;
        Width = 200; // 180px–210px compact sidebar width
        BackColor = Color.FromArgb(224, 237, 253); // Light Blue #E0EDFD (Reference Image 1)
        Padding = new Padding(10, 12, 10, 12);
        DoubleBuffered = true;

        // Action Panel Header: "ACTIONS" (15px–17px, Weight 700, #1B365D)
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

        // Corporate Navy Color from Image 2: #1B365D (RGB: 27, 54, 93)
        var btnNavy = Color.FromArgb(27, 54, 93);
        var btnNavyHover = Color.FromArgb(36, 71, 107);

        // 1. CREATE Button: Corporate Navy #1B365D (Image 2), Vector Icon: Plus
        btnCreate = new ActionNavButton(
            "CREATE",
            "Alt+C",
            "CREATE",
            btnNavy,
            btnNavyHover,
            Color.White)
        {
            TabIndex = 101,
            AccessibleName = "Create Group"
        };
        btnCreate.Click += (s, e) => CreateClicked?.Invoke(this, EventArgs.Empty);

        // 2. ALTER Button: Corporate Navy #1B365D (Image 2), Vector Icon: Pencil
        btnAlter = new ActionNavButton(
            "ALTER",
            "Alt+A",
            "ALTER",
            btnNavy,
            btnNavyHover,
            Color.White)
        {
            TabIndex = 102,
            AccessibleName = "Alter Group"
        };
        btnAlter.Click += (s, e) => AlterClicked?.Invoke(this, EventArgs.Empty);

        // 3. DELETE Button: Corporate Navy #1B365D (Image 2), Vector Icon: Trash
        btnDelete = new ActionNavButton(
            "DELETE",
            "Del",
            "DELETE",
            btnNavy,
            btnNavyHover,
            Color.White)
        {
            TabIndex = 103,
            AccessibleName = "Delete Group"
        };
        btnDelete.Click += (s, e) => DeleteClicked?.Invoke(this, EventArgs.Empty);

        // 4. SEARCH Button: Corporate Navy #1B365D (Image 2), Vector Icon: Magnifier
        btnSearch = new ActionNavButton(
            "SEARCH",
            "Alt+S",
            "SEARCH",
            btnNavy,
            btnNavyHover,
            Color.White)
        {
            TabIndex = 104,
            AccessibleName = "Search Group"
        };
        btnSearch.Click += (s, e) => TriggerSearch();

        // 5. CLOSE Button: Corporate Navy #1B365D (Image 2), Vector Icon: X
        btnClose = new ActionNavButton(
            "CLOSE",
            "Esc",
            "CLOSE",
            btnNavy,
            btnNavyHover,
            Color.White)
        {
            TabIndex = 105,
            AccessibleName = "Close Group Master"
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
        const int btnHeight = 58; // Two-line comfortable button height (56px–64px)
        const int spacing = 8;
        const int closeGap = 20;

        int currentY = Padding.Top + 26; // Leave room for compact "ACTIONS" header
        int btnX = Padding.Left;

        btnCreate.SetBounds(btnX, currentY, btnWidth, btnHeight);
        currentY += btnHeight + spacing;

        btnAlter.SetBounds(btnX, currentY, btnWidth, btnHeight);
        currentY += btnHeight + spacing;

        btnDelete.SetBounds(btnX, currentY, btnWidth, btnHeight);
        currentY += btnHeight + spacing;

        btnSearch.SetBounds(btnX, currentY, btnWidth, btnHeight);
        currentY += btnHeight + closeGap;

        // Position CLOSE button toward bottom or after gap if height is constrained (Section 29)
        int closeY = Math.Max(currentY, Height - Padding.Bottom - btnHeight);
        btnClose.SetBounds(btnX, closeY, btnWidth, btnHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;

        // Draw 1px subtle left separator border #CBD5E1 (Section 4)
        using var pen = new Pen(Color.FromArgb(203, 213, 225), 1);
        g.DrawLine(pen, 0, 0, 0, Height);
    }

    public static GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>
/// Professional two-line action navigation button displaying GDI+ vector icon, action title, and keyboard shortcut.
/// Strictly enforces two-line typography:
/// Line 1: Action Name (14px–15px, 700 bold, centered)
/// Line 2: Shortcut Key (11px–12px, regular/medium, centered)
/// Crisp GDI+ vector icons eliminate broken square box characters.
/// Features subtle shadow, 2px #2563EB focus border, and smooth hover state.
/// </summary>
public class ActionNavButton : Button
{
    private bool _isHovered;
    private bool _isPressed;
    private readonly Color _baseBackColor;
    private readonly Color _hoverBackColor;
    private readonly Color _borderColor;
    private readonly string _actionTitle;
    private readonly string _shortcutHint;
    private readonly string _actionType;

    public string ActionTitle => _actionTitle;
    public string ShortcutHint => _shortcutHint;
    public string ActionType => _actionType;

    public ActionNavButton(
        string title,
        string shortcut,
        string actionType,
        Color backColor,
        Color hoverColor,
        Color foreColor,
        Color? borderColor = null)
    {
        _actionTitle = title;
        _shortcutHint = shortcut;
        _actionType = actionType;
        _baseBackColor = backColor;
        _hoverBackColor = hoverColor;
        _borderColor = borderColor ?? Color.Transparent;

        Text = $"{title}\n{shortcut}";
        ForeColor = foreColor;
        BackColor = backColor;
        Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        DoubleBuffered = true;

        MouseEnter += (s, e) => { _isHovered = true; Invalidate(); };
        MouseLeave += (s, e) => { _isHovered = false; Invalidate(); };
        MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) { _isPressed = true; Invalidate(); } };
        MouseUp += (s, e) => { _isPressed = false; Invalidate(); };
        GotFocus += (s, e) => Invalidate();
        LostFocus += (s, e) => Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        // Clear button area to match panel background
        using (var parentBrush = new SolidBrush(Parent?.BackColor ?? Color.FromArgb(224, 237, 253)))
        {
            g.FillRectangle(parentBrush, ClientRectangle);
        }

        // Subtle bottom shadow (0 2px 4px rgba(15, 23, 42, 0.08))
        var shadowBounds = new Rectangle(1, 2, Width - 2, Height - 2);
        using (var shadowPath = GroupMasterRightActionPanel.CreateRoundedRectanglePath(shadowBounds, 5))
        using (var shadowBrush = new SolidBrush(Color.FromArgb(18, 15, 23, 42)))
        {
            g.FillPath(shadowBrush, shadowPath);
        }

        var bounds = new Rectangle(0, 0, Width - 1, Height - 2);
        const int radius = 5; // Rounded corners around 4px–6px (Section 27)

        Color currentBg;
        Color effectiveForeColor;
        Color effectiveShortcutColor;
        Color effectiveIconColor;

        if (!Enabled)
        {
            currentBg = Color.FromArgb(226, 232, 240); // Soft disabled gray-blue #E2E8F0
            effectiveForeColor = Color.FromArgb(148, 163, 184); // Muted slate #94A3B8
            effectiveShortcutColor = Color.FromArgb(148, 163, 184);
            effectiveIconColor = Color.FromArgb(148, 163, 184);
        }
        else
        {
            currentBg = _isPressed
                ? ControlPaint.Dark(_baseBackColor, 0.05f)
                : (_isHovered ? _hoverBackColor : _baseBackColor);
            effectiveForeColor = ForeColor;
            effectiveShortcutColor = ForeColor == Color.White
                ? Color.FromArgb(224, 237, 253) // #E0EDFD on dark buttons
                : Color.FromArgb(100, 116, 139); // #64748B on light buttons
            effectiveIconColor = ForeColor;
        }

        using (var path = GroupMasterRightActionPanel.CreateRoundedRectanglePath(bounds, radius))
        {
            using (var bgBrush = new SolidBrush(currentBg))
            {
                g.FillPath(bgBrush, path);
            }

            // Keyboard focus border or standard border
            if (Focused && Enabled)
            {
                using var focusPen = new Pen(Color.FromArgb(37, 99, 235), 2); // #2563EB focus blue
                g.DrawPath(focusPen, path);
            }
            else if (_borderColor != Color.Transparent || !Enabled)
            {
                Color borderC = !Enabled ? Color.FromArgb(203, 213, 225) : _borderColor;
                using var borderPen = new Pen(borderC, 1);
                g.DrawPath(borderPen, path);
            }
        }

        // Crisp GDI+ Vector Icon on the left (never a broken square box!)
        var iconRect = new Rectangle(14, (Height - 2 - 16) / 2, 16, 16);
        DrawVectorIcon(g, _actionType, iconRect, effectiveIconColor);

        // Two-Line Centered Typography
        using var foreBrush = new SolidBrush(effectiveForeColor);
        using var titleFont = new Font("Segoe UI", 10F, FontStyle.Bold);
        using var shortcutFont = new Font("Segoe UI", 8.5F, FontStyle.Regular);

        var titleSize = g.MeasureString(_actionTitle, titleFont);
        var shortcutSize = g.MeasureString(_shortcutHint, shortcutFont);

        float textBlockHeight = titleSize.Height + shortcutSize.Height - 3;
        float startY = (Height - 2 - textBlockHeight) / 2f;

        // Centered across the button
        float titleX = (Width - titleSize.Width) / 2f;
        float shortcutX = (Width - shortcutSize.Width) / 2f;

        // Line 1: Action Name (14px–15px Bold)
        g.DrawString(_actionTitle, titleFont, foreBrush, titleX, startY);

        // Line 2: Shortcut Key (11px–12px Regular/Medium)
        using var shortcutBrush = new SolidBrush(effectiveShortcutColor);
        g.DrawString(_shortcutHint, shortcutFont, shortcutBrush, shortcutX, startY + titleSize.Height - 3);
    }

    private static void DrawVectorIcon(Graphics g, string actionType, Rectangle rect, Color color)
    {
        using var pen = new Pen(color, 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        float cx = rect.X + rect.Width / 2f;
        float cy = rect.Y + rect.Height / 2f;

        switch (actionType.ToUpperInvariant())
        {
            case "CREATE":
                // Plus (+) Icon
                g.DrawLine(pen, cx - 5.5f, cy, cx + 5.5f, cy);
                g.DrawLine(pen, cx, cy - 5.5f, cx, cy + 5.5f);
                break;

            case "ALTER":
                // Pencil (✎) Icon
                using (var pPen = new Pen(color, 1.8f))
                {
                    g.DrawLine(pPen, cx - 5, cy + 5, cx + 3, cy - 3);
                    g.DrawLine(pPen, cx - 3, cy + 6, cx + 5, cy - 2);
                    g.DrawLine(pPen, cx + 3, cy - 3, cx + 5, cy - 2);
                    g.DrawLine(pPen, cx - 5, cy + 5, cx - 7, cy + 7);
                    g.DrawLine(pPen, cx - 3, cy + 6, cx - 7, cy + 7);
                }
                break;

            case "DELETE":
                // Trash (🗑) Icon
                g.DrawLine(pen, cx - 6, cy - 5, cx + 6, cy - 5);
                g.DrawLine(pen, cx - 2, cy - 7, cx + 2, cy - 7);
                using (var tPen = new Pen(color, 1.6f))
                {
                    g.DrawLine(tPen, cx - 5, cy - 4, cx - 4, cy + 6);
                    g.DrawLine(tPen, cx + 5, cy - 4, cx + 4, cy + 6);
                    g.DrawLine(tPen, cx - 4, cy + 6, cx + 4, cy + 6);
                    g.DrawLine(tPen, cx - 1.5f, cy - 2, cx - 1.5f, cy + 4);
                    g.DrawLine(tPen, cx + 1.5f, cy - 2, cx + 1.5f, cy + 4);
                }
                break;

            case "SEARCH":
            case "FIND":
            case "VIEW":
                // Magnifier / Search (🔍) Icon
                using (var vPen = new Pen(color, 1.8f))
                {
                    g.DrawEllipse(vPen, cx - 6, cy - 6, 9, 9);
                    g.DrawLine(pen, cx + 2, cy + 2, cx + 6, cy + 6);
                }
                break;

            case "CLOSE":
                // Close / X (✕) Icon
                g.DrawLine(pen, cx - 5, cy - 5, cx + 5, cy + 5);
                g.DrawLine(pen, cx + 5, cy - 5, cx - 5, cy + 5);
                break;
        }
    }
}
