using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Forms;

public class LedgerCreateEditForm : Form
{
    private readonly ILedgerService _ledgerService;
    private readonly IGroupService _groupService;
    private readonly int _companyId;
    private readonly int? _ledgerId;

    private TextBox _txtName = null!;
    private ComboBox _cmbGroup = null!;
    private NumericUpDown _numOpeningBalance = null!;
    private ComboBox _cmbBalanceType = null!;
    private TextBox _txtAddress = null!;
    private ComboBox _txtState = null!;
    private TextBox _txtPhone = null!;
    private TextBox _txtEmail = null!;
    private TextBox _txtPAN = null!;
    private TextBox _txtBankName = null!;
    private TextBox _txtBankAccountNumber = null!;
    private TextBox _txtIFSC = null!;
    private NumericUpDown _numCreditLimit = null!;
    private NumericUpDown _numCreditDays = null!;
    private CheckBox _chkIsActive = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;

    private readonly int? _initialGroupId;

    public LedgerCreateEditForm(
        ILedgerService ledgerService,
        IGroupService groupService,
        int companyId,
        int? ledgerId = null,
        int? initialGroupId = null)
    {
        _ledgerService = ledgerService;
        _groupService = groupService;
        _companyId = companyId;
        _ledgerId = ledgerId;
        _initialGroupId = initialGroupId;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = _ledgerId.HasValue ? "Ledger Alteration (Edit)" : "Ledger Creation";
        Size = new Size(680, 680);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = ExecLedgerTheme.UIRegular9;
        KeyPreview = true;

        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 16,
            Padding = new Padding(20),
            AutoScroll = true
        };

        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        mainPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Header Label
        var lblHeader = new Label
        {
            Text = _ledgerId.HasValue ? "ALTER LEDGER" : "CREATE NEW LEDGER",
            Font = ExecLedgerTheme.UIBold12,
            ForeColor = ExecLedgerTheme.PrimaryNavy,
            Dock = DockStyle.Fill,
            Height = 35
        };
        mainPanel.Controls.Add(lblHeader, 0, 0);
        mainPanel.SetColumnSpan(lblHeader, 2);

