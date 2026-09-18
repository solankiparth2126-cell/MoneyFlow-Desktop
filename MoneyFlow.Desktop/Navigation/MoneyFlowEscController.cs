using System;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Desktop.Controls.Lookup;
using MoneyFlow.Desktop.Dialogs;

namespace MoneyFlow.Desktop.Navigation;

/// <summary>
/// Universal centralized ESC behavior controller across all MoneyFlow modules:
/// 
/// Decision Flow:
/// 1. If a lookup popup is open: close the popup, keep field value unchanged, keep focus.
/// 2. If an editable input control is focused (TextBox, Guna2TextBox, SearchBox, Lookup editor, etc.):
///    - If the field contains text: CLEAR CURRENT FIELD ONLY, keep cursor visible, do not close module.
///    - If the field is already empty: SHOW CLOSE CONFIRMATION ("Are you sure you want to close this?").
/// 3. If no editable input is focused (e.g. grid, panel, button):
///    - Directly SHOW CLOSE CONFIRMATION ("Are you sure you want to close this?").
/// 4. On confirmation:
///    - Enter or 'Y'/'y' -> Confirm close: invokes closeAction to return to parent screen.
///    - Esc or 'N'/'n'   -> Cancel confirmation: keeps module open and restores focus.
/// </summary>
public static class MoneyFlowEscController
{
    /// <summary>
    /// Optional hook for unit testing to simulate confirmation dialog responses without modal windows.
    /// </summary>
    public static Func<IWin32Window?, bool>? ConfirmationDialogProvider { get; set; }

    /// <summary>
    /// Processes an ESC keypress for the specified active control and host form.
    /// </summary>
    /// <param name="activeControl">The control currently holding keyboard focus.</param>
    /// <param name="hostForm">The active module form.</param>
    /// <param name="closeAction">The action to execute when closing is confirmed (e.g. Close(), ReturnToParent()).</param>
    /// <param name="hasOpenPopup">Optional function to check if a custom popup/dropdown is open.</param>
    /// <param name="closePopupAction">Optional action to close the open popup/dropdown.</param>
    /// <returns>True if the ESC key was handled.</returns>
    public static bool HandleEsc(
        Control? activeControl,
        Form hostForm,
        Action closeAction,
        Func<bool>? hasOpenPopup = null,
        Action? closePopupAction = null)
    {
        // ── Rule 1: If custom or lookup popup is open, close popup only ──
        if (hasOpenPopup != null && hasOpenPopup())
        {
            closePopupAction?.Invoke();
            return true;
        }

        // Check if focused control belongs to a MoneyFlowTextLookup with an open popup
        var lookupAncestor = FindAncestorOrSelf<MoneyFlowTextLookup>(activeControl);
        if (lookupAncestor != null && lookupAncestor.IsPopupOpen)
        {
            lookupAncestor.ClosePopup();
            return true;
        }

        // ── Rule 2: Inspect if active control is an editable input ──
        if (TryGetEditableInput(activeControl, out var inputHelper))
        {
            if (inputHelper.HasValue)
            {
                // First ESC: Clear current field only, keep focus & cursor visible
                inputHelper.ClearValue();
                return true;
            }
        }

        // ── Rule 3: Active field is empty OR non-input focused -> Show Close Confirmation ──
        bool confirmed = ConfirmationDialogProvider != null
            ? ConfirmationDialogProvider(hostForm)
            : CloseModuleConfirmationDialog.ConfirmClose(hostForm);

        if (confirmed)
        {
            closeAction();
        }
        else
        {
            // Restore focus to the previous control
            if (activeControl != null && activeControl.CanFocus && !activeControl.IsDisposed)
            {
                activeControl.Focus();
            }
        }

        return true;
    }

    /// <summary>
    /// Resolves editable input controls (TextBox, Guna2TextBox, MaskedTextBox, MoneyFlowTextLookup, ComboBox).
    /// </summary>
    private static bool TryGetEditableInput(Control? control, out IEditableInputHelper helper)
    {
        helper = null!;
        if (control == null) return false;

        // Check for MoneyFlowTextLookup or inside it
        var lookup = FindAncestorOrSelf<MoneyFlowTextLookup>(control);
        if (lookup != null)
        {
            helper = new LookupInputHelper(lookup);
            return true;
        }

        if (control is TextBox tb)
        {
            if (tb.ReadOnly) return false;
            helper = new StandardTextBoxHelper(tb);
            return true;
        }

        if (control is Guna2TextBox gtb)
        {
            if (gtb.ReadOnly) return false;
            helper = new Guna2TextBoxHelper(gtb);
            return true;
        }

        if (control is MaskedTextBox mtb)
        {
            if (mtb.ReadOnly) return false;
            helper = new MaskedTextBoxHelper(mtb);
            return true;
        }

        if (control is ComboBox cb)
        {
            if (cb.DropDownStyle == ComboBoxStyle.DropDownList) return false;
            helper = new ComboBoxHelper(cb);
            return true;
        }

        return false;
    }

    private static T? FindAncestorOrSelf<T>(Control? control) where T : Control
    {
        var cur = control;
        while (cur != null)
        {
            if (cur is T match) return match;
            cur = cur.Parent;
        }
        return null;
    }

    private interface IEditableInputHelper
    {
        bool HasValue { get; }
        void ClearValue();
    }

    private class StandardTextBoxHelper : IEditableInputHelper
    {
        private readonly TextBox _tb;
        public StandardTextBoxHelper(TextBox tb) => _tb = tb;

        public bool HasValue => !string.IsNullOrEmpty(_tb.Text);

        public void ClearValue()
        {
            _tb.Clear();
            _tb.SelectionStart = 0;
            _tb.SelectionLength = 0;
            _tb.Focus();
        }
    }

    private class Guna2TextBoxHelper : IEditableInputHelper
    {
        private readonly Guna2TextBox _gtb;
        public Guna2TextBoxHelper(Guna2TextBox gtb) => _gtb = gtb;

        public bool HasValue => !string.IsNullOrEmpty(_gtb.Text);

        public void ClearValue()
        {
            _gtb.Clear();
            _gtb.SelectionStart = 0;
            _gtb.SelectionLength = 0;
            _gtb.Focus();
        }
    }

    private class MaskedTextBoxHelper : IEditableInputHelper
    {
        private readonly MaskedTextBox _mtb;
        public MaskedTextBoxHelper(MaskedTextBox mtb) => _mtb = mtb;

        public bool HasValue => !string.IsNullOrWhiteSpace(_mtb.Text);

        public void ClearValue()
        {
            _mtb.Clear();
            _mtb.Focus();
        }
    }

    private class ComboBoxHelper : IEditableInputHelper
    {
        private readonly ComboBox _cb;
        public ComboBoxHelper(ComboBox cb) => _cb = cb;

        public bool HasValue => !string.IsNullOrEmpty(_cb.Text);

        public void ClearValue()
        {
            _cb.Text = string.Empty;
            _cb.SelectedIndex = -1;
            _cb.Focus();
        }
    }

    private class LookupInputHelper : IEditableInputHelper
    {
        private readonly MoneyFlowTextLookup _lookup;
        public LookupInputHelper(MoneyFlowTextLookup lookup) => _lookup = lookup;

        public bool HasValue => !string.IsNullOrEmpty(_lookup.Text) || _lookup.SelectedValue != null;

        public void ClearValue()
        {
            _lookup.Clear();
            _lookup.Focus();
        }
    }
}
