using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MoneyFlow.Desktop.Controls.Dropdown.Events;
using MoneyFlow.Desktop.Controls.Dropdown.Models;

namespace MoneyFlow.Desktop.Controls.Dropdown;

/// <summary>
/// Project-wide standard Tally-style dropdown component for MoneyFlow Desktop ERP.
/// Replaces standard ComboBox / Guna2ComboBox across all master lookup fields.
/// </summary>
[DefaultEvent(nameof(SelectedItemChanged))]
public class MoneyFlowTallyDropdown : UserControl
{
    private DropdownConfiguration _config = new();
    private List<DropdownItemModel> _items = new();
    private DropdownItemModel? _selectedItem;
    private MoneyFlowTallyDropdownPopup? _activePopup;
    private bool _isFocused = false;

    // UI elements
    private readonly Label lblText;
    private readonly Label lblArrow;

    public event EventHandler? SelectedItemChanged;
    public event EventHandler<DropdownCreateEventArgs>? CreateRequested;
    public event EventHandler<DropdownMoreEventArgs>? ShowMoreRequested;

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DropdownConfiguration Configuration
    {
        get => _config;
        set => _config = value ?? new DropdownConfiguration();
    }

    [Category("MoneyFlow ERP")]
    public MasterType MasterType
    {
        get => _config.MasterType;
        set => _config.MasterType = value;
    }

    [Category("MoneyFlow ERP")]
    public string HeaderText
    {
        get => _config.HeaderText;
        set => _config.HeaderText = value;
    }

    [Category("MoneyFlow ERP")]
    public bool AllowCreate
    {
        get => _config.AllowCreate;
        set => _config.AllowCreate = value;
    }

    [Category("MoneyFlow ERP")]
    public string CreateText
    {
        get => _config.CreateText;
        set => _config.CreateText = value;
    }

    [Category("MoneyFlow ERP")]
    public bool AllowShowMore
    {
        get => _config.AllowShowMore;
        set => _config.AllowShowMore = value;
    }

