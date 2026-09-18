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
            Location = new Point(10, 7),
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
            Location = new Point(10, 6)
        };
        pnlSub.Controls.Add(_lblRecordCount);

        int rightX = Width - 12;

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
            Padding = new Padding(10, 5, 10, 5),
            Margin = new Padding(0)
        };

        var lblFindIcon = new Label
        {
            Text = "Find:",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            AutoSize = true,
            Location = new Point(10, 7)
        };
        pnlFind.Controls.Add(lblFindIcon);

        _txtSearch = new TextBox
        {
            Location = new Point(54, 5),
            Width = Width - 66,
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
            g.DrawString(_config.DescriptionColumnHeader, font, brush, 12, 4);

            if (_config.ShowCodeColumn)
            {
                int codeX = Width - 80;
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
            Location = new Point(10, 4)
        };
        pnlStatus.Controls.Add(_lblFilterStatus);

        _lblItemCounter = new Label
        {
            Text = "Item 0 of 0",
            Font = new Font("Segoe UI", 7.5F),
            ForeColor = Color.FromArgb(100, 116, 139),
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 110, 4)
        };
        pnlStatus.Controls.Add(_lblItemCounter);

        // ── 6. Keyboard Shortcut Footer ──
        _pnlFooter = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ExecLedgerTheme.PrimaryNavy,
            Margin = new Padding(0)
        };

        var lblFooterHelp = new Label
        {
            Text = "↑↓ Navigate   ENTER Select   ESC Close   ALT+C Create   ALT+M More",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(226, 232, 240),
            AutoSize = true,
            Location = new Point(10, 6)
        };
        _pnlFooter.Controls.Add(lblFooterHelp);

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
        await ReloadDataAsync(initialSearch);
        PositionNear(anchorControl);

        if (!Visible)
        {
            Show(anchorControl.FindForm());
        }

        _txtSearch.Focus();
        if (!string.IsNullOrEmpty(initialSearch))
        {
            _txtSearch.Text = initialSearch;
            _txtSearch.SelectionStart = _txtSearch.Text.Length;
        }
    }

    public void PositionNear(Control anchorControl)
    {
        var anchorScreen = anchorControl.PointToScreen(Point.Empty);
        var screen = Screen.FromControl(anchorControl);
        var workArea = screen.WorkingArea;

        int popW = Width;
        int popH = Height;

        // Preferred: below anchor
        int top = anchorScreen.Y + anchorControl.Height + 2;
        if (top + popH > workArea.Bottom)
        {
            // Position above if not enough space below
            top = anchorScreen.Y - popH - 2;
        }

        // Horizontal alignment
        int left = anchorScreen.X;
        if (left + popW > workArea.Right)
        {
            left = workArea.Right - popW - 6;
        }
        if (left < workArea.Left)
        {
            left = workArea.Left + 6;
        }

        // Clamp vertically
        if (top < workArea.Top) top = workArea.Top + 6;
        if (top + popH > workArea.Bottom) top = workArea.Bottom - popH - 6;

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

        // Left cursor arrow
        int textX = bgRect.X + 8;
        if (isSelected)
        {
            using var fontArrow = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            using var brushArrow = new SolidBrush(ExecLedgerTheme.SystemFocusBlue);
            g.DrawString(">", fontArrow, brushArrow, textX, textY);
        }
        textX += 12;

        // Indentation for Hierarchy
        if (_config.HierarchyEnabled && item.Level > 0)
        {
            textX += item.Level * 16;
            // Draw small indent guide line
            using var guidePen = new Pen(Color.FromArgb(203, 213, 225), 1);
            g.DrawLine(guidePen, textX - 8, bgRect.Y, textX - 8, bgRect.Y + bgRect.Height);
            g.DrawLine(guidePen, textX - 8, bgRect.Y + (bgRect.Height / 2), textX - 2, bgRect.Y + (bgRect.Height / 2));
        }

        // Hierarchy indicator
        if (item.HasChildren)
        {
            using var fontGlyph = new Font("Segoe UI", 8F, FontStyle.Bold);
            using var brushGlyph = new SolidBrush(Color.FromArgb(100, 116, 139));
            string glyph = item.IsExpanded ? "▾" : "▸";
            g.DrawString(glyph, fontGlyph, brushGlyph, textX, textY);
            textX += 14;
        }

        // Name
        using var fontName = new Font("Segoe UI", 9F, isSelected ? FontStyle.Bold : (item.IsSentinel ? FontStyle.Italic : FontStyle.Regular));
        using var brushName = new SolidBrush(isSelected ? ExecLedgerTheme.PrimaryNavy : (item.IsSentinel ? Color.FromArgb(100, 116, 139) : ExecLedgerTheme.PrimaryText));
        g.DrawString(item.Name, fontName, brushName, textX, textY);

        // Subtitle (if space allows and provided)
        int nameWidth = TextRenderer.MeasureText(item.Name, fontName).Width;
        if (!string.IsNullOrEmpty(item.Subtitle))
        {
            int subX = textX + nameWidth + 8;
            if (subX < Width - 120)
            {
                using var fontSub = new Font("Segoe UI", 8F, FontStyle.Regular);
                using var brushSub = new SolidBrush(Color.FromArgb(148, 163, 184));
                g.DrawString($"({item.Subtitle})", fontSub, brushSub, subX, textY + 1);
            }
        }

        // Code Column
        if (_config.ShowCodeColumn && !string.IsNullOrEmpty(item.Code))
        {
            using var fontCode = new Font("Segoe UI", 8.25F, FontStyle.Bold);
            using var brushCode = new SolidBrush(isSelected ? ExecLedgerTheme.SystemFocusBlue : Color.FromArgb(71, 85, 105));
            int codeWidth = TextRenderer.MeasureText(item.Code, fontCode).Width;
            int codeX = Width - codeWidth - 16;
            g.DrawString(item.Code, fontCode, brushCode, codeX, textY);
        }
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

    private void SelectCurrentItem()
    {
        if (_lstItems.SelectedIndex >= 0 && _lstItems.SelectedIndex < _visibleItems.Count)
        {
            SelectItem(_visibleItems[_lstItems.SelectedIndex]);
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
