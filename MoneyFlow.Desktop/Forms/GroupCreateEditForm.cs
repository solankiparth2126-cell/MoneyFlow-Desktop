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

    private Panel headerPanel = null!;
    private Panel pnlScroll = null!;
    private Panel card = null!;
    private Label lblSection = null!;
    private Panel sepLine = null!;
    private bool _isSaving;

    public MoneyFlowField FieldName => fldName;
    public MoneyFlowField FieldAlias => fldAlias;
    public MoneyFlowField FieldUnder => fldUnder;
    public MoneyFlowField FieldSubLedger => fldSubLedger;
    public MoneyFlowField FieldNettBalances => fldNettBalances;
    public MoneyFlowField FieldCalculation => fldCalculation;
    public MoneyFlowField FieldMethodToAllocate => fldMethodToAllocate;
    public MoneyFlowField FieldNature => fldNature;
    public MoneyFlowField FieldAffectsPL => fldAffectsPL;
    public Button ButtonSave => btnSave;
    public Button ButtonCancel => btnCancel;
    public Panel CardPanel => card;
    public Panel HeaderPanel => headerPanel;
    public Label SectionLabel => lblSection;
    public int ActiveFieldIndex => _activeFieldIndex;

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

        // ── 1. Top Header Bar (Full Width, Crisp ERP styling) ──
        headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = ExecLedgerTheme.PrimaryNavy // #1B365D
        };

        var lblBrand = new Label
        {
            Text = "MONEYFLOW DESKTOP ERP",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(147, 197, 253), // #93C5FD
            Location = new Point(24, 7),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblBrand);

        var lblTitle = new Label
        {
            Text = isEdit ? "ALTER GROUP" : "GROUP CREATION",
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(24, 21),
            AutoSize = true
        };
        headerPanel.Controls.Add(lblTitle);

        var lblShortcutHelp = new Label
        {
            Text = "ENTER = Next Field   SHIFT+ENTER = Previous   F4 = Lookup   ESC = Cancel / Discard",
            Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
            ForeColor = Color.FromArgb(203, 213, 225),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            AutoSize = true
        };
        void PositionShortcutHelp()
        {
            if (headerPanel.Width > 0)
            {
                lblShortcutHelp.Location = new Point(Math.Max(260, headerPanel.Width - lblShortcutHelp.Width - 24), 16);
            }
        }
        headerPanel.Controls.Add(lblShortcutHelp);
        headerPanel.Resize += (s, e) => PositionShortcutHelp();
        Controls.Add(headerPanel);

        // ── 2. Scrollable Body ──
        pnlScroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Color.FromArgb(240, 242, 245), // Workspace #F0F2F5
            Padding = new Padding(24, 16, 24, 24)
        };

        // Form Card (responsive centered container, comfortable 780-840px width, content-hugging height)
        card = new Panel
        {
            BackColor = Color.White,
            Location = new Point(24, 16),
            Padding = new Padding(28, 20, 28, 24)
        };
        card.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        // Section Title: Group Information
        lblSection = new Label
        {
            Text = "Group Information",
            Location = new Point(28, 20),
            AutoSize = true,
            Font = new Font("Segoe UI", 11.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42) // #0F172A
        };
        card.Controls.Add(lblSection);

        var lblSectionSub = new Label
        {
            Text = isEdit ? "Modify group details, parent hierarchy, and accounting classification" : "Define group parameters, parent hierarchy, and classification",
            Location = new Point(28, 41),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139) // Slate-500
        };
        card.Controls.Add(lblSectionSub);

        sepLine = new Panel
        {
            Location = new Point(28, 60),
            Height = 1,
            BackColor = Color.FromArgb(226, 232, 240)
        };
        card.Controls.Add(sepLine);

        int curY = 74;
        int labelWidth = 290;
        int contentX = 28 + labelWidth + 4; // 322

        // 1. Name * (prominent primary identifier)
        txtGroupName = CreateActiveTextBox(isProminent: true);
        txtGroupName.TextChanged += (s, e) =>
        {
            if (!_isInitializing) _isDirty = true;
        };
        txtGroupName.KeyDown += (s, e) => HandleEditorKeyDown(0, e);
        fldName = new MoneyFlowField("Name *", labelWidth, rowHeight: 36) { Location = new Point(28, curY) };
        fldName.SetEditor(txtGroupName, () => txtGroupName.Text.Trim());
        fldName.Activated += (s, e) => SetActiveField(0);
        card.Controls.Add(fldName);
        _fields.Add(fldName);
        curY += 40;

        // 2. (alias)
        txtAlias = CreateActiveTextBox(isProminent: false);
        txtAlias.TextChanged += (s, e) =>
        {
            if (!_isInitializing) _isDirty = true;
        };
        txtAlias.KeyDown += (s, e) => HandleEditorKeyDown(1, e);
        fldAlias = new MoneyFlowField("(alias)", labelWidth, rowHeight: 36) { Location = new Point(28, curY) };
        fldAlias.SetEditor(txtAlias, () => txtAlias.Text.Trim());
        fldAlias.Activated += (s, e) => SetActiveField(1);
        card.Controls.Add(fldAlias);
        _fields.Add(fldAlias);
        curY += 40;

        // 3. Under
        cmbParentGroup = new MoneyFlowTextLookup { AutoOpenOnFocus = true };
        cmbParentGroup.SelectedValueChanged += OnParentGroupChanged;
        cmbParentGroup.SelectionCommitted += (s, e) => MoveToNextField(2);
        cmbParentGroup.EditorKeyDown += (s, e) => HandleEditorKeyDown(2, e);
        cmbParentGroup.KeyDown += (s, e) => HandleEditorKeyDown(2, e);
        fldUnder = new MoneyFlowField("Under", labelWidth, rowHeight: 36) { Location = new Point(28, curY) };
        fldUnder.SetEditor(cmbParentGroup, () => cmbParentGroup.SelectedItem?.Name ?? "[Primary Group — No Parent]");
        fldUnder.Activated += (s, e) => SetActiveField(2);
        card.Controls.Add(fldUnder);
        _fields.Add(fldUnder);
        curY += 40;

        // 4. Group behaves like a sub-ledger
        cmbSubLedger = CreateBinaryLookup("No");
        cmbSubLedger.SelectionCommitted += (s, e) => MoveToNextField(3);
        cmbSubLedger.EditorKeyDown += (s, e) => HandleEditorKeyDown(3, e);
        cmbSubLedger.KeyDown += (s, e) => HandleEditorKeyDown(3, e);
        fldSubLedger = new MoneyFlowField("Group behaves like a sub-ledger", labelWidth, rowHeight: 36) { Location = new Point(28, curY) };
        fldSubLedger.SetEditor(cmbSubLedger, () => cmbSubLedger.SelectedValue?.ToString() ?? "No");
        fldSubLedger.Activated += (s, e) => SetActiveField(3);
        card.Controls.Add(fldSubLedger);
        _fields.Add(fldSubLedger);
        curY += 40;

        // 5. Nett Debit/Credit Balances for Reporting
        cmbNettBalances = CreateBinaryLookup("No");
        cmbNettBalances.SelectionCommitted += (s, e) => MoveToNextField(4);
        cmbNettBalances.EditorKeyDown += (s, e) => HandleEditorKeyDown(4, e);
        cmbNettBalances.KeyDown += (s, e) => HandleEditorKeyDown(4, e);
        fldNettBalances = new MoneyFlowField("Nett Debit/Credit Balances for Reporting", labelWidth, rowHeight: 36) { Location = new Point(28, curY) };
        fldNettBalances.SetEditor(cmbNettBalances, () => cmbNettBalances.SelectedValue?.ToString() ?? "No");
        fldNettBalances.Activated += (s, e) => SetActiveField(4);
        card.Controls.Add(fldNettBalances);
        _fields.Add(fldNettBalances);
        curY += 40;

        // 6. Used for calculation
        cmbCalculation = CreateBinaryLookup("No");
        cmbCalculation.SelectionCommitted += (s, e) => MoveToNextField(5);
        cmbCalculation.EditorKeyDown += (s, e) => HandleEditorKeyDown(5, e);
        cmbCalculation.KeyDown += (s, e) => HandleEditorKeyDown(5, e);
        fldCalculation = new MoneyFlowField("Used for calculation", labelWidth, rowHeight: 36) { Location = new Point(28, curY) };
        fldCalculation.SetEditor(cmbCalculation, () => cmbCalculation.SelectedValue?.ToString() ?? "No");
        fldCalculation.Activated += (s, e) => SetActiveField(5);
        card.Controls.Add(fldCalculation);
        _fields.Add(fldCalculation);
        curY += 40;

        // 7. Method to allocate when used in purchase invoice
        cmbMethodToAllocate = new MoneyFlowTextLookup { AutoOpenOnFocus = true };
        var allocOptions = new[] { "Not Applicable", "Appropriate by Qty", "Appropriate by Value" };
        cmbMethodToAllocate.SetProvider(new ListLookupProvider<string>(allocOptions, x => x, x => x), new LookupConfig
        {
            Title = "METHOD TO ALLOCATE",
            AllowClear = false
        });
        cmbMethodToAllocate.SelectedValue = "Not Applicable";
        cmbMethodToAllocate.SelectionCommitted += (s, e) => MoveToNextField(6);
        cmbMethodToAllocate.EditorKeyDown += (s, e) => HandleEditorKeyDown(6, e);
        cmbMethodToAllocate.KeyDown += (s, e) => HandleEditorKeyDown(6, e);
        fldMethodToAllocate = new MoneyFlowField("Method to allocate when used in purchase invoice", labelWidth, rowHeight: 36) { Location = new Point(28, curY) };
        fldMethodToAllocate.SetEditor(cmbMethodToAllocate, () => cmbMethodToAllocate.SelectedValue?.ToString() ?? "Not Applicable");
        fldMethodToAllocate.Activated += (s, e) => SetActiveField(6);
        card.Controls.Add(fldMethodToAllocate);
        _fields.Add(fldMethodToAllocate);
        curY += 40;

        // 8. Nature
        cmbNature = new MoneyFlowTextLookup { AutoOpenOnFocus = true };
        cmbNature.SetProvider(new EnumLookupProvider<GroupNature>(), new LookupConfig
        {
            Title = "GROUP NATURE",
            AllowClear = false
        });
        cmbNature.SelectedValue = GroupNature.Assets;
        cmbNature.SelectionCommitted += (s, e) => MoveToNextField(7);
        cmbNature.EditorKeyDown += (s, e) => HandleEditorKeyDown(7, e);
        cmbNature.KeyDown += (s, e) => HandleEditorKeyDown(7, e);
        fldNature = new MoneyFlowField("Nature", labelWidth, rowHeight: 36) { Location = new Point(28, curY) };
        fldNature.SetEditor(cmbNature, () => cmbNature.SelectedItem?.Name ?? (cmbNature.SelectedValue is GroupNature gn ? gn.ToString() : (cmbNature.SelectedValue is int gi ? ((GroupNature)gi).ToString() : "Assets")));
        fldNature.Activated += (s, e) => SetActiveField(7);
        card.Controls.Add(fldNature);
        _fields.Add(fldNature);
        curY += 40;

        // 9. Affects Gross / Net Profit / Loss
        cmbAffectsPL = CreateBinaryLookup("No");
        cmbAffectsPL.SelectionCommitted += (s, e) => MoveToNextField(8);
        cmbAffectsPL.EditorKeyDown += (s, e) => HandleEditorKeyDown(8, e);
        cmbAffectsPL.KeyDown += (s, e) => HandleEditorKeyDown(8, e);
        fldAffectsPL = new MoneyFlowField("Affects Gross / Net Profit / Loss", labelWidth, rowHeight: 36) { Location = new Point(28, curY) };
        fldAffectsPL.SetEditor(cmbAffectsPL, () => cmbAffectsPL.SelectedValue?.ToString() ?? "No");
        fldAffectsPL.Activated += (s, e) => SetActiveField(8);
        card.Controls.Add(fldAffectsPL);
        _fields.Add(fldAffectsPL);
        curY += 48;

        // Separator line before action area
        var footerSep = new Panel
        {
            Location = new Point(28, curY - 10),
            Height = 1,
            BackColor = Color.FromArgb(226, 232, 240)
        };
        card.Controls.Add(footerSep);

        // ── Actions: Save & Cancel ──
        int btnX = contentX; // Align buttons with input controls
        btnSave = new Button
        {
            Text = isEdit ? "Save Changes (Enter)" : "Create Group (Enter)",
            Location = new Point(btnX, curY),
            Size = new Size(180, 38),
            BackColor = Color.FromArgb(16, 185, 129), // #10B981 Emerald
            ForeColor = Color.White,
            Font = ExecLedgerTheme.UIBold9,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TabStop = true
        };
        btnSave.FlatAppearance.BorderSize = 0;
        btnSave.FlatAppearance.MouseOverBackColor = Color.FromArgb(5, 150, 105);
        btnSave.FlatAppearance.MouseDownBackColor = Color.FromArgb(4, 120, 87);
        btnSave.Click += async (s, e) => await SaveAsync();
        btnSave.KeyDown += OnButtonSaveKeyDown;
        card.Controls.Add(btnSave);

        btnCancel = new Button
        {
            Text = "Cancel (Esc)",
            Location = new Point(btnX + 195, curY),
            Size = new Size(120, 38),
            BackColor = Color.FromArgb(248, 250, 252),
            ForeColor = Color.FromArgb(51, 65, 85),
            Font = ExecLedgerTheme.UIRegular9,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TabStop = true
        };
        btnCancel.FlatAppearance.BorderSize = 1;
        btnCancel.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        btnCancel.FlatAppearance.MouseOverBackColor = Color.FromArgb(226, 232, 240);
        btnCancel.FlatAppearance.MouseDownBackColor = Color.FromArgb(203, 213, 225);
        btnCancel.Click += (s, e) => HandleCancel();
        btnCancel.KeyDown += OnButtonCancelKeyDown;
        card.Controls.Add(btnCancel);

        card.Height = curY + 38 + 24; // 442 + 38 + 24 = 504 (tight, content-hugging, no empty void)

        void UpdateCardLayout()
        {
            int availableWidth = pnlScroll.ClientSize.Width;
            // Responsive centered container: 740px to 840px on desktop
            int cardWidth = Math.Clamp(availableWidth - 48, 740, 840);
            int cardX = Math.Max(24, (availableWidth - cardWidth) / 2);
            card.Location = new Point(cardX, 16);
            card.Width = cardWidth;
            sepLine.Width = cardWidth - 56;
            footerSep.Width = cardWidth - 56;

            int editorW = Math.Clamp(cardWidth - contentX - 36, 360, 480);
            foreach (var fld in _fields)
            {
                fld.Width = cardWidth - 56;
                fld.UpdateEditorWidth(editorW);
            }
            card.Invalidate();
        }

        pnlScroll.Resize += (s, e) => UpdateCardLayout();
        pnlScroll.Controls.Add(card);
        Controls.Add(pnlScroll);
        headerPanel.SendToBack();

        UpdateCardLayout();
        PositionShortcutHelp();

        // Window Shortcuts
        KeyDown += OnFormKeyDown;

        // Initial Focus: activate first field (Name)
        Shown += (s, e) =>
        {
            SetActiveField(0);
        };
    }

    private static TextBox CreateActiveTextBox(bool isProminent = false)
    {
        return new TextBox
        {
            Font = new Font("Segoe UI", isProminent ? 10F : 9.5F, isProminent ? FontStyle.Bold : FontStyle.Regular),
            BackColor = Color.White,
            ForeColor = Color.FromArgb(15, 23, 42),
            BorderStyle = BorderStyle.None
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

    public void MoveToNextField(int currentIndex)
    {
        int next = currentIndex + 1;
        while (next < _fields.Count && (!_fields[next].Enabled || (_fields[next].Editor != null && !_fields[next].Editor!.Enabled)))
        {
            next++;
        }

        if (next < _fields.Count)
        {
            SetActiveField(next);
        }
        else
        {
            FocusSaveButton();
        }
    }

    public void MoveToPreviousField(int currentIndex)
    {
        int prev = currentIndex - 1;
        while (prev >= 0 && (!_fields[prev].Enabled || (_fields[prev].Editor != null && !_fields[prev].Editor!.Enabled)))
        {
            prev--;
        }

        if (prev >= 0)
        {
            SetActiveField(prev);
        }
    }

    public void FocusSaveButton()
    {
        for (int i = 0; i < _fields.Count; i++)
        {
            _fields[i].SetActive(false);
        }
        _activeFieldIndex = -1;
        ActiveControl = btnSave;
        btnSave.Focus();
    }

    private void HandleEditorKeyDown(int fieldIndex, KeyEventArgs e)
    {
        // 1. Shift+Enter or Shift+Tab -> Previous Field
        if ((e.KeyCode == Keys.Enter && e.Shift) || (e.KeyCode == Keys.Tab && e.Shift))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            MoveToPreviousField(fieldIndex);
            return;
        }

        // 2. Tab without Shift -> Next Field
        if (e.KeyCode == Keys.Tab && !e.Shift)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            MoveToNextField(fieldIndex);
            return;
        }

        // 3. Plain Enter -> Next Field or Move to Save button on final field
        if (e.KeyCode == Keys.Enter && !e.Alt && !e.Control && !e.Shift)
        {
            // If editor is a MoneyFlowTextLookup and popup is currently open:
            // let popup handle committing the selection
            if (_fields[fieldIndex].Editor is MoneyFlowTextLookup lookup && lookup.IsPopupOpen)
            {
                return;
            }

            // If editor is a ComboBox and dropped down:
            if (_fields[fieldIndex].Editor is ComboBox cb && cb.DroppedDown)
            {
                cb.DroppedDown = false;
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;

            // Validate Name field if on field 0
            if (fieldIndex == 0 && string.IsNullOrWhiteSpace(txtGroupName.Text))
            {
                MoneyFlowMessageBox.Show("Please enter a valid Group Name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                SetActiveField(0);
                return;
            }

            MoveToNextField(fieldIndex);
            return;
        }

        // 4. Escape -> close popup or cancel
        if (e.KeyCode == Keys.Escape)
        {
            if (MoneyFlowLookupPopup.WasAnyPopupJustClosedByEsc)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (_fields[fieldIndex].Editor is MoneyFlowTextLookup lookup && lookup.IsPopupOpen)
            {
                lookup.ClosePopup();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (_fields[fieldIndex].Editor is ComboBox cb && cb.DroppedDown)
            {
                cb.DroppedDown = false;
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;
            HandleCancel();
            return;
        }
    }

    private void OnButtonSaveKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter && !e.Alt && !e.Control && !e.Shift)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            _ = SaveAsync();
            return;
        }

        if ((e.KeyCode == Keys.Enter && e.Shift) || (e.KeyCode == Keys.Tab && e.Shift))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            MoveToPreviousField(_fields.Count);
            return;
        }

        if (e.KeyCode == Keys.Tab && !e.Shift)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            btnCancel.Focus();
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            HandleCancel();
            return;
        }
    }

    private void OnButtonCancelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            HandleCancel();
            return;
        }

        if ((e.KeyCode == Keys.Enter && e.Shift) || (e.KeyCode == Keys.Tab && e.Shift))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            FocusSaveButton();
            return;
        }

        if (e.KeyCode == Keys.Tab && !e.Shift)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            SetActiveField(0);
            return;
        }

        if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            HandleCancel();
            return;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            if (MoneyFlowLookupPopup.WasAnyPopupJustClosedByEsc || HasOpenPopup())
            {
                CloseOpenPopup();
                return true;
            }

            HandleCancel();
            return true;
        }

        // 1. Shift+Tab or Shift+Enter -> Previous Field
        if (keyData == (Keys.Tab | Keys.Shift) || keyData == (Keys.Enter | Keys.Shift))
        {
            if (ActiveControl == btnCancel)
            {
                FocusSaveButton();
                return true;
            }
            if (ActiveControl == btnSave)
            {
                MoveToPreviousField(_fields.Count);
                return true;
            }
            if (_activeFieldIndex >= 0)
            {
                MoveToPreviousField(_activeFieldIndex);
                return true;
            }
        }

        // 2. Tab -> Next Field
        if (keyData == Keys.Tab)
        {
            if (_activeFieldIndex >= 0 && _activeFieldIndex < _fields.Count)
            {
                var curEditor = _fields[_activeFieldIndex].Editor;
                if (curEditor is MoneyFlowTextLookup lookup && lookup.IsPopupOpen)
                {
                    lookup.CloseLookup();
                }

                MoveToNextField(_activeFieldIndex);
                return true;
            }

            if (ActiveControl == btnSave)
            {
                btnCancel.Focus();
                return true;
            }

            if (ActiveControl == btnCancel)
            {
                SetActiveField(0);
                return true;
            }
        }

        // 3. Plain Enter on active field editor -> advance to next field
        if (keyData == Keys.Enter && !keyData.HasFlag(Keys.Alt) && !keyData.HasFlag(Keys.Control))
        {
            if (_activeFieldIndex >= 0 && _activeFieldIndex < _fields.Count)
            {
                var curEditor = _fields[_activeFieldIndex].Editor;
                if (curEditor is MoneyFlowTextLookup lookup && lookup.IsPopupOpen)
                {
                    // Let popup handle Enter to select item
                    return base.ProcessCmdKey(ref msg, keyData);
                }

                if (_activeFieldIndex == 0 && string.IsNullOrWhiteSpace(txtGroupName.Text))
                {
                    MoneyFlowMessageBox.Show("Please enter a valid Group Name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    SetActiveField(0);
                    return true;
                }

                MoveToNextField(_activeFieldIndex);
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
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
            MoneyFlowMessageBox.Show($"Failed to load group data: {ex.Message}", "Data Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnParentGroupChanged(object? sender, EventArgs e)
    {
        if (!_isInitializing)
        {
            _isDirty = true;
        }

        bool hasParent = cmbParentGroup.SelectedItem?.RawData is Group;
        if (hasParent)
        {
            var selected = (Group)cmbParentGroup.SelectedItem!.RawData!;
            cmbNature.SelectedValue = selected.Nature;
            cmbNature.Enabled = false;
            fldNature.Enabled = false;

            cmbAffectsPL.SelectedValue = selected.AffectProfitLoss ? "Yes" : "No";
            cmbAffectsPL.Enabled = false;
            fldAffectsPL.Enabled = false;
        }
        else
        {
            cmbNature.Enabled = true;
            fldNature.Enabled = true;

            cmbAffectsPL.Enabled = true;
            fldAffectsPL.Enabled = true;
        }

        fldUnder.UpdateDisplayValue();
        fldNature.UpdateDisplayValue();
        fldAffectsPL.UpdateDisplayValue();
    }

    private async Task SaveAsync()
    {
        if (_isSaving) return;
        _isSaving = true;

        string name = txtGroupName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            _isSaving = false;
            MoneyFlowMessageBox.Show("Group name is required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            _isSaving = false;
            MoneyFlowMessageBox.Show("Group already exists.", "Duplicate Group", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
            MoneyFlowMessageBox.Show($"Failed to save group: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetActiveField(0);
        }
        finally
        {
            btnSave.Enabled = true;
            _isSaving = false;
        }
    }

    public bool HasOpenPopup()
    {
        return cmbParentGroup.IsPopupOpen ||
               cmbSubLedger.IsPopupOpen ||
               cmbNettBalances.IsPopupOpen ||
               cmbCalculation.IsPopupOpen ||
               cmbMethodToAllocate.IsPopupOpen ||
               cmbNature.IsPopupOpen ||
               cmbAffectsPL.IsPopupOpen;
    }

    public void CloseOpenPopup()
    {
        if (cmbParentGroup.IsPopupOpen) cmbParentGroup.ClosePopup();
        if (cmbSubLedger.IsPopupOpen) cmbSubLedger.ClosePopup();
        if (cmbNettBalances.IsPopupOpen) cmbNettBalances.ClosePopup();
        if (cmbCalculation.IsPopupOpen) cmbCalculation.ClosePopup();
        if (cmbMethodToAllocate.IsPopupOpen) cmbMethodToAllocate.ClosePopup();
        if (cmbNature.IsPopupOpen) cmbNature.ClosePopup();
        if (cmbAffectsPL.IsPopupOpen) cmbAffectsPL.ClosePopup();
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
