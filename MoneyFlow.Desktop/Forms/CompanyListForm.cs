using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Desktop.Dialogs;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

public class CompanyListForm : Form
{
    private readonly ICompanyService _companyService;
    private readonly ICompanyContext _companyContext;
    private readonly ICompanySplitService? _companySplitService;

    // Controls
    private Panel pnlHeader = null!;
    private Panel pnlSubHeader = null!;
    private Panel pnlClient = null!;
    private Panel pnlLeftInfo = null!;
    private Panel pnlCenterCard = null!;
    private Panel pnlBottomBar = null!;

    private TextBox txtSearch = null!;
    private DoubleBufferedListBox lstCompanies = null!;
    private Label lblListCount = null!;

    private string _currentDataPath = @"C:\MoneyFlow\Data";
    private List<CompanySummaryDto> _allCompanies = new();
    private List<CompanyListItem> _filteredItems = new();

    public bool CompanySelected { get; private set; }

    public CompanyListForm(ICompanyService companyService, ICompanyContext companyContext, ICompanySplitService? companySplitService = null)
    {
        _companyService = companyService;
        _companyContext = companyContext;
        _companySplitService = companySplitService;

        if (!Directory.Exists(_currentDataPath))
        {
            try { Directory.CreateDirectory(_currentDataPath); } catch { }
        }

        InitializeTallyComponent();
        LoadCompaniesAsync();
    }

    private void InitializeTallyComponent()
    {
        this.Text = "MoneyFlow Prime — Select Company";
        this.Size = new Size(1100, 700);
        this.MinimumSize = new Size(950, 600);
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.None;
        this.BackColor = Color.FromArgb(204, 222, 237); // Tally soft cyan/slate
        this.Font = new Font("Segoe UI", 9.5F);
        this.KeyPreview = true;

        // 1. Top Ribbon (Tally Prime Navy Header)
        pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            BackColor = Color.FromArgb(0, 56, 101)
        };
        BuildTopRibbon(pnlHeader);
        this.Controls.Add(pnlHeader);

