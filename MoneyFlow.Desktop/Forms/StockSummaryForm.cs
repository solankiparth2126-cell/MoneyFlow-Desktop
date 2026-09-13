using System;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;

using Guna.UI2.WinForms;
using MoneyFlow.Desktop.Styling;

namespace MoneyFlow.Desktop.Forms;

public class StockSummaryForm : Form
{
    private readonly IInventoryService _inventoryService;
    private readonly ICompanyContext _companyContext;

    private DateTimePicker _dtpAsOfDate = null!;
    private TextBox _txtSearch = null!;
    private Button _btnRefresh = null!;
    private Guna2DataGridView _dgvSummary = null!;
    private Label _lblTotalItems = null!;
    private Label _lblTotalClosingValue = null!;
    private Button _btnExportCsv = null!;
    private Button _btnPrint = null!;
    private Button _btnClose = null!;

    private StockSummaryReportDto? _currentReport;

    public StockSummaryForm(
        IInventoryService inventoryService,
        ICompanyContext companyContext)
    {
        _inventoryService = inventoryService;
        _companyContext = companyContext;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Stock Summary (Inventory Valuation)";
        Size = new Size(1100, 680);
        StartPosition = FormStartPosition.CenterParent;
        Font = ExecLedgerTheme.UIRegular9;
        KeyPreview = true;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(15)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Filter bar
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Footer Summary
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));  // Actions

        // 1. Filter Bar
        var pnlFilters = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            BackColor = ExecLedgerTheme.SecondarySurface,
            Padding = new Padding(10, 8, 10, 8)
        };
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // "As of Date:"
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140)); // DTP
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // Search
        pnlFilters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // Refresh

        pnlFilters.Controls.Add(new Label { Text = "As of Date (F2):", AutoSize = true, Anchor = AnchorStyles.Left, Font = ExecLedgerTheme.UIBold9 }, 0, 0);

        _dtpAsOfDate = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "dd-MMM-yyyy",
            Width = 130
        };
        _dtpAsOfDate.ValueChanged += async (s, e) => await LoadSummaryDataAsync();
        pnlFilters.Controls.Add(_dtpAsOfDate, 1, 0);

        _txtSearch = new TextBox
        {
            PlaceholderText = "Search item name or unit (F3)...",
            Dock = DockStyle.Fill
        };
        _txtSearch.TextChanged += (s, e) => RenderSummary();
        pnlFilters.Controls.Add(_txtSearch, 2, 0);

        _btnRefresh = new Button
        {
            Text = "Refresh (F5)",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(220, 230, 242)
        };
        _btnRefresh.Click += async (s, e) => await LoadSummaryDataAsync();
        pnlFilters.Controls.Add(_btnRefresh, 3, 0);

        mainLayout.Controls.Add(pnlFilters, 0, 0);

        // 2. DataGridView
        _dgvSummary = new Guna2DataGridView
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
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        _dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColItemName", HeaderText = "Item Particulars", FillWeight = 35 });
        _dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColUnit", HeaderText = "Unit", FillWeight = 10, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter } });
        _dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColOpQty", HeaderText = "Opening Qty", FillWeight = 13, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } });
        _dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColOpVal", HeaderText = "Opening Val (₹)", FillWeight = 15, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } });
        _dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColClQty", HeaderText = "Closing Qty", FillWeight = 13, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } });
        _dgvSummary.Columns.Add(new DataGridViewTextBoxColumn { Name = "ColClRate", HeaderText = "Rate (₹)", FillWeight = 12, DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N2" } });
        _dgvSummary.Columns.Add(new DataGridViewTextBoxColumn {
            Name = "ColClVal",
            HeaderText = "Closing Value (₹)",
            FillWeight = 18,
            DefaultCellStyle = {
                Alignment = DataGridViewContentAlignment.MiddleRight,
                Format = "N2",
                Font = ExecLedgerTheme.UIBold9,
                ForeColor = Color.FromArgb(24, 43, 73)
            }
        });

        mainLayout.Controls.Add(_dgvSummary, 0, 1);

        // 3. Footer Summary Panel
        var pnlFooter = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = ExecLedgerTheme.SecondarySurface,
            Padding = new Padding(12, 10, 12, 10)
        };
        pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        pnlFooter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        _lblTotalItems = new Label
        {
            Text = "Total Stock Items: 0",
            Font = ExecLedgerTheme.UIBold10,
            ForeColor = Color.FromArgb(70, 80, 95),
            Anchor = AnchorStyles.Left,
            AutoSize = true
        };

        _lblTotalClosingValue = new Label
        {
            Text = "Total Stock Value: ₹0.00",
            Font = ExecLedgerTheme.UIBold10,
            ForeColor = Color.FromArgb(16, 125, 65),
            Anchor = AnchorStyles.Right,
            AutoSize = true
        };

        pnlFooter.Controls.Add(_lblTotalItems, 0, 0);
        pnlFooter.Controls.Add(_lblTotalClosingValue, 1, 0);
        mainLayout.Controls.Add(pnlFooter, 0, 2);

        // 4. Action Buttons
        var pnlActions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };

        _btnClose = new Button { Text = "Close (Esc)", Width = 110, Height = 36 };
        _btnClose.Click += (s, e) => Close();

        _btnPrint = new Button { Text = "Print (Ctrl+P)", Width = 120, Height = 36, BackColor = Color.FromArgb(220, 235, 252) };
        _btnPrint.Click += (s, e) => PrintSummary();

        _btnExportCsv = new Button { Text = "Export CSV", Width = 110, Height = 36, BackColor = Color.FromArgb(220, 245, 230) };
        _btnExportCsv.Click += (s, e) => ExportToCsv();

        pnlActions.Controls.Add(_btnClose);
        pnlActions.Controls.Add(_btnPrint);
        pnlActions.Controls.Add(_btnExportCsv);
        mainLayout.Controls.Add(pnlActions, 0, 3);

        Controls.Add(mainLayout);

        KeyDown += StockSummaryForm_KeyDown;
        Load += async (s, e) => await InitializeFormAsync();
    }

    private async Task InitializeFormAsync()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            MessageBox.Show(this, "Please select or open a company first.", "No Company", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Close();
            return;
        }

        var fy = _companyContext.CurrentFinancialYear;
        if (fy != null && DateTime.Today > fy.EndDate)
        {
            _dtpAsOfDate.Value = fy.EndDate;
        }
        else
        {
            _dtpAsOfDate.Value = DateTime.Today;
        }

        await LoadSummaryDataAsync();
    }

    private async Task LoadSummaryDataAsync()
    {
        var company = _companyContext.CurrentCompany;
        if (company == null) return;

        try
        {
            UseWaitCursor = true;
            _currentReport = await _inventoryService.GetStockSummaryAsync(company.CompanyId, _dtpAsOfDate.Value.Date);
            RenderSummary();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to load Stock Summary: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void RenderSummary()
    {
        _dgvSummary.Rows.Clear();
        if (_currentReport == null) return;

        var filter = _txtSearch.Text.Trim().ToLowerInvariant();

        var query = _currentReport.Items.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            query = query.Where(i => i.ItemName.ToLowerInvariant().Contains(filter) ||
                                     i.UnitName.ToLowerInvariant().Contains(filter));
        }

        var list = query.OrderBy(i => i.ItemName).ToList();

        foreach (var item in list)
        {
            var idx = _dgvSummary.Rows.Add();
            var row = _dgvSummary.Rows[idx];
            row.Tag = item;

            row.Cells["ColItemName"].Value = item.ItemName;
            row.Cells["ColUnit"].Value = !string.IsNullOrEmpty(item.UnitName) ? item.UnitName : "—";
            row.Cells["ColOpQty"].Value = item.OpeningQuantity;
            row.Cells["ColOpVal"].Value = item.OpeningValue;
            row.Cells["ColClQty"].Value = item.ClosingQuantity;
            row.Cells["ColClRate"].Value = item.ClosingRate;
            row.Cells["ColClVal"].Value = item.ClosingValue;
        }

        decimal totalClosingVal = list.Sum(i => i.ClosingValue);
        _lblTotalItems.Text = $"Total Stock Items: {list.Count} (of {_currentReport.Items.Count})";
        _lblTotalClosingValue.Text = $"Total Closing Stock Value: ₹{totalClosingVal:N2}";
    }

    private void ExportToCsv()
    {
        if (_currentReport == null || _currentReport.Items.Count == 0)
        {
            MessageBox.Show(this, "No inventory items to export.", "Empty Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var sfd = new SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv",
            FileName = $"StockSummary_{_currentReport.AsOfDate:yyyyMMdd_HHmmss}.csv"
        };

        if (sfd.ShowDialog(this) == DialogResult.OK)
        {
            var sb = new StringBuilder();
            sb.AppendLine("\"Stock Summary / Inventory Valuation Register\"");
            sb.AppendLine($"\"As of Date: {_currentReport.AsOfDate:dd-MMM-yyyy}\"");
            sb.AppendLine();
            sb.AppendLine("ItemName,Unit,OpeningQty,OpeningValue,ClosingQty,ClosingRate,ClosingValue");

            foreach (var item in _currentReport.Items)
            {
                sb.AppendLine($"\"{EscapeCsv(item.ItemName)}\",\"{EscapeCsv(item.UnitName)}\",{item.OpeningQuantity:F2},{item.OpeningValue:F2},{item.ClosingQuantity:F2},{item.ClosingRate:F2},{item.ClosingValue:F2}");
            }

            sb.AppendLine();
            sb.AppendLine($",,,,Total Closing Value:,{_currentReport.TotalClosingValue:F2}");

            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show(this, $"Exported {_currentReport.Items.Count} items successfully to:\n{sfd.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void PrintSummary()
    {
        if (_currentReport == null || _currentReport.Items.Count == 0)
        {
            MessageBox.Show(this, "No inventory items to print.", "Empty Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            using var printDoc = new PrintDocument();
            printDoc.DocumentName = $"Stock Summary - {_currentReport.AsOfDate:dd-MMM-yyyy}";
            int currentIndex = 0;

            printDoc.PrintPage += (s, e) =>
            {
                var g = e.Graphics!;
                float y = 50;
                var fontHeader = new Font("Segoe UI", 13F, FontStyle.Bold);
                var fontSubHeader = ExecLedgerTheme.UIBold9;
                var fontBody = ExecLedgerTheme.UIRegular8;

                string companyName = _companyContext.CurrentCompany?.CompanyName ?? "MoneyFlow Desktop Accounting";
                g.DrawString(companyName, fontHeader, Brushes.Black, 50, y);
                y += 25;

                g.DrawString($"STOCK SUMMARY (As of {_currentReport.AsOfDate:dd-MMM-yyyy})", fontSubHeader, Brushes.Black, 50, y);
                y += 25;

                g.DrawLine(Pens.Black, 50, y, e.MarginBounds.Right, y);
                y += 5;

                // Table Header
                g.DrawString("Item Name", fontSubHeader, Brushes.Black, 50, y);
                g.DrawString("Unit", fontSubHeader, Brushes.Black, 300, y);
                g.DrawString("Qty", fontSubHeader, Brushes.Black, 400, y);
                g.DrawString("Rate (₹)", fontSubHeader, Brushes.Black, 500, y);
                g.DrawString("Value (₹)", fontSubHeader, Brushes.Black, 620, y);
                y += 20;

                g.DrawLine(Pens.Black, 50, y, e.MarginBounds.Right, y);
                y += 8;

                while (currentIndex < _currentReport.Items.Count)
                {
                    if (y > e.MarginBounds.Bottom - 40)
                    {
                        e.HasMorePages = true;
                        return;
                    }

                    var item = _currentReport.Items[currentIndex];
                    string name = item.ItemName.Length > 32 ? item.ItemName.Substring(0, 29) + "..." : item.ItemName;
                    g.DrawString(name, fontBody, Brushes.Black, 50, y);
                    g.DrawString(item.UnitName, fontBody, Brushes.Black, 300, y);
                    g.DrawString(item.ClosingQuantity.ToString("N2"), fontBody, Brushes.Black, 400, y);
                    g.DrawString(item.ClosingRate.ToString("N2"), fontBody, Brushes.Black, 500, y);
                    g.DrawString(item.ClosingValue.ToString("N2"), fontBody, Brushes.Black, 620, y);

                    y += 18;
                    currentIndex++;
                }

                y += 10;
                g.DrawLine(Pens.Black, 50, y, e.MarginBounds.Right, y);
                y += 5;
                g.DrawString($"Total Stock Value: ₹{_currentReport.TotalClosingValue:N2}", fontSubHeader, Brushes.Black, 50, y);

                e.HasMorePages = false;
            };

            using var preview = new PrintPreviewDialog
            {
                Document = printDoc,
                Width = 950,
                Height = 700,
                StartPosition = FormStartPosition.CenterParent
            };
            preview.ShowDialog(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Print Preview failed: {ex.Message}", "Print Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static string EscapeCsv(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;
        return field.Replace("\"", "\"\"");
    }

    private void StockSummaryForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F2)
        {
            _dtpAsOfDate.Focus();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F3)
        {
            _txtSearch.Focus();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.F5)
        {
            _ = LoadSummaryDataAsync();
            e.Handled = true;
        }
        else if (e.Control && e.KeyCode == Keys.P)
        {
            PrintSummary();
            e.Handled = true;
        }
    }
}
