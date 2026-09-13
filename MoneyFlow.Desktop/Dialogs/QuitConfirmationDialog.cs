using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace MoneyFlow.Desktop.Dialogs;

/// <summary>
/// Modern MoneyFlow theme Quit Confirmation Dialog:
/// Deep Navy header with question badge and close button, simple clear message,
/// elevation shadow, and Yes/No buttons.
/// 
/// Fast Key navigation:
/// - Pressing Y or Enter -> Exits application (DialogResult.Yes)
/// - Pressing N or Esc -> Does not exit; dismisses dialog (DialogResult.No)
/// - Clicking Yes -> Exits application
/// - Clicking No or [X] -> Does not exit
/// </summary>
public class QuitConfirmationDialog : Form
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

    public QuitConfirmationDialog(string? companyName = null, string? snapshotPath = null)
    {
        InitializeComponent();
    }

    public static bool ShowQuitDialog(IWin32Window? owner, string? companyName = null, string? snapshotPath = null)
    {
        using var dlg = new QuitConfirmationDialog(companyName, snapshotPath);
        return dlg.ShowDialog(owner) == DialogResult.Yes;
    }

    private void InitializeComponent()
    {
        AutoScaleMode = AutoScaleMode.None;
        Text = "Quit — MoneyFlow Desktop ERP";
        Size = new Size(540, 200);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(11, 39, 66); // Outer border tint
        KeyPreview = true;
        DoubleBuffered = true;

        // Elevation Drop Shadow
        _ = new Guna2ShadowForm
        {
            TargetForm = this,
            ShadowColor = Color.FromArgb(15, 23, 42),
            BorderRadius = 8
        };

        // Main Card Container (Rounded 8px)
        var card = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            FillColor = Color.White,
            BorderRadius = 8,
            BorderThickness = 1,
            BorderColor = Color.FromArgb(15, 34, 58),
            Padding = new Padding(0)
        };
        Controls.Add(card);

        // ═══════════════════════════════════════════════════════════════
        //  1. HEADER BAR (36px Deep Navy #0B2742)
        // ═══════════════════════════════════════════════════════════════
        var pnlHeader = new Panel
        {
            Height = 36,
            BackColor = Color.FromArgb(11, 39, 66)
        };

        // Header Dragging
        void DragHeader(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }
        pnlHeader.MouseDown += DragHeader;

        // Question Icon Badge on Title Bar
        var iconBadge = new Guna2Panel
        {
            Size = new Size(20, 20),
            Location = new Point(12, 8),
            FillColor = Color.FromArgb(2, 132, 199),
            BorderRadius = 3
        };
        var lblHdrQ = new Label
        {
            Text = "?",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        lblHdrQ.MouseDown += DragHeader;
        iconBadge.Controls.Add(lblHdrQ);
        pnlHeader.Controls.Add(iconBadge);

        var lblTitle = new Label
        {
            Text = "Quit — MoneyFlow Desktop ERP",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(38, 9),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblTitle.MouseDown += DragHeader;
        pnlHeader.Controls.Add(lblTitle);

        // Close Button (X)
        var btnClose = new Button
        {
            Text = "✕",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(180, 200, 220),
            Size = new Size(36, 36),
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
            DialogResult = DialogResult.No;
            Close();
        };
        pnlHeader.Controls.Add(btnClose);
        card.Controls.Add(pnlHeader);

        // ═══════════════════════════════════════════════════════════════
        //  2. BODY: SIMPLE MESSAGE WITH ICON
        // ═══════════════════════════════════════════════════════════════
        var pnlBody = new Panel
        {
            BackColor = Color.White
        };

        // Circular Question Mark Icon (#0284C7)
        var iconCircle = new Guna2Panel
        {
            Size = new Size(46, 46),
            Location = new Point(24, 26),
            FillColor = Color.FromArgb(2, 132, 199),
            BorderRadius = 23
        };
        var lblCircleQ = new Label
        {
            Text = "?",
            Font = new Font("Segoe UI", 20F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        iconCircle.Controls.Add(lblCircleQ);
        pnlBody.Controls.Add(iconCircle);

        // Simple Message Text
        var lblMessage = new Label
        {
            Text = "Do you want to exit MoneyFlow Desktop ERP?",
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(86, 26),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        var lblSubMessage = new Label
        {
            Text = "Press Y or Enter to exit, N or Esc to cancel.",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(88, 54),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlBody.Controls.Add(lblMessage);
        pnlBody.Controls.Add(lblSubMessage);
        card.Controls.Add(pnlBody);

        // ═══════════════════════════════════════════════════════════════
        //  3. FOOTER BAR: [ Yes ] and [ No ] BUTTONS
        // ═══════════════════════════════════════════════════════════════
        var pnlFooter = new Panel
        {
            Height = 54,
            BackColor = Color.FromArgb(248, 250, 252) // Soft slate background
        };

        // 1px top border line on footer
        pnlFooter.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0);
        };

        // No Button
        var btnNo = new Guna2Button
        {
            Text = "No",
            Size = new Size(84, 34),
            BorderRadius = 5,
            BorderThickness = 1,
            BorderColor = Color.FromArgb(203, 213, 225),
            FillColor = Color.White,
            ForeColor = Color.FromArgb(51, 65, 85),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Animated = true
        };
        btnNo.HoverState.BorderColor = Color.FromArgb(148, 163, 184);
        btnNo.HoverState.FillColor = Color.FromArgb(241, 245, 249);
        btnNo.HoverState.ForeColor = Color.FromArgb(15, 23, 42);
        btnNo.Click += (s, e) =>
        {
            DialogResult = DialogResult.No;
            Close();
        };

        // Yes Button (Primary outline / highlight matching screenshot)
        var btnYes = new Guna2Button
        {
            Text = "Yes",
            Size = new Size(84, 34),
            BorderRadius = 5,
            BorderThickness = 2,
            BorderColor = Color.FromArgb(2, 132, 199),
            FillColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Animated = true
        };
        btnYes.HoverState.BorderColor = Color.FromArgb(3, 105, 161);
        btnYes.HoverState.FillColor = Color.FromArgb(240, 249, 255);
        btnYes.HoverState.ForeColor = Color.FromArgb(2, 132, 199);
        btnYes.Click += (s, e) =>
        {
            DialogResult = DialogResult.Yes;
            Close();
        };

        pnlFooter.Controls.Add(btnYes);
        pnlFooter.Controls.Add(btnNo);
        card.Controls.Add(pnlFooter);

        // Precise dynamic layout of header, body, footer, and buttons
        void LayoutCard()
        {
            int w = card.ClientSize.Width;
            int h = card.ClientSize.Height;

            pnlHeader.Bounds = new Rectangle(0, 0, w, 36);
            btnClose.Location = new Point(w - 36, 0);

            pnlFooter.Bounds = new Rectangle(0, h - 54, w, 54);
            btnNo.Location = new Point(w - 84 - 18, 10);
            btnYes.Location = new Point(w - 84 - 18 - 84 - 12, 10);

            pnlBody.Bounds = new Rectangle(0, 36, w, h - 36 - 54);
        }

        card.Resize += (s, e) => LayoutCard();
        Load += (s, e) => LayoutCard();
        LayoutCard();

        // Focus default on Yes button
        Shown += (s, e) => btnYes.Focus();
    }

    // ═══════════════════════════════════════════════════════════════
    //  KEYBOARD SHORTCUTS: Y / Enter -> Exit; N / Esc -> Cancel
    // ═══════════════════════════════════════════════════════════════
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;
        if (key == Keys.Y || key == Keys.Enter)
        {
            DialogResult = DialogResult.Yes;
            Close();
            return true;
        }
        if (key == Keys.N || key == Keys.Escape)
        {
            DialogResult = DialogResult.No;
            Close();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
