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

    /// <summary>Stylized circular MoneyFlow monogram for title bar badge</summary>
    public static Bitmap CreateAppLogoIcon(Color color)
    {
        var bmp = new Bitmap(18, 18);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var pen = new Pen(color, 1.4f);

        g.DrawEllipse(pen, 1.5f, 1.5f, 15f, 15f);

        using var mPen = new Pen(color, 1.6f);
        g.DrawLines(mPen, new[] {
            new PointF(5f, 12.5f),
            new PointF(5f, 6f),
            new PointF(9f, 9.5f),
            new PointF(13f, 6f),
            new PointF(13f, 12.5f)
        });
        return bmp;
    }
}