        // 2. Sub-Header Bar (Deep Blue Secondary)
        pnlSubHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = Color.FromArgb(0, 75, 135)
        };
        BuildSubHeader(pnlSubHeader);
        this.Controls.Add(pnlSubHeader);

        // 3. Bottom Status Bar (Tally Quick Status)
        pnlBottomBar = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            BackColor = Color.FromArgb(228, 238, 246)
        };
        BuildBottomBar(pnlBottomBar);
        this.Controls.Add(pnlBottomBar);

        // 4. Main Client Canvas
        pnlClient = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(204, 222, 237)
        };
        this.Controls.Add(pnlClient);

        // Left Info Panel (Current Period / Current Company)
        pnlLeftInfo = new Panel
        {
            Location = new Point(24, 20),
            Size = new Size(240, 200),
            BackColor = Color.Transparent
        };
        BuildLeftInfo(pnlLeftInfo);
        pnlClient.Controls.Add(pnlLeftInfo);

        // Center Modal Box (List of Companies)
        pnlCenterCard = new Panel
        {
            Size = new Size(680, 560),
            BackColor = Color.Transparent
        };
        BuildCenterCard(pnlCenterCard);
        pnlClient.Controls.Add(pnlCenterCard);

        pnlClient.Resize += (s, e) => PositionCenterCard();
        PositionCenterCard();

        // Keyboard Handlers
        this.KeyDown += OnFormKeyDown;
    }

    private void PositionCenterCard()
    {
        if (pnlCenterCard == null) return;
        int x = Math.Max(260, (pnlClient.ClientSize.Width - pnlCenterCard.Width) / 2);
        int y = Math.Max(10, (pnlClient.ClientSize.Height - pnlCenterCard.Height) / 2 - 10);
        pnlCenterCard.Location = new Point(x, y);
    }

    private void BuildTopRibbon(Panel panel)
    {
        // Brand Title
        var lblLogo = new Label
        {
            Text = "MoneyFlow GOLD Prime",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(254, 194, 14), // Gold
            Location = new Point(14, 6),
            AutoSize = true
        };
        panel.Controls.Add(lblLogo);

        // Top Shortcuts
        var flowShortcuts = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 5, 10, 0),
            BackColor = Color.Transparent
        };

        AddHeaderShortcut(flowShortcuts, "K: Company", () => { });
        AddHeaderShortcut(flowShortcuts, "Y: Data", (btn) => ShowDataMenu(btn));
        AddHeaderShortcut(flowShortcuts, "Z: Exchange", () => { });
        AddHeaderShortcut(flowShortcuts, "G: Go To", () => { });
        AddHeaderShortcut(flowShortcuts, "O: Import", () => { });
        AddHeaderShortcut(flowShortcuts, "E: Export", () => { });
        AddHeaderShortcut(flowShortcuts, "M: Share", () => { });
        AddHeaderShortcut(flowShortcuts, "P: Print", () => { });
        AddHeaderShortcut(flowShortcuts, "F1: Help", () => { });

        // Close / Minimize Buttons
        var btnClose = new Button
        {
            Text = "✕",
            Size = new Size(30, 24),
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Margin = new Padding(10, 0, 0, 0)
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, e) => this.Close();
        flowShortcuts.Controls.Add(btnClose);

        panel.Controls.Add(flowShortcuts);
    }

    private void AddHeaderShortcut(FlowLayoutPanel flow, string text, Action onClick)
    {
        AddHeaderShortcut(flow, text, _ => onClick());
    }

    private void AddHeaderShortcut(FlowLayoutPanel flow, string text, Action<Control> onClick)
    {
        var btn = new Button
        {
            Text = text,
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat,
            AutoSize = true,
            Cursor = Cursors.Hand,
            Margin = new Padding(2, 0, 2, 0)
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (s, e) => onClick(btn);
        flow.Controls.Add(btn);
    }

    private void BuildSubHeader(Panel panel)
    {
        var lblSub = new Label
        {
            Text = "Select Company",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(14, 5),
            AutoSize = true
        };
        panel.Controls.Add(lblSub);

        string currentComp = _companyContext.CurrentCompany?.CompanyName ?? "No Company Selected";
        var lblCurrent = new Label
        {
            Text = currentComp,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(200, 225, 255),
            AutoSize = true
        };
        panel.Controls.Add(lblCurrent);

        panel.Resize += (s, e) =>
        {
            lblCurrent.Location = new Point((panel.Width - lblCurrent.Width) / 2, 5);
        };
    }

    private void BuildLeftInfo(Panel panel)
    {
        int y = 5;
        var lblH1 = new Label
        {
            Text = "CURRENT PERIOD",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(80, 110, 140),
            Location = new Point(0, y),
            AutoSize = true
        };
        panel.Controls.Add(lblH1);

        y += 18;
        string fyText = _companyContext.CurrentFinancialYear != null
            ? $"{_companyContext.CurrentFinancialYear.StartDate:d-MMM-yy} to {_companyContext.CurrentFinancialYear.EndDate:d-MMM-yy}"
            : "1-Apr-26 to 31-Mar-27";
        var lblPeriod = new Label
        {
            Text = fyText,
            Font = new Font("Segoe UI", 9F, FontStyle.Regular),
            ForeColor = Color.FromArgb(40, 50, 60),
            Location = new Point(0, y),
            AutoSize = true
        };
        panel.Controls.Add(lblPeriod);

        y += 38;
        var lblH2 = new Label
        {
            Text = "NAME OF COMPANY",
            Font = new Font("Segoe UI", 8F, FontStyle.Bold),
            ForeColor = Color.FromArgb(80, 110, 140),
            Location = new Point(0, y),
            AutoSize = true
        };
        panel.Controls.Add(lblH2);

        y += 18;
        string compName = _companyContext.CurrentCompany?.CompanyName ?? "(None)";
        var lblComp = new Label
        {
            Text = compName,
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 45, 65),
            Location = new Point(0, y),
            AutoSize = true
        };
        panel.Controls.Add(lblComp);
    }

    private void BuildCenterCard(Panel card)
    {
        // 1. Top Search Container
        var pnlSearchBox = new Panel
        {
            Location = new Point(100, 0),
            Size = new Size(480, 58),
            BackColor = Color.Transparent
        };

        var lblSelectTitle = new Label
        {
            Text = "Select Company",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 56, 101),
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 20
        };
        pnlSearchBox.Controls.Add(lblSelectTitle);

        var txtContainer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 28,
            BackColor = Color.FromArgb(255, 248, 204), // Pale yellow
            Padding = new Padding(2)
        };
        txtContainer.Paint += (s, e) =>
        {
            ControlPaint.DrawBorder(e.Graphics, txtContainer.ClientRectangle,
                Color.FromArgb(200, 160, 40), ButtonBorderStyle.Solid);
        };

        txtSearch = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = Color.FromArgb(255, 248, 204),
            Font = new Font("Segoe UI", 10.5F, FontStyle.Regular),
            ForeColor = Color.Black
        };
        txtSearch.TextChanged += (s, e) => ApplyFilter();
        txtSearch.KeyDown += OnSearchKeyDown;

        txtContainer.Controls.Add(txtSearch);
        pnlSearchBox.Controls.Add(txtContainer);
        card.Controls.Add(pnlSearchBox);

        // 2. Main List of Companies Card
        var pnlListCard = new Panel
        {
            Location = new Point(0, 68),
            Size = new Size(680, 480),
            BackColor = Color.White
        };
        pnlListCard.Paint += (s, e) =>
        {
            ControlPaint.DrawBorder(e.Graphics, pnlListCard.ClientRectangle,
                Color.FromArgb(100, 140, 180), ButtonBorderStyle.Solid);
        };

        // Header "List of Companies"
        var pnlListHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 26,
            BackColor = Color.FromArgb(0, 75, 135)
        };
        var lblListHeading = new Label
        {
            Text = "List of Companies",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(10, 4),
            AutoSize = true
        };
        pnlListHeader.Controls.Add(lblListHeading);
        pnlListCard.Controls.Add(pnlListHeader);

        // Column Header Bar
        var pnlColHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 24,
            BackColor = Color.FromArgb(216, 236, 248) // Light blue cyan
        };
        pnlColHeader.Paint += (s, e) =>
        {
            using var pen = new Pen(Color.FromArgb(178, 212, 235));
            e.Graphics.DrawLine(pen, 0, pnlColHeader.Height - 1, pnlColHeader.Width, pnlColHeader.Height - 1);
        };

        var lblCol1 = new Label
        {
            Text = "Data Path/Name",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 56, 101),
            Location = new Point(12, 4),
            AutoSize = true
        };
        var lblCol2 = new Label
        {
            Text = "Number",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 56, 101),
            Location = new Point(340, 4),
            AutoSize = true
        };
        var lblCol3 = new Label
        {
            Text = "Period",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 56, 101),
            Location = new Point(480, 4),
            AutoSize = true
        };
        pnlColHeader.Controls.Add(lblCol1);
        pnlColHeader.Controls.Add(lblCol2);
        pnlColHeader.Controls.Add(lblCol3);
        pnlListCard.Controls.Add(pnlColHeader);

        // Footer Bar (Count indicator)
        var pnlListFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 22,
            BackColor = Color.FromArgb(240, 246, 252)
        };
        lblListCount = new Label
        {
            Text = "0 ▼",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(80, 100, 120),
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 15, 0),
            Width = 80
        };
        pnlListFooter.Controls.Add(lblListCount);
        pnlListCard.Controls.Add(pnlListFooter);

        // The ListBox
        lstCompanies = new DoubleBufferedListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 23,
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 9.25F, FontStyle.Regular),
            IntegralHeight = false
        };
        lstCompanies.DrawItem += OnDrawListItem;
        lstCompanies.DoubleClick += (s, e) => ExecuteCurrentSelection();
        lstCompanies.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                ExecuteCurrentSelection();
            }
        };

        pnlListCard.Controls.Add(lstCompanies);
        pnlListCard.Controls.SetChildIndex(lstCompanies, 1); // Between headers and footer

        card.Controls.Add(pnlListCard);
    }

    private void BuildBottomBar(Panel panel)
    {
        var lblQuit = new Label
        {
            Text = "Q: Quit",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 75, 135),
            Location = new Point(14, 5),
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        lblQuit.Click += (s, e) => this.Close();
        panel.Controls.Add(lblQuit);

        var lblEsc = new Label
        {
            Text = "Esc: Back",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(100, 120, 140),
            Location = new Point(80, 5),
            AutoSize = true
        };
        panel.Controls.Add(lblEsc);

        var lblCreate = new Label
        {
            Text = "Alt+C: Create Company",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(0, 110, 60),
            Location = new Point(170, 5),
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        lblCreate.Click += (s, e) => CreateNewCompany();
        panel.Controls.Add(lblCreate);

        var lblAlter = new Label
        {
            Text = "Alt+A: Alter Company",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(0, 75, 135),
            Location = new Point(330, 5),
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        lblAlter.Click += (s, e) => AlterCurrentCompany();
        panel.Controls.Add(lblAlter);

        var lblSplit = new Label
        {
            Text = "Alt+S: Split Company",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
            ForeColor = Color.FromArgb(180, 83, 9),
            Location = new Point(480, 5),
            AutoSize = true,
            Cursor = Cursors.Hand
        };
        lblSplit.Click += (s, e) => SplitCurrentCompany();
        panel.Controls.Add(lblSplit);

        var lblPathHint = new Label
        {
            Text = $"Data Folder: {_currentDataPath}",
            Font = new Font("Segoe UI", 8.5F, FontStyle.Italic),
            ForeColor = Color.FromArgb(100, 116, 139),
            Dock = DockStyle.Right,
            TextAlign = ContentAlignment.MiddleRight,
            Padding = new Padding(0, 0, 15, 0),
            AutoSize = true
        };
        panel.Controls.Add(lblPathHint);
    }

    private async void LoadCompaniesAsync()
    {
        try
        {
            var companies = await _companyService.GetAllCompaniesAsync();
            _allCompanies = companies.ToList();
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
        _filteredItems.Clear();

        // 1. Actions at Top (just like Tally Prime screenshot)
        _filteredItems.Add(new CompanyListItem
        {
            Type = ListItemType.ActionCreateCompany,
            DisplayText = "Create Company",
            IsSelectable = true
        });

        _filteredItems.Add(new CompanyListItem
        {
            Type = ListItemType.ActionSelectRemote,
            DisplayText = "Select Remote Company",
            IsSelectable = true
        });

        _filteredItems.Add(new CompanyListItem
        {
            Type = ListItemType.ActionSpecifyPath,
            DisplayText = "Specify Path",
            IsSelectable = true
        });

        _filteredItems.Add(new CompanyListItem
        {
            Type = ListItemType.ActionSelectFromDrive,
            DisplayText = "Select from Drive",
            IsSelectable = true
        });

        // 2. Active Data Path
        _filteredItems.Add(new CompanyListItem
        {
            Type = ListItemType.PathHeader,
            DisplayText = _currentDataPath,
            IsSelectable = false
        });

        _filteredItems.Add(new CompanyListItem
        {
            Type = ListItemType.PathUp,
            DisplayText = "◆ Up",
            IsSelectable = true
        });

        // 3. Companies List
        var matchedCompanies = _allCompanies
            .Where(c => string.IsNullOrWhiteSpace(query)
                     || c.CompanyName.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || c.CompanyNumber.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var c in matchedCompanies)
        {
            _filteredItems.Add(new CompanyListItem
            {
                Type = ListItemType.Company,
                DisplayText = c.CompanyName,
                NumberText = $"({c.CompanyNumber})",
                PeriodText = c.PeriodDisplay,
                CompanyDto = c,
                IsSelectable = true
            });
        }

        lstCompanies.BeginUpdate();
        lstCompanies.Items.Clear();
        foreach (var item in _filteredItems)
        {
            lstCompanies.Items.Add(item);
        }
        lstCompanies.EndUpdate();

        lblListCount.Text = $"{matchedCompanies.Count} ▼";

        // Select first matching company or Create Company
        if (matchedCompanies.Any())
        {
            var firstCompIndex = _filteredItems.FindIndex(x => x.Type == ListItemType.Company);
            if (firstCompIndex >= 0)
            {
                lstCompanies.SelectedIndex = firstCompIndex;
            }
        }
        else if (lstCompanies.Items.Count > 0)
        {
            lstCompanies.SelectedIndex = 0;
        }
    }

    private void OnDrawListItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _filteredItems.Count) return;
        var item = _filteredItems[e.Index];
        bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected && item.IsSelectable;

        // Background
        Color bg = isSelected ? Color.FromArgb(255, 191, 0) : Color.White; // Tally Golden Amber
        using (var brushBg = new SolidBrush(bg))
        {
            e.Graphics.FillRectangle(brushBg, e.Bounds);
        }

        Color fg = isSelected ? Color.Black : Color.FromArgb(20, 30, 45);

        switch (item.Type)
        {
            case ListItemType.ActionCreateCompany:
            case ListItemType.ActionSelectRemote:
            case ListItemType.ActionSpecifyPath:
            case ListItemType.ActionSelectFromDrive:
                // Drawn right-aligned/indented in the middle-right area just like Tally
                using (var brushAction = new SolidBrush(isSelected ? Color.Black : Color.FromArgb(30, 50, 80)))
                using (var fontAction = new Font("Segoe UI", 9.25F, isSelected ? FontStyle.Bold : FontStyle.Regular))
                {
                    var rectAction = new Rectangle(e.Bounds.Left + 320, e.Bounds.Top + 2, e.Bounds.Width - 340, e.Bounds.Height - 4);
                    var sf = new StringFormat { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(item.DisplayText, fontAction, brushAction, rectAction, sf);
                }
                break;

            case ListItemType.PathHeader:
                using (var brushPath = new SolidBrush(Color.FromArgb(10, 30, 60)))
                using (var fontPath = new Font("Segoe UI", 9F, FontStyle.Bold))
                {
                    e.Graphics.DrawString(item.DisplayText, fontPath, brushPath, e.Bounds.Left + 12, e.Bounds.Top + 3);
                }
                break;

            case ListItemType.PathUp:
                using (var brushUp = new SolidBrush(isSelected ? Color.Black : Color.FromArgb(0, 75, 135)))
                using (var fontUp = new Font("Segoe UI", 9F, FontStyle.Bold))
                {
                    e.Graphics.DrawString(item.DisplayText, fontUp, brushUp, e.Bounds.Left + 12, e.Bounds.Top + 3);
                }
                break;

            case ListItemType.Company:
                // 1. Name (Left)
                using (var brushName = new SolidBrush(fg))
                using (var fontName = new Font("Segoe UI", 9.25F, isSelected ? FontStyle.Bold : FontStyle.Regular))
                {
                    var nameRect = new Rectangle(e.Bounds.Left + 12, e.Bounds.Top + 2, 310, e.Bounds.Height - 4);
                    var sf = new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };
                    e.Graphics.DrawString(item.DisplayText, fontName, brushName, nameRect, sf);
                }

                // 2. Number (Center)
                using (var brushNum = new SolidBrush(isSelected ? Color.Black : Color.FromArgb(90, 105, 120)))
                using (var fontNum = new Font("Segoe UI", 9F, FontStyle.Regular))
                {
                    var numRect = new Rectangle(e.Bounds.Left + 330, e.Bounds.Top + 2, 120, e.Bounds.Height - 4);
                    var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(item.NumberText, fontNum, brushNum, numRect, sf);
                }

                // 3. Period (Right)
                using (var brushPeriod = new SolidBrush(isSelected ? Color.Black : Color.FromArgb(100, 115, 130)))
                using (var fontPeriod = new Font("Segoe UI", 8.5F, FontStyle.Italic))
                {
                    var periodRect = new Rectangle(e.Bounds.Left + 460, e.Bounds.Top + 2, e.Bounds.Width - 470, e.Bounds.Height - 4);
                    var sf = new StringFormat { LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(item.PeriodText, fontPeriod, brushPeriod, periodRect, sf);
                }
                break;
        }

        // Focus rectangle
        if (isSelected)
        {
            using var penBorder = new Pen(Color.FromArgb(180, 130, 0), 1);
            e.Graphics.DrawRectangle(penBorder, e.Bounds.Left, e.Bounds.Top, e.Bounds.Width - 1, e.Bounds.Height - 1);
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down)
        {
            e.Handled = true;
            MoveSelection(1);
        }
        else if (e.KeyCode == Keys.Up)
        {
            e.Handled = true;
            MoveSelection(-1);
        }
        else if (e.KeyCode == Keys.Enter)
        {
            e.Handled = true;
            ExecuteCurrentSelection();
        }
        else if (e.KeyCode == Keys.Escape)
        {
            e.Handled = true;
            this.Close();
        }
    }

    private void OnFormKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            this.Close();
        }
        else if (e.Alt && e.KeyCode == Keys.C)
        {
            e.Handled = true;
            CreateNewCompany();
        }
        else if (e.Alt && e.KeyCode == Keys.A)
        {
            e.Handled = true;
            AlterCurrentCompany();
        }
        else if (e.Alt && e.KeyCode == Keys.D)
        {
            e.Handled = true;
            DeleteCurrentCompany();
        }
        else if (e.Alt && e.KeyCode == Keys.S)
        {
            e.Handled = true;
            SplitCurrentCompany();
        }
        else if (e.Alt && e.KeyCode == Keys.Y)
        {
            e.Handled = true;
            ShowDataMenu(this);
        }
    }

    private void MoveSelection(int delta)
    {
        if (lstCompanies.Items.Count == 0) return;
        int next = lstCompanies.SelectedIndex + delta;

        while (next >= 0 && next < _filteredItems.Count && !_filteredItems[next].IsSelectable)
        {
            next += delta;
        }

        if (next >= 0 && next < _filteredItems.Count)
        {
            lstCompanies.SelectedIndex = next;
        }
    }

    private void ExecuteCurrentSelection()
    {
        if (lstCompanies.SelectedIndex < 0 || lstCompanies.SelectedIndex >= _filteredItems.Count) return;
        var item = _filteredItems[lstCompanies.SelectedIndex];

        switch (item.Type)
        {
            case ListItemType.ActionCreateCompany:
                CreateNewCompany();
                break;

            case ListItemType.ActionSelectRemote:
                MessageBox.Show("Remote company access is currently in local mode.", "Tally Prime", MessageBoxButtons.OK, MessageBoxIcon.Information);
                break;

            case ListItemType.ActionSpecifyPath:
            case ListItemType.ActionSelectFromDrive:
                SelectDataPath();
                break;

            case ListItemType.PathUp:
                try
                {
                    var parent = Directory.GetParent(_currentDataPath);
                    if (parent != null && parent.Exists)
                    {
                        _currentDataPath = parent.FullName;
                        ApplyFilter();
                    }
                }
                catch { }
                break;

            case ListItemType.Company:
                if (item.CompanyDto != null)
                {
                    SelectCompany(item.CompanyDto.CompanyId);
                }
                break;
        }
    }

    private async void SelectCompany(int companyId)
    {
        var comp = _allCompanies.FirstOrDefault(c => c.CompanyId == companyId);
        if (comp != null && comp.IsPasswordProtected)
        {
            using var pwdDlg = new CompanyPasswordPromptDialog(comp.CompanyName, comp.CompanyNumber);
            bool verified = false;
            while (!verified)
            {
                if (pwdDlg.ShowDialog(this) != DialogResult.OK)
                {
                    return; // user cancelled password prompt
                }

                bool valid = await _companyService.VerifyCompanyPasswordAsync(companyId, pwdDlg.EnteredPassword);
                if (valid)
                {
                    verified = true;
                }
                else
                {
                    pwdDlg.SetError("Incorrect Tally Vault password. Please try again.");
                }
            }
        }

        bool ok = await _companyService.OpenCompanyAsync(companyId);
        if (ok)
        {
            CompanySelected = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        else
        {
            MessageBox.Show("Unable to open selected company.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void SplitCurrentCompany()
    {
        var comp = GetSelectedCompany();
        if (comp == null)
        {
            MessageBox.Show("Please select a company to split.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_companySplitService == null)
        {
            MessageBox.Show("Company Split service is not available.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var fullComp = await _companyService.GetCompanyByIdAsync(comp.CompanyId);
        if (fullComp == null)
        {
            MessageBox.Show("Could not load company details.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        using var splitDlg = new CompanySplitDialog(_companySplitService, fullComp);
        if (splitDlg.ShowDialog(this) == DialogResult.OK)
        {
            LoadCompaniesAsync();
        }
    }

    private void ShowDataMenu(Control anchor)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Split Company Data (Financial Year-End Rollover)...", null, (s, e) => SplitCurrentCompany());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Specify Company Data Path...", null, (s, e) => SelectDataPath());
        menu.Items.Add("Select from Drive...", null, (s, e) => SelectDataPath());
        menu.Show(anchor, new Point(0, anchor.Height));
    }

    private void CreateNewCompany()
    {
        using var form = new CompanyCreateEditForm(_companyService);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            LoadCompaniesAsync();
            CompanySelected = true;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    private void AlterCurrentCompany()
    {
        var comp = GetSelectedCompany();
        if (comp == null)
        {
            MessageBox.Show("Please select a company to alter.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new CompanyCreateEditForm(_companyService, comp.CompanyId);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            LoadCompaniesAsync();
        }
    }

    private async void DeleteCurrentCompany()
    {
        var comp = GetSelectedCompany();
        if (comp == null)
        {
            MessageBox.Show("Please select a company to delete.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var res = MessageBox.Show($"Are you sure you want to mark company '{comp.CompanyName}' as inactive?",
            "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (res == DialogResult.Yes)
        {
            await _companyService.DeleteCompanyAsync(comp.CompanyId);
            LoadCompaniesAsync();
        }
    }

    private CompanySummaryDto? GetSelectedCompany()
    {
        if (lstCompanies.SelectedIndex < 0 || lstCompanies.SelectedIndex >= _filteredItems.Count) return null;
        var item = _filteredItems[lstCompanies.SelectedIndex];
        return item.CompanyDto;
    }

    private void SelectDataPath()
    {
        using var fbd = new FolderBrowserDialog();
        fbd.Description = "Select Tally / MoneyFlow Company Data Path";
        fbd.UseDescriptionForTitle = true;
        if (Directory.Exists(_currentDataPath))
        {
            fbd.SelectedPath = _currentDataPath;
        }

        if (fbd.ShowDialog(this) == DialogResult.OK)
        {
            _currentDataPath = fbd.SelectedPath;
            ApplyFilter();
        }
    }
}

internal enum ListItemType
{
    ActionCreateCompany,
    ActionSelectRemote,
    ActionSpecifyPath,
    ActionSelectFromDrive,
    PathHeader,
    PathUp,
    Company
}

internal class CompanyListItem
{
    public ListItemType Type { get; set; }
    public string DisplayText { get; set; } = string.Empty;
    public string NumberText { get; set; } = string.Empty;
    public string PeriodText { get; set; } = string.Empty;
    public CompanySummaryDto? CompanyDto { get; set; }
    public bool IsSelectable { get; set; } = true;

    public override string ToString() => DisplayText;
}

internal class DoubleBufferedListBox : ListBox
{
    public DoubleBufferedListBox()
    {
        this.SetStyle(ControlStyles.OptimizedDoubleBuffer |
                      ControlStyles.AllPaintingInWmPaint |
                      ControlStyles.UserPaint, false);
        this.DoubleBuffered = true;
    }
}
