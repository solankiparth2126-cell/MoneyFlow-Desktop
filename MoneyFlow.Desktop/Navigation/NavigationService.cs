using System;
using System.Drawing;
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

    private INavigationHost? _host;

    public void RegisterHost(INavigationHost host)
    {
        _host = host;
    }

    public void UnregisterHost(INavigationHost host)
    {
        if (_host == host) _host = null;
    }

    public void NavigateToGateway()
    {
        _host?.ReturnToGateway();
        ActiveModuleChanged?.Invoke("Gateway");
    }

    public bool NavigateBack()
    {
        var handled = _host?.NavigateBack() ?? false;
        if (handled)
        {
            ActiveModuleChanged?.Invoke(_host?.CurrentModuleKey ?? "Gateway");
        }
        return handled;
    }

    public string CurrentModuleKey => _host?.CurrentModuleKey ?? "Gateway";

    public event Action<string>? ActiveModuleChanged;

    private void NavigateOrModal(string moduleKey, string moduleTitle, Func<Form> formFactory, IWin32Window? owner)
    {
        if (_host != null)
        {
            _host.ShowInWorkspace(formFactory, moduleKey, moduleTitle);
            ActiveModuleChanged?.Invoke(moduleKey);
        }
        else
        {
            using var form = formFactory();
            ShowModal(form, owner);
        }
    }

    // --- Masters ---
    public void OpenGroupList(IWin32Window? owner = null)
    {
        if (!EnsureCompanyOpen(owner)) return;
        NavigateOrModal("Groups", "Group Master", () => _formFactory.Create<GroupListForm>(), owner);
    }

    public void OpenLedgerList(IWin32Window? owner = null)
    {
        if (!EnsureCompanyOpen(owner)) return;
        NavigateOrModal("Ledgers", "Ledgers Master", () => _formFactory.Create<LedgerListForm>(), owner);
    }

    // --- Transactions / Vouchers ---
    public void OpenContraVoucher(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        NavigateOrModal("Contra", "Contra Voucher", () => _formFactory.Create<ContraVoucherForm>(), owner);
    }

    public void OpenPaymentVoucher(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        NavigateOrModal("Payment", "Payment Voucher", () => _formFactory.Create<PaymentVoucherForm>(), owner);
    }

    public void OpenReceiptVoucher(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        NavigateOrModal("Receipt", "Receipt Voucher", () => _formFactory.Create<ReceiptVoucherForm>(), owner);
    }

    public void OpenJournalVoucher(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        NavigateOrModal("Journal", "Journal Voucher", () => _formFactory.Create<JournalVoucherForm>(), owner);
    }

    public void OpenSalesVoucher(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        NavigateOrModal("Sales", "Sales Voucher", () => _formFactory.Create<SalesVoucherForm>(), owner);
    }

    public void OpenPurchaseVoucher(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        NavigateOrModal("Purchase", "Purchase Voucher", () => _formFactory.Create<PurchaseVoucherForm>(), owner);
    }

    public void OpenDayBook(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        NavigateOrModal("DayBook", "Day Book", () => _formFactory.Create<DayBookForm>(), owner);
    }

    public void OpenLedgerStatement(IWin32Window? owner = null, int? ledgerId = null)
    {
        if (!EnsureCompanyOpen(owner)) return;
        NavigateOrModal("LedgerStatement", "Ledger Accounts", () => ledgerId.HasValue ? _formFactory.Create<LedgerStatementForm>(ledgerId.Value) : _formFactory.Create<LedgerStatementForm>(), owner);
    }

    public void OpenProfitLoss(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        NavigateOrModal("ProfitLoss", "Profit & Loss", () => _formFactory.Create<ProfitLossForm>(), owner);
    }

    public void OpenBalanceSheet(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        NavigateOrModal("BalanceSheet", "Balance Sheet", () => _formFactory.Create<BalanceSheetForm>(), owner);
    }

    public void OpenCashBankBook(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        NavigateOrModal("CashBankBook", "Cash & Bank Book", () => _formFactory.Create<CashBankBookForm>(), owner);
    }

    public void OpenBankReconciliation(IWin32Window? owner = null)
    {
        if (!EnsureCompanyAndFinancialYear(owner)) return;
        NavigateOrModal("BankReconciliation", "Bank Reconciliation", () => _formFactory.Create<BankReconciliationForm>(), owner);
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

    public void OpenStartupConfiguration(IWin32Window? owner = null)
    {
        using var form = _formFactory.Create<StartupConfigurationForm>();
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
        FitFormToScreen(form, owner);
        if (owner != null) form.ShowDialog(owner);
        else form.ShowDialog();
    }

    private static void FitFormToScreen(Form form, IWin32Window? owner)
    {
        try
        {
            var screen = owner is Control c ? Screen.FromControl(c) : (Screen.FromControl(form) ?? Screen.PrimaryScreen);
            if (screen == null) return;

            var workArea = screen.WorkingArea;

            // Full-screen desktop ERP voucher forms open maximized within the usable work area
            if (form.WindowState == FormWindowState.Maximized || form is BaseVoucherForm)
            {
                form.WindowState = FormWindowState.Maximized;
                return;
            }

            int maxW = Math.Max(600, workArea.Width - 32);
            int maxH = Math.Max(400, workArea.Height - 48);

            if (form.Width > maxW || form.Height > maxH)
            {
                form.Width = Math.Min(form.Width, maxW);
                form.Height = Math.Min(form.Height, maxH);
                form.AutoScroll = true;
            }

            if (owner is Form ownerForm && ownerForm.WindowState != FormWindowState.Minimized)
            {
                form.StartPosition = FormStartPosition.Manual;
                int targetX = Math.Clamp(ownerForm.Left + (ownerForm.Width - form.Width) / 2, workArea.Left, Math.Max(workArea.Left, workArea.Right - form.Width));
                int targetY = Math.Clamp(ownerForm.Top + (ownerForm.Height - form.Height) / 2, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - form.Height));
                form.Location = new Point(targetX, targetY);
            }
            else
            {
                form.StartPosition = FormStartPosition.CenterScreen;
            }
        }
        catch
        {
            // Fallback non-blocking
        }
    }
}
