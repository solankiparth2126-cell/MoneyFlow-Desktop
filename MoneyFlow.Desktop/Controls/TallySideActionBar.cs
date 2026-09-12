using System;
using System.Drawing;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Controls;

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
        Width = 110;
        Dock = DockStyle.Right;
        BackColor = TallyPrimeTheme.RightSidebarBg;
        BorderStyle = BorderStyle.None;

        _pnlButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = false,
            Padding = new Padding(2, 2, 2, 2)
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

        AddButton("F2: Date", () => DateClicked?.Invoke());
        AddButton("F3: Company", () => CompanyClicked?.Invoke());
        AddSpacer();

        AddVoucherButton("F4: Contra", "Contra", () => ContraClicked?.Invoke());
        AddVoucherButton("F5: Payment", "Payment", () => PaymentClicked?.Invoke());
        AddVoucherButton("F6: Receipt", "Receipt", () => ReceiptClicked?.Invoke());
        AddVoucherButton("F7: Journal", "Journal", () => JournalClicked?.Invoke());
        AddVoucherButton("F8: Sales", "Sales", () => SalesClicked?.Invoke());
        AddVoucherButton("F9: Purchase", "Purchase", () => PurchaseClicked?.Invoke());
        AddButton("F10: Other", null);

        AddSpacer();
        AddButton("H: Mode", null);
        AddButton("!: Details", null);
        AddButton("Q: Reports", null);

        AddSpacer();
        AddButton("F12: Config", () => ConfigureClicked?.Invoke());
    }

    private void AddVoucherButton(string text, string voucherName, Action? onClick)
    {
        bool isActive = string.Equals(_activeVoucher, voucherName, StringComparison.OrdinalIgnoreCase);

        var btn = new Button
        {
            Text = text,
            Width = 104,
            Height = 27,
            Margin = new Padding(1, 1, 1, 1),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5F, isActive ? FontStyle.Bold : FontStyle.Regular),
            BackColor = isActive ? TallyPrimeTheme.RightButtonActive : TallyPrimeTheme.RightButtonBg,
            ForeColor = isActive ? Color.FromArgb(0, 56, 101) : Color.FromArgb(40, 60, 80),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = isActive ? Color.FromArgb(0, 90, 156) : TallyPrimeTheme.RightSidebarBorder;
        btn.FlatAppearance.BorderSize = 1;

        if (onClick != null)
        {
            btn.Click += (s, e) => onClick();
        }

        _pnlButtons.Controls.Add(btn);
    }

    private void AddButton(string text, Action? onClick)
    {
        var btn = new Button
        {
            Text = text,
            Width = 104,
            Height = 27,
            Margin = new Padding(1, 1, 1, 1),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 8.5F),
            BackColor = TallyPrimeTheme.RightButtonBg,
            ForeColor = Color.FromArgb(40, 60, 80),
            TextAlign = ContentAlignment.MiddleLeft,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = TallyPrimeTheme.RightSidebarBorder;
        btn.FlatAppearance.BorderSize = 1;

        if (onClick != null)
        {
            btn.Click += (s, e) => onClick();
        }

        _pnlButtons.Controls.Add(btn);
    }

    private void AddSpacer()
    {
        var pnl = new Panel
        {
            Width = 104,
            Height = 4,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 2, 0, 2)
        };
        _pnlButtons.Controls.Add(pnl);
    }
}
