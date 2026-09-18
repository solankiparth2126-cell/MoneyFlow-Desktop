using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Dialogs;

/// <summary>
/// Universal Close Confirmation Dialog for MoneyFlow Desktop:
/// Displays "Are you sure you want to close this?" with clear dialog elevation,
/// shadow form, translucent backdrop overlay, and Yes / No actions.
/// 
/// Keyboard shortcuts:
/// - Enter or 'Y' / 'y' -> Confirm Close (DialogResult.Yes)
/// - Esc or 'N' / 'n'   -> Cancel / Stay on module (DialogResult.No)
/// </summary>
public class CloseModuleConfirmationDialog : Form
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

    private Guna2Button btnYes = null!;
    private Guna2Button btnNo = null!;

    public CloseModuleConfirmationDialog()
    {
        InitializeComponent();
    }

    public static bool ConfirmClose(IWin32Window? owner)
    {
        Form? parentForm = (owner as Control)?.FindForm() ?? owner as Form ?? Form.ActiveForm;
        if (parentForm != null && parentForm.Visible && parentForm.IsHandleCreated)
        {
            using var overlay = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                BackColor = Color.FromArgb(15, 23, 42),
                Opacity = 0.35,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = parentForm.PointToScreen(Point.Empty),
                Size = parentForm.ClientSize,
                Owner = parentForm
            };
            overlay.Show();

            using var dlg = new CloseModuleConfirmationDialog();
            var result = dlg.ShowDialog(overlay);
            overlay.Close();
            return result == DialogResult.Yes;
        }
        else
        {
            using var dlg = new CloseModuleConfirmationDialog();
            return dlg.ShowDialog(owner) == DialogResult.Yes;
        }
    }

    private void InitializeComponent()
    {
        AutoScaleMode = AutoScaleMode.None;
        Text = "Confirm Close";
        Size = new Size(480, 210);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(27, 54, 93); // Outer border tint
        KeyPreview = true;
        DoubleBuffered = true;
        ShowInTaskbar = false;

        // Native elevation shadow
        _ = new Guna2ShadowForm
        {
            TargetForm = this,
            ShadowColor = Color.FromArgb(15, 23, 42),
            BorderRadius = 10
        };

        var card = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            FillColor = Color.White,
            BorderRadius = 10,
            BorderThickness = 1,
            BorderColor = Color.FromArgb(148, 163, 184), // Slate-400 clean contrast border
            Padding = new Padding(0)
        };
        Controls.Add(card);

        // Header (38px Navy #1B365D)
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            BackColor = Color.FromArgb(27, 54, 93)
        };
        pnlHeader.Paint += (s, e) =>
        {
            using var brush = new SolidBrush(Color.FromArgb(27, 54, 93));
            using var path = CreateTopRoundedRectanglePath(new Rectangle(0, 0, pnlHeader.Width, pnlHeader.Height), 10);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, path);
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

        // Icon badge on title bar
        var picBadge = new PictureBox
        {
            Image = ExecLedgerIcons.GetAppLogo(16, 16),
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(16, 16),
            Location = new Point(14, 11),
            BackColor = Color.Transparent
        };
        picBadge.MouseDown += DragHeader;
        pnlHeader.Controls.Add(picBadge);

        var lblHeader = new Label
        {
            Text = "MoneyFlow — Close Confirmation",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(38, 9),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblHeader.MouseDown += DragHeader;
        pnlHeader.Controls.Add(lblHeader);

        // Close button (X)
        var btnClose = new Button
        {
            Text = "✕",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(203, 213, 225),
            Size = new Size(36, 36),
            Location = new Point(pnlHeader.Width - 38, 1),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 29, 72);
        btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(190, 18, 60);
        btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.White;
        btnClose.MouseLeave += (s, e) => btnClose.ForeColor = Color.FromArgb(203, 213, 225);
        btnClose.Click += (s, e) =>
        {
            DialogResult = DialogResult.No;
            Close();
        };
        pnlHeader.Controls.Add(btnClose);

        // Message Body Panel (Middle)
        var pnlBody = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };

        // Circular Question Mark Badge Icon (#0284C7)
        var iconCircle = new Guna2Panel
        {
            Size = new Size(44, 44),
            Location = new Point(24, 26),
            FillColor = Color.FromArgb(2, 132, 199),
            BorderRadius = 22
        };
        var lblCircleQ = new Label
        {
            Text = "?",
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        iconCircle.Controls.Add(lblCircleQ);
        pnlBody.Controls.Add(iconCircle);

        var lblMessage = new Label
        {
            Text = "Are you sure you want to close this?",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(82, 24),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlBody.Controls.Add(lblMessage);

        var lblSubMessage = new Label
        {
            Text = "Press Y or Enter to confirm, N or Esc to cancel.",
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(83, 50),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlBody.Controls.Add(lblSubMessage);

        // Buttons Panel (Bottom)
        var pnlButtons = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = Color.FromArgb(248, 250, 252)
        };
        pnlButtons.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlButtons.Width, 0);
        };

        btnYes = new Guna2Button
        {
            Text = "Yes",
            Size = new Size(100, 36),
            Location = new Point(132, 10),
            FillColor = Color.FromArgb(37, 99, 235), // Primary Blue #2563EB
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            BorderRadius = 5,
            Cursor = Cursors.Hand
        };
        btnYes.Click += (s, e) =>
        {
            DialogResult = DialogResult.Yes;
            Close();
        };

        btnNo = new Guna2Button
        {
            Text = "No",
            Size = new Size(100, 36),
            Location = new Point(248, 10),
            FillColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            BorderColor = Color.FromArgb(203, 213, 225),
            BorderThickness = 1,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            BorderRadius = 5,
            Cursor = Cursors.Hand
        };
        btnNo.Click += (s, e) =>
        {
            DialogResult = DialogResult.No;
            Close();
        };

        pnlButtons.Controls.Add(btnYes);
        pnlButtons.Controls.Add(btnNo);

        card.Controls.Add(pnlHeader);
        card.Controls.Add(pnlButtons);
        card.Controls.Add(pnlBody);
        pnlHeader.SendToBack();
        pnlButtons.SendToBack();
        pnlBody.BringToFront();

        KeyDown += OnDialogKeyDown;
        Shown += (s, e) => btnYes.Focus();
    }

    private void OnDialogKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.N)
        {
            e.Handled = true;
            DialogResult = DialogResult.No;
            Close();
        }
        else if (e.KeyCode == Keys.Y)
        {
            e.Handled = true;
            DialogResult = DialogResult.Yes;
            Close();
        }
        else if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            // If No button is currently focused, Enter activates No; otherwise Yes
            DialogResult = btnNo.Focused ? DialogResult.No : DialogResult.Yes;
            Close();
        }
    }

    private static GraphicsPath CreateTopRoundedRectanglePath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddLine(bounds.Right, bounds.Bottom, bounds.X, bounds.Bottom);
        path.CloseFigure();
        return path;
    }
}
