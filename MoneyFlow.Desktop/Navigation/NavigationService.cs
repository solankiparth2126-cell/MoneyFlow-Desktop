using System;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Dialogs;
using MoneyFlow.Desktop.Forms;

namespace MoneyFlow.Desktop.Navigation;

/// <summary>
/// Centralized navigation service that coordinates form creation, context preconditions, and user flows.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly IFormFactory _formFactory;
    private readonly ICompanyContext _companyContext;
    private readonly ICompanyService _companyService;
    private readonly IUserContext _userContext;

    public NavigationService(
        IFormFactory formFactory,
        ICompanyContext companyContext,
        ICompanyService companyService,
        IUserContext userContext)
    {
        _formFactory = formFactory ?? throw new ArgumentNullException(nameof(formFactory));
        _companyContext = companyContext ?? throw new ArgumentNullException(nameof(companyContext));
        _companyService = companyService ?? throw new ArgumentNullException(nameof(companyService));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
    }

    // --- Context Preconditions ---
    private bool EnsureCompanyOpen(IWin32Window? owner = null)
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            MessageBox.Show(owner, "Please select or create a company first.", "Company Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenCompanyList(owner);
            return false;
        }
        return true;
    }

    private bool EnsureCompanyAndFinancialYear(IWin32Window? owner = null)
    {
        if (!EnsureCompanyOpen(owner)) return false;

        if (_companyContext.CurrentFinancialYear == null)
        {
            MessageBox.Show(owner, "Please select or set a financial year first.", "Financial Year Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenFinancialYearList(owner);
            return false;
        }
        return true;
    }

    // --- Company & Session ---
    public void OpenCompanyList(IWin32Window? owner = null)
    {
        using var form = _formFactory.Create<CompanyListForm>();
        ShowModal(form, owner);
    }

    public void OpenCreateCompany(IWin32Window? owner = null)
    {
        using var form = _formFactory.Create<CompanyCreateEditForm>();
        ShowModal(form, owner);
    }

    public void OpenAlterCompany(IWin32Window? owner = null)
    {
        if (!EnsureCompanyOpen(owner)) return;

        using var form = _formFactory.Create<CompanyCreateEditForm>(_companyContext.CurrentCompany!.CompanyId);
        ShowModal(form, owner);
    }

    public void CloseActiveCompany(IWin32Window? owner = null)
    {
        if (!_companyContext.IsCompanyOpen)
        {
            MessageBox.Show(owner, "No company is currently open.", "Executive Ledger", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            owner,
            $"Are you sure you want to close company '{_companyContext.CurrentCompany?.CompanyName}'?",
            "Close Company",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm == DialogResult.Yes)
        {
            _companyService.CloseCompany();
        }
    }

    public void OpenFinancialYearList(IWin32Window? owner = null)
    {
        if (!EnsureCompanyOpen(owner)) return;

        using var form = _formFactory.Create<FinancialYearListForm>();
        ShowModal(form, owner);
    }

    public void OpenLoginForm(IWin32Window? owner = null, Action? onLoginSuccessful = null)
    {
        using var form = _formFactory.Create<LoginForm>();
        if (form.ShowDialog(owner) == DialogResult.OK)
        {
            onLoginSuccessful?.Invoke();
        }
    }

    // --- Masters ---
    public void OpenGroupList(IWin32Window? owner = null)
    {
        if (!EnsureCompanyOpen(owner)) return;
        using var form = _formFactory.Create<GroupListForm>();
        ShowModal(form, owner);
    }

    public void OpenLedgerList(IWin32Window? owner = null)
    {
        if (!EnsureCompanyOpen(owner)) return;
        using var form = _formFactory.Create<LedgerListForm>();
        ShowModal(form, owner);
    }



    // --- Transactions / Vouchers ---
    public void OpenContraVoucher(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        using var form = _formFactory.Create<ContraVoucherForm>();
        ShowModal(form, owner);
    }

    public void OpenPaymentVoucher(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        using var form = _formFactory.Create<PaymentVoucherForm>();
        ShowModal(form, owner);
    }

    public void OpenReceiptVoucher(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        using var form = _formFactory.Create<ReceiptVoucherForm>();
        ShowModal(form, owner);
    }

    public void OpenJournalVoucher(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        using var form = _formFactory.Create<JournalVoucherForm>();
        ShowModal(form, owner);
    }

    public void OpenSalesVoucher(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        using var form = _formFactory.Create<SalesVoucherForm>();
        ShowModal(form, owner);
    }

    public void OpenPurchaseVoucher(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        using var form = _formFactory.Create<PurchaseVoucherForm>();
        ShowModal(form, owner);
    }





    public void OpenDayBook(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        using var form = _formFactory.Create<DayBookForm>();
        ShowModal(form, owner);
    }

    public void OpenLedgerStatement(IWin32Window? owner = null, int? ledgerId = null)
    {
        if (!EnsureCompanyOpen(owner)) return;
        using var form = _formFactory.Create<LedgerStatementForm>();
        ShowModal(form, owner);
    }



    public void OpenProfitLoss(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        using var form = _formFactory.Create<ProfitLossForm>();
        ShowModal(form, owner);
    }

    public void OpenBalanceSheet(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        using var form = _formFactory.Create<BalanceSheetForm>();
        ShowModal(form, owner);
    }

    public void OpenCashBankBook(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        using var form = _formFactory.Create<CashBankBookForm>();
        ShowModal(form, owner);
    }

    public void OpenBankReconciliation(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        using var form = _formFactory.Create<BankReconciliationForm>();
        ShowModal(form, owner);
    }





    // --- Utilities & System ---
    public void OpenGlobalSearch(IWin32Window? owner = null)
    {
        using var searchForm = _formFactory.Create<GlobalSearchForm>(new Action<GlobalSearchResultDto>(r => HandleSearchResultNavigation(r, owner)));
        ShowModal(searchForm, owner);
    }

    public void OpenImportExport(IWin32Window? owner = null)
    {
        if (!EnsureCompanyOpen(owner)) return;
        using var form = _formFactory.Create<ImportExportForm>();
        ShowModal(form, owner);
    }

    public void OpenBackupRestore(IWin32Window? owner = null)
    {
        using var form = _formFactory.Create<BackupRestoreForm>();
        ShowModal(form, owner);
    }

    public void OpenUserManagement(IWin32Window? owner = null)
    {
        using var form = _formFactory.Create<UserManagementForm>();
        ShowModal(form, owner);
    }



    // --- Dynamic Routing ---
    public void HandleGatewaySelection(string selectedItem, IWin32Window? owner = null)
    {
        if (string.IsNullOrWhiteSpace(selectedItem) || selectedItem.StartsWith("-")) return;

        var text = selectedItem.Trim();

        if (text.Contains("Go To") || text.Contains("Search"))
        {
            OpenGlobalSearch(owner);
        }
        else if (text.Equals("Create", StringComparison.OrdinalIgnoreCase))
        {
            OpenLedgerList(owner);
        }
        else if (text.Equals("Alter", StringComparison.OrdinalIgnoreCase))
        {
            OpenAlterCompany(owner);
        }
        else if (text.Contains("Chart of Accounts"))
        {
            OpenGroupList(owner);
        }
        else if (text.Equals("Vouchers", StringComparison.OrdinalIgnoreCase))
        {
            OpenReceiptVoucher(owner);
        }
        else if (text.Equals("Banking", StringComparison.OrdinalIgnoreCase))
        {
            OpenCashBankBook(owner);
        }
        else if (text.Contains("Company Info"))
        {
            OpenCompanyList(owner);
        }
        else if (text.Contains("Groups"))
        {
            OpenGroupList(owner);
        }
        else if (text.Contains("Ledgers"))
        {
            OpenLedgerList(owner);
        }

        else if (text.Contains("Contra"))
        {
            OpenContraVoucher(owner);
        }
        else if (text.Contains("Payment"))
        {
            OpenPaymentVoucher(owner);
        }
        else if (text.Contains("Receipt"))
        {
            OpenReceiptVoucher(owner);
        }
        else if (text.Contains("Journal"))
        {
            OpenJournalVoucher(owner);
        }
        else if (text.Contains("Sales"))
        {
            OpenSalesVoucher(owner);
        }
        else if (text.Contains("Purchase"))
        {
            OpenPurchaseVoucher(owner);
        }


        else if (text.Contains("Day Book"))
        {
            OpenDayBook(owner);
        }
        else if (text.Contains("Ledger Statement"))
        {
            OpenLedgerStatement(owner);
        }

        else if (text.Contains("Profit & Loss"))
        {
            OpenProfitLoss(owner);
        }
        else if (text.Contains("Balance Sheet"))
        {
            OpenBalanceSheet(owner);
        }
        else if (text.Contains("Cash / Bank Book") || text.Contains("Cash Book") || text.Contains("Bank Book"))
        {
            OpenCashBankBook(owner);
        }
        else if (text.Contains("Bank Reconciliation") || text.Contains("Reconciliation") || text.Contains("BRS"))
        {
            OpenBankReconciliation(owner);
        }


        else if (text.Contains("Import") || text.Contains("Export"))
        {
            OpenImportExport(owner);
        }
        else if (text.Contains("Backup") || text.Contains("Restore"))
        {
            OpenBackupRestore(owner);
        }
        else if (text.Contains("User Management") || text.Contains("Security"))
        {
            OpenUserManagement(owner);
        }

        else if (text.Contains("Quit"))
        {
            Application.Exit();
        }
        else
        {
            if (!EnsureCompanyOpen(owner)) return;
            MessageBox.Show(
                owner,
                $"{text} is scheduled in upcoming phases per the Master Prompt.",
                "MoneyFlow Desktop Accounting",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
    }

    public void HandleSearchResultNavigation(GlobalSearchResultDto result, IWin32Window? owner = null)
    {
        if (result == null) return;

        switch (result.Category)
        {
            case GlobalSearchCategory.Navigation:
                switch (result.NavigationTarget)
                {
                    case "ImportExport": OpenImportExport(owner); break;
                    case "BackupRestore": OpenBackupRestore(owner); break;
                    case "UserManagement": OpenUserManagement(owner); break;
                    case "DayBook": OpenDayBook(owner); break;
                    case "ProfitLoss": OpenProfitLoss(owner); break;
                    case "BalanceSheet": OpenBalanceSheet(owner); break;
                    case "CashBankBook": OpenCashBankBook(owner); break;
                    case "Ledgers": OpenLedgerList(owner); break;
                    case "Groups": OpenGroupList(owner); break;
                    case "Contra": OpenContraVoucher(owner); break;
                    case "Payment": OpenPaymentVoucher(owner); break;
                    case "Receipt": OpenReceiptVoucher(owner); break;
                    case "Journal": OpenJournalVoucher(owner); break;
                    case "Sales": OpenSalesVoucher(owner); break;
                    case "Purchase": OpenPurchaseVoucher(owner); break;
                }
                break;

            case GlobalSearchCategory.Ledger:
                if (result.EntityId.HasValue)
                {
                    OpenLedgerStatement(owner, result.EntityId.Value);
                }
                break;

            case GlobalSearchCategory.StockItem:
                break;

            case GlobalSearchCategory.Voucher:
                if (result.EntityId.HasValue)
                {
                    MessageBox.Show(
                        owner,
                        $"{result.Title}\n{result.Subtitle}\nAmount: {result.FormattedAmount}",
                        "Voucher Quick View",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                break;
        }
    }

    private static void ShowModal(Form form, IWin32Window? owner)
    {
        if (owner != null) form.ShowDialog(owner);
        else form.ShowDialog();
    }
}
