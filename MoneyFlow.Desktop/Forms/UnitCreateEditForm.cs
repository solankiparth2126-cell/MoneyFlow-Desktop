using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Forms;

public class UnitCreateEditForm : Form
{
    private readonly IInventoryService _inventoryService;
    private readonly ICompanyContext _companyContext;
    private readonly int? _unitId;

    private TextBox _txtSymbol = null!;
    private TextBox _txtFormalName = null!;
    private NumericUpDown _numDecimalPlaces = null!;
    private CheckBox _chkIsActive = null!;
    private Button _btnSave = null!;
    private Button _btnCancel = null!;

    public UnitCreateEditForm(
        IInventoryService inventoryService,
        ICompanyContext companyContext,
        int? unitId = null)
    {
        _inventoryService = inventoryService;
        _companyContext = companyContext;
        _unitId = unitId;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = _unitId.HasValue ? "Alter Unit of Measure" : "Create Unit of Measure";
        Size = new Size(480, 320);
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
            RowCount = 5,
            Padding = new Padding(20)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));

        // 1. Symbol
        panel.Controls.Add(new Label { Text = "Symbol / Name:*", Anchor = AnchorStyles.Left, AutoSize = true, Font = ExecLedgerTheme.UIBold9 }, 0, 0);
        _txtSymbol = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "e.g. Nos, Kg, Box, Mtr" };
        panel.Controls.Add(_txtSymbol, 1, 0);

        // 2. Formal Name
        panel.Controls.Add(new Label { Text = "Formal Name:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 1);
        _txtFormalName = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "e.g. Numbers, Kilograms" };
        panel.Controls.Add(_txtFormalName, 1, 1);

        // 3. Decimal Places
        panel.Controls.Add(new Label { Text = "Decimal Places:", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 2);
        _numDecimalPlaces = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 4,
            Value = 0,
            Width = 80,
            Anchor = AnchorStyles.Left
        };
        panel.Controls.Add(_numDecimalPlaces, 1, 2);

        // 4. IsActive (only for edit)
        _chkIsActive = new CheckBox { Text = "Is Active", Checked = true, Anchor = AnchorStyles.Left };
        if (_unitId.HasValue)
        {
            panel.Controls.Add(_chkIsActive, 1, 3);
        }

        // 5. Buttons
        var btnPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };

        _btnCancel = new Button { Text = "Cancel (Esc)", Width = 100, Height = 36 };
        _btnCancel.Click += (s, e) => DialogResult = DialogResult.Cancel;

        _btnSave = new Button { Text = "Save (Ctrl+A)", Width = 120, Height = 36, BackColor = Color.FromArgb(24, 43, 73), ForeColor = Color.White };
        _btnSave.Click += async (s, e) => await SaveUnitAsync();

        btnPanel.Controls.Add(_btnCancel);
        btnPanel.Controls.Add(_btnSave);
        panel.Controls.Add(btnPanel, 1, 4);

        Controls.Add(panel);

        KeyDown += UnitCreateEditForm_KeyDown;
        Load += async (s, e) => await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        if (!_unitId.HasValue) return;

        try
        {
            UseWaitCursor = true;
            var unit = await _inventoryService.GetUnitByIdAsync(_unitId.Value);
            if (unit != null)
            {
                _txtSymbol.Text = unit.UnitName;
                _txtFormalName.Text = unit.FormalName;
                _numDecimalPlaces.Value = Math.Clamp(unit.DecimalPlaces, 0, 4);
                _chkIsActive.Checked = unit.IsActive;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to load Unit: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task SaveUnitAsync()
    {
        var company = _companyContext.CurrentCompany;
        if (company == null)
        {
            MessageBox.Show(this, "No company context found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(_txtSymbol.Text))
        {
            MessageBox.Show(this, "Please enter a Unit Symbol (e.g. Nos, Kg, Box).", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtSymbol.Focus();
            return;
        }

        try
        {
            UseWaitCursor = true;

            if (_unitId.HasValue)
            {
                await _inventoryService.UpdateUnitAsync(_unitId.Value, new UnitUpdateDto
                {
                    UnitName = _txtSymbol.Text.Trim(),
                    FormalName = _txtFormalName.Text.Trim(),
                    DecimalPlaces = (int)_numDecimalPlaces.Value,
                    IsActive = _chkIsActive.Checked
                });
            }
            else
            {
                await _inventoryService.CreateUnitAsync(company.CompanyId, new UnitCreateDto
                {
                    UnitName = _txtSymbol.Text.Trim(),
                    FormalName = _txtFormalName.Text.Trim(),
                    DecimalPlaces = (int)_numDecimalPlaces.Value
                });
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Error Saving Unit", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void UnitCreateEditForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            DialogResult = DialogResult.Cancel;
            Close();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.A)
        {
            _ = SaveUnitAsync();
            e.Handled = true;
        }
    }
}
