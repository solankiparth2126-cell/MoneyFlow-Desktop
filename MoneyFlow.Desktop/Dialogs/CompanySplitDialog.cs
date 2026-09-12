using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Dialogs;

/// <summary>
/// Tally-style Split Company Data dialog for financial year-end rollover.
/// Splits company data, carries forward closing balances & pending bills,
/// and creates isolated company directory.
/// </summary>
public class CompanySplitDialog : Form
{
    private readonly ICompanySplitService _companySplitService;
    private readonly Company _sourceCompany;

    private TextBox txtSourceCompany = null!;
    private DateTimePicker dtpSplitDate = null!;
    private TextBox txtNewCompanyName = null!;
    private Label lblDataDirPreview = null!;
    private Label lblInfo = null!;
    private Label lblStatus = null!;
    private Button btnSplit = null!;
    private Button btnCancel = null!;
    private ProgressBar progressBar = null!;

    public Company? CreatedCompany { get; private set; }

    public CompanySplitDialog(ICompanySplitService companySplitService, Company sourceCompany)
    {
        _companySplitService = companySplitService;
        _sourceCompany = sourceCompany;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "Split Company Data — Financial Year-End Rollover";
        this.Size = new Size(580, 520);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.FromArgb(245, 247, 250);
        this.Font = new Font("Segoe UI", 9.5F);

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(24, 43, 73)
        };

