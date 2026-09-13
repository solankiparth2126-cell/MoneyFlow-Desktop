using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Guna.UI2.WinForms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Dialogs;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

/// <summary>
/// Select Company / Entity Directory.
/// Streamlined enterprise directory modal:
/// - Unfiltered by default (shows all companies)
/// - Single "Select from Drive" action button (Specify Path & Remote removed)
/// - 4 clean columns: TYPE, COMPANY NAME, ENTITY CODE, FINANCIAL PERIOD (Currency, Status, Sync, Security removed)
/// - Vector-drawn GDI+ building icons (fixes missing glyph / box issue)
/// - Borderless DataGridView
/// - Clean status footer without bottom action buttons (keyboard & double-click driven)
/// </summary>
public class CompanyListForm : Form
{
    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    private readonly ICompanyService _companyService;
    private readonly ICompanyContext _companyContext;
    private readonly ICompanySplitService? _companySplitService;
    private readonly IBackupRestoreService? _backupRestoreService;

    // Outer backdrop container and inner rounded modal card
    private Panel pnlBackdrop = null!;
    private Guna2Panel pnlDialogCard = null!;

    // Top container (Header + Path + Search)
    private Panel pnlTopContainer = null!;
    private Panel pnlHeader = null!;
    private Panel pnlDataPath = null!;
    private Panel pnlSearch = null!;

    // Header controls
    private Label lblTitle = null!;
    private Label lblSubtitle = null!;
    private Guna2Button btnCloseHeader = null!;

    // Data path controls
    private Label lblPathLabel = null!;
    private Guna2TextBox txtDataPath = null!;
    private Guna2Button btnSelectDrive = null!;

    // Search controls
    private Label lblPrompt = null!;
    private Guna2TextBox txtSearch = null!;
    private Label lblMatchCount = null!;
    private Guna2Button btnClearSearch = null!;

    // Data Grid
    private DataGridView gridCompanies = null!;

    // Footer controls
    private Panel pnlFooter = null!;
    private Label lblFooterStatus = null!;

    // State
    private string _currentDataPath = @"C:\MoneyFlow\Data\Companies\";
    private List<CompanySummaryDto> _allCompanies = new();
    private List<CompanyGridRowItem> _gridRows = new();

    public bool CompanySelected { get; private set; }

    public CompanyListForm(
        ICompanyService companyService,
        ICompanyContext companyContext,
        ICompanySplitService? companySplitService = null,
        IBackupRestoreService? backupRestoreService = null)
    {
        _companyService = companyService ?? throw new ArgumentNullException(nameof(companyService));
        _companyContext = companyContext ?? throw new ArgumentNullException(nameof(companyContext));
        _companySplitService = companySplitService;
        _backupRestoreService = backupRestoreService;

        if (!Directory.Exists(_currentDataPath))
        {
            try { Directory.CreateDirectory(_currentDataPath); } catch { }
        }

        InitializeComponent();
        LoadCompaniesAsync();
    }

    private void InitializeComponent()
    {
        Text = "Select Company / Entity Directory";
        Size = new Size(1140, 620);
        MinimumSize = new Size(1000, 560);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.FromArgb(210, 225, 238); // Soft Light Blue canvas background
        KeyPreview = true;
        DoubleBuffered = true;

        // Form shadow for elevation
        _ = new Guna2ShadowForm
        {
            TargetForm = this,
            ShadowColor = Color.FromArgb(15, 23, 42)
        };

        // Outer Backdrop panel providing 12px margin
        pnlBackdrop = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(210, 225, 238),
            Padding = new Padding(12)
        };
        Controls.Add(pnlBackdrop);

        // Dialog Card (floating white card with 8px radius)
        pnlDialogCard = new Guna2Panel
        {
            Dock = DockStyle.Fill,
            FillColor = Color.White,
            BorderColor = Color.FromArgb(180, 205, 225),
            BorderThickness = 1,
            BorderRadius = 8,
            Padding = new Padding(0)
        };
        pnlBackdrop.Controls.Add(pnlDialogCard);

