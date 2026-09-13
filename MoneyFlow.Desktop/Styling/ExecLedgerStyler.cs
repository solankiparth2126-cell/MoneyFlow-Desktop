using System;
using System.Drawing;
using System.Windows.Forms;
using Guna.UI2.WinForms;

namespace MoneyFlow.Desktop.Styling;

/// <summary>
/// Utility class that applies the Executive Ledger theme to Guna.UI2 and standard WinForms controls.
/// Centralizes all styling logic so individual forms don't hardcode colors/fonts.
/// </summary>
public static class ExecLedgerStyler
{
    // ═══════════════════════════════════════════════════════════════
    //  FORM-LEVEL STYLING
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Apply Executive Ledger base styling to a Form.</summary>
    public static void ApplyFormBase(Form form)
    {
        form.BackColor = ExecLedgerTheme.ApplicationCanvas;
        form.Font = ExecLedgerTheme.UIRegular9;
        form.ForeColor = ExecLedgerTheme.PrimaryText;
    }

    /// <summary>Apply styling to a child form (voucher entry, report, master list, etc.).</summary>
    public static void ApplyChildForm(Form form, string title)
    {
        ApplyFormBase(form);
        form.Text = $"Executive Ledger Desktop — {title}";
        form.StartPosition = FormStartPosition.CenterParent;
        form.WindowState = FormWindowState.Maximized;
        form.KeyPreview = true;
    }

