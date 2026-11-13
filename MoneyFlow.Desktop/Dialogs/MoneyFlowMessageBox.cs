using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop;

/// <summary>
/// Universal Modern MoneyFlow ERP Themed Dialog Box.
/// Replaces standard Win32 MessageBox.Show across the application with:
/// - Deep Navy (#1B365D) branded header with App Logo and draggable caption
/// - Native drop shadow and rounded corners (8-10px)
/// - Themed circular status badges (Information, Success, Question, Warning, Error)
/// - High-contrast readable typography and dynamic content sizing
/// - Styled Guna2 buttons matching MoneyFlow desktop UI guidelines
/// - Translucent backdrop overlay over the active owner window
/// - Full keyboard accessibility (Enter, Esc, Y, N shortcuts)
/// </summary>
public class MoneyFlowMessageBox : Form
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
            var cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }

    private readonly string _message;
    private readonly string _caption;
    private readonly MessageBoxButtons _buttons;
    private readonly MessageBoxIcon _icon;
    private readonly MessageBoxDefaultButton _defaultButton;

    private Guna2Button? _btnPrimary;
    private Guna2Button? _btnSecondary;
    private Guna2Button? _btnTertiary;

    public MoneyFlowMessageBox(
        string message,
        string caption,
        MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.None,
        MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
    {
        _message = message ?? string.Empty;
        _caption = string.IsNullOrWhiteSpace(caption) ? "MoneyFlow ERP" : caption.Trim();
        _buttons = buttons;
        _icon = icon;
        _defaultButton = defaultButton;

        InitializeComponent();
    }

    #region Static Show Methods (Drop-in MessageBox replacements)

    public static DialogResult Show(string text)
        => ShowInternal(null, text, "MoneyFlow ERP", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(string text, string caption)
        => ShowInternal(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons)
        => ShowInternal(null, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        => ShowInternal(null, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        => ShowInternal(null, text, caption, buttons, icon, defaultButton);

    public static DialogResult Show(IWin32Window? owner, string text)
        => ShowInternal(owner, text, "MoneyFlow ERP", MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(IWin32Window? owner, string text, string caption)
        => ShowInternal(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(IWin32Window? owner, string text, string caption, MessageBoxButtons buttons)
        => ShowInternal(owner, text, caption, buttons, MessageBoxIcon.None, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(IWin32Window? owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        => ShowInternal(owner, text, caption, buttons, icon, MessageBoxDefaultButton.Button1);

    public static DialogResult Show(IWin32Window? owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        => ShowInternal(owner, text, caption, buttons, icon, defaultButton);

    private static DialogResult ShowInternal(
        IWin32Window? owner,
        string message,
        string caption,
        MessageBoxButtons buttons,
        MessageBoxIcon icon,
        MessageBoxDefaultButton defaultButton)
    {
        Form? targetOwner = (owner as Control)?.FindForm() ?? owner as Form ?? Form.ActiveForm;
        while (targetOwner != null && !targetOwner.TopLevel && targetOwner.Parent != null)
        {
            var next = targetOwner.Parent.FindForm();
            if (next == null || next == targetOwner) break;
            targetOwner = next;
        }

        if (targetOwner != null && !targetOwner.TopLevel)
        {
            targetOwner = Form.ActiveForm ?? Application.OpenForms.Cast<Form>().FirstOrDefault(f => f.TopLevel && f.Visible);
        }

        // If running in headless / test environment, display directly without backdrop overlay
        bool isTestHost = AppDomain.CurrentDomain.FriendlyName.IndexOf("testhost", StringComparison.OrdinalIgnoreCase) >= 0;
        if (isTestHost || targetOwner == null || !targetOwner.Visible || !targetOwner.IsHandleCreated)
        {
            using var dlg = new MoneyFlowMessageBox(message, caption, buttons, icon, defaultButton);
            return dlg.ShowDialog(owner);
        }

        // Display modern translucent backdrop over parent window for focus
        using var backdrop = new Form
        {
            FormBorderStyle = FormBorderStyle.None,
            BackColor = Color.FromArgb(15, 23, 42),
            Opacity = 0.28,
            ShowInTaskbar = false,
            StartPosition = FormStartPosition.Manual,
            Location = targetOwner.PointToScreen(Point.Empty),
            Size = targetOwner.ClientSize,
            Owner = targetOwner.TopLevel ? targetOwner : null
        };

        try
        {
            backdrop.Show();
            using var dlg = new MoneyFlowMessageBox(message, caption, buttons, icon, defaultButton);
            return dlg.ShowDialog(backdrop);
        }
        finally
        {
            backdrop.Close();
        }
    }

    #endregion

    private void InitializeComponent()
    {
        AutoScaleMode = AutoScaleMode.None;
        Text = _caption;
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(27, 54, 93); // Outer border tint
        KeyPreview = true;
        DoubleBuffered = true;
        ShowInTaskbar = false;

        var appIcon = ExecLedgerIcons.GetAppIcon();
        if (appIcon != null)
        {
            Icon = appIcon;
            ShowIcon = true;
        }

        // Drop shadow (disabled in testhost)
        if (!AppDomain.CurrentDomain.FriendlyName.IndexOf("testhost", StringComparison.OrdinalIgnoreCase).Equals(-1))
        {
            _ = new Guna2ShadowForm
            {
                TargetForm = this,
                ShadowColor = Color.FromArgb(15, 23, 42),
                BorderRadius = 10
            };
        }

        // Main Card Container with 10px rounded corners
        var card = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            FillColor = Color.White,
            BorderRadius = 10,
            BorderThickness = 1,
            BorderColor = Color.FromArgb(148, 163, 184),
            Padding = new Padding(0)
        };
        Controls.Add(card);

        // ═══════════════════════════════════════════════════════════════
        //  1. HEADER BAR (38px Navy #1B365D)
        // ═══════════════════════════════════════════════════════════════
        var pnlHeader = new Panel
        {
            Location = new Point(0, 0),
            Height = 38,
            BackColor = Color.FromArgb(27, 54, 93),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
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

        // Header Logo Badge
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

        string headerTitle = _caption.StartsWith("MoneyFlow", StringComparison.OrdinalIgnoreCase)
            ? _caption
            : $"MoneyFlow — {_caption}";

        var lblTitle = new Label
        {
            Text = headerTitle,
            Font = new Font("Segoe UI", 9.25F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(38, 9),
            AutoSize = true,
            BackColor = Color.Transparent,
            UseMnemonic = false
        };
        lblTitle.MouseDown += DragHeader;
        pnlHeader.Controls.Add(lblTitle);

        // Close button (X)
        var btnClose = new Button
        {
            Text = "✕",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(203, 213, 225),
            Size = new Size(36, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        btnClose.Location = new Point(pnlHeader.Width - 38, 1);
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(225, 29, 72);
        btnClose.FlatAppearance.MouseDownBackColor = Color.FromArgb(190, 18, 60);
        btnClose.MouseEnter += (s, e) => btnClose.ForeColor = Color.White;
        btnClose.MouseLeave += (s, e) => btnClose.ForeColor = Color.FromArgb(203, 213, 225);
        btnClose.Click += (s, e) => HandleCloseAction();
        pnlHeader.Controls.Add(btnClose);

        pnlHeader.Resize += (s, e) =>
        {
            btnClose.Location = new Point(pnlHeader.Width - 38, 1);
        };

        // ═══════════════════════════════════════════════════════════════
        //  2. BODY CONTENT AREA (Middle, starts at Y = 38 below Header)
        // ═══════════════════════════════════════════════════════════════
        var pnlBody = new Panel
        {
            Location = new Point(0, 38),
            BackColor = Color.White,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // Resolve Badge Color and Symbol based on icon and context
        var (badgeColor, badgeChar) = ResolveBadgeInfo(_icon, _caption, _message);

        var badgeCircle = new Guna2Panel
        {
            Size = new Size(44, 44),
            Location = new Point(24, 18),
            FillColor = badgeColor,
            BorderRadius = 22
        };

        float badgeFontSize = badgeChar switch
        {
            "✓" => 18F,
            "!" => 19F,
            "?" => 18F,
            "✕" => 17F,
            _ => 17F
        };

        var lblBadgeChar = new Label
        {
            Text = badgeChar,
            Font = new Font("Segoe UI", badgeFontSize, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = Color.Transparent
        };
        badgeCircle.Controls.Add(lblBadgeChar);
        pnlBody.Controls.Add(badgeCircle);

        // Calculate dynamic dimensions based on text length
        using var fontMeasure = new Font("Segoe UI", 10F, FontStyle.Regular);
        int maxTextWidth = 440;
        var measuredSize = TextRenderer.MeasureText(_message, fontMeasure, new Size(maxTextWidth, int.MaxValue), TextFormatFlags.WordBreak);

        int contentWidth = Math.Clamp(measuredSize.Width, 280, 540);
        int dialogWidth = Math.Clamp(contentWidth + 110, 460, 660);

        var lblMessage = new Label
        {
            Text = _message,
            Font = fontMeasure,
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(82, 18),
            Size = new Size(dialogWidth - 110, Math.Max(measuredSize.Height + 10, 44)),
            AutoSize = false,
            BackColor = Color.Transparent,
            UseMnemonic = false
        };
        pnlBody.Controls.Add(lblMessage);

        int bodyHeight = Math.Max(80, lblMessage.Bottom + 20);
        pnlBody.Size = new Size(dialogWidth, bodyHeight);

        pnlHeader.Size = new Size(dialogWidth, 38);

        // ═══════════════════════════════════════════════════════════════
        //  3. FOOTER BAR (54px #F8FAFC with Buttons, starts at Y = 38 + bodyHeight)
        // ═══════════════════════════════════════════════════════════════
        var pnlFooter = new Panel
        {
            Location = new Point(0, 38 + bodyHeight),
            Size = new Size(dialogWidth, 54),
            BackColor = Color.FromArgb(248, 250, 252),
            Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        pnlFooter.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0);
        };

        int totalHeight = 38 + bodyHeight + 54;
        ClientSize = new Size(dialogWidth, totalHeight);

        // Build Buttons
        BuildButtons(pnlFooter);

        // Add panels to card with absolute coordinates (No Dock overlapping issues)
        card.Controls.Add(pnlHeader);
        card.Controls.Add(pnlBody);
        card.Controls.Add(pnlFooter);

        // Wire Keyboard
        KeyDown += OnDialogKeyDown;
    }

    private void BuildButtons(Panel footer)
    {
        int rightMargin = 20;

        switch (_buttons)
        {
            case MessageBoxButtons.OK:
                _btnPrimary = CreateButton("OK", DialogResult.OK, isPrimary: true);
                _btnPrimary.Location = new Point(Width - rightMargin - _btnPrimary.Width, 10);
                footer.Controls.Add(_btnPrimary);
                AcceptButton = _btnPrimary;
                break;

            case MessageBoxButtons.OKCancel:
                _btnSecondary = CreateButton("Cancel (Esc)", DialogResult.Cancel, isPrimary: false);
                _btnPrimary = CreateButton("OK (Enter)", DialogResult.OK, isPrimary: true);

                _btnSecondary.Location = new Point(Width - rightMargin - _btnSecondary.Width, 10);
                _btnPrimary.Location = new Point(_btnSecondary.Left - 10 - _btnPrimary.Width, 10);

                footer.Controls.Add(_btnSecondary);
                footer.Controls.Add(_btnPrimary);

                if (_defaultButton == MessageBoxDefaultButton.Button2)
                {
                    AcceptButton = _btnSecondary;
                    CancelButton = _btnSecondary;
                }
                else
                {
                    AcceptButton = _btnPrimary;
                    CancelButton = _btnSecondary;
                }
                break;

            case MessageBoxButtons.YesNo:
                _btnSecondary = CreateButton("No (Esc)", DialogResult.No, isPrimary: false);
                _btnPrimary = CreateButton("Yes (Enter)", DialogResult.Yes, isPrimary: true);

                _btnSecondary.Location = new Point(Width - rightMargin - _btnSecondary.Width, 10);
                _btnPrimary.Location = new Point(_btnSecondary.Left - 10 - _btnPrimary.Width, 10);

                footer.Controls.Add(_btnSecondary);
                footer.Controls.Add(_btnPrimary);

                if (_defaultButton == MessageBoxDefaultButton.Button2)
                {
                    AcceptButton = _btnSecondary;
                    CancelButton = _btnSecondary;
                }
                else
                {
                    AcceptButton = _btnPrimary;
                    CancelButton = _btnSecondary;
                }
                break;

            case MessageBoxButtons.YesNoCancel:
                _btnTertiary = CreateButton("Cancel", DialogResult.Cancel, isPrimary: false);
                _btnSecondary = CreateButton("No", DialogResult.No, isPrimary: false);
                _btnPrimary = CreateButton("Yes", DialogResult.Yes, isPrimary: true);

                _btnTertiary.Location = new Point(Width - rightMargin - _btnTertiary.Width, 10);
                _btnSecondary.Location = new Point(_btnTertiary.Left - 10 - _btnSecondary.Width, 10);
                _btnPrimary.Location = new Point(_btnSecondary.Left - 10 - _btnPrimary.Width, 10);

                footer.Controls.Add(_btnTertiary);
                footer.Controls.Add(_btnSecondary);
                footer.Controls.Add(_btnPrimary);

                AcceptButton = _btnPrimary;
                CancelButton = _btnTertiary;
                break;

            case MessageBoxButtons.RetryCancel:
                _btnSecondary = CreateButton("Cancel", DialogResult.Cancel, isPrimary: false);
                _btnPrimary = CreateButton("Retry", DialogResult.Retry, isPrimary: true);

                _btnSecondary.Location = new Point(Width - rightMargin - _btnSecondary.Width, 10);
                _btnPrimary.Location = new Point(_btnSecondary.Left - 10 - _btnPrimary.Width, 10);

                footer.Controls.Add(_btnSecondary);
                footer.Controls.Add(_btnPrimary);

                AcceptButton = _btnPrimary;
                CancelButton = _btnSecondary;
                break;

            case MessageBoxButtons.AbortRetryIgnore:
                _btnTertiary = CreateButton("Ignore", DialogResult.Ignore, isPrimary: false);
                _btnSecondary = CreateButton("Retry", DialogResult.Retry, isPrimary: true);
                _btnPrimary = CreateButton("Abort", DialogResult.Abort, isPrimary: false);

                _btnTertiary.Location = new Point(Width - rightMargin - _btnTertiary.Width, 10);
                _btnSecondary.Location = new Point(_btnTertiary.Left - 10 - _btnSecondary.Width, 10);
                _btnPrimary.Location = new Point(_btnSecondary.Left - 10 - _btnPrimary.Width, 10);

                footer.Controls.Add(_btnTertiary);
                footer.Controls.Add(_btnSecondary);
                footer.Controls.Add(_btnPrimary);

                CancelButton = _btnPrimary;
                break;
        }

        footer.Resize += (s, e) =>
        {
            if (_btnTertiary != null && _btnSecondary != null && _btnPrimary != null)
            {
                _btnTertiary.Location = new Point(footer.Width - rightMargin - _btnTertiary.Width, 10);
                _btnSecondary.Location = new Point(_btnTertiary.Left - 10 - _btnSecondary.Width, 10);
                _btnPrimary.Location = new Point(_btnSecondary.Left - 10 - _btnPrimary.Width, 10);
            }
            else if (_btnSecondary != null && _btnPrimary != null)
            {
                _btnSecondary.Location = new Point(footer.Width - rightMargin - _btnSecondary.Width, 10);
                _btnPrimary.Location = new Point(_btnSecondary.Left - 10 - _btnPrimary.Width, 10);
            }
            else if (_btnPrimary != null)
            {
                _btnPrimary.Location = new Point(footer.Width - rightMargin - _btnPrimary.Width, 10);
            }
        };
    }

    private Guna2Button CreateButton(string text, DialogResult result, bool isPrimary)
    {
        var btn = new Guna2Button
        {
            Text = text,
            DialogResult = result,
            Height = 34,
            BorderRadius = 4,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9.25F, FontStyle.Bold)
        };

        int textWidth = TextRenderer.MeasureText(text, btn.Font).Width;
        btn.Width = Math.Max(92, textWidth + 28);

        if (isPrimary)
        {
            btn.FillColor = Color.FromArgb(27, 54, 93); // Navy Primary
            btn.ForeColor = Color.White;
            btn.HoverState.FillColor = Color.FromArgb(15, 34, 58);
            btn.HoverState.ForeColor = Color.White;
        }
        else
        {
            btn.FillColor = Color.White;
            btn.ForeColor = Color.FromArgb(51, 65, 85); // Slate-700
            btn.BorderColor = Color.FromArgb(203, 213, 225); // Slate-300
            btn.BorderThickness = 1;
            btn.HoverState.FillColor = Color.FromArgb(241, 245, 249);
            btn.HoverState.BorderColor = Color.FromArgb(148, 163, 184);
            btn.HoverState.ForeColor = Color.FromArgb(15, 23, 42);
        }

        btn.Click += (s, e) =>
        {
            DialogResult = result;
            Close();
        };

        return btn;
    }

    private static (Color Color, string Symbol) ResolveBadgeInfo(MessageBoxIcon icon, string caption, string message)
    {
        string combined = $"{caption} {message}".ToLowerInvariant();

        // Check for success / confirmation
        if (combined.Contains("success") || combined.Contains("created successfully") || combined.Contains("saved successfully") || combined.Contains("completed"))
        {
            return (Color.FromArgb(16, 185, 129), "✓"); // Emerald Green
        }

        switch (icon)
        {
            case MessageBoxIcon.Information:
                return (Color.FromArgb(2, 132, 199), "ℹ"); // Sky Blue

            case MessageBoxIcon.Question:
                return (Color.FromArgb(2, 132, 199), "?"); // Sky Blue

            case MessageBoxIcon.Warning:
                return (Color.FromArgb(217, 119, 6), "!"); // Amber Warning

            case MessageBoxIcon.Error:
                return (Color.FromArgb(220, 38, 38), "✕"); // Red Error

            default:
                if (combined.Contains("error") || combined.Contains("failed") || combined.Contains("exception"))
                    return (Color.FromArgb(220, 38, 38), "✕");
                if (combined.Contains("warn") || combined.Contains("caution") || combined.Contains("delete"))
                    return (Color.FromArgb(217, 119, 6), "!");
                if (combined.Contains("?") || combined.Contains("are you sure"))
                    return (Color.FromArgb(2, 132, 199), "?");

                return (Color.FromArgb(2, 132, 199), "ℹ");
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (_defaultButton == MessageBoxDefaultButton.Button2 && _btnSecondary != null && _btnSecondary.CanFocus)
        {
            _btnSecondary.Focus();
        }
        else if (_btnPrimary != null && _btnPrimary.CanFocus)
        {
            _btnPrimary.Focus();
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter)
        {
            if (_defaultButton == MessageBoxDefaultButton.Button2 && _btnSecondary != null)
            {
                DialogResult = _btnSecondary.DialogResult;
                Close();
                return true;
            }
            if (_btnPrimary != null)
            {
                DialogResult = _btnPrimary.DialogResult;
                Close();
                return true;
            }
        }

        if (keyData == Keys.Escape)
        {
            HandleCloseAction();
            return true;
        }

        if (keyData == Keys.Y && _buttons == MessageBoxButtons.YesNo)
        {
            DialogResult = DialogResult.Yes;
            Close();
            return true;
        }

        if (keyData == Keys.N && _buttons == MessageBoxButtons.YesNo)
        {
            DialogResult = DialogResult.No;
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void HandleCloseAction()
    {
        if (_buttons == MessageBoxButtons.YesNo)
        {
            DialogResult = DialogResult.No;
        }
        else if (_buttons == MessageBoxButtons.OKCancel || _buttons == MessageBoxButtons.YesNoCancel || _buttons == MessageBoxButtons.RetryCancel)
        {
            DialogResult = DialogResult.Cancel;
        }
        else
        {
            DialogResult = DialogResult.OK;
        }
        Close();
    }

    private void OnDialogKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            HandleCloseAction();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Y && _buttons == MessageBoxButtons.YesNo)
        {
            DialogResult = DialogResult.Yes;
            Close();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.N && _buttons == MessageBoxButtons.YesNo)
        {
            DialogResult = DialogResult.No;
            Close();
            e.Handled = true;
        }
    }

    private static GraphicsPath CreateTopRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddLine(rect.Right, rect.Bottom, rect.Left, rect.Bottom);
        path.CloseFigure();
        return path;
    }
}
