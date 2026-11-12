using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Desktop.Controls.Dropdown.Events;
using MoneyFlow.Desktop.Controls.Dropdown.Models;

namespace MoneyFlow.Desktop.Controls.Dropdown;

/// <summary>
/// Borderless, lightweight Tally-style dropdown popup window positioned near the calling field.
/// Implements keyboard-first filtering, Alt+C instant master creation, Alt+M show more, and multi-monitor safe positioning.
/// </summary>
public class MoneyFlowTallyDropdownPopup : Form
{
    private readonly DropdownConfiguration _config;
    private readonly Control _anchorControl;

    // Controls
    private Panel pnlHeader = null!;
    private Label lblTitle = null!;
    private Button btnF4 = null!;
    private Button btnClose = null!;

    private Panel pnlRecordBar = null!;
    private Label lblRecordCount = null!;
    private Label lblRecordSubtitle = null!;
    private LinkLabel lnkCreate = null!;
    private LinkLabel lnkShowMore = null!;

    private Panel pnlSearch = null!;
    private Guna2TextBox txtSearch = null!;

    private MoneyFlowTallyListControl listControl = null!;

    private Panel pnlStatusFooter = null!;
    private Label lblFilterStatus = null!;
    private Label lblItemCount = null!;
    private Label lblMoreIndicator = null!;

    private Panel pnlKeyboardFooter = null!;
    private Label lblKeyboardShortcuts = null!;

    public DropdownItemModel? SelectedItem => listControl.SelectedItem;
    public object? SelectedValue => listControl.SelectedItem?.Id;

    public event EventHandler<DropdownItemSelectedEventArgs>? ItemSelected;
    public event EventHandler<DropdownCreateEventArgs>? CreateRequested;
    public event EventHandler<DropdownMoreEventArgs>? ShowMoreRequested;

    public MoneyFlowTallyDropdownPopup(Control anchorControl, DropdownConfiguration config)
    {
        _anchorControl = anchorControl ?? throw new ArgumentNullException(nameof(anchorControl));
        _config = config ?? new DropdownConfiguration();

        InitializePopup();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_DROPSHADOW = 0x00020000;
            CreateParams cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    private void InitializePopup()
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(_config.PopupWidth, _config.PopupHeight);
        BackColor = MoneyFlowERPTheme.PopupBorderColor;
        KeyPreview = true;
        DoubleBuffered = true;

        // Inner Canvas Container (1px border simulation)
        var pnlCanvas = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = MoneyFlowERPTheme.BodyBackColor,
            Padding = new Padding(1)
        };
        Controls.Add(pnlCanvas);

        // 1. Header (30px)
        BuildHeader(pnlCanvas);

        // 2. Record Information Bar (46px)
        BuildRecordBar(pnlCanvas);

        // 3. Search Bar (34px)
        BuildSearchBar(pnlCanvas);

        // 4. Keyboard Shortcuts Footer (Bottom, 26px)
        BuildKeyboardFooter(pnlCanvas);

        // 5. Status & Count Footer (Above Keyboard Footer, 22px)
        BuildStatusFooter(pnlCanvas);

        // 6. List Control (Fill remaining space)
        BuildListControl(pnlCanvas);

