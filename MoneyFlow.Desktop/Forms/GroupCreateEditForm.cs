using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Controls.Fields;
using MoneyFlow.Desktop.Controls.Lookup;
using MoneyFlow.Desktop.Styling;

using MoneyFlow.Desktop.Navigation;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Full-screen accounting-style Group Creation & Alteration view for MoneyFlow.
/// Follows strict Tally-style active field display:
/// - Only the currently active/focused field appears as a real white textbox with blue border.
/// - Inactive fields display their values as plain text without borders or chrome.
/// - Display value and active editor align at the exact same horizontal position.
/// - ENTER is the primary navigation key to advance from field to field.
/// - Universal lookup fields automatically open their popup list when focused.
/// </summary>
public partial class GroupCreateEditForm : Form, IBackNavigable
{
    private readonly IGroupService _groupService;
    private readonly int _companyId;
    private readonly int? _groupIdToEdit;
    private readonly int? _initialParentGroupId;

    // Field Rows
    private MoneyFlowField fldName = null!;
    private MoneyFlowField fldAlias = null!;
    private MoneyFlowField fldUnder = null!;
    private MoneyFlowField fldSubLedger = null!;
    private MoneyFlowField fldNettBalances = null!;
    private MoneyFlowField fldCalculation = null!;
    private MoneyFlowField fldMethodToAllocate = null!;
    private MoneyFlowField fldNature = null!;
    private MoneyFlowField fldAffectsPL = null!;

    // Underlying Editors
    private TextBox txtGroupName = null!;
    private TextBox txtAlias = null!;
    private MoneyFlowTextLookup cmbParentGroup = null!;
    private MoneyFlowTextLookup cmbSubLedger = null!;
    private MoneyFlowTextLookup cmbNettBalances = null!;
    private MoneyFlowTextLookup cmbCalculation = null!;
    private MoneyFlowTextLookup cmbMethodToAllocate = null!;
    private MoneyFlowTextLookup cmbNature = null!;
    private MoneyFlowTextLookup cmbAffectsPL = null!;

    private Button btnSave = null!;
    private Button btnCancel = null!;

    private readonly List<MoneyFlowField> _fields = new();
    private int _activeFieldIndex = 0;
    private bool _isInitializing = true;
    private bool _isDirty;
    private string _originalName = string.Empty;

    public bool IsSaved { get; private set; }
    public int? NewlySavedGroupId { get; private set; }

    public event Action<int>? Saved;
    public event Action? Cancelled;

    public GroupCreateEditForm(
        IGroupService groupService,
        int companyId,
        int? groupIdToEdit = null,
        int? initialParentGroupId = null)
    {
        _groupService = groupService ?? throw new ArgumentNullException(nameof(groupService));
        _companyId = companyId;
        _groupIdToEdit = groupIdToEdit;
        _initialParentGroupId = initialParentGroupId;

        InitializeComponent();
        LoadParentGroupsAndDataAsync();
    }

    private void InitializeComponent()
    {
        bool isEdit = _groupIdToEdit.HasValue;
        Text = isEdit ? "MoneyFlow — Alter Group" : "MoneyFlow — Group Creation";
        Size = new Size(1060, 680);
        MinimumSize = new Size(800, 550);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        Dock = DockStyle.Fill;
        BackColor = Color.FromArgb(240, 242, 245); // Workspace #F0F2F5
        Font = ExecLedgerTheme.UIRegular9;
        KeyPreview = true;

        // ── 1. Top Header Bar (Full Width) ──
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = ExecLedgerTheme.PrimaryNavy // #1B365D
        };

        var lblTitle = new Label
        {
            Text = isEdit ? "ALTER GROUP" : "GROUP CREATION",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(20, 12),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblTitle);

        var lblShortcutHelp = new Label
        {
            Text = "ENTER = Next Field   SHIFT+ENTER = Previous   F4 = Lookup   ESC = Cancel / Discard",
            Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
            ForeColor = Color.FromArgb(203, 213, 225),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(Width - 440, 16),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblShortcutHelp);
        Controls.Add(headerPanel);

