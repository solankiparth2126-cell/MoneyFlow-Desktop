using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Controls;
using MoneyFlow.Desktop.Navigation;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Reusable master shell for all accounting voucher forms (F4 Contra, F5 Payment, F6 Receipt, F7 Journal, F8 Sales, F9 Purchase).
/// Enforces full-screen desktop ERP layout, right-side F-key panel, top header bar, ledger flyout, and consistent action ribbons.
/// </summary>
public abstract class BaseVoucherForm : Form
{
    protected readonly ICompanyContext CompanyContext;
    protected readonly INavigationService? NavigationService;

    // Shell Controls
    protected TallyTopHeaderBar TopHeaderBar = null!;
    protected TallySideActionBar SideActionBar = null!;
    protected TallyLedgerFlyoutPanel FlyoutPanel = null!;
    protected Panel CenterWorkPanel = null!;

    protected BaseVoucherForm(
        string voucherTitle,
        string activeVoucherKey,
        ICompanyContext companyContext,
        INavigationService? navigationService = null)
    {
        CompanyContext = companyContext ?? throw new ArgumentNullException(nameof(companyContext));
        NavigationService = navigationService;

        // Apply full-screen Executive Ledger child form styling
        ExecLedgerStyler.ApplyChildForm(this, voucherTitle);
        MinimumSize = new Size(950, 600);
        KeyPreview = true;

        InitializeVoucherShell(voucherTitle, activeVoucherKey);
    }

