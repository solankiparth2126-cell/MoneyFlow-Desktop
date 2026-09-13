using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;

using Guna.UI2.WinForms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

public class StockItemCreateEditForm : Form
{
    private readonly IInventoryService _inventoryService;
    private readonly ICompanyContext _companyContext;
    private readonly int? _stockItemId;

    private TextBox _txtItemName = null!;
    private ComboBox _cmbUnit = null!;
    private NumericUpDown _numOpeningQty = null!;
    private NumericUpDown _numOpeningRate = null!;
    private TextBox _txtOpeningValue = null!;
    private CheckBox _chkIsActive = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;

    public StockItemCreateEditForm(
        IInventoryService inventoryService,
        ICompanyContext companyContext,
        int? stockItemId = null)
    {
        _inventoryService = inventoryService;
        _companyContext = companyContext;
        _stockItemId = stockItemId;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = _stockItemId.HasValue ? "Alter Stock Item" : "Create Stock Item";
        Size = new Size(520, 390);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = ExecLedgerTheme.UIRegular9;
        KeyPreview = true;

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(20)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));

        // 1. Item Name
        panel.Controls.Add(new Label { Text = "Item Name:*", Anchor = AnchorStyles.Left, AutoSize = true, Font = ExecLedgerTheme.UIBold9 }, 0, 0);
        _txtItemName = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "e.g. Premium Cotton Shirt" };
        panel.Controls.Add(_txtItemName, 1, 0);

        // 2. Unit of Measure
        panel.Controls.Add(new Label { Text = "Unit of Measure:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        _cmbUnit = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        panel.Controls.Add(_cmbUnit, 1, 1);

        // 3. Opening Quantity
        panel.Controls.Add(new Label { Text = "Opening Quantity:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
        _numOpeningQty = new NumericUpDown
        {
            DecimalPlaces = 2,
            Minimum = 0,
            Maximum = 10000000,
            Value = 0,
            Width = 140,
            Anchor = AnchorStyles.Left
        };
        _numOpeningQty.ValueChanged += (s, e) => RecalculateOpeningValue();
        panel.Controls.Add(_numOpeningQty, 1, 2);

        // 4. Opening Rate
        panel.Controls.Add(new Label { Text = "Rate (₹ per Unit):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 3);
        _numOpeningRate = new NumericUpDown
        {
            DecimalPlaces = 2,
            Minimum = 0,
            Maximum = 10000000,
            Value = 0,
            Width = 140,
            Anchor = AnchorStyles.Left
        };
        _numOpeningRate.ValueChanged += (s, e) => RecalculateOpeningValue();
        panel.Controls.Add(_numOpeningRate, 1, 3);

        // 5. Opening Value (Calculated)
        panel.Controls.Add(new Label { Text = "Opening Value (₹):", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 4);
        _txtOpeningValue = new TextBox
        {
            ReadOnly = true,
            BackColor = ExecLedgerTheme.SecondarySurface,
            Font = ExecLedgerTheme.UIBold9,
            ForeColor = Color.FromArgb(24, 43, 73),
            Width = 160,
            Anchor = AnchorStyles.Left,
            Text = "₹0.00"
        };
        panel.Controls.Add(_txtOpeningValue, 1, 4);

        // 6. IsActive
        _chkIsActive = new CheckBox { Text = "Is Active", Checked = true, Anchor = AnchorStyles.Left };
        if (_stockItemId.HasValue)
        {
            panel.Controls.Add(_chkIsActive, 1, 5);
        }

        // 7. Buttons
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };

        _btnCancel = new Button { Text = "Cancel (Esc)", Width = 100, Height = 36 };
        _btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

        _btnSave = new Button { Text = "Save (Ctrl+A)", Width = 120, Height = 36, BackColor = Color.FromArgb(24, 43, 73), ForeColor = Color.White };
        _btnSave.Click += async (s, e) => await SaveStockItemAsync();

        btnPanel.Controls.Add(_btnCancel);
        btnPanel.Controls.Add(_btnSave);
        panel.Controls.Add(btnPanel, 1, 6);

        Controls.Add(panel);

        KeyDown += StockItemCreateEditForm_KeyDown;
        Load += async (s, e) => await LoadDataAsync();
    }

    private void RecalculateOpeningValue()
    {
        decimal val = Math.Round(_numOpeningQty.Value * _numOpeningRate.Value, 2);
        _txtOpeningValue.Text = $"₹{val:N2}";
    }

    private async Task LoadDataAsync()
    {
        var company = _companyContext.CurrentCompany;
        if (company == null) return;

        try
        {
            UseWaitCursor = true;

            // Load Units
            var units = await _inventoryService.GetUnitsByCompanyAsync(company.CompanyId);
            var comboItems = new System.Collections.Generic.List<UnitComboItem>
            {
                new UnitComboItem { UnitId = null, DisplayName = "— None / Not Applicable —" }
            };

            foreach (var u in units.Where(u => u.IsActive).OrderBy(u => u.UnitName))
            {
                string display = !string.IsNullOrEmpty(u.FormalName) ? $"{u.UnitName} ({u.FormalName})" : u.UnitName;
                comboItems.Add(new UnitComboItem { UnitId = u.UnitId, DisplayName = display });
            }

            _cmbUnit.DisplayMember = "DisplayName";
            _cmbUnit.ValueMember = "UnitId";
            _cmbUnit.DataSource = comboItems;

            if (_stockItemId.HasValue)
            {
                var item = await _inventoryService.GetStockItemByIdAsync(_stockItemId.Value);
                if (item != null)
                {
                    _txtItemName.Text = item.ItemName;
                    _numOpeningQty.Value = Math.Clamp(item.OpeningQuantity, _numOpeningQty.Minimum, _numOpeningQty.Maximum);
                    _numOpeningRate.Value = Math.Clamp(item.OpeningRate, _numOpeningRate.Minimum, _numOpeningRate.Maximum);
                    _chkIsActive.Checked = item.IsActive;

                    if (item.UnitId.HasValue)
                    {
                        var match = comboItems.FirstOrDefault(c => c.UnitId == item.UnitId.Value);
                        if (match != null)
                        {
                            _cmbUnit.SelectedItem = match;
                        }
                    }

                    RecalculateOpeningValue();
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to load data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task SaveStockItemAsync()
    {
        var company = _companyContext.CurrentCompany;
        if (company == null)
        {
            MessageBox.Show(this, "No company context found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(_txtItemName.Text))
        {
            MessageBox.Show(this, "Please enter an Item Name.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtItemName.Focus();
            return;
        }

        int? selectedUnitId = (_cmbUnit.SelectedItem as UnitComboItem)?.UnitId;
        decimal openingVal = Math.Round(_numOpeningQty.Value * _numOpeningRate.Value, 2);

        try
        {
            UseWaitCursor = true;

            if (_stockItemId.HasValue)
            {
                await _inventoryService.UpdateStockItemAsync(_stockItemId.Value, new StockItemUpdateDto
                {
                    ItemName = _txtItemName.Text.Trim(),
                    UnitId = selectedUnitId,
                    OpeningQuantity = _numOpeningQty.Value,
                    OpeningRate = _numOpeningRate.Value,
                    OpeningValue = openingVal,
                    IsActive = _chkIsActive.Checked
                });
            }
            else
            {
                await _inventoryService.CreateStockItemAsync(company.CompanyId, new StockItemCreateDto
                {
                    ItemName = _txtItemName.Text.Trim(),
                    UnitId = selectedUnitId,
                    OpeningQuantity = _numOpeningQty.Value,
                    OpeningRate = _numOpeningRate.Value,
                    OpeningValue = openingVal
                });
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error Saving Stock Item", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void StockItemCreateEditForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.A)
        {
            _ = SaveStockItemAsync();
            e.Handled = true;
        }
    }

    private class UnitComboItem
    {
        public int? UnitId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}
