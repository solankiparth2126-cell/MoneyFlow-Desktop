using System;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace MoneyFlow.Desktop.Styling;

/// <summary>
/// Executive Ledger Desktop — Centralized design token system.
/// All colors, fonts, dimensions, and geometry constants for the entire application.
/// </summary>
public static class ExecLedgerTheme
{
    // ═══════════════════════════════════════════════════════════════
    //  PRIMARY CORPORATE PALETTE
    // ═══════════════════════════════════════════════════════════════

    public static readonly Color PrimaryNavy       = Color.FromArgb(27, 54, 93);        // #1B365D
    public static readonly Color DeepNavy          = Color.FromArgb(0, 32, 70);          // #002046
    public static readonly Color SteelBlue         = Color.FromArgb(46, 91, 136);        // #2E5B88
    public static readonly Color SystemFocusBlue   = Color.FromArgb(37, 99, 235);        // #2563EB
    public static readonly Color PrimarySelection  = Color.FromArgb(224, 237, 253);      // #E0EDFD

    // ═══════════════════════════════════════════════════════════════
    //  WORKSPACE SURFACES
    // ═══════════════════════════════════════════════════════════════

    public static readonly Color ApplicationCanvas = Color.FromArgb(240, 242, 245);      // #F0F2F5
    public static readonly Color WorkSurface       = Color.White;                         // #FFFFFF
    public static readonly Color SecondarySurface  = Color.FromArgb(248, 250, 252);      // #F8FAFC
    public static readonly Color GridSurface       = Color.White;                         // #FFFFFF

    // ═══════════════════════════════════════════════════════════════
    //  BORDERS
    // ═══════════════════════════════════════════════════════════════

    public static readonly Color PrimaryBorder     = Color.FromArgb(203, 213, 225);      // #CBD5E1
    public static readonly Color GridBorder        = Color.FromArgb(226, 232, 240);      // #E2E8F0
    public static readonly Color MutedBorder       = Color.FromArgb(148, 163, 184);      // #94A3B8

    // ═══════════════════════════════════════════════════════════════
    //  TEXT
    // ═══════════════════════════════════════════════════════════════

    public static readonly Color PrimaryText       = Color.FromArgb(15, 23, 42);         // #0F172A
    public static readonly Color SecondaryText     = Color.FromArgb(100, 116, 139);      // #64748B
    public static readonly Color DisabledText      = Color.FromArgb(148, 163, 184);      // #94A3B8
    public static readonly Color WhiteText         = Color.White;

    // ═══════════════════════════════════════════════════════════════
    //  FUNCTIONAL COLORS
    // ═══════════════════════════════════════════════════════════════

    public static readonly Color SuccessGreen      = Color.FromArgb(21, 128, 61);        // #15803D
    public static readonly Color WarningAmber      = Color.FromArgb(180, 83, 9);         // #B45309
    public static readonly Color ErrorRed          = Color.FromArgb(185, 28, 28);        // #B91C1C

    public static readonly Color SuccessBg         = Color.FromArgb(220, 252, 231);      // #DCFCE7
    public static readonly Color WarningBg         = Color.FromArgb(254, 243, 199);      // #FEF3C7
    public static readonly Color ErrorBg           = Color.FromArgb(254, 226, 226);      // #FEE2E2

    public static readonly Color SuccessBorder     = Color.FromArgb(134, 239, 172);      // #86EFAC
    public static readonly Color WarningBorder     = Color.FromArgb(253, 230, 138);      // #FDE68A
    public static readonly Color ErrorBorder       = Color.FromArgb(252, 165, 165);      // #FCA5A5

    // ═══════════════════════════════════════════════════════════════
    //  INTERACTIVE STATES
    // ═══════════════════════════════════════════════════════════════

    public static readonly Color MenuHover         = Color.FromArgb(224, 237, 253);      // #E0EDFD
    public static readonly Color ToolbarHover      = Color.FromArgb(240, 242, 245);      // #F0F2F5
    public static readonly Color ToolbarPressed    = Color.FromArgb(224, 237, 253);      // #E0EDFD
    public static readonly Color ButtonHover       = Color.FromArgb(46, 91, 136);        // #2E5B88
    public static readonly Color CloseHover        = Color.FromArgb(185, 28, 28);        // #B91C1C
    public static readonly Color GridRowHover      = Color.FromArgb(241, 245, 249);      // #F1F5F9
    public static readonly Color SelectedRowBorder = Color.FromArgb(147, 197, 253);      // #93C5FD

    // ═══════════════════════════════════════════════════════════════
    //  STATUS BAR
    // ═══════════════════════════════════════════════════════════════

    public static readonly Color StatusBarBg       = Color.FromArgb(226, 232, 240);      // #E2E8F0

    // ═══════════════════════════════════════════════════════════════
    //  INPUT STATES
    // ═══════════════════════════════════════════════════════════════

    public static readonly Color InputBg           = Color.White;
    public static readonly Color InputFocusBorder  = Color.FromArgb(37, 99, 235);        // #2563EB
    public static readonly Color InputInvalidBorder = Color.FromArgb(185, 28, 28);       // #B91C1C
    public static readonly Color InputInvalidBg    = Color.FromArgb(254, 242, 242);      // #FEF2F2
    public static readonly Color InputReadOnlyBg   = Color.FromArgb(248, 250, 252);      // #F8FAFC

    // ═══════════════════════════════════════════════════════════════
    //  DIMENSIONS (pixels)
    // ═══════════════════════════════════════════════════════════════

