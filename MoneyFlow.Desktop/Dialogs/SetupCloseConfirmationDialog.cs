using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Dialogs;

/// <summary>
/// Professional Modal Confirmation dialog displayed when the user attempts to close the
/// Application Startup wizard before completing initial setup.
/// Features:
/// - Elevated floating card with Guna2 drop shadow
/// - Executive Navy header with warning icon and close button
/// - Body with circular amber warning badge and structured 3-line alert message
/// - Balanced footer with [ Continue Setup ] (primary/focused) and [ Exit MyERP ] (secondary)
/// - Keyboard shortcuts: Enter / Esc safely map to Continue Setup
/// </summary>
public class SetupCloseConfirmationDialog : Form
{
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

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

    private Guna2Panel card = null!;
    private Panel pnlHeader = null!;
    private Label lblHeaderTitle = null!;
    private Guna2Button btnHeaderClose = null!;

    private Panel pnlBody = null!;
    private Guna2Panel pnlWarningBadge = null!;
    private Label lblMsgLine1 = null!;
    private Label lblMsgLine2 = null!;
    private Label lblMsgLine3 = null!;

    private Panel pnlFooter = null!;
    private Guna2Button btnContinueSetup = null!;
    private Guna2Button btnExitApp = null!;

    public SetupCloseConfirmationDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AutoScaleMode = AutoScaleMode.None;
        Text = "Close Application Setup?";
        Size = new Size(480, 230);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(11, 40, 71); // Outer navy tint
        KeyPreview = true;
        DoubleBuffered = true;

        var appIcon = ExecLedgerIcons.GetAppIcon();
        if (appIcon != null)
        {
            Icon = appIcon;
            ShowIcon = true;
        }

        // Form elevation drop shadow
        _ = new Guna2ShadowForm
        {
            TargetForm = this,
            ShadowColor = Color.FromArgb(15, 23, 42),
            BorderRadius = 10
        };

