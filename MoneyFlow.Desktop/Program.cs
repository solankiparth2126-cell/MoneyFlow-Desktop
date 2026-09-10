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

                services.AddTransient<MainForm>();
                services.AddTransient<DatabaseConnectionDialog>();
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