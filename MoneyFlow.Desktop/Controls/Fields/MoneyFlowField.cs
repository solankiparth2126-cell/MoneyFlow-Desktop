using System;
using System.Drawing;
using System.Windows.Forms;
using MoneyFlow.Desktop.Controls.Lookup;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Controls.Fields;

/// <summary>
/// Professional accounting field row for MoneyFlow.
/// Follows the strict Tally-style active/inactive visual requirement:
/// - Inactive: Displays only plain label and text value without any textbox chrome/borders.
/// - Active: Hides the plain text label and displays the real white textbox editor with blue focus border.
/// - Value Alignment: The display value and active editor text align at the exact same horizontal position.
/// </summary>
public class MoneyFlowField : Panel
{
    private readonly Label _lblTitle;
    private readonly Label _lblColon;
    private readonly Label _lblDisplay;
    private readonly Panel _editorContainer;
    private Control? _editor;
    private Func<string>? _valueGetter;
    private bool _isActive;

    public event EventHandler? Activated;
    public event EventHandler? Deactivated;

    public MoneyFlowField(string labelText, int labelWidth = 280, int rowHeight = 38)
    {
        Size = new Size(820, rowHeight);
        MinimumSize = new Size(600, rowHeight);
        BackColor = Color.Transparent;
        Margin = new Padding(0, 2, 0, 2);

        // 1. Label Column (wraps long labels cleanly without overlapping)
        _lblTitle = new Label
        {
            Text = labelText,
            Location = new Point(4, 8),
            Size = new Size(labelWidth - 20, rowHeight - 8),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(15, 23, 42),
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = false
        };
        Controls.Add(_lblTitle);

        // 2. Colon separator
        _lblColon = new Label
        {
            Text = ":",
            Location = new Point(labelWidth - 14, 8),
            Size = new Size(12, 20),
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            TextAlign = ContentAlignment.TopCenter
        };
        Controls.Add(_lblColon);

        int contentX = labelWidth + 4;

        // 3. Inactive Plain Text Display Value (Aligned at contentX + 6 for matching editor text padding)
        _lblDisplay = new Label
        {
            Location = new Point(contentX + 7, 8),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.25F, FontStyle.Regular),
            ForeColor = Color.FromArgb(15, 23, 42),
            BackColor = Color.Transparent,
            Visible = true,
            Cursor = Cursors.IBeam
        };
        _lblDisplay.Click += (s, e) => SetActive(true);
        Controls.Add(_lblDisplay);

        // 4. Editor Container (hosts the active input control)
        _editorContainer = new Panel
        {
            Location = new Point(contentX, 3),
            Size = new Size(480, 32),
            BackColor = Color.Transparent,
            Visible = false
        };
        Controls.Add(_editorContainer);

        // Clicking row focuses editor
        Click += (s, e) => SetActive(true);
        _lblTitle.Click += (s, e) => SetActive(true);
    }

    public string LabelText
    {
        get => _lblTitle.Text;
        set => _lblTitle.Text = value;
    }

    public bool IsActive => _isActive;

    public Control? Editor => _editor;

    public void SetEditor(Control editor, Func<string> valueGetter)
    {
        _editor = editor ?? throw new ArgumentNullException(nameof(editor));
        _valueGetter = valueGetter ?? throw new ArgumentNullException(nameof(valueGetter));

        _editor.Dock = DockStyle.Fill;
        _editorContainer.Controls.Clear();
        _editorContainer.Controls.Add(_editor);

        // Track focus to activate/deactivate
        if (_editor is MoneyFlowTextLookup lookup)
        {
            foreach (Control inner in lookup.Controls)
            {
                inner.GotFocus += (s, e) => SetActive(true);
            }
        }
        else
        {
            _editor.GotFocus += (s, e) => SetActive(true);
        }

        UpdateDisplayValue();
    }

    public void SetActive(bool active)
    {
        if (_isActive == active) return;

        _isActive = active;

        if (_isActive)
        {
            _lblDisplay.Visible = false;
            _editorContainer.Visible = true;
            _editorContainer.BringToFront();

            if (_editor != null)
            {
                if (_editor is MoneyFlowTextLookup lookup)
                {
                    lookup.Focus();
                    lookup.SelectAll();
                }
                else if (_editor is TextBoxBase tb)
                {
                    tb.Focus();
                    tb.SelectAll();
                }
                else
                {
                    _editor.Focus();
                }
            }

            Activated?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            UpdateDisplayValue();
            _editorContainer.Visible = false;
            _lblDisplay.Visible = true;
            _lblDisplay.BringToFront();

            Deactivated?.Invoke(this, EventArgs.Empty);
        }
    }

    public void UpdateDisplayValue()
    {
        string val = _valueGetter != null ? _valueGetter() : string.Empty;
        _lblDisplay.Text = string.IsNullOrWhiteSpace(val) ? "-" : val;
    }
}
