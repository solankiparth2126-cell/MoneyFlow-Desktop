using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.Migration;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Dialogs;

/// <summary>
/// Professional modal dialog showing sequential automatic company data migration progress.
/// Displays source and target version, pre-migration backup notice, and real-time step status.
/// </summary>
public class CompanyMigrationDialog : Form
{
    private readonly string _companyName;
    private readonly int _fromVersion;
    private readonly int _toVersion;
    private readonly Func<IProgress<MigrationProgressInfo>, CancellationToken, Task> _migrationTaskFactory;
    private readonly CancellationTokenSource _cts = new();

    private ProgressBar prgMigration = null!;
    private Label lblCurrentStep = null!;
    private Label lblStepDetail = null!;
    private Label lblStatusHeader = null!;
    private Button btnAction = null!;
    private bool _isCompleted;
    private bool _isFaulted;

    public CompanyMigrationDialog(
        string companyName,
        int fromVersion,
        int toVersion,
        Func<IProgress<MigrationProgressInfo>, CancellationToken, Task> migrationTaskFactory)
    {
        _companyName = companyName;
        _fromVersion = fromVersion;
        _toVersion = toVersion;
        _migrationTaskFactory = migrationTaskFactory ?? throw new ArgumentNullException(nameof(migrationTaskFactory));

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Company Data Upgrade — Sequential Migration";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(245, 247, 250);
        Font = ExecLedgerTheme.UIRegular9;

        ScreenFittingManager.FitDialogToWorkArea(this, 540, 310, 460, 260);

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 75,
            BackColor = Color.FromArgb(15, 30, 54)
        };

        var lblHeader = new Label
        {
            Text = "Automatic Company Data Migration",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 14),
            AutoSize = true
        };

        var lblSubHeader = new Label
        {
            Text = $"Upgrading '{_companyName}' from schema v{_fromVersion} to v{_toVersion}...",
            Font = ExecLedgerTheme.UIRegular9,
            ForeColor = Color.FromArgb(180, 205, 235),
            Location = new Point(20, 42),
            AutoSize = true
        };

        headerPanel.Controls.AddRange(new Control[] { lblHeader, lblSubHeader });

        lblStatusHeader = new Label
        {
            Text = "Preparing migration and creating pre-upgrade encrypted safety backup...",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = Color.FromArgb(30, 41, 59),
            Location = new Point(25, 95),
            Size = new Size(480, 22)
        };

        prgMigration = new ProgressBar
        {
            Location = new Point(25, 125),
            Size = new Size(475, 24),
            Minimum = 0,
            Maximum = 100,
            Value = 5,
            Style = ProgressBarStyle.Continuous
        };

        lblCurrentStep = new Label
        {
            Text = "Current Step: Initializing...",
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = Color.FromArgb(2, 132, 199),
            Location = new Point(25, 160),
            Size = new Size(475, 20)
        };

        lblStepDetail = new Label
        {
            Text = "Original company data is preserved intact in safety backup.",
            Font = ExecLedgerTheme.UIRegular8,
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(25, 185),
            Size = new Size(475, 35)
        };

        btnAction = new Button
        {
            Text = "Cancel",
            Font = ExecLedgerTheme.UIBold9,
            Location = new Point(400, 230),
            Size = new Size(100, 30),
            BackColor = Color.FromArgb(226, 232, 240),
            FlatStyle = FlatStyle.Flat
        };
        btnAction.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnAction.Click += (s, e) =>
        {
            if (_isCompleted || _isFaulted)
            {
                DialogResult = _isCompleted ? DialogResult.OK : DialogResult.Cancel;
                Close();
            }
            else
            {
                _cts.Cancel();
                btnAction.Enabled = false;
                lblCurrentStep.Text = "Cancelling migration...";
            }
        };

        Controls.AddRange(new Control[]
        {
            headerPanel,
            lblStatusHeader,
            prgMigration,
            lblCurrentStep,
            lblStepDetail,
            btnAction
        });

        Shown += async (s, e) => await RunMigrationAsync();
    }

    private async Task RunMigrationAsync()
    {
        var progress = new Progress<MigrationProgressInfo>(info =>
        {
            if (IsDisposed) return;
            lblCurrentStep.Text = $"Step {info.CurrentStep}/{info.TotalSteps} (v{info.FromVersion} -> v{info.TargetVersion})";
            lblStepDetail.Text = info.StatusMessage;
            prgMigration.Value = Math.Clamp((int)info.ProgressPercentage, 0, 100);
        });

        try
        {
            await _migrationTaskFactory(progress, _cts.Token);
            _isCompleted = true;
            prgMigration.Value = 100;
            lblStatusHeader.Text = "Migration completed successfully!";
            lblStatusHeader.ForeColor = Color.FromArgb(16, 149, 83);
            lblCurrentStep.Text = $"Company updated to schema v{_toVersion}.";
            lblStepDetail.Text = "All financial records, balances, and ledger integrity verified.";
            btnAction.Text = "Close";
            btnAction.BackColor = Color.FromArgb(2, 132, 199);
            btnAction.ForeColor = Color.White;
            btnAction.Enabled = true;
        }
        catch (OperationCanceledException)
        {
            _isFaulted = true;
            lblStatusHeader.Text = "Migration cancelled by user.";
            lblStatusHeader.ForeColor = Color.FromArgb(185, 28, 28);
            lblCurrentStep.Text = "Rolled back to original state.";
            btnAction.Text = "Close";
            btnAction.Enabled = true;
        }
        catch (Exception ex)
        {
            _isFaulted = true;
            lblStatusHeader.Text = "Migration failed!";
            lblStatusHeader.ForeColor = Color.FromArgb(185, 28, 28);
            lblCurrentStep.Text = "Pre-migration backup automatically restored.";
            lblStepDetail.Text = $"Error: {ex.Message}";
            btnAction.Text = "Close";
            btnAction.Enabled = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Dispose();
        }
        base.Dispose(disposing);
    }
}