    [Category("MoneyFlow ERP")]
    public string MoreText
    {
        get => _config.MoreText;
        set => _config.MoreText = value;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public DropdownItemModel? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (_selectedItem != value)
            {
                _selectedItem = value;
                UpdateDisplayText();
                SelectedItemChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public object? SelectedValue
    {
        get => _selectedItem?.Id;
        set
        {
            if (value == null)
            {
                SelectedItem = null;
                return;
            }

            string valStr = value.ToString() ?? "";
            var found = _items.FirstOrDefault(i =>
                (i.Id != null && string.Equals(i.Id.ToString(), valStr, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals(i.Name, valStr, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(i.Code, valStr, StringComparison.OrdinalIgnoreCase));

            SelectedItem = found;
        }
    }

    [Category("MoneyFlow ERP")]
    public string SelectedText
    {
        get => _selectedItem?.Name ?? string.Empty;
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                SelectedItem = null;
                return;
            }

            var found = _items.FirstOrDefault(i => string.Equals(i.Name, value, StringComparison.OrdinalIgnoreCase));
            if (found != null)
            {
                SelectedItem = found;
            }
            else
            {
                lblText.Text = value;
            }
        }
    }

    public MoneyFlowTallyDropdown()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.Selectable,
            true);

        Size = new Size(240, 26);
        BackColor = MoneyFlowERPTheme.FieldNormalBackColor;
        Cursor = Cursors.Hand;
        TabStop = true;

        lblText = new Label
        {
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(6, 4),
            AutoSize = false,
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true
        };
        lblText.Click += (s, e) => { Focus(); ShowPopup(); };
        Controls.Add(lblText);

        lblArrow = new Label
        {
            Text = "▾",
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Size = new Size(18, 20),
            BackColor = Color.Transparent,
            TextAlign = ContentAlignment.MiddleCenter,
            Cursor = Cursors.Hand
        };
        lblArrow.Click += (s, e) => { Focus(); ShowPopup(); };
        Controls.Add(lblArrow);

        UpdateLayout();
        UpdateDisplayText();
    }

    public void SetItems(IEnumerable<DropdownItemModel> items, object? preselectedValue = null)
    {
        _items = items?.ToList() ?? new List<DropdownItemModel>();
        if (preselectedValue != null)
        {
            SelectedValue = preselectedValue;
        }
        else if (_selectedItem != null)
        {
            SelectedValue = _selectedItem.Id ?? _selectedItem.Name;
        }
        else
        {
            UpdateDisplayText();
        }
    }

    public void SetItems<T>(IEnumerable<T> source, Func<T, object> idSelector, Func<T, string> nameSelector, Func<T, string>? codeSelector = null, object? preselectedValue = null)
    {
        if (source == null)
        {
            SetItems(Enumerable.Empty<DropdownItemModel>(), null);
            return;
        }

        var list = source.Select(item => new DropdownItemModel
        {
            Id = idSelector(item),
            Name = nameSelector(item),
            Code = codeSelector?.Invoke(item) ?? string.Empty,
            Tag = item
        }).ToList();

        SetItems(list, preselectedValue);
    }

    public void ShowPopup()
    {
        if (_activePopup != null && !_activePopup.IsDisposed && _activePopup.Visible)
        {
            _activePopup.Close();
            return;
        }

        _activePopup = new MoneyFlowTallyDropdownPopup(this, _config);
        _activePopup.LoadItems(_items, SelectedValue);

        _activePopup.ItemSelected += (s, e) =>
        {
            SelectedItem = e.Item;
            Focus();
        };

        _activePopup.CreateRequested += (s, e) =>
        {
            CreateRequested?.Invoke(this, e);
            if (e.CreatedItem != null)
            {
                _items.Add(e.CreatedItem);
                if (_config.AutoSelectCreatedItem)
                {
                    SelectedItem = e.CreatedItem;
                }
            }
        };

        _activePopup.ShowMoreRequested += (s, e) =>
        {
            ShowMoreRequested?.Invoke(this, e);
        };

        _activePopup.FormClosed += (s, e) =>
        {
            _activePopup = null;
            Invalidate();
        };

        _activePopup.ShowBesideAnchor();
    }

    public void ClosePopup()
    {
        if (_activePopup != null && !_activePopup.IsDisposed)
        {
            _activePopup.Close();
            _activePopup = null;
        }
    }

    private void UpdateDisplayText()
    {
        if (lblText == null) return;
        if (_selectedItem != null)
        {
            lblText.Text = _selectedItem.Name;
            lblText.ForeColor = Color.FromArgb(15, 23, 42);
        }
        else
        {
            lblText.Text = _config.EmptyText;
            lblText.ForeColor = Color.FromArgb(148, 163, 184);
        }
    }

    private void UpdateLayout()
    {
        if (lblArrow == null || lblText == null) return;
        lblArrow.Location = new Point(Width - lblArrow.Width - 4, (Height - lblArrow.Height) / 2);
        lblText.Size = new Size(Math.Max(10, lblArrow.Left - 8), Height - 6);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateLayout();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        _isFocused = true;
        BackColor = MoneyFlowERPTheme.FieldFocusBackColor;
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _isFocused = false;
        BackColor = MoneyFlowERPTheme.FieldNormalBackColor;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Draw Border
        Color borderColor = _isFocused ? MoneyFlowERPTheme.FieldFocusBorderColor : MoneyFlowERPTheme.FieldNormalBorderColor;
        int borderThickness = _isFocused ? 2 : 1;

        using var pen = new Pen(borderColor, borderThickness);
        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Enter || keyData == Keys.Down || keyData == (Keys.Alt | Keys.Down) || keyData == Keys.Space)
        {
            ShowPopup();
            return true;
        }

        if (keyData == (Keys.Alt | Keys.C))
        {
            var args = new DropdownCreateEventArgs(_config.MasterType);
            CreateRequested?.Invoke(this, args);
            if (args.CreatedItem != null)
            {
                _items.Add(args.CreatedItem);
                SelectedItem = args.CreatedItem;
            }
            return true;
        }

        if (keyData == (Keys.Alt | Keys.M))
        {
            var args = new DropdownMoreEventArgs(_config.MasterType);
            ShowMoreRequested?.Invoke(this, args);
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClosePopup();
        }
        base.Dispose(disposing);
    }
}
