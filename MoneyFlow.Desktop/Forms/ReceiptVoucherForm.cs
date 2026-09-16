using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Navigation;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Receipt Voucher Form (F6).
/// Inherits from unified AccountingVoucherForm to support instant, in-place Tally-style voucher switching.
/// </summary>
public class ReceiptVoucherForm : AccountingVoucherForm
{
    public ReceiptVoucherForm(
        IAccountingService accountingService,
        ILedgerService ledgerService,
        IGroupService groupService,
        ICompanyContext companyContext,
        INavigationService? navigationService = null)
        : base(VoucherTypeEnum.Receipt, accountingService, ledgerService, groupService, companyContext, navigationService)
    {
    }
}
