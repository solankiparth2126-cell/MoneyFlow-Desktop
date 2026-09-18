using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MoneyFlow.Desktop.Controls.Lookup;

/// <summary>
/// Centralized keyboard handling and navigation controller for MoneyFlow universal lookups.
/// </summary>
public class LookupKeyboardController
{
    public int SelectedIndex { get; set; }
    public int PageSize { get; set; } = 10;

    public void ResetSelection()
    {
        SelectedIndex = 0;
    }

    public void ClampSelection(int count)
    {
        if (count <= 0)
        {
            SelectedIndex = -1;
        }
        else if (SelectedIndex < 0)
        {
            SelectedIndex = 0;
        }
        else if (SelectedIndex >= count)
        {
            SelectedIndex = count - 1;
        }
    }

    public bool HandleKeyDown(
        KeyEventArgs e,
        IReadOnlyList<LookupItem> items,
        Action<LookupItem>? onSelect,
        Action? onCancel,
        Action? onCreate,
        Action? onShowMore,
        Action<bool>? onToggleHierarchy = null)
    {
        int count = items.Count;
        if (count > 0) ClampSelection(count);

        if (e.Alt && e.KeyCode == Keys.C)
        {
            onCreate?.Invoke();
            e.Handled = true;
            return true;
        }

        if (e.Alt && e.KeyCode == Keys.M)
        {
            onShowMore?.Invoke();
            e.Handled = true;
            return true;
        }

        switch (e.KeyCode)
        {
            case Keys.Down:
                if (count > 0)
                {
                    SelectedIndex = Math.Min(SelectedIndex + 1, count - 1);
                }
                e.Handled = true;
                return true;

            case Keys.Up:
                if (count > 0)
                {
                    SelectedIndex = Math.Max(SelectedIndex - 1, 0);
                }
                e.Handled = true;
                return true;

            case Keys.PageDown:
                if (count > 0)
                {
                    SelectedIndex = Math.Min(SelectedIndex + PageSize, count - 1);
                }
                e.Handled = true;
                return true;

            case Keys.PageUp:
                if (count > 0)
                {
                    SelectedIndex = Math.Max(SelectedIndex - PageSize, 0);
                }
                e.Handled = true;
                return true;

            case Keys.Home:
                if (count > 0) SelectedIndex = 0;
                e.Handled = true;
                return true;

            case Keys.End:
                if (count > 0) SelectedIndex = count - 1;
                e.Handled = true;
                return true;

            case Keys.Right:
                // Expand hierarchy
                if (SelectedIndex >= 0 && SelectedIndex < count)
                {
                    var cur = items[SelectedIndex];
                    if (cur.HasChildren && !cur.IsExpanded)
                    {
                        cur.IsExpanded = true;
                        onToggleHierarchy?.Invoke(true);
                    }
                }
                e.Handled = true;
                return true;

            case Keys.Left:
                // Collapse hierarchy
                if (SelectedIndex >= 0 && SelectedIndex < count)
                {
                    var cur = items[SelectedIndex];
                    if (cur.HasChildren && cur.IsExpanded)
                    {
                        cur.IsExpanded = false;
                        onToggleHierarchy?.Invoke(false);
                    }
                }
                e.Handled = true;
                return true;

            case Keys.Enter:
                if (SelectedIndex >= 0 && SelectedIndex < count)
                {
                    onSelect?.Invoke(items[SelectedIndex]);
                }
                e.Handled = true;
                return true;

            case Keys.Escape:
                onCancel?.Invoke();
                e.Handled = true;
                return true;
        }

        return false;
    }
}
