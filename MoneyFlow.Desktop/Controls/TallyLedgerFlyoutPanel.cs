using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Controls;

/// <summary>
/// Tally-style FLAT Ledger lookup flyout panel for voucher entry screens.
///
/// KEY DESIGN RULE:
///   This panel shows ONLY actual Ledger records — NEVER Groups or Sub-Groups.
///   The Group hierarchy lives in Masters -> Chart of Accounts, NOT here.
///
/// Features:
///   - Flat alphabetical ledger list (owner-drawn ListBox)
///   - "End of List" sentinel at top when includeEndOfList = true
///   - Instant type-to-search filtering (case-insensitive substring)
///   - Full keyboard nav: Up/Down/Enter/Esc, Alt+C
///   - Create (Alt+C) fires CreateRequested -> caller opens LedgerCreateEditForm
///   - Show More fires ShowMoreRequested -> caller navigates to Ledger Master
///   - Detail footer: shows "Under: BankAccounts | Nature: Assets" for selection
/// </summary>
public class TallyLedgerFlyoutPanel : UserControl
{
    // ── UI controls
    private readonly Label _lblTitle;
    private readonly Guna2Button _btnClose;
    private readonly LinkLabel _lnkCreate;
    private readonly LinkLabel _lnkShowMore;
    private readonly Guna2TextBox _txtSearch;
    private readonly ListBox _lstLedgers;
    private readonly Panel _pnlDetail;
    private readonly Label _lblDetailText;

    // ── State
    private List<LedgerSummaryDto> _allLedgers = new();
    private List<object> _displayItems = new();
    private bool _includeEndOfList;
    private bool _isRebuilding;

    // ── Sentinel for "End of List" row
    private sealed class EndOfListMarker
    {
        public override string ToString() => "-- End of List --";
    }
    private static readonly EndOfListMarker EndOfListSentinel = new();

    // ── Events
    public event Action<LedgerSummaryDto?>? LedgerSelected;
    public event Action? CreateRequested;
    public event Action? ShowMoreRequested;
    public event Action? Closed;

    // ─────────────────────────────────────────────────────────────────────────
    public TallyLedgerFlyoutPanel()
    {
        Width = 330;
        BackColor = ExecLedgerTheme.WorkSurface;
        BorderStyle = BorderStyle.None;

        Paint += (s, e) =>
        {
            using var pen = new Pen(ExecLedgerTheme.PrimaryBorder, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(1),
            Margin = new Padding(0)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

        // [0] Header
        var pnlHeader = new Panel { Dock = DockStyle.Fill, BackColor = ExecLedgerTheme.PrimaryNavy, Margin = new Padding(0) };
        _lblTitle = new Label
        {
            Text = "List of Ledger Accounts",
            ForeColor = ExecLedgerTheme.WhiteText,
            Font = ExecLedgerTheme.UIBold9,
            AutoSize = false,
            Location = new Point(8, 6),
            Size = new Size(280, 18),
            BackColor = Color.Transparent
        };
        _btnClose = new Guna2Button
        {
            Text = "X",
            ForeColor = ExecLedgerTheme.WhiteText,
            FillColor = Color.Transparent,
            BorderThickness = 0,
            BorderRadius = 0,
            Size = new Size(28, 28),
            Location = new Point(300, 1),
            Font = ExecLedgerTheme.UIRegular8,
            Cursor = Cursors.Hand,
            HoverState = { FillColor = ExecLedgerTheme.CloseHover }
        };
        _btnClose.Click += (s, e) => CloseFlyout();
        pnlHeader.Controls.Add(_lblTitle);
        pnlHeader.Controls.Add(_btnClose);

        // [1] Actions
        var pnlActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = ExecLedgerTheme.WorkSurface,
            Padding = new Padding(4, 2, 4, 0),
            Margin = new Padding(0)
        };
        _lnkShowMore = new LinkLabel
        {
            Text = "Show More",
            LinkColor = ExecLedgerTheme.SteelBlue,
            Font = ExecLedgerTheme.UIRegular8,
            AutoSize = true,
            Margin = new Padding(6, 2, 0, 0)
        };
        _lnkShowMore.LinkClicked += (s, e) => ShowMoreRequested?.Invoke();

        _lnkCreate = new LinkLabel
        {
            Text = "Create (Alt+C)",
            LinkColor = ExecLedgerTheme.SteelBlue,
            Font = ExecLedgerTheme.UIBold8,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0)
        };
        _lnkCreate.LinkClicked += (s, e) => CreateRequested?.Invoke();
        pnlActions.Controls.Add(_lnkShowMore);
        pnlActions.Controls.Add(_lnkCreate);

        // [2] Search
        _txtSearch = new Guna2TextBox
        {
            Dock = DockStyle.Fill,
            Font = ExecLedgerTheme.UIRegular9,
            BorderRadius = ExecLedgerTheme.BorderRadius,
            BorderColor = ExecLedgerTheme.PrimaryBorder,
            BorderThickness = 1,
            FillColor = ExecLedgerTheme.InputBg,
            ForeColor = ExecLedgerTheme.PrimaryText,
            PlaceholderText = "Search ledger (e.g. HDFC, Cash)...",
            Margin = new Padding(4, 2, 4, 2)
        };
        _txtSearch.FocusedState.BorderColor = ExecLedgerTheme.InputFocusBorder;
        _txtSearch.TextChanged += (s, e) => ApplyFilter(_txtSearch.Text);
        _txtSearch.KeyDown += OnSearchKeyDown;

        // [3] Flat ledger list
        _lstLedgers = new ListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawFixed,
            BorderStyle = BorderStyle.None,
            Font = ExecLedgerTheme.UIRegular9,
            BackColor = ExecLedgerTheme.WorkSurface,
            IntegralHeight = false,
            ItemHeight = 22,
            Margin = new Padding(0)
        };
        _lstLedgers.DrawItem += OnListDrawItem;
        _lstLedgers.KeyDown += OnListKeyDown;
        _lstLedgers.DoubleClick += (s, e) => ConfirmSelection();
        _lstLedgers.SelectedIndexChanged += OnSelectedIndexChanged;

