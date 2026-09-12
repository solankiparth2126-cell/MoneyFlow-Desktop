using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Configuration;
using MoneyFlow.Desktop.Diagnostics;
using MoneyFlow.Desktop.Dialogs;
using MoneyFlow.Desktop.Forms;
using MoneyFlow.Services;
using Serilog;

namespace MoneyFlow.Desktop;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        // 1. Initialize Logging & Diagnostics
        var logger = LoggingConfiguration.ConfigureLogging();
        Log.Information("=== MoneyFlow Desktop ERP Starting ===");

        // 2. Global Unhandled Exception Hooks
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        Application.ThreadException += (s, e) =>
        {
            Log.Error(e.Exception, "Fatal unhandled WinForms thread exception caught.");
            ShowCrashDialog(e.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log.Fatal(ex, "Fatal unhandled AppDomain exception caught. IsTerminating={IsTerminating}", e.IsTerminating);
                ShowCrashDialog(ex);
            }
            else
            {
                Log.Fatal("Non-exception fatal unhandled error in AppDomain: {Object}", e.ExceptionObject);
            }
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Log.Error(e.Exception, "Unobserved background task exception caught.");
            e.SetObserved();
            ShowCrashDialog(e.Exception);
        };

        try
        {
            // 3. Build Configuration with Environment-Specific Fallbacks
            var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            var configuration = builder.Build();

            // 4. Setup Dependency Injection with Modular Extensions
            var host = Host.CreateDefaultBuilder()
                .UseDefaultServiceProvider((_, options) =>
                {
                    options.ValidateScopes = false;
                    options.ValidateOnBuild = false;
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddLogging(loggingBuilder =>
                    {
                        loggingBuilder.ClearProviders();
                        loggingBuilder.AddSerilog(Log.Logger, dispose: true);
                    });

                    services.AddDatabaseServices(configuration);
                    services.AddDataRepositories();
                    services.AddDomainServices();
                    services.AddNavigationServices();
                    services.AddDesktopForms();
                })
                .Build();

            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;
            var dbSetup = services.GetRequiredService<IDatabaseSetupService>();

            // 5. First-Run Database Probe
            bool isDbReady = false;
            try
            {
                var initTask = dbSetup.InitializeDatabaseAsync();
                initTask.Wait(10000); // 10-second probe for first-run / cold-start

                if (initTask.IsCompleted && initTask.Result.IsSuccess)
                {
                    isDbReady = true;
                    Log.Information("Database initialization verified successfully.");
                }
                else
                {
                    Log.Warning("Database check timed out or reported failure. Prompting user configuration.");
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed initial database probe.");
                isDbReady = false;
            }

            if (!isDbReady)
            {
                using var connDialog = new DatabaseConnectionDialog(dbSetup);
                var dialogResult = connDialog.ShowDialog();
                if (dialogResult != DialogResult.OK)
                {
                    Log.Information("User cancelled database connection dialog. Terminating application.");
                    return;
                }
            }

            // 6. Launch Gateway Form
            Log.Information("Launching Main Gateway form.");
            var mainForm = services.GetRequiredService<MainForm>();
            Application.Run(mainForm);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Catastrophic error during application bootstrapping.");
            ShowCrashDialog(ex);
        }
        finally
        {
            Log.Information("=== MoneyFlow Desktop ERP Terminated ===");
            Log.CloseAndFlush();
        }
    }

    private static void ShowCrashDialog(Exception ex)
    {
        try
        {
            using var crashDialog = new CrashReportDialog(ex, LoggingConfiguration.LogDirectory);
            crashDialog.ShowDialog();
        }
        catch
        {
            MessageBox.Show(
                $"A critical error occurred and crash dialog could not be displayed:\n\n{ex.Message}\n\n{ex.StackTrace}",
                "Critical Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}