using System;
using System.Drawing;
using System.Windows.Forms;

namespace MoneyFlow.Desktop.Styling;

public enum AppTheme
{
    ClassicTeal,
    DarkSlate,
    LightNeutral
}

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

public static class ThemeManager
{
    public static AppTheme CurrentTheme { get; private set; } = AppTheme.ClassicTeal;
    public static string CurrentDensity { get; private set; } = "Compact";

    private static readonly ThemeColors ClassicTealColors = new()
    {
        HeaderBg = Color.FromArgb(24, 43, 73),
        HeaderFg = Color.White,
        AccentColor = Color.FromArgb(212, 160, 23),
        WindowBg = Color.FromArgb(240, 243, 246),
        SurfaceBg = Color.White,
        SurfaceFg = Color.FromArgb(33, 37, 41),
        BorderColor = Color.FromArgb(206, 212, 218),
        GridHeaderBg = Color.FromArgb(233, 236, 239),
        GridHeaderFg = Color.FromArgb(33, 37, 41),
        GridRowAltBg = Color.FromArgb(248, 249, 250),
        GridSelectionBg = Color.FromArgb(13, 110, 253),
        GridSelectionFg = Color.White,
        SuccessBg = Color.FromArgb(212, 237, 218),
        SuccessFg = Color.FromArgb(21, 87, 36),
        DangerBg = Color.FromArgb(248, 215, 218),
        DangerFg = Color.FromArgb(114, 28, 36)
    };

    private static readonly ThemeColors DarkSlateColors = new()
    {
        HeaderBg = Color.FromArgb(15, 23, 42),
        HeaderFg = Color.FromArgb(248, 250, 252),
        AccentColor = Color.FromArgb(56, 189, 248),
        WindowBg = Color.FromArgb(30, 41, 59),
        SurfaceBg = Color.FromArgb(51, 65, 85),
        SurfaceFg = Color.FromArgb(241, 245, 249),
        BorderColor = Color.FromArgb(71, 85, 105),
        GridHeaderBg = Color.FromArgb(15, 23, 42),
        GridHeaderFg = Color.FromArgb(226, 232, 240),
        GridRowAltBg = Color.FromArgb(40, 53, 72),
        GridSelectionBg = Color.FromArgb(2, 132, 199),
        GridSelectionFg = Color.White,
        SuccessBg = Color.FromArgb(6, 78, 59),
        SuccessFg = Color.FromArgb(167, 243, 208),
        DangerBg = Color.FromArgb(127, 29, 29),
        DangerFg = Color.FromArgb(254, 202, 202)
    };

    private static readonly ThemeColors LightNeutralColors = new()
    {
        HeaderBg = Color.FromArgb(55, 65, 81),
        HeaderFg = Color.White,
        AccentColor = Color.FromArgb(37, 99, 235),
        WindowBg = Color.FromArgb(248, 250, 252),
        SurfaceBg = Color.White,
        SurfaceFg = Color.FromArgb(17, 24, 39),
        BorderColor = Color.FromArgb(229, 231, 235),
        GridHeaderBg = Color.FromArgb(243, 244, 246),
        GridHeaderFg = Color.FromArgb(31, 41, 55),
        GridRowAltBg = Color.FromArgb(249, 250, 251),
        GridSelectionBg = Color.FromArgb(37, 99, 235),
        GridSelectionFg = Color.White,
        SuccessBg = Color.FromArgb(220, 252, 231),
        SuccessFg = Color.FromArgb(22, 101, 52),
        DangerBg = Color.FromArgb(254, 226, 226),
        DangerFg = Color.FromArgb(153, 27, 27)
    };

    public static ThemeColors Colors => CurrentTheme switch
    {
        AppTheme.DarkSlate => DarkSlateColors,
        AppTheme.LightNeutral => LightNeutralColors,
        _ => ClassicTealColors
    };

    public static void SetTheme(string? themeName, string? density = null)
    {
        CurrentTheme = themeName switch
        {
            "DarkSlate" => AppTheme.DarkSlate,
            "LightNeutral" => AppTheme.LightNeutral,
            _ => AppTheme.ClassicTeal
        };

        if (!string.IsNullOrWhiteSpace(density))
        {
            CurrentDensity = density;
        }
    }

