using System.Drawing;

namespace MoneyFlow.Desktop.Controls.Dropdown.Models;

/// <summary>
/// Centralized aesthetic styling and color palette for MoneyFlow Tally-style dropdown popups and controls.
/// </summary>
public static class MoneyFlowERPTheme
{
    // Header
    public static readonly Color HeaderBackColor = Color.FromArgb(15, 30, 54); // Deep Dark Navy
    public static readonly Color HeaderForeColor = Color.White;
    public static readonly Color HeaderAccentColor = Color.FromArgb(56, 189, 248); // Sky Blue F4 badge

    // Record Information Bar
    public static readonly Color RecordBarBackColor = Color.FromArgb(224, 236, 248); // Soft Light Blue
    public static readonly Color RecordBarForeColor = Color.FromArgb(30, 41, 59); // Slate-800
    public static readonly Color ActionLinkColor = Color.FromArgb(3, 105, 161); // Sky-700
    public static readonly Color ActionShortcutColor = Color.FromArgb(100, 116, 139); // Slate-500

    // Search Bar
    public static readonly Color SearchBackColor = Color.White;
    public static readonly Color SearchBorderColor = Color.FromArgb(203, 213, 225); // Slate-300
    public static readonly Color SearchForeColor = Color.FromArgb(15, 23, 42); // Slate-900

    // List Body & Rows
    public static readonly Color BodyBackColor = Color.White;
    public static readonly Color RowNormalForeColor = Color.FromArgb(15, 23, 42); // Dark Navy/Black
    public static readonly Color RowCodeForeColor = Color.FromArgb(71, 85, 105); // Slate-600
    public static readonly Color SpecialRowForeColor = Color.FromArgb(51, 65, 85); // Slate-700

    // Selected Row (Exact Reference Style: Vibrant Yellow/Gold)
    public static readonly Color SelectedRowBackColor = Color.FromArgb(254, 240, 138); // Yellow-200 / Gold
    public static readonly Color SelectedRowForeColor = Color.FromArgb(15, 23, 42); // Black / Bold Charcoal
    public static readonly Color SelectedRowBorderColor = Color.FromArgb(234, 179, 8); // Gold-500
    public static readonly Color SelectedBadgeBackColor = Color.FromArgb(15, 30, 54); // Dark Navy Badge
    public static readonly Color SelectedBadgeForeColor = Color.White;

    // Hover Row
    public static readonly Color HoverRowBackColor = Color.FromArgb(241, 245, 249); // Slate-100

    // Status & Count Footer
    public static readonly Color StatusFooterBackColor = Color.FromArgb(224, 236, 248); // Soft Light Blue
    public static readonly Color StatusFooterForeColor = Color.FromArgb(30, 41, 59); // Slate-800
    public static readonly Color FilterBadgeColor = Color.FromArgb(2, 132, 199); // Sky-600

    // Bottom Keyboard Shortcuts Bar
    public static readonly Color KeyboardFooterBackColor = Color.FromArgb(15, 30, 54); // Deep Navy
    public static readonly Color KeyboardFooterForeColor = Color.White;
    public static readonly Color KeyboardShortcutKeyColor = Color.FromArgb(254, 240, 138); // Gold Key Accent

    // Outer Popup Border
    public static readonly Color PopupBorderColor = Color.FromArgb(148, 163, 184); // Slate-400

    // Field on Form (Input Control)
    public static readonly Color FieldNormalBackColor = Color.White;
    public static readonly Color FieldNormalBorderColor = Color.FromArgb(203, 213, 225); // Slate-300
    public static readonly Color FieldFocusBackColor = Color.FromArgb(255, 251, 235); // Pale Warm Amber/Yellow
    public static readonly Color FieldFocusBorderColor = Color.FromArgb(2, 132, 199); // Sky-600

    // Dimensions
    public const int DefaultRowHeight = 26;
    public const int DefaultPopupWidth = 430;
    public const int DefaultPopupHeight = 410;
}
