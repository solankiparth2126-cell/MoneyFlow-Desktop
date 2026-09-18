using System.Drawing;
using System.Drawing.Drawing2D;

namespace MoneyFlow.Desktop.Styling;

/// <summary>
/// High-resolution vector icon generator for toolbar actions, title badges, and indicators.
/// Uses GDI+ anti-aliased geometry to guarantee razor-sharp rendering on all Windows displays.
/// </summary>
public static class ExecLedgerIcons
{
    public static GraphicsPath CreateRoundedRectanglePath(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float diameter = radius * 2f;
        path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
        path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
        path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>Building / Bank icon with pediment triangle roof and 3 columns</summary>
    public static Bitmap CreateCompanyIcon(Color color)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var pen = new Pen(color, 1.4f);

        // Pediment roof
        g.DrawPolygon(pen, new[] { new PointF(8f, 2f), new PointF(2.5f, 6f), new PointF(13.5f, 6f) });
        // Roof beam
        g.DrawLine(pen, 2f, 6.5f, 14f, 6.5f);
        // 3 Columns
        g.DrawLine(pen, 4.5f, 7.5f, 4.5f, 11.5f);
        g.DrawLine(pen, 8f, 7.5f, 8f, 11.5f);
        g.DrawLine(pen, 11.5f, 7.5f, 11.5f, 11.5f);
        // Base
        g.DrawLine(pen, 2f, 12.5f, 14f, 12.5f);
        g.DrawLine(pen, 1f, 14f, 15f, 14f);
        return bmp;
    }

    /// <summary>Calendar icon with binder rings and grid indicator dots</summary>
    public static Bitmap CreateCalendarIcon(Color color)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var pen = new Pen(color, 1.3f);
        using var brush = new SolidBrush(color);

        var rect = new RectangleF(2f, 3.5f, 12f, 10.5f);
        using var path = CreateRoundedRectanglePath(rect, 2f);
        g.DrawPath(pen, path);