        // Main Card Container (Rounded 10px)
        card = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            FillColor = Color.White,
            BorderRadius = 10,
            BorderThickness = 1,
            BorderColor = Color.FromArgb(203, 213, 225),
            Padding = new Padding(0)
        };
        Controls.Add(card);

        BuildHeader();
        BuildFooter();
        BuildBody();

        // Keyboard navigation
        KeyDown += SetupCloseConfirmationDialog_KeyDown;
    }

    private void BuildHeader()
    {
        pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(11, 40, 71) // Deep Navy
        };

        // Header dragging
        void DragHeader(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }
        pnlHeader.MouseDown += DragHeader;

        // Warning Icon Paint on Title Bar
        pnlHeader.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Amber warning triangle
            using var brush = new SolidBrush(Color.FromArgb(245, 158, 11)); // Amber #F59E0B
            var pts = new[]
            {
                new Point(24, 11),
                new Point(35, 31),
                new Point(13, 31)
            };
            e.Graphics.FillPolygon(brush, pts);

            // Exclamation mark inside triangle
            using var markBrush = new SolidBrush(Color.FromArgb(11, 40, 71));
            using var font = new Font("Segoe UI", 9F, FontStyle.Bold);
            e.Graphics.DrawString("!", font, markBrush, 21, 14);
        };
        card.Controls.Add(pnlHeader);

        // Header Title
        lblHeaderTitle = new Label
        {
            Text = "Close Application Setup?",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(44, 11),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblHeaderTitle.MouseDown += DragHeader;
        pnlHeader.Controls.Add(lblHeaderTitle);

        // Header [X] Button
        btnHeaderClose = new Guna2Button
        {
            Text = "✕",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            FillColor = Color.Transparent,
            BorderThickness = 0,
            Size = new Size(32, 32),
            Location = new Point(pnlHeader.Width - 38, 6),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Cursor = Cursors.Hand
        };
        btnHeaderClose.HoverState.ForeColor = Color.White;
        new ToolTip().SetToolTip(btnHeaderClose, "Close [Esc]");
        btnHeaderClose.Click += (s, e) =>
        {
            DialogResult = DialogResult.No;
            Close();
        };
        pnlHeader.Controls.Add(btnHeaderClose);
    }

    private void BuildFooter()
    {
        pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 58,
            BackColor = ExecLedgerTheme.SecondarySurface // #F8FAFC
        };
        pnlFooter.Paint += (s, e) =>
        {
            using var pen = new Pen(ExecLedgerTheme.GridBorder, 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0);
        };
        card.Controls.Add(pnlFooter);

        // Continue Setup Button (BACK TO SETUP ON [Esc])
        btnContinueSetup = new Guna2Button
        {
            Text = "Continue Setup [Esc]",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            FillColor = Color.White,
            BorderThickness = 1,
            BorderColor = Color.FromArgb(203, 213, 225),
            BorderRadius = 6,
            Size = new Size(160, 36),
            Cursor = Cursors.Hand
        };
        btnContinueSetup.HoverState.BorderColor = Color.FromArgb(100, 116, 139);
        btnContinueSetup.HoverState.FillColor = Color.FromArgb(241, 245, 249);
        btnContinueSetup.Click += (s, e) =>
        {
            DialogResult = DialogResult.No;
            Close();
        };
        new ToolTip().SetToolTip(btnContinueSetup, "Return to Application Setup [Esc]");
        pnlFooter.Controls.Add(btnContinueSetup);

        // Exit MoneyFlow Button (CLOSE / EXIT ON [Enter])
        btnExitApp = new Guna2Button
        {
            Text = "Exit MoneyFlow [Enter]",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            FillColor = Color.FromArgb(11, 40, 71), // Deep Navy
            BorderThickness = 1,
            BorderColor = Color.FromArgb(56, 189, 248), // Cyan focus outline
            BorderRadius = 6,
            Size = new Size(168, 36),
            Cursor = Cursors.Hand
        };
        btnExitApp.HoverState.FillColor = Color.FromArgb(15, 53, 92);
        btnExitApp.Click += (s, e) =>
        {
            DialogResult = DialogResult.Yes;
            Close();
        };
        new ToolTip().SetToolTip(btnExitApp, "Exit MoneyFlow and close application [Enter]");
        pnlFooter.Controls.Add(btnExitApp);

        // Initial layout positioning (vertically centered, aligned to right with 20px padding)
        int btnTop = (pnlFooter.Height - 36) / 2;
        btnExitApp.Location = new Point(pnlFooter.Width - 20 - btnExitApp.Width, btnTop);
        btnContinueSetup.Location = new Point(btnExitApp.Left - 12 - btnContinueSetup.Width, btnTop);

        // Enter key activates Exit (Close); Esc key activates Continue Setup (Back)
        AcceptButton = btnExitApp;
        CancelButton = btnContinueSetup;

        Shown += (s, e) => btnExitApp.Focus();

        pnlFooter.Resize += (s, e) =>
        {
            int top = (pnlFooter.Height - 36) / 2;
            btnExitApp.Location = new Point(pnlFooter.Width - 20 - btnExitApp.Width, top);
            btnContinueSetup.Location = new Point(btnExitApp.Left - 12 - btnContinueSetup.Width, top);
        };
    }

    private void BuildBody()
    {
        pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(22, 18, 22, 12)
        };
        card.Controls.Add(pnlBody);
        pnlBody.BringToFront();

        // Warning Icon Badge on Left (Circular amber badge with triangle)
        pnlWarningBadge = new Guna2Panel
        {
            Size = new Size(46, 46),
            Location = new Point(22, 20),
            FillColor = Color.FromArgb(254, 243, 199), // Warm amber background #FEF3C7
            BorderColor = Color.FromArgb(253, 230, 138), // #FDE68A
            BorderThickness = 1,
            BorderRadius = 23
        };
        pnlWarningBadge.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            // Warning triangle
            using var brush = new SolidBrush(Color.FromArgb(217, 119, 6)); // #D97706
            var pts = new[]
            {
                new Point(23, 10),
                new Point(35, 32),
                new Point(11, 32)
            };
            e.Graphics.FillPolygon(brush, pts);

            // Exclamation mark
            using var markBrush = new SolidBrush(Color.White);
            using var font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            e.Graphics.DrawString("!", font, markBrush, 20, 14);
        };
        pnlBody.Controls.Add(pnlWarningBadge);

        // Right Content: 3 Structured message lines
        lblMsgLine1 = new Label
        {
            Text = "Your initial setup has not been completed.",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(51, 65, 85),
            Location = new Point(82, 16),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlBody.Controls.Add(lblMsgLine1);

        lblMsgLine2 = new Label
        {
            Text = "If you close now, MoneyFlow will not be initialized.",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(51, 65, 85),
            Location = new Point(82, 38),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlBody.Controls.Add(lblMsgLine2);

        lblMsgLine3 = new Label
        {
            Text = "Do you want to exit MoneyFlow?",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(82, 68),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlBody.Controls.Add(lblMsgLine3);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape || keyData == Keys.N || keyData == Keys.C)
        {
            DialogResult = DialogResult.No;
            Close();
            return true;
        }

        if (keyData == Keys.Enter)
        {
            if (btnContinueSetup.Focused)
            {
                btnContinueSetup.PerformClick();
            }
            else
            {
                btnExitApp.PerformClick();
            }
            return true;
        }

        if (keyData == Keys.Y || keyData == Keys.E)
        {
            btnExitApp.PerformClick();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void SetupCloseConfirmationDialog_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            if (btnContinueSetup.Focused)
            {
                btnContinueSetup.PerformClick();
            }
            else
            {
                btnExitApp.PerformClick();
            }
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            btnContinueSetup.PerformClick();
            e.Handled = true;
        }
    }
}
