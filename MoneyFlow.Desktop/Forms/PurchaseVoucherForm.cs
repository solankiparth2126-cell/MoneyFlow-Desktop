using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Navigation;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Purchase Voucher Form (F9).
/// Inherits from unified AccountingVoucherForm to support instant, in-place Tally-style voucher switching.
/// </summary>
public class PurchaseVoucherForm : AccountingVoucherForm
{
    public PurchaseVoucherForm(
        IAccountingService accountingService,
        ILedgerService ledgerService,
        IGroupService groupService,
        ICompanyContext companyContext,
        INavigationService? navigationService = null)
        : base(VoucherTypeEnum.Purchase, accountingService, ledgerService, groupService, companyContext, navigationService)
    {
    }

    public PurchaseVoucherForm(
        IAccountingService accountingService,
        IGroupService? groupService,
        ICompanyContext companyContext,
        INavigationService? navigationService = null)
        : base(VoucherTypeEnum.Purchase, accountingService, null, groupService, companyContext, navigationService)
    {
    }
}
