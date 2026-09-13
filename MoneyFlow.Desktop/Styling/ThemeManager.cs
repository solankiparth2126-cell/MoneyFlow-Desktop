using System;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace MoneyFlow.Desktop.Styling;

/// <summary>
/// Executive Ledger Desktop — Unified theme manager.
/// Single-theme system applying the Executive Ledger identity across all forms.
/// Replaces the previous multi-theme (ClassicTeal/DarkSlate/LightNeutral) system.
/// </summary>
public static class ThemeManager
{
    public static string CurrentDensity { get; private set; } = "Compact";

    /// <summary>Set display density. "Compact" uses 24px grid rows, "Standard" uses 28px.</summary>
    public static void SetDensity(string density)
    {
        if (!string.IsNullOrWhiteSpace(density))
            CurrentDensity = density;
    }

    /// <summary>
    /// Retained for backward compatibility with existing code that calls SetTheme().
    /// The theme parameter is ignored — Executive Ledger is the only theme.
    /// </summary>
    public static void SetTheme(string? themeName, string? density = null)
    {
        if (!string.IsNullOrWhiteSpace(density))
            CurrentDensity = density!;
    }

    /// <summary>Apply the Executive Ledger theme to a form and all its children.</summary>
    public static void ApplyTheme(Form form)
    {
        ExecLedgerStyler.ApplyFormBase(form);
        ExecLedgerStyler.ApplyToAllControls(form.Controls);
    }

    /// <summary>Apply the theme recursively to a control collection.</summary>
    public static void ApplyToControls(Control.ControlCollection controls)
    {
        ExecLedgerStyler.ApplyToAllControls(controls);
    }

    /// <summary>Style a DataGridView with Executive Ledger grid appearance.</summary>
    public static void StyleGrid(DataGridView grid, string? density = null)
    {
        bool dense = (density ?? CurrentDensity).Equals("Compact", StringComparison.OrdinalIgnoreCase);

        if (grid is Guna2DataGridView g2Grid)
            ExecLedgerStyler.StyleGrid(g2Grid, dense);
        else
            ExecLedgerStyler.StyleStandardGrid(grid, dense);
    }

    /// <summary>Style a Button with Executive Ledger appearance.</summary>
    public static void StyleButton(Button btn, bool isPrimary = false)
    {
        if (isPrimary)
            ExecLedgerStyler.StylePrimaryButton(btn);
        else
            ExecLedgerStyler.StyleSecondaryButton(btn);
    }

    // Backward-compatible color accessors for forms that reference ThemeManager.Colors
    public static ThemeColors Colors => _colors;

    private static readonly ThemeColors _colors = new()
    {
        HeaderBg = ExecLedgerTheme.PrimaryNavy,
        HeaderFg = ExecLedgerTheme.WhiteText,
        AccentColor = ExecLedgerTheme.SystemFocusBlue,
        WindowBg = ExecLedgerTheme.ApplicationCanvas,
        SurfaceBg = ExecLedgerTheme.WorkSurface,
        SurfaceFg = ExecLedgerTheme.PrimaryText,
        BorderColor = ExecLedgerTheme.PrimaryBorder,
        GridHeaderBg = ExecLedgerTheme.ApplicationCanvas,
        GridHeaderFg = ExecLedgerTheme.PrimaryText,
        GridRowAltBg = ExecLedgerTheme.SecondarySurface,
        GridSelectionBg = ExecLedgerTheme.PrimarySelection,
        GridSelectionFg = ExecLedgerTheme.PrimaryText,
        SuccessBg = ExecLedgerTheme.SuccessBg,
        SuccessFg = ExecLedgerTheme.SuccessGreen,
        DangerBg = ExecLedgerTheme.ErrorBg,
        DangerFg = ExecLedgerTheme.ErrorRed
    };
}

/// <summary>
/// Backward-compatible theme color container.
/// Maps Executive Ledger tokens to the property names used by existing forms.
/// </summary>
public class ThemeColors
{
    public Color HeaderBg { get; set; }
    public Color HeaderFg { get; set; }
    public Color AccentColor { get; set; }
    public Color WindowBg { get; set; }
    public Color SurfaceBg { get; set; }
    public Color SurfaceFg { get; set; }
    public Color BorderColor { get; set; }
    public Color GridHeaderBg { get; set; }
    public Color GridHeaderFg { get; set; }
    public Color GridRowAltBg { get; set; }
    public Color GridSelectionBg { get; set; }
    public Color GridSelectionFg { get; set; }
    public Color SuccessBg { get; set; }
    public Color SuccessFg { get; set; }
    public Color DangerBg { get; set; }
    public Color DangerFg { get; set; }
}
