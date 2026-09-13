using System;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Controls;

/// <summary>
/// Executive Ledger side action bar — right-docked panel with F-key shortcut buttons.
/// Sharp rectangular buttons with keyboard hints, grouped by function.
/// </summary>
public class TallySideActionBar : UserControl
{
    public event Action? DateClicked;
    public event Action? CompanyClicked;
    public event Action? ContraClicked;
    public event Action? PaymentClicked;
    public event Action? ReceiptClicked;
    public event Action? JournalClicked;
    public event Action? SalesClicked;
    public event Action? PurchaseClicked;
    public event Action? ConfigureClicked;

    private readonly FlowLayoutPanel _pnlButtons;
    private string _activeVoucher = "Receipt";

    public TallySideActionBar()
    {
        Width = 120;
        Dock = DockStyle.Right;
        BackColor = ExecLedgerTheme.ApplicationCanvas;
        BorderStyle = BorderStyle.None;

        // Left border
        Paint += (s, e) =>
        {
            using var pen = new Pen(ExecLedgerTheme.PrimaryBorder, 1);
            e.Graphics.DrawLine(pen, 0, 0, 0, Height);
        };

        _pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding(6, 6, 4, 4)
        };

        Controls.Add(_pnlButtons);
        RebuildButtons();
    }

    public void SetActiveVoucher(string voucherName)
    {
        _activeVoucher = voucherName;
        RebuildButtons();
    }

    private void RebuildButtons()
    {
        _pnlButtons.Controls.Clear();

        AddButton("F2: Date", () => DateClicked?.Invoke(), false);
        AddButton("F3: Company", () => CompanyClicked?.Invoke(), false);
        AddSpacer();

        AddVoucherButton("F4: Contra", "Contra", () => ContraClicked?.Invoke());
        AddVoucherButton("F5: Payment", "Payment", () => PaymentClicked?.Invoke());
        AddVoucherButton("F6: Receipt", "Receipt", () => ReceiptClicked?.Invoke());
        AddVoucherButton("F7: Journal", "Journal", () => JournalClicked?.Invoke());
        AddVoucherButton("F8: Sales", "Sales", () => SalesClicked?.Invoke());
        AddVoucherButton("F9: Purchase", "Purchase", () => PurchaseClicked?.Invoke());
        AddButton("F10: Other", null, false);

        AddSpacer();
        AddButton("I: Details", null, false);
        AddButton("Q: Reports", null, false);

        AddSpacer();
        AddButton("F12: Config", () => ConfigureClicked?.Invoke(), false);
    }

    private void AddVoucherButton(string text, string voucherName, Action? onClick)
    {
        bool isActive = string.Equals(_activeVoucher, voucherName, StringComparison.OrdinalIgnoreCase);
        AddButton(text, onClick, isActive);
    }

    private void AddButton(string text, Action? onClick, bool isActive)
    {
        var btn = new Guna2Button
        {
            Text = text,
            Size = new Size(108, ExecLedgerTheme.ToolbarButtonHeight),
            Margin = new Padding(0, 1, 0, 1),
            BorderRadius = ExecLedgerTheme.BorderRadius,
            TextAlign = HorizontalAlignment.Left,
            Font = isActive ? ExecLedgerTheme.UIBold8 : ExecLedgerTheme.UIRegular8,
            Cursor = Cursors.Hand
        };

        if (isActive)
        {
            btn.FillColor = ExecLedgerTheme.PrimaryNavy;
            btn.ForeColor = ExecLedgerTheme.WhiteText;
            btn.BorderColor = ExecLedgerTheme.DeepNavy;
            btn.BorderThickness = 1;
            btn.HoverState.FillColor = ExecLedgerTheme.ButtonHover;
            btn.HoverState.ForeColor = ExecLedgerTheme.WhiteText;
        }
        else
        {
            btn.FillColor = ExecLedgerTheme.WorkSurface;
            btn.ForeColor = ExecLedgerTheme.PrimaryText;
            btn.BorderColor = ExecLedgerTheme.PrimaryBorder;
            btn.BorderThickness = 1;
            btn.HoverState.FillColor = ExecLedgerTheme.MenuHover;
            btn.HoverState.ForeColor = ExecLedgerTheme.PrimaryText;
            btn.HoverState.BorderColor = ExecLedgerTheme.SteelBlue;
        }

        if (onClick != null)
            btn.Click += (s, e) => onClick();

        _pnlButtons.Controls.Add(btn);
    }

    private void AddSpacer()
    {
        var pnl = new Panel
        {
            Width = 108,
            Height = 4,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 2, 0, 2)
        };
        _pnlButtons.Controls.Add(pnl);
    }
}
