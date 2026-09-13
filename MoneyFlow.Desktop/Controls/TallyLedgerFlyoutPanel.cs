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
/// Executive Ledger lookup flyout panel — used for F4 account/item lookup.
/// Sharp borders, navy header, search input, keyboard navigation, Enter to select, Esc to cancel.
/// </summary>
public class TallyLedgerFlyoutPanel : UserControl
{
    private readonly Label _lblTitle;
    private readonly Guna2Button _btnClose;
    private readonly LinkLabel _lnkCreate;
    private readonly LinkLabel _lnkShowMore;
    private readonly Guna2TextBox _txtSearch;
    private readonly ListBox _lstLedgers;

    private List<LedgerSummaryDto> _allLedgers = new();
    private List<LedgerSummaryDto> _filteredLedgers = new();

    public event Action<LedgerSummaryDto?>? LedgerSelected;
    public event Action? CreateRequested;
    public event Action? Closed;

    public TallyLedgerFlyoutPanel()
    {
        Width = 290;
        BackColor = ExecLedgerTheme.WorkSurface;
        BorderStyle = BorderStyle.None;

        // Outer border
        Paint += (s, e) =>
        {
            using var pen = new Pen(ExecLedgerTheme.PrimaryBorder, 1);
            e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        };

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(1),
            Margin = new Padding(0)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); // Actions
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Search
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // List

