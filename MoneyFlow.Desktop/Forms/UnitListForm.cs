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

namespace MoneyFlow.Desktop.Forms;

public class UnitListForm : Form
{
    private readonly IInventoryService _inventoryService;
    private readonly ICompanyContext _companyContext;

    private Guna2DataGridView _dgvUnits = null!;
    private TextBox _txtSearch = null!;
    private Button _btnCreate = null!;
    private Button _btnAlter = null!;
    private Button _btnDelete = null!;
    private Button _btnClose = null!;
    private Label _lblStatus = null!;

    private IReadOnlyList<UnitDto> _allUnits = new List<UnitDto>();

    public UnitListForm(
        IInventoryService inventoryService,
        ICompanyContext companyContext)
    {
        _inventoryService = inventoryService;
        _companyContext = companyContext;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Units of Measure (Inventory Masters)";
        Size = new Size(820, 520);
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

        // 1. Top Filter
        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight
        };

        var lblSearch = new Label { Text = "Search Unit (F3):", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 0) };
        _txtSearch = new TextBox { Width = 280, PlaceholderText = "Search symbol or formal name..." };
        _txtSearch.TextChanged += (s, e) => FilterUnits();

        topPanel.Controls.Add(lblSearch);
        topPanel.Controls.Add(_txtSearch);
        mainLayout.Controls.Add(topPanel, 0, 0);

        // 2. DataGridView
        _dgvUnits = new Guna2DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.Fixed3D,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        _dgvUnits.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColSymbol", HeaderText = "Symbol / Name", FillWeight = 30 });
        _dgvUnits.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColFormalName", HeaderText = "Formal Name", FillWeight = 40 });
        _dgvUnits.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColDecimals", HeaderText = "Decimal Places", FillWeight = 15, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _dgvUnits.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColStatus", HeaderText = "Status", FillWeight = 15, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });

        _dgvUnits.DoubleClick += (s, e) => AlterSelectedUnit();
        _dgvUnits.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                AlterSelectedUnit();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                DeleteSelectedUnit();
                e.Handled = true;
            }
        };

        mainLayout.Controls.Add(_dgvUnits, 0, 1);

        // 3. Status Label
        _lblStatus = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.FromArgb(70, 80, 95),
            Font = ExecLedgerTheme.UIRegular9
        };
        mainLayout.Controls.Add(_lblStatus, 0, 2);

        // 4. Action Buttons
        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };

        _btnClose = new Button { Text = "Close (Esc)", Width = 110, Height = 36 };
        _btnClose.Click += (s, e) => Close();

        _btnDelete = new Button { Text = "Delete", Width = 100, Height = 36, BackColor = Color.FromArgb(254, 226, 226), ForeColor = Color.FromArgb(185, 28, 28) };
        _btnDelete.Click += (s, e) => DeleteSelectedUnit();

        _btnAlter = new Button { Text = "Edit (Enter)", Width = 110, Height = 36 };
        _btnAlter.Click += (s, e) => AlterSelectedUnit();

        _btnCreate = new Button { Text = "Create Unit (Alt+C)", Width = 150, Height = 36, BackColor = Color.FromArgb(24, 43, 73), ForeColor = Color.White };
        _btnCreate.Click += (s, e) => OpenCreateUnitDialog();

        actionPanel.Controls.Add(_btnClose);
        actionPanel.Controls.Add(_btnDelete);
        actionPanel.Controls.Add(_btnAlter);
        actionPanel.Controls.Add(_btnCreate);
        mainLayout.Controls.Add(actionPanel, 0, 3);

        Controls.Add(mainLayout);

        KeyDown += UnitListForm_KeyDown;
        Load += async (s, e) => await LoadUnitsAsync();
    }

    private async Task LoadUnitsAsync()
    {
        var company = _companyContext.CurrentCompany;
        if (company == null) return;

        try
        {
            UseWaitCursor = true;
            _allUnits = await _inventoryService.GetUnitsByCompanyAsync(company.CompanyId);
            FilterUnits();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error loading Units: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void FilterUnits()
    {
        _dgvUnits.Rows.Clear();

        var query = _allUnits.AsEnumerable();
        var search = _txtSearch.Text.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u => u.UnitName.ToLowerInvariant().Contains(search) ||
                                     u.FormalName.ToLowerInvariant().Contains(search));
        }

        var list = query.OrderBy(u => u.UnitName).ToList();

        foreach (var u in list)
        {
            var idx = _dgvUnits.Rows.Add();
            var row = _dgvUnits.Rows[idx];
            row.Tag = u;

            row.Cells["ColSymbol"].Value = u.UnitName;
            row.Cells["ColFormalName"].Value = u.FormalName;
            row.Cells["ColDecimals"].Value = u.DecimalPlaces;
            row.Cells["ColStatus"].Value = u.IsActive ? "Active" : "Inactive";
        }

        _lblStatus.Text = $"Total Units: {list.Count} (of {_allUnits.Count})";
    }

    private void OpenCreateUnitDialog()
    {
        using var createForm = new UnitCreateEditForm(_inventoryService, _companyContext);
        if (createForm.ShowDialog(this) == DialogResult.OK)
        {
            _ = LoadUnitsAsync();
        }
    }

    private void AlterSelectedUnit()
    {
        if (_dgvUnits.CurrentRow?.Tag is not UnitDto selected) return;

        using var editForm = new UnitCreateEditForm(_inventoryService, _companyContext, selected.UnitId);
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            _ = LoadUnitsAsync();
        }
    }

    private async void DeleteSelectedUnit()
    {
        if (_dgvUnits.CurrentRow?.Tag is not UnitDto selected) return;

        var confirm = MessageBox.Show(
            this,
            $"Are you sure you want to delete Unit '{selected.UnitName}' ({selected.FormalName})?",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        try
        {
            bool deleted = await _inventoryService.DeleteUnitAsync(selected.UnitId);
            if (deleted)
            {
                await LoadUnitsAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to delete Unit: {ex.Message}", "Delete Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void UnitListForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F3)
        {
            _txtSearch.Focus();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F5)
        {
            _ = LoadUnitsAsync();
            e.Handled = true;
        }
        else if (e.Alt && e.KeyCode == Keys.C)
        {
            OpenCreateUnitDialog();
            e.Handled = true;
        }
    }
}
