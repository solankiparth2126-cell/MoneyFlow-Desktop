using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;
using Guna.UI2.WinForms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;

using MoneyFlow.Desktop.Controls.Lookup;
using MoneyFlow.Desktop.Navigation;

namespace MoneyFlow.Desktop.Forms;

public class LedgerListForm : Form
{
    private readonly ILedgerService _ledgerService;
    private readonly IGroupService _groupService;
    private readonly ICompanyContext _companyContext;

    private Guna2DataGridView _dgvLedgers = null!;
    private TextBox _txtSearch = null!;
    private MoneyFlowTextLookup _cmbGroupFilter = null!;
    private Button _btnCreate = null!;
    private Button _btnAlter = null!;
    private Button _btnDelete = null!;
    private Button _btnClose = null!;
    private Label _lblStatus = null!;
    private bool _isInitializing;

    public LedgerListForm(
        ILedgerService ledgerService,
        IGroupService groupService,
        ICompanyContext companyContext)
    {
        _ledgerService = ledgerService;
        _groupService = groupService;
        _companyContext = companyContext;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Ledgers (Chart of Accounts)";
        Size = new Size(950, 600);
        StartPosition = FormStartPosition.CenterParent;
        Font = ExecLedgerTheme.UIRegular9;
        KeyPreview = true;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // Header & Filter
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); // Status
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Actions

