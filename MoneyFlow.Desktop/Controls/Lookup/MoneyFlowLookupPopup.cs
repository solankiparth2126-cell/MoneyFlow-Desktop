using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Controls.Lookup;

/// <summary>
/// Universal Tally-style lookup overlay popup.
/// Adapts dynamically to any data source (Groups, Ledgers, Customers, etc.)
/// with keyboard-first navigation, instant search, and hierarchical expand/collapse.
/// </summary>
public class MoneyFlowLookupPopup : Form
{
    private readonly LookupConfig _config;
    private readonly ILookupProvider _provider;
    private readonly LookupKeyboardController _keyboardController = new();

    // UI elements
    private Label _lblTitle = null!;
    private Label _lblRecordCount = null!;
    private Button? _btnNew;
    private LinkLabel? _lnkShowMore;
    private TextBox _txtSearch = null!;
    private ListBox _lstItems = null!;
    private Label _lblFilterStatus = null!;
    private Label _lblItemCounter = null!;
    private Panel _pnlFooter = null!;

    // State
    private IReadOnlyList<LookupItem> _allItems = new List<LookupItem>();
    private List<LookupItem> _visibleItems = new();
    private CancellationTokenSource? _searchCts;
    private bool _isSelecting;

    // Events
    public event Action<LookupItem>? ItemSelected;
    public event Action? CreateRequested;
    public event Action? ShowMoreRequested;
    public event Action? Cancelled;

