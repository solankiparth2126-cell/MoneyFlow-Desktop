using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Serilog;

namespace MoneyFlow.Desktop.Styling;

/// <summary>
/// Display layout tier for desktop accounting interface adaptation.
/// </summary>
public enum LayoutTier
{
    /// <summary>Small displays (e.g. 1280x720, 1366x768 at 125% DPI). Compact vertical margins.</summary>
    Compact,
    /// <summary>Standard desktop displays (e.g. 1366x768 at 100%, 1920x1080 at 125%/150%). Balanced layout.</summary>
    Standard,
    /// <summary>High-resolution displays (e.g. 1920x1080 at 100%, 2560x1440, 4K). Generous desktop canvas.</summary>
    Large
}

/// <summary>
/// Current monitor and display metrics snapshot.
/// </summary>
public sealed class DisplayMetrics
{
    public string MonitorDeviceName { get; init; } = "";
    public bool IsPrimary { get; init; }
    public Rectangle ScreenBounds { get; init; }
    public Rectangle WorkArea { get; init; }
    public int Dpi { get; init; }
    public float ScaleFactor { get; init; }
    public int ScalePercentage => (int)Math.Round(ScaleFactor * 100);
    public Size WindowSize { get; init; }
    public Size ClientSize { get; init; }
    public LayoutTier Tier { get; init; }

    public override string ToString() =>
        $"Monitor={MonitorDeviceName} (Primary={IsPrimary}), Resolution={ScreenBounds.Width}x{ScreenBounds.Height}, " +
        $"WorkArea={WorkArea.Width}x{WorkArea.Height}, DPI={Dpi} ({ScalePercentage}%), Tier={Tier}, Window={WindowSize.Width}x{WindowSize.Height}";
}

/// <summary>
/// Native Windows screen fitting, usable work area detection, and Per-Monitor V2 DPI management.
/// Ensures borderless desktop ERP windows strictly respect the Windows Taskbar and multi-monitor setups.
/// </summary>
public static class ScreenFittingManager
{
    public const int WM_GETMINMAXINFO = 0x0024;
    public const int WM_DPICHANGED = 0x02E0;
    public const int WM_MOVE = 0x0003;
    public const int WM_DISPLAYCHANGE = 0x007E;

    private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
        public POINT(int x, int y) { X = x; Y = y; }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    /// <summary>
    /// Accurately extracts the current monitor metrics and scaling for the given form.
    /// </summary>
    public static DisplayMetrics GetDisplayMetrics(Form form)
    {
        var screen = (form.IsHandleCreated ? Screen.FromHandle(form.Handle) : null) ?? Screen.PrimaryScreen ?? Screen.AllScreens[0];
        int dpi = form.DeviceDpi;
        if (dpi <= 0) dpi = 96;
        float scaleFactor = dpi / 96.0f;

        var client = form.ClientSize;
        var tier = ClassifyTier(client.Width, client.Height);

        return new DisplayMetrics
        {
            MonitorDeviceName = screen.DeviceName,
            IsPrimary = screen.Primary,
            ScreenBounds = screen.Bounds,
            WorkArea = screen.WorkingArea,
            Dpi = dpi,
            ScaleFactor = scaleFactor,
            WindowSize = form.Size,
            ClientSize = client,
            Tier = tier
        };
    }

    /// <summary>
    /// Classifies screen client area into compact, standard, or large tier.
    /// </summary>
    public static LayoutTier ClassifyTier(int clientWidth, int clientHeight)
    {
        if (clientHeight < 630 || clientWidth < 1200)
            return LayoutTier.Compact;
        if (clientHeight < 860)
            return LayoutTier.Standard;
        return LayoutTier.Large;
    }

    /// <summary>
    /// Intercepts WM_GETMINMAXINFO to guarantee borderless maximization snaps strictly to
    /// the usable work area of the window's current monitor, preventing bleed under the Taskbar.
    /// </summary>
    public static bool HandleGetMinMaxInfo(IntPtr hWnd, IntPtr lParam, int minTrackWidth = 800, int minTrackHeight = 460)
    {
        try
        {
            IntPtr hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
            if (hMonitor == IntPtr.Zero) return false;

            var mi = new MONITORINFO();
            mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(hMonitor, ref mi)) return false;

            var mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO))!;

            // Clamp maximized bounds strictly to the monitor's work area (excluding Taskbar)
            mmi.ptMaxPosition.X = Math.Abs(mi.rcWork.Left - mi.rcMonitor.Left);
            mmi.ptMaxPosition.Y = Math.Abs(mi.rcWork.Top - mi.rcMonitor.Top);
            mmi.ptMaxSize.X = Math.Abs(mi.rcWork.Right - mi.rcWork.Left);
            mmi.ptMaxSize.Y = Math.Abs(mi.rcWork.Bottom - mi.rcWork.Top);

            // Minimum tracking size to prevent form collapsing
            mmi.ptMinTrackSize.X = minTrackWidth;
            mmi.ptMinTrackSize.Y = minTrackHeight;

            Marshal.StructureToPtr(mmi, lParam, true);
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed in HandleGetMinMaxInfo Win32 calculation.");
            return false;
        }
    }

    /// <summary>
    /// Configures the form bounds on startup or monitor change to safely fit within the work area.
    /// </summary>
    public static void InitializeWindowPlacement(Form form, bool startMaximized = true)
    {
        var screen = (form.IsHandleCreated ? Screen.FromHandle(form.Handle) : null) ?? Screen.PrimaryScreen ?? Screen.AllScreens[0];
        var workArea = screen.WorkingArea;

        // Set safe minimum size that will never push controls off screen on 720p at 125%
        int safeMinW = Math.Max(100, Math.Min(880, workArea.Width - 20));
        int safeMinH = Math.Max(100, Math.Min(480, workArea.Height - 20));
        form.MinimumSize = new Size(safeMinW, safeMinH);

        if (startMaximized)
        {
            form.WindowState = FormWindowState.Maximized;
        }
        else
        {
            // Restore within usable work area with a safe 24px margin
            int targetW = Math.Clamp((int)(workArea.Width * 0.94f), safeMinW, workArea.Width);
            int targetH = Math.Clamp((int)(workArea.Height * 0.94f), safeMinH, workArea.Height);
            int targetX = workArea.X + (workArea.Width - targetW) / 2;
            int targetY = workArea.Y + (workArea.Height - targetH) / 2;

            form.StartPosition = FormStartPosition.Manual;
            form.Bounds = new Rectangle(targetX, targetY, targetW, targetH);
        }

        var metrics = GetDisplayMetrics(form);
        Log.Information("Window initialized on display: {Metrics}", metrics);
    }
}
