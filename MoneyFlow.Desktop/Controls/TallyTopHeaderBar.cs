using System;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Controls;

/// <summary>
/// Executive Ledger top header bar — used on child forms (voucher entry, reports, etc.).
/// Compact navy header showing the form subtitle, company context, and close button.
/// Height: 32px. Sharp 0px geometry.
/// </summary>
public class TallyTopHeaderBar : UserControl
{
    private readonly Label _lblCompanyName;
    private readonly Label _lblSubtitle;
    public event Action? CloseRequested;
    public event Action? CompanyMenuRequested;

    public TallyTopHeaderBar()
    {
        Dock = DockStyle.Top;
        Height = ExecLedgerTheme.TitleBarHeight;
        BackColor = ExecLedgerTheme.PrimaryNavy;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0),
            Margin = new Padding(0)
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36));

        // Subtitle (left side)
        _lblSubtitle = new Label
        {
            Text = "Accounting Voucher",
            ForeColor = ExecLedgerTheme.WhiteText,
            Font = ExecLedgerTheme.UIBold9,
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Margin = new Padding(10, 0, 0, 0),
            BackColor = Color.Transparent,
            Cursor = Cursors.Default
        };

        // Company name (center)
        _lblCompanyName = new Label
        {
            Text = "",
            ForeColor = Color.FromArgb(180, 200, 220),
            Font = ExecLedgerTheme.UIRegular8,
            Anchor = AnchorStyles.Right,
            AutoSize = true,
            Margin = new Padding(0, 0, 8, 0),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand
        };
        _lblCompanyName.Click += (s, e) => CompanyMenuRequested?.Invoke();

        // Close button
        var btnClose = new Guna2Button
        {
            Text = "✕",
            ForeColor = ExecLedgerTheme.WhiteText,
            FillColor = Color.Transparent,
            BorderThickness = 0,
            BorderRadius = 0,
            Size = new Size(36, ExecLedgerTheme.TitleBarHeight),
            Font = ExecLedgerTheme.UIRegular10,
            Anchor = AnchorStyles.Right,
            Cursor = Cursors.Hand,
            HoverState = { FillColor = ExecLedgerTheme.CloseHover, ForeColor = ExecLedgerTheme.WhiteText }
        };
        btnClose.Click += (s, e) => CloseRequested?.Invoke();

        mainLayout.Controls.Add(_lblSubtitle, 0, 0);
        mainLayout.Controls.Add(_lblCompanyName, 1, 0);
        mainLayout.Controls.Add(btnClose, 2, 0);

        Controls.Add(mainLayout);

        // Bottom border
        Paint += (s, e) =>
        {
            using var pen = new Pen(ExecLedgerTheme.DeepNavy, 1);
            e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
        };
    }

    public void SetCompany(string companyName)
    {
        _lblCompanyName.Text = companyName;
    }

    public void SetSubtitle(string subtitle)
    {
        _lblSubtitle.Text = subtitle;
    }
}
