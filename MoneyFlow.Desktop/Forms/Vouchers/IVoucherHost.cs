using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Controls;
using MoneyFlow.Desktop.Navigation;

namespace MoneyFlow.Desktop.Forms.Vouchers;

/// <summary>
/// Interface implemented by the master VoucherShell form to provide shell services to voucher views.
/// </summary>
public interface IVoucherHost
{
    ICompanyContext CompanyContext { get; }
    INavigationService? NavigationService { get; }
    TallyLedgerFlyoutPanel FlyoutPanel { get; }
    TallyTopHeaderBar TopHeaderBar { get; }
    TallySideActionBar SideActionBar { get; }

    /// <summary>
    /// Switches the active voucher view inside the existing window shell.
    /// Returns true if switch succeeded, false if aborted due to unsaved changes.
    /// </summary>
    Task<bool> SwitchVoucherAsync(VoucherTypeEnum targetType, bool bypassConfirm = false);

    /// <summary>
    /// Closes the voucher shell window.
    /// </summary>
    void CloseHost();

    /// <summary>
    /// Creates a standard styled action ribbon button.
    /// </summary>
    Guna.UI2.WinForms.Guna2Button CreateActionButton(string text, bool isPrimary, Action onClick);
}