    /// <summary>Apply dialog styling (fixed size, no maximize).</summary>
    public static void ApplyDialog(Form dialog, string title, int width, int height)
    {
        dialog.BackColor = ExecLedgerTheme.WorkSurface;
        dialog.Font = ExecLedgerTheme.UIRegular9;
        dialog.ForeColor = ExecLedgerTheme.PrimaryText;
        dialog.Text = title;
        dialog.Size = new Size(width, height);
        dialog.StartPosition = FormStartPosition.CenterParent;
        dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
        dialog.MaximizeBox = false;
        dialog.MinimizeBox = false;
        dialog.KeyPreview = true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  GUNA2 DATAGRIDVIEW STYLING
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Apply the Executive Ledger accounting grid style to a Guna2DataGridView.</summary>
    public static void StyleGrid(Guna2DataGridView grid, bool dense = false)
    {
        int rowHeight = dense ? ExecLedgerTheme.DenseGridRow : ExecLedgerTheme.StandardGridRow;

        // Core grid properties
        grid.BackgroundColor = ExecLedgerTheme.GridSurface;
        grid.GridColor = ExecLedgerTheme.GridBorder;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.EnableHeadersVisualStyles = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AllowUserToResizeRows = false;
        grid.AllowUserToAddRows = false;
        grid.ReadOnly = true;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        // Guna2 specific — sharp geometry
        grid.ThemeStyle.HeaderStyle.Height = ExecLedgerTheme.GridHeaderHeight;
        grid.ThemeStyle.HeaderStyle.BackColor = ExecLedgerTheme.ApplicationCanvas;
        grid.ThemeStyle.HeaderStyle.ForeColor = ExecLedgerTheme.PrimaryText;
        grid.ThemeStyle.HeaderStyle.Font = ExecLedgerTheme.GridHeaderFont;
        grid.ThemeStyle.HeaderStyle.BorderStyle = DataGridViewHeaderBorderStyle.Single;

        grid.ThemeStyle.RowsStyle.BackColor = ExecLedgerTheme.WorkSurface;
        grid.ThemeStyle.RowsStyle.ForeColor = ExecLedgerTheme.PrimaryText;
        grid.ThemeStyle.RowsStyle.Font = ExecLedgerTheme.UIRegular9;
        grid.ThemeStyle.RowsStyle.Height = rowHeight;
        grid.ThemeStyle.RowsStyle.BorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ThemeStyle.RowsStyle.SelectionBackColor = ExecLedgerTheme.PrimarySelection;
        grid.ThemeStyle.RowsStyle.SelectionForeColor = ExecLedgerTheme.PrimaryText;

        grid.ThemeStyle.AlternatingRowsStyle.BackColor = ExecLedgerTheme.SecondarySurface;
        grid.ThemeStyle.AlternatingRowsStyle.ForeColor = ExecLedgerTheme.PrimaryText;
        grid.ThemeStyle.AlternatingRowsStyle.Font = ExecLedgerTheme.UIRegular9;
        grid.ThemeStyle.AlternatingRowsStyle.SelectionBackColor = ExecLedgerTheme.PrimarySelection;
        grid.ThemeStyle.AlternatingRowsStyle.SelectionForeColor = ExecLedgerTheme.PrimaryText;

        grid.ThemeStyle.BackColor = ExecLedgerTheme.WorkSurface;
        grid.ThemeStyle.GridColor = ExecLedgerTheme.GridBorder;

        // Column headers
        grid.ColumnHeadersHeight = ExecLedgerTheme.GridHeaderHeight;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

        // Row template
        grid.RowTemplate.Height = rowHeight;
    }

    /// <summary>Apply the Executive Ledger style to a standard DataGridView (fallback).</summary>
    public static void StyleStandardGrid(DataGridView grid, bool dense = false)
    {
        int rowHeight = dense ? ExecLedgerTheme.DenseGridRow : ExecLedgerTheme.StandardGridRow;

        grid.BackgroundColor = ExecLedgerTheme.GridSurface;
        grid.GridColor = ExecLedgerTheme.GridBorder;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.EnableHeadersVisualStyles = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AllowUserToResizeRows = false;
        grid.AllowUserToAddRows = false;

        grid.ColumnHeadersHeight = ExecLedgerTheme.GridHeaderHeight;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = ExecLedgerTheme.ApplicationCanvas,
            ForeColor = ExecLedgerTheme.PrimaryText,
            Font = ExecLedgerTheme.GridHeaderFont,
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(4, 0, 4, 0)
        };

        grid.RowTemplate.Height = rowHeight;
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = ExecLedgerTheme.WorkSurface,
            ForeColor = ExecLedgerTheme.PrimaryText,
            Font = ExecLedgerTheme.UIRegular9,
            SelectionBackColor = ExecLedgerTheme.PrimarySelection,
            SelectionForeColor = ExecLedgerTheme.PrimaryText,
            Padding = new Padding(4, 2, 4, 2)
        };

        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = ExecLedgerTheme.SecondarySurface,
            ForeColor = ExecLedgerTheme.PrimaryText,
            Font = ExecLedgerTheme.UIRegular9,
            SelectionBackColor = ExecLedgerTheme.PrimarySelection,
            SelectionForeColor = ExecLedgerTheme.PrimaryText,
            Padding = new Padding(4, 2, 4, 2)
        };
    }

    /// <summary>Configure a DataGridView column for right-aligned financial values with mono font.</summary>
    public static void SetFinancialColumn(DataGridViewColumn column)
    {
        column.DefaultCellStyle.Font = ExecLedgerTheme.MonoRegular9;
        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        column.DefaultCellStyle.Padding = new Padding(4, 0, 8, 0);
    }

    /// <summary>Configure a DataGridView column for monospaced code/date values.</summary>
    public static void SetCodeColumn(DataGridViewColumn column)
    {
        column.DefaultCellStyle.Font = ExecLedgerTheme.MonoRegular9;
        column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
    }

    // ═══════════════════════════════════════════════════════════════
    //  GUNA2 BUTTON STYLING
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Style a Guna2Button as a primary action (Post, Save, Submit, etc.).</summary>
    public static void StylePrimaryButton(Guna2Button btn)
    {
        btn.FillColor = ExecLedgerTheme.PrimaryNavy;
        btn.ForeColor = ExecLedgerTheme.WhiteText;
        btn.BorderColor = ExecLedgerTheme.DeepNavy;
        btn.BorderThickness = 1;
        btn.BorderRadius = ExecLedgerTheme.BorderRadius;
        btn.Font = ExecLedgerTheme.UIBold9;
        btn.Size = new Size(Math.Max(btn.Width, 90), ExecLedgerTheme.ToolbarButtonHeight);
        btn.HoverState.FillColor = ExecLedgerTheme.ButtonHover;
        btn.HoverState.ForeColor = ExecLedgerTheme.WhiteText;
        btn.HoverState.BorderColor = ExecLedgerTheme.SteelBlue;
        btn.Cursor = Cursors.Hand;
    }

    /// <summary>Style a Guna2Button as a secondary/neutral action (Cancel, Close, etc.).</summary>
    public static void StyleSecondaryButton(Guna2Button btn)
    {
        btn.FillColor = ExecLedgerTheme.WorkSurface;
        btn.ForeColor = ExecLedgerTheme.PrimaryText;
        btn.BorderColor = ExecLedgerTheme.PrimaryBorder;
        btn.BorderThickness = 1;
        btn.BorderRadius = ExecLedgerTheme.BorderRadius;
        btn.Font = ExecLedgerTheme.UIRegular9;
        btn.Size = new Size(Math.Max(btn.Width, 85), ExecLedgerTheme.ToolbarButtonHeight);
        btn.HoverState.FillColor = ExecLedgerTheme.ApplicationCanvas;
        btn.HoverState.ForeColor = ExecLedgerTheme.PrimaryText;
        btn.HoverState.BorderColor = ExecLedgerTheme.MutedBorder;
        btn.Cursor = Cursors.Hand;
    }

    /// <summary>Style a Guna2Button as a destructive action (Delete, Reverse, etc.).</summary>
    public static void StyleDangerButton(Guna2Button btn)
    {
        btn.FillColor = ExecLedgerTheme.ErrorRed;
        btn.ForeColor = ExecLedgerTheme.WhiteText;
        btn.BorderColor = ExecLedgerTheme.ErrorRed;
        btn.BorderThickness = 1;
        btn.BorderRadius = ExecLedgerTheme.BorderRadius;
        btn.Font = ExecLedgerTheme.UIBold9;
        btn.Size = new Size(Math.Max(btn.Width, 85), ExecLedgerTheme.ToolbarButtonHeight);
        btn.HoverState.FillColor = Color.FromArgb(153, 27, 27);
        btn.HoverState.ForeColor = ExecLedgerTheme.WhiteText;
        btn.Cursor = Cursors.Hand;
    }

    /// <summary>Style a standard WinForms Button as primary.</summary>
    public static void StylePrimaryButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = ExecLedgerTheme.PrimaryNavy;
        btn.ForeColor = ExecLedgerTheme.WhiteText;
        btn.FlatAppearance.BorderColor = ExecLedgerTheme.DeepNavy;
        btn.FlatAppearance.BorderSize = 1;
        btn.Font = ExecLedgerTheme.UIBold9;
        btn.Height = ExecLedgerTheme.ToolbarButtonHeight;
        btn.Cursor = Cursors.Hand;
    }

    /// <summary>Style a standard WinForms Button as secondary.</summary>
    public static void StyleSecondaryButton(Button btn)
    {
        btn.FlatStyle = FlatStyle.Flat;
        btn.BackColor = ExecLedgerTheme.WorkSurface;
        btn.ForeColor = ExecLedgerTheme.PrimaryText;
        btn.FlatAppearance.BorderColor = ExecLedgerTheme.PrimaryBorder;
        btn.FlatAppearance.BorderSize = 1;
        btn.Font = ExecLedgerTheme.UIRegular9;
        btn.Height = ExecLedgerTheme.ToolbarButtonHeight;
        btn.Cursor = Cursors.Hand;
    }

    // ═══════════════════════════════════════════════════════════════
    //  GUNA2 TEXTBOX STYLING
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Style a Guna2TextBox with Executive Ledger styling.</summary>
    public static void StyleInput(Guna2TextBox txt)
    {
        txt.FillColor = ExecLedgerTheme.InputBg;
        txt.ForeColor = ExecLedgerTheme.PrimaryText;
        txt.BorderColor = ExecLedgerTheme.PrimaryBorder;
        txt.BorderThickness = 1;
        txt.BorderRadius = ExecLedgerTheme.BorderRadius;
        txt.Font = ExecLedgerTheme.UIRegular9;
        txt.Size = new Size(txt.Width, ExecLedgerTheme.InputHeight);
        txt.FocusedState.BorderColor = ExecLedgerTheme.InputFocusBorder;
    }

    /// <summary>Style a Guna2TextBox for financial/numeric entry (monospace, right-aligned).</summary>
    public static void StyleFinancialInput(Guna2TextBox txt)
    {
        StyleInput(txt);
        txt.Font = ExecLedgerTheme.MonoRegular9;
        txt.TextAlign = HorizontalAlignment.Right;
    }

    /// <summary>Style a Guna2TextBox as read-only.</summary>
    public static void StyleReadOnlyInput(Guna2TextBox txt)
    {
        StyleInput(txt);
        txt.FillColor = ExecLedgerTheme.InputReadOnlyBg;
        txt.ReadOnly = true;
    }

    // ═══════════════════════════════════════════════════════════════
    //  GUNA2 COMBOBOX STYLING
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Style a Guna2ComboBox.</summary>
    public static void StyleComboBox(Guna2ComboBox cmb)
    {
        cmb.FillColor = ExecLedgerTheme.InputBg;
        cmb.ForeColor = ExecLedgerTheme.PrimaryText;
        cmb.BorderColor = ExecLedgerTheme.PrimaryBorder;
        cmb.BorderThickness = 1;
        cmb.BorderRadius = ExecLedgerTheme.BorderRadius;
        cmb.Font = ExecLedgerTheme.UIRegular9;
        cmb.Size = new Size(cmb.Width, ExecLedgerTheme.InputHeight);
        cmb.FocusedState.BorderColor = ExecLedgerTheme.InputFocusBorder;
    }

    // ═══════════════════════════════════════════════════════════════
    //  GUNA2 CHECKBOX / RADIO STYLING
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Style a Guna2CheckBox.</summary>
    public static void StyleCheckBox(Guna2CheckBox chk)
    {
        chk.UncheckedState.FillColor = ExecLedgerTheme.WorkSurface;
        chk.UncheckedState.BorderColor = ExecLedgerTheme.MutedBorder;
        chk.UncheckedState.BorderThickness = 1;
        chk.UncheckedState.BorderRadius = 0;
        chk.CheckedState.FillColor = ExecLedgerTheme.PrimaryNavy;
        chk.CheckedState.BorderColor = ExecLedgerTheme.PrimaryNavy;
        chk.CheckedState.BorderThickness = 1;
        chk.CheckedState.BorderRadius = 0;
        chk.Font = ExecLedgerTheme.UIRegular9;
        chk.ForeColor = ExecLedgerTheme.PrimaryText;
    }

    /// <summary>Style a Guna2RadioButton.</summary>
    public static void StyleRadioButton(Guna2RadioButton radio)
    {
        radio.UncheckedState.FillColor = ExecLedgerTheme.WorkSurface;
        radio.UncheckedState.BorderColor = ExecLedgerTheme.MutedBorder;
        radio.UncheckedState.BorderThickness = 1;
        radio.CheckedState.FillColor = ExecLedgerTheme.PrimaryNavy;
        radio.CheckedState.BorderColor = ExecLedgerTheme.PrimaryNavy;
        radio.CheckedState.BorderThickness = 1;
        radio.Font = ExecLedgerTheme.UIRegular9;
        radio.ForeColor = ExecLedgerTheme.PrimaryText;
    }

    // ═══════════════════════════════════════════════════════════════
    //  GUNA2 PANEL STYLING
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Style a Guna2Panel as a workspace surface.</summary>
    public static void StylePanel(Guna2Panel panel, bool withBorder = true)
    {
        panel.FillColor = ExecLedgerTheme.WorkSurface;
        panel.BorderColor = withBorder ? ExecLedgerTheme.PrimaryBorder : Color.Transparent;
        panel.BorderThickness = withBorder ? 1 : 0;
        panel.BorderRadius = ExecLedgerTheme.BorderRadius;
    }

    /// <summary>Style a Guna2Panel as a section header.</summary>
    public static void StyleSectionHeader(Guna2Panel panel)
    {
        panel.FillColor = ExecLedgerTheme.ApplicationCanvas;
        panel.BorderColor = ExecLedgerTheme.PrimaryBorder;
        panel.BorderThickness = 1;
        panel.BorderRadius = ExecLedgerTheme.BorderRadius;
    }

    // ═══════════════════════════════════════════════════════════════
    //  GUNA2 SEPARATOR
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Create a horizontal separator line.</summary>
    public static Guna2Separator CreateSeparator()
    {
        return new Guna2Separator
        {
            FillColor = ExecLedgerTheme.PrimaryBorder,
            Height = 1,
            Dock = DockStyle.Top
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  GUNA2 PROGRESS BAR
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Style a Guna2ProgressBar.</summary>
    public static void StyleProgressBar(Guna2ProgressBar bar)
    {
        bar.FillColor = ExecLedgerTheme.ApplicationCanvas;
        bar.ProgressColor = ExecLedgerTheme.PrimaryNavy;
        bar.ProgressColor2 = ExecLedgerTheme.SteelBlue;
        bar.BorderRadius = ExecLedgerTheme.BorderRadius;
    }

    // ═══════════════════════════════════════════════════════════════
    //  MENUSTRIP STYLING (standard WinForms)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Style a MenuStrip with Executive Ledger appearance.</summary>
    public static void StyleMenuStrip(MenuStrip menu)
    {
        menu.BackColor = ExecLedgerTheme.ApplicationCanvas;
        menu.ForeColor = ExecLedgerTheme.PrimaryText;
        menu.Font = ExecLedgerTheme.MenuFont;
        menu.Height = ExecLedgerTheme.MenuBarHeight;
        menu.Padding = new Padding(4, 0, 0, 0);
        menu.RenderMode = ToolStripRenderMode.Professional;
        menu.Renderer = new ExecLedgerMenuRenderer();
    }

    /// <summary>Style a StatusStrip with Executive Ledger appearance.</summary>
    public static void StyleStatusStrip(StatusStrip status)
    {
        status.BackColor = ExecLedgerTheme.StatusBarBg;
        status.ForeColor = ExecLedgerTheme.PrimaryText;
        status.Font = ExecLedgerTheme.StatusBarFont;
        status.Height = ExecLedgerTheme.StatusBarHeight;
        status.SizingGrip = false;
        status.RenderMode = ToolStripRenderMode.Professional;
    }

    // ═══════════════════════════════════════════════════════════════
    //  BALANCE STATE PANEL
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Create a balance state indicator panel.</summary>
    public static Panel CreateBalanceIndicator(decimal totalDebit, decimal totalCredit)
    {
        var (text, fg, bg) = ExecLedgerTheme.FormatVariance(totalDebit, totalCredit);
        var isBalanced = totalDebit == totalCredit;

        var panel = new Panel
        {
            Height = ExecLedgerTheme.GridFooterHeight,
            BackColor = bg,
            Dock = DockStyle.Bottom,
            Padding = new Padding(8, 0, 8, 0)
        };

        var border = isBalanced ? ExecLedgerTheme.SuccessBorder : ExecLedgerTheme.ErrorBorder;
        panel.Paint += (s, e) =>
        {
            using var pen = new Pen(border, 1);
            e.Graphics.DrawLine(pen, 0, 0, panel.Width, 0);
        };

        var lbl = new Label
        {
            Text = text,
            ForeColor = fg,
            Font = ExecLedgerTheme.UIBold9,
            AutoSize = false,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        panel.Controls.Add(lbl);
        return panel;
    }

    // ═══════════════════════════════════════════════════════════════
    //  ACCOUNTING TOTALS FOOTER
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Create a double-rule accounting totals footer panel.</summary>
    public static Panel CreateTotalsFooter(decimal totalDebit, decimal totalCredit, int recordCount = 0)
    {
        var panel = new Panel
        {
            Height = ExecLedgerTheme.GridFooterHeight * 2,
            BackColor = ExecLedgerTheme.ApplicationCanvas,
            Dock = DockStyle.Bottom,
            Padding = new Padding(4, 0, 4, 0)
        };

        // Double-rule top border
        panel.Paint += (s, e) =>
        {
            using var borderPen = new Pen(ExecLedgerTheme.PrimaryBorder, 1);
            e.Graphics.DrawLine(borderPen, 0, 0, panel.Width, 0);
            e.Graphics.DrawLine(borderPen, 0, 3, panel.Width, 3);
        };

        var debitLabel = new Label
        {
            Text = $"TOTAL DEBIT    {ExecLedgerTheme.FormatCurrency(totalDebit)}",
            Font = ExecLedgerTheme.MonoBold9,
            ForeColor = ExecLedgerTheme.PrimaryText,
            Location = new Point(8, 8),
            AutoSize = true
        };

        var creditLabel = new Label
        {
            Text = $"TOTAL CREDIT   {ExecLedgerTheme.FormatCurrency(totalCredit)}",
            Font = ExecLedgerTheme.MonoBold9,
            ForeColor = ExecLedgerTheme.PrimaryText,
            Location = new Point(8, 28),
            AutoSize = true
        };

        var variance = totalDebit - totalCredit;
        var (varText, varColor) = ExecLedgerTheme.FormatBalance(variance);
        var varianceLabel = new Label
        {
            Text = $"VARIANCE       {varText}",
            Font = ExecLedgerTheme.MonoBold9,
            ForeColor = varColor,
            Location = new Point(350, 8),
            AutoSize = true
        };

        if (recordCount > 0)
        {
            var countLabel = new Label
            {
                Text = $"{recordCount} records",
                Font = ExecLedgerTheme.UIRegular8,
                ForeColor = ExecLedgerTheme.SecondaryText,
                Location = new Point(350, 28),
                AutoSize = true
            };
            panel.Controls.Add(countLabel);
        }

        panel.Controls.AddRange(new Control[] { debitLabel, creditLabel, varianceLabel });
        return panel;
    }

    // ═══════════════════════════════════════════════════════════════
    //  RECURSIVE CONTROL STYLING
    // ═══════════════════════════════════════════════════════════════

    /// <summary>Recursively apply theme to all controls in a container.</summary>
    public static void ApplyToAllControls(Control.ControlCollection controls)
    {
        foreach (Control ctrl in controls)
        {
            switch (ctrl)
            {
                case Guna2DataGridView g2Grid:
                    StyleGrid(g2Grid);
                    break;
                case DataGridView stdGrid:
                    StyleStandardGrid(stdGrid);
                    break;
                case Guna2Button g2Btn:
                    // Don't override if already styled
                    break;
                case Guna2TextBox g2Txt:
                    StyleInput(g2Txt);
                    break;
                case Guna2ComboBox g2Cmb:
                    StyleComboBox(g2Cmb);
                    break;
                case Guna2CheckBox g2Chk:
                    StyleCheckBox(g2Chk);
                    break;
                case Guna2Panel g2Panel:
                    // Don't override manually styled panels
                    break;
                case MenuStrip menuStrip:
                    StyleMenuStrip(menuStrip);
                    break;
                case StatusStrip statusStrip:
                    StyleStatusStrip(statusStrip);
                    break;
            }

            if (ctrl.HasChildren)
                ApplyToAllControls(ctrl.Controls);
        }
    }
}

/// <summary>
/// Custom menu renderer for Executive Ledger menu appearance.
/// Provides flat, sharp menus with proper hover states.
/// </summary>
internal class ExecLedgerMenuRenderer : ToolStripProfessionalRenderer
{
    public ExecLedgerMenuRenderer() : base(new ExecLedgerMenuColorTable()) { }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (e.ToolStrip is MenuStrip)
        {
            if (e.Item.Selected || e.Item.Pressed)
            {
                var rc = new Rectangle(2, 2, e.Item.Width - 4, e.Item.Height - 4);
                using var brush = new SolidBrush(Color.FromArgb(224, 242, 254)); // #E0F2FE Light Sky
                using var pen = new Pen(Color.FromArgb(186, 230, 253), 1);      // #BAE6FD
                using var path = ExecLedgerIcons.CreateRoundedRectanglePath(rc, 4f);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
                e.Graphics.DrawPath(pen, path);
            }
            return;
        }

        var dropRc = new Rectangle(Point.Empty, e.Item.Size);
        if (e.Item.Selected || e.Item.Pressed)
        {
            using var brush = new SolidBrush(ExecLedgerTheme.MenuHover);
            e.Graphics.FillRectangle(brush, dropRc);
        }
        else
        {
            using var brush = new SolidBrush(e.Item.Owner?.BackColor ?? ExecLedgerTheme.ApplicationCanvas);
            e.Graphics.FillRectangle(brush, dropRc);
        }
    }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(e.ToolStrip.BackColor);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        if (e.ToolStrip is MenuStrip)
        {
            using var pen = new Pen(ExecLedgerTheme.GridBorder, 1);
            e.Graphics.DrawLine(pen, 0, e.AffectedBounds.Height - 1, e.AffectedBounds.Width, e.AffectedBounds.Height - 1);
        }
        else
        {
            base.OnRenderToolStripBorder(e);
        }
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        var bounds = new Rectangle(Point.Empty, e.Item.Size);
        int y = bounds.Height / 2;
        using var pen = new Pen(ExecLedgerTheme.GridBorder, 1);
        e.Graphics.DrawLine(pen, 4, y, bounds.Width - 4, y);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        if (e.ToolStrip is MenuStrip && (e.Item.Selected || e.Item.Pressed))
        {
            e.TextColor = Color.FromArgb(2, 132, 199); // #0284C7 Sky Blue
        }
        else
        {
            e.TextColor = ExecLedgerTheme.PrimaryText;
        }
        e.TextFormat &= ~TextFormatFlags.HidePrefix;
        e.TextFormat &= ~TextFormatFlags.NoPrefix;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
        // No image margin background
    }
}

