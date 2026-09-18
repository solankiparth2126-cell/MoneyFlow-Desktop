using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Dialogs;

/// <summary>
/// Friendly, diagnostic-rich crash reporter displayed upon unhandled exceptions.
/// </summary>
public class CrashReportDialog : Form
{
    private readonly Exception _exception;
    private readonly string _logDirectory;

    public CrashReportDialog(Exception exception, string logDirectory)
    {
        _exception = exception ?? throw new ArgumentNullException(nameof(exception));
        _logDirectory = logDirectory ?? string.Empty;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "MoneyFlow Desktop — Unexpected Error";
        Size = new Size(650, 480);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = true;
        BackColor = Color.FromArgb(248, 250, 252);
        Font = ExecLedgerTheme.UIRegular9;

        var appIcon = ExecLedgerIcons.GetAppIcon();
        if (appIcon != null)
        {
            Icon = appIcon;
            ShowIcon = true;
        }

        // Header Panel
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(220, 38, 38), // Red warning banner
            Padding = new Padding(15)
        };

        var lblTitle = new Label
        {
            Text = "MoneyFlow encountered an unexpected error",
            ForeColor = Color.White,
            Font = ExecLedgerTheme.UIBold12,
            AutoSize = true,
            Location = new Point(15, 12)
        };

        var lblSubtitle = new Label
        {
            Text = "We apologize for the inconvenience. Details have been captured below.",
            ForeColor = Color.FromArgb(254, 226, 226),
            Font = ExecLedgerTheme.UIRegular8,
            AutoSize = true,
            Location = new Point(16, 38)
        };

        headerPanel.Controls.Add(lblTitle);
        headerPanel.Controls.Add(lblSubtitle);
        Controls.Add(headerPanel);

        // Main Body Panel
        var bodyPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20)
        };

        var lblMessage = new Label
        {
            Text = $"Error: {_exception.GetType().Name}: {_exception.Message}",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = Color.FromArgb(30, 41, 59),
            Dock = DockStyle.Top,
            Height = 45
        };
        bodyPanel.Controls.Add(lblMessage);

        var txtDetails = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 8.5F),
            BackColor = Color.FromArgb(241, 245, 249),
            ForeColor = Color.FromArgb(15, 23, 42),
            Text = GetDiagnosticReport()
        };
        bodyPanel.Controls.Add(txtDetails);
        Controls.Add(bodyPanel);

        // Bottom Actions Panel
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = Color.FromArgb(241, 245, 249),
            Padding = new Padding(10)
        };

        var btnCopy = new Button
        {
            Text = "📋 Copy Details",
            Size = new Size(110, 32),
            Location = new Point(15, 11),
            BackColor = Color.White,
            FlatStyle = FlatStyle.System
        };
        btnCopy.Click += (s, e) =>
        {
            Clipboard.SetText(GetDiagnosticReport());
            MessageBox.Show(this, "Diagnostic details copied to clipboard.", "Executive Ledger", MessageBoxButtons.OK, MessageBoxIcon.Information);
        };

        var btnOpenLogs = new Button
        {
            Text = "📁 Open Logs Folder",
            Size = new Size(130, 32),
            Location = new Point(135, 11),
            BackColor = Color.White,
            FlatStyle = FlatStyle.System
        };
        btnOpenLogs.Click += (s, e) =>
        {
            try
            {
                if (Directory.Exists(_logDirectory))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = _logDirectory,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
                else
                {
                    MessageBox.Show(this, $"Log directory does not exist yet:\n{_logDirectory}", "Logs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"Could not open log folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };

        var btnClose = new Button
        {
            Text = "Close Application",
            Size = new Size(130, 32),
            Location = new Point(480, 11),
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.System
        };
        btnClose.Click += (s, e) => Close();

        bottomPanel.Controls.Add(btnCopy);
        bottomPanel.Controls.Add(btnOpenLogs);
        bottomPanel.Controls.Add(btnClose);
        Controls.Add(bottomPanel);
    }

    private string GetDiagnosticReport()
    {
        return $"MoneyFlow Diagnostic Crash Report\r\n" +
               $"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n" +
               $"OS: {Environment.OSVersion}\r\n" +
               $".NET Runtime: {Environment.Version}\r\n" +
               $"Logs Folder: {_logDirectory}\r\n" +
               $"----------------------------------------\r\n" +
               $"Exception: {_exception.GetType().FullName}\r\n" +
               $"Message: {_exception.Message}\r\n\r\n" +
               $"Stack Trace:\r\n{_exception.StackTrace}\r\n\r\n" +
               $"Inner Exception:\r\n{_exception.InnerException?.ToString() ?? "None"}";
    }
}
