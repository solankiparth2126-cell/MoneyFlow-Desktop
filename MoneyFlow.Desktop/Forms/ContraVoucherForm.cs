using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Navigation;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Contra Voucher Form (F4).
/// Inherits from unified AccountingVoucherForm to support instant, in-place Tally-style voucher switching.
/// </summary>
public class ContraVoucherForm : AccountingVoucherForm
{
    public ContraVoucherForm(
        IAccountingService accountingService,
        ILedgerService ledgerService,
        IGroupService groupService,
        ICompanyContext companyContext,
        INavigationService? navigationService = null)
        : base(VoucherTypeEnum.Contra, accountingService, ledgerService, groupService, companyContext, navigationService)
    {
    }
}