/// <summary>
/// Color table for the Executive Ledger menu system.
/// </summary>
internal class ExecLedgerMenuColorTable : ProfessionalColorTable
{
    public override Color MenuBorder => ExecLedgerTheme.PrimaryBorder;
    public override Color MenuItemBorder => Color.Transparent;
    public override Color MenuItemSelected => ExecLedgerTheme.MenuHover;
    public override Color MenuItemSelectedGradientBegin => ExecLedgerTheme.MenuHover;
    public override Color MenuItemSelectedGradientEnd => ExecLedgerTheme.MenuHover;
    public override Color MenuItemPressedGradientBegin => ExecLedgerTheme.PrimarySelection;
    public override Color MenuItemPressedGradientEnd => ExecLedgerTheme.PrimarySelection;
    public override Color MenuStripGradientBegin => ExecLedgerTheme.ApplicationCanvas;
    public override Color MenuStripGradientEnd => ExecLedgerTheme.ApplicationCanvas;
    public override Color ToolStripDropDownBackground => ExecLedgerTheme.WorkSurface;
    public override Color ImageMarginGradientBegin => ExecLedgerTheme.WorkSurface;
    public override Color ImageMarginGradientMiddle => ExecLedgerTheme.WorkSurface;
    public override Color ImageMarginGradientEnd => ExecLedgerTheme.WorkSurface;
    public override Color SeparatorDark => ExecLedgerTheme.GridBorder;
    public override Color SeparatorLight => ExecLedgerTheme.WorkSurface;
}
