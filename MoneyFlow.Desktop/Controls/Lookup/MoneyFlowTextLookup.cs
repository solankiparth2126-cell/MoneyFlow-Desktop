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
                if (_isFocused && (_popup == null || !_popup.Visible))
                {
                    await OpenLookupAsync();
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
            if (_isFocused && (_popup == null || !_popup.Visible))
            {
                // Typing text into field opens lookup with the typed query
                await OpenLookupAsync(_innerTextBox.Text);
            }
        };

        Controls.Add(_innerTextBox);

        Click += (s, e) => _innerTextBox.Focus();
        Paint += OnCustomPaint;
    }

    public bool AutoOpenOnFocus { get; set; } = true;

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
        var g = e.Graphics;
        var rect = new Rectangle(0, 0, Width - 1, Height - 1);

        // Background: strictly white (#FFFFFF)
        using (var brush = new SolidBrush(Color.White))
        {
            g.FillRectangle(brush, rect);
        }

        // Border: #2563EB when focused, #CBD5E1 when idle
        Color borderColor = _isFocused ? ExecLedgerTheme.SystemFocusBlue : ExecLedgerTheme.PrimaryBorder;
        int borderWidth = _isFocused ? 2 : 1;

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

    private async Task HandleInnerKeyDownAsync(KeyEventArgs e)
    {
        // 1. Enter Key Handling: jump to next control immediately
        if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;

            if (_popup != null && _popup.Visible)
            {
                return;
            }

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

                // Immediately advance focus to the next control in the form
                BeginInvoke(new Action(() =>
                {
                    var parent = FindForm();
                    parent?.SelectNextControl(this, true, true, true, true);
                }));
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
                _innerTextBox.Focus();
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
