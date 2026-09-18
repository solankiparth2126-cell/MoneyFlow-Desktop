using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Premium Money Flow Desktop Application Splash Screen.
/// Built strictly with the Executive Ledger Desktop Theme:
/// - Light Blue Financial Workspace background (#F8F9FF) with low-opacity accounting grid and ledger structures
/// - Abstract financial flow curves (Income -> Transactions -> Balance -> Growth) with square data points
/// - Official Money Flow ribbon logo with soft grounding shadow
/// - Arimo typography, 24px Bold "Money Flow" header (#0F172A)
/// - 320x6px sharp 0px border-radius process bar (#E5EEFF track, #CBD5E1 border, #1B365D / #2E5B88 progress)
/// - Dynamic loading statuses in Arimo 10px (#64748B)
/// - Bottom branding: "Money Flow • Version 1.0.0"
/// - Elevation shadow on window and logo
/// - Smooth 60 FPS animation sequence with auto-completion and graceful transition
/// </summary>
public class SplashScreenForm : Form
{
    protected override CreateParams CreateParams
    {
        get
        {
            const int CS_DROPSHADOW = 0x00020000;
            CreateParams cp = base.CreateParams;
            cp.ClassStyle |= CS_DROPSHADOW;
            return cp;
        }
    }
    // ═══════════════════════════════════════════════════════════════
    //  THEME PALETTE CONSTANTS
    // ═══════════════════════════════════════════════════════════════
    private static readonly Color ColorBaseBg = Color.FromArgb(248, 249, 255);       // #F8F9FF
    private static readonly Color ColorPrimaryNavy = Color.FromArgb(27, 54, 93);      // #1B365D
    private static readonly Color ColorSecondaryBlue = Color.FromArgb(46, 91, 136);   // #2E5B88
    private static readonly Color ColorFocusAccent = Color.FromArgb(37, 99, 235);     // #2563EB
    private static readonly Color ColorTitleText = Color.FromArgb(15, 23, 42);        // #0F172A
    private static readonly Color ColorMutedText = Color.FromArgb(100, 116, 139);     // #64748B
    private static readonly Color ColorBorder = Color.FromArgb(203, 213, 225);        // #CBD5E1
    private static readonly Color ColorTrackBg = Color.FromArgb(229, 238, 255);       // #E5EEFF
    private static readonly Color ColorFlowLine = Color.FromArgb(211, 228, 254);      // #D3E4FE
    private static readonly Color ColorTonalCard = Color.FromArgb(220, 233, 255);     // #DCE9FF

    // Dimensions
    public const int SplashWidth = 640;
    public const int SplashHeight = 400;
    private const int ProgressBarWidth = 320;
    private const int ProgressBarHeight = 6;

    public bool IsDoubleBuffered => DoubleBuffered;

    // Fonts
    private readonly Font _fontTitle;
    private readonly Font _fontStatus;
    private readonly Font _fontVersion;
    private readonly Font _fontLedgerSymbols;

    // Animation & State
    private readonly System.Windows.Forms.Timer _animTimer;
    private readonly Stopwatch _stopwatch = new();
    private const int TotalDurationMs = 2400; // 2.4s professional splash duration
    private float _progress = 0f;
    private float _displayProgress = 0f;
    private string _currentStatus = "Loading...";
    private float _fadeAlpha = 0f;
    private float _logoSlideOffset = 8f;
    private float _flowWavePhase = 0f;

    /// <summary>
    /// Event raised when the splash animation completes.
    /// </summary>
    public event EventHandler? SplashCompleted;

