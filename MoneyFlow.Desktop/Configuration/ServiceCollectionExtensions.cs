using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;
using MoneyFlow.Data.Repositories;
using MoneyFlow.Desktop.Dialogs;
using MoneyFlow.Desktop.Forms;
using MoneyFlow.Desktop.Navigation;
using MoneyFlow.Services;
using MoneyFlow.Services.Accounting;
using MoneyFlow.Services.Backup;
using MoneyFlow.Services.Company;
using MoneyFlow.Services.Dashboard;
using MoneyFlow.Services.FinancialYear;
using MoneyFlow.Services.Group;
using MoneyFlow.Services.ImportExport;
using MoneyFlow.Services.Inventory;
using MoneyFlow.Services.Ledger;
using MoneyFlow.Services.Search;
using MoneyFlow.Services.Security;
using MoneyFlow.Services.Settings;

namespace MoneyFlow.Desktop.Configuration;

/// <summary>
/// Modular DI service registration extensions separating database, data access, domain services, and UI components.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? @"Server=.\SQLEXPRESS;Database=MoneyFlowDB;Trusted_Connection=True;TrustServerCertificate=True;";

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IDatabaseSetupService, DatabaseSetupService>();

        return services;
    }

    public static IServiceCollection AddDataRepositories(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IFinancialYearRepository, FinancialYearRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<ILedgerRepository, LedgerRepository>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();

        return services;
    }

    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        // Company & Session
        services.AddSingleton<ICompanyContext, CompanyContext>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<ICompanySplitService, CompanySplitService>();

        // Financial Year
        services.AddScoped<IFinancialYearService, FinancialYearService>();

        // Master Data
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<IInventoryService, InventoryService>();

        // Accounting Engine & Operations
        services.AddScoped<IAccountingHierarchyService, AccountingHierarchyService>();
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<IBillAllocationService, BillAllocationService>();
        services.AddScoped<IBankReconciliationService, BankReconciliationService>();

        // Search & Analytics
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IDashboardService, DashboardService>();

        // Utilities
        services.AddScoped<IImportExportService, ImportExportService>();
        services.AddScoped<IBackupRestoreService, BackupRestoreService>();

        // Security & User System
        services.AddSingleton<IUserContext, UserContext>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISecurityService, SecurityService>();

        // Settings
        services.AddScoped<ISettingsService, SettingsService>();

        return services;
    }

    public static IServiceCollection AddNavigationServices(this IServiceCollection services)
    {
        services.AddSingleton<IFormFactory, FormFactory>();
        services.AddScoped<INavigationService, NavigationService>();

        return services;
    }

    public static IServiceCollection AddDesktopForms(this IServiceCollection services)
    {
        services.AddTransient<MainForm>();
        services.AddTransient<DatabaseConnectionDialog>();
        services.AddTransient<CompanyListForm>();
        services.AddTransient<CompanyCreateEditForm>();
        services.AddTransient<FinancialYearListForm>();
        services.AddTransient<GroupListForm>();
        services.AddTransient<LedgerListForm>();
        services.AddTransient<AccountingVoucherForm>();
        services.AddTransient<PaymentVoucherForm>();
        services.AddTransient<ReceiptVoucherForm>();
        services.AddTransient<ContraVoucherForm>();
        services.AddTransient<JournalVoucherForm>();
        services.AddTransient<SalesVoucherForm>();
        services.AddTransient<PurchaseVoucherForm>();
        services.AddTransient<DayBookForm>();
        services.AddTransient<LedgerStatementForm>();
        services.AddTransient<ProfitLossForm>();
        services.AddTransient<BalanceSheetForm>();
        services.AddTransient<CashBankBookForm>();
        services.AddTransient<BankReconciliationForm>();
        services.AddTransient<ImportExportForm>();
        services.AddTransient<BackupRestoreForm>();
        services.AddTransient<LoginForm>();
        services.AddTransient<UserManagementForm>();

        return services;
    }
}
