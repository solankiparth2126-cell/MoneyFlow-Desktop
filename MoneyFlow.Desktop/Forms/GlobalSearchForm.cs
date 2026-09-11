using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Forms;

public class GlobalSearchForm : Form
{
    private readonly ISearchService _searchService;
    private readonly ICompanyContext _companyContext;
    private readonly Action<GlobalSearchResultDto> _onNavigate;

    private TextBox _txtSearch = null!;
    private RadioButton _rbAll = null!;
    private RadioButton _rbVouchers = null!;
    private RadioButton _rbLedgers = null!;
    private RadioButton _rbStockItems = null!;
    private RadioButton _rbScreens = null!;
    private DataGridView _dgvResults = null!;
    private Label _lblResultCount = null!;
    private System.Windows.Forms.Timer _debounceTimer = null!;

    private GlobalSearchCategory _currentCategory = GlobalSearchCategory.All;
    private IReadOnlyList<GlobalSearchResultDto> _currentResults = new List<GlobalSearchResultDto>();

    public GlobalSearchForm(
        ISearchService searchService,
        ICompanyContext companyContext,
        Action<GlobalSearchResultDto> onNavigate)
    {
        _searchService = searchService;
        _companyContext = companyContext;
        _onNavigate = onNavigate;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Go To — Global Accounting & Inventory Search (Alt+G)";
        Size = new Size(1000, 620);
        StartPosition = FormStartPosition.CenterParent;
        Font = new Font("Segoe UI", 9.5F);
        KeyPreview = true;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(15)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Search input
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));  // Filter tabs
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Results Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));  // Footer Bar

        // 1. Search Box
        var pnlSearch = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(24, 43, 73),
            Padding = new Padding(8)
        };

        _txtSearch = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 12F),
            PlaceholderText = "Search ledgers, vouchers, amounts, items, or screens (e.g. Rent, 5000, SLS-001, Balance Sheet)..."
        };
        _txtSearch.TextChanged += (s, e) => RestartDebounce();
        _txtSearch.KeyDown += TxtSearch_KeyDown;
        pnlSearch.Controls.Add(_txtSearch);
        mainLayout.Controls.Add(pnlSearch, 0, 0);

        // 2. Filter Tabs
        var pnlTabs = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            BackColor = Color.FromArgb(245, 248, 252),
            Padding = new Padding(8, 6, 8, 4)
        };

        _rbAll = CreateCategoryRadio("All Records", GlobalSearchCategory.All, true);
        _rbVouchers = CreateCategoryRadio("Vouchers", GlobalSearchCategory.Voucher);
        _rbLedgers = CreateCategoryRadio("Ledgers", GlobalSearchCategory.Ledger);
        _rbStockItems = CreateCategoryRadio("Stock Items", GlobalSearchCategory.StockItem);
        _rbScreens = CreateCategoryRadio("Go To Screens", GlobalSearchCategory.Navigation);

        pnlTabs.Controls.AddRange(new Control[] { _rbAll, _rbVouchers, _rbLedgers, _rbStockItems, _rbScreens });
        mainLayout.Controls.Add(pnlTabs, 0, 1);

        // 3. Results Grid
        _dgvResults = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.Fixed3D,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
            RowTemplate = { Height = 32 }
        };

        SetupGridColumns();
        _dgvResults.DoubleClick += (s, e) => ExecuteNavigation();
        _dgvResults.KeyDown += DgvResults_KeyDown;
        mainLayout.Controls.Add(_dgvResults, 0, 2);

        // 4. Footer
        var pnlFooter = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.FromArgb(245, 248, 252),
            Padding = new Padding(10, 8, 10, 8)
        };
        pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _lblResultCount = new Label
        {
            Text = "Results: 0",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            ForeColor = Color.FromArgb(70, 80, 95),
            Anchor = AnchorStyles.Left,
            AutoSize = true
        };

        var lblHelp = new Label
        {
            Text = "Press [Enter] to Open Record | [Esc] to Close | [↑/↓] to Browse",
            Font = new Font("Segoe UI", 9F),
            ForeColor = Color.FromArgb(100, 110, 125),
            Anchor = AnchorStyles.Right,
            AutoSize = true
        };

        pnlFooter.Controls.Add(_lblResultCount, 0, 0);
        pnlFooter.Controls.Add(lblHelp, 1, 0);
        mainLayout.Controls.Add(pnlFooter, 0, 3);

        Controls.Add(mainLayout);

        // Debounce timer (200ms)
        _debounceTimer = new System.Windows.Forms.Timer { Interval = 200 };
        _debounceTimer.Tick += async (s, e) =>
        {
            _debounceTimer.Stop();
            await PerformSearchAsync();
        };

        KeyDown += GlobalSearchForm_KeyDown;
        Load += async (s, e) =>
        {
            _txtSearch.Focus();
            await PerformSearchAsync();
        };
    }

    private RadioButton CreateCategoryRadio(string label, GlobalSearchCategory category, bool isChecked = false)
    {
        var rb = new RadioButton
        {
            Text = label,
            Checked = isChecked,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 43, 73),
            AutoSize = true,
            Margin = new Padding(0, 0, 15, 0)
        };
        rb.CheckedChanged += async (s, e) =>
        {
            if (rb.Checked)
            {
                _currentCategory = category;
                await PerformSearchAsync();
            }
        };
        return rb;
    }

    private void SetupGridColumns()
    {
        _dgvResults.Columns.Clear();

        _dgvResults.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColCategory",
            HeaderText = "Category",
            Width = 120,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }
        });

        _dgvResults.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColTitle",
            HeaderText = "Title / Particulars",
            Width = 260,
            DefaultCellStyle = { Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) }
        });

        _dgvResults.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColSubtitle",
            HeaderText = "Context / Details",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });

        _dgvResults.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColDate",
            HeaderText = "Date",
            Width = 100,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
        });

        _dgvResults.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "ColAmount",
            HeaderText = "Amount (₹)",
            Width = 125,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F, FontStyle.Bold), ForeColor = Color.FromArgb(24, 43, 73) }
        });
    }

    private void RestartDebounce()
    {
        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    private async Task PerformSearchAsync()
    {
        var company = _companyContext.CurrentCompany;
        if (company == null) return;

        try
        {
            _currentResults = await _searchService.SearchAsync(
                company.CompanyId,
                _txtSearch.Text,
                _currentCategory,
                maxResults: 60);

            RenderResults();
        }
        catch (Exception ex)
        {
            _lblResultCount.Text = $"Search error: {ex.Message}";
        }
    }

    private void RenderResults()
    {
        _dgvResults.Rows.Clear();

        foreach (var item in _currentResults)
        {
            var idx = _dgvResults.Rows.Add();
            var row = _dgvResults.Rows[idx];
            row.Tag = item;

            row.Cells["ColCategory"].Value = item.CategoryName;
            row.Cells["ColTitle"].Value = item.Title;
            row.Cells["ColSubtitle"].Value = item.Subtitle;
            row.Cells["ColDate"].Value = item.FormattedDate;
            row.Cells["ColAmount"].Value = item.FormattedAmount;

            // Apply distinct category styling
            switch (item.Category)
            {
                case GlobalSearchCategory.Navigation:
                    row.Cells["ColCategory"].Style.ForeColor = Color.FromArgb(37, 99, 235); // Blue
                    break;
                case GlobalSearchCategory.Ledger:
                    row.Cells["ColCategory"].Style.ForeColor = Color.FromArgb(16, 125, 65); // Green
                    break;
                case GlobalSearchCategory.Voucher:
                    row.Cells["ColCategory"].Style.ForeColor = Color.FromArgb(109, 40, 217); // Purple
                    break;
                case GlobalSearchCategory.StockItem:
                    row.Cells["ColCategory"].Style.ForeColor = Color.FromArgb(194, 65, 12); // Amber
                    break;
            }
        }

        _lblResultCount.Text = $"Results: {_currentResults.Count}";

        if (_dgvResults.Rows.Count > 0)
        {
            _dgvResults.Rows[0].Selected = true;
        }
    }

    private void ExecuteNavigation()
    {
        if (_dgvResults.CurrentRow?.Tag is not GlobalSearchResultDto selected) return;

        Close();
        _onNavigate?.Invoke(selected);
    }

    private void TxtSearch_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Down)
        {
            if (_dgvResults.Rows.Count > 0)
            {
                _dgvResults.Focus();
                e.Handled = true;
            }
        }
        else if (e.KeyCode == Keys.Enter)
        {
            ExecuteNavigation();
            e.Handled = true;
        }
    }

    private void DgvResults_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            ExecuteNavigation();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Up && _dgvResults.CurrentRow?.Index == 0)
        {
            _txtSearch.Focus();
            _txtSearch.SelectAll();
            e.Handled = true;
        }
    }

    private void GlobalSearchForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _debounceTimer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
