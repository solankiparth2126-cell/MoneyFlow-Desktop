using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Forms;

public class LoginForm : Form
{
    private readonly ISecurityService _securityService;
    private readonly IUserContext _userContext;

    private TextBox _txtUsername = null!;
    private TextBox _txtPassword = null!;
    private Button _btnLogin = null!;
    private Button _btnCancel = null!;
    private Label _lblError = null!;

    public bool AuthenticatedSuccessfully { get; private set; }

    public LoginForm(ISecurityService securityService, IUserContext userContext)
    {
        _securityService = securityService;
        _userContext = userContext;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "User Authentication — MoneyFlow Accounting";
        Size = new Size(460, 310);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = ExecLedgerTheme.UIRegular9;
        BackColor = Color.FromArgb(244, 246, 249);
        KeyPreview = true;

        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 50,
            BackColor = Color.FromArgb(18, 52, 86),
            Padding = new Padding(15, 12, 15, 10)
        };

        var lblHeader = new Label
        {
            Text = "MONEYFLOW USER LOGIN",
            ForeColor = Color.White,
            Font = ExecLedgerTheme.UIBold11,
            AutoSize = true,
            Location = new Point(15, 14)
        };
        pnlHeader.Controls.Add(lblHeader);

        var lblPrompt = new Label
        {
            Text = "Enter your credentials to access company records and vouchers:",
            Font = ExecLedgerTheme.UIRegular8,
            ForeColor = Color.FromArgb(100, 110, 120),
            Location = new Point(25, 65),
            Size = new Size(400, 20)
        };

        var lblUser = new Label { Text = "Username:", Location = new Point(25, 100), AutoSize = true };
        _txtUsername = new TextBox
        {
            Location = new Point(125, 97),
            Width = 280,
            Text = _userContext.Username ?? "admin"
        };

        var lblPass = new Label { Text = "Password:", Location = new Point(25, 140), AutoSize = true };
        _txtPassword = new TextBox
        {
            Location = new Point(125, 137),
            Width = 280,
            UseSystemPasswordChar = true
        };

        _lblError = new Label
        {
            ForeColor = Color.FromArgb(192, 57, 43),
            Font = ExecLedgerTheme.UIBold8,
            Location = new Point(125, 170),
            Size = new Size(280, 30),
            Visible = false
        };

        _btnLogin = new Button
        {
            Text = "Login",
            Font = ExecLedgerTheme.UIBold9,
            BackColor = Color.FromArgb(41, 128, 185),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Location = new Point(220, 215),
            Width = 90,
            Height = 32
        };
        _btnLogin.FlatAppearance.BorderSize = 0;
        _btnLogin.Click += async (s, e) => await ExecuteLoginAsync();

        _btnCancel = new Button
        {
            Text = "Cancel",
            Font = ExecLedgerTheme.UIRegular9,
            BackColor = Color.FromArgb(189, 195, 199),
            ForeColor = Color.FromArgb(44, 62, 80),
            FlatStyle = FlatStyle.Flat,
            Location = new Point(320, 215),
            Width = 85,
            Height = 32
        };
        _btnCancel.FlatAppearance.BorderSize = 0;
        _btnCancel.Click += (s, e) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        AcceptButton = _btnLogin;
        CancelButton = _btnCancel;

        Controls.Add(pnlHeader);
        Controls.Add(lblPrompt);
        Controls.Add(lblUser);
        Controls.Add(_txtUsername);
        Controls.Add(lblPass);
        Controls.Add(_txtPassword);
        Controls.Add(_lblError);
        Controls.Add(_btnLogin);
        Controls.Add(_btnCancel);
    }

    private async Task ExecuteLoginAsync()
    {
        string username = _txtUsername.Text.Trim();
        string password = _txtPassword.Text;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _lblError.Text = "Please enter both username and password.";
            _lblError.Visible = true;
            return;
        }

        _btnLogin.Enabled = false;
        _lblError.Visible = false;

        try
        {
            var result = await _securityService.AuthenticateAsync(username, password);
            if (result.IsSuccess)
            {
                AuthenticatedSuccessfully = true;
                DialogResult = DialogResult.OK;
                Close();
            }
            else
            {
                _lblError.Text = result.Message;
                _lblError.Visible = true;
                _txtPassword.SelectAll();
                _txtPassword.Focus();
            }
        }
        catch (Exception ex)
        {
            _lblError.Text = $"Error: {ex.Message}";
            _lblError.Visible = true;
        }
        finally
        {
            _btnLogin.Enabled = true;
        }
    }
}
