using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Dialogs;

/// <summary>
/// "No Company Selected" enforcement dialog.
/// Displays when user attempts to dismiss Select Company without an active company.
/// Buttons: [Stay] (return to Select Company) and [Exit] (close MoneyFlow).
/// </summary>
public class NoCompanySelectedDialog : Form
{
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    private const int WM_NCLBUTTONDOWN = 0xA1;
    private const int HT_CAPTION = 0x2;

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

    public NoCompanySelectedDialog()
    {
        InitializeComponent();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int preference = DWMWCP_ROUND;
            DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
        }
        catch { }
        UpdateWindowRegion();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateWindowRegion();
    }

    private void UpdateWindowRegion()
    {
        if (IsHandleCreated && Width > 0 && Height > 0)
        {
            Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, 10, 10));
        }
    }

    public static new bool ShowDialog(IWin32Window? owner)
    {
        using var dlg = new NoCompanySelectedDialog();
        ScreenFittingManager.FitDialogToWorkArea(dlg, 580, 220, 480, 180, owner);
        var result = ((Form)dlg).ShowDialog(owner);
        return result == DialogResult.Abort; // true if user wants to Exit Application
    }

    private void InitializeComponent()
    {
        AutoScaleMode = AutoScaleMode.None;
        Text = "No Company Selected — MoneyFlow Desktop ERP";
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(17, 36, 58); // Deep navy perimeter border #11243A
        Padding = new Padding(1); // 1px uniform perimeter border around all 4 sides
        KeyPreview = true;
        DoubleBuffered = true;

        ScreenFittingManager.FitDialogToWorkArea(this, 580, 220, 480, 180);

        // Card Container
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(0)
        };
        Controls.Add(card);

        // Header Bar (Deep Navy #11243A, 38px)
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = Color.FromArgb(17, 36, 58)
        };

        void DragHeader(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }
        pnlHeader.MouseDown += DragHeader;

        // Header close button (✕)
        var btnClose = new Button
        {
            Text = "✕",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(180, 200, 220),
            Size = new Size(38, 38),
            Dock = DockStyle.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 29, 72); // Crimson red on hover
        btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(190, 18, 60);
        btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.White;
        btnClose.MouseLeave += (s, e) => btnClose.ForeColor = Color.FromArgb(180, 200, 220);
        btnClose.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        pnlHeader.Controls.Add(btnClose);

        // Header Paint: Warning badge + Title + separator line
        pnlHeader.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Orange warning icon badge
            using var badgeBrush = new SolidBrush(Color.FromArgb(234, 88, 12));
            using var badgePath = new GraphicsPath();
            int r = 3;
            var bRect = new Rectangle(14, 9, 20, 20);
            badgePath.AddArc(bRect.X, bRect.Y, r * 2, r * 2, 180, 90);
            badgePath.AddArc(bRect.Right - r * 2, bRect.Y, r * 2, r * 2, 270, 90);
            badgePath.AddArc(bRect.Right - r * 2, bRect.Bottom - r * 2, r * 2, r * 2, 0, 90);
            badgePath.AddArc(bRect.X, bRect.Bottom - r * 2, r * 2, r * 2, 90, 90);
            badgePath.CloseFigure();
            e.Graphics.FillPath(badgeBrush, badgePath);

            using var whiteBrush = new SolidBrush(Color.White);
            using var badgeFont = new Font("Segoe UI", 9F, FontStyle.Bold);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString("!", badgeFont, whiteBrush, new RectangleF(14, 9, 20, 20), sf);

            // Header Title
            using var titleFont = new Font("Segoe UI", 9F, FontStyle.Bold);
            e.Graphics.DrawString("No Company Selected — MoneyFlow Desktop ERP", titleFont, whiteBrush, 40, 10);

            // Bottom border separator
            using var sepPen = new Pen(Color.FromArgb(30, 58, 95), 1);
            e.Graphics.DrawLine(sepPen, 0, pnlHeader.Height - 1, pnlHeader.Width, pnlHeader.Height - 1);
        };

        // Footer Bar (54px)
        var pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            BackColor = Color.FromArgb(248, 250, 252)
        };
        pnlFooter.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0);
        };

        var lblTip = new Label
        {
            Text = "[Enter / S] Stay   •   [Esc / E] Exit Application",
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(18, 19),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlFooter.Controls.Add(lblTip);

        // Exit Application button
        var btnExit = new Guna2Button
        {
            Text = "✕  Exit",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(185, 28, 28),
            FillColor = Color.FromArgb(254, 242, 242),
            BorderColor = Color.FromArgb(252, 165, 165),
            BorderThickness = 1,
            BorderRadius = 5,
            Size = new Size(100, 34),
            Cursor = Cursors.Hand,
            HoverState = { FillColor = Color.FromArgb(239, 68, 68), ForeColor = Color.White, BorderColor = Color.FromArgb(220, 38, 38) }
        };
        btnExit.Click += (s, e) =>
        {
            DialogResult = DialogResult.Abort;
            Close();
        };
        pnlFooter.Controls.Add(btnExit);

        // Stay button (primary action)
        var btnStay = new Guna2Button
        {
            Text = "✓  Stay",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.White,
            FillColor = Color.FromArgb(2, 132, 199), // MoneyFlow Blue
            BorderRadius = 5,
            Size = new Size(100, 34),
            Cursor = Cursors.Hand,
            HoverState = { FillColor = Color.FromArgb(3, 105, 161) }
        };
        btnStay.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };
        pnlFooter.Controls.Add(btnStay);

        void LayoutFooter()
        {
            int w = pnlFooter.ClientSize.Width;
            btnStay.Location = new Point(w - btnStay.Width - 18, (pnlFooter.Height - btnStay.Height) / 2);
            btnExit.Location = new Point(btnStay.Left - btnExit.Width - 10, (pnlFooter.Height - btnExit.Height) / 2);
        }
        pnlFooter.Resize += (s, e) => LayoutFooter();
        LayoutFooter();

        // Body Panel (Middle, Fill)
        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(0)
        };

        // Circular Warning Icon
        var iconCircle = new Guna2Panel
        {
            Size = new Size(46, 46),
            Location = new Point(24, 24),
            FillColor = Color.FromArgb(254, 242, 242),
            BorderColor = Color.FromArgb(252, 165, 165),
            BorderThickness = 1,
            BorderRadius = 23
        };
        var lblCircleQ = new Label
        {
            Text = "⚠",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.FromArgb(220, 38, 38),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        iconCircle.Controls.Add(lblCircleQ);
        pnlBody.Controls.Add(iconCircle);

        var lblTitle = new Label
        {
            Text = "No Company Selected",
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(84, 18),
            AutoSize = true
        };
        pnlBody.Controls.Add(lblTitle);

        var lblDetail = new Label
        {
            Text = "You must select or create a company before continuing.\nMoneyFlow ERP requires an active company to access the workspace.",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(84, 46),
            AutoSize = true,
            MaximumSize = new Size(475, 0)
        };
        pnlBody.Controls.Add(lblDetail);

        // ENFORCE PROPER DOCKING Z-ORDER:
        // In Windows Forms, controls docked with Fill MUST be added first (or brought to front)
        // so that Top and Bottom docks are calculated first, and Fill occupies strictly the center area!
        card.Controls.Add(pnlBody);
        card.Controls.Add(pnlFooter);
        card.Controls.Add(pnlHeader);
        pnlBody.BringToFront();

        // Keyboard handler
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.S || e.KeyCode == Keys.Enter)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.E || e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Abort;
                Close();
                e.Handled = true;
            }
        };

        // Default focus on Stay button
        Shown += (s, e) => btnStay.Focus();
    }
}