    public const int TitleBarHeight       = 32;
    public const int MenuBarHeight        = 26;
    public const int ToolbarHeight        = 36;
    public const int ToolbarButtonHeight  = 28;
    public const int ToolbarButtonMinWidth = 28;
    public const int ToolbarButtonPadH    = 8;
    public const int StandardGridRow      = 28;
    public const int DenseGridRow         = 24;
    public const int GridHeaderHeight     = 26;
    public const int GridFooterHeight     = 28;
    public const int StatusBarHeight      = 24;
    public const int DockSplitter         = 4;
    public const int InputHeight          = 24;
    public const int InputPadH            = 6;
    public const int InputPadV            = 2;
    public const int CheckboxSize         = 14;
    public const int RadioSize            = 14;
    public const int LookupTriggerWidth   = 20;
    public const int DialogButtonWidth    = 85;
    public const int DialogButtonHeight   = 28;

    // ═══════════════════════════════════════════════════════════════
    //  GEOMETRY
    // ═══════════════════════════════════════════════════════════════

    public const int BorderRadius = 0;

    // ═══════════════════════════════════════════════════════════════
    //  TYPOGRAPHY
    // ═══════════════════════════════════════════════════════════════

    private static string? _uiFontFamily;
    private static string? _monoFontFamily;

    /// <summary>UI font: Arimo → Segoe UI fallback.</summary>
    public static string UiFontFamily => _uiFontFamily ??= ResolveFont("Arimo", "Segoe UI");

    /// <summary>Financial numeric font: JetBrains Mono → Consolas fallback.</summary>
    public static string MonoFontFamily => _monoFontFamily ??= ResolveFont("JetBrains Mono", "Consolas");

    // --- UI Fonts ---
    public static Font UIRegular9     => new(UiFontFamily, 9F, FontStyle.Regular);
    public static Font UIBold9        => new(UiFontFamily, 9F, FontStyle.Bold);
    public static Font UIRegular8     => new(UiFontFamily, 8F, FontStyle.Regular);
    public static Font UIBold8        => new(UiFontFamily, 8F, FontStyle.Bold);
    public static Font UIRegular10    => new(UiFontFamily, 10F, FontStyle.Regular);
    public static Font UIBold10       => new(UiFontFamily, 10F, FontStyle.Bold);
    public static Font UIRegular11    => new(UiFontFamily, 11F, FontStyle.Regular);
    public static Font UIBold11       => new(UiFontFamily, 11F, FontStyle.Bold);
    public static Font UIRegular12    => new(UiFontFamily, 12F, FontStyle.Regular);
    public static Font UIBold12       => new(UiFontFamily, 12F, FontStyle.Bold);

    // --- Contextual Fonts ---
    public static Font TitleBarFont   => new(UiFontFamily, 9.5F, FontStyle.Bold);
    public static Font MenuFont       => new(UiFontFamily, 9F, FontStyle.Regular);
    public static Font GridHeaderFont => new(UiFontFamily, 8.5F, FontStyle.Bold);
    public static Font StatusBarFont  => new(UiFontFamily, 8F, FontStyle.Regular);

    // --- Financial / Monospace Fonts ---
    public static Font MonoRegular9   => new(MonoFontFamily, 9F, FontStyle.Regular);
    public static Font MonoBold9      => new(MonoFontFamily, 9F, FontStyle.Bold);
    public static Font MonoRegular8   => new(MonoFontFamily, 8F, FontStyle.Regular);
    public static Font MonoRegular10  => new(MonoFontFamily, 10F, FontStyle.Regular);
    public static Font MonoBold10     => new(MonoFontFamily, 10F, FontStyle.Bold);

    // ═══════════════════════════════════════════════════════════════
    //  FORMATTING HELPERS
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Format a currency value with right-alignment style: 1,250.00</summary>
    public static string FormatCurrency(decimal value)
    {
        if (value == 0m) return "-";
        return value.ToString("N2");
    }

    /// <summary>Format a negative currency value in parentheses: (1,250.00)</summary>
    public static string FormatNegative(decimal value)
    {
        if (value == 0m) return "-";
        if (value < 0) return $"({Math.Abs(value):N2})";
        return value.ToString("N2");
    }

    /// <summary>Format a balance value: positive normal, negative in red parentheses, zero as dash.</summary>
    public static (string Text, Color ForeColor) FormatBalance(decimal value)
    {
        if (value == 0m) return ("-", SecondaryText);
        if (value < 0) return ($"({Math.Abs(value):N2})", ErrorRed);
        return (value.ToString("N2"), PrimaryText);
    }

    /// <summary>Format variance for balanced/unbalanced display.</summary>
    public static (string Text, Color ForeColor, Color BackColor) FormatVariance(decimal debit, decimal credit)
    {
        var diff = debit - credit;
        if (diff == 0m)
            return ("● BALANCED (0.00)", SuccessGreen, SuccessBg);
        var sign = diff > 0 ? "+" : "";
        return ($"▲ OUT OF BALANCE: {sign}{diff:N2}", ErrorRed, ErrorBg);
    }

    // ═══════════════════════════════════════════════════════════════
    //  PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════

    private static string ResolveFont(string preferred, string fallback)
    {
        try
        {
            using var families = new InstalledFontCollection();
            if (families.Families.Any(f => f.Name.Equals(preferred, StringComparison.OrdinalIgnoreCase)))
                return preferred;
        }
        catch { /* Font enumeration failed, use fallback */ }
        return fallback;
    }
}