    public SplashScreenForm()
    {
        // 1. Form Window Properties
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(SplashWidth, SplashHeight);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = ColorBaseBg;
        DoubleBuffered = true;

        // Form elevation shadow
        _ = new Guna2ShadowForm
        {
            TargetForm = this,
            ShadowColor = Color.FromArgb(15, 23, 42),
            BorderRadius = 4
        };

        // Form Icon
        try
        {
            var appIcon = ExecLedgerIcons.GetAppIcon();
            if (appIcon != null)
            {
                Icon = appIcon;
            }
        }
        catch
        {
            // Ignore non-fatal icon resolution fallback
        }

        // 2. Initialize Typography with Arimo fallback
        _fontTitle = CreateArimoFont(24f, FontStyle.Bold);
        _fontStatus = CreateArimoFont(10f, FontStyle.Regular);
        _fontVersion = CreateArimoFont(10f, FontStyle.Regular);
        _fontLedgerSymbols = CreateArimoFont(9f, FontStyle.Regular);

        // 3. User interaction to skip splash
        Cursor = Cursors.Default;
        Click += (s, e) => SkipToEnd();
        KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                SkipToEnd();
            }
        };

        // 4. Setup High-Frame-Rate Animation Timer (60 FPS, ~16ms)
        _animTimer = new System.Windows.Forms.Timer
        {
            Interval = 16
        };
        _animTimer.Tick += OnAnimationTick;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _stopwatch.Restart();
        _animTimer.Start();
    }

    private void SkipToEnd()
    {
        _progress = 1f;
        _displayProgress = 1f;
        _fadeAlpha = 1f;
        _logoSlideOffset = 0f;
        _currentStatus = "Ready";
        Invalidate();
        CompleteSplash();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        long elapsed = _stopwatch.ElapsedMilliseconds;

        // Stage 1: Fade-in background and flow structures (0 - 350ms)
        _fadeAlpha = Math.Clamp(elapsed / 350f, 0f, 1f);

        // Stage 2: Logo and title slide into place (100 - 450ms)
        float logoT = Math.Clamp((elapsed - 100) / 350f, 0f, 1f);
        _logoSlideOffset = (1f - SmoothStep(logoT)) * 8f;

        // Stage 3: Progress Bar loading (250 - 2150ms)
        float loadT = Math.Clamp((elapsed - 250) / 1900f, 0f, 1f);
        _progress = SmoothStep(loadT);

        // Smooth visual lag on progress bar
        _displayProgress += (_progress - _displayProgress) * 0.25f;

        // Subtle fluid motion on background flow lines
        _flowWavePhase = (elapsed / 1000f) * (float)Math.PI;

        // Update Dynamic Loading Statuses
        _currentStatus = _progress switch
        {
            < 0.15f => "Loading...",
            < 0.35f => "Preparing workspace...",
            < 0.60f => "Loading accounts...",
            < 0.82f => "Initializing transactions...",
            < 0.95f => "Preparing dashboard...",
            _ => "Ready"
        };

        Invalidate();

        // Stage 4: Completion
        if (elapsed >= TotalDurationMs)
        {
            CompleteSplash();
        }
    }

    private void CompleteSplash()
    {
        _animTimer.Stop();
        SplashCompleted?.Invoke(this, EventArgs.Empty);
        DialogResult = DialogResult.OK;
        Close();
    }

    private static float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }

    // ═══════════════════════════════════════════════════════════════
    //  PAINT PIPELINE
    // ═══════════════════════════════════════════════════════════════

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;

        // 1. Base Workspace Background
        g.Clear(ColorBaseBg);

        // 2. Faint Financial Workspace Ledger Structures
        DrawLedgerBackground(g);

        // 3. Subtle Abstract Financial Flow Curves & Data Points
        DrawFinancialFlowEffect(g);

        // 4. Center Geometric Money Flow Logo
        DrawCenterLogo(g);

        // 5. Application Name ("Money Flow")
        DrawApplicationTitle(g);

        // 6. Sharp Desktop Process Bar (320x6px, 0px border-radius)
        DrawProgressBar(g);

        // 7. Loading Status Text
        DrawLoadingStatus(g);

        // 8. Bottom Information ("Money Flow • Version 1.0.0")
        DrawBottomInfo(g);

        // 9. Sharp Perimeter Border
        using var borderPen = new Pen(ColorBorder, 1);
        g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
    }

    // ═══════════════════════════════════════════════════════════════
    //  1. SUBTLE ACCOUNTING LEDGER BACKGROUND
    // ═══════════════════════════════════════════════════════════════
    private void DrawLedgerBackground(Graphics g)
    {
        int alphaMultiplier = (int)(255 * _fadeAlpha);
        if (alphaMultiplier <= 0) return;

        // A. Subtle Tonal Surface Blocks (Header Cards, Balance Panels)
        int blockAlpha = Math.Clamp((int)(18 * _fadeAlpha), 0, 255);
        int blockBorderAlpha = Math.Clamp((int)(40 * _fadeAlpha), 0, 255);

        using (var brushTopLeft = new SolidBrush(Color.FromArgb(blockAlpha, ColorTrackBg)))
        using (var brushTopRight = new SolidBrush(Color.FromArgb(blockAlpha, ColorTonalCard)))
        using (var brushBottomLeft = new SolidBrush(Color.FromArgb(blockAlpha, ColorFlowLine)))
        using (var brushBottomRight = new SolidBrush(Color.FromArgb(blockAlpha, ColorTrackBg)))
        using (var penBlockBorder = new Pen(Color.FromArgb(blockBorderAlpha, ColorBorder), 1))
        {
            // Top-left summary block
            var rectTL = new Rectangle(20, 18, 140, 52);
            g.FillRectangle(brushTopLeft, rectTL);
            g.DrawRectangle(penBlockBorder, rectTL);

            // Top-right balance block
            var rectTR = new Rectangle(Width - 160, 18, 140, 68);
            g.FillRectangle(brushTopRight, rectTR);
            g.DrawRectangle(penBlockBorder, rectTR);

            // Bottom-left transaction ledger block
            var rectBL = new Rectangle(20, Height - 88, 130, 56);
            g.FillRectangle(brushBottomLeft, rectBL);
            g.DrawRectangle(penBlockBorder, rectBL);

            // Bottom-right accounts summary block
            var rectBR = new Rectangle(Width - 150, Height - 88, 130, 56);
            g.FillRectangle(brushBottomRight, rectBR);
            g.DrawRectangle(penBlockBorder, rectBR);
        }

        // B. Thin Horizontal Ledger Lines
        int lineAlpha = Math.Clamp((int)(38 * _fadeAlpha), 0, 255);
        using (var penHorizontal = new Pen(Color.FromArgb(lineAlpha, ColorFlowLine), 1))
        {
            for (int y = 44; y < Height - 30; y += 32)
            {
                g.DrawLine(penHorizontal, 12, y, Width - 12, y);
            }
        }

        // C. Faint Vertical Accounting Column Divisions
        int colAlpha = Math.Clamp((int)(32 * _fadeAlpha), 0, 255);
        using (var penVertical = new Pen(Color.FromArgb(colAlpha, ColorBorder), 1))
        {
            int[] columnX = { 65, 160, 480, 575 };
            foreach (int x in columnX)
            {
                g.DrawLine(penVertical, x, 14, x, Height - 14);
            }
        }

        // D. Accounting Double-Underline Total Rules
        using (var penDouble = new Pen(Color.FromArgb(colAlpha, ColorBorder), 1))
        {
            // Inside top-right balance block
            g.DrawLine(penDouble, Width - 150, 76, Width - 30, 76);
            g.DrawLine(penDouble, Width - 150, 79, Width - 30, 79);

            // Inside bottom-right block
            g.DrawLine(penDouble, Width - 140, Height - 42, Width - 30, Height - 42);
            g.DrawLine(penDouble, Width - 140, Height - 39, Width - 30, Height - 39);
        }

        // E. Small, Low-Opacity Currency & Accounting Symbols
        int symbolAlpha = Math.Clamp((int)(40 * _fadeAlpha), 0, 255);
        using var brushSymbol = new SolidBrush(Color.FromArgb(symbolAlpha, ColorMutedText));

        // Decorative ledger notations placed strategically across margins & cells
        g.DrawString("₹", _fontLedgerSymbols, brushSymbol, 32, 28);
        g.DrawString("0.00", _fontLedgerSymbols, brushSymbol, 92, 28);
        g.DrawString("DR", _fontLedgerSymbols, brushSymbol, Width - 145, 28);
        g.DrawString("CR", _fontLedgerSymbols, brushSymbol, Width - 75, 28);

        g.DrawString("$", _fontLedgerSymbols, brushSymbol, 32, 118);
        g.DrawString("€", _fontLedgerSymbols, brushSymbol, Width - 50, 118);

        g.DrawString("£", _fontLedgerSymbols, brushSymbol, 32, 212);
        g.DrawString("%", _fontLedgerSymbols, brushSymbol, Width - 50, 212);

        g.DrawString("∑", _fontLedgerSymbols, brushSymbol, 32, Height - 76);
        g.DrawString("BAL", _fontLedgerSymbols, brushSymbol, Width - 110, Height - 76);
    }

    // ═══════════════════════════════════════════════════════════════
    //  2. FINANCIAL FLOW EFFECT (Subtle curved geometric flow lines)
    // ═══════════════════════════════════════════════════════════════
    private void DrawFinancialFlowEffect(Graphics g)
    {
        int flowAlpha = Math.Clamp((int)(55 * _fadeAlpha), 0, 255);
        if (flowAlpha <= 0) return;

        using var penFlow = new Pen(Color.FromArgb(flowAlpha, ColorFlowLine), 1.5f);
        penFlow.DashStyle = DashStyle.Solid;

        float waveOffset = (float)Math.Sin(_flowWavePhase) * 3f;

        // Flow Line 1 (Lower-Left towards Upper-Right, suggesting Income -> Growth)
        var p1A = new PointF(50, Height - 70);
        var p1Ctrl1 = new PointF(190 + waveOffset, Height - 110);
        var p1Ctrl2 = new PointF(360 - waveOffset, 150);
        var p1B = new PointF(Width - 50, 75);
        g.DrawBezier(penFlow, p1A, p1Ctrl1, p1Ctrl2, p1B);

        // Flow Line 2 (Secondary lower trajectory)
        var p2A = new PointF(35, Height - 100);
        var p2Ctrl1 = new PointF(160 - waveOffset, Height - 150);
        var p2Ctrl2 = new PointF(410 + waveOffset, 120);
        var p2B = new PointF(Width - 65, 45);
        g.DrawBezier(penFlow, p2A, p2Ctrl1, p2Ctrl2, p2B);

        // Flow Line 3 (Lower accent branch)
        var p3A = new PointF(75, Height - 45);
        var p3Ctrl1 = new PointF(230 + waveOffset, Height - 80);
        var p3Ctrl2 = new PointF(390 - waveOffset, 185);
        var p3B = new PointF(Width - 90, 110);
        g.DrawBezier(penFlow, p3A, p3Ctrl1, p3Ctrl2, p3B);

        // Tiny Square Data Points along the flow lines
        int dotAlpha = Math.Clamp((int)(80 * _fadeAlpha), 0, 255);
        using var brushDot = new SolidBrush(Color.FromArgb(dotAlpha, ColorSecondaryBlue));

        PointF[] dataPoints =
        {
            new(130, Height - 95 + waveOffset),
            new(245, Height - 145 - waveOffset),
            new(395, 160 + waveOffset),
            new(490, 105 - waveOffset),
            new(545, 82 + waveOffset)
        };

        foreach (var pt in dataPoints)
        {
            g.FillRectangle(brushDot, pt.X - 2.5f, pt.Y - 2.5f, 5f, 5f);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  3. CENTER MONEY FLOW LOGO & LOGO SHADOW
    // ═══════════════════════════════════════════════════════════════
    private void DrawCenterLogo(Graphics g)
    {
        int logoAlpha = Math.Clamp((int)(255 * _fadeAlpha), 0, 255);
        if (logoAlpha <= 0) return;

        // Logo Center: X = 320, Base Y = 86 (with slide offset)
        float centerX = Width / 2f;
        float logoTop = 86f - _logoSlideOffset;
        int logoSize = 64;
        float x = centerX - (logoSize / 2f);
        float y = logoTop;

        // A. Small subtle soft shadow directly under the logo
        int shadowAlpha = Math.Clamp((int)(32 * _fadeAlpha), 0, 255);
        using (var shadowBrush = new SolidBrush(Color.FromArgb(shadowAlpha, 15, 23, 42)))
        {
            g.FillEllipse(shadowBrush, x + 8, y + logoSize - 2, logoSize - 16, 7);
        }

        // B. Official Money Flow Logo from Resources (app_preview.png)
        var logoImage = ExecLedgerIcons.GetAppLogo(logoSize, logoSize);
        if (logoImage != null)
        {
            if (logoAlpha >= 254)
            {
                g.DrawImage(logoImage, (int)x, (int)y, logoSize, logoSize);
            }
            else
            {
                var cm = new System.Drawing.Imaging.ColorMatrix
                {
                    Matrix33 = logoAlpha / 255f
                };
                using var ia = new System.Drawing.Imaging.ImageAttributes();
                ia.SetColorMatrix(cm);
                g.DrawImage(
                    logoImage,
                    new Rectangle((int)x, (int)y, logoSize, logoSize),
                    0, 0, logoSize, logoSize,
                    GraphicsUnit.Pixel,
                    ia);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  4. APPLICATION NAME ("Money Flow")
    // ═══════════════════════════════════════════════════════════════
    private void DrawApplicationTitle(Graphics g)
    {
        int titleAlpha = Math.Clamp((int)(255 * _fadeAlpha), 0, 255);
        if (titleAlpha <= 0) return;

        using var brushTitle = new SolidBrush(Color.FromArgb(titleAlpha, ColorTitleText));

        const string appTitle = "Money Flow";
        var textSize = g.MeasureString(appTitle, _fontTitle);

        float textX = (Width - textSize.Width) / 2f;
        float textY = 164f - (_logoSlideOffset * 0.5f);

        g.DrawString(appTitle, _fontTitle, brushTitle, textX, textY);
    }

    // ═══════════════════════════════════════════════════════════════
    //  5. SHARP DESKTOP PROCESS BAR (320px x 6px, 0px border-radius)
    // ═══════════════════════════════════════════════════════════════
    private void DrawProgressBar(Graphics g)
    {
        int barAlpha = Math.Clamp((int)(255 * _fadeAlpha), 0, 255);
        if (barAlpha <= 0) return;

        int barX = (Width - ProgressBarWidth) / 2;
        int barY = 214;

        // A. Track: #E5EEFF with 1px border #CBD5E1
        using (var brushTrack = new SolidBrush(Color.FromArgb(barAlpha, ColorTrackBg)))
        using (var penBorder = new Pen(Color.FromArgb(barAlpha, ColorBorder), 1))
        {
            var trackRect = new Rectangle(barX, barY, ProgressBarWidth, ProgressBarHeight);
            g.FillRectangle(brushTrack, trackRect);
            g.DrawRectangle(penBorder, trackRect);
        }

        // B. Active Progress Fill: #1B365D with #2E5B88 Accent Lead Edge
        int fillWidth = (int)Math.Round(ProgressBarWidth * Math.Clamp(_displayProgress, 0f, 1f));
        if (fillWidth > 0)
        {
            using var brushProgress = new SolidBrush(Color.FromArgb(barAlpha, ColorPrimaryNavy));
            g.FillRectangle(brushProgress, barX, barY, fillWidth, ProgressBarHeight);

            // Leading edge accent tip (4px)
            if (fillWidth >= 4)
            {
                using var brushLead = new SolidBrush(Color.FromArgb(barAlpha, ColorSecondaryBlue));
                g.FillRectangle(brushLead, barX + fillWidth - 4, barY, 4, ProgressBarHeight);
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  6. LOADING STATUS TEXT
    // ═══════════════════════════════════════════════════════════════
    private void DrawLoadingStatus(Graphics g)
    {
        int statusAlpha = Math.Clamp((int)(255 * _fadeAlpha), 0, 255);
        if (statusAlpha <= 0) return;

        using var brushStatus = new SolidBrush(Color.FromArgb(statusAlpha, ColorMutedText));

        var statusSize = g.MeasureString(_currentStatus, _fontStatus);
        float statusX = (Width - statusSize.Width) / 2f;
        float statusY = 228f;

        g.DrawString(_currentStatus, _fontStatus, brushStatus, statusX, statusY);
    }

    // ═══════════════════════════════════════════════════════════════
    //  7. BOTTOM BRANDING & VERSION INFORMATION
    // ═══════════════════════════════════════════════════════════════
    private void DrawBottomInfo(Graphics g)
    {
        int infoAlpha = Math.Clamp((int)(200 * _fadeAlpha), 0, 255);
        if (infoAlpha <= 0) return;

        using var brushVersion = new SolidBrush(Color.FromArgb(infoAlpha, ColorMutedText));

        const string bottomText = "Money Flow • Version 1.0.0";
        var textSize = g.MeasureString(bottomText, _fontVersion);
        float textX = (Width - textSize.Width) / 2f;
        float textY = Height - 28f;

        g.DrawString(bottomText, _fontVersion, brushVersion, textX, textY);
    }

    // ═══════════════════════════════════════════════════════════════
    //  FONT FACTORY WITH FALLBACK
    // ═══════════════════════════════════════════════════════════════
    private static Font CreateArimoFont(float size, FontStyle style)
    {
        try
        {
            using var test = new FontFamily("Arimo");
            return new Font("Arimo", size, style, GraphicsUnit.Pixel);
        }
        catch
        {
            return new Font("Segoe UI", size, style, GraphicsUnit.Pixel);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animTimer.Dispose();
            _fontTitle.Dispose();
            _fontStatus.Dispose();
            _fontVersion.Dispose();
            _fontLedgerSymbols.Dispose();
        }
        base.Dispose(disposing);
    }
}