        // 1. Header Bar
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ExecLedgerTheme.PrimaryNavy,
            Margin = new Padding(0)
        };
        _lblTitle = new Label
        {
            Text = "List of Ledger Accounts",
            ForeColor = ExecLedgerTheme.WhiteText,
            Font = ExecLedgerTheme.UIBold9,
            AutoSize = false,
            Location = new Point(8, 5),
            Size = new Size(240, 18),
            BackColor = Color.Transparent
        };
        _btnClose = new Guna2Button
        {
            Text = "✕",
            ForeColor = ExecLedgerTheme.WhiteText,
            FillColor = Color.Transparent,
            BorderThickness = 0,
            BorderRadius = 0,
            Size = new Size(28, 28),
            Location = new Point(260, 0),
            Font = ExecLedgerTheme.UIRegular8,
            Cursor = Cursors.Hand,
            HoverState = { FillColor = ExecLedgerTheme.CloseHover }
        };
        _btnClose.Click += (s, e) =>
        {
            Visible = false;
            Closed?.Invoke();
        };
        pnlHeader.Controls.Add(_lblTitle);
        pnlHeader.Controls.Add(_btnClose);

        // 2. Action bar
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
        _lnkCreate = new LinkLabel
        {
            Text = "Create",
            LinkColor = ExecLedgerTheme.SteelBlue,
            Font = ExecLedgerTheme.UIBold8,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0)
        };
        _lnkCreate.LinkClicked += (s, e) => CreateRequested?.Invoke();
        pnlActions.Controls.Add(_lnkShowMore);
        pnlActions.Controls.Add(_lnkCreate);

        // 3. Search Box
        _txtSearch = new Guna2TextBox
        {
            Dock = DockStyle.Fill,
            Font = ExecLedgerTheme.UIRegular9,
            BorderRadius = ExecLedgerTheme.BorderRadius,
            BorderColor = ExecLedgerTheme.PrimaryBorder,
            BorderThickness = 1,
            FillColor = ExecLedgerTheme.InputBg,
            ForeColor = ExecLedgerTheme.PrimaryText,
            PlaceholderText = "Search ledger...",
            Margin = new Padding(4, 2, 4, 2)
        };
        _txtSearch.FocusedState.BorderColor = ExecLedgerTheme.InputFocusBorder;
        _txtSearch.TextChanged += (s, e) => ApplyFilter(_txtSearch.Text);
        _txtSearch.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Down)
            {
                if (_lstLedgers != null && _lstLedgers.SelectedIndex < _lstLedgers.Items.Count - 1)
                    _lstLedgers.SelectedIndex++;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Up)
            {
                if (_lstLedgers != null && _lstLedgers.SelectedIndex > 0)
                    _lstLedgers.SelectedIndex--;
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                ConfirmSelection();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Visible = false;
                Closed?.Invoke();
                e.Handled = true;
            }
        };

        // 4. ListBox with Executive Ledger selection style
        _lstLedgers = new ListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = ExecLedgerTheme.DenseGridRow,
            BorderStyle = BorderStyle.None,
            Font = ExecLedgerTheme.UIRegular9,
            BackColor = ExecLedgerTheme.WorkSurface,
            IntegralHeight = false
        };
        _lstLedgers.DrawItem += OnDrawItem;
        _lstLedgers.DoubleClick += (s, e) => ConfirmSelection();
        _lstLedgers.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                ConfirmSelection();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape)
            {
                Visible = false;
                Closed?.Invoke();
                e.Handled = true;
            }
        };

        mainLayout.Controls.Add(pnlHeader, 0, 0);
        mainLayout.Controls.Add(pnlActions, 0, 1);
        mainLayout.Controls.Add(_txtSearch, 0, 2);
        mainLayout.Controls.Add(_lstLedgers, 0, 3);

        Controls.Add(mainLayout);
    }

    public void SetTitle(string title)
    {
        _lblTitle.Text = title;
    }

    public void LoadLedgers(IEnumerable<LedgerSummaryDto> ledgers, bool includeEndOfList = true)
    {
        _allLedgers = ledgers.ToList();
        _txtSearch.Text = string.Empty;
        ApplyFilter(string.Empty, includeEndOfList);
    }

    public void FocusSearch()
    {
        _txtSearch.Focus();
    }

    private void ApplyFilter(string query, bool includeEndOfList = true)
    {
        _lstLedgers.BeginUpdate();
        _lstLedgers.Items.Clear();

        if (includeEndOfList && string.IsNullOrWhiteSpace(query))
        {
            _lstLedgers.Items.Add("— End of List —");
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            _filteredLedgers = _allLedgers.ToList();
        }
        else
        {
            _filteredLedgers = _allLedgers
                .Where(l => l.LedgerName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                            l.GroupName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        foreach (var ledger in _filteredLedgers)
        {
            _lstLedgers.Items.Add(ledger);
        }

        if (_lstLedgers.Items.Count > 0)
        {
            _lstLedgers.SelectedIndex = 0;
        }

        _lstLedgers.EndUpdate();
    }

    private void OnDrawItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _lstLedgers.Items.Count) return;

        bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        var item = _lstLedgers.Items[e.Index];

        // Background
        using var bgBrush = new SolidBrush(isSelected ? ExecLedgerTheme.PrimarySelection : ExecLedgerTheme.WorkSurface);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        // Selection left accent
        if (isSelected)
        {
            using var accentBrush = new SolidBrush(ExecLedgerTheme.PrimaryNavy);
            e.Graphics.FillRectangle(accentBrush, e.Bounds.Left, e.Bounds.Top, 3, e.Bounds.Height);
        }

        string text;
        Font font;
        Color textColor;

        if (item is string str)
        {
            text = str;
            font = ExecLedgerTheme.UIBold8;
            textColor = ExecLedgerTheme.SecondaryText;
        }
        else if (item is LedgerSummaryDto dto)
        {
            text = dto.LedgerName;
            font = isSelected ? ExecLedgerTheme.UIBold9 : ExecLedgerTheme.UIRegular9;
            textColor = isSelected ? ExecLedgerTheme.PrimaryNavy : ExecLedgerTheme.PrimaryText;
        }
        else
        {
            text = item.ToString() ?? string.Empty;
            font = ExecLedgerTheme.UIRegular9;
            textColor = ExecLedgerTheme.PrimaryText;
        }

        using var textBrush = new SolidBrush(textColor);
        var textBounds = new Rectangle(e.Bounds.Left + 10, e.Bounds.Top + 2, e.Bounds.Width - 16, e.Bounds.Height - 4);
        e.Graphics.DrawString(text, font, textBrush, textBounds, new StringFormat { LineAlignment = StringAlignment.Center });
    }

    private void ConfirmSelection()
    {
        if (_lstLedgers.SelectedIndex < 0) return;

        var selected = _lstLedgers.SelectedItem;
        if (selected is string && selected.ToString() == "— End of List —")
        {
            LedgerSelected?.Invoke(null);
        }
        else if (selected is LedgerSummaryDto dto)
        {
            LedgerSelected?.Invoke(dto);
        }

        Visible = false;
    }
}
