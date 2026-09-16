using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Navigation;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Payment Voucher Form (F5).
/// Inherits from unified AccountingVoucherForm to support instant, in-place Tally-style voucher switching.
/// </summary>
public class PaymentVoucherForm : AccountingVoucherForm
{
    public PaymentVoucherForm(
        IAccountingService accountingService,
        ILedgerService ledgerService,
        IGroupService groupService,
        ICompanyContext companyContext,
        INavigationService? navigationService = null)
        : base(VoucherTypeEnum.Payment, accountingService, ledgerService, groupService, companyContext, navigationService)
    {
    }
}
