using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Controls.Lookup;

/// <summary>
/// Universal keyboard-first lookup field for MoneyFlow.
/// Visually renders as a clean white textbox without standard dropdown arrows.
/// Strictly follows MoneyFlow theme (pure white background, #CBD5E1 border, #2563EB focus border).
/// </summary>
public class MoneyFlowTextLookup : UserControl
{
    private readonly TextBox _innerTextBox;
    private ILookupProvider? _provider;
    private LookupConfig _config = new();
    private MoneyFlowLookupPopup? _popup;

    private LookupItem? _selectedItem;
    private object? _selectedValue;
    private bool _isFocused;
    private bool _suppressTextChange;
    private bool _suppressAutoOpenUntilLeave;
    private DateTime _lastClosedByEscUtc = DateTime.MinValue;

    public bool WasJustClosedByEsc => (DateTime.UtcNow - _lastClosedByEscUtc).TotalMilliseconds < 500;

    public event EventHandler? SelectedValueChanged;
    public event Action? CreateRequested;
    public event Action? ShowMoreRequested;

    public MoneyFlowTextLookup()
    {
        SetStyle(ControlStyles.DoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);

        BackColor = Color.White;
        Padding = new Padding(6, 4, 6, 4);
        Size = new Size(260, 28);

        _innerTextBox = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            ForeColor = ExecLedgerTheme.PrimaryText,
            Font = new Font("Segoe UI", 9.25F, FontStyle.Regular),
            Dock = DockStyle.Fill
        };

        _innerTextBox.GotFocus += async (s, e) =>
        {
            _isFocused = true;
            Invalidate();

            if (AutoOpenOnFocus && !_suppressAutoOpenUntilLeave && (_popup == null || !_popup.Visible))
            {
                // Delay slightly to allow normal focus routing to complete
                await Task.Yield();
                var parent = FindForm();
                if (!IsDisposed && _isFocused && parent != null && !parent.IsDisposed && parent.Visible && (_popup == null || !_popup.Visible))
                {
                    try
                    {
                        await OpenLookupAsync();
                    }
                    catch { }
                }
            }
        };

        _innerTextBox.LostFocus += (s, e) =>
        {
            _isFocused = false;
            _suppressAutoOpenUntilLeave = false;
            Invalidate();
        };

        _innerTextBox.KeyDown += async (s, e) => await HandleInnerKeyDownAsync(e);
        _innerTextBox.Click += async (s, e) =>
        {
            if (_popup == null || !_popup.Visible)
            {
                await OpenLookupAsync();
            }
        };

        _innerTextBox.TextChanged += async (s, e) =>
        {
            if (_suppressTextChange) return;
            if (_isFocused)
            {
                if (_popup == null || !_popup.Visible)
                {
                    // Typing text into field opens lookup with the typed query
                    await OpenLookupAsync(_innerTextBox.Text);
                }
                else
                {
                    await _popup.ReloadDataAsync(_innerTextBox.Text);
                }
            }
        };

        Controls.Add(_innerTextBox);