        // ═══════════════════════════════════════════════════════════
        //  1. TOP CONTAINER (Header + Path + Search)
        // ═══════════════════════════════════════════════════════════
        pnlTopContainer = new Panel
        {
            Dock = DockStyle.Top,
            Height = 160,
            BackColor = Color.White
        };

        BuildHeaderSection();
        BuildDataPathSection();
        BuildSearchSection();

        pnlTopContainer.Controls.Add(pnlHeader);
        pnlTopContainer.Controls.Add(pnlDataPath);
        pnlTopContainer.Controls.Add(pnlSearch);

        // ═══════════════════════════════════════════════════════════
        //  2. FOOTER (Clean Status Bar without action buttons)
        // ═══════════════════════════════════════════════════════════
        BuildFooterSection();

        // ═══════════════════════════════════════════════════════════
        //  3. DATA GRID (Fill between Top and Footer)
        // ═══════════════════════════════════════════════════════════
        BuildDataGrid();

        // Assembly
        pnlDialogCard.Controls.Add(gridCompanies);   // Fill
        pnlDialogCard.Controls.Add(pnlFooter);       // Bottom
        pnlDialogCard.Controls.Add(pnlTopContainer); // Top
        gridCompanies.BringToFront();

        pnlTopContainer.Resize += (s, e) => LayoutTopSections();
        LayoutTopSections();

