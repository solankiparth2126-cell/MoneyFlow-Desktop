using System.Drawing;
using System.Windows.Forms;

namespace MoneyFlow.Desktop.Styling;

/// <summary>
/// Authentic Tally Prime color palette and styling constants.
/// Extracted directly from official Tally Prime UI designs.
/// </summary>
public static class TallyPrimeTheme
{
    // Primary Header & Brand Colors
    public static readonly Color NavyTopBar = Color.FromArgb(0, 56, 101);       // #003865 - Dark Navy Top Bar
    public static readonly Color NavySubBar = Color.FromArgb(0, 75, 135);       // #004B87 - Secondary blue bar
    public static readonly Color BlueVoucherTag = Color.FromArgb(0, 48, 86);     // #003056 - Voucher badge background
    public static readonly Color GoldAccent = Color.FromArgb(254, 194, 14);     // #FEC20E - Tally Gold
    public static readonly Color AmberSelection = Color.FromArgb(255, 191, 0);   // #FFBF00 - Selected item in lists

    // Surface & Window Colors
    public static readonly Color WindowBg = Color.FromArgb(240, 246, 252);      // #F0F6FC - Tally soft window background
    public static readonly Color FormSurface = Color.White;
    public static readonly Color ActiveInputYellow = Color.FromArgb(255, 248, 204); // #FFF8CC - Warm active input highlight
    public static readonly Color ActiveBorderGold = Color.FromArgb(218, 165, 32);  // Border when input is focused

    // Grid / Table Styling
    public static readonly Color GridHeaderBg = Color.FromArgb(216, 236, 248);  // #D8ECF8 - Signature soft cyan header
    public static readonly Color GridHeaderBorder = Color.FromArgb(178, 212, 235);
    public static readonly Color GridHeaderFg = Color.FromArgb(0, 40, 80);      // Dark blue table header text
    public static readonly Color GridRowHighlight = Color.FromArgb(255, 249, 210); // Active row highlight
    public static readonly Color GridLineColor = Color.FromArgb(232, 240, 248);

    // Sidebar & Button Strip
    public static readonly Color RightSidebarBg = Color.FromArgb(235, 243, 250); // #EBF3FA - Right F-key bar bg
    public static readonly Color RightSidebarBorder = Color.FromArgb(190, 216, 236);
    public static readonly Color RightButtonBg = Color.FromArgb(248, 251, 254);
    public static readonly Color RightButtonHover = Color.FromArgb(220, 236, 248);
    public static readonly Color RightButtonActive = Color.FromArgb(196, 224, 244);
    public static readonly Color RightButtonFg = Color.FromArgb(0, 56, 101);

    // Flyout "List of Ledger Accounts"
    public static readonly Color FlyoutHeaderBg = Color.FromArgb(0, 65, 121);    // #004179 - Deep blue panel header
    public static readonly Color FlyoutHeaderFg = Color.White;
    public static readonly Color FlyoutBodyBg = Color.FromArgb(245, 249, 253);
    public static readonly Color FlyoutSelectedBg = Color.FromArgb(255, 191, 0); // #FFBF00 - Golden highlight
    public static readonly Color FlyoutSelectedFg = Color.Black;

    // Bottom Status Ribbon
    public static readonly Color BottomRibbonBg = Color.FromArgb(235, 243, 250);
    public static readonly Color BottomRibbonBorder = Color.FromArgb(190, 216, 236);
    public static readonly Color BottomRibbonFg = Color.FromArgb(0, 56, 101);

    // Text & Subdued
    public static readonly Color TextPrimary = Color.FromArgb(30, 30, 30);
    public static readonly Color TextMuted = Color.FromArgb(90, 110, 130);
    public static readonly Color BalanceGreen = Color.FromArgb(0, 110, 50);
    public static readonly Color BalanceRed = Color.FromArgb(180, 20, 20);

    // Fonts
    public static readonly Font HeaderFont = new("Segoe UI", 9.5F, FontStyle.Bold);
    public static readonly Font RegularFont = new("Segoe UI", 9.25F, FontStyle.Regular);
    public static readonly Font BoldFont = new("Segoe UI", 9.25F, FontStyle.Bold);
    public static readonly Font SmallItalicFont = new("Segoe UI", 8.5F, FontStyle.Italic);
    public static readonly Font TitleBarFont = new("Segoe UI", 10F, FontStyle.Bold);
    public static readonly Font VoucherTagFont = new("Segoe UI", 10.5F, FontStyle.Bold);
}
