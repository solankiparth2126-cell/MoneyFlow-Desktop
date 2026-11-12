using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MoneyFlow.Data.Storage;
using MoneyFlow.Desktop.Configuration;
using MoneyFlow.Desktop.Diagnostics;
using MoneyFlow.Desktop.Dialogs;
using MoneyFlow.Desktop.Forms;
using MoneyFlow.Services.Startup;
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
            // 3. First-Time Installation Detection & Setup Flow
            string? effectiveStoragePath = null;
            if (SystemEnvironmentManager.IsFirstTimeSetup(out var existingStoragePath, out var loadedConfig))
            {
                Log.Information("First-time installation detected. Launching Application Startup / Initial Setup wizard.");
                using var startupForm = new StartupConfigurationForm();
                var dialogResult = startupForm.ShowDialog();
                if (dialogResult != DialogResult.OK)
                {
                    Log.Information("Initial setup was cancelled or exited by the user. Exiting application.");
                    return;
                }

                effectiveStoragePath = startupForm.Config.CompanyDataPath;
                Log.Information("Initial environment configured successfully. Root storage path: {Path}", effectiveStoragePath);
            }
            else
            {
                effectiveStoragePath = existingStoragePath;
                Log.Information("Existing installation detected. Using storage path: {Path}", effectiveStoragePath);
            }

            // 4. Build Configuration with Environment-Specific Fallbacks
            var env = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            var configuration = builder.Build();

            // 5. Setup Dependency Injection — File-Based Storage (No SQL Server)
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

                    services.AddStorageServices(configuration, effectiveStoragePath);
                    services.AddDataRepositories();
                    services.AddDomainServices();
                    services.AddNavigationServices();
                    services.AddDesktopForms();
                })
                .Build();

            using var scope = host.Services.CreateScope();
            var services = scope.ServiceProvider;

            // 6. Initialize file-based storage (create directories, no SQL)
            var initService = services.GetRequiredService<ApplicationInitializationService>();
            initService.Initialize();
            Log.Information("File-based storage initialized successfully. No SQL Server required.");

            // 7. Display Executive Ledger Splash Screen
            Log.Information("Displaying Executive Ledger Splash Screen.");
            using (var splash = services.GetRequiredService<SplashScreenForm>())
            {
                splash.ShowDialog();
            }

            // 8. Launch Gateway Form
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