    private void InitializeVoucherShell(string voucherTitle, string activeVoucherKey)
    {
        // 1. Top Header Bar (32px Navy)
        TopHeaderBar = new TallyTopHeaderBar();
        TopHeaderBar.SetSubtitle(voucherTitle);
        TopHeaderBar.SetCompany(CompanyContext.CurrentCompany?.CompanyName ?? "Executive Ledger");
        TopHeaderBar.CloseRequested += () => Close();
        TopHeaderBar.CompanyMenuRequested += () => NavigationService?.OpenCompanyList(this);

        // 2. Right Side Action Bar (120px Vertical F-Key Panel)
        SideActionBar = new TallySideActionBar();
        SideActionBar.SetActiveVoucher(activeVoucherKey);
        SideActionBar.DateClicked += () => OnFocusDateRequested();
        SideActionBar.CompanyClicked += () => NavigationService?.OpenCompanyList(this);
        SideActionBar.ContraClicked += async () => await SwitchVoucherAsync(VoucherTypeEnum.Contra);
        SideActionBar.PaymentClicked += async () => await SwitchVoucherAsync(VoucherTypeEnum.Payment);
        SideActionBar.ReceiptClicked += async () => await SwitchVoucherAsync(VoucherTypeEnum.Receipt);
        SideActionBar.JournalClicked += async () => await SwitchVoucherAsync(VoucherTypeEnum.Journal);
        SideActionBar.SalesClicked += async () => await SwitchVoucherAsync(VoucherTypeEnum.Sales);
        SideActionBar.PurchaseClicked += async () => await SwitchVoucherAsync(VoucherTypeEnum.Purchase);
        SideActionBar.OtherClicked += () => ShowOtherVouchersMenu();

        // 3. Right Flyout (List of Ledger Accounts)
        FlyoutPanel = new TallyLedgerFlyoutPanel
        {
            Dock = DockStyle.Right,
            Visible = false
        };
        // CreateRequested: override OnFlyoutCreateRequested() to open LedgerCreateEditForm modally.
        FlyoutPanel.CreateRequested += () => OnFlyoutCreateRequested();
        // ShowMoreRequested: falls back to opening the full Ledger List screen.
        FlyoutPanel.ShowMoreRequested += () => OnFlyoutShowMoreRequested();

        // 4. Center Work Area (Fill)
        CenterWorkPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = ExecLedgerTheme.WorkSurface,
            Padding = new Padding(12, 8, 12, 4)
        };
    }

    /// <summary>
    /// Ensures that shell controls (Header, SideActionBar, Flyout, CenterWorkPanel) are mounted to the form.
    /// </summary>
    protected void EnsureShellMounted()
    {
        if (!Controls.Contains(TopHeaderBar))
        {
            Controls.Add(CenterWorkPanel);
            Controls.Add(FlyoutPanel);
            Controls.Add(SideActionBar);
            Controls.Add(TopHeaderBar);
        }
    }

    /// <summary>
    /// Mounts the voucher's custom layout inside the standard shell container and adds shell controls to the Form.
    /// </summary>
    protected void MountVoucherContent(Control contentControl)
    {
        contentControl.Dock = DockStyle.Fill;
        CenterWorkPanel.Controls.Add(contentControl);

        EnsureShellMounted();
    }

    /// <summary>
    /// Switches the active voucher. Overridden in unified AccountingVoucherForm to perform in-place instant switching.
    /// </summary>
    public virtual Task<bool> SwitchVoucherAsync(VoucherTypeEnum targetType)
    {
        Close();
        switch (targetType)
        {
            case VoucherTypeEnum.Contra: NavigationService?.OpenContraVoucher(); break;
            case VoucherTypeEnum.Payment: NavigationService?.OpenPaymentVoucher(); break;
            case VoucherTypeEnum.Receipt: NavigationService?.OpenReceiptVoucher(); break;
            case VoucherTypeEnum.Journal: NavigationService?.OpenJournalVoucher(); break;
            case VoucherTypeEnum.Sales: NavigationService?.OpenSalesVoucher(); break;
            case VoucherTypeEnum.Purchase: NavigationService?.OpenPurchaseVoucher(); break;
        }
        return Task.FromResult(true);
    }

    /// <summary>
    /// Creates a styled action ribbon button (Ctrl+A: Accept, Esc: Quit, Clear, Ctrl+P: Print).
    /// </summary>
    protected Guna2Button CreateActionButton(string text, bool isPrimary, Action onClick)
    {
        var btn = new Guna2Button
        {
            Text = text,
            Size = new Size(text.Length > 10 ? 116 : 84, 28),
            Font = isPrimary ? ExecLedgerTheme.UIBold8 : ExecLedgerTheme.UIRegular8,
            Cursor = Cursors.Hand,
            Margin = new Padding(3, 0, 3, 0),
            BorderRadius = ExecLedgerTheme.BorderRadius
        };

        if (isPrimary)
        {
            btn.FillColor = ExecLedgerTheme.PrimaryNavy;
            btn.ForeColor = ExecLedgerTheme.WhiteText;
            btn.BorderColor = ExecLedgerTheme.DeepNavy;
            btn.BorderThickness = 1;
            btn.HoverState.FillColor = ExecLedgerTheme.ButtonHover;
        }
        else
        {
            btn.FillColor = ExecLedgerTheme.WorkSurface;
            btn.ForeColor = ExecLedgerTheme.PrimaryText;
            btn.BorderColor = ExecLedgerTheme.PrimaryBorder;
            btn.BorderThickness = 1;
            btn.HoverState.FillColor = ExecLedgerTheme.MenuHover;
        }

        btn.Click += (s, e) => onClick();
        return btn;
    }

    protected virtual void OnFocusDateRequested() { }

    /// <summary>
    /// Called when the flyout "Create (Alt+C)" button is clicked.
    /// Override in subclasses to open LedgerCreateEditForm modally and refresh the flyout.
    /// Default: falls back to opening the full Ledger List screen.
    /// </summary>
    protected virtual void OnFlyoutCreateRequested()
    {
        NavigationService?.OpenLedgerList(this);
    }

    /// <summary>
    /// Called when the flyout "Show More" link is clicked.
    /// Default: opens the full Ledger List screen.
    /// </summary>
    protected virtual void OnFlyoutShowMoreRequested()
    {
        NavigationService?.OpenLedgerList(this);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        // Ensure form maximizes strictly inside the monitor's usable work area
        var screen = Screen.FromHandle(Handle);
        if (screen != null)
        {
            MaximizedBounds = screen.WorkingArea;
        }
        WindowState = FormWindowState.Maximized;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            if (FlyoutPanel.Visible)
            {
                FlyoutPanel.Visible = false;
                return true;
            }
            Close();
            return true;
        }
        if (keyData == Keys.F2) { OnFocusDateRequested(); return true; }
        if (keyData == Keys.F4) { _ = SwitchVoucherAsync(VoucherTypeEnum.Contra); return true; }
        if (keyData == Keys.F5) { _ = SwitchVoucherAsync(VoucherTypeEnum.Payment); return true; }
        if (keyData == Keys.F6) { _ = SwitchVoucherAsync(VoucherTypeEnum.Receipt); return true; }
        if (keyData == Keys.F7) { _ = SwitchVoucherAsync(VoucherTypeEnum.Journal); return true; }
        if (keyData == Keys.F8) { _ = SwitchVoucherAsync(VoucherTypeEnum.Sales); return true; }
        if (keyData == Keys.F9) { _ = SwitchVoucherAsync(VoucherTypeEnum.Purchase); return true; }
        if (keyData == Keys.F10) { ShowOtherVouchersMenu(); return true; }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>
    /// Displays a compact popup menu for Other Vouchers (F10) allowing instant switching to any voucher type.
    /// </summary>
    protected void ShowOtherVouchersMenu()
    {
        var menu = new ContextMenuStrip
        {
            Font = ExecLedgerTheme.UIRegular9,
            ShowImageMargin = false
        };

        menu.Items.Add("Contra (F4)", null, async (s, e) => await SwitchVoucherAsync(VoucherTypeEnum.Contra));
        menu.Items.Add("Payment (F5)", null, async (s, e) => await SwitchVoucherAsync(VoucherTypeEnum.Payment));
        menu.Items.Add("Receipt (F6)", null, async (s, e) => await SwitchVoucherAsync(VoucherTypeEnum.Receipt));
        menu.Items.Add("Journal (F7)", null, async (s, e) => await SwitchVoucherAsync(VoucherTypeEnum.Journal));
        menu.Items.Add("Sales (F8)", null, async (s, e) => await SwitchVoucherAsync(VoucherTypeEnum.Sales));
        menu.Items.Add("Purchase (F9)", null, async (s, e) => await SwitchVoucherAsync(VoucherTypeEnum.Purchase));

        var location = SideActionBar != null && SideActionBar.Visible
            ? SideActionBar.PointToScreen(new Point(0, Math.Min(230, SideActionBar.Height / 2)))
            : PointToScreen(new Point(Width / 2, Height / 2));

        menu.Show(location);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
    }
}
