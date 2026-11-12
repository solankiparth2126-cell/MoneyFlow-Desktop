using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;
using Microsoft.EntityFrameworkCore;

using GroupEntity = MoneyFlow.Core.Entities.Group;
using LedgerEntity = MoneyFlow.Core.Entities.Ledger;
using MoneyFlow.Data.Repositories;

namespace MoneyFlow.Services.ImportExport;

public class ImportExportService : IImportExportService
{
<<<<<<< Updated upstream
    private readonly AppDataContext _context;
=======
    private readonly ICompanyDataStore _dataStore;
>>>>>>> Stashed changes
    private readonly IUnitOfWork _uow;
    private readonly ILogger<ImportExportService> _logger;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public ImportExportService(
<<<<<<< Updated upstream
        AppDataContext context,
=======
>>>>>>> Stashed changes
        IUnitOfWork uow,
        ILogger<ImportExportService> logger,
        ICompanyDataStore? dataStore = null)
    {
        _dataStore = dataStore!;
        _uow = uow;
        _logger = logger;
    }

    internal ImportExportService(
        MoneyFlow.Data.AppDbContext context,
        IUnitOfWork uow,
        ILogger<ImportExportService> logger)
        : this(uow, logger, null)
    {
    }

    public Task<string> GenerateTemplateCsvAsync(ImportEntityType entityType)
    {
        var records = new List<List<string>>();

        switch (entityType)
        {
            case ImportEntityType.Ledgers:
                records.Add(new List<string> { "Ledger Name", "Group Name", "Opening Balance", "Dr/Cr", "Description" });
                records.Add(new List<string> { "Acme Corporation", "Sundry Debtors", "15000.00", "Dr", "Wholesale customer" });
                records.Add(new List<string> { "Office Electricity", "Indirect Expenses", "2500.00", "Dr", "Utility expense" });
                records.Add(new List<string> { "Premier Bank A/c", "Bank Accounts", "50000.00", "Dr", "Operating bank account" });
                break;

            case ImportEntityType.StockItems:
                records.Add(new List<string> { "Item Name", "Unit Symbol", "Opening Quantity", "Opening Rate", "Description" });
                records.Add(new List<string> { "Premium Basmati Rice 5kg", "Box", "25", "450.00", "5kg packaged box" });
                records.Add(new List<string> { "Organic Green Tea 100g", "Nos", "100", "120.00", "Herbal green tea pouch" });
                break;

            case ImportEntityType.Vouchers:
                records.Add(new List<string> { "Voucher Type", "Voucher Date", "Voucher Number", "Reference", "Debit Ledger", "Credit Ledger", "Amount", "Narration" });
                records.Add(new List<string> { "Receipt", "15-May-2026", "REC-2026-001", "CHQ-10024", "Cash", "Sales", "12000.00", "Cash sale received" });
                records.Add(new List<string> { "Payment", "18-May-2026", "PAY-2026-001", "NEFT-5542", "Purchase", "Cash", "8500.00", "Vendor advance payout" });
                break;
        }

        return Task.FromResult(CsvUtility.WriteCsv(records));
    }

    public async Task<ImportPreviewResultDto> PreviewImportCsvAsync(
        int companyId,
        ImportEntityType entityType,
        string csvContent,
        DuplicateAction duplicateAction,
        CancellationToken ct = default)
    {
        var records = CsvUtility.ParseCsv(csvContent);
        var preview = new ImportPreviewResultDto { EntityType = entityType };

        if (records.Count < 2)
        {
            preview.ErrorCount = 1;
            preview.Rows.Add(new ImportPreviewRowDto
            {
                RowNumber = 1,
                Status = ImportRowStatus.Error,
                ErrorMessage = "CSV file must contain a header row and at least one data row."
            });
            return preview;
        }

        var header = records[0].Select(h => h.Trim()).ToList();
        var dataRows = records.Skip(1).ToList();
        preview.TotalRows = dataRows.Count;

        switch (entityType)
        {
            case ImportEntityType.Ledgers:
                await PreviewLedgersAsync(companyId, header, dataRows, duplicateAction, preview, ct);
                break;

            case ImportEntityType.StockItems:
                await PreviewStockItemsAsync(companyId, header, dataRows, duplicateAction, preview, ct);
                break;

            case ImportEntityType.Vouchers:
                await PreviewVouchersAsync(companyId, header, dataRows, duplicateAction, preview, ct);
                break;
        }

        preview.ValidCount = preview.Rows.Count(r => r.Status == ImportRowStatus.Valid);
        preview.DuplicateCount = preview.Rows.Count(r => r.Status == ImportRowStatus.Duplicate);
        preview.ErrorCount = preview.Rows.Count(r => r.Status == ImportRowStatus.Error);

        return preview;
    }

