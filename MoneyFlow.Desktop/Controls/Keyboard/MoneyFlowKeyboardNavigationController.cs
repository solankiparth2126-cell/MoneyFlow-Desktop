using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MoneyFlow.Desktop.Controls.Lookup;

namespace MoneyFlow.Desktop.Controls.Keyboard;

/// <summary>
/// Reusable keyboard-first navigation controller for MoneyFlow forms.
/// Provides Tally-style ENTER navigation across form controls without manual per-field wiring.
/// Supports ENTER to advance, SHIFT+ENTER / SHIFT+TAB to go back, automatic lookup handling,
/// and executing a submit/save action on the final actionable field.
/// </summary>
public class MoneyFlowKeyboardNavigationController
{
    private readonly Control _container;
    private readonly Action? _onSubmit;
    private readonly Func<Control, bool>? _customValidator;
    private readonly HashSet<Control> _excludedControls = new();

    public MoneyFlowKeyboardNavigationController(Control container, Action? onSubmit = null, Func<Control, bool>? customValidator = null)
    {
        _container = container ?? throw new ArgumentNullException(nameof(container));
        _onSubmit = onSubmit;
        _customValidator = customValidator;

        Attach(_container);
    }

    /// <summary>
    /// Excludes a control from the Enter navigation loop (e.g. Cancel or Delete buttons).
    /// </summary>
    public void Exclude(Control control)
    {
        if (control != null)
        {
            _excludedControls.Add(control);
        }
    }

    /// <summary>
    /// Attaches key listeners to container and all focusable child controls recursively.
    /// </summary>
    public void Attach(Control control)
    {
        control.KeyDown -= OnControlKeyDown;
        control.KeyDown += OnControlKeyDown;

        if (control is MoneyFlowTextLookup lookup)
        {
            // MoneyFlowTextLookup contains an inner TextBox
            foreach (Control inner in lookup.Controls)
            {
                inner.KeyDown -= OnControlKeyDown;
                inner.KeyDown += OnControlKeyDown;
            }
        }

        foreach (Control child in control.Controls)
        {
            Attach(child);
        }

        control.ControlAdded -= OnControlAdded;
        control.ControlAdded += OnControlAdded;
    }

    private void OnControlAdded(object? sender, ControlEventArgs e)
    {
        if (e.Control != null)
        {
            Attach(e.Control);
        }
    }

    private void OnControlKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control current) return;

        // If control is inside a lookup popup, do not intercept
        if (current.FindForm() is MoneyFlowLookupPopup)
        {
            return;
        }

        // If control is an inner TextBox of a lookup and lookup popup is currently visible, let popup handle it
        var lookupParent = FindLookupParent(current);
        if (lookupParent != null && lookupParent.Config != null)
        {
            // Handled inside MoneyFlowTextLookup
        }

        // 1. Shift+Enter or Shift+Tab: Navigate Previous
        if ((e.KeyCode == Keys.Enter && e.Shift) || (e.KeyCode == Keys.Tab && e.Shift))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            NavigatePrevious(current);
            return;
        }

        // 2. Plain Enter: Navigate Next or Submit on final field
        if (e.KeyCode == Keys.Enter && !e.Alt && !e.Control && !e.Shift)
        {
            // If multiline TextBox and not configured to submit, let newline happen
            if (current is TextBox tb && tb.Multiline && !tb.ReadOnly)
            {
                return;
            }

            e.Handled = true;
            e.SuppressKeyPress = true;

            // Validate current field
            if (_customValidator != null && !_customValidator(current))
            {
                return;
            }

            var activeList = GetFocusableControls();
            var target = lookupParent ?? current;

            // Check if this is the final actionable field
            var lastControl = activeList.LastOrDefault();
            if (lastControl != null && (target == lastControl || target is Button))
            {
                _onSubmit?.Invoke();
                return;
            }

            NavigateNext(target);
        }
    }

    /// <summary>
    /// Moves focus to the next focusable control in TabIndex / logical order.
    /// </summary>
    public void NavigateNext(Control current)
    {
        var controls = GetFocusableControls();
        int idx = controls.IndexOf(current);

        if (idx >= 0 && idx < controls.Count - 1)
        {
            FocusControl(controls[idx + 1]);
        }
        else if (idx == controls.Count - 1)
        {
            // Reached the end -> execute submit
            _onSubmit?.Invoke();
        }
        else
        {
            // Fallback to WinForms standard
            var form = _container.FindForm() ?? _container;
            form.SelectNextControl(current, true, true, true, true);
        }
    }

    /// <summary>
    /// Moves focus to the previous focusable control.
    /// </summary>
    public void NavigatePrevious(Control current)
    {
        var controls = GetFocusableControls();
        int idx = controls.IndexOf(current);

        if (idx > 0)
        {
            FocusControl(controls[idx - 1]);
        }
        else
        {
            var form = _container.FindForm() ?? _container;
            form.SelectNextControl(current, false, true, true, true);
        }
    }

    public Control? CurrentlyFocusedControl { get; private set; }

    private void FocusControl(Control ctrl)
    {
        CurrentlyFocusedControl = ctrl;

        if (_container is ContainerControl containerCtrl)
        {
            try { containerCtrl.ActiveControl = ctrl; } catch { }
        }

        if (ctrl is MoneyFlowTextLookup lookup)
        {
            lookup.Focus();
            lookup.SelectAll();
        }
        else if (ctrl is TextBoxBase tb)
        {
            tb.Focus();
            tb.SelectAll();
        }
        else
        {
            ctrl.Focus();
        }
    }

    private List<Control> GetFocusableControls()
    {
        var list = new List<Control>();
        CollectFocusable(_container, list);

        // Sort by TabIndex and layout position
        return list
            .Where(c => !_excludedControls.Contains(c))
            .OrderBy(c => c.TabIndex)
            .ThenBy(c => c.Top)
            .ThenBy(c => c.Left)
            .ToList();
    }

    private void CollectFocusable(Control parent, List<Control> list)
    {
        bool containerShown = _container.Visible;
        foreach (Control c in parent.Controls)
        {
            if (!c.Enabled) continue;
            if (containerShown && !c.Visible) continue;

            if (c is MoneyFlowTextLookup lookup)
            {
                list.Add(lookup);
                continue;
            }

            if (c is TextBoxBase tb && !tb.ReadOnly && tb.TabStop)
            {
                list.Add(tb);
                continue;
            }

            if (c is CheckBox cb && cb.TabStop)
            {
                list.Add(cb);
                continue;
            }

            if (c is NumericUpDown num && num.TabStop)
            {
                list.Add(num);
                continue;
            }

            if (c is Button btn && btn.TabStop && !_excludedControls.Contains(btn))
            {
                list.Add(btn);
                continue;
            }

            // Traverse containers
            if (c.HasChildren && !(c is TextBoxBase) && !(c is Button))
            {
                CollectFocusable(c, list);
            }
        }
    }

    private static MoneyFlowTextLookup? FindLookupParent(Control control)
    {
        var p = control.Parent;
        while (p != null)
        {
            if (p is MoneyFlowTextLookup lookup) return lookup;
            p = p.Parent;
        }
        return null;
    }
}
