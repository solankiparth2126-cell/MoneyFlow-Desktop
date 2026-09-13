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

public class StockItemListForm : Form
{
    private readonly IInventoryService _inventoryService;
    private readonly ICompanyContext _companyContext;

    private Guna2DataGridView _dgvStockItems = null!;
    private TextBox _txtSearch = null!;
    private Button _btnCreate = null!;
    private Button _btnAlter = null!;
    private Button _btnDelete = null!;
    private Button _btnClose = null!;
    private Label _lblStatus = null!;

    private IReadOnlyList<StockItemDto> _allItems = new List<StockItemDto>();

    public StockItemListForm(
        IInventoryService inventoryService,
        ICompanyContext companyContext)
    {
        _inventoryService = inventoryService;
        _companyContext = companyContext;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Stock Items (Inventory Masters)";
        Size = new Size(950, 580);
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

        // 1. Filter bar
        var topPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight
        };

        var lblSearch = new Label { Text = "Search Stock Item (F3):", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 8, 0) };
        _txtSearch = new TextBox { Width = 320, PlaceholderText = "Search item name or unit..." };
        _txtSearch.TextChanged += (s, e) => FilterItems();

        topPanel.Controls.Add(lblSearch);
        topPanel.Controls.Add(_txtSearch);
        mainLayout.Controls.Add(topPanel, 0, 0);

        // 2. DataGridView
        _dgvStockItems = new Guna2DataGridView
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

        _dgvStockItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColItemName", HeaderText = "Item Name", FillWeight = 40 });
        _dgvStockItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColUnit", HeaderText = "Unit", FillWeight = 15, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _dgvStockItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColQty", HeaderText = "Opening Qty", FillWeight = 15, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } });
        _dgvStockItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColRate", HeaderText = "Rate (₹)", FillWeight = 15, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } });
        _dgvStockItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColValue", HeaderText = "Opening Value (₹)", FillWeight = 20, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } });
        _dgvStockItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColStatus", HeaderText = "Status", FillWeight = 12, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });

        _dgvStockItems.DoubleClick += (s, e) => AlterSelectedItem();
        _dgvStockItems.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                AlterSelectedItem();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                DeleteSelectedItem();
                e.Handled = true;
            }
        };

        mainLayout.Controls.Add(_dgvStockItems, 0, 1);

        // 3. Status
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
        _btnDelete.Click += (s, e) => DeleteSelectedItem();

        _btnAlter = new Button { Text = "Edit (Enter)", Width = 110, Height = 36 };
        _btnAlter.Click += (s, e) => AlterSelectedItem();

        _btnCreate = new Button { Text = "Create Item (Alt+C)", Width = 150, Height = 36, BackColor = Color.FromArgb(24, 43, 73), ForeColor = Color.White };
        _btnCreate.Click += (s, e) => OpenCreateItemDialog();

        actionPanel.Controls.Add(_btnClose);
        actionPanel.Controls.Add(_btnDelete);
        actionPanel.Controls.Add(_btnAlter);
        actionPanel.Controls.Add(_btnCreate);
        mainLayout.Controls.Add(actionPanel, 0, 3);

        Controls.Add(mainLayout);

        KeyDown += StockItemListForm_KeyDown;
        Load += async (s, e) => await LoadStockItemsAsync();
    }

    private async Task LoadStockItemsAsync()
    {
        var company = _companyContext.CurrentCompany;
        if (company == null) return;

        try
        {
            UseWaitCursor = true;
            _allItems = await _inventoryService.GetStockItemsByCompanyAsync(company.CompanyId);
            FilterItems();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Error loading Stock Items: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void FilterItems()
    {
        _dgvStockItems.Rows.Clear();

        var query = _allItems.AsEnumerable();
        var search = _txtSearch.Text.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(i => i.ItemName.ToLowerInvariant().Contains(search) ||
                                     i.UnitName.ToLowerInvariant().Contains(search));
        }

        var list = query.OrderBy(i => i.ItemName).ToList();

        foreach (var item in list)
        {
            var idx = _dgvStockItems.Rows.Add();
            var row = _dgvStockItems.Rows[idx];
            row.Tag = item;

            row.Cells["ColItemName"].Value = item.ItemName;
            row.Cells["ColUnit"].Value = !string.IsNullOrEmpty(item.UnitName) ? item.UnitName : "—";
            row.Cells["ColQty"].Value = item.OpeningQuantity;
            row.Cells["ColRate"].Value = item.OpeningRate;
            row.Cells["ColValue"].Value = item.OpeningValue;
            row.Cells["ColStatus"].Value = item.IsActive ? "Active" : "Inactive";
        }

        decimal totalValue = list.Sum(i => i.OpeningValue);
        _lblStatus.Text = $"Total Items: {list.Count} (of {_allItems.Count}) | Total Opening Value: ₹{totalValue:N2}";
    }

    private void OpenCreateItemDialog()
    {
        using var createForm = new StockItemCreateEditForm(_inventoryService, _companyContext);
        if (createForm.ShowDialog(this) == DialogResult.OK)
        {
            _ = LoadStockItemsAsync();
        }
    }

    private void AlterSelectedItem()
    {
        if (_dgvStockItems.CurrentRow?.Tag is not StockItemDto selected) return;

        using var editForm = new StockItemCreateEditForm(_inventoryService, _companyContext, selected.StockItemId);
        if (editForm.ShowDialog(this) == DialogResult.OK)
        {
            _ = LoadStockItemsAsync();
        }
    }

    private async void DeleteSelectedItem()
    {
        if (_dgvStockItems.CurrentRow?.Tag is not StockItemDto selected) return;

        var confirm = MessageBox.Show(
            this,
            $"Are you sure you want to delete Stock Item '{selected.ItemName}'?",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        try
        {
            bool deleted = await _inventoryService.DeleteStockItemAsync(selected.StockItemId);
            if (deleted)
            {
                await LoadStockItemsAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to delete Stock Item: {ex.Message}", "Delete Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void StockItemListForm_KeyDown(object? sender, KeyEventArgs e)
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
            _ = LoadStockItemsAsync();
            e.Handled = true;
        }
        else if (e.Alt && e.KeyCode == Keys.C)
        {
            OpenCreateItemDialog();
            e.Handled = true;
        }
    }
}
