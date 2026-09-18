using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;
using MoneyFlow.Data.Encryption;
using MoneyFlow.Data.Migration;
using MoneyFlow.Data.Repositories;
using MoneyFlow.Data.Storage;
using MoneyFlow.Desktop.Forms;
using MoneyFlow.Desktop.Navigation;
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
using MoneyFlow.Services.Startup;

namespace MoneyFlow.Desktop.Configuration;

/// <summary>
/// Modular DI service registration extensions.
/// SQL Server dependencies have been completely replaced with file-based storage.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Compatibility alias for legacy tests and setup code.
    /// Redirects database service registration to file storage services.
    /// </summary>
    public static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration? configuration = null)
    {
        var config = configuration ?? new ConfigurationBuilder().Build();
        services.AddStorageServices(config);
        services.AddDataRepositories();
        return services;
    }

    /// <summary>
    /// Registers the file-based storage engine, encryption, and company management services.
    /// Replaces the old AddDatabaseServices which registered EF Core + SQL Server.
    /// </summary>
    public static IServiceCollection AddStorageServices(this IServiceCollection services, IConfiguration configuration, string? overrideDataPath = null)
    {
        var basePath = !string.IsNullOrWhiteSpace(overrideDataPath)
            ? overrideDataPath
            : (configuration.GetValue<string>("ApplicationSettings:DataPath") ?? SystemEnvironmentManager.GetDefaultCompanyDataPath());

        var systemConfig = SystemConfiguration.Load(basePath);
        services.AddSingleton(systemConfig);

        // Core storage infrastructure
        services.AddSingleton<AesGcmEncryptor>();
        services.AddSingleton<KeyManager>();
        services.AddSingleton<StorageEngine>();
        services.AddSingleton(sp => new CompanyManager(
            basePath,
            sp.GetRequiredService<StorageEngine>(),
            sp.GetRequiredService<KeyManager>(),
            sp.GetService<Microsoft.Extensions.Logging.ILogger<CompanyManager>>()));

        // Company session — singleton because only one company is open at a time
        services.AddSingleton(sp => new CompanySession(
            sp.GetRequiredService<StorageEngine>(),
            sp.GetRequiredService<KeyManager>(),
            sp.GetService<Microsoft.Extensions.Logging.ILogger<CompanySession>>()));

        // Backup & Migration
        services.AddSingleton(sp => new BackupManager(
            sp.GetRequiredService<CompanyManager>(),
            sp.GetRequiredService<StorageEngine>(),
            sp.GetService<Microsoft.Extensions.Logging.ILogger<BackupManager>>()));

        services.AddSingleton(sp => new MigrationManager(
            sp.GetRequiredService<BackupManager>(),
            sp.GetService<Microsoft.Extensions.Logging.ILogger<MigrationManager>>()));

        // Initialization service (replaces DatabaseSetupService)
        services.AddSingleton<ApplicationInitializationService>();

        // AppDataContext — in-memory replacement for AppDbContext
        // Provides IQueryable properties matching old EF Core DbSet names
        services.AddScoped(sp => new AppDataContext(sp.GetRequiredService<CompanySession>()));
        services.AddScoped(sp => new AppDbContext(sp.GetRequiredService<CompanySession>()));

        return services;
    }

    /// <summary>
    /// Registers file-based repositories and unit of work.
    /// Replaces the old AddDataRepositories which registered EF Core repositories.
    /// </summary>
    public static IServiceCollection AddDataRepositories(this IServiceCollection services)
    {
        // Unit of Work
        services.AddScoped<IUnitOfWork>(sp => new FileUnitOfWork(sp.GetRequiredService<CompanySession>()));

        // Generic repository
        services.AddScoped(typeof(IRepository<>), typeof(FileRepository<>));

        // Specialized accounting repositories
        services.AddScoped<ICompanyRepository>(sp => new FileCompanyRepository(sp.GetRequiredService<CompanySession>()));
        services.AddScoped<IFinancialYearRepository>(sp => new FileFinancialYearRepository(sp.GetRequiredService<CompanySession>()));
        services.AddScoped<IGroupRepository>(sp => new FileGroupRepository(sp.GetRequiredService<CompanySession>()));
        services.AddScoped<ILedgerRepository>(sp => new FileLedgerRepository(sp.GetRequiredService<CompanySession>()));
        services.AddScoped<IVoucherRepository>(sp => new FileVoucherRepository(sp.GetRequiredService<CompanySession>()));

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
        services.AddTransient<StartupConfigurationForm>();
        services.AddTransient<SplashScreenForm>();
        services.AddTransient<MainForm>();
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
