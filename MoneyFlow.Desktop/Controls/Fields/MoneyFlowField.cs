using System;
using System.Drawing;
using System.Windows.Forms;
using MoneyFlow.Desktop.Controls.Lookup;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Controls.Fields;

/// <summary>
/// Professional accounting field row for MoneyFlow.
/// Follows modern ERP accounting active/inactive visual design:
/// - Inactive: Displays clean label and crisp display text without textbox chrome.
/// - Active: Hides display label and shows active editor with clear focus ring.
/// - Value Alignment: Display value and active editor align at the exact same horizontal position.
/// - Read-Only distinction: Derived/disabled fields display an inherited indicator badge.
/// </summary>
public class MoneyFlowField : Panel
{
    private readonly Label _lblTitle;
    private readonly Label _lblColon;
    private readonly Label _lblDisplay;
    private readonly Label _lblBadge;
    private readonly Panel _editorContainer;
    private Control? _editor;
    private Func<string>? _valueGetter;
    private bool _isActive;
    private readonly int _labelWidth;
    private readonly int _contentX;

    public event EventHandler? Activated;
    public event EventHandler? Deactivated;

    public MoneyFlowField(string labelText, int labelWidth = 290, int rowHeight = 36)
    {
        _labelWidth = labelWidth;
        _contentX = labelWidth + 4;

        Size = new Size(820, rowHeight);
        MinimumSize = new Size(600, rowHeight);
        BackColor = Color.Transparent;
        Margin = new Padding(0, 1, 0, 1);

        // 1. Label Column (clean modern typography, vertical centering, no text clipping)
        _lblTitle = new Label
        {
            Text = labelText,
            Location = new Point(4, 4),
            Size = new Size(labelWidth - 18, rowHeight - 6),
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(51, 65, 85), // Slate-700 #334155
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = false
        };
        Controls.Add(_lblTitle);

        // 2. Colon separator (perfectly aligned and vertically centered)
        _lblColon = new Label
        {
            Text = ":",
            Location = new Point(labelWidth - 14, 8),
            Size = new Size(12, 20),
            Font = new Font("Segoe UI", 9.25F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139), // Slate-500 #64748B
            TextAlign = ContentAlignment.TopCenter
        };
        Controls.Add(_lblColon);

        // 3. Inactive Plain Text Display Value (Aligned at contentX + 7 for matching editor text padding)
        _lblDisplay = new Label
        {
            Location = new Point(_contentX + 7, 8),
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(15, 23, 42), // Slate-900 #0F172A
            BackColor = Color.Transparent,
            Visible = true,
            Cursor = Cursors.IBeam
        };
        _lblDisplay.Click += (s, e) => { if (Enabled) SetActive(true); };
        Controls.Add(_lblDisplay);

        // 4. Read-Only / Inherited badge for disabled fields
        _lblBadge = new Label
        {
            Location = new Point(_contentX + 180, 9),
            AutoSize = true,
            Font = new Font("Segoe UI", 8F, FontStyle.Italic),
            ForeColor = Color.FromArgb(148, 163, 184), // Slate-400
            BackColor = Color.Transparent,
            Text = "(inherited from parent)",
            Visible = false
        };
        Controls.Add(_lblBadge);

        // 5. Editor Container (hosts the active input control with focus ring)
        _editorContainer = new Panel
        {
            Location = new Point(_contentX, 3),
            Size = new Size(480, 30),
            BackColor = Color.White,
            Padding = new Padding(2),
            Visible = false
        };
        _editorContainer.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(37, 99, 235), 2); // SystemFocusBlue focus ring
            e.Graphics.DrawRectangle(pen, 0, 0, _editorContainer.Width - 1, _editorContainer.Height - 1);
        };
        Controls.Add(_editorContainer);

        // Clicking row focuses editor when enabled
        Click += (s, e) => { if (Enabled) SetActive(true); };
        _lblTitle.Click += (s, e) => { if (Enabled) SetActive(true); };
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

    public void UpdateEditorWidth(int width)
    {
        if (_editorContainer != null && width >= 50)
        {
            _editorContainer.Width = width;
        }
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        if (_lblTitle != null)
        {
            _lblTitle.ForeColor = Enabled ? Color.FromArgb(51, 65, 85) : Color.FromArgb(148, 163, 184);
        }
        if (_lblColon != null)
        {
            _lblColon.ForeColor = Enabled ? Color.FromArgb(100, 116, 139) : Color.FromArgb(148, 163, 184);
        }
        if (_lblDisplay != null)
        {
            _lblDisplay.ForeColor = Enabled ? Color.FromArgb(15, 23, 42) : Color.FromArgb(100, 116, 139);
        }
        if (_lblBadge != null)
        {
            _lblBadge.Visible = !Enabled;
            if (!Enabled && _lblDisplay != null)
            {
                _lblBadge.Location = new Point(_lblDisplay.Location.X + _lblDisplay.Width + 8, 9);
            }
        }
    }

    public void SetActive(bool active)
    {
        if (_isActive == active)
        {
            if (active && _editor != null && _editor.CanFocus)
            {
                FocusEditor();
            }
            return;
        }

        _isActive = active;

        if (_isActive)
        {
            _lblDisplay.Visible = false;
            _lblBadge.Visible = false;
            _editorContainer.Visible = true;
            _editorContainer.BringToFront();

            FocusEditor();

            Activated?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            UpdateDisplayValue();
            _editorContainer.Visible = false;
            _lblDisplay.Visible = true;
            _lblDisplay.BringToFront();

            if (!Enabled && _lblBadge != null)
            {
                _lblBadge.Visible = true;
                _lblBadge.Location = new Point(_lblDisplay.Location.X + _lblDisplay.Width + 8, 9);
            }

            Deactivated?.Invoke(this, EventArgs.Empty);
        }
    }

    private void FocusEditor()
    {
        if (_editor == null) return;
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
        else if (_editor is ComboBox cb)
        {
            cb.Focus();
            cb.SelectAll();
        }
        else
        {
            _editor.Focus();
        }
    }

    public void UpdateDisplayValue()
    {
        string val = _valueGetter != null ? _valueGetter() : string.Empty;
        _lblDisplay.Text = string.IsNullOrWhiteSpace(val) ? "-" : val;

        if (_lblBadge != null && !Enabled)
        {
            _lblBadge.Location = new Point(_lblDisplay.Location.X + _lblDisplay.Width + 8, 9);
            _lblBadge.Visible = true;
        }
        else if (_lblBadge != null && Enabled)
        {
            _lblBadge.Visible = false;
        }
    }
}