        // ── 2. Scrollable Body ──
        var pnlScroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(24, 20, 24, 20)
        };

        // Form Card (responsive 75%–90% width)
        var card = new Panel
        {
            BackColor = Color.White,
            Location = new Point(24, 20),
            Size = new Size(960, 520),
            Padding = new Padding(32, 24, 32, 24),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        // Section Title: Group Information
        var lblSection = new Label
        {
            Text = "Group Information",
            Location = new Point(24, 16),
            AutoSize = true,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(27, 54, 93)
        };
        card.Controls.Add(lblSection);

        var sepLine = new Panel
        {
            Location = new Point(24, 42),
            Size = new Size(card.Width - 48, 1),
            BackColor = Color.FromArgb(226, 232, 240),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        card.Controls.Add(sepLine);

        int curY = 56;
        int labelWidth = 280;

        // 1. Name *
        txtGroupName = CreateActiveTextBox();
        txtGroupName.TextChanged += (s, e) =>
        {
            if (!_isInitializing) _isDirty = true;
        };
        txtGroupName.KeyDown += (s, e) => HandleEditorKeyDown(0, e);
        fldName = new MoneyFlowField("Name *", labelWidth, rowHeight: 36) { Location = new Point(24, curY) };
        fldName.SetEditor(txtGroupName, () => txtGroupName.Text.Trim());
        fldName.Activated += (s, e) => SetActiveField(0);
        card.Controls.Add(fldName);
        _fields.Add(fldName);
        curY += 38;

        // 2. (alias)
        txtAlias = CreateActiveTextBox();
        txtAlias.TextChanged += (s, e) =>
        {
            if (!_isInitializing) _isDirty = true;
        };
        txtAlias.KeyDown += (s, e) => HandleEditorKeyDown(1, e);
        fldAlias = new MoneyFlowField("(alias)", labelWidth, rowHeight: 36) { Location = new Point(24, curY) };
        fldAlias.SetEditor(txtAlias, () => txtAlias.Text.Trim());
        fldAlias.Activated += (s, e) => SetActiveField(1);
        card.Controls.Add(fldAlias);
        _fields.Add(fldAlias);
        curY += 38;

        // 3. Under
        cmbParentGroup = new MoneyFlowTextLookup { AutoOpenOnFocus = true };
        cmbParentGroup.SelectedValueChanged += OnParentGroupChanged;
        cmbParentGroup.KeyDown += (s, e) => HandleEditorKeyDown(2, e);
        fldUnder = new MoneyFlowField("Under", labelWidth, rowHeight: 36) { Location = new Point(24, curY) };
        fldUnder.SetEditor(cmbParentGroup, () => cmbParentGroup.SelectedItem?.Name ?? "[Primary Group — No Parent]");
        fldUnder.Activated += (s, e) => SetActiveField(2);
        card.Controls.Add(fldUnder);
        _fields.Add(fldUnder);
        curY += 38;

        // 4. Group behaves like a sub-ledger
        cmbSubLedger = CreateBinaryLookup("No");
        cmbSubLedger.KeyDown += (s, e) => HandleEditorKeyDown(3, e);
        fldSubLedger = new MoneyFlowField("Group behaves like a sub-ledger", labelWidth, rowHeight: 36) { Location = new Point(24, curY) };
        fldSubLedger.SetEditor(cmbSubLedger, () => cmbSubLedger.SelectedValue?.ToString() ?? "No");
        fldSubLedger.Activated += (s, e) => SetActiveField(3);
        card.Controls.Add(fldSubLedger);
        _fields.Add(fldSubLedger);
        curY += 38;

        // 5. Nett Debit/Credit Balances for Reporting
        cmbNettBalances = CreateBinaryLookup("No");
        cmbNettBalances.KeyDown += (s, e) => HandleEditorKeyDown(4, e);
        fldNettBalances = new MoneyFlowField("Nett Debit/Credit Balances for Reporting", labelWidth, rowHeight: 36) { Location = new Point(24, curY) };
        fldNettBalances.SetEditor(cmbNettBalances, () => cmbNettBalances.SelectedValue?.ToString() ?? "No");
        fldNettBalances.Activated += (s, e) => SetActiveField(4);
        card.Controls.Add(fldNettBalances);
        _fields.Add(fldNettBalances);
        curY += 38;

        // 6. Used for calculation
        cmbCalculation = CreateBinaryLookup("No");
        cmbCalculation.KeyDown += (s, e) => HandleEditorKeyDown(5, e);
        fldCalculation = new MoneyFlowField("Used for calculation", labelWidth, rowHeight: 36) { Location = new Point(24, curY) };
        fldCalculation.SetEditor(cmbCalculation, () => cmbCalculation.SelectedValue?.ToString() ?? "No");
        fldCalculation.Activated += (s, e) => SetActiveField(5);
        card.Controls.Add(fldCalculation);
        _fields.Add(fldCalculation);
        curY += 38;

        // 7. Method to allocate when used in purchase invoice (multi-line label)
        cmbMethodToAllocate = new MoneyFlowTextLookup { AutoOpenOnFocus = true };
        var allocOptions = new[] { "Not Applicable", "Appropriate by Qty", "Appropriate by Value" };
        cmbMethodToAllocate.SetProvider(new ListLookupProvider<string>(allocOptions, x => x, x => x), new LookupConfig
        {
            Title = "METHOD TO ALLOCATE",
            AllowClear = false
        });
        cmbMethodToAllocate.SelectedValue = "Not Applicable";
        cmbMethodToAllocate.KeyDown += (s, e) => HandleEditorKeyDown(6, e);
        fldMethodToAllocate = new MoneyFlowField("Method to allocate when used in purchase invoice", labelWidth, rowHeight: 44) { Location = new Point(24, curY) };
        fldMethodToAllocate.SetEditor(cmbMethodToAllocate, () => cmbMethodToAllocate.SelectedValue?.ToString() ?? "Not Applicable");
        fldMethodToAllocate.Activated += (s, e) => SetActiveField(6);
        card.Controls.Add(fldMethodToAllocate);
        _fields.Add(fldMethodToAllocate);
        curY += 46;

        // 8. Nature
        cmbNature = new MoneyFlowTextLookup { AutoOpenOnFocus = true };
        cmbNature.SetProvider(new EnumLookupProvider<GroupNature>(), new LookupConfig
        {
            Title = "GROUP NATURE",
            AllowClear = false
        });
        cmbNature.SelectedValue = GroupNature.Assets;
        cmbNature.KeyDown += (s, e) => HandleEditorKeyDown(7, e);
        fldNature = new MoneyFlowField("Nature", labelWidth, rowHeight: 36) { Location = new Point(24, curY) };
        fldNature.SetEditor(cmbNature, () => cmbNature.SelectedValue?.ToString() ?? "Assets");
        fldNature.Activated += (s, e) => SetActiveField(7);
        card.Controls.Add(fldNature);
        _fields.Add(fldNature);
        curY += 38;

        // 9. Affects Gross / Net Profit / Loss
        cmbAffectsPL = CreateBinaryLookup("No");
        cmbAffectsPL.KeyDown += (s, e) => HandleEditorKeyDown(8, e);
        fldAffectsPL = new MoneyFlowField("Affects Gross / Net Profit / Loss", labelWidth, rowHeight: 36) { Location = new Point(24, curY) };
        fldAffectsPL.SetEditor(cmbAffectsPL, () => cmbAffectsPL.SelectedValue?.ToString() ?? "No");
        fldAffectsPL.Activated += (s, e) => SetActiveField(8);
        card.Controls.Add(fldAffectsPL);
        _fields.Add(fldAffectsPL);
        curY += 48;

        // ── Actions: Save & Cancel ──
        int btnX = labelWidth + 28;
        btnSave = new Button
        {
            Text = isEdit ? "Save Changes (Enter)" : "Create Group (Enter)",
            Location = new Point(btnX, curY),
            Size = new Size(180, 36),
            BackColor = Color.FromArgb(16, 185, 129), // #10B981 Emerald
            ForeColor = Color.White,
            Font = ExecLedgerTheme.UIBold9,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.Click += async (s, e) => await SaveAsync();
        card.Controls.Add(btnSave);

        btnCancel = new Button
        {
            Text = "Cancel (Esc)",
            Location = new Point(btnX + 195, curY),
            Size = new Size(120, 36),
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = ExecLedgerTheme.UIRegular9,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderSize = 0;
        btnCancel.Click += (s, e) => HandleCancel();
        card.Controls.Add(btnCancel);

        pnlScroll.Controls.Add(card);
        Controls.Add(pnlScroll);

        // Window Shortcuts
        KeyDown += OnFormKeyDown;

        // Initial Focus: activate first field (Name)
        Shown += (s, e) =>
        {
            SetActiveField(0);
        };
    }

    private static TextBox CreateActiveTextBox()
    {
        return new TextBox
        {
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private static MoneyFlowTextLookup CreateBinaryLookup(string defaultValue)
    {
        var lookup = new MoneyFlowTextLookup { AutoOpenOnFocus = true };
        var options = new[] { "No", "Yes" };
        lookup.SetProvider(new ListLookupProvider<string>(options, s => s, s => s), new LookupConfig
        {
            Title = "OPTIONS",
            AllowClear = false
        });
        lookup.SelectedValue = defaultValue;
        return lookup;
    }

    public void SetActiveField(int index)
    {
        if (index < 0 || index >= _fields.Count) return;

        _activeFieldIndex = index;

        // Only the currently active field has its editor visible
        for (int i = 0; i < _fields.Count; i++)
        {
            _fields[i].SetActive(i == index);
        }
    }

    private void HandleEditorKeyDown(int fieldIndex, KeyEventArgs e)
    {
        // 1. Shift+Enter -> Previous Field
        if (e.KeyCode == Keys.Enter && e.Shift)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            if (fieldIndex > 0)
            {
                SetActiveField(fieldIndex - 1);
            }
            return;
        }

        // 2. Plain Enter -> Next Field or Save on final field
        if (e.KeyCode == Keys.Enter && !e.Alt && !e.Control && !e.Shift)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            // Validate Name field
            if (fieldIndex == 0 && string.IsNullOrWhiteSpace(txtGroupName.Text))
            {
                MessageBox.Show("Please enter a valid Group Name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetActiveField(0);
                return;
            }

            if (fieldIndex < _fields.Count - 1)
            {
                SetActiveField(fieldIndex + 1);
            }
            else
            {
                // Final field -> trigger Save
                _ = SaveAsync();
            }
            return;
        }

        // 3. Tab -> Next Field
        if (e.KeyCode == Keys.Tab && !e.Shift)
        {
            e.Handled = true;
            if (fieldIndex < _fields.Count - 1)
            {
                SetActiveField(fieldIndex + 1);
            }
            return;
        }

        // 4. Shift+Tab -> Previous Field
        if (e.KeyCode == Keys.Tab && e.Shift)
        {
            e.Handled = true;
            if (fieldIndex > 0)
            {
                SetActiveField(fieldIndex - 1);
            }
            return;
        }
    }

    private async void LoadParentGroupsAndDataAsync()
    {
        try
        {
            var potentialParents = await _groupService.GetPotentialParentGroupsAsync(_companyId, _groupIdToEdit);

            var items = new List<LookupItem>
            {
                new()
                {
                    Id = null,
                    Name = "[Primary Group — No Parent]",
                    Code = "PRIMARY",
                    Subtitle = null,
                    RawData = null,
                    IsSentinel = true
                }
            };

            foreach (var p in potentialParents)
            {
                items.Add(new LookupItem
                {
                    Id = p.GroupId,
                    Name = p.GroupName,
                    Code = p.Nature.ToString(),
                    Subtitle = null,
                    RawData = p
                });
            }

            var groupProvider = new ListLookupProvider<LookupItem>(items, x => x.Id, x => x.Name, x => x.Code, x => x.Subtitle);
            cmbParentGroup.SetProvider(groupProvider, new LookupConfig
            {
                Title = "SELECT UNDER GROUP",
                Placeholder = "Select Parent Group...",
                AllowClear = false
            });
            cmbParentGroup.SelectedValue = null;

            if (_groupIdToEdit.HasValue)
            {
                var group = await _groupService.GetGroupByIdAsync(_groupIdToEdit.Value);
                if (group != null)
                {
                    _originalName = group.GroupName;
                    txtGroupName.Text = group.GroupName;
                    cmbNature.SelectedValue = group.Nature;
                    cmbAffectsPL.SelectedValue = group.AffectProfitLoss ? "Yes" : "No";

                    if (group.ParentGroupId.HasValue)
                    {
                        cmbParentGroup.SelectedValue = group.ParentGroupId.Value;
                    }
                    else
                    {
                        cmbParentGroup.SelectedValue = null;
                    }

                    _isDirty = false;
                }
            }
            else if (_initialParentGroupId.HasValue)
            {
                cmbParentGroup.SelectedValue = _initialParentGroupId.Value;
                _isDirty = false;
            }

            // Update all inactive labels
            foreach (var fld in _fields)
            {
                fld.UpdateDisplayValue();
            }

            _isInitializing = false;
            _isDirty = false;

            SetActiveField(0);
        }
        catch (Exception ex)
        {
            _isInitializing = false;
            _isDirty = false;
            MessageBox.Show($"Failed to load group data: {ex.Message}", "Data Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnParentGroupChanged(object? sender, EventArgs e)
    {
        if (!_isInitializing)
        {
            _isDirty = true;
        }
        if (cmbParentGroup.SelectedItem?.RawData is Group selected)
        {
            cmbNature.SelectedValue = selected.Nature;
            cmbNature.Enabled = false;
            cmbAffectsPL.SelectedValue = selected.AffectProfitLoss ? "Yes" : "No";
            cmbAffectsPL.Enabled = false;
        }
        else
        {
            cmbNature.Enabled = true;
            cmbAffectsPL.Enabled = true;
        }

        fldUnder.UpdateDisplayValue();
        fldNature.UpdateDisplayValue();
        fldAffectsPL.UpdateDisplayValue();
    }

    private async Task SaveAsync()
    {
        string name = txtGroupName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Group name is required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SetActiveField(0);
            return;
        }

        // Check duplicate group against the current company
        var allGroups = await _groupService.GetGroupsByCompanyAsync(_companyId);
        bool isDuplicate = allGroups.Any(g =>
            string.Equals(g.GroupName.Trim(), name, StringComparison.OrdinalIgnoreCase) &&
            (!_groupIdToEdit.HasValue || g.GroupId != _groupIdToEdit.Value));

        if (isDuplicate)
        {
            MessageBox.Show("Group already exists.", "Duplicate Group", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SetActiveField(0);
            return;
        }

        int? parentId = cmbParentGroup.SelectedValue as int?;
        GroupNature nature = cmbNature.SelectedValue is GroupNature n ? n : GroupNature.Assets;
        bool affectPL = string.Equals(cmbAffectsPL.SelectedValue?.ToString(), "Yes", StringComparison.OrdinalIgnoreCase);

        try
        {
            btnSave.Enabled = false;

            if (_groupIdToEdit.HasValue)
            {
                var updateDto = new GroupUpdateDto
                {
                    GroupId = _groupIdToEdit.Value,
                    GroupName = name,
                    ParentGroupId = parentId,
                    Nature = nature,
                    AffectProfitLoss = affectPL
                };
                var updated = await _groupService.UpdateGroupAsync(updateDto);
                NewlySavedGroupId = updated.GroupId;
            }
            else
            {
                var createDto = new GroupCreateDto
                {
                    GroupName = name,
                    ParentGroupId = parentId,
                    Nature = nature,
                    AffectProfitLoss = affectPL
                };
                var created = await _groupService.CreateGroupAsync(_companyId, createDto);
                NewlySavedGroupId = created.GroupId;
            }

            IsSaved = true;
            _isDirty = false;
            DialogResult = DialogResult.OK;

            Saved?.Invoke(NewlySavedGroupId ?? 0);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save group: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetActiveField(0);
        }
        finally
        {
            btnSave.Enabled = true;
        }
    }

    public bool HasOpenPopup()
    {
        return cmbParentGroup.IsPopupOpen || cmbSubLedger.IsPopupOpen || cmbNettBalances.IsPopupOpen;
    }

    public void CloseOpenPopup()
    {
        if (cmbParentGroup.IsPopupOpen) cmbParentGroup.ClosePopup();
        if (cmbSubLedger.IsPopupOpen) cmbSubLedger.ClosePopup();
        if (cmbNettBalances.IsPopupOpen) cmbNettBalances.ClosePopup();
    }

    public void HandleCancel()
    {
        Control? focused = ActiveControl;
        if (focused == null && _activeFieldIndex >= 0 && _activeFieldIndex < _fields.Count)
        {
            focused = _fields[_activeFieldIndex].Editor;
        }

        MoneyFlowEscController.HandleEsc(
            focused,
            this,
            closeAction: () =>
            {
                _isDirty = false;
                Cancelled?.Invoke();
                Close();
            },
            hasOpenPopup: HasOpenPopup,
            closePopupAction: CloseOpenPopup);
    }

    public bool HandleBackNavigation()
    {
        HandleCancel();
        return true;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            HandleCancel();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            HandleCancel();
        }
        else if (e.KeyCode == Keys.A && !e.Control && !e.Alt)
        {
            // 'A' acts as Accept/Save when on button or actionable state
            if (_activeFieldIndex == _fields.Count - 1)
            {
                e.Handled = true;
                _ = SaveAsync();
            }
        }
    }
}
