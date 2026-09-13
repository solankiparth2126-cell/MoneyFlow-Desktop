using System;
using System.Drawing;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Forms;

public class FinancialYearCreateForm : Form
{
    private readonly IFinancialYearService _fyService;
    private readonly int _companyId;

    private DateTimePicker dtpStartDate = null!;
    private DateTimePicker dtpEndDate = null!;
    private TextBox txtYearName = null!;
    private Button btnSave = null!;
    private Button btnCancel = null!;

    public bool IsSaved { get; private set; }

    public FinancialYearCreateForm(IFinancialYearService fyService, int companyId, DateTime? suggestedStart = null)
    {
        _fyService = fyService;
        _companyId = companyId;
        InitializeComponent(suggestedStart);
    }

    private void InitializeComponent(DateTime? suggestedStart)
    {
        this.Text = "MoneyFlow Desktop — New Financial Year";
        this.Size = new Size(480, 320);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.FromArgb(245, 247, 250);
        this.Font = ExecLedgerTheme.UIRegular9;

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = Color.FromArgb(24, 43, 73)
        };
        var lblTitle = new Label
        {
            Text = "Create Financial Year",
            Font = ExecLedgerTheme.UIBold11,
            ForeColor = Color.White,
            Location = new Point(18, 15),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblTitle);
        this.Controls.Add(headerPanel);

        var grp = new GroupBox
        {
            Text = "Financial Year Details",
            Location = new Point(20, 70),
            Size = new Size(425, 145),
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        DateTime defaultStart = suggestedStart ?? new DateTime(DateTime.Today.Year, 4, 1);
        DateTime defaultEnd = defaultStart.AddYears(1).AddDays(-1);

        grp.Controls.Add(new Label { Text = "Start Date:", Location = new Point(20, 30), AutoSize = true });
        dtpStartDate = new DateTimePicker
        {
            Location = new Point(140, 27),
            Width = 260,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MMM-yyyy",
            Value = defaultStart
        };
        dtpStartDate.ValueChanged += (s, e) => UpdateYearName();
        grp.Controls.Add(dtpStartDate);

        grp.Controls.Add(new Label { Text = "End Date:", Location = new Point(20, 65), AutoSize = true });
        dtpEndDate = new DateTimePicker
        {
            Location = new Point(140, 62),
            Width = 260,
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MMM-yyyy",
            Value = defaultEnd
        };
        dtpEndDate.ValueChanged += (s, e) => UpdateYearName();
        grp.Controls.Add(dtpEndDate);

        grp.Controls.Add(new Label { Text = "Year Name:", Location = new Point(20, 100), AutoSize = true });
        txtYearName = new TextBox
        {
            Location = new Point(140, 97),
            Width = 260
        };
        grp.Controls.Add(txtYearName);

        this.Controls.Add(grp);

        btnSave = new Button
        {
            Text = "Create FY (Enter)",
            Location = new Point(185, 230),
            Size = new Size(140, 34),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            Font = ExecLedgerTheme.UIBold9
        };
        btnSave.Click += async (s, e) => await SaveAsync();

        btnCancel = new Button
        {
            Text = "Cancel (Esc)",
            Location = new Point(335, 230),
            Size = new Size(110, 34),
            BackColor = Color.FromArgb(226, 232, 240)
        };
        btnCancel.Click += (s, e) => this.Close();

        this.Controls.Add(btnSave);
        this.Controls.Add(btnCancel);

        this.AcceptButton = btnSave;
        this.CancelButton = btnCancel;

        UpdateYearName();
    }

    private void UpdateYearName()
    {
        txtYearName.Text = $"{dtpStartDate.Value.Year}-{(dtpEndDate.Value.Year % 100):D2}";
    }

    private async Task SaveAsync()
    {
        if (dtpStartDate.Value >= dtpEndDate.Value)
        {
            MessageBox.Show("Start Date must be earlier than End Date.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btnSave.Enabled = false;

        try
        {
            var dto = new FinancialYearCreateDto
            {
                YearName = txtYearName.Text.Trim(),
                StartDate = dtpStartDate.Value.Date,
                EndDate = dtpEndDate.Value.Date
            };

            var created = await _fyService.CreateFinancialYearAsync(_companyId, dto);
            MessageBox.Show($"Financial Year '{created.YearName}' created successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);

            IsSaved = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to create Financial Year:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            btnSave.Enabled = true;
        }
    }
}
