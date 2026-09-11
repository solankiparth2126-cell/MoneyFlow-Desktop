using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Desktop.Forms;

public class ImportExportForm : Form
{
    private readonly IImportExportService _importExportService;
    private readonly ICompanyContext _companyContext;

    // Tab Control
    private TabControl _tabMain = null!;

    // Tab 1: Import Controls
    private ComboBox _cmbImportEntity = null!;
    private TextBox _txtImportFilePath = null!;
    private Button _btnBrowseFile = null!;
    private Button _btnDownloadTemplate = null!;
    private RadioButton _rbSkipDuplicates = null!;
    private RadioButton _rbUpdateDuplicates = null!;
    private RadioButton _rbRejectDuplicates = null!;
    private Button _btnAnalyzePreview = null!;
    private DataGridView _dgvImportPreview = null!;
    private Label _lblImportSummary = null!;
    private Button _btnExecuteImport = null!;

    private ImportPreviewResultDto? _currentPreview;

    // Tab 2: Export Controls
    private ComboBox _cmbExportEntity = null!;
    private RadioButton _rbFormatCsv = null!;
    private RadioButton _rbFormatJson = null!;
    private DateTimePicker _dtpExportFrom = null!;
    private DateTimePicker _dtpExportTo = null!;
    private Panel _pnlDateRange = null!;
    private Button _btnExecuteExport = null!;

    public ImportExportForm(
        IImportExportService importExportService,
        ICompanyContext companyContext)
    {
        _importExportService = importExportService;
        _companyContext = companyContext;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Data Import & Export Wizard — MoneyFlow Accounting";
        Size = new Size(980, 680);
        MinimumSize = new Size(880, 560);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
        BackColor = Color.FromArgb(244, 246, 249);
        KeyPreview = true;

        // Top Header Strip
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = Color.FromArgb(18, 52, 86),
            Padding = new Padding(15, 12, 15, 10)
        };

        var lblTitle = new Label
        {
            Text = "DATA IMPORT & EXPORT WIZARD",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(15, 14)
        };

        var btnClose = new Button
        {
            Text = "Close (Esc)",
            Width = 80,
            Height = 28,
            BackColor = Color.FromArgb(192, 57, 43),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(880, 13)
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, e) => Close();

        pnlHeader.Controls.Add(lblTitle);
        pnlHeader.Controls.Add(btnClose);