        var lblHeader = new Label
        {
            Text = "Split Company Data (Year-End Rollover)",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 12),
            AutoSize = true
        };

        var lblSubHeader = new Label
        {
            Text = "Closes financial year, carries forward balances & pending bills into a fresh company",
            Font = new Font("Segoe UI", 8.5F),
            ForeColor = Color.FromArgb(200, 215, 235),
            Location = new Point(20, 38),
            AutoSize = true
        };

        headerPanel.Controls.AddRange(new Control[] { lblHeader, lblSubHeader });

        var pnlContent = new Panel
        {
            Location = new Point(25, 80),
            Size = new Size(515, 360)
        };

        // Source Company
        var lblSource = new Label
        {
            Text = "Company to Split:",
            Location = new Point(0, 5),
            Size = new Size(160, 20),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        DateTime fyTo = _sourceCompany.FinancialYearFrom.AddYears(1).AddDays(-1);

        txtSourceCompany = new TextBox
        {
            Location = new Point(0, 28),
            Size = new Size(510, 26),
            ReadOnly = true,
            BackColor = Color.FromArgb(240, 243, 246),
            Text = $"{_sourceCompany.CompanyName} ({_sourceCompany.CompanyNumber}) — FY: {_sourceCompany.FinancialYearFrom:dd-MMM-yyyy} to {fyTo:dd-MMM-yyyy}"
        };

        // Split From Date
        var lblSplitDate = new Label
        {
            Text = "Split From Date (New Financial Year Begins):",
            Location = new Point(0, 68),
            Size = new Size(320, 20),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        DateTime defaultSplitDate = fyTo.AddDays(1);
        if (defaultSplitDate < DateTime.Today.AddYears(-5) || defaultSplitDate > DateTime.Today.AddYears(5))
        {
            defaultSplitDate = new DateTime(DateTime.Today.Year, 4, 1);
            if (defaultSplitDate <= _sourceCompany.FinancialYearFrom)
                defaultSplitDate = defaultSplitDate.AddYears(1);
        }

        dtpSplitDate = new DateTimePicker
        {
            Location = new Point(0, 92),
            Size = new Size(240, 26),
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MMM-yyyy",
            Value = defaultSplitDate
        };
        dtpSplitDate.ValueChanged += (s, e) => UpdateNewCompanyName();

        // New Company Name
        var lblNewName = new Label
        {
            Text = "New Company Name:",
            Location = new Point(0, 132),
            Size = new Size(200, 20),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        txtNewCompanyName = new TextBox
        {
            Location = new Point(0, 155),
            Size = new Size(510, 26)
        };
        UpdateNewCompanyName();

        // Data Directory preview
        lblDataDirPreview = new Label
        {
            Location = new Point(0, 190),
            Size = new Size(510, 38),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
            ForeColor = Color.FromArgb(71, 85, 105),
            Text = $"Data Directory: {_sourceCompany.DataDirectory ?? "C:\\MoneyFlow\\Data"}\\<Auto-Generated Number>\\"
        };

        // Info box
        lblInfo = new Label
        {
            Location = new Point(0, 235),
            Size = new Size(510, 60),
            BackColor = Color.FromArgb(238, 242, 255),
            ForeColor = Color.FromArgb(49, 46, 129),
            Font = new Font("Segoe UI", 8.5F),
            Padding = new Padding(8),
            Text = "• Split creates a completely isolated new company directory and records.\n" +
                   "• Pure Bookkeeping Mode: Ledgers, Groups, and Units are preserved.\n" +
                   "• Prior Year Net Profit is folded into Capital; Balance Sheet opening balances & pending bills carry over."
        };

        progressBar = new ProgressBar
        {
            Location = new Point(0, 305),
            Size = new Size(510, 16),
            Style = ProgressBarStyle.Marquee,
            Visible = false
        };

        lblStatus = new Label
        {
            Location = new Point(0, 328),
            Size = new Size(510, 22),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 64, 175),
            Text = string.Empty
        };

        pnlContent.Controls.AddRange(new Control[] {
            lblSource,
            txtSourceCompany,
            lblSplitDate,
            dtpSplitDate,
            lblNewName,
            txtNewCompanyName,
            lblDataDirPreview,
            lblInfo,
            progressBar,
            lblStatus
        });

        // Bottom Button Panel
        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = Color.FromArgb(235, 239, 245)
        };

        btnSplit = new Button
        {
            Text = "Split Now (Enter)",
            Size = new Size(140, 32),
            Location = new Point(245, 12),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSplit.FlatAppearance.BorderSize = 0;
        btnSplit.Click += async (s, e) => await OnSplitClickedAsync();

        btnCancel = new Button
        {
            Text = "Cancel (Esc)",
            Size = new Size(110, 32),
            Location = new Point(395, 12),
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9F),
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (s, e) =>
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        };

        buttonPanel.Controls.AddRange(new Control[] { btnSplit, btnCancel });

        this.Controls.AddRange(new Control[] {
            headerPanel,
            pnlContent,
            buttonPanel
        });

        this.AcceptButton = btnSplit;
        this.CancelButton = btnCancel;
    }

    private void UpdateNewCompanyName()
    {
        txtNewCompanyName.Text = $"{_sourceCompany.CompanyName} (From {dtpSplitDate.Value:dd-MMM-yyyy})";
    }

    private async Task OnSplitClickedAsync()
    {
        if (string.IsNullOrWhiteSpace(txtNewCompanyName.Text))
        {
            MessageBox.Show("Please enter a valid company name for the new split company.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtNewCompanyName.Focus();
            return;
        }

        var confirm = MessageBox.Show(
            $"Are you sure you want to split company '{_sourceCompany.CompanyName}' from {dtpSplitDate.Value:dd-MMM-yyyy}?\n\n" +
            "This will create a new isolated company with carried forward opening balances.",
            "Confirm Split Company",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        btnSplit.Enabled = false;
        btnCancel.Enabled = false;
        progressBar.Visible = true;
        lblStatus.Text = "Splitting company and carrying forward balances...";

        try
        {
            var splitDto = new CompanySplitDto
            {
                SourceCompanyId = _sourceCompany.CompanyId,
                SplitFromDate = dtpSplitDate.Value.Date,
                NewCompanyName = txtNewCompanyName.Text.Trim(),
                CarryForwardPendingBills = true,
                CarryForwardOpeningBalances = true
            };

            CreatedCompany = await _companySplitService.SplitCompanyAsync(splitDto);

            lblStatus.ForeColor = Color.FromArgb(5, 150, 105);
            lblStatus.Text = "Company split successfully completed!";
            progressBar.Visible = false;

            DateTime createdFyTo = CreatedCompany.FinancialYearFrom.AddYears(1).AddDays(-1);

            MessageBox.Show(
                $"Company split completed successfully!\n\n" +
                $"New Company: {CreatedCompany.CompanyName}\n" +
                $"Company Number: {CreatedCompany.CompanyNumber}\n" +
                $"Financial Year: {CreatedCompany.FinancialYearFrom:dd-MMM-yyyy} to {createdFyTo:dd-MMM-yyyy}\n" +
                $"Data Directory: {CreatedCompany.DataDirectory}",
                "Split Complete",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            progressBar.Visible = false;
            lblStatus.ForeColor = Color.FromArgb(220, 38, 38);
            lblStatus.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to split company:\n{ex.Message}", "Split Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            btnSplit.Enabled = true;
            btnCancel.Enabled = true;
        }
    }
}
