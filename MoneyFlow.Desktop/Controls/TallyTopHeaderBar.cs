using System;
using System.Drawing;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Controls;

public class TallyTopHeaderBar : UserControl
{
    private readonly Label _lblCompanyName;
    private readonly Label _lblSubtitle;
    public event Action? CloseRequested;
    public event Action? CompanyMenuRequested;

    public TallyTopHeaderBar()
    {
        Dock = DockStyle.Top;
        Height = 54;
        BackColor = TallyPrimeTheme.NavyTopBar;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Main navy bar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26)); // Sub blue bar

        // 1. Row 0: Dark Navy Brand & Menus Bar
        var pnlTop = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = TallyPrimeTheme.NavyTopBar,
            Margin = new Padding(0)
        };

        var lblBrand = new Label
        {
            Text = "Tally GOLD Prime",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(8, 5)
        };

        var pnlTopMenus = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Location = new Point(140, 3)
        };

        AddTopMenu(pnlTopMenus, "K: Company", () => CompanyMenuRequested?.Invoke());
        AddTopMenu(pnlTopMenus, "Y: Data", null);
        AddTopMenu(pnlTopMenus, "Z: Exchange", null);

        var btnGoTo = new Button
        {
            Text = "G: Go To",
            Size = new Size(68, 22),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(235, 240, 248),
            ForeColor = Color.FromArgb(0, 56, 101),
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            Margin = new Padding(4, 0, 4, 0),
            Cursor = Cursors.Hand
        };
        btnGoTo.FlatAppearance.BorderSize = 0;
        pnlTopMenus.Controls.Add(btnGoTo);

        AddTopMenu(pnlTopMenus, "O: Import", null);
        AddTopMenu(pnlTopMenus, "E: Export", null);
        AddTopMenu(pnlTopMenus, "P: Print", null);
        AddTopMenu(pnlTopMenus, "F1: Help", null);

        var txtSearch = new TextBox
        {
            Text = "🔍 Search (Alt+F)",
            ForeColor = Color.Gray,
            BackColor = Color.FromArgb(245, 248, 252),
            Font = new Font("Segoe UI", 8.5F),
            Width = 180,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 190, 3)
        };
        txtSearch.Enter += (s, e) => { if (txtSearch.Text.Contains("Search")) txtSearch.Text = ""; };
        txtSearch.Leave += (s, e) => { if (string.IsNullOrWhiteSpace(txtSearch.Text)) txtSearch.Text = "🔍 Search (Alt+F)"; };

        pnlTop.Controls.Add(lblBrand);
        pnlTop.Controls.Add(pnlTopMenus);
        pnlTop.Controls.Add(txtSearch);

        // 2. Row 1: Secondary Ribbon with Voucher Subtitle, Company Name, and Close
        var pnlSub = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = TallyPrimeTheme.NavySubBar,
            ColumnCount = 3,
            RowCount = 1,
            Margin = new Padding(0)
        };
        pnlSub.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
        pnlSub.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
        pnlSub.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 35));

        _lblSubtitle = new Label
        {
            Text = "Accounting Voucher Creation",
            ForeColor = Color.FromArgb(220, 235, 250),
            Font = new Font("Segoe UI", 8.5F),
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Margin = new Padding(8, 0, 0, 0)
        };

        _lblCompanyName = new Label
        {
            Text = "Parth",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Anchor = AnchorStyles.None,
            AutoSize = true
        };

        var btnClose = new Button
        {
            Text = "✕",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Size = new Size(24, 20),
            Anchor = AnchorStyles.Right,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            Margin = new Padding(0, 0, 4, 0)
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, e) => CloseRequested?.Invoke();

        pnlSub.Controls.Add(_lblSubtitle, 0, 0);
        pnlSub.Controls.Add(_lblCompanyName, 1, 0);
        pnlSub.Controls.Add(btnClose, 2, 0);

        mainLayout.Controls.Add(pnlTop, 0, 0);
        mainLayout.Controls.Add(pnlSub, 0, 1);

        Controls.Add(mainLayout);
    }

    public void SetCompany(string companyName)
    {
        _lblCompanyName.Text = companyName;
    }

    public void SetSubtitle(string subtitle)
    {
        _lblSubtitle.Text = subtitle;
    }

    private void AddTopMenu(FlowLayoutPanel pnl, string title, Action? onClick)
    {
        var lbl = new Label
        {
            Text = title,
            ForeColor = Color.FromArgb(225, 238, 252),
            Font = new Font("Segoe UI", 8.5F),
            AutoSize = true,
            Margin = new Padding(6, 4, 4, 0),
            Cursor = Cursors.Hand
        };
        if (onClick != null)
        {
            lbl.Click += (s, e) => onClick();
        }
        pnl.Controls.Add(lbl);
    }
}