        Click += async (s, e) =>
        {
            _innerTextBox.Focus();
            if (_popup == null || !_popup.Visible)
            {
                await OpenLookupAsync();
            }
        };
        Paint += OnCustomPaint;
        Resize += (s, e) => UpdatePadding();
        UpdatePadding();
    }

    public bool AutoOpenOnFocus { get; set; } = true;
    public bool DrawBorder { get; set; } = true;

    private bool _showDropdownArrow;
    public bool ShowDropdownArrow
    {
        get => _showDropdownArrow;
        set
        {
            _showDropdownArrow = value;
            UpdatePadding();
            Invalidate();
        }
    }

    public Color BorderColor { get; set; } = ExecLedgerTheme.PrimaryBorder;
    public Color FocusBorderColor { get; set; } = ExecLedgerTheme.SystemFocusBlue;
    public int BorderRadius { get; set; }

    private void UpdatePadding()
    {
        int topPad = Math.Max(2, (Height - _innerTextBox.PreferredHeight) / 2);
        Padding = new Padding(8, topPad, _showDropdownArrow ? 24 : 8, 2);
    }

    public void SetProvider(ILookupProvider provider, LookupConfig? config = null)
    {
        _provider = provider;
        if (config != null)
        {
            _config = config;
        }

        if (_popup != null)
        {
            _popup.Dispose();
            _popup = null;
        }
    }

    public LookupConfig Config => _config;

    public bool IsPopupOpen => _popup != null && _popup.Visible;

    public void ClosePopup()
    {
        _lastClosedByEscUtc = DateTime.UtcNow;
        if (_popup != null && _popup.Visible)
        {
            _suppressAutoOpenUntilLeave = true;
            _popup.ClosePopup(isCancelled: true);
        }
    }

    public LookupItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            _selectedValue = value?.Id;
            _suppressTextChange = true;
            _innerTextBox.Text = value?.Name ?? string.Empty;
            _suppressTextChange = false;
            SelectedValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public object? SelectedValue
    {
        get => _selectedValue;
        set
        {
            _selectedValue = value;
            if (_provider != null && value != null)
            {
                var match = _provider.FindById(value);
                if (match != null)
                {
                    _selectedItem = match;
                    _suppressTextChange = true;
                    _innerTextBox.Text = match.Name;
                    _suppressTextChange = false;
                    SelectedValueChanged?.Invoke(this, EventArgs.Empty);
                    return;
                }
            }

            if (value == null)
            {
                _selectedItem = null;
                _suppressTextChange = true;
                _innerTextBox.Text = string.Empty;
                _suppressTextChange = false;
                SelectedValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    [System.Diagnostics.CodeAnalysis.AllowNull]
    public override string Text
    {
        get => _innerTextBox.Text;
        set
        {
            _suppressTextChange = true;
            _innerTextBox.Text = value ?? string.Empty;
            _suppressTextChange = false;
        }
    }

    public string PlaceholderText
    {
        get => _innerTextBox.PlaceholderText;
        set => _innerTextBox.PlaceholderText = value;
    }

    public bool ReadOnlyInput
    {
        get => _innerTextBox.ReadOnly;
        set => _innerTextBox.ReadOnly = value;
    }

    public void SelectAll()
    {
        _innerTextBox.SelectAll();
    }

    public void Clear()
    {
        SelectedValue = null;
        Text = string.Empty;
    }

    public new bool Focus()
    {
        return _innerTextBox.Focus();
    }

    private void OnCustomPaint(object? sender, PaintEventArgs e)
    {
        if (!DrawBorder) return;

        var g = e.Graphics;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        Color borderColor = _isFocused ? FocusBorderColor : BorderColor;
        int borderWidth = _isFocused ? 2 : 1;

        if (BorderRadius > 0)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = CreateRoundedRectanglePath(rect, BorderRadius);
            using var brush = new SolidBrush(Color.White);
            g.FillPath(brush, path);
            using var pen = new Pen(borderColor, borderWidth);
            g.DrawPath(pen, path);
        }
        else
        {
            using (var brush = new SolidBrush(Color.White))
            {
                g.FillRectangle(brush, rect);
            }

            using (var pen = new Pen(borderColor, borderWidth))
            {
                if (_isFocused)
                {
                    g.DrawRectangle(pen, 1, 1, Width - 2, Height - 2);
                }
                else
                {
                    g.DrawRectangle(pen, rect);
                }
            }
        }

        if (ShowDropdownArrow)
        {
            int arrowX = Width - 18;
            int arrowY = (Height - 4) / 2;
            Point[] arrowPoints =
            {
                new(arrowX, arrowY),
                new(arrowX + 8, arrowY),
                new(arrowX + 4, arrowY + 5)
            };
            using var arrowBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
            g.FillPolygon(arrowBrush, arrowPoints);
        }
    }

    private static System.Drawing.Drawing2D.GraphicsPath CreateRoundedRectanglePath(Rectangle rect, int radius)
    {
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public event KeyEventHandler? EditorKeyDown;
    public event EventHandler? SelectionCommitted;

    public TextBox InnerTextBox => _innerTextBox;

    private async Task HandleInnerKeyDownAsync(KeyEventArgs e)
    {
        // 0. Intercept Escape immediately when popup is open so parent forms NEVER see this key
        if (e.KeyCode == Keys.Escape)
        {
            if (_popup != null && _popup.Visible)
            {
                _suppressAutoOpenUntilLeave = true;
                _lastClosedByEscUtc = DateTime.UtcNow;
                _popup.ClosePopup(isCancelled: true);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (WasJustClosedByEsc || MoneyFlowLookupPopup.WasAnyPopupJustClosedByEsc)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
        }

        // Navigation when popup is visible - prioritize popup list navigation over field navigation
        if (_popup != null && _popup.Visible)
        {
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down || e.KeyCode == Keys.PageUp || e.KeyCode == Keys.PageDown)
            {
                _popup.HandleExternalKeyDown(e);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Tab)
            {
                _popup.SelectCurrentItem();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
        }

        // Forward inner key to external listeners and UserControl.OnKeyDown
        EditorKeyDown?.Invoke(this, e);
        if (e.Handled || e.SuppressKeyPress) return;

        OnKeyDown(e);
        if (e.Handled || e.SuppressKeyPress) return;

        // 1. Enter Key Handling (when popup is closed)
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            SelectionCommitted?.Invoke(this, EventArgs.Empty);

            var parent = FindForm();
            parent?.SelectNextControl(this, true, true, true, true);
            return;
        }

        // 2. F4 or Alt+Down opens lookup
        if (e.KeyCode == Keys.F4 || (e.Alt && e.KeyCode == Keys.Down))
        {
            await OpenLookupAsync(_innerTextBox.Text);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        // 3. Alt+C triggers creation
        if (e.Alt && e.KeyCode == Keys.C)
        {
            CreateRequested?.Invoke();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        // 4. Escape closes popup if open
        if (e.KeyCode == Keys.Escape)
        {
            if (_popup != null && _popup.Visible)
            {
                _suppressAutoOpenUntilLeave = true;
                _popup.ClosePopup(isCancelled: true);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            // If popup is closed, do NOT suppress Escape so parent form can handle Back/Exit
        }
    }

    public async Task OpenLookupAsync(string? initialSearch = null)
    {
        if (_provider == null) return;

        EnsurePopupCreated();
        if (_popup == null) return;

        await _popup.LoadAndShowAsync(this, initialSearch);
    }

    public void OpenPopup(string? initialSearch = null)
    {
        OpenLookupAsync(initialSearch).GetAwaiter().GetResult();
    }

    public void CloseLookup()
    {
        _suppressAutoOpenUntilLeave = true;
        _popup?.ClosePopup(isCancelled: true);
    }

    private void EnsurePopupCreated()
    {
        if (_popup == null && _provider != null)
        {
            _popup = new MoneyFlowLookupPopup(_provider, _config);

            _popup.ItemSelected += item =>
            {
                _selectedItem = item;
                _selectedValue = item.Id;

                _suppressTextChange = true;
                _innerTextBox.Text = item.IsSentinel ? string.Empty : item.Name;
                _suppressTextChange = false;

                _suppressAutoOpenUntilLeave = true;

                SelectedValueChanged?.Invoke(this, EventArgs.Empty);
                SelectionCommitted?.Invoke(this, EventArgs.Empty);

                // If no external commit listener is registered, fallback to form's SelectNextControl
                if (SelectionCommitted == null)
                {
                    BeginInvoke(new Action(() =>
                    {
                        var parent = FindForm();
                        parent?.SelectNextControl(this, true, true, true, true);
                    }));
                }
            };

            _popup.CreateRequested += () =>
            {
                _suppressAutoOpenUntilLeave = true;
                _popup.ClosePopup(isCancelled: true);
                CreateRequested?.Invoke();
            };

            _popup.ShowMoreRequested += () =>
            {
                _suppressAutoOpenUntilLeave = true;
                _popup.ClosePopup(isCancelled: true);
                ShowMoreRequested?.Invoke();
            };

            _popup.Cancelled += () =>
            {
                _suppressAutoOpenUntilLeave = true;
                _lastClosedByEscUtc = DateTime.UtcNow;
                BeginInvoke(new Action(() =>
                {
                    if (!IsDisposed && _innerTextBox.CanFocus)
                    {
                        _innerTextBox.Focus();
                    }
                }));
            };
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_popup != null)
            {
                _popup.Dispose();
                _popup = null;
            }
        }
        base.Dispose(disposing);
    }
}