        // Header bar
        g.DrawLine(pen, 2f, 7f, 14f, 7f);
        // Rings
        g.DrawLine(pen, 5f, 1.5f, 5f, 4f);
        g.DrawLine(pen, 11f, 1.5f, 11f, 4f);
        // 3 date dots
        g.FillEllipse(brush, 4.5f, 9f, 1.8f, 1.8f);
        g.FillEllipse(brush, 7.5f, 9f, 1.8f, 1.8f);
        g.FillEllipse(brush, 10.5f, 9f, 1.8f, 1.8f);
        return bmp;
    }

    /// <summary>Day book document with folded dog-ear corner and lines</summary>
    public static Bitmap CreateDayBookIcon(Color color)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var pen = new Pen(color, 1.3f);

        g.DrawLines(pen, new[] {
            new PointF(9.5f, 2f),
            new PointF(3f, 2f),
            new PointF(3f, 14f),
            new PointF(13f, 14f),
            new PointF(13f, 5.5f),
            new PointF(9.5f, 2f),
            new PointF(9.5f, 5.5f),
            new PointF(13f, 5.5f)
        });

        g.DrawLine(pen, 5.5f, 8f, 10.5f, 8f);
        g.DrawLine(pen, 5.5f, 11f, 9.5f, 11f);
        return bmp;
    }

    /// <summary>Trial balance spreadsheet/table icon with columns</summary>
    public static Bitmap CreateTrialBalanceIcon(Color color)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var pen = new Pen(color, 1.3f);

        var rect = new RectangleF(2f, 3f, 12f, 10.5f);
        using var path = CreateRoundedRectanglePath(rect, 2f);
        g.DrawPath(pen, path);

        g.DrawLine(pen, 2f, 6.5f, 14f, 6.5f);
        g.DrawLine(pen, 8f, 6.5f, 8f, 13.5f);
        g.DrawLine(pen, 2f, 10f, 14f, 10f);
        return bmp;
    }

    /// <summary>Profit & Loss ascending 3-bar chart</summary>
    public static Bitmap CreateProfitLossIcon(Color color)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var pen = new Pen(color, 1.3f);
        using var brush = new SolidBrush(color);

        g.DrawLine(pen, 2f, 14f, 14f, 14f);
        g.FillRectangle(brush, 3.5f, 9f, 2.5f, 4.5f);
        g.FillRectangle(brush, 7f, 6f, 2.5f, 7.5f);
        g.FillRectangle(brush, 10.5f, 3f, 2.5f, 10.5f);
        return bmp;
    }

    /// <summary>Balance sheet checklist/notebook icon</summary>
    public static Bitmap CreateBalanceSheetIcon(Color color)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var pen = new Pen(color, 1.3f);

        var rect = new RectangleF(2.5f, 2.5f, 11f, 11.5f);
        using var path = CreateRoundedRectanglePath(rect, 2f);
        g.DrawPath(pen, path);

        using var checkPen = new Pen(color, 1.5f);
        g.DrawLines(checkPen, new[] {
            new PointF(5f, 8.5f),
            new PointF(7f, 10.5f),
            new PointF(11f, 6f)
        });
        return bmp;
    }

    /// <summary>Magnifying glass search icon</summary>
    public static Bitmap CreateSearchIcon(Color color)
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var pen = new Pen(color, 1.5f);

        g.DrawEllipse(pen, 2.5f, 2.5f, 7f, 7f);
        g.DrawLine(pen, 8.5f, 8.5f, 13.5f, 13.5f);
        return bmp;
    }

    private static Icon? _cachedAppIcon;
    private static readonly System.Collections.Generic.Dictionary<int, Bitmap> _cachedLogos = new();

    /// <summary>Resolves the canonical app.ico icon from Resources in the whole project</summary>
    public static Icon? GetAppIcon()
    {
        if (_cachedAppIcon != null) return _cachedAppIcon;

        try
        {
            string[] candidatePaths = {
                System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico"),
                System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", "app.ico"),
                System.IO.Path.Combine(System.Environment.CurrentDirectory, "Resources", "app.ico")
            };

            foreach (var path in candidatePaths)
            {
                if (System.IO.File.Exists(path))
                {
                    _cachedAppIcon = new Icon(path);
                    return _cachedAppIcon;
                }
            }

            if (!string.IsNullOrEmpty(System.Windows.Forms.Application.ExecutablePath) && System.IO.File.Exists(System.Windows.Forms.Application.ExecutablePath))
            {
                _cachedAppIcon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
                if (_cachedAppIcon != null) return _cachedAppIcon;
            }
        }
        catch { }

        return null;
    }

    /// <summary>Returns the official MoneyFlow application logo at desired resolution from Resources/app_preview.png or Resources/app.ico</summary>
    public static Image GetAppLogo(int width = 24, int height = 24)
    {
        int key = (width << 16) | (height & 0xFFFF);
        if (_cachedLogos.TryGetValue(key, out var cached))
        {
            return cached;
        }

        try
        {
            string[] candidatePaths = {
                System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "app_preview.png"),
                System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Resources", "app_preview.png"),
                System.IO.Path.Combine(System.Environment.CurrentDirectory, "Resources", "app_preview.png")
            };

            foreach (var path in candidatePaths)
            {
                if (System.IO.File.Exists(path))
                {
                    using var src = new Bitmap(path);
                    var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using var g = Graphics.FromImage(bmp);
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.Clear(Color.Transparent);
                    g.DrawImage(src, new Rectangle(0, 0, width, height));
                    _cachedLogos[key] = bmp;
                    return bmp;
                }
            }

            var icon = GetAppIcon();
            if (icon != null)
            {
                using var iconBmp = icon.ToBitmap();
                var bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(bmp);
                g.SmoothingMode = SmoothingMode.HighQuality;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.CompositingQuality = CompositingQuality.HighQuality;
                g.Clear(Color.Transparent);
                g.DrawImage(iconBmp, new Rectangle(0, 0, width, height));
                _cachedLogos[key] = bmp;
                return bmp;
            }
        }
        catch { }

        // Fallback drawing if resources missing
        var fallbackBmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(fallbackBmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new LinearGradientBrush(new Point(0, 0), new Point(width, height), Color.FromArgb(168, 85, 247), Color.FromArgb(100, 64, 217));
            using var pen = new Pen(brush, Math.Max(2f, width / 8f)) { EndCap = LineCap.ArrowAnchor };
            g.DrawBezier(pen, new Point(width * 3 / 20, height * 14 / 20), new Point(width * 4 / 20, height * 7 / 20), new Point(width * 14 / 20, height * 13 / 20), new Point(width * 16 / 20, height * 5 / 20));
        }
        _cachedLogos[key] = fallbackBmp;
        return fallbackBmp;
    }

    /// <summary>Purple growth-arrow application logo for title bar and headers</summary>
    public static Bitmap CreateAppLogoIcon(Color? color = null)
    {
        return (Bitmap)GetAppLogo(20, 20);
    }
}
