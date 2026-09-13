using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Forms;

public class GroupCreateEditForm : Form
{
    private readonly IGroupService _groupService;
    private readonly int _companyId;
    private readonly int? _groupIdToEdit;

    // UI Controls
    private TextBox txtGroupName = null!;
    private ComboBox cmbParentGroup = null!;
    private ComboBox cmbNature = null!;
    private CheckBox chkAffectPL = null!;
    private Button btnSave = null!;
    private Button btnCancel = null!;

    public bool IsSaved { get; private set; }

    public GroupCreateEditForm(IGroupService groupService, int companyId, int? groupIdToEdit = null)
    {
        _groupService = groupService;
        _companyId = companyId;
        _groupIdToEdit = groupIdToEdit;

        InitializeComponent();
        LoadParentGroupsAndDataAsync();
    }

    private void InitializeComponent()
    {
        bool isEdit = _groupIdToEdit.HasValue;
        this.Text = isEdit ? "MoneyFlow Desktop — Alter Group" : "MoneyFlow Desktop — Create Group";
        this.Size = new Size(520, 360);
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
            Text = isEdit ? "Alter Account Group" : "Create Account Group",
            Font = ExecLedgerTheme.UIBold11,
            ForeColor = Color.White,
            Location = new Point(18, 15),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblTitle);
        this.Controls.Add(headerPanel);

        var grp = new GroupBox
        {
            Text = "Group Details",
            Location = new Point(20, 70),
            Size = new Size(465, 185),
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        // Group Name
        grp.Controls.Add(new Label { Text = "Group Name *:", Location = new Point(20, 30), AutoSize = true });
        txtGroupName = new TextBox { Location = new Point(150, 27), Width = 290 };
        grp.Controls.Add(txtGroupName);

        // Under (Parent Group)
        grp.Controls.Add(new Label { Text = "Under (Parent):", Location = new Point(20, 68), AutoSize = true });
        cmbParentGroup = new ComboBox
        {
            Location = new Point(150, 65),
            Width = 290,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbParentGroup.SelectedIndexChanged += OnParentGroupChanged;
        grp.Controls.Add(cmbParentGroup);

        // Nature
        grp.Controls.Add(new Label { Text = "Nature:", Location = new Point(20, 106), AutoSize = true });
        cmbNature = new ComboBox
        {
            Location = new Point(150, 103),
            Width = 290,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        cmbNature.Items.AddRange(new object[] {
            GroupNature.Assets,
            GroupNature.Liabilities,
            GroupNature.Income,
            GroupNature.Expenses
        });
        cmbNature.SelectedIndex = 0;
        grp.Controls.Add(cmbNature);

        // Affect Profit & Loss
        chkAffectPL = new CheckBox
        {
            Text = "Affects Gross / Net Profit Loss",
            Location = new Point(150, 142),
            AutoSize = true
        };
        grp.Controls.Add(chkAffectPL);

        this.Controls.Add(grp);

        // Buttons
        btnSave = new Button
        {
            Text = isEdit ? "Save Changes (Enter)" : "Create Group (Enter)",
            Location = new Point(210, 270),
            Size = new Size(155, 34),
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            Font = ExecLedgerTheme.UIBold9
        };
        btnSave.Click += async (s, e) => await SaveAsync();

        btnCancel = new Button
        {
            Text = "Cancel (Esc)",
            Location = new Point(375, 270),
            Size = new Size(110, 34),
            BackColor = Color.FromArgb(226, 232, 240)
        };
        btnCancel.Click += (s, e) => this.Close();

        this.Controls.Add(btnSave);
        this.Controls.Add(btnCancel);

        this.AcceptButton = btnSave;
        this.CancelButton = btnCancel;
    }

    private class ParentGroupItem
    {
        public int? GroupId { get; set; }
        public string Name { get; set; } = string.Empty;
        public GroupNature Nature { get; set; }
        public bool AffectPL { get; set; }

        public override string ToString() => Name;
    }

    private async void LoadParentGroupsAndDataAsync()
    {
        cmbParentGroup.Items.Clear();
        cmbParentGroup.Items.Add(new ParentGroupItem { GroupId = null, Name = "[Primary Group — No Parent]" });

        var potentialParents = await _groupService.GetPotentialParentGroupsAsync(_companyId, _groupIdToEdit);
        foreach (var p in potentialParents)
        {
            cmbParentGroup.Items.Add(new ParentGroupItem
            {
                GroupId = p.GroupId,
                Name = $"{p.GroupName} ({p.Nature})",
                Nature = p.Nature,
                AffectPL = p.AffectProfitLoss
            });
        }
        cmbParentGroup.SelectedIndex = 0;

        if (_groupIdToEdit.HasValue)
        {
            var group = await _groupService.GetGroupByIdAsync(_groupIdToEdit.Value);
            if (group != null)
            {
                txtGroupName.Text = group.GroupName;
                cmbNature.SelectedItem = group.Nature;
                chkAffectPL.Checked = group.AffectProfitLoss;

                if (group.ParentGroupId.HasValue)
                {
                    for (int i = 0; i < cmbParentGroup.Items.Count; i++)
                    {
                        if (cmbParentGroup.Items[i] is ParentGroupItem item && item.GroupId == group.ParentGroupId.Value)
                        {
                            cmbParentGroup.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }
    }

    private void OnParentGroupChanged(object? sender, EventArgs e)
    {
        if (cmbParentGroup.SelectedItem is ParentGroupItem selected && selected.GroupId.HasValue)
        {
            // Inherit parent's nature and disable manual nature override
            cmbNature.SelectedItem = selected.Nature;
            cmbNature.Enabled = false;
            chkAffectPL.Checked = selected.AffectPL;
            chkAffectPL.Enabled = false;
        }
        else
        {
            cmbNature.Enabled = true;
            chkAffectPL.Enabled = true;
        }
    }

    private async Task SaveAsync()
    {
        string name = txtGroupName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Please enter a valid Group Name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            txtGroupName.Focus();
            return;
        }

        btnSave.Enabled = false;

        try
        {
            var selectedParent = cmbParentGroup.SelectedItem as ParentGroupItem;
            int? parentId = selectedParent?.GroupId;
            var nature = (GroupNature)cmbNature.SelectedItem!;

            if (_groupIdToEdit.HasValue)
            {
                var updateDto = new GroupUpdateDto
                {
                    GroupId = _groupIdToEdit.Value,
                    GroupName = name,
                    ParentGroupId = parentId,
                    Nature = nature,
                    AffectProfitLoss = chkAffectPL.Checked
                };

                await _groupService.UpdateGroupAsync(updateDto);
                MessageBox.Show($"Group '{name}' updated successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                var createDto = new GroupCreateDto
                {
                    GroupName = name,
                    ParentGroupId = parentId,
                    Nature = nature,
                    AffectProfitLoss = chkAffectPL.Checked
                };

                await _groupService.CreateGroupAsync(_companyId, createDto);
                MessageBox.Show($"Group '{name}' created successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            IsSaved = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Unable to save Group:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            btnSave.Enabled = true;
        }
    }
}
