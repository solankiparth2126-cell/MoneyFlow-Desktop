using System;
using System.IO;
using Serilog;
using Serilog.Events;

namespace MoneyFlow.Desktop.Diagnostics;

/// <summary>
/// Centralized diagnostic and structured logging configuration.
/// </summary>
public static class LoggingConfiguration
{
    private static string? _logDirectory;

    public static string LogDirectory
    {
        get
        {
            if (string.IsNullOrEmpty(_logDirectory))
            {
                _logDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MoneyFlow",
                    "logs");
            }
            return _logDirectory;
        }
    }

    /// <summary>
    /// Configures and sets Serilog.Log.Logger to write to console and a rolling disk file.
    /// </summary>
    public static Serilog.ILogger ConfigureLogging()
    {
        Directory.CreateDirectory(LogDirectory);

        var logFilePath = Path.Combine(LogDirectory, "moneyflow-.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                path: logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        return Log.Logger;
    }
}
