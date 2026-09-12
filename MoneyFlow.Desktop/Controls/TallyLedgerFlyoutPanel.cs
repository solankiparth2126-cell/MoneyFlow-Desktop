using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Controls;

public class TallyLedgerFlyoutPanel : UserControl
{
    private readonly Label _lblTitle;
    private readonly Button _btnClose;
    private readonly LinkLabel _lnkCreate;
    private readonly LinkLabel _lnkShowMore;
    private readonly TextBox _txtSearch;
    private readonly ListBox _lstLedgers;

    private List<LedgerSummaryDto> _allLedgers = new();
    private List<LedgerSummaryDto> _filteredLedgers = new();

    public event Action<LedgerSummaryDto?>? LedgerSelected;
    public event Action? CreateRequested;
    public event Action? Closed;

    public TallyLedgerFlyoutPanel()
    {
        Width = 290;
        BackColor = TallyPrimeTheme.FlyoutBodyBg;
        BorderStyle = BorderStyle.FixedSingle;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Header
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24)); // Actions (Create / Show More)
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Search box
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // List

        // 1. Header Bar
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = TallyPrimeTheme.FlyoutHeaderBg,
            Margin = new Padding(0)
        };
        _lblTitle = new Label
        {
            Text = "List of Ledger Accounts",
            ForeColor = TallyPrimeTheme.FlyoutHeaderFg,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            AutoSize = false,
            Location = new Point(8, 5),
            Size = new Size(245, 18)
        };
        _btnClose = new Button
        {
            Text = "✕",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Size = new Size(22, 20),
            Location = new Point(262, 3),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 8F, FontStyle.Bold)
        };
        _btnClose.FlatAppearance.BorderSize = 0;
        _btnClose.Click += (s, e) =>
        {
            Visible = false;
            Closed?.Invoke();
        };
        pnlHeader.Controls.Add(_lblTitle);
        pnlHeader.Controls.Add(_btnClose);

        // 2. Action bar (Create, Show More)
        var pnlActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            BackColor = TallyPrimeTheme.FlyoutBodyBg,
            Padding = new Padding(4, 2, 4, 0),
            Margin = new Padding(0)
        };
        _lnkShowMore = new LinkLabel
        {
            Text = "Show More",
            LinkColor = Color.FromArgb(0, 75, 135),
            Font = new Font("Segoe UI", 8.5F),
            AutoSize = true,
            Margin = new Padding(6, 2, 0, 0)
        };
        _lnkCreate = new LinkLabel
        {
            Text = "Create",
            LinkColor = Color.FromArgb(0, 75, 135),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0)
        };
        _lnkCreate.LinkClicked += (s, e) => CreateRequested?.Invoke();
        pnlActions.Controls.Add(_lnkShowMore);
        pnlActions.Controls.Add(_lnkCreate);

        // 3. Search Box
        _txtSearch = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9F),
            Margin = new Padding(4, 0, 4, 2)
        };
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

        // 4. ListBox with Tally-style golden selection
        _lstLedgers = new ListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 22,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 9F),
            BackColor = Color.White,
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
        _txtSearch.SelectAll();
    }

    private void ApplyFilter(string query, bool includeEndOfList = true)
    {
        _lstLedgers.BeginUpdate();
        _lstLedgers.Items.Clear();

        if (includeEndOfList && string.IsNullOrWhiteSpace(query))
        {
            _lstLedgers.Items.Add("♦ End of List");
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

        using var bgBrush = new SolidBrush(isSelected ? TallyPrimeTheme.FlyoutSelectedBg : Color.White);
        e.Graphics.FillRectangle(bgBrush, e.Bounds);

        string text;
        Font font = TallyPrimeTheme.RegularFont;
        Color textColor = isSelected ? Color.Black : TallyPrimeTheme.TextPrimary;

        if (item is string str)
        {
            text = str;
            font = new Font("Segoe UI", 9F, FontStyle.Bold);
        }
        else if (item is LedgerSummaryDto dto)
        {
            text = dto.LedgerName;
        }
        else
        {
            text = item.ToString() ?? string.Empty;
        }

        using var textBrush = new SolidBrush(textColor);
        var textBounds = new Rectangle(e.Bounds.Left + 8, e.Bounds.Top + 2, e.Bounds.Width - 16, e.Bounds.Height - 4);
        e.Graphics.DrawString(text, font, textBrush, textBounds, new StringFormat { LineAlignment = StringAlignment.Center });

        e.DrawFocusRectangle();
    }

    private void ConfirmSelection()
    {
        if (_lstLedgers.SelectedIndex < 0) return;

        var selected = _lstLedgers.SelectedItem;
        if (selected is string && selected.ToString() == "♦ End of List")
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
