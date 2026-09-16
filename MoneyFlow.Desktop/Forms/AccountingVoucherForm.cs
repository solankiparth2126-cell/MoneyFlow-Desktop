using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Controls;
using MoneyFlow.Desktop.Forms.Vouchers;
using MoneyFlow.Desktop.Navigation;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Executive Ledger Desktop — Unified Accounting Voucher Form.
/// Acts as the single window / VoucherShell host enabling instant, in-place
/// Tally-style switching between Contra (F4), Payment (F5), Receipt (F6), Journal (F7),
/// Sales (F8), and Purchase (F9) without closing or reopening windows.
/// </summary>
public class AccountingVoucherForm : BaseVoucherForm, IVoucherHost
{
    private readonly IAccountingService _accountingService;
    private readonly ILedgerService? _ledgerService;
    private readonly IGroupService? _groupService;

    private IVoucherView? _currentView;

    /// <summary>
    /// The currently active embedded voucher view.
    /// </summary>
    public IVoucherView? CurrentView => _currentView;

    // IVoucherHost explicit implementation & delegations
    ICompanyContext IVoucherHost.CompanyContext => CompanyContext;
    INavigationService? IVoucherHost.NavigationService => NavigationService;
    TallyLedgerFlyoutPanel IVoucherHost.FlyoutPanel => FlyoutPanel;
    TallyTopHeaderBar IVoucherHost.TopHeaderBar => TopHeaderBar;
    TallySideActionBar IVoucherHost.SideActionBar => SideActionBar;

    public AccountingVoucherForm(
        VoucherTypeEnum initialType,
        IAccountingService accountingService,
        ILedgerService? ledgerService,
        IGroupService? groupService,
        ICompanyContext companyContext,
        INavigationService? navigationService = null)
        : base(GetTitleForType(initialType), GetActiveKeyForType(initialType), companyContext, navigationService)
    {
        _accountingService = accountingService ?? throw new ArgumentNullException(nameof(accountingService));
        _ledgerService = ledgerService;
        _groupService = groupService;

        EnsureShellMounted();
        _ = SwitchVoucherAsync(initialType, bypassConfirm: true);
    }

    public AccountingVoucherForm(
        IAccountingService accountingService,
        ILedgerService ledgerService,
        IGroupService groupService,
        ICompanyContext companyContext,
        INavigationService? navigationService = null)
        : this(VoucherTypeEnum.Payment, accountingService, ledgerService, groupService, companyContext, navigationService)
    {
    }

    public void CloseHost() => Close();

    Guna2Button IVoucherHost.CreateActionButton(string text, bool isPrimary, Action onClick)
        => CreateActionButton(text, isPrimary, onClick);

    public override async Task<bool> SwitchVoucherAsync(VoucherTypeEnum targetType)
    {
        return await SwitchVoucherAsync(targetType, bypassConfirm: false);
    }

    /// <summary>
    /// Centralized navigation method for switching voucher types in-place.
    /// Used by both F-key handlers and mouse clicks on the right-side action bar.
    /// </summary>
    public async Task<bool> SwitchVoucherAsync(VoucherTypeEnum targetType, bool bypassConfirm = false)
    {
        if (_currentView != null && _currentView.VoucherType == targetType)
        {
            return true; // Already displaying this voucher type
        }

        // Check for unsaved accounting changes before switching
        if (!bypassConfirm && _currentView != null && _currentView.HasUnsavedChanges())
        {
            var result = MessageBox.Show(
                $"You have unsaved changes in the current {_currentView.VoucherTitle}.\nSwitching voucher type will discard these changes.\n\nDo you want to discard your changes and switch to {GetTitleForType(targetType)}?",
                "Discard Changes?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                return false;
            }
        }

        // Create the requested voucher view
        var newView = CreateViewForType(targetType);

        // Instant in-place transition within the same window and layout
        SuspendLayout();
        CenterWorkPanel.SuspendLayout();
        try
        {
            CenterWorkPanel.Controls.Clear();

            _currentView = newView;
            _currentView.ContentControl.Dock = DockStyle.Fill;
            CenterWorkPanel.Controls.Add(_currentView.ContentControl);

            // Update top header bar subtitle and window title
            TopHeaderBar.SetSubtitle(_currentView.VoucherTitle);
            Text = _currentView.VoucherTitle;

            // Update highlighted shortcut button on side action bar
            SideActionBar.SetActiveVoucher(_currentView.ActiveVoucherKey);
        }
        finally
        {
            CenterWorkPanel.ResumeLayout(true);
            ResumeLayout(true);
        }

        // Initialize view data asynchronously
        await _currentView.InitializeAsync();
        _currentView.FocusDefault();

        return true;
    }

    private IVoucherView CreateViewForType(VoucherTypeEnum type)
    {
        return type switch
        {
            VoucherTypeEnum.Contra => new ContraVoucherView(this, _accountingService, _ledgerService ?? throw new InvalidOperationException("ILedgerService is required for Contra voucher.")),
            VoucherTypeEnum.Payment => new PaymentVoucherView(this, _accountingService, _ledgerService ?? throw new InvalidOperationException("ILedgerService is required for Payment voucher.")),
            VoucherTypeEnum.Receipt => new ReceiptVoucherView(this, _accountingService, _ledgerService ?? throw new InvalidOperationException("ILedgerService is required for Receipt voucher.")),
            VoucherTypeEnum.Journal => new JournalVoucherView(this, _accountingService, _ledgerService ?? throw new InvalidOperationException("ILedgerService is required for Journal voucher.")),
            VoucherTypeEnum.Sales => new SalesVoucherView(this, _accountingService),
            VoucherTypeEnum.Purchase => new PurchaseVoucherView(this, _accountingService),
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported voucher type: {type}")
        };
    }