        // [4] Detail footer
        _pnlDetail = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(245, 246, 250), Margin = new Padding(0) };
        _lblDetailText = new Label
        {
            Dock = DockStyle.Fill,
            Font = ExecLedgerTheme.UIRegular8,
            ForeColor = ExecLedgerTheme.SecondaryText,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 4, 0),
            Text = string.Empty
        };
        _pnlDetail.Controls.Add(_lblDetailText);
        _pnlDetail.Paint += (s, e) =>
        {
            using var pen = new Pen(ExecLedgerTheme.PrimaryBorder, 1);
            e.Graphics.DrawLine(pen, 0, 0, _pnlDetail.Width, 0);
        };

        mainLayout.Controls.Add(pnlHeader, 0, 0);
        mainLayout.Controls.Add(pnlActions, 0, 1);
        mainLayout.Controls.Add(_txtSearch, 0, 2);
        mainLayout.Controls.Add(_lstLedgers, 0, 3);
        mainLayout.Controls.Add(_pnlDetail, 0, 4);
        Controls.Add(mainLayout);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void SetTitle(string title) => _lblTitle.Text = title;

    public void FocusSearch()
    {
        _txtSearch.Focus();
        _txtSearch.SelectAll();
    }

    public void FocusList()
    {
        if (_lstLedgers.Items.Count > 0)
        {
            if (_lstLedgers.SelectedIndex < 0)
                _lstLedgers.SelectedIndex = 0;
            _lstLedgers.Focus();
        }
    }

    /// <summary>
    /// Load a flat list of actual Ledger DTOs.
    /// Groups are NEVER passed here.
    /// </summary>
    public void LoadLedgers(IEnumerable<LedgerSummaryDto> ledgers, bool includeEndOfList = true)
    {
        _allLedgers = ledgers
            .Where(l => l.IsActive)
            .OrderBy(l => l.LedgerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _includeEndOfList = includeEndOfList;
        _txtSearch.Text = string.Empty;
        RebuildFlatList(string.Empty);
        SelectFirst();
    }

    /// <summary>
    /// Backward-compatible: extracts only Ledger leaves from hierarchy nodes.
    /// Groups are silently discarded.
    /// </summary>
    public void LoadHierarchy(IEnumerable<AccountHierarchyNodeDto> roots, bool includeEndOfList = true)
    {
        var ledgers = new List<LedgerSummaryDto>();

        void Flatten(AccountHierarchyNodeDto node)
        {
            if (!node.IsGroup)
            {
                ledgers.Add(new LedgerSummaryDto
                {
                    LedgerId = node.Id,
                    GroupId = node.ParentGroupId ?? 0,
                    LedgerName = node.Name,
                    GroupName = node.Path.Contains('>') ? node.Path[..node.Path.LastIndexOf('>')].Trim() : string.Empty,
                    GroupNature = node.Nature,
                    OpeningBalance = node.Balance,
                    OpeningBalanceType = node.BalanceType,
                    IsActive = node.IsActive
                });
            }
            foreach (var child in node.Children) Flatten(child);
        }

        foreach (var r in roots) Flatten(r);
        LoadLedgers(ledgers, includeEndOfList);
    }

    /// <summary>
    /// Refresh the list after a ledger was created.
    /// Optionally auto-selects the newly created ledger by name.
    /// </summary>
    public void RefreshWith(IEnumerable<LedgerSummaryDto> updatedLedgers, string? selectLedgerName = null)
    {
        _allLedgers = updatedLedgers
            .Where(l => l.IsActive)
            .OrderBy(l => l.LedgerName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _txtSearch.Text = string.Empty;
        RebuildFlatList(string.Empty);

        if (selectLedgerName != null)
        {
            var idx = _displayItems.FindIndex(x =>
                x is LedgerSummaryDto dto &&
                dto.LedgerName.Equals(selectLedgerName, StringComparison.OrdinalIgnoreCase));

            _lstLedgers.SelectedIndex = idx >= 0 ? idx : 0;
        }
        else
        {
            SelectFirst();
        }
    }

    // ── Internal list management ──────────────────────────────────────────────

    private void RebuildFlatList(string query)
    {
        _isRebuilding = true;
        try
        {
            IEnumerable<LedgerSummaryDto> filtered = _allLedgers;

            if (!string.IsNullOrWhiteSpace(query))
            {
                var term = query.Trim().ToLowerInvariant();
                filtered = _allLedgers.Where(l =>
                    (l.LedgerName != null && l.LedgerName.ToLowerInvariant().Contains(term)) ||
                    (!string.IsNullOrEmpty(l.GroupName) && l.GroupName.ToLowerInvariant().Contains(term)));
            }

            _displayItems = new List<object>();

            if (_includeEndOfList && string.IsNullOrWhiteSpace(query))
                _displayItems.Add(EndOfListSentinel);

            foreach (var l in filtered)
                _displayItems.Add(l);

            _lstLedgers.BeginUpdate();
            _lstLedgers.Items.Clear();
            foreach (var item in _displayItems)
                _lstLedgers.Items.Add(item);
            _lstLedgers.EndUpdate();
        }
        finally
        {
            _isRebuilding = false;
        }

        UpdateDetailFooter(null);
    }

    private void SelectFirst()
    {
        if (_lstLedgers.Items.Count > 0)
        {
            _lstLedgers.SelectedIndex = 0;
            // NOTE: Do NOT call _lstLedgers.Focus() here.
            // Calling Focus() during LoadLedgers steals focus from active voucher inputs (e.g. _cmbAccount, DataGridView cells)
            // which causes focus bounce, re-entrant Enter events, or application hangs.
        }
    }

    private void ApplyFilter(string query)
    {
        RebuildFlatList(query);
        if (_lstLedgers.Items.Count > 0)
            _lstLedgers.SelectedIndex = 0;
    }

    private void OnSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_isRebuilding) return;

        var idx = _lstLedgers.SelectedIndex;
        if (idx < 0 || idx >= _displayItems.Count)
        {
            UpdateDetailFooter(null);
            return;
        }
        UpdateDetailFooter(_displayItems[idx] as LedgerSummaryDto);
    }

    private void UpdateDetailFooter(LedgerSummaryDto? ledger)
    {
        if (ledger == null) { _lblDetailText.Text = string.Empty; return; }

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(ledger.GroupName)) parts.Add($"Under: {ledger.GroupName}");
        var nature = ledger.GroupNature.ToString();
        if (!string.IsNullOrEmpty(nature) && nature != "None" && nature != "0")
            parts.Add($"Nature: {nature}");
        _lblDetailText.Text = parts.Count > 0 ? string.Join("   |   ", parts) : string.Empty;
    }

    // ── Owner-drawn rendering ─────────────────────────────────────────────────

    private void OnListDrawItem(object? sender, DrawItemEventArgs e)
    {
        try
        {
            if (e.Index < 0 || e.Index >= _lstLedgers.Items.Count)
            {
                e.DrawBackground();
                return;
            }

            var item = _lstLedgers.Items[e.Index];
            bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

            // Background
            using (var bgBrush = new SolidBrush(isSelected ? ExecLedgerTheme.PrimarySelection : ExecLedgerTheme.WorkSurface))
                e.Graphics.FillRectangle(bgBrush, e.Bounds);

            // Left accent bar
            if (isSelected)
            {
                using var accentBrush = new SolidBrush(ExecLedgerTheme.PrimaryNavy);
                e.Graphics.FillRectangle(accentBrush, e.Bounds.Left, e.Bounds.Top, 3, e.Bounds.Height);
            }

            // Row separator
            using (var sep = new Pen(Color.FromArgb(235, 237, 244), 1))
                e.Graphics.DrawLine(sep, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);

            using var sf = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap };

            if (item is EndOfListMarker || ReferenceEquals(item, EndOfListSentinel))
            {
                using var brush = new SolidBrush(Color.FromArgb(180, 120, 50));
                var r = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top, e.Bounds.Width - 14, e.Bounds.Height);
                e.Graphics.DrawString("-- End of List --", ExecLedgerTheme.UIRegular9, brush, r, sf);
            }
            else if (item is LedgerSummaryDto ledger)
            {
                Font font = isSelected ? ExecLedgerTheme.UIBold9 : ExecLedgerTheme.UIRegular9;
                Color fg = isSelected ? ExecLedgerTheme.PrimaryNavy : ExecLedgerTheme.PrimaryText;
                using var brush = new SolidBrush(fg);
                var r = new Rectangle(e.Bounds.Left + 12, e.Bounds.Top, e.Bounds.Width - 16, e.Bounds.Height);
                string name = (ledger.LedgerName ?? string.Empty).ToUpperInvariant();
                e.Graphics.DrawString(name, font, brush, r, sf);
            }
        }
        catch
        {
            e.DrawBackground();
        }
    }

    // ── Keyboard: Search box ──────────────────────────────────────────────────

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Down: MoveSelection(+1); e.Handled = true; break;
            case Keys.Up: MoveSelection(-1); e.Handled = true; break;
            case Keys.Enter: ConfirmSelection(); e.Handled = true; break;
            case Keys.Escape: CloseFlyout(); e.Handled = true; break;
            case Keys.C when e.Alt: CreateRequested?.Invoke(); e.Handled = true; break;
        }
    }

    // ── Keyboard: Ledger list ─────────────────────────────────────────────────

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.KeyCode)
        {
            case Keys.Enter: ConfirmSelection(); e.Handled = true; break;
            case Keys.Escape: CloseFlyout(); e.Handled = true; break;
            case Keys.C when e.Alt: CreateRequested?.Invoke(); e.Handled = true; break;
            default:
                if (!e.Control && !e.Alt && e.KeyCode != Keys.Tab)
                {
                    char ch = (char)e.KeyValue;
                    if (char.IsLetterOrDigit(ch) || ch == ' ')
                    {
                        _txtSearch.Focus();
                        _txtSearch.Text += char.ToLower(ch);
                        _txtSearch.SelectionStart = _txtSearch.Text.Length;
                        e.Handled = true;
                    }
                }
                break;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void MoveSelection(int delta)
    {
        if (_lstLedgers.Items.Count == 0) return;
        _lstLedgers.SelectedIndex = Math.Clamp(_lstLedgers.SelectedIndex + delta, 0, _lstLedgers.Items.Count - 1);
    }

    private void ConfirmSelection()
    {
        int idx = _lstLedgers.SelectedIndex;
        if (idx < 0 || idx >= _displayItems.Count) return;

        var item = _displayItems[idx];

        if (item is EndOfListMarker || ReferenceEquals(item, EndOfListSentinel))
        {
            LedgerSelected?.Invoke(null);
            Visible = false;
            return;
        }

        if (item is LedgerSummaryDto dto)
        {
            LedgerSelected?.Invoke(dto);
            Visible = false;
        }
    }

    private void CloseFlyout()
    {
        Visible = false;
        Closed?.Invoke();
    }
}