    public MoneyFlowLookupPopup(ILookupProvider provider, LookupConfig config)
    {
        _provider = provider;
        _config = config;

        InitializePopup();
        _provider.DataChanged += OnProviderDataChanged;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_DROPSHADOW = 0x00020000;
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    private void InitializePopup()
    {
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = false;
        DoubleBuffered = true;
        KeyPreview = true;
        BackColor = ExecLedgerTheme.WorkSurface;
        Size = new Size(_config.DefaultWidth, _config.DefaultHeight);

        // Outer Border
        Paint += (s, e) =>
        {
            using var pen = new Pen(ExecLedgerTheme.PrimaryBorder, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };

        // Layout Containers
        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(1),
            Margin = new Padding(0),
            BackColor = Color.White
        };

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); // 0: Title Bar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // 1: Sub Bar (Counts & New)
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); // 2: Find Search Bar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); // 3: Columns Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // 4: Items List
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); // 5: Status Filter & Counter
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // 6: Keyboard Shortcut Footer

        // ── 0. Title Bar ──
        var pnlTitle = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ExecLedgerTheme.PrimaryNavy,
            Margin = new Padding(0)
        };

        _lblTitle = new Label
        {
            Text = _config.Title,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.25F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(14, 7),
            BackColor = Color.Transparent
        };
        pnlTitle.Controls.Add(_lblTitle);

        var lblF4 = new Label
        {
            Text = "F4",
            ForeColor = Color.FromArgb(203, 213, 225),
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 56, 8),
            BackColor = Color.Transparent
        };
        pnlTitle.Controls.Add(lblF4);

        var btnClose = new Label
        {
            Text = "✕",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Size = new Size(20, 20),
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 28, 6),
            Cursor = Cursors.Hand,
            BackColor = Color.Transparent
        };
        btnClose.Click += (s, e) => ClosePopup(isCancelled: true);
        pnlTitle.Controls.Add(btnClose);

        // ── 1. Sub Bar ──
        var pnlSub = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252),
            Margin = new Padding(0)
        };

        _lblRecordCount = new Label
        {
            Text = "0 Records",
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(14, 6),
            BackColor = Color.Transparent
        };
        pnlSub.Controls.Add(_lblRecordCount);

        int rightX = Width - 14;

        if (_config.AllowShowMore)
        {
            _lnkShowMore = new LinkLabel
            {
                Text = "Show More (Alt+M)",
                Font = new Font("Segoe UI", 8F),
                LinkColor = ExecLedgerTheme.SystemFocusBlue,
                AutoSize = true,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(rightX - 120, 6)
            };
            _lnkShowMore.LinkClicked += (s, e) => ShowMoreRequested?.Invoke();
            pnlSub.Controls.Add(_lnkShowMore);
            rightX -= 130;
        }

        if (_config.AllowCreate)
        {
            _btnNew = new Button
            {
                Text = $"{_config.CreateButtonText} (Alt+C)",
                Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
                ForeColor = ExecLedgerTheme.PrimaryNavy,
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Size = new Size(110, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(rightX - 114, 3),
                Cursor = Cursors.Hand
            };
            _btnNew.FlatAppearance.BorderColor = ExecLedgerTheme.PrimaryBorder;
            _btnNew.FlatAppearance.BorderSize = 1;
            _btnNew.Click += (s, e) => CreateRequested?.Invoke();
            pnlSub.Controls.Add(_btnNew);
        }

        // ── 2. Find Search Bar ──
        var pnlFind = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(14, 5, 14, 5),
            Margin = new Padding(0)
        };
        pnlFind.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(241, 245, 249), 1);
            e.Graphics.DrawLine(pen, 0, pnlFind.Height - 1, pnlFind.Width, pnlFind.Height - 1);
        };

        var lblFindIcon = new Label
        {
            Text = "Find:",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            AutoSize = true,
            Location = new Point(14, 7),
            BackColor = Color.Transparent
        };
        pnlFind.Controls.Add(lblFindIcon);

        _txtSearch = new TextBox
        {
            Location = new Point(56, 5),
            Width = Width - 70,
            Font = new Font("Segoe UI", 9.25F, FontStyle.Regular),
            ForeColor = ExecLedgerTheme.PrimaryText,
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _txtSearch.TextChanged += async (s, e) => await OnSearchTextChangedAsync();
        _txtSearch.KeyDown += OnSearchKeyDown;
        pnlFind.Controls.Add(_txtSearch);

        // ── 3. Column Headers ──
        var pnlCols = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(241, 245, 249),
            Margin = new Padding(0)
        };
        pnlCols.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            using var font = new Font("Segoe UI", 8.25F, FontStyle.Bold);
            using var brush = new SolidBrush(Color.FromArgb(71, 85, 105));
            g.DrawString(_config.DescriptionColumnHeader, font, brush, 14, 4);

            if (_config.ShowCodeColumn)
            {
                int scrollbarWidth = SystemInformation.VerticalScrollBarWidth;
                int rightMargin = scrollbarWidth + 12;
                int codeHeaderWidth = TextRenderer.MeasureText(_config.CodeColumnHeader, font).Width;
                int codeX = pnlCols.Width - rightMargin - codeHeaderWidth;
                g.DrawString(_config.CodeColumnHeader, font, brush, codeX, 4);
            }

            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            g.DrawLine(pen, 0, pnlCols.Height - 1, pnlCols.Width, pnlCols.Height - 1);
        };

        // ── 4. Items List ──
        _lstItems = new ListBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 28,
            IntegralHeight = false,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            Margin = new Padding(0)
        };
        _lstItems.DrawItem += OnDrawItem;
        _lstItems.DoubleClick += (s, e) => SelectCurrentItem();
        _lstItems.SelectedIndexChanged += (s, e) =>
        {
            _keyboardController.SelectedIndex = _lstItems.SelectedIndex;
            UpdateStatusLabels();
        };
        _lstItems.KeyDown += OnListKeyDown;

        // ── 5. Status Bar ──
        var pnlStatus = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 250, 252),
            Margin = new Padding(0)
        };
        pnlStatus.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlStatus.Width, 0);
        };

        _lblFilterStatus = new Label
        {
            Text = "Filter: [All]",
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            Location = new Point(14, 4),
            BackColor = Color.Transparent
        };
        pnlStatus.Controls.Add(_lblFilterStatus);

        _lblItemCounter = new Label
        {
            Text = "Item 0 of 0",
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 100, 4),
            BackColor = Color.Transparent
        };
        pnlStatus.Controls.Add(_lblItemCounter);

        // ── 6. Keyboard Shortcut Footer ──
        _pnlFooter = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ExecLedgerTheme.PrimaryNavy,
            Margin = new Padding(0)
        };
        _pnlFooter.Paint += (s, e) =>
        {
            var g = e.Graphics;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var items = new (string Key, string Action)[]
            {
                ("↑↓", "Navigate"),
                ("ENTER", "Select"),
                ("ESC", "Close"),
                ("ALT+C", "Create"),
                ("ALT+M", "More")
            };

            using var fontKey = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            using var fontAction = new Font("Segoe UI", 7.5F, FontStyle.Regular);
            using var brushKey = new SolidBrush(Color.FromArgb(147, 197, 253));
            using var brushAction = new SolidBrush(Color.FromArgb(226, 232, 240));

            int y = (_pnlFooter.Height - 16) / 2;
            int x = 14;

            foreach (var item in items)
            {
                g.DrawString(item.Key, fontKey, brushKey, x, y);
                int kw = TextRenderer.MeasureText(item.Key, fontKey).Width;

                g.DrawString(" " + item.Action, fontAction, brushAction, x + kw - 3, y);
                int aw = TextRenderer.MeasureText(" " + item.Action, fontAction).Width;

                x += kw + aw + 12;
            }
        };

        // Assemble Layout
        mainLayout.Controls.Add(pnlTitle, 0, 0);
        mainLayout.Controls.Add(pnlSub, 0, 1);
        mainLayout.Controls.Add(pnlFind, 0, 2);
        mainLayout.Controls.Add(pnlCols, 0, 3);
        mainLayout.Controls.Add(_lstItems, 0, 4);
        mainLayout.Controls.Add(pnlStatus, 0, 5);
        mainLayout.Controls.Add(_pnlFooter, 0, 6);

        Controls.Add(mainLayout);

        // Window Shortcuts
        KeyDown += OnPopupKeyDown;
    }

    public async Task LoadAndShowAsync(Control anchorControl, string? initialSearch = null)
    {
        if (anchorControl == null || anchorControl.IsDisposed) return;
        var parentForm = anchorControl.FindForm();
        if (parentForm == null || parentForm.IsDisposed) return;

        try
        {
            await ReloadDataAsync(initialSearch);
            if (anchorControl.IsDisposed || parentForm.IsDisposed) return;
            PositionNear(anchorControl);

            if (!Visible)
            {
                if (parentForm.Visible)
                {
                    Show(parentForm);
                }
                else
                {
                    Show();
                }
            }

            _txtSearch.Focus();
            if (!string.IsNullOrEmpty(initialSearch))
            {
                _txtSearch.Text = initialSearch;
                _txtSearch.SelectionStart = _txtSearch.Text.Length;
            }
        }
        catch
        {
        }
    }

    public void PositionNear(Control anchorControl)
    {
        var parentForm = anchorControl.FindForm();
        var screen = Screen.FromControl(anchorControl);
        var workArea = screen.WorkingArea;

        // Viewport: bounded by parent application window if present, otherwise screen working area
        Rectangle viewport = workArea;
        if (parentForm?.Owner != null && parentForm.Owner.Visible)
        {
            var appBounds = parentForm.Owner.RectangleToScreen(parentForm.Owner.ClientRectangle);
            var inter = Rectangle.Intersect(workArea, appBounds);
            if (!inter.IsEmpty && inter.Width >= 400 && inter.Height >= 300)
            {
                viewport = inter;
            }
        }

        int popW = Math.Min(_config.DefaultWidth, viewport.Width - 24);
        int targetH = Math.Min(_config.DefaultHeight, viewport.Height - 40);

        var anchorScreen = anchorControl.RectangleToScreen(anchorControl.ClientRectangle);
        if (anchorControl.Parent != null && anchorControl.Parent.Height <= 50)
        {
            var pScreen = anchorControl.Parent.RectangleToScreen(anchorControl.Parent.ClientRectangle);
            if (pScreen.Bottom >= anchorScreen.Bottom)
            {
                anchorScreen = pScreen;
            }
        }

        // Calculate available vertical space
        int spaceBelow = viewport.Bottom - anchorScreen.Bottom - 8;
        int spaceAbove = anchorScreen.Top - viewport.Top - 8;

        int top;
        int popH;

        if (spaceBelow >= 140 || spaceBelow >= spaceAbove)
        {
            // Position directly below anchor
            top = anchorScreen.Bottom + 2;
            popH = Math.Min(targetH, Math.Max(180, spaceBelow));
        }
        else
        {
            // Position directly above anchor
            popH = Math.Min(targetH, Math.Max(180, spaceAbove));
            top = anchorScreen.Top - popH - 2;
        }

        // Horizontal positioning: align with anchor's right, staying inside parent form & viewport
        int left = anchorScreen.Right - popW;

        if (parentForm != null)
        {
            var formScreen = parentForm.RectangleToScreen(parentForm.ClientRectangle);
            if (popW <= formScreen.Width - 24)
            {
                // Align within parent form boundaries
                if (left + popW > formScreen.Right - 12)
                    left = formScreen.Right - 12 - popW;
                if (left < formScreen.Left + 12)
                    left = formScreen.Left + 12;
            }
        }

        // Final clamp to viewport
        if (left + popW > viewport.Right - 8)
            left = viewport.Right - 8 - popW;
        if (left < viewport.Left + 8)
            left = viewport.Left + 8;

        if (top + popH > viewport.Bottom - 8)
            top = viewport.Bottom - 8 - popH;
        if (top < viewport.Top + 8)
            top = viewport.Top + 8;

        Size = new Size(popW, popH);
        Location = new Point(left, top);
    }

    public async Task ReloadDataAsync(string? searchQuery = null)
    {
        _allItems = await _provider.GetItemsAsync(null);

        var list = new List<LookupItem>();
        if (_config.AllowClear)
        {
            list.Add(new LookupItem
            {
                Id = null,
                Name = _config.ClearItemText,
                IsSentinel = true
            });
        }

        list.AddRange(_allItems);
        ApplyFilter(searchQuery ?? _txtSearch.Text);
    }

    private void ApplyFilter(string? query)
    {
        var filtered = new List<LookupItem>();
        if (_config.AllowClear)
        {
            filtered.Add(new LookupItem
            {
                Id = null,
                Name = _config.ClearItemText,
                IsSentinel = true
            });
        }

        foreach (var item in _allItems)
        {
            if (item.Matches(query))
            {
                filtered.Add(item);
            }
        }

        _visibleItems = filtered;
        _lstItems.BeginUpdate();
        _lstItems.Items.Clear();
        foreach (var item in _visibleItems)
        {
            _lstItems.Items.Add(item);
        }
        _lstItems.EndUpdate();

        _keyboardController.ResetSelection();
        _keyboardController.ClampSelection(_visibleItems.Count);

        if (_visibleItems.Count > 0 && _keyboardController.SelectedIndex >= 0)
        {
            _lstItems.SelectedIndex = _keyboardController.SelectedIndex;
        }

        _lblRecordCount.Text = $"{_allItems.Count} Records";
        UpdateStatusLabels();
    }

    private void UpdateStatusLabels()
    {
        string q = _txtSearch.Text.Trim();
        _lblFilterStatus.Text = string.IsNullOrEmpty(q) ? "Filter: [All]" : $"Filter: [{q}]";

        int total = _visibleItems.Count;
        int current = _lstItems.SelectedIndex >= 0 ? _lstItems.SelectedIndex + 1 : (total > 0 ? 1 : 0);
        _lblItemCounter.Text = $"Item {current} of {total}";
    }

    private async Task OnSearchTextChangedAsync()
    {
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            await Task.Delay(50, token);
            if (!token.IsCancellationRequested)
            {
                ApplyFilter(_txtSearch.Text);
            }
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _visibleItems.Count) return;

        var g = e.Graphics;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var item = _visibleItems[e.Index];
        bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected || e.Index == _keyboardController.SelectedIndex;

        // Background
        var bgRect = e.Bounds;
        using (var bgBrush = new SolidBrush(isSelected ? ExecLedgerTheme.PrimarySelection : (e.Index % 2 == 0 ? Color.White : Color.FromArgb(248, 250, 252))))
        {
            g.FillRectangle(bgBrush, bgRect);
        }

        // Active left accent bar
        if (isSelected)
        {
            using var accentBrush = new SolidBrush(ExecLedgerTheme.SystemFocusBlue);
            g.FillRectangle(accentBrush, bgRect.X, bgRect.Y, 3, bgRect.Height);
        }

        int textY = bgRect.Y + (bgRect.Height - 17) / 2;

        // Left selection arrow cursor
        int curX = bgRect.X + 6;
        if (isSelected)
        {
            using var fontArrow = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            using var brushArrow = new SolidBrush(ExecLedgerTheme.SystemFocusBlue);
            g.DrawString(">", fontArrow, brushArrow, curX, textY);
        }
        curX += 10;

        // Indentation for Hierarchy
        if (_config.HierarchyEnabled && item.Level > 0)
        {
            curX += item.Level * 16;
            // Draw small indent guide line
            using var guidePen = new Pen(Color.FromArgb(203, 213, 225), 1);
            g.DrawLine(guidePen, curX - 8, bgRect.Y, curX - 8, bgRect.Y + bgRect.Height);
            g.DrawLine(guidePen, curX - 8, bgRect.Y + (bgRect.Height / 2), curX - 2, bgRect.Y + (bgRect.Height / 2));
        }

        // Hierarchy indicator: keep fixed 14px slot so all names at the same level align identically
        if (_config.HierarchyEnabled)
        {
            if (item.HasChildren)
            {
                using var fontGlyph = new Font("Segoe UI", 8F, FontStyle.Bold);
                using var brushGlyph = new SolidBrush(Color.FromArgb(100, 116, 139));
                string glyph = item.IsExpanded ? "▾" : "▸";
                g.DrawString(glyph, fontGlyph, brushGlyph, curX, textY);
            }
            curX += 14;
        }

        // Code Column: right-aligned with fixed right boundary clear of the scrollbar
        int scrollbarWidth = SystemInformation.VerticalScrollBarWidth;
        int rightMargin = scrollbarWidth + 12;
        const int maxCodeWidth = 90;
        int codeAreaRight = bgRect.Right - rightMargin;

        if (_config.ShowCodeColumn && !string.IsNullOrEmpty(item.Code))
        {
            using var fontCode = new Font("Segoe UI", 8.25F, FontStyle.Bold);
            using var brushCode = new SolidBrush(isSelected ? ExecLedgerTheme.SystemFocusBlue : Color.FromArgb(71, 85, 105));
            int codeWidth = TextRenderer.MeasureText(item.Code, fontCode).Width;
            int codeX = codeAreaRight - codeWidth;
            g.DrawString(item.Code, fontCode, brushCode, codeX, textY);
        }

        // Name: drawn once, clipped to available description width so it never overlaps the Code column
        int maxNameWidth = (_config.ShowCodeColumn ? (codeAreaRight - maxCodeWidth - 8) : codeAreaRight) - curX;
        if (maxNameWidth < 20) maxNameWidth = 20;

        using var fontName = new Font("Segoe UI", 9F, isSelected ? FontStyle.Bold : (item.IsSentinel ? FontStyle.Italic : FontStyle.Regular));
        using var brushName = new SolidBrush(isSelected ? ExecLedgerTheme.PrimaryNavy : (item.IsSentinel ? Color.FromArgb(100, 116, 139) : ExecLedgerTheme.PrimaryText));

        var nameFormat = new StringFormat
        {
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap
        };
        var nameRect = new RectangleF(curX, textY, maxNameWidth, 18);
        g.DrawString(item.Name, fontName, brushName, nameRect, nameFormat);
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        bool handled = _keyboardController.HandleKeyDown(
            e,
            _visibleItems,
            onSelect: SelectItem,
            onCancel: () => ClosePopup(isCancelled: true),
            onCreate: () => CreateRequested?.Invoke(),
            onShowMore: () => ShowMoreRequested?.Invoke(),
            onToggleHierarchy: (expanded) => _lstItems.Invalidate());

        if (handled)
        {
            if (_keyboardController.SelectedIndex >= 0 && _keyboardController.SelectedIndex < _visibleItems.Count)
            {
                _lstItems.SelectedIndex = _keyboardController.SelectedIndex;
            }
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        OnSearchKeyDown(sender, e);
    }

    private void OnPopupKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.F4)
        {
            ClosePopup(isCancelled: true);
            e.Handled = true;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Tab || keyData == Keys.Enter)
        {
            SelectCurrentItem();
            return true;
        }

        if (keyData == Keys.Escape)
        {
            ClosePopup(isCancelled: true);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void SelectCurrentItem()
    {
        if (_lstItems.SelectedIndex >= 0 && _lstItems.SelectedIndex < _visibleItems.Count)
        {
            SelectItem(_visibleItems[_lstItems.SelectedIndex]);
        }
        else if (_visibleItems.Count > 0)
        {
            SelectItem(_visibleItems[0]);
        }
    }

    private void SelectItem(LookupItem item)
    {
        if (_isSelecting) return;
        _isSelecting = true;

        try
        {
            ItemSelected?.Invoke(item);
            ClosePopup(isCancelled: false);
        }
        finally
        {
            _isSelecting = false;
        }
    }

    public void ClosePopup(bool isCancelled)
    {
        if (isCancelled)
        {
            Cancelled?.Invoke();
        }

        Hide();
    }

    private void OnProviderDataChanged()
    {
        if (IsHandleCreated && !IsDisposed)
        {
            BeginInvoke(new Action(async () => await ReloadDataAsync()));
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _searchCts?.Dispose();
            _provider.DataChanged -= OnProviderDataChanged;
        }
        base.Dispose(disposing);
    }
}