    public static string GetTitleForType(VoucherTypeEnum type) => type switch
    {
        VoucherTypeEnum.Contra => "Contra Voucher (F4)",
        VoucherTypeEnum.Payment => "Payment Voucher (F5)",
        VoucherTypeEnum.Receipt => "Receipt Voucher (F6)",
        VoucherTypeEnum.Journal => "Journal Voucher (F7)",
        VoucherTypeEnum.Sales => "Sales Voucher (F8)",
        VoucherTypeEnum.Purchase => "Purchase Voucher (F9)",
        _ => "Accounting Voucher"
    };

    public static string GetActiveKeyForType(VoucherTypeEnum type) => type switch
    {
        VoucherTypeEnum.Contra => "Contra",
        VoucherTypeEnum.Payment => "Payment",
        VoucherTypeEnum.Receipt => "Receipt",
        VoucherTypeEnum.Journal => "Journal",
        VoucherTypeEnum.Sales => "Sales",
        VoucherTypeEnum.Purchase => "Purchase",
        _ => ""
    };

    protected override void OnFocusDateRequested()
    {
        _currentView?.FocusDate();
    }

    /// <summary>
    /// Opens LedgerCreateEditForm modally. On success, refreshes the flyout with the full
    /// ledger list so the new ledger is immediately available for voucher selection.
    /// </summary>
    protected override void OnFlyoutCreateRequested()
    {
        if (_ledgerService == null || _groupService == null || CompanyContext.CurrentCompany == null)
        {
            NavigationService?.OpenLedgerList(this);
            return;
        }

        int companyId = CompanyContext.CurrentCompany.CompanyId;

        using var dlg = new LedgerCreateEditForm(_ledgerService, _groupService, companyId);
        var result = dlg.ShowDialog(this);

        if (result == DialogResult.OK)
        {
            // Reload the ledger list and refresh the flyout asynchronously on the UI thread
            _ = RefreshFlyoutAfterCreateAsync(companyId);
        }
    }

    private async System.Threading.Tasks.Task RefreshFlyoutAfterCreateAsync(int companyId)
    {
        try
        {
            if (_ledgerService == null) return;

            var updatedLedgers = await _ledgerService.GetLedgersByCompanyAsync(companyId);

            // Re-show and refresh the flyout so the user can immediately select the new ledger
            FlyoutPanel.RefreshWith(updatedLedgers);
            FlyoutPanel.Visible = true;
        }
        catch
        {
            // Non-fatal: flyout will still work with its prior list
        }
    }

    /// <summary>
    /// "Show More" opens the full Ledger Master list.
    /// </summary>
    protected override void OnFlyoutShowMoreRequested()
    {
        NavigationService?.OpenLedgerList(this);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == (Keys.Control | Keys.A) ||
            keyData == (Keys.Alt | Keys.S) ||
            keyData == (Keys.Alt | Keys.N) ||
            keyData == (Keys.Control | Keys.P))
        {
            var buttons = GetAllControlsRecurse<Guna2Button>(this).ToList();
            if (keyData == (Keys.Control | Keys.A))
            {
                var btn = buttons.FirstOrDefault(b => b.Text.Contains("Accept", StringComparison.OrdinalIgnoreCase));
                if (btn != null && btn.Enabled) { btn.PerformClick(); return true; }
            }
            else if (keyData == (Keys.Alt | Keys.S))
            {
                var btn = buttons.FirstOrDefault(b => b.Text.Contains("Save & New", StringComparison.OrdinalIgnoreCase) || b.Text.Contains("Alt+S", StringComparison.OrdinalIgnoreCase));
                if (btn != null && btn.Enabled) { btn.PerformClick(); return true; }
            }
            else if (keyData == (Keys.Alt | Keys.N))
            {
                var btn = buttons.FirstOrDefault(b => b.Text.Equals("Clear", StringComparison.OrdinalIgnoreCase) || b.Text.Contains("Alt+N", StringComparison.OrdinalIgnoreCase));
                if (btn != null && btn.Enabled) { btn.PerformClick(); return true; }
            }
            else if (keyData == (Keys.Control | Keys.P))
            {
                var btn = buttons.FirstOrDefault(b => b.Text.Contains("Print", StringComparison.OrdinalIgnoreCase));
                if (btn != null && btn.Enabled) { btn.PerformClick(); return true; }
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing && _currentView != null && _currentView.HasUnsavedChanges())
        {
            var result = MessageBox.Show(
                $"You have unsaved changes in {_currentView.VoucherTitle}.\nDo you want to discard your changes and quit?",
                "Discard Changes?",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (result != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
        }
        base.OnFormClosing(e);
    }

    private static IEnumerable<T> GetAllControlsRecurse<T>(Control parent) where T : Control
    {
        foreach (Control child in parent.Controls)
        {
            if (child is T typed)
                yield return typed;

            foreach (var grandChild in GetAllControlsRecurse<T>(child))
                yield return grandChild;
        }
    }
}