        // Wire Events
        Deactivate += (s, e) =>
        {
            if (!IsDisposed && Visible)
            {
                Close();
            }
        };
    }

    private void BuildHeader(Panel container)
    {
        pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 32,
            BackColor = MoneyFlowERPTheme.HeaderBackColor,
            Padding = new Padding(8, 0, 8, 0)
        };
        pnlHeader.Paint += (s, e) =>
        {
            // Vector Globe Icon
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Color.FromArgb(56, 189, 248), 1.6f);
            g.DrawEllipse(pen, 10, 8, 15, 15);
            g.DrawLine(pen, 10, 15, 25, 15);
            g.DrawEllipse(pen, 13, 8, 9, 15);
        };

        lblTitle = new Label
        {
            Text = _config.HeaderText.ToUpperInvariant(),
            Font = new Font("Segoe UI", 8.75F, FontStyle.Bold),
            ForeColor = MoneyFlowERPTheme.HeaderForeColor,
            Location = new Point(32, 7),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlHeader.Controls.Add(lblTitle);

        btnClose = new Button
        {
            Text = "✕",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(24, 24),
            Location = new Point(pnlHeader.Width - 32, 4),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, e) => Close();
        pnlHeader.Controls.Add(btnClose);

        btnF4 = new Button
        {
            Text = "F4",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = MoneyFlowERPTheme.HeaderAccentColor,
            BackColor = Color.FromArgb(30, 41, 59),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(28, 20),
            Location = new Point(btnClose.Left - 34, 6),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        btnF4.FlatAppearance.BorderSize = 0;
        pnlHeader.Controls.Add(btnF4);

        container.Controls.Add(pnlHeader);
    }

    private void BuildRecordBar(Panel container)
    {
        pnlRecordBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = MoneyFlowERPTheme.RecordBarBackColor,
            Padding = new Padding(12, 4, 12, 4)
        };
        pnlRecordBar.Paint += (s, e) =>
        {
            using var p = new Pen(Color.FromArgb(203, 213, 225));
            e.Graphics.DrawLine(p, 0, pnlRecordBar.Height - 1, pnlRecordBar.Width, pnlRecordBar.Height - 1);
        };

        // Left Record Count Labels
        lblRecordCount = new Label
        {
            Text = "0 Records",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = MoneyFlowERPTheme.RecordBarForeColor,
            Location = new Point(12, 6),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlRecordBar.Controls.Add(lblRecordCount);

        lblRecordSubtitle = new Label
        {
            Text = _config.StatusSubtitle,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(12, 24),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlRecordBar.Controls.Add(lblRecordSubtitle);

        // Right Actions (+ New Master [Alt+C] & Show More [Alt+M])
        int rightX = _config.PopupWidth - 24;

        if (_config.AllowShowMore)
        {
            lnkShowMore = new LinkLabel
            {
                Text = $"{_config.MoreText} [{_config.MoreShortcut}]",
                Font = new Font("Segoe UI", 7.75F, FontStyle.Regular),
                LinkColor = MoneyFlowERPTheme.ActionLinkColor,
                ActiveLinkColor = Color.FromArgb(2, 132, 199),
                AutoSize = true,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            lnkShowMore.Location = new Point(rightX - 110, 24);
            lnkShowMore.LinkClicked += (s, e) => TriggerShowMore();
            pnlRecordBar.Controls.Add(lnkShowMore);
        }

        if (_config.AllowCreate)
        {
            lnkCreate = new LinkLabel
            {
                Text = $"⊕ {_config.CreateText} [{_config.CreateShortcut}]",
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                LinkColor = MoneyFlowERPTheme.ActionLinkColor,
                ActiveLinkColor = Color.FromArgb(2, 132, 199),
                AutoSize = true,
                BackColor = Color.Transparent,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Cursor = Cursors.Hand
            };
            lnkCreate.Location = new Point(rightX - 130, 6);
            lnkCreate.LinkClicked += (s, e) => TriggerCreate();
            pnlRecordBar.Controls.Add(lnkCreate);
        }

        container.Controls.Add(pnlRecordBar);
    }

    private void BuildSearchBar(Panel container)
    {
        pnlSearch = new Panel
        {
            Dock = DockStyle.Top,
            Height = 34,
            BackColor = Color.White,
            Padding = new Padding(8, 4, 8, 4)
        };
        pnlSearch.Paint += (s, e) =>
        {
            using var p = new Pen(Color.FromArgb(226, 232, 240));
            e.Graphics.DrawLine(p, 0, pnlSearch.Height - 1, pnlSearch.Width, pnlSearch.Height - 1);
        };

        txtSearch = new Guna2TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "🔍 Type to search...",
            PlaceholderForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = MoneyFlowERPTheme.SearchForeColor,
            FillColor = Color.White,
            BorderColor = MoneyFlowERPTheme.SearchBorderColor,
            BorderRadius = 3,
            BorderThickness = 1,
            Cursor = Cursors.IBeam
        };

        txtSearch.TextChanged += (s, e) =>
        {
            listControl.ApplyFilter(txtSearch.Text);
            UpdateStatusIndicators();
        };

        pnlSearch.Controls.Add(txtSearch);
        container.Controls.Add(pnlSearch);
    }

    private void BuildKeyboardFooter(Panel container)
    {
        pnlKeyboardFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 26,
            BackColor = MoneyFlowERPTheme.KeyboardFooterBackColor,
            Padding = new Padding(8, 0, 8, 0)
        };

        string shortcuts = "↑↓ Navigate   ↵ Enter Select   Esc Close";
        if (_config.AllowCreate) shortcuts += $"   {_config.CreateShortcut}: Create";
        if (_config.AllowShowMore) shortcuts += $"   {_config.MoreShortcut}: More";

        lblKeyboardShortcuts = new Label
        {
            Text = shortcuts,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Regular),
            ForeColor = MoneyFlowERPTheme.KeyboardFooterForeColor,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        pnlKeyboardFooter.Controls.Add(lblKeyboardShortcuts);
        container.Controls.Add(pnlKeyboardFooter);
    }

    private void BuildStatusFooter(Panel container)
    {
        pnlStatusFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            BackColor = MoneyFlowERPTheme.StatusFooterBackColor,
            Padding = new Padding(8, 0, 8, 0)
        };
        pnlStatusFooter.Paint += (s, e) =>
        {
            using var p = new Pen(Color.FromArgb(203, 213, 225));
            e.Graphics.DrawLine(p, 0, 0, pnlStatusFooter.Width, 0);
        };

        lblFilterStatus = new Label
        {
            Text = "● Filter: [All]",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Regular),
            ForeColor = MoneyFlowERPTheme.StatusFooterForeColor,
            Location = new Point(8, 3),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlStatusFooter.Controls.Add(lblFilterStatus);

        lblMoreIndicator = new Label
        {
            Text = "▼ MORE",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(2, 132, 199),
            Location = new Point(pnlStatusFooter.Width - 62, 3),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AutoSize = true,
            BackColor = Color.Transparent,
            Visible = false
        };
        pnlStatusFooter.Controls.Add(lblMoreIndicator);

        lblItemCount = new Label
        {
            Text = "Item 0 of 0",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Regular),
            ForeColor = MoneyFlowERPTheme.StatusFooterForeColor,
            Location = new Point(lblMoreIndicator.Left - 80, 3),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlStatusFooter.Controls.Add(lblItemCount);

        container.Controls.Add(pnlStatusFooter);
    }

    private void BuildListControl(Panel container)
    {
        listControl = new MoneyFlowTallyListControl
        {
            Dock = DockStyle.Fill,
            RowHeight = _config.RowHeight
        };

        listControl.SelectedIndexChanged += (s, e) => UpdateStatusIndicators();
        listControl.FilterChanged += (s, e) => UpdateStatusIndicators();
        listControl.ItemConfirmed += (s, item) => ConfirmSelection(item);

        container.Controls.Add(listControl);
    }

    public void LoadItems(IEnumerable<DropdownItemModel> items, object? preselectedValue = null)
    {
        listControl.SetItems(items, preselectedValue);
        lblRecordCount.Text = $"{listControl.TotalCount} Records";
        UpdateStatusIndicators();
    }

    private void UpdateStatusIndicators()
    {
        string q = listControl.CurrentFilter;
        lblFilterStatus.Text = string.IsNullOrEmpty(q) ? "● Filter: [All]" : $"● Filter: [{q}]";

        int current = listControl.SelectedIndex >= 0 ? listControl.SelectedIndex + 1 : 0;
        lblItemCount.Text = $"Item {current} of {listControl.ItemCount}";
        lblMoreIndicator.Visible = _config.AllowShowMore && (listControl.ItemCount > 15);
    }

    public void ShowBesideAnchor()
    {
        if (_anchorControl == null || _anchorControl.IsDisposed) return;

        // Position calculation respecting multi-monitor and working area
        Point screenPos = _anchorControl.PointToScreen(new Point(0, _anchorControl.Height));
        var currentScreen = Screen.FromControl(_anchorControl);
        Rectangle workingArea = currentScreen.WorkingArea;

        int targetX = screenPos.X;
        int targetY = screenPos.Y;

        // Check vertical overflow (if opening below exceeds screen, open above)
        if (targetY + Height > workingArea.Bottom)
        {
            targetY = _anchorControl.PointToScreen(Point.Empty).Y - Height;
        }

        // Check horizontal overflow
        if (targetX + Width > workingArea.Right)
        {
            targetX = workingArea.Right - Width - 8;
        }
        if (targetX < workingArea.Left)
        {
            targetX = workingArea.Left + 8;
        }

        Location = new Point(targetX, targetY);
        Show(_anchorControl.FindForm());
        txtSearch.Focus();
    }

    private void ConfirmSelection(DropdownItemModel? item)
    {
        if (item != null && item.IsEnabled)
        {
            ItemSelected?.Invoke(this, new DropdownItemSelectedEventArgs(item));
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    private void TriggerCreate()
    {
        var args = new DropdownCreateEventArgs(_config.MasterType);
        CreateRequested?.Invoke(this, args);

        if (args.CreatedItem != null)
        {
            ConfirmSelection(args.CreatedItem);
        }
    }

    private void TriggerShowMore()
    {
        var args = new DropdownMoreEventArgs(_config.MasterType);
        ShowMoreRequested?.Invoke(this, args);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        // 1. Shortcuts: Alt+C (Create), Alt+M (More), F4
        if (keyData == (Keys.Alt | Keys.C))
        {
            TriggerCreate();
            return true;
        }
        if (keyData == (Keys.Alt | Keys.M))
        {
            TriggerShowMore();
            return true;
        }
        if (keyData == Keys.F4)
        {
            TriggerCreate();
            return true;
        }

        // 2. Navigation Keys
        if (keyData == Keys.Down)
        {
            listControl.MoveNext();
            return true;
        }
        if (keyData == Keys.Up)
        {
            listControl.MovePrevious();
            return true;
        }
        if (keyData == Keys.PageDown)
        {
            listControl.MovePageDown();
            return true;
        }
        if (keyData == Keys.PageUp)
        {
            listControl.MovePageUp();
            return true;
        }
        if (keyData == Keys.Home)
        {
            listControl.MoveFirst();
            return true;
        }
        if (keyData == Keys.End)
        {
            listControl.MoveLast();
            return true;
        }

        // 3. Selection & Cancel
        if (keyData == Keys.Enter)
        {
            ConfirmSelection(listControl.SelectedItem);
            return true;
        }
        if (keyData == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }
}