    public static void ApplyTheme(Form form)
    {
        form.BackColor = Colors.WindowBg;
        form.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);

        ApplyToControls(form.Controls);
    }

    public static void ApplyToControls(Control.ControlCollection controls)
    {
        var colors = Colors;
        foreach (Control ctrl in controls)
        {
            if (ctrl is MenuStrip menu)
            {
                menu.BackColor = colors.HeaderBg;
                menu.ForeColor = colors.HeaderFg;
                menu.RenderMode = ToolStripRenderMode.System;
            }
            else if (ctrl is StatusStrip status)
            {
                status.BackColor = colors.HeaderBg;
                status.ForeColor = colors.HeaderFg;
                status.RenderMode = ToolStripRenderMode.System;
            }
            else if (ctrl is ToolStrip toolStrip)
            {
                toolStrip.BackColor = colors.GridHeaderBg;
                toolStrip.ForeColor = colors.SurfaceFg;
                toolStrip.GripStyle = ToolStripGripStyle.Hidden;
            }
            else if (ctrl is DataGridView grid)
            {
                StyleGrid(grid);
            }
            else if (ctrl is Button btn)
            {
                StyleButton(btn);
            }
            else if (ctrl is GroupBox grp)
            {
                grp.ForeColor = colors.SurfaceFg;
            }

            if (ctrl.HasChildren)
            {
                ApplyToControls(ctrl.Controls);
            }
        }
    }

    public static void StyleGrid(DataGridView grid, string? density = null)
    {
        var colors = Colors;
        var mode = density ?? CurrentDensity;
        int rowHeight = mode.Equals("Comfortable", StringComparison.OrdinalIgnoreCase) ? 32 : 24;
        int headerHeight = mode.Equals("Comfortable", StringComparison.OrdinalIgnoreCase) ? 36 : 28;
        float fontSize = mode.Equals("Comfortable", StringComparison.OrdinalIgnoreCase) ? 9.5F : 9F;

        grid.BackgroundColor = colors.SurfaceBg;
        grid.GridColor = colors.BorderColor;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        grid.EnableHeadersVisualStyles = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AllowUserToResizeRows = false;

        // Header style
        grid.ColumnHeadersHeight = headerHeight;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = colors.GridHeaderBg,
            ForeColor = colors.GridHeaderFg,
            Font = new Font("Segoe UI", fontSize, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(4)
        };

        // Default cell style
        grid.RowTemplate.Height = rowHeight;
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = colors.SurfaceBg,
            ForeColor = colors.SurfaceFg,
            Font = new Font("Segoe UI", fontSize, FontStyle.Regular),
            SelectionBackColor = colors.GridSelectionBg,
            SelectionForeColor = colors.GridSelectionFg,
            Padding = new Padding(4, 2, 4, 2)
        };

        // Alternating row style for readability
        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = colors.GridRowAltBg,
            ForeColor = colors.SurfaceFg,
            Font = new Font("Segoe UI", fontSize, FontStyle.Regular),
            SelectionBackColor = colors.GridSelectionBg,
            SelectionForeColor = colors.GridSelectionFg,
            Padding = new Padding(4, 2, 4, 2)
        };
    }

    public static void StyleButton(Button btn, bool isPrimary = false)
    {
        var colors = Colors;
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderSize = 1;
        btn.Cursor = Cursors.Hand;
        btn.Font = new Font("Segoe UI", 9F, isPrimary ? FontStyle.Bold : FontStyle.Regular);

        if (isPrimary)
        {
            btn.BackColor = colors.HeaderBg;
            btn.ForeColor = colors.HeaderFg;
            btn.FlatAppearance.BorderColor = colors.HeaderBg;
        }
        else
        {
            btn.BackColor = colors.SurfaceBg;
            btn.ForeColor = colors.SurfaceFg;
            btn.FlatAppearance.BorderColor = colors.BorderColor;
        }
    }
}