    private async Task PreviewLedgersAsync(
        int companyId,
        List<string> header,
        List<List<string>> dataRows,
        DuplicateAction duplicateAction,
        ImportPreviewResultDto preview,
        CancellationToken ct)
    {
        int nameIdx = FindColumnIndex(header, "Ledger Name", "Name", "Ledger");
        int groupIdx = FindColumnIndex(header, "Group Name", "Group");
        int balIdx = FindColumnIndex(header, "Opening Balance", "Balance", "Opening");
        int drCrIdx = FindColumnIndex(header, "Dr/Cr", "Type", "Balance Type");
        int descIdx = FindColumnIndex(header, "Description", "Narration", "Note");

<<<<<<< Updated upstream
        var existingGroups = await _context.Groups
            
=======
        var existingGroups = _dataStore.GetEntityList<GroupEntity>()
>>>>>>> Stashed changes
            .Where(g => g.CompanyId == companyId && g.IsActive)
            .ToDictionary(g => g.GroupName.ToLowerInvariant(), g => g);

<<<<<<< Updated upstream
        var existingLedgers = await _context.Ledgers
            
=======
        var existingLedgers = _dataStore.GetEntityList<LedgerEntity>()
>>>>>>> Stashed changes
            .Where(l => l.CompanyId == companyId)
            .ToDictionary(l => l.LedgerName.ToLowerInvariant(), l => l);

        var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < dataRows.Count; i++)
        {
            var row = dataRows[i];
            int rowNum = i + 2;
            string name = GetField(row, nameIdx);
            string groupName = GetField(row, groupIdx);
            string balStr = GetField(row, balIdx);
            string drCrStr = GetField(row, drCrIdx);
            string desc = GetField(row, descIdx);

            var previewRow = new ImportPreviewRowDto
            {
                RowNumber = rowNum,
                PrimaryIdentifier = name,
                RawFields = new Dictionary<string, string>
                {
                    ["Name"] = name,
                    ["Group"] = groupName,
                    ["Balance"] = balStr,
                    ["DrCr"] = drCrStr,
                    ["Description"] = desc
                }
            };

            if (string.IsNullOrWhiteSpace(name))
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = "Ledger Name is required.";
                preview.Rows.Add(previewRow);
                continue;
            }