        // Main TabControl
        _tabMain = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 9.5F),
            Padding = new Point(14, 6)
        };

        // Tab 1: Import
        var tabImport = new TabPage("  Import Data (CSV / Staged)  ");
        InitializeImportTab(tabImport);

        // Tab 2: Export
        var tabExport = new TabPage("  Export Data (CSV / JSON)  ");
        InitializeExportTab(tabExport);

        _tabMain.TabPages.Add(tabImport);
        _tabMain.TabPages.Add(tabExport);

        Controls.Add(_tabMain);
        Controls.Add(pnlHeader);
    }

    private void InitializeImportTab(TabPage tab)
    {
        var pnlTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 155,
            BackColor = Color.White,
            Padding = new Padding(15, 10, 15, 10)
        };

        // Entity Selection
        var lblEntity = new Label { Text = "Target Entity:", Location = new Point(15, 15), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
        _cmbImportEntity = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(115, 12),
            Width = 220
        };
        _cmbImportEntity.Items.AddRange(new object[] {
            "Chart of Accounts (Ledgers)",
            "Stock Items (Inventory)",
            "Day Book (Vouchers / Transactions)"
        });
        _cmbImportEntity.SelectedIndex = 0;

        _btnDownloadTemplate = new Button
        {
            Text = "Download Sample Template CSV",
            Location = new Point(350, 11),
            Width = 210,
            Height = 26,
            BackColor = Color.FromArgb(52, 73, 94),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnDownloadTemplate.FlatAppearance.BorderSize = 0;
        _btnDownloadTemplate.Click += async (s, e) => await DownloadTemplateAsync();

        // File Picker
        var lblFile = new Label { Text = "CSV File Path:", Location = new Point(15, 50), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
        _txtImportFilePath = new TextBox { Location = new Point(115, 47), Width = 445, ReadOnly = true };
        _btnBrowseFile = new Button
        {
            Text = "Browse...",
            Location = new Point(570, 46),
            Width = 85,
            Height = 26,
            BackColor = Color.FromArgb(41, 128, 185),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnBrowseFile.FlatAppearance.BorderSize = 0;
        _btnBrowseFile.Click += (s, e) => BrowseCsvFile();

        // Duplicate Handling
        var lblDup = new Label { Text = "Duplicates:", Location = new Point(15, 85), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
        _rbSkipDuplicates = new RadioButton { Text = "Skip Duplicates (Recommended)", Location = new Point(115, 83), AutoSize = true, Checked = true };
        _rbUpdateDuplicates = new RadioButton { Text = "Update Existing", Location = new Point(325, 83), AutoSize = true };
        _rbRejectDuplicates = new RadioButton { Text = "Reject Batch on Duplicate", Location = new Point(455, 83), AutoSize = true };

        // Analyze & Preview Button
        _btnAnalyzePreview = new Button
        {
            Text = "Analyze & Preview (F5)",
            Location = new Point(115, 115),
            Width = 175,
            Height = 30,
            BackColor = Color.FromArgb(39, 174, 96),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat
        };
        _btnAnalyzePreview.FlatAppearance.BorderSize = 0;
        _btnAnalyzePreview.Click += async (s, e) => await AnalyzeAndPreviewAsync();

        pnlTop.Controls.AddRange(new Control[] {
            lblEntity, _cmbImportEntity, _btnDownloadTemplate,
            lblFile, _txtImportFilePath, _btnBrowseFile,
            lblDup, _rbSkipDuplicates, _rbUpdateDuplicates, _rbRejectDuplicates,
            _btnAnalyzePreview
        });

        // Bottom Summary & Action Bar
        var pnlBottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = Color.FromArgb(235, 240, 245),
            Padding = new Padding(15, 10, 15, 10)
        };

        _lblImportSummary = new Label
        {
            Text = "Select a file and click 'Analyze & Preview' to inspect rows prior to import.",
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            ForeColor = Color.FromArgb(50, 60, 70),
            Dock = DockStyle.Left,
            AutoSize = true
        };

        _btnExecuteImport = new Button
        {
            Text = "Confirm & Execute Import",
            Width = 195,
            Height = 30,
            BackColor = Color.FromArgb(18, 52, 86),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Dock = DockStyle.Right,
            Enabled = false
        };
        _btnExecuteImport.FlatAppearance.BorderSize = 0;
        _btnExecuteImport.Click += async (s, e) => await ExecuteImportAsync();

        pnlBottom.Controls.Add(_lblImportSummary);
        pnlBottom.Controls.Add(_btnExecuteImport);

        // Central Grid for Staged Preview
        _dgvImportPreview = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            EnableHeadersVisualStyles = false
        };

        _dgvImportPreview.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(235, 240, 245);
        _dgvImportPreview.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        _dgvImportPreview.ColumnHeadersHeight = 28;
        _dgvImportPreview.RowTemplate.Height = 25;

        _dgvImportPreview.Columns.Add("RowNumber", "Row #");
        _dgvImportPreview.Columns.Add("Status", "Validation Status");
        _dgvImportPreview.Columns.Add("Identifier", "Record Name / Number");
        _dgvImportPreview.Columns.Add("Details", "Staged Details");
        _dgvImportPreview.Columns.Add("Message", "Validation Notes / Errors");

        _dgvImportPreview.Columns[0].FillWeight = 8;
        _dgvImportPreview.Columns[1].FillWeight = 15;
        _dgvImportPreview.Columns[2].FillWeight = 25;
        _dgvImportPreview.Columns[3].FillWeight = 32;
        _dgvImportPreview.Columns[4].FillWeight = 35;

        tab.Controls.Add(_dgvImportPreview);
        tab.Controls.Add(pnlBottom);
        tab.Controls.Add(pnlTop);
    }

    private void InitializeExportTab(TabPage tab)
    {
        var pnlExport = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(30, 25, 30, 25)
        };

        var lblSection = new Label
        {
            Text = "SELECT DATA TO EXPORT",
            Font = new Font("Segoe UI", 11F, FontStyle.Bold),
            ForeColor = Color.FromArgb(18, 52, 86),
            Location = new Point(30, 25),
            AutoSize = true
        };

        var lblEntity = new Label { Text = "Entity Type:", Location = new Point(30, 65), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        _cmbExportEntity = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(140, 62),
            Width = 260
        };
        _cmbExportEntity.Items.AddRange(new object[] {
            "Chart of Accounts (Ledgers)",
            "Stock Items (Inventory)",
            "Day Book (Vouchers / Transactions)"
        });
        _cmbExportEntity.SelectedIndex = 0;
        _cmbExportEntity.SelectedIndexChanged += (s, e) =>
        {
            _pnlDateRange.Visible = _cmbExportEntity.SelectedIndex == 2;
        };

        // Format Selection
        var lblFormat = new Label { Text = "Export Format:", Location = new Point(30, 110), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        _rbFormatCsv = new RadioButton { Text = "CSV (Excel Compatible RFC 4180)", Location = new Point(140, 108), AutoSize = true, Checked = true };
        _rbFormatJson = new RadioButton { Text = "JSON (Structured Interchange)", Location = new Point(380, 108), AutoSize = true };

        // Date Range Panel (for Vouchers)
        _pnlDateRange = new Panel
        {
            Location = new Point(30, 150),
            Size = new Size(500, 45),
            Visible = false
        };
        var lblDateRange = new Label { Text = "Date Range:", Location = new Point(0, 8), AutoSize = true, Font = new Font("Segoe UI", 9.5F, FontStyle.Bold) };
        _dtpExportFrom = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Location = new Point(110, 5), Width = 130 };
        _dtpExportTo = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "dd-MMM-yyyy", Location = new Point(260, 5), Width = 130 };
        _dtpExportFrom.Value = DateTime.Today.AddMonths(-1);
        _dtpExportTo.Value = DateTime.Today;

        _pnlDateRange.Controls.AddRange(new Control[] { lblDateRange, _dtpExportFrom, _dtpExportTo });

        // Export Action Button
        _btnExecuteExport = new Button
        {
            Text = "Export Data File... (Ctrl+E)",
            Location = new Point(140, 215),
            Width = 220,
            Height = 36,
            BackColor = Color.FromArgb(39, 174, 96),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat
        };
        _btnExecuteExport.FlatAppearance.BorderSize = 0;
        _btnExecuteExport.Click += async (s, e) => await ExecuteExportAsync();

        pnlExport.Controls.AddRange(new Control[] {
            lblSection, lblEntity, _cmbExportEntity,
            lblFormat, _rbFormatCsv, _rbFormatJson,
            _pnlDateRange, _btnExecuteExport
        });

        tab.Controls.Add(pnlExport);
    }

    private ImportEntityType GetSelectedImportEntityType()
    {
        return _cmbImportEntity.SelectedIndex switch
        {
            1 => ImportEntityType.StockItems,
            2 => ImportEntityType.Vouchers,
            _ => ImportEntityType.Ledgers
        };
    }

    private DuplicateAction GetSelectedDuplicateAction()
    {
        if (_rbUpdateDuplicates.Checked) return DuplicateAction.Update;
        if (_rbRejectDuplicates.Checked) return DuplicateAction.Reject;
        return DuplicateAction.Skip;
    }

    private void BrowseCsvFile()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Title = "Select CSV File to Import"
        };
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            _txtImportFilePath.Text = ofd.FileName;
        }
    }

    private async Task DownloadTemplateAsync()
    {
        var entityType = GetSelectedImportEntityType();
        using var sfd = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            FileName = $"Sample_Template_{entityType}.csv",
            Title = "Save Sample Template CSV"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            try
            {
                var template = await _importExportService.GenerateTemplateCsvAsync(entityType);
                await File.WriteAllTextAsync(sfd.FileName, template, Encoding.UTF8);
                MessageBox.Show($"Sample template successfully saved to:\n{sfd.FileName}", "Template Downloaded", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save template: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async Task AnalyzeAndPreviewAsync()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            MessageBox.Show("Please select an active company first.", "No Active Company", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string filePath = _txtImportFilePath.Text.Trim();
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            MessageBox.Show("Please browse and select a valid CSV file first.", "File Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        Cursor = Cursors.WaitCursor;
        _btnAnalyzePreview.Enabled = false;
        try
        {
            string csvContent = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            var entityType = GetSelectedImportEntityType();
            var dupAction = GetSelectedDuplicateAction();

            var preview = await _importExportService.PreviewImportCsvAsync(
                _companyContext.CurrentCompany.CompanyId,
                entityType,
                csvContent,
                dupAction);

            _currentPreview = preview;

            _dgvImportPreview.Rows.Clear();
            foreach (var row in preview.Rows)
            {
                int rowIdx = _dgvImportPreview.Rows.Add(
                    row.RowNumber,
                    row.Status.ToString(),
                    row.PrimaryIdentifier,
                    row.Details,
                    row.ErrorMessage
                );

                var dgvRow = _dgvImportPreview.Rows[rowIdx];
                if (row.Status == ImportRowStatus.Valid)
                {
                    dgvRow.DefaultCellStyle.BackColor = Color.FromArgb(240, 255, 244);
                    dgvRow.Cells[1].Style.ForeColor = Color.FromArgb(39, 174, 96);
                    dgvRow.Cells[1].Value = "Valid [✔]";
                }
                else if (row.Status == ImportRowStatus.Duplicate)
                {
                    dgvRow.DefaultCellStyle.BackColor = Color.FromArgb(255, 253, 235);
                    dgvRow.Cells[1].Style.ForeColor = Color.FromArgb(211, 84, 0);
                    dgvRow.Cells[1].Value = "Duplicate [!]";
                }
                else
                {
                    dgvRow.DefaultCellStyle.BackColor = Color.FromArgb(255, 240, 240);
                    dgvRow.Cells[1].Style.ForeColor = Color.FromArgb(192, 57, 43);
                    dgvRow.Cells[1].Value = "Error [✖]";
                }
            }

            _lblImportSummary.Text = $"Total Rows: {preview.TotalRows} | Valid: {preview.ValidCount} | Duplicates: {preview.DuplicateCount} | Errors: {preview.ErrorCount}";
            _lblImportSummary.ForeColor = preview.ErrorCount > 0 ? Color.FromArgb(192, 57, 43) : Color.FromArgb(39, 174, 96);

            _btnExecuteImport.Enabled = preview.CanExecute && (preview.ValidCount > 0 || (preview.DuplicateCount > 0 && dupAction == DuplicateAction.Update));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to analyze file: {ex.Message}", "Analysis Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
            _btnAnalyzePreview.Enabled = true;
        }
    }

    private async Task ExecuteImportAsync()
    {
        if (_currentPreview == null || !_currentPreview.CanExecute)
        {
            MessageBox.Show("Please run an analysis that passes validation before importing.", "Validation Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var dupAction = GetSelectedDuplicateAction();
        var confirm = MessageBox.Show(
            $"Are you sure you want to import {_currentPreview.ValidCount} new records into company '{_companyContext.CurrentCompany?.CompanyName}'?\n\nDuplicate Policy: {dupAction}",
            "Confirm Import Execution",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        Cursor = Cursors.WaitCursor;
        _btnExecuteImport.Enabled = false;
        try
        {
            var result = await _importExportService.ExecuteImportAsync(
                _companyContext.CurrentCompany!.CompanyId,
                _currentPreview.EntityType,
                _currentPreview,
                dupAction);

            if (result.Success)
            {
                MessageBox.Show(
                    $"Import successfully executed!\n\nInserted: {result.InsertedCount}\nUpdated: {result.UpdatedCount}\nSkipped: {result.SkippedCount}",
                    "Import Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                _dgvImportPreview.Rows.Clear();
                _currentPreview = null;
                _btnExecuteImport.Enabled = false;
                _lblImportSummary.Text = "Import completed successfully. Ready for next operation.";
                _lblImportSummary.ForeColor = Color.FromArgb(39, 174, 96);
            }
            else
            {
                string msg = string.Join("\n", result.Messages);
                MessageBox.Show($"Import failed:\n{msg}", "Import Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Execution error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private async Task ExecuteExportAsync()
    {
        if (!_companyContext.IsCompanyOpen || _companyContext.CurrentCompany == null)
        {
            MessageBox.Show("Please select an active company first.", "No Active Company", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var entityType = _cmbExportEntity.SelectedIndex switch
        {
            1 => ImportEntityType.StockItems,
            2 => ImportEntityType.Vouchers,
            _ => ImportEntityType.Ledgers
        };

        var format = _rbFormatJson.Checked ? ExportFormat.Json : ExportFormat.Csv;
        string ext = format == ExportFormat.Json ? "json" : "csv";

        using var sfd = new SaveFileDialog
        {
            Filter = format == ExportFormat.Json ? "JSON files (*.json)|*.json" : "CSV files (*.csv)|*.csv",
            FileName = $"{entityType}_{_companyContext.CurrentCompany.CompanyName}_{DateTime.Now:yyyyMMdd}.{ext}",
            Title = "Save Export File"
        };

        if (sfd.ShowDialog() == DialogResult.OK)
        {
            Cursor = Cursors.WaitCursor;
            try
            {
                var options = new ExportOptionsDto
                {
                    EntityType = entityType,
                    Format = format,
                    FromDate = entityType == ImportEntityType.Vouchers ? _dtpExportFrom.Value.Date : null,
                    ToDate = entityType == ImportEntityType.Vouchers ? _dtpExportTo.Value.Date : null
                };

                var content = await _importExportService.ExportDataAsync(_companyContext.CurrentCompany.CompanyId, options);
                await File.WriteAllTextAsync(sfd.FileName, content, Encoding.UTF8);

                MessageBox.Show($"Data successfully exported to:\n{sfd.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape)
        {
            Close();
            return true;
        }
        if (keyData == Keys.F5)
        {
            if (_tabMain.SelectedIndex == 0)
            {
                _ = AnalyzeAndPreviewAsync();
                return true;
            }
        }
        if (keyData == (Keys.Control | Keys.E))
        {
            _tabMain.SelectedIndex = 1;
            _ = ExecuteExportAsync();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