        // 1. Top Panel (Search & Group Filter)
        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0)
        };

        var lblSearch = new Label
        {
            Text = "Search:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 7, 5, 0)
        };
        _txtSearch = new TextBox
        {
            Width = 220,
            Font = ExecLedgerTheme.UIRegular10
        };
        _txtSearch.TextChanged += async (s, e) => await LoadLedgersAsync();

        var lblGroup = new Label
        {
            Text = "Group Filter:",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(20, 7, 5, 0)
        };
        _cmbGroupFilter = new MoneyFlowTextLookup
        {
            Width = 240,
            Height = 28
        };
        _cmbGroupFilter.SelectedValueChanged += async (s, e) =>
        {
            if (!_isInitializing) await LoadLedgersAsync();
        };

        topPanel.Controls.Add(lblSearch);
        topPanel.Controls.Add(_txtSearch);
        topPanel.Controls.Add(lblGroup);
        topPanel.Controls.Add(_cmbGroupFilter);

        // 2. DataGridView
        _dgvLedgers = new Guna2DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            RowHeadersVisible = false,
            BorderStyle = BorderStyle.FixedSingle,
            GridColor = Color.FromArgb(230, 230, 230),
            RowTemplate = { Height = 28 }
        };

        _dgvLedgers.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "LedgerId",
            HeaderText = "ID",
            Width = 60,
            Visible = false
        });

        _dgvLedgers.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "LedgerName",
            HeaderText = "Ledger Name",
            Width = 280
        });

        _dgvLedgers.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "DisplayGroup",
            HeaderText = "Under Group (Hierarchy)",
            Width = 260
        });

        _dgvLedgers.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "GroupNature",
            HeaderText = "Nature",
            Width = 120
        });

        _dgvLedgers.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = "FormattedOpeningBalance",
            HeaderText = "Opening Balance",
            Width = 150,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
        });

        _dgvLedgers.Columns.Add(new DataGridViewCheckBoxColumn
        {
            DataPropertyName = "IsActive",
            HeaderText = "Active",
            Width = 60
        });

        _dgvLedgers.CellDoubleClick += async (s, e) => await OnAlterAsync();

        // 3. Status Bar
        _lblStatus = new Label
        {
            Text = "Ready",
            Dock = DockStyle.Fill,
            ForeColor = Color.DimGray,
            Font = ExecLedgerTheme.UIRegular9
        };

        // 4. Action Buttons
        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0, 5, 0, 0)
        };

        _btnCreate = new Button
        {
            Text = "Create (Alt+C)",
            BackColor = ExecLedgerTheme.PrimaryNavy,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(130, 34),
            Font = ExecLedgerTheme.UIBold9
        };
        _btnCreate.FlatAppearance.BorderSize = 0;
        _btnCreate.Click += async (s, e) => await OnCreateAsync();

        _btnAlter = new Button
        {
            Text = "Alter (Alt+A)",
            BackColor = Color.FromArgb(235, 243, 250),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 34),
            Font = ExecLedgerTheme.UIRegular9
        };
        _btnAlter.FlatAppearance.BorderColor = ExecLedgerTheme.PrimaryNavy;
        _btnAlter.Click += async (s, e) => await OnAlterAsync();

        _btnDelete = new Button
        {
            Text = "Delete (Alt+D)",
            BackColor = Color.FromArgb(253, 237, 237),
            ForeColor = Color.FromArgb(192, 0, 0),
            FlatStyle = FlatStyle.Flat,
            Size = new Size(120, 34),
            Font = ExecLedgerTheme.UIRegular9
        };
        _btnDelete.FlatAppearance.BorderColor = Color.FromArgb(192, 0, 0);
        _btnDelete.Click += async (s, e) => await OnDeleteAsync();

        _btnClose = new Button
        {
            Text = "Close (Esc)",
            Size = new Size(100, 34),
            Font = ExecLedgerTheme.UIRegular9
        };
        _btnClose.Click += (s, e) => Close();

        actionPanel.Controls.Add(_btnCreate);
        actionPanel.Controls.Add(_btnAlter);
        actionPanel.Controls.Add(_btnDelete);
        actionPanel.Controls.Add(_btnClose);

        mainLayout.Controls.Add(topPanel, 0, 0);
        mainLayout.Controls.Add(_dgvLedgers, 0, 1);
        mainLayout.Controls.Add(_lblStatus, 0, 2);
        mainLayout.Controls.Add(actionPanel, 0, 3);

        Controls.Add(mainLayout);

        KeyDown += async (s, e) =>
        {
            if (e.Alt && e.KeyCode == Keys.C)
            {
                e.Handled = true;
                await OnCreateAsync();
            }
            else if ((e.Alt && e.KeyCode == Keys.A) || (!e.Control && !e.Alt && e.KeyCode == Keys.Enter && _dgvLedgers.Focused))
            {
                e.Handled = true;
                await OnAlterAsync();
            }
            else if (e.Alt && e.KeyCode == Keys.D)
            {
                e.Handled = true;
                await OnDeleteAsync();
            }
            else if (e.KeyCode == Keys.F5)
            {
                e.Handled = true;
                await LoadLedgersAsync();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                MoneyFlowEscController.HandleEsc(
                    ActiveControl,
                    this,
                    closeAction: () => Close());
            }
        };

        Load += async (s, e) => await OnFormLoadAsync();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            return MoneyFlowEscController.HandleEsc(
                ActiveControl,
                this,
                closeAction: () => Close());
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async Task OnFormLoadAsync()
    {
        if (_companyContext.CurrentCompany == null)
        {
            MessageBox.Show("Please select an active company first.", "No Company Selected", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Close();
            return;
        }

        try
        {
            var groups = await _groupService.GetGroupsByCompanyAsync(_companyContext.CurrentCompany.CompanyId);
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

            var groupItems = new List<LookupItem>
            {
                new LookupItem { Id = null, Name = "-- All Groups --", IsSentinel = true }
            };

            foreach (var g in groups.OrderBy(g => ResolvePath(g)))
            {
                groupItems.Add(new LookupItem
                {
                    Id = g.GroupId,
                    Name = g.GroupName,
                    Subtitle = ResolvePath(g),
                    Code = g.Nature.ToString(),
                    RawData = g
                });
            }

            var groupProvider = new ListLookupProvider<LookupItem>(groupItems, x => x.Id, x => x.Name, x => x.Code, x => x.Subtitle);
            _cmbGroupFilter.SetProvider(groupProvider, new LookupConfig
            {
                Title = "FILTER BY GROUP",
                Placeholder = "-- All Groups --",
                AllowClear = false
            });

            _isInitializing = true;
            try
            {
                _cmbGroupFilter.SelectedValue = null;
            }
            finally
            {
                _isInitializing = false;
            }

            await LoadLedgersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load filter groups: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadLedgersAsync()
    {
        if (_isInitializing || _companyContext.CurrentCompany == null) return;

        try
        {
            int? selectedGroupId = _cmbGroupFilter.SelectedValue as int?;

            var ledgers = await _ledgerService.GetLedgersByCompanyAsync(
                _companyContext.CurrentCompany.CompanyId,
                _txtSearch.Text.Trim(),
                selectedGroupId);

            _dgvLedgers.DataSource = ledgers;
            _lblStatus.Text = $"Showing {ledgers.Count} ledger(s) in company '{_companyContext.CurrentCompany.CompanyName}'";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Error: {ex.Message}";
        }
    }

    private async Task OnCreateAsync()
    {
        if (_companyContext.CurrentCompany == null) return;

        using var dialog = new LedgerCreateEditForm(_ledgerService, _groupService, _companyContext.CurrentCompany.CompanyId);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            await LoadLedgersAsync();
        }
    }

    private async Task OnAlterAsync()
    {
        if (_companyContext.CurrentCompany == null || _dgvLedgers.CurrentRow == null) return;

        if (_dgvLedgers.CurrentRow.DataBoundItem is LedgerSummaryDto selected)
        {
            using var dialog = new LedgerCreateEditForm(_ledgerService, _groupService, _companyContext.CurrentCompany.CompanyId, selected.LedgerId);
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                await LoadLedgersAsync();
            }
        }
    }

    private async Task OnDeleteAsync()
    {
        if (_companyContext.CurrentCompany == null || _dgvLedgers.CurrentRow == null) return;

        if (_dgvLedgers.CurrentRow.DataBoundItem is LedgerSummaryDto selected)
        {
            var confirm = MessageBox.Show(
                $"Are you sure you want to delete ledger '{selected.LedgerName}'?\n\nThis cannot be undone.",
                "Confirm Delete Ledger",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            try
            {
                var success = await _ledgerService.DeleteLedgerAsync(selected.LedgerId);
                if (success)
                {
                    await LoadLedgersAsync();
                    MessageBox.Show($"Ledger '{selected.LedgerName}' deleted successfully.", "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete ledger: {ex.Message}", "Delete Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