            if (processedNames.Contains(name))
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = $"Duplicate ledger name '{name}' within the import file.";
                preview.Rows.Add(previewRow);
                continue;
            }
            processedNames.Add(name);

            if (string.IsNullOrWhiteSpace(groupName) || !existingGroups.TryGetValue(groupName.ToLowerInvariant(), out var matchedGroup))
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = $"Group '{groupName}' does not exist in company Chart of Accounts.";
                preview.Rows.Add(previewRow);
                continue;
            }

            decimal balance = 0m;
            if (!string.IsNullOrWhiteSpace(balStr) && !decimal.TryParse(balStr, NumberStyles.Any, CultureInfo.InvariantCulture, out balance))
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = $"Invalid numeric Opening Balance '{balStr}'.";
                preview.Rows.Add(previewRow);
                continue;
            }
            if (balance < 0)
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = "Opening balance cannot be negative.";
                preview.Rows.Add(previewRow);
                continue;
            }

            var balanceType = BalanceType.Debit;
            if (!string.IsNullOrWhiteSpace(drCrStr))
            {
                var lower = drCrStr.Trim().ToLowerInvariant();
                if (lower == "cr" || lower == "credit")
                {
                    balanceType = BalanceType.Credit;
                }
                else if (lower != "dr" && lower != "debit")
                {
                    previewRow.Status = ImportRowStatus.Error;
                    previewRow.ErrorMessage = $"Invalid Balance Type '{drCrStr}'. Use 'Dr' or 'Cr'.";
                    preview.Rows.Add(previewRow);
                    continue;
                }
            }

            previewRow.Details = $"Group: {matchedGroup.GroupName} | Balance: ₹{balance:N2} {balanceType}";

            if (existingLedgers.ContainsKey(name.ToLowerInvariant()))
            {
                if (duplicateAction == DuplicateAction.Reject)
                {
                    previewRow.Status = ImportRowStatus.Error;
                    previewRow.ErrorMessage = $"Ledger '{name}' already exists in company (Batch rejected on duplicate).";
                }
                else
                {
                    previewRow.Status = ImportRowStatus.Duplicate;
                    previewRow.ErrorMessage = duplicateAction == DuplicateAction.Update
                        ? $"Ledger '{name}' already exists (Will be updated)."
                        : $"Ledger '{name}' already exists (Will be skipped).";
                }
            }
            else
            {
                previewRow.Status = ImportRowStatus.Valid;
            }

            preview.Rows.Add(previewRow);
        }
    }

    private async Task PreviewStockItemsAsync(
        int companyId,
        List<string> header,
        List<List<string>> dataRows,
        DuplicateAction duplicateAction,
        ImportPreviewResultDto preview,
        CancellationToken ct)
    {
        int nameIdx = FindColumnIndex(header, "Item Name", "Name", "Stock Item");
        int unitIdx = FindColumnIndex(header, "Unit Symbol", "Unit", "UOM");
        int qtyIdx = FindColumnIndex(header, "Opening Quantity", "Quantity", "Qty");
        int rateIdx = FindColumnIndex(header, "Opening Rate", "Rate", "Price");
        int descIdx = FindColumnIndex(header, "Description", "Narration", "Note");

<<<<<<< Updated upstream
        var existingUnits = await _context.Units
            
=======
        var existingUnits = _dataStore.GetEntityList<Unit>()
>>>>>>> Stashed changes
            .Where(u => u.CompanyId == companyId && u.IsActive)
            .ToDictionary(u => u.UnitName.ToLowerInvariant(), u => u);

<<<<<<< Updated upstream
        var existingItems = await _context.StockItems
            
=======
        var existingItems = _dataStore.GetEntityList<StockItem>()
>>>>>>> Stashed changes
            .Where(s => s.CompanyId == companyId)
            .ToDictionary(s => s.ItemName.ToLowerInvariant(), s => s);

        var processedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < dataRows.Count; i++)
        {
            var row = dataRows[i];
            int rowNum = i + 2;
            string name = GetField(row, nameIdx);
            string unitSymbol = GetField(row, unitIdx);
            string qtyStr = GetField(row, qtyIdx);
            string rateStr = GetField(row, rateIdx);
            string desc = GetField(row, descIdx);

            var previewRow = new ImportPreviewRowDto
            {
                RowNumber = rowNum,
                PrimaryIdentifier = name,
                RawFields = new Dictionary<string, string>
                {
                    ["Name"] = name,
                    ["Unit"] = unitSymbol,
                    ["Quantity"] = qtyStr,
                    ["Rate"] = rateStr,
                    ["Description"] = desc
                }
            };

            if (string.IsNullOrWhiteSpace(name))
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = "Stock Item Name is required.";
                preview.Rows.Add(previewRow);
                continue;
            }

            if (processedNames.Contains(name))
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = $"Duplicate item name '{name}' within the import file.";
                preview.Rows.Add(previewRow);
                continue;
            }
            processedNames.Add(name);

            if (string.IsNullOrWhiteSpace(unitSymbol) || !existingUnits.TryGetValue(unitSymbol.ToLowerInvariant(), out var matchedUnit))
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = $"Unit '{unitSymbol}' does not exist in company Units master.";
                preview.Rows.Add(previewRow);
                continue;
            }

            decimal qty = 0m;
            if (!string.IsNullOrWhiteSpace(qtyStr) && !decimal.TryParse(qtyStr, NumberStyles.Any, CultureInfo.InvariantCulture, out qty))
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = $"Invalid numeric Opening Quantity '{qtyStr}'.";
                preview.Rows.Add(previewRow);
                continue;
            }

            decimal rate = 0m;
            if (!string.IsNullOrWhiteSpace(rateStr) && !decimal.TryParse(rateStr, NumberStyles.Any, CultureInfo.InvariantCulture, out rate))
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = $"Invalid numeric Opening Rate '{rateStr}'.";
                preview.Rows.Add(previewRow);
                continue;
            }

            if (qty < 0 || rate < 0)
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = "Opening Quantity and Rate cannot be negative.";
                preview.Rows.Add(previewRow);
                continue;
            }

            decimal val = qty * rate;
            previewRow.Details = $"Unit: {matchedUnit.UnitName} | Qty: {qty:N2} @ ₹{rate:N2} = ₹{val:N2}";

            if (existingItems.ContainsKey(name.ToLowerInvariant()))
            {
                if (duplicateAction == DuplicateAction.Reject)
                {
                    previewRow.Status = ImportRowStatus.Error;
                    previewRow.ErrorMessage = $"Stock Item '{name}' already exists (Batch rejected on duplicate).";
                }
                else
                {
                    previewRow.Status = ImportRowStatus.Duplicate;
                    previewRow.ErrorMessage = duplicateAction == DuplicateAction.Update
                        ? $"Stock Item '{name}' already exists (Will be updated)."
                        : $"Stock Item '{name}' already exists (Will be skipped).";
                }
            }
            else
            {
                previewRow.Status = ImportRowStatus.Valid;
            }

            preview.Rows.Add(previewRow);
        }
    }

    private async Task PreviewVouchersAsync(
        int companyId,
        List<string> header,
        List<List<string>> dataRows,
        DuplicateAction duplicateAction,
        ImportPreviewResultDto preview,
        CancellationToken ct)
    {
        int typeIdx = FindColumnIndex(header, "Voucher Type", "Type");
        int dateIdx = FindColumnIndex(header, "Voucher Date", "Date");
        int numIdx = FindColumnIndex(header, "Voucher Number", "Voucher No", "Number");
        int refIdx = FindColumnIndex(header, "Reference", "Ref");
        int drLedgerIdx = FindColumnIndex(header, "Debit Ledger", "Dr Ledger", "Debit Account");
        int crLedgerIdx = FindColumnIndex(header, "Credit Ledger", "Cr Ledger", "Credit Account");
        int amtIdx = FindColumnIndex(header, "Amount", "Total Amount");
        int narrIdx = FindColumnIndex(header, "Narration", "Remarks");

<<<<<<< Updated upstream
        var existingLedgers = await _context.Ledgers
            
=======
        var existingLedgers = _dataStore.GetEntityList<LedgerEntity>()
>>>>>>> Stashed changes
            .Where(l => l.CompanyId == companyId && l.IsActive)
            .ToDictionary(l => l.LedgerName.ToLowerInvariant(), l => l);

<<<<<<< Updated upstream
        var existingVouchers = await _context.Vouchers
            
=======
        var existingVouchers = _dataStore.GetEntityList<Voucher>()
>>>>>>> Stashed changes
            .Where(v => v.CompanyId == companyId && !v.IsDeleted)
            .ToDictionary(v => v.VoucherNumber.ToLowerInvariant(), v => v);

<<<<<<< Updated upstream
        var voucherTypes = await _context.VoucherTypes
            
            .ToDictionaryAsync(t => t.Name.ToLowerInvariant(), t => t, ct);
=======
        var voucherTypes = _dataStore.GetEntityList<VoucherType>()
            .ToDictionary(t => t.Name.ToLowerInvariant(), t => t);
>>>>>>> Stashed changes

        var processedNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < dataRows.Count; i++)
        {
            var row = dataRows[i];
            int rowNum = i + 2;
            string typeName = GetField(row, typeIdx);
            string dateStr = GetField(row, dateIdx);
            string vchNo = GetField(row, numIdx);
            string refNo = GetField(row, refIdx);
            string drName = GetField(row, drLedgerIdx);
            string crName = GetField(row, crLedgerIdx);
            string amtStr = GetField(row, amtIdx);
            string narr = GetField(row, narrIdx);

            var previewRow = new ImportPreviewRowDto
            {
                RowNumber = rowNum,
                PrimaryIdentifier = vchNo,
                RawFields = new Dictionary<string, string>
                {
                    ["Type"] = typeName,
                    ["Date"] = dateStr,
                    ["Number"] = vchNo,
                    ["Reference"] = refNo,
                    ["DebitLedger"] = drName,
                    ["CreditLedger"] = crName,
                    ["Amount"] = amtStr,
                    ["Narration"] = narr
                }
            };

            if (string.IsNullOrWhiteSpace(vchNo))
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = "Voucher Number is required.";
                preview.Rows.Add(previewRow);
                continue;
            }

            if (processedNumbers.Contains(vchNo))
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = $"Duplicate voucher number '{vchNo}' within the import file.";
                preview.Rows.Add(previewRow);
                continue;
            }
            processedNumbers.Add(vchNo);

            if (string.IsNullOrWhiteSpace(typeName) || !voucherTypes.TryGetValue(typeName.ToLowerInvariant(), out var matchedType))
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = $"Voucher Type '{typeName}' is not recognized.";
                preview.Rows.Add(previewRow);
                continue;
            }

            if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var vchDate) &&
                !DateTime.TryParse(dateStr, out vchDate))
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = $"Invalid Voucher Date '{dateStr}'.";
                preview.Rows.Add(previewRow);
                continue;
            }

            if (string.IsNullOrWhiteSpace(drName) || !existingLedgers.TryGetValue(drName.ToLowerInvariant(), out var drLedger))
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = $"Debit Ledger '{drName}' does not exist.";
                preview.Rows.Add(previewRow);
                continue;
            }

            if (string.IsNullOrWhiteSpace(crName) || !existingLedgers.TryGetValue(crName.ToLowerInvariant(), out var crLedger))
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = $"Credit Ledger '{crName}' does not exist.";
                preview.Rows.Add(previewRow);
                continue;
            }

            if (drLedger.LedgerId == crLedger.LedgerId)
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = "Debit and Credit ledgers cannot be identical.";
                preview.Rows.Add(previewRow);
                continue;
            }

            if (!decimal.TryParse(amtStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amt) || amt <= 0)
            {
                previewRow.Status = ImportRowStatus.Error;
                previewRow.ErrorMessage = $"Invalid positive transaction amount '{amtStr}'.";
                preview.Rows.Add(previewRow);
                continue;
            }

            previewRow.Details = $"Type: {matchedType.Name} | Date: {vchDate:dd-MMM-yyyy} | Dr: {drLedger.LedgerName} | Cr: {crLedger.LedgerName} | Amount: ₹{amt:N2}";

            if (existingVouchers.ContainsKey(vchNo.ToLowerInvariant()))
            {
                if (duplicateAction == DuplicateAction.Reject)
                {
                    previewRow.Status = ImportRowStatus.Error;
                    previewRow.ErrorMessage = $"Voucher '{vchNo}' already exists (Batch rejected on duplicate).";
                }
                else
                {
                    previewRow.Status = ImportRowStatus.Duplicate;
                    previewRow.ErrorMessage = duplicateAction == DuplicateAction.Update
                        ? $"Voucher '{vchNo}' already exists (Will be replaced/updated)."
                        : $"Voucher '{vchNo}' already exists (Will be skipped).";
                }
            }
            else
            {
                previewRow.Status = ImportRowStatus.Valid;
            }

            preview.Rows.Add(previewRow);
        }
    }

    public async Task<ImportExecutionResultDto> ExecuteImportAsync(
        int companyId,
        ImportEntityType entityType,
        ImportPreviewResultDto preview,
        DuplicateAction duplicateAction,
        CancellationToken ct = default)
    {
        var result = new ImportExecutionResultDto();

        if (preview.ErrorCount > 0)
        {
            result.Success = false;
            result.Messages.Add($"Cannot execute import: {preview.ErrorCount} validation error(s) must be resolved first.");
            return result;
        }

<<<<<<< Updated upstream
=======
        IDbTransactionContext? transaction = null;
        try
        {
            transaction = await _uow.BeginTransactionAsync(ct);
        }
        catch { }

>>>>>>> Stashed changes
        try
        {
            switch (entityType)
            {
                case ImportEntityType.Ledgers:
                    await ExecuteLedgersImportAsync(companyId, preview, duplicateAction, result, ct);
                    break;

                case ImportEntityType.StockItems:
                    await ExecuteStockItemsImportAsync(companyId, preview, duplicateAction, result, ct);
                    break;

                case ImportEntityType.Vouchers:
                    await ExecuteVouchersImportAsync(companyId, preview, duplicateAction, result, ct);
                    break;
            }

            await _uow.SaveChangesAsync(ct);
            result.Success = true;
            result.Messages.Add($"Import completed successfully. Total processed: {result.TotalProcessed} (Inserted: {result.InsertedCount}, Updated: {result.UpdatedCount}, Skipped: {result.SkippedCount}).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed executing import for company {CompanyId}", companyId);
            result.Success = false;
            result.ErrorCount++;
            result.Messages.Add($"Import aborted: {ex.Message}");
        }

        return result;
    }

    private async Task ExecuteLedgersImportAsync(
        int companyId,
        ImportPreviewResultDto preview,
        DuplicateAction duplicateAction,
        ImportExecutionResultDto result,
        CancellationToken ct)
    {
        var groups = _dataStore.GetEntityList<GroupEntity>()
            .Where(g => g.CompanyId == companyId && g.IsActive)
            .ToDictionary(g => g.GroupName.ToLowerInvariant(), g => g);

        var existingLedgers = _dataStore.GetEntityList<LedgerEntity>()
            .Where(l => l.CompanyId == companyId)
            .ToDictionary(l => l.LedgerName.ToLowerInvariant(), l => l);

        foreach (var row in preview.Rows)
        {
            if (row.Status == ImportRowStatus.Error) continue;

            string name = row.RawFields["Name"].Trim();
            string groupName = row.RawFields["Group"].Trim();
            decimal.TryParse(row.RawFields["Balance"], NumberStyles.Any, CultureInfo.InvariantCulture, out var bal);
            string drCr = row.RawFields["DrCr"].Trim().ToLowerInvariant();
            var bType = (drCr == "cr" || drCr == "credit") ? BalanceType.Credit : BalanceType.Debit;
            string desc = row.RawFields.TryGetValue("Description", out var d) ? d : string.Empty;

            var group = groups[groupName.ToLowerInvariant()];
            result.TotalProcessed++;

            if (existingLedgers.TryGetValue(name.ToLowerInvariant(), out var existing))
            {
                if (duplicateAction == DuplicateAction.Skip)
                {
                    result.SkippedCount++;
                    continue;
                }
                else if (duplicateAction == DuplicateAction.Update)
                {
                    existing.GroupId = group.GroupId;
                    existing.OpeningBalance = bal;
                    existing.OpeningBalanceType = bType;
                    existing.UpdatedAt = DateTime.Now;
                    result.UpdatedCount++;
                }
            }
            else
            {
                int nextLId = _dataStore.GetEntityList<LedgerEntity>().Count > 0 ? _dataStore.GetEntityList<LedgerEntity>().Max(l => l.LedgerId) + 1 : 1;
                var newLedger = new LedgerEntity
                {
                    LedgerId = nextLId,
                    CompanyId = companyId,
                    GroupId = group.GroupId,
                    LedgerName = name,
                    OpeningBalance = bal,
                    OpeningBalanceType = bType,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
<<<<<<< Updated upstream
                _context.Add(newLedger);
=======
                _dataStore.GetEntityList<LedgerEntity>().Add(newLedger);
>>>>>>> Stashed changes
                result.InsertedCount++;
            }
        }
    }

    private async Task ExecuteStockItemsImportAsync(
        int companyId,
        ImportPreviewResultDto preview,
        DuplicateAction duplicateAction,
        ImportExecutionResultDto result,
        CancellationToken ct)
    {
        var units = _dataStore.GetEntityList<Unit>()
            .Where(u => u.CompanyId == companyId && u.IsActive)
            .ToDictionary(u => u.UnitName.ToLowerInvariant(), u => u);

        var existingItems = _dataStore.GetEntityList<StockItem>()
            .Where(s => s.CompanyId == companyId)
            .ToDictionary(s => s.ItemName.ToLowerInvariant(), s => s);

        foreach (var row in preview.Rows)
        {
            if (row.Status == ImportRowStatus.Error) continue;

            string name = row.RawFields["Name"].Trim();
            string unitSymbol = row.RawFields["Unit"].Trim();
            decimal.TryParse(row.RawFields["Quantity"], NumberStyles.Any, CultureInfo.InvariantCulture, out var qty);
            decimal.TryParse(row.RawFields["Rate"], NumberStyles.Any, CultureInfo.InvariantCulture, out var rate);
            string desc = row.RawFields.TryGetValue("Description", out var d) ? d : string.Empty;

            var unit = units[unitSymbol.ToLowerInvariant()];
            decimal val = qty * rate;
            result.TotalProcessed++;

            if (existingItems.TryGetValue(name.ToLowerInvariant(), out var existing))
            {
                if (duplicateAction == DuplicateAction.Skip)
                {
                    result.SkippedCount++;
                    continue;
                }
                else if (duplicateAction == DuplicateAction.Update)
                {
                    existing.UnitId = unit.UnitId;
                    existing.OpeningQuantity = qty;
                    existing.OpeningRate = rate;
                    existing.OpeningValue = val;
                    existing.UpdatedAt = DateTime.Now;
                    result.UpdatedCount++;
                }
            }
            else
            {
                int nextSId = _dataStore.GetEntityList<StockItem>().Count > 0 ? _dataStore.GetEntityList<StockItem>().Max(s => s.StockItemId) + 1 : 1;
                var newItem = new StockItem
                {
                    StockItemId = nextSId,
                    CompanyId = companyId,
                    ItemName = name,
                    UnitId = unit.UnitId,
                    OpeningQuantity = qty,
                    OpeningRate = rate,
                    OpeningValue = val,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };
<<<<<<< Updated upstream
                _context.Add(newItem);
=======
                _dataStore.GetEntityList<StockItem>().Add(newItem);
>>>>>>> Stashed changes
                result.InsertedCount++;
            }
        }
    }

    private async Task ExecuteVouchersImportAsync(
        int companyId,
        ImportPreviewResultDto preview,
        DuplicateAction duplicateAction,
        ImportExecutionResultDto result,
        CancellationToken ct)
    {
        var ledgers = _dataStore.GetEntityList<LedgerEntity>()
            .Where(l => l.CompanyId == companyId && l.IsActive)
            .ToDictionary(l => l.LedgerName.ToLowerInvariant(), l => l);

        var voucherTypes = _dataStore.GetEntityList<VoucherType>()
            .ToDictionary(t => t.Name.ToLowerInvariant(), t => t);

<<<<<<< Updated upstream
        var existingVouchers = await _context.Vouchers
            
=======
        var existingVouchers = _dataStore.GetEntityList<Voucher>()
>>>>>>> Stashed changes
            .Where(v => v.CompanyId == companyId && !v.IsDeleted)
            .ToDictionary(v => v.VoucherNumber.ToLowerInvariant(), v => v);

        foreach (var row in preview.Rows)
        {
            if (row.Status == ImportRowStatus.Error) continue;

            string typeName = row.RawFields["Type"].Trim();
            string dateStr = row.RawFields["Date"].Trim();
            string vchNo = row.RawFields["Number"].Trim();
            string refNo = row.RawFields["Reference"].Trim();
            string drName = row.RawFields["DebitLedger"].Trim();
            string crName = row.RawFields["CreditLedger"].Trim();
            decimal.TryParse(row.RawFields["Amount"], NumberStyles.Any, CultureInfo.InvariantCulture, out var amt);
            string narr = row.RawFields.TryGetValue("Narration", out var n) ? n : string.Empty;

            DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var vchDate);
            var vchType = voucherTypes[typeName.ToLowerInvariant()];
            var drLedger = ledgers[drName.ToLowerInvariant()];
            var crLedger = ledgers[crName.ToLowerInvariant()];

            result.TotalProcessed++;

            if (existingVouchers.TryGetValue(vchNo.ToLowerInvariant(), out var existing))
            {
                if (duplicateAction == DuplicateAction.Skip)
                {
                    result.SkippedCount++;
                    continue;
                }
                else if (duplicateAction == DuplicateAction.Update)
                {
                    existing.VoucherTypeId = vchType.VoucherTypeId;
                    existing.VoucherDate = vchDate;
                    existing.ReferenceNumber = refNo;
                    existing.Narration = narr;

<<<<<<< Updated upstream
                    _context.RemoveRange(existing.VoucherEntries);
                    existing.VoucherEntries.Add(new VoucherEntry { VoucherId = existing.VoucherId, LedgerId = drLedger.LedgerId, Debit = amt, Credit = 0m, Narration = narr });
                    existing.VoucherEntries.Add(new VoucherEntry { VoucherId = existing.VoucherId, LedgerId = crLedger.LedgerId, Debit = 0m, Credit = amt, Narration = narr });
=======
                    _dataStore.GetEntityList<VoucherEntry>().RemoveAll(ve => ve.VoucherId == existing.VoucherId);
                    int nextVEId = _dataStore.GetEntityList<VoucherEntry>().Count > 0 ? _dataStore.GetEntityList<VoucherEntry>().Max(ve => ve.VoucherEntryId) + 1 : 1;
                    _dataStore.GetEntityList<VoucherEntry>().Add(new VoucherEntry { VoucherEntryId = nextVEId++, VoucherId = existing.VoucherId, LedgerId = drLedger.LedgerId, Debit = amt, Credit = 0m, Narration = narr });
                    _dataStore.GetEntityList<VoucherEntry>().Add(new VoucherEntry { VoucherEntryId = nextVEId++, VoucherId = existing.VoucherId, LedgerId = crLedger.LedgerId, Debit = 0m, Credit = amt, Narration = narr });
>>>>>>> Stashed changes
                    result.UpdatedCount++;
                }
            }
            else
            {
                int nextVId = _dataStore.GetEntityList<Voucher>().Count > 0 ? _dataStore.GetEntityList<Voucher>().Max(v => v.VoucherId) + 1 : 1;
                var voucher = new Voucher
                {
                    VoucherId = nextVId,
                    CompanyId = companyId,
                    VoucherTypeId = vchType.VoucherTypeId,
                    VoucherNumber = vchNo,
                    VoucherDate = vchDate,
                    ReferenceNumber = refNo,
                    Narration = narr,
                    IsDeleted = false,
                    CreatedAt = DateTime.Now
                };
<<<<<<< Updated upstream
                _context.Add(voucher);
                await _context.SaveChangesAsync(ct);

                _context.Add(new VoucherEntry { VoucherId = voucher.VoucherId, LedgerId = drLedger.LedgerId, Debit = amt, Credit = 0m, Narration = narr });
                _context.Add(new VoucherEntry { VoucherId = voucher.VoucherId, LedgerId = crLedger.LedgerId, Debit = 0m, Credit = amt, Narration = narr });
=======
                _dataStore.GetEntityList<Voucher>().Add(voucher);

                int nextVEId2 = _dataStore.GetEntityList<VoucherEntry>().Count > 0 ? _dataStore.GetEntityList<VoucherEntry>().Max(ve => ve.VoucherEntryId) + 1 : 1;
                _dataStore.GetEntityList<VoucherEntry>().Add(new VoucherEntry { VoucherEntryId = nextVEId2++, VoucherId = voucher.VoucherId, LedgerId = drLedger.LedgerId, Debit = amt, Credit = 0m, Narration = narr });
                _dataStore.GetEntityList<VoucherEntry>().Add(new VoucherEntry { VoucherEntryId = nextVEId2++, VoucherId = voucher.VoucherId, LedgerId = crLedger.LedgerId, Debit = 0m, Credit = amt, Narration = narr });
>>>>>>> Stashed changes
                result.InsertedCount++;
            }
        }
    }

    public async Task<string> ExportDataAsync(int companyId, ExportOptionsDto options, CancellationToken ct = default)
    {
        switch (options.EntityType)
        {
            case ImportEntityType.Ledgers:
<<<<<<< Updated upstream
                var ledgers = await _context.Ledgers
                    
                    
                    .Where(l => l.CompanyId == companyId && l.IsActive)
                    .OrderBy(l => l.LedgerName)
                    .ToListAsync();
=======
                var allGroups = _dataStore.GetEntityList<GroupEntity>().ToDictionary(g => g.GroupId);
                var ledgers = _dataStore.GetEntityList<LedgerEntity>()
                    .Where(l => l.CompanyId == companyId && l.IsActive)
                    .OrderBy(l => l.LedgerName)
                    .ToList();
                foreach (var l in ledgers)
                {
                    if (l.Group == null && allGroups.TryGetValue(l.GroupId, out var grp)) l.Group = grp;
                }
>>>>>>> Stashed changes

                if (options.Format == ExportFormat.Json)
                {
                    var jsonData = ledgers.Select(l => new
                    {
                        l.LedgerName,
                        GroupName = l.Group?.GroupName ?? string.Empty,
                        l.OpeningBalance,
                        OpeningBalanceType = l.OpeningBalanceType.ToString()
                    });
                    return JsonSerializer.Serialize(jsonData, new JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    var rows = new List<List<string>>
                    {
                        new() { "Ledger Name", "Group Name", "Opening Balance", "Dr/Cr" }
                    };
                    foreach (var l in ledgers)
                    {
                        rows.Add(new List<string>
                        {
                            l.LedgerName,
                            l.Group?.GroupName ?? string.Empty,
                            l.OpeningBalance.ToString("F2", CultureInfo.InvariantCulture),
                            l.OpeningBalanceType.ToString()
                        });
                    }
                    return CsvUtility.WriteCsv(rows);
                }

            case ImportEntityType.StockItems:
<<<<<<< Updated upstream
                var items = _context.StockItems
                    
                    
                    .Where(s => s.CompanyId == companyId && s.IsActive)
                    .OrderBy(s => s.ItemName)
                    .ToList();
=======
                var allUnits = _dataStore.GetEntityList<Unit>().ToDictionary(u => u.UnitId);
                var items = _dataStore.GetEntityList<StockItem>()
                    .Where(s => s.CompanyId == companyId && s.IsActive)
                    .OrderBy(s => s.ItemName)
                    .ToList();
                foreach (var s in items)
                {
                    if (s.Unit == null && s.UnitId.HasValue && allUnits.TryGetValue(s.UnitId.Value, out var unt)) s.Unit = unt;
                }
>>>>>>> Stashed changes

                if (options.Format == ExportFormat.Json)
                {
                    var jsonData = items.Select(s => new
                    {
                        s.ItemName,
                        Unit = s.Unit?.UnitName ?? string.Empty,
                        s.OpeningQuantity,
                        s.OpeningRate,
                        s.OpeningValue
                    });
                    return JsonSerializer.Serialize(jsonData, new JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    var rows = new List<List<string>>
                    {
                        new() { "Item Name", "Unit Symbol", "Opening Quantity", "Opening Rate", "Opening Value" }
                    };
                    foreach (var s in items)
                    {
                        rows.Add(new List<string>
                        {
                            s.ItemName,
                            s.Unit?.UnitName ?? string.Empty,
                            s.OpeningQuantity.ToString("F2", CultureInfo.InvariantCulture),
                            s.OpeningRate.ToString("F2", CultureInfo.InvariantCulture),
                            s.OpeningValue.ToString("F2", CultureInfo.InvariantCulture)
                        });
                    }
                    return CsvUtility.WriteCsv(rows);
                }

            case ImportEntityType.Vouchers:
<<<<<<< Updated upstream
                var query = _context.Vouchers
                    
                    
                    
                        
=======
                var allTypes = _dataStore.GetEntityList<VoucherType>().ToDictionary(vt => vt.VoucherTypeId);
                var allLedgersDict = _dataStore.GetEntityList<LedgerEntity>().ToDictionary(l => l.LedgerId);
                var allEntries = _dataStore.GetEntityList<VoucherEntry>();

                var q = _dataStore.GetEntityList<Voucher>()
>>>>>>> Stashed changes
                    .Where(v => v.CompanyId == companyId && !v.IsDeleted);

                if (options.FromDate.HasValue) q = q.Where(v => v.VoucherDate >= options.FromDate.Value);
                if (options.ToDate.HasValue) q = q.Where(v => v.VoucherDate <= options.ToDate.Value);

                var vouchers = q
                    .OrderBy(v => v.VoucherDate)
                    .ThenBy(v => v.VoucherId)
<<<<<<< Updated upstream
                    .ToListAsync();
=======
                    .ToList();

                foreach (var v in vouchers)
                {
                    if (v.VoucherType == null && allTypes.TryGetValue(v.VoucherTypeId, out var vt)) v.VoucherType = vt;
                    if (v.VoucherEntries.Count == 0) v.VoucherEntries = allEntries.Where(e => e.VoucherId == v.VoucherId).ToList();
                    foreach (var e in v.VoucherEntries)
                    {
                        if (e.Ledger == null && allLedgersDict.TryGetValue(e.LedgerId, out var lg)) e.Ledger = lg;
                    }
                }
>>>>>>> Stashed changes

                if (options.Format == ExportFormat.Json)
                {
                    var jsonData = vouchers.Select(v => new
                    {
                        v.VoucherNumber,
                        Type = v.VoucherType?.Name ?? string.Empty,
                        Date = v.VoucherDate.ToString("yyyy-MM-dd"),
                        v.ReferenceNumber,
                        v.Narration,
                        Entries = v.VoucherEntries.Select(e => new
                        {
                            Ledger = e.Ledger?.LedgerName ?? string.Empty,
                            e.Debit,
                            e.Credit,
                            e.Narration
                        })
                    });
                    return JsonSerializer.Serialize(jsonData, new JsonSerializerOptions { WriteIndented = true });
                }
                else
                {
                    var rows = new List<List<string>>
                    {
                        new() { "Voucher Type", "Voucher Date", "Voucher Number", "Reference", "Debit Ledger", "Credit Ledger", "Amount", "Narration" }
                    };
                    foreach (var v in vouchers)
                    {
                        var dr = v.VoucherEntries.FirstOrDefault(e => e.Debit > 0)?.Ledger?.LedgerName ?? "—";
                        var cr = v.VoucherEntries.FirstOrDefault(e => e.Credit > 0)?.Ledger?.LedgerName ?? "—";
                        decimal amt = v.VoucherEntries.Sum(e => e.Debit);

                        rows.Add(new List<string>
                        {
                            v.VoucherType?.Name ?? string.Empty,
                            v.VoucherDate.ToString("dd-MMM-yyyy"),
                            v.VoucherNumber,
                            v.ReferenceNumber,
                            dr,
                            cr,
                            amt.ToString("F2", CultureInfo.InvariantCulture),
                            v.Narration
                        });
                    }
                    return CsvUtility.WriteCsv(rows);
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(options.EntityType));
        }
    }

    private static int FindColumnIndex(List<string> headers, params string[] candidates)
    {
        for (int i = 0; i < headers.Count; i++)
        {
            var h = headers[i];
            foreach (var c in candidates)
            {
                if (h.Equals(c, StringComparison.OrdinalIgnoreCase) || h.Contains(c, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }
        return -1;
    }

    private static string GetField(List<string> row, int index)
    {
        if (index >= 0 && index < row.Count)
        {
            return row[index]?.Trim() ?? string.Empty;
        }
        return string.Empty;
    }
}