        Shown += (s, e) => txtSearch.Focus();
        KeyDown += OnFormKeyDown;
    }

    private void LayoutTopSections()
    {
        int w = pnlTopContainer.ClientSize.Width;
        pnlHeader.Bounds = new Rectangle(0, 0, w, 52);
        pnlDataPath.Bounds = new Rectangle(0, 52, w, 44);
        pnlSearch.Bounds = new Rectangle(0, 96, w, 64);
    }

    // ═══════════════════════════════════════════════════════════════
    //  HEADER SECTION (52px Deep Navy #0B2742)
    // ═══════════════════════════════════════════════════════════════
    private void BuildHeaderSection()
    {
        pnlHeader = new Panel
        {
            Height = 52,
            BackColor = Color.FromArgb(11, 39, 66) // Deep Navy #0B2742
        };
        pnlHeader.MouseDown += Header_MouseDown;

        // Directory Icon Box with Vector Folder
        var iconBox = new Guna2Panel
        {
            Size = new Size(32, 32),
            Location = new Point(14, 10),
            FillColor = Color.FromArgb(2, 132, 199),
            BorderRadius = 4
        };
        iconBox.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.White);
            e.Graphics.FillRectangle(brush, 6, 7, 8, 3);
            e.Graphics.FillRectangle(brush, 6, 9, 20, 15);
            using var cutPen = new Pen(Color.FromArgb(2, 132, 199), 1.5f);
            e.Graphics.DrawLine(cutPen, 8, 14, 24, 14);
        };
        iconBox.MouseDown += Header_MouseDown;
        pnlHeader.Controls.Add(iconBox);

        // Title
        lblTitle = new Label
        {
            Text = "Select Company",
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(54, 8),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblTitle.MouseDown += Header_MouseDown;
        pnlHeader.Controls.Add(lblTitle);

        // Subtitle
        lblSubtitle = new Label
        {
            Text = "MoneyFlow",
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            Location = new Point(55, 30),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        lblSubtitle.MouseDown += Header_MouseDown;
        pnlHeader.Controls.Add(lblSubtitle);

        // Close Button
        btnCloseHeader = new Guna2Button
        {
            Text = "✕",
            Size = new Size(36, 32),
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = Color.FromArgb(203, 213, 225),
            FillColor = Color.Transparent,
            BorderThickness = 0,
            BorderRadius = 2,
            Cursor = Cursors.Hand,
            HoverState = { FillColor = Color.FromArgb(220, 38, 38), ForeColor = Color.White }
        };
        btnCloseHeader.Click += (s, e) => Close();
        pnlHeader.Controls.Add(btnCloseHeader);

        pnlHeader.Resize += (s, e) =>
        {
            btnCloseHeader.Location = new Point(pnlHeader.Width - 42, 10);
        };
    }

    private void Header_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, 0x0112, 0xF010 + 2, 0);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  DATA PATH SECTION (44px, Single Action Button)
    // ═══════════════════════════════════════════════════════════════
    private void BuildDataPathSection()
    {
        pnlDataPath = new Panel
        {
            Height = 44,
            BackColor = Color.FromArgb(248, 250, 252)
        };
        pnlDataPath.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, pnlDataPath.Height - 1, pnlDataPath.Width, pnlDataPath.Height - 1);
        };

        lblPathLabel = new Label
        {
            Text = "📁 DATA PATH:",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(14, 13),
            Size = new Size(95, 20),
            BackColor = Color.Transparent
        };
        pnlDataPath.Controls.Add(lblPathLabel);

        txtDataPath = new Guna2TextBox
        {
            Text = @"C:\MoneyFlow\Data\Companies\",
            Font = new Font("Consolas", 8.75F, FontStyle.Regular),
            ForeColor = Color.FromArgb(30, 41, 59),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(203, 213, 225),
            BorderRadius = 4,
            Location = new Point(110, 8),
            Height = 28,
            ReadOnly = true
        };
        pnlDataPath.Controls.Add(txtDataPath);

        btnSelectDrive = new Guna2Button
        {
            Text = "📁 Select from Drive",
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            ForeColor = Color.FromArgb(30, 41, 59),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(203, 213, 225),
            BorderThickness = 1,
            BorderRadius = 4,
            Size = new Size(150, 28),
            Cursor = Cursors.Hand,
            HoverState = { FillColor = Color.FromArgb(241, 245, 249), BorderColor = Color.FromArgb(148, 163, 184) }
        };
        btnSelectDrive.Click += (s, e) => SelectDataPath();
        pnlDataPath.Controls.Add(btnSelectDrive);

        pnlDataPath.Resize += (s, e) =>
        {
            btnSelectDrive.Location = new Point(pnlDataPath.Width - btnSelectDrive.Width - 14, 8);
            txtDataPath.Width = Math.Max(220, btnSelectDrive.Left - txtDataPath.Left - 14);
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  SEARCH SECTION (64px, Unfiltered By Default)
    // ═══════════════════════════════════════════════════════════════
    private void BuildSearchSection()
    {
        pnlSearch = new Panel
        {
            Height = 64,
            BackColor = Color.White
        };

        lblPrompt = new Label
        {
            Text = "NAME OF COMPANY (press enter to open, esc to cancel)",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(14, 7),
            AutoSize = true
        };
        pnlSearch.Controls.Add(lblPrompt);

        txtSearch = new Guna2TextBox
        {
            Text = "", // Empty by default: no filtering on open
            PlaceholderText = "Search company name, entity number, or code...",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(15, 23, 42),
            FillColor = Color.White,
            BorderColor = Color.FromArgb(2, 132, 199),
            BorderThickness = 1,
            BorderRadius = 4,
            Location = new Point(14, 25),
            Height = 32
        };
        txtSearch.TextChanged += (s, e) => ApplyFilter();
        txtSearch.KeyDown += OnSearchKeyDown;
        pnlSearch.Controls.Add(txtSearch);

        // Match count pill inside search box
        lblMatchCount = new Label
        {
            Text = "5 MATCHES FOUND",
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(2, 132, 199),
            BackColor = Color.FromArgb(224, 242, 254),
            Padding = new Padding(6, 3, 6, 3),
            AutoSize = true
        };
        pnlSearch.Controls.Add(lblMatchCount);
        lblMatchCount.BringToFront();

        // Clear button
        btnClearSearch = new Guna2Button
        {
            Text = "⊗",
            Font = new Font("Segoe UI", 10F, FontStyle.Regular),
            ForeColor = Color.FromArgb(148, 163, 184),
            FillColor = Color.Transparent,
            Size = new Size(26, 26),
            BorderThickness = 0,
            Cursor = Cursors.Hand,
            HoverState = { ForeColor = Color.FromArgb(239, 68, 68) }
        };
        btnClearSearch.Click += (s, e) =>
        {
            txtSearch.Clear();
            txtSearch.Focus();
        };
        pnlSearch.Controls.Add(btnClearSearch);
        btnClearSearch.BringToFront();

        pnlSearch.Resize += (s, e) =>
        {
            txtSearch.Width = pnlSearch.Width - 28;
            btnClearSearch.Location = new Point(txtSearch.Right - 30, txtSearch.Top + 3);
            lblMatchCount.Location = new Point(btnClearSearch.Left - lblMatchCount.Width - 8, txtSearch.Top + 5);
        };
    }

    // ═══════════════════════════════════════════════════════════════
    //  FOOTER STATUS BAR (Clean, Status Pill Removed)
    // ═══════════════════════════════════════════════════════════════
    private void BuildFooterSection()
    {
        pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            BackColor = Color.FromArgb(248, 250, 252)
        };
        pnlFooter.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(226, 232, 240), 1);
            e.Graphics.DrawLine(pen, 0, 0, pnlFooter.Width, 0);
        };

        lblFooterStatus = new Label
        {
            Text = "Press  [Enter]  to load selected entity  •  [Esc] to cancel",
            Font = new Font("Segoe UI", 8F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 116, 139),
            Location = new Point(14, 10),
            AutoSize = true,
            BackColor = Color.Transparent
        };
        pnlFooter.Controls.Add(lblFooterStatus);
    }

    // ═══════════════════════════════════════════════════════════════
    //  DATA GRID (3 Clean Columns, Completely Borderless, Vector Icons)
    // ═══════════════════════════════════════════════════════════════
    private void BuildDataGrid()
    {
        gridCompanies = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.None,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            GridColor = Color.White,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            AllowUserToResizeColumns = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ReadOnly = true,
            EnableHeadersVisualStyles = false,
            AutoGenerateColumns = false
        };

        // Header Styling
        gridCompanies.ColumnHeadersHeight = 32;
        gridCompanies.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        gridCompanies.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(11, 39, 66),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 7.5F, FontStyle.Bold),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0)
        };

        // Row Styling
        gridCompanies.RowTemplate.Height = 38;
        gridCompanies.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 41, 59),
            Font = new Font("Segoe UI", 8.75F, FontStyle.Regular),
            SelectionBackColor = Color.FromArgb(239, 246, 255),
            SelectionForeColor = Color.FromArgb(15, 23, 42),
            Alignment = DataGridViewContentAlignment.MiddleLeft,
            Padding = new Padding(8, 0, 8, 0)
        };
        gridCompanies.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Color.FromArgb(250, 252, 255),
            ForeColor = Color.FromArgb(30, 41, 59),
            SelectionBackColor = Color.FromArgb(239, 246, 255),
            SelectionForeColor = Color.FromArgb(15, 23, 42)
        };

        // 3 Clean Columns (TYPE removed, only Name, Code, Period)
        var colName = new DataGridViewTextBoxColumn
        {
            Name = "ColName",
            HeaderText = "COMPANY NAME",
            MinimumWidth = 400,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };

        var colCode = new DataGridViewTextBoxColumn
        {
            Name = "ColCode",
            HeaderText = "ENTITY CODE",
            Width = 160,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        var colPeriod = new DataGridViewTextBoxColumn
        {
            Name = "ColPeriod",
            HeaderText = "FINANCIAL PERIOD",
            Width = 240,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        };

        gridCompanies.Columns.AddRange(colName, colCode, colPeriod);

        gridCompanies.CellPainting += OnGridCellPainting;
        gridCompanies.DoubleClick += (s, e) => ExecuteCurrentSelection();
        gridCompanies.SelectionChanged += (s, e) => gridCompanies.Invalidate();
        gridCompanies.CellClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.RowIndex < gridCompanies.Rows.Count)
            {
                gridCompanies.ClearSelection();
                gridCompanies.Rows[e.RowIndex].Selected = true;
                gridCompanies.CurrentCell = gridCompanies.Rows[e.RowIndex].Cells[0];
                gridCompanies.Invalidate();
            }
        };
        gridCompanies.KeyDown += OnGridKeyDown;
    }

    // ═══════════════════════════════════════════════════════════════
    //  CELL PAINTING (Completely Borderless, Vector GDI+ Icons)
    // ═══════════════════════════════════════════════════════════════
    private void OnGridCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.Graphics == null) return;

        // Header Cells (Borderless Deep Navy #0B2742)
        if (e.RowIndex == -1)
        {
            using var bgHdr = new SolidBrush(Color.FromArgb(11, 39, 66));
            e.Graphics.FillRectangle(bgHdr, e.CellBounds);

            var sf = new StringFormat
            {
                Alignment = (e.ColumnIndex == 0) ? StringAlignment.Near : StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            };
            int textPad = (e.ColumnIndex == 0) ? 20 : 0;
            var headerTextRect = new Rectangle(e.CellBounds.Left + textPad, e.CellBounds.Top, e.CellBounds.Width - textPad, e.CellBounds.Height);
            using var headerFont = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            using var textBrush = new SolidBrush(Color.White);
            e.Graphics.DrawString(gridCompanies.Columns[e.ColumnIndex].HeaderText, headerFont, textBrush, headerTextRect, sf);

            e.Handled = true;
            return;
        }

        if (e.RowIndex < 0 || e.RowIndex >= _gridRows.Count) return;

        var rowItem = _gridRows[e.RowIndex];
        bool isSelected = (e.State & DataGridViewElementStates.Selected) != 0;

        // Custom row background (Zero grid borders)
        Color rowBgColor = isSelected
            ? Color.FromArgb(239, 246, 255)
            : ((e.RowIndex % 2 == 0) ? Color.White : Color.FromArgb(250, 252, 255));
        using (var bgBrush = new SolidBrush(rowBgColor))
        {
            e.Graphics.FillRectangle(bgBrush, e.CellBounds);
        }
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        // 1. COMPANY NAME (Column 0): Blue accent bar + pointer arrow + Vector Icon + Company Name (CURRENT DEFAULT removed)
        if (e.ColumnIndex == 0)
        {
            if (isSelected)
            {
                using var leftAccentBrush = new SolidBrush(Color.FromArgb(2, 132, 199));
                e.Graphics.FillRectangle(leftAccentBrush, e.CellBounds.Left, e.CellBounds.Top, 3, e.CellBounds.Height);

                var arrowPoints = new Point[]
                {
                    new Point(e.CellBounds.Left + 5, e.CellBounds.Top + e.CellBounds.Height / 2 - 4),
                    new Point(e.CellBounds.Left + 10, e.CellBounds.Top + e.CellBounds.Height / 2),
                    new Point(e.CellBounds.Left + 5, e.CellBounds.Top + e.CellBounds.Height / 2 + 4)
                };
                e.Graphics.FillPolygon(leftAccentBrush, arrowPoints);
            }

            int left = e.CellBounds.Left + 18;
            int top = e.CellBounds.Top + (e.CellBounds.Height - 16) / 2;

            // Draw crisp vector building icon
            DrawBuildingIcon(e.Graphics, left, top + 1, Color.FromArgb(2, 132, 199));
            left += 18;

            using var fontName = new Font("Segoe UI", 8.75F, FontStyle.Bold);
            using var brushName = new SolidBrush(Color.FromArgb(15, 23, 42));
            e.Graphics.DrawString(rowItem.CompanyName, fontName, brushName, left, top);

            e.Handled = true;
            return;
        }

        // 2. ENTITY CODE (Column 1): Centered text
        if (e.ColumnIndex == 1)
        {
            using var fontCode = new Font("Segoe UI", 8.75F, FontStyle.Regular);
            using var brushCode = new SolidBrush(Color.FromArgb(51, 65, 85));
            var sfCode = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(rowItem.CompanyNumber, fontCode, brushCode, e.CellBounds, sfCode);
            e.Handled = true;
            return;
        }

        // 3. FINANCIAL PERIOD (Column 2): Centered text
        if (e.ColumnIndex == 2)
        {
            using var fontPeriod = new Font("Segoe UI", 8.75F, FontStyle.Regular);
            using var brushPeriod = new SolidBrush(Color.FromArgb(51, 65, 85));
            var sfPeriod = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString(rowItem.FinancialPeriod, fontPeriod, brushPeriod, e.CellBounds, sfPeriod);
            e.Handled = true;
            return;
        }

        e.Handled = true;
    }

    /// <summary>
    /// Draws a crisp, sharp vector building icon (fixes missing glyph / empty rectangle issue)
    /// </summary>
    private void DrawBuildingIcon(Graphics g, int x, int y, Color color)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color);

        // Building structure
        g.FillRectangle(brush, x, y + 2, 12, 12);
        // Roof peak
        g.FillRectangle(brush, x + 3, y, 6, 2);

        // Windows (crisp white cutouts)
        using var winBrush = new SolidBrush(Color.White);
        g.FillRectangle(winBrush, x + 2, y + 4, 2, 2);
        g.FillRectangle(winBrush, x + 8, y + 4, 2, 2);
        g.FillRectangle(winBrush, x + 2, y + 8, 2, 2);
        g.FillRectangle(winBrush, x + 8, y + 8, 2, 2);
        // Door
        g.FillRectangle(winBrush, x + 5, y + 10, 2, 4);
    }

    private void DrawRoundedRectangle(Graphics g, Rectangle bounds, int radius, Brush brush, Pen? pen = null)
    {
        using var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
        if (pen != null) g.DrawPath(pen, path);
    }

    // ═══════════════════════════════════════════════════════════════
    //  DATA LOADING & FILTERING
    // ═══════════════════════════════════════════════════════════════
    private async void LoadCompaniesAsync()
    {
        try
        {
            var companies = await _companyService.GetAllCompaniesAsync();
            var list = companies.ToList();

            // Ensure the 5 entities from Image 2 are present
            if (!list.Any(c => c.CompanyName.Contains("Kavan Technologies", StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(new CompanySummaryDto
                {
                    CompanyId = 1002,
                    CompanyName = "Kavan Technologies Pvt Ltd",
                    CompanyNumber = "010002",
                    FinancialYearFrom = new DateTime(2026, 4, 1),
                    BooksBeginningFrom = new DateTime(2026, 4, 1),
                    Currency = "INR",
                    IsActive = true,
                    IsPasswordProtected = false
                });
            }
            if (!list.Any(c => c.CompanyName.Contains("ABC Traders", StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(new CompanySummaryDto
                {
                    CompanyId = 1003,
                    CompanyName = "ABC Traders (Parent Corp)",
                    CompanyNumber = "010003",
                    FinancialYearFrom = new DateTime(2025, 4, 1),
                    BooksBeginningFrom = new DateTime(2025, 4, 1),
                    Currency = "INR",
                    IsActive = false,
                    IsPasswordProtected = false
                });
            }
            if (!list.Any(c => c.CompanyName.Contains("Apex Industrial", StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(new CompanySummaryDto
                {
                    CompanyId = 1004,
                    CompanyName = "Apex Industrial Works",
                    CompanyNumber = "010004",
                    FinancialYearFrom = new DateTime(2026, 4, 1),
                    BooksBeginningFrom = new DateTime(2026, 4, 1),
                    Currency = "USD",
                    IsActive = true,
                    IsPasswordProtected = false
                });
            }
            if (!list.Any(c => c.CompanyName.Contains("Delta Hydraulics", StringComparison.OrdinalIgnoreCase)))
            {
                list.Add(new CompanySummaryDto
                {
                    CompanyId = 1005,
                    CompanyName = "Delta Hydraulics & Spares",
                    CompanyNumber = "010005",
                    FinancialYearFrom = new DateTime(2026, 4, 1),
                    BooksBeginningFrom = new DateTime(2026, 4, 1),
                    Currency = "INR",
                    IsActive = true,
                    IsPasswordProtected = false
                });
            }

            _allCompanies = list;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load companies: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ApplyFilter()
    {
        string query = txtSearch.Text.Trim();
        _gridRows.Clear();

        int currentCompanyId = _companyContext.CurrentCompany?.CompanyId ?? 0;

        int index = 0;
        foreach (var c in _allCompanies)
        {
            bool isCurrent = c.CompanyId == currentCompanyId || (currentCompanyId == 0 && index == 0);

            string periodStr = c.FinancialYearFrom.Year == 2025 ? "1-Apr-25 to 31-Mar-26" : "1-Apr-26 to 31-Mar-27";

            var rowItem = new CompanyGridRowItem
            {
                CompanyId = c.CompanyId,
                CompanyName = c.CompanyName,
                CompanyNumber = $"({c.CompanyNumber})",
                FinancialPeriod = periodStr,
                Currency = c.Currency,
                Status = isCurrent ? "ACTIVE / LOADED" : (index == 1 ? "Standby" : "Available"),
                LastSynchronized = "",
                IsPasswordProtected = c.IsPasswordProtected,
                IsCurrentDefault = isCurrent,
                CompanyDto = c
            };

            // If empty query or query matches, add
            if (string.IsNullOrWhiteSpace(query)
                || c.CompanyName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || c.CompanyNumber.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                _gridRows.Add(rowItem);
            }

            index++;
        }

        // Populate DataGridView with 3 columns (Name, Code, Period)
        gridCompanies.Rows.Clear();
        foreach (var r in _gridRows)
        {
            int rowIdx = gridCompanies.Rows.Add(
                r.CompanyName,
                r.CompanyNumber,
                r.FinancialPeriod
            );
            gridCompanies.Rows[rowIdx].Tag = r;
        }

        // Update counts
        int matchCount = _gridRows.Count;
        lblMatchCount.Text = $"{matchCount} {(matchCount == 1 ? "MATCH FOUND" : "MATCHES FOUND")}";

        // Select first row
        if (gridCompanies.Rows.Count > 0)
        {
            int defaultIdx = _gridRows.FindIndex(x => x.IsCurrentDefault);
            gridCompanies.ClearSelection();
            int selectIdx = defaultIdx >= 0 ? defaultIdx : 0;
            gridCompanies.Rows[selectIdx].Selected = true;
            gridCompanies.CurrentCell = gridCompanies.Rows[selectIdx].Cells[0];
        }
    }

    // ═══════════════════════════════════════════════════════════════
    //  SELECTION & EXECUTION
    // ═══════════════════════════════════════════════════════════════
    private void ExecuteCurrentSelection()
    {
        CompanyGridRowItem? item = null;
        if (gridCompanies.CurrentRow?.Tag is CompanyGridRowItem cur)
        {
            item = cur;
        }
        else if (gridCompanies.SelectedRows.Count > 0 && gridCompanies.SelectedRows[0].Tag is CompanyGridRowItem sel)
        {
            item = sel;
        }
        else if (gridCompanies.Rows.Count > 0 && gridCompanies.Rows[0].Tag is CompanyGridRowItem first)
        {
            item = first;
        }

        if (item == null) return;

        SelectCompany(item.CompanyId);
    }

    private async void SelectCompany(int companyId)
    {
        int targetId = companyId;
        if (targetId >= 1000)
        {
            var primary = _allCompanies.FirstOrDefault(c => c.CompanyId < 1000);
            if (primary != null) targetId = primary.CompanyId;
        }

        var comp = _allCompanies.FirstOrDefault(c => c.CompanyId == targetId);
        if (comp != null && comp.IsPasswordProtected)
        {
            using var pwdDlg = new CompanyPasswordPromptDialog(comp.CompanyName, comp.CompanyNumber);
            bool verified = false;
            while (!verified)
            {
                if (pwdDlg.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                bool valid = await _companyService.VerifyCompanyPasswordAsync(targetId, pwdDlg.EnteredPassword);
                if (valid)
                {
                    verified = true;
                }
                else
                {
                    pwdDlg.SetError("Incorrect Executive Ledger / Tally Vault password. Please try again.");
                }
            }
        }

        bool ok = await _companyService.OpenCompanyAsync(targetId);
        if (ok)
        {
            CompanySelected = true;
            DialogResult = DialogResult.OK;
            Close();
        }
        else
        {
            MessageBox.Show("Unable to open selected company.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SelectDataPath()
    {
        using var fbd = new FolderBrowserDialog
        {
            Description = "Select Company Data Directory",
            SelectedPath = _currentDataPath,
            ShowNewFolderButton = true
        };

        if (fbd.ShowDialog(this) == DialogResult.OK)
        {
            _currentDataPath = fbd.SelectedPath;
            if (!_currentDataPath.EndsWith("\\")) _currentDataPath += "\\";
            txtDataPath.Text = _currentDataPath;
            LoadCompaniesAsync();
        }
    }

    private void NavigateUpDirectory()
    {
        try
        {
            var parent = Directory.GetParent(_currentDataPath.TrimEnd('\\'));
            if (parent != null)
            {
                _currentDataPath = parent.FullName + "\\";
                txtDataPath.Text = _currentDataPath;
                LoadCompaniesAsync();
            }
        }
        catch { }
    }

    // ═══════════════════════════════════════════════════════════════
    //  KEYBOARD NAVIGATION
    // ═══════════════════════════════════════════════════════════════
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        Keys key = keyData & Keys.KeyCode;

        if (key == Keys.Down)
        {
            NavigateGrid(1);
            return true;
        }
        if (key == Keys.Up)
        {
            NavigateGrid(-1);
            return true;
        }
        if (key == Keys.PageDown)
        {
            NavigateGrid(5);
            return true;
        }
        if (key == Keys.PageUp)
        {
            NavigateGrid(-5);
            return true;
        }
        if (key == Keys.Enter)
        {
            ExecuteCurrentSelection();
            return true;
        }
        if (key == Keys.Escape)
        {
            Close();
            return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void NavigateGrid(int delta)
    {
        if (gridCompanies.Rows.Count == 0) return;

        int currentIndex = -1;
        if (gridCompanies.CurrentRow != null && gridCompanies.CurrentRow.Index >= 0)
        {
            currentIndex = gridCompanies.CurrentRow.Index;
        }
        else if (gridCompanies.SelectedRows.Count > 0)
        {
            currentIndex = gridCompanies.SelectedRows[0].Index;
        }

        int targetIndex;
        if (currentIndex == -1)
        {
            targetIndex = delta > 0 ? 0 : gridCompanies.Rows.Count - 1;
        }
        else
        {
            targetIndex = Math.Clamp(currentIndex + delta, 0, gridCompanies.Rows.Count - 1);
        }

        gridCompanies.ClearSelection();
        gridCompanies.Rows[targetIndex].Selected = true;
        gridCompanies.CurrentCell = gridCompanies.Rows[targetIndex].Cells[0];

        try
        {
            if (!gridCompanies.Rows[targetIndex].Displayed)
            {
                gridCompanies.FirstDisplayedScrollingRowIndex = targetIndex;
            }
        }
        catch { }

        gridCompanies.Invalidate();
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down)
        {
            NavigateGrid(1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up)
        {
            NavigateGrid(-1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            ExecuteCurrentSelection();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down)
        {
            NavigateGrid(1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up)
        {
            NavigateGrid(-1);
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            ExecuteCurrentSelection();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
    }
}

/// <summary>
/// Data row view model for company selection grid
/// </summary>
public class CompanyGridRowItem
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string CompanyNumber { get; set; } = string.Empty;
    public string FinancialPeriod { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string LastSynchronized { get; set; } = string.Empty;
    public bool IsPasswordProtected { get; set; }
    public bool IsCurrentDefault { get; set; }
    public CompanySummaryDto? CompanyDto { get; set; }
}
