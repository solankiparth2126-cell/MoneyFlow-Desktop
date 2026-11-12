using System;
using System.Drawing;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Dialogs;

/// <summary>
/// Tally Vault security dialog prompted when opening a password-protected company.
/// </summary>
public class CompanyPasswordPromptDialog : Form
{
    private readonly string _companyName;
    private readonly string _companyNumber;

    private TextBox txtPassword = null!;
    private Label lblError = null!;
    private Button btnOk = null!;
    private Button btnCancel = null!;

    public string EnteredPassword => txtPassword.Text;

    public CompanyPasswordPromptDialog(string companyName, string companyNumber)
    {
        _companyName = companyName;
        _companyNumber = companyNumber;
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "Tally Vault — Company Security";
        this.Size = new Size(460, 260);
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.FromArgb(245, 247, 250);
        this.Font = ExecLedgerTheme.UIRegular9;

        var appIcon = ExecLedgerIcons.GetAppIcon();
        if (appIcon != null)
        {
            this.Icon = appIcon;
            this.ShowIcon = true;
        }

        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.FromArgb(24, 43, 73)
        };

        var lblHeader = new Label
        {
            Text = "Tally Vault Access",
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 12),
            AutoSize = true
        };

        var lblSubHeader = new Label
        {
            Text = $"Company: {_companyName} ({_companyNumber})",
            Font = ExecLedgerTheme.UIRegular8,
            ForeColor = Color.FromArgb(200, 215, 235),
            Location = new Point(20, 36),
            AutoSize = true
        };

        headerPanel.Controls.AddRange(new Control[] { lblHeader, lblSubHeader });

        var lblPrompt = new Label
        {
            Text = "Enter Vault Password:",
            Location = new Point(30, 85),
            Size = new Size(160, 22),
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        txtPassword = new TextBox
        {
            Location = new Point(30, 110),
            Size = new Size(385, 26),
            Font = ExecLedgerTheme.UIRegular10,
            PasswordChar = '●',
            UseSystemPasswordChar = true
        };

        lblError = new Label
        {
            Location = new Point(30, 142),
            Size = new Size(385, 22),
            ForeColor = Color.FromArgb(220, 38, 38),
            Font = ExecLedgerTheme.UIBold8,
            Text = string.Empty
        };

        var buttonPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 55,
            BackColor = Color.FromArgb(235, 239, 245)
        };

        btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(85, 32),
            Location = new Point(235, 12),
            BackColor = Color.FromArgb(30, 64, 175),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = ExecLedgerTheme.UIBold9,
            Cursor = Cursors.Hand
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += (s, e) =>
        {
            if (string.IsNullOrEmpty(txtPassword.Text))
            {
                SetError("Password is required.");
                this.DialogResult = DialogResult.None;
                return;
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        };

        btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Size = new Size(85, 32),
            Location = new Point(330, 12),
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(51, 65, 85),
            FlatStyle = FlatStyle.Flat,
            Font = ExecLedgerTheme.UIRegular9,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (s, e) =>
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        };

        buttonPanel.Controls.AddRange(new Control[] { btnOk, btnCancel });

        this.Controls.AddRange(new Control[] {
            headerPanel,
            lblPrompt,
            txtPassword,
            lblError,
            buttonPanel
        });

        this.AcceptButton = btnOk;
        this.CancelButton = btnCancel;

        this.Shown += (s, e) => txtPassword.Focus();
    }

    public void SetError(string message)
    {
        lblError.Text = message;
        txtPassword.SelectAll();
        txtPassword.Focus();
    }
}
