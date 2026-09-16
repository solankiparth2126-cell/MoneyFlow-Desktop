using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.Enums;

namespace MoneyFlow.Desktop.Forms.Vouchers;

/// <summary>
/// Defines the contract for an accounting voucher view hosted inside the unified VoucherShell.
/// </summary>
public interface IVoucherView
{
    VoucherTypeEnum VoucherType { get; }
    string VoucherTitle { get; }
    string ActiveVoucherKey { get; }
    Control ContentControl { get; }

    /// <summary>
    /// Checks whether the user has entered unposted accounting data or modified fields.
    /// </summary>
    bool HasUnsavedChanges();

    /// <summary>
    /// Asynchronously initializes or refreshes voucher data (numbering, ledger lists, balances).
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Sets keyboard focus to the voucher's default primary input control.
    /// </summary>
    void FocusDefault();

    /// <summary>
    /// Sets keyboard focus to the voucher's date picker control.
    /// </summary>
    void FocusDate();
}
