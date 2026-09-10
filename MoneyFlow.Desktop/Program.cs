using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;
using MoneyFlow.Data.Repositories;
using MoneyFlow.Desktop.Dialogs;
using MoneyFlow.Desktop.Forms;
using MoneyFlow.Services;
using MoneyFlow.Services.Company;
using MoneyFlow.Services.FinancialYear;
using MoneyFlow.Services.Group;
using MoneyFlow.Services.Ledger;
using MoneyFlow.Services.Accounting;

namespace MoneyFlow.Desktop;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // 1. Build Configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        var configuration = builder.Build();

        // 2. Setup Dependency Injection
        var host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection")
                    ?? @"Server=.\SQLEXPRESS02;Database=MoneyFlowDB;Trusted_Connection=True;TrustServerCertificate=True;";

                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(connectionString));

                services.AddLogging(configure => configure.AddConsole());
                services.AddScoped<IDatabaseSetupService, DatabaseSetupService>();

                // Repositories & Unit of Work (Phase 2 Foundation)
                services.AddScoped<IUnitOfWork, UnitOfWork>();
                services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
                services.AddScoped<ICompanyRepository, CompanyRepository>();
                services.AddScoped<IFinancialYearRepository, FinancialYearRepository>();
                services.AddScoped<IGroupRepository, GroupRepository>();
                services.AddScoped<ILedgerRepository, LedgerRepository>();
                services.AddScoped<IVoucherRepository, VoucherRepository>();

                // Company Services (Phase 3)
                services.AddSingleton<ICompanyContext, CompanyContext>();
                services.AddScoped<ICompanyService, CompanyService>();

                // Financial Year Services (Phase 4)
                services.AddScoped<IFinancialYearService, FinancialYearService>();

                // Group Master Services (Phase 5)
                services.AddScoped<IGroupService, GroupService>();

                // Ledger Master Services (Phase 6)
                services.AddScoped<ILedgerService, LedgerService>();

                // Accounting Engine (Phase 7 Milestone)
                services.AddScoped<IAccountingService, AccountingService>();

                services.AddTransient<MainForm>();
                services.AddTransient<DatabaseConnectionDialog>();
                services.AddTransient<CompanyListForm>();
                services.AddTransient<CompanyCreateEditForm>();
                services.AddTransient<FinancialYearListForm>();
                services.AddTransient<FinancialYearCreateForm>();
                services.AddTransient<GroupListForm>();
                services.AddTransient<GroupCreateEditForm>();
                services.AddTransient<LedgerListForm>();
                services.AddTransient<LedgerCreateEditForm>();
                services.AddTransient<PaymentVoucherForm>();
                services.AddTransient<ReceiptVoucherForm>();
                services.AddTransient<ContraVoucherForm>();
                services.AddTransient<JournalVoucherForm>();
                services.AddTransient<SalesVoucherForm>();
                services.AddTransient<PurchaseVoucherForm>();
                services.AddTransient<DebitNoteForm>();
                services.AddTransient<CreditNoteForm>();
                services.AddTransient<DayBookForm>();
                services.AddTransient<LedgerStatementForm>();
                services.AddTransient<TrialBalanceForm>();
            })
            .Build();

        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;
        var dbSetup = services.GetRequiredService<IDatabaseSetupService>();

        // 3. First-Run Database Check
        bool isDbReady = false;
        try
        {
            var initTask = dbSetup.InitializeDatabaseAsync();
            initTask.Wait(3000); // 3-second quick probe

            if (initTask.IsCompleted && initTask.Result.IsSuccess)
            {
                isDbReady = true;
            }
        }
        catch
        {
            isDbReady = false;
        }

        if (!isDbReady)
        {
            // Prompt connection dialog with diagnostic button per Section 60
            using var connDialog = new DatabaseConnectionDialog(dbSetup);
            var dialogResult = connDialog.ShowDialog();
            if (dialogResult != DialogResult.OK)
            {
                return; // User exited or cancelled
            }
        }

        // 4. Launch Main Gateway
        var mainForm = services.GetRequiredService<MainForm>();
        Application.Run(mainForm);
    }
}