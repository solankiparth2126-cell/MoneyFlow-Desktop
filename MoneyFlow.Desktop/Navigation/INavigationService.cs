using System.Windows.Forms;
using MoneyFlow.Core.DTOs;

namespace MoneyFlow.Desktop.Navigation;

/// <summary>
/// Centralized navigation coordinator decoupling UI forms and menus from child form construction and domain services.
/// </summary>
public interface INavigationService
{
    // Company & Session
    void OpenCompanyList(IWin32Window? owner = null);
    void OpenCreateCompany(IWin32Window? owner = null);
    void OpenAlterCompany(IWin32Window? owner = null);
    void CloseActiveCompany(IWin32Window? owner = null);
    void OpenFinancialYearList(IWin32Window? owner = null);
    void OpenLoginForm(IWin32Window? owner = null, Action? onLoginSuccessful = null);

    // Masters
    void OpenGroupList(IWin32Window? owner = null);
    void OpenLedgerList(IWin32Window? owner = null);

    // Transactions / Vouchers
    void OpenContraVoucher(IWin32Window? owner = null);
    void OpenPaymentVoucher(IWin32Window? owner = null);
    void OpenReceiptVoucher(IWin32Window? owner = null);
    void OpenJournalVoucher(IWin32Window? owner = null);
    void OpenSalesVoucher(IWin32Window? owner = null);
    void OpenPurchaseVoucher(IWin32Window? owner = null);

    // Reports
    void OpenDayBook(IWin32Window? owner = null);
    void OpenLedgerStatement(IWin32Window? owner = null, int? ledgerId = null);
    void OpenProfitLoss(IWin32Window? owner = null);
    void OpenBalanceSheet(IWin32Window? owner = null);
    void OpenCashBankBook(IWin32Window? owner = null);
    void OpenBankReconciliation(IWin32Window? owner = null);

    // Utilities & System
    void OpenGlobalSearch(IWin32Window? owner = null);
    void OpenImportExport(IWin32Window? owner = null);
    void OpenBackupRestore(IWin32Window? owner = null);
    void OpenUserManagement(IWin32Window? owner = null);

    // Dynamic Routing
    void HandleGatewaySelection(string selectedItem, IWin32Window? owner = null);
    void HandleSearchResultNavigation(GlobalSearchResultDto result, IWin32Window? owner = null);
}