        // 1. Ledger Name
        mainPanel.Controls.Add(new Label { Text = "Ledger Name *:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        _txtName = new TextBox { Width = 380, Font = ExecLedgerTheme.UIRegular10 };
        mainPanel.Controls.Add(_txtName, 1, 1);

        // 2. Under Group
        mainPanel.Controls.Add(new Label { Text = "Under Group *:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
        _cmbGroup = new ComboBox
        {
            Width = 380,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = ExecLedgerTheme.UIRegular10
        };
        mainPanel.Controls.Add(_cmbGroup, 1, 2);

        // 3. Opening Balance
        mainPanel.Controls.Add(new Label { Text = "Opening Balance (₹):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 3);
        var pnlBalance = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _numOpeningBalance = new NumericUpDown
        {
            Width = 180,
            DecimalPlaces = 2,
            Maximum = 999999999,
            ThousandsSeparator = true,
            Font = ExecLedgerTheme.UIRegular10
        };
        _cmbBalanceType = new ComboBox
        {
            Width = 120,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = ExecLedgerTheme.UIRegular10
        };
        _cmbBalanceType.Items.Add("Debit (Dr)");
        _cmbBalanceType.Items.Add("Credit (Cr)");
        _cmbBalanceType.SelectedIndex = 0;
        pnlBalance.Controls.Add(_numOpeningBalance);
        pnlBalance.Controls.Add(_cmbBalanceType);
        mainPanel.Controls.Add(pnlBalance, 1, 3);

        // Section Separator: Mailing Details
        var lblMailing = new Label
        {
            Text = "Mailing Details",
            Font = ExecLedgerTheme.UIBold10,
            ForeColor = Color.FromArgb(70, 70, 70),
            Margin = new Padding(0, 10, 0, 5)
        };
        mainPanel.Controls.Add(lblMailing, 0, 4);
        mainPanel.SetColumnSpan(lblMailing, 2);

        // 4. Address
        mainPanel.Controls.Add(new Label { Text = "Address:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 5);
        _txtAddress = new TextBox { Width = 380, Height = 45, Multiline = true, Font = ExecLedgerTheme.UIRegular9 };
        mainPanel.Controls.Add(_txtAddress, 1, 5);

        // 5. State
        mainPanel.Controls.Add(new Label { Text = "State:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 6);
        _txtState = new ComboBox
        {
            Width = 250,
            Font = ExecLedgerTheme.UIRegular9,
            DropDownStyle = ComboBoxStyle.DropDown,
            AutoCompleteMode = AutoCompleteMode.SuggestAppend,
            AutoCompleteSource = AutoCompleteSource.ListItems
        };
        _txtState.Items.AddRange(new object[] {
            "Andaman and Nicobar Islands", "Andhra Pradesh", "Arunachal Pradesh", "Assam", "Bihar",
            "Chandigarh", "Chhattisgarh", "Dadra and Nagar Haveli and Daman and Diu", "Delhi", "Goa",
            "Gujarat", "Haryana", "Himachal Pradesh", "Jammu and Kashmir", "Jharkhand",
            "Karnataka", "Kerala", "Ladakh", "Lakshadweep", "Madhya Pradesh",
            "Maharashtra", "Manipur", "Meghalaya", "Mizoram", "Nagaland",
            "Odisha", "Puducherry", "Punjab", "Rajasthan", "Sikkim",
            "Tamil Nadu", "Telangana", "Tripura", "Uttar Pradesh", "Uttarakhand", "West Bengal"
        });
        mainPanel.Controls.Add(_txtState, 1, 6);

        // 6. Phone & Email
        mainPanel.Controls.Add(new Label { Text = "Phone / Mobile:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 7);
        _txtPhone = new TextBox { Width = 250, Font = ExecLedgerTheme.UIRegular9 };
        mainPanel.Controls.Add(_txtPhone, 1, 7);

        mainPanel.Controls.Add(new Label { Text = "Email:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 8);
        _txtEmail = new TextBox { Width = 250, Font = ExecLedgerTheme.UIRegular9 };
        mainPanel.Controls.Add(_txtEmail, 1, 8);

        // 7. PAN
        mainPanel.Controls.Add(new Label { Text = "PAN Number:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 9);
        _txtPAN = new TextBox { Width = 250, MaxLength = 10, Font = ExecLedgerTheme.UIRegular9 };
        mainPanel.Controls.Add(_txtPAN, 1, 9);

        // Section Separator: Banking Details
        var lblBanking = new Label
        {
            Text = "Banking Details",
            Font = ExecLedgerTheme.UIBold10,
            ForeColor = Color.FromArgb(70, 70, 70),
            Margin = new Padding(0, 10, 0, 5)
        };
        mainPanel.Controls.Add(lblBanking, 0, 10);
        mainPanel.SetColumnSpan(lblBanking, 2);

        // 8. Bank Name
        mainPanel.Controls.Add(new Label { Text = "Bank Name:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 11);
        _txtBankName = new TextBox { Width = 250, Font = ExecLedgerTheme.UIRegular9 };
        mainPanel.Controls.Add(_txtBankName, 1, 11);

        // 9. Bank Account No & IFSC
        mainPanel.Controls.Add(new Label { Text = "Account Number:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 12);
        _txtBankAccountNumber = new TextBox { Width = 250, Font = ExecLedgerTheme.UIRegular9 };
        mainPanel.Controls.Add(_txtBankAccountNumber, 1, 12);

        mainPanel.Controls.Add(new Label { Text = "IFSC Code:", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 13);
        _txtIFSC = new TextBox { Width = 250, MaxLength = 11, Font = ExecLedgerTheme.UIRegular9 };
        mainPanel.Controls.Add(_txtIFSC, 1, 13);

        // 10. Credit Limit & Days
        mainPanel.Controls.Add(new Label { Text = "Credit Limit (₹):", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 14);
        var pnlCredit = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
        _numCreditLimit = new NumericUpDown
        {
            Width = 140,
            DecimalPlaces = 2,
            Maximum = 999999999,
            ThousandsSeparator = true,
            Font = ExecLedgerTheme.UIRegular9
        };
        var lblCreditDays = new Label { Text = "Credit Days:", AutoSize = true, Margin = new Padding(15, 5, 5, 0) };
        _numCreditDays = new NumericUpDown
        {
            Width = 80,
            Maximum = 365,
            Font = ExecLedgerTheme.UIRegular9
        };
        pnlCredit.Controls.Add(_numCreditLimit);
        pnlCredit.Controls.Add(lblCreditDays);
        pnlCredit.Controls.Add(_numCreditDays);
        mainPanel.Controls.Add(pnlCredit, 1, 14);

        // 11. IsActive Checkbox
        _chkIsActive = new CheckBox { Text = "Active Ledger", Checked = true, AutoSize = true };
        mainPanel.Controls.Add(_chkIsActive, 1, 15);

        // Button Panel at bottom
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(15, 8, 15, 8),
            BackColor = Color.FromArgb(240, 240, 240)
        };

        _btnCancel = new Button
        {
            Text = "Cancel (Esc)",
            DialogResult = DialogResult.Cancel,
            Size = new Size(110, 32),
            Font = ExecLedgerTheme.UIRegular9
        };

        _btnSave = new Button
        {
            Text = _ledgerId.HasValue ? "Update (Enter)" : "Save (Enter)",
            BackColor = ExecLedgerTheme.PrimaryNavy,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 32),
            Font = ExecLedgerTheme.UIBold9
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += async (s, e) => await OnSaveAsync();

        buttonPanel.Controls.Add(_btnCancel);
        buttonPanel.Controls.Add(_btnSave);

        Controls.Add(mainPanel);
        Controls.Add(buttonPanel);

        AcceptButton = _btnSave;
        CancelButton = _btnCancel;

        Load += async (s, e) => await OnFormLoadAsync();
    }

    private async Task OnFormLoadAsync()
    {
        try
        {
            var groups = await _groupService.GetGroupsByCompanyAsync(_companyId);
            _cmbGroup.DisplayMember = "DisplayName";
            _cmbGroup.ValueMember = "GroupId";

            var groupMap = groups.ToDictionary(g => g.GroupId);
            string ResolvePath(GroupSummaryDto g)
            {
                var stack = new List<string> { g.GroupName };
                var cur = g.ParentGroupId;
                var visited = new HashSet<int> { g.GroupId };
                while (cur.HasValue && visited.Add(cur.Value) && groupMap.TryGetValue(cur.Value, out var parent))
                {
                    stack.Insert(0, parent.GroupName);
                    cur = parent.ParentGroupId;
                }
                return string.Join(" > ", stack);
            }

            var items = groups
                .OrderBy(g => ResolvePath(g))
                .Select(g => new
                {
                    GroupId = g.GroupId,
                    DisplayName = $"{ResolvePath(g)} ({g.Nature})"
                }).ToList();

            _cmbGroup.DataSource = items;

            if (_ledgerId.HasValue)
            {
                var ledger = await _ledgerService.GetLedgerDetailsAsync(_ledgerId.Value);
                if (ledger != null)
                {
                    _txtName.Text = ledger.LedgerName;
                    _cmbGroup.SelectedValue = ledger.GroupId;
                    _numOpeningBalance.Value = ledger.OpeningBalance;
                    _cmbBalanceType.SelectedIndex = ledger.OpeningBalanceType == BalanceType.Debit ? 0 : 1;
                    _txtAddress.Text = ledger.Address;
                    _txtState.Text = ledger.State;
                    _txtPhone.Text = ledger.Phone;
                    _txtEmail.Text = ledger.Email;
                    _txtPAN.Text = ledger.PAN;
                    _txtBankName.Text = ledger.BankName;
                    _txtBankAccountNumber.Text = ledger.BankAccountNumber;
                    _txtIFSC.Text = ledger.IFSC;
                    _numCreditLimit.Value = ledger.CreditLimit;
                    _numCreditDays.Value = ledger.CreditDays;
                    _chkIsActive.Checked = ledger.IsActive;
                }
            }
            else if (_initialGroupId.HasValue)
            {
                _cmbGroup.SelectedValue = _initialGroupId.Value;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load ledger data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task OnSaveAsync()
    {
        var name = _txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Please enter a ledger name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtName.Focus();
            return;
        }

        if (_cmbGroup.SelectedValue == null)
        {
            MessageBox.Show("Please select a group.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _cmbGroup.Focus();
            return;
        }

        var groupId = (int)_cmbGroup.SelectedValue;
        var balanceType = _cmbBalanceType.SelectedIndex == 0 ? BalanceType.Debit : BalanceType.Credit;

        try
        {
            _btnSave.Enabled = false;

            if (_ledgerId.HasValue)
            {
                var updateDto = new LedgerUpdateDto
                {
                    LedgerId = _ledgerId.Value,
                    GroupId = groupId,
                    LedgerName = name,
                    OpeningBalance = _numOpeningBalance.Value,
                    OpeningBalanceType = balanceType,
                    Address = _txtAddress.Text.Trim(),
                    State = _txtState.Text.Trim(),
                    Phone = _txtPhone.Text.Trim(),
                    Email = _txtEmail.Text.Trim(),
                    PAN = _txtPAN.Text.Trim().ToUpperInvariant(),
                    BankName = _txtBankName.Text.Trim(),
                    BankAccountNumber = _txtBankAccountNumber.Text.Trim(),
                    IFSC = _txtIFSC.Text.Trim().ToUpperInvariant(),
                    CreditLimit = _numCreditLimit.Value,
                    CreditDays = (int)_numCreditDays.Value,
                    IsActive = _chkIsActive.Checked
                };

                await _ledgerService.UpdateLedgerAsync(updateDto);
            }
            else
            {
                var createDto = new LedgerCreateDto
                {
                    GroupId = groupId,
                    LedgerName = name,
                    OpeningBalance = _numOpeningBalance.Value,
                    OpeningBalanceType = balanceType,
                    Address = _txtAddress.Text.Trim(),
                    State = _txtState.Text.Trim(),
                    Phone = _txtPhone.Text.Trim(),
                    Email = _txtEmail.Text.Trim(),
                    PAN = _txtPAN.Text.Trim().ToUpperInvariant(),
                    BankName = _txtBankName.Text.Trim(),
                    BankAccountNumber = _txtBankAccountNumber.Text.Trim(),
                    IFSC = _txtIFSC.Text.Trim().ToUpperInvariant(),
                    CreditLimit = _numCreditLimit.Value,
                    CreditDays = (int)_numCreditDays.Value
                };

                await _ledgerService.CreateLedgerAsync(_companyId, createDto);
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save ledger: {ex.Message}", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            _btnSave.Enabled = true;
        }
    }
}
