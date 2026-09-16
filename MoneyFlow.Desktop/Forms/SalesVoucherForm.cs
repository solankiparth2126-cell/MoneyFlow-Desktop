using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Navigation;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Sales Voucher Form (F8).
/// Inherits from unified AccountingVoucherForm to support instant, in-place Tally-style voucher switching.
/// </summary>
public class SalesVoucherForm : AccountingVoucherForm
{
    public SalesVoucherForm(
        IAccountingService accountingService,
        ILedgerService ledgerService,
        IGroupService groupService,
        ICompanyContext companyContext,
        INavigationService? navigationService = null)
        : base(VoucherTypeEnum.Sales, accountingService, ledgerService, groupService, companyContext, navigationService)
    {
    }

    public SalesVoucherForm(
        IAccountingService accountingService,
        IGroupService? groupService,
        ICompanyContext companyContext,
        INavigationService? navigationService = null)
        : base(VoucherTypeEnum.Sales, accountingService, null, groupService, companyContext, navigationService)
    {
    }
}
