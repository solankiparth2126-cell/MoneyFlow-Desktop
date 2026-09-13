using System.Drawing;

namespace MoneyFlow.Desktop.Styling;

/// <summary>
/// DEPRECATED: Backward-compatibility shim for code referencing TallyPrimeTheme.
/// All values now delegate to ExecLedgerTheme.
/// New code should use ExecLedgerTheme directly.
/// </summary>
[System.Obsolete("Use ExecLedgerTheme instead. This class exists only for backward compatibility.")]
public static class TallyPrimeTheme
{
    // Primary Header & Brand Colors — mapped to Executive Ledger palette
    public static readonly Color NavyTopBar        = ExecLedgerTheme.PrimaryNavy;
    public static readonly Color NavySubBar        = ExecLedgerTheme.SteelBlue;
    public static readonly Color BlueVoucherTag    = ExecLedgerTheme.DeepNavy;
    public static readonly Color GoldAccent        = ExecLedgerTheme.SystemFocusBlue;
    public static readonly Color AmberSelection    = ExecLedgerTheme.PrimarySelection;

    // Surface & Window Colors
    public static readonly Color WindowBg          = ExecLedgerTheme.ApplicationCanvas;
    public static readonly Color FormSurface        = ExecLedgerTheme.WorkSurface;
    public static readonly Color ActiveInputYellow  = ExecLedgerTheme.PrimarySelection;
    public static readonly Color ActiveBorderGold   = ExecLedgerTheme.SystemFocusBlue;

    // Grid / Table Styling
    public static readonly Color GridHeaderBg       = ExecLedgerTheme.ApplicationCanvas;
    public static readonly Color GridHeaderBorder    = ExecLedgerTheme.PrimaryBorder;
    public static readonly Color GridHeaderFg        = ExecLedgerTheme.PrimaryText;
    public static readonly Color GridRowHighlight    = ExecLedgerTheme.PrimarySelection;
    public static readonly Color GridLineColor       = ExecLedgerTheme.GridBorder;

    // Sidebar & Button Strip
    public static readonly Color RightSidebarBg      = ExecLedgerTheme.ApplicationCanvas;
    public static readonly Color RightSidebarBorder   = ExecLedgerTheme.PrimaryBorder;
    public static readonly Color RightButtonBg        = ExecLedgerTheme.WorkSurface;
    public static readonly Color RightButtonHover     = ExecLedgerTheme.MenuHover;
    public static readonly Color RightButtonActive    = ExecLedgerTheme.PrimarySelection;
    public static readonly Color RightButtonFg        = ExecLedgerTheme.PrimaryNavy;

    // Flyout
    public static readonly Color FlyoutHeaderBg       = ExecLedgerTheme.PrimaryNavy;
    public static readonly Color FlyoutHeaderFg        = ExecLedgerTheme.WhiteText;
    public static readonly Color FlyoutBodyBg          = ExecLedgerTheme.WorkSurface;
    public static readonly Color FlyoutSelectedBg      = ExecLedgerTheme.PrimarySelection;
    public static readonly Color FlyoutSelectedFg      = ExecLedgerTheme.PrimaryText;

    // Bottom Status Ribbon
    public static readonly Color BottomRibbonBg        = ExecLedgerTheme.StatusBarBg;
    public static readonly Color BottomRibbonBorder     = ExecLedgerTheme.PrimaryBorder;
    public static readonly Color BottomRibbonFg         = ExecLedgerTheme.PrimaryText;

    // Text
    public static readonly Color TextPrimary           = ExecLedgerTheme.PrimaryText;
    public static readonly Color TextMuted             = ExecLedgerTheme.SecondaryText;
    public static readonly Color BalanceGreen          = ExecLedgerTheme.SuccessGreen;
    public static readonly Color BalanceRed            = ExecLedgerTheme.ErrorRed;

    // Fonts — use Executive Ledger fonts
    public static readonly Font HeaderFont             = ExecLedgerTheme.UIBold9;
    public static readonly Font RegularFont            = ExecLedgerTheme.UIRegular9;
    public static readonly Font BoldFont               = ExecLedgerTheme.UIBold9;
    public static readonly Font SmallItalicFont        = new(ExecLedgerTheme.UiFontFamily, 8.5F, FontStyle.Italic);
    public static readonly Font TitleBarFont           = ExecLedgerTheme.TitleBarFont;
    public static readonly Font VoucherTagFont         = ExecLedgerTheme.UIBold10;
}
