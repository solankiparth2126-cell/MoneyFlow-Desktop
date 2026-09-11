using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Data;
using MoneyFlow.Data.Repositories;
using MoneyFlow.Services.Company;
using MoneyFlow.Services.FinancialYear;
using MoneyFlow.Services.Group;
using MoneyFlow.Services.ImportExport;
using MoneyFlow.Services.Inventory;
using MoneyFlow.Services.Ledger;
using Xunit;

namespace MoneyFlow.Tests.ImportExportTests;

public class Phase26ImportExportTests
{
    private (AppDbContext Context, CompanyService CompService, InventoryService InvService, ImportExportService IoService) CreateTestSetup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var companyRepo = new CompanyRepository(context);
        var fyRepo = new FinancialYearRepository(context);
        var groupRepo = new GroupRepository(context);
        var ledgerRepo = new LedgerRepository(context);
        var uow = new UnitOfWork(context);
        var companyContext = new CompanyContext();

        var compService = new CompanyService(
            companyRepo,
            fyRepo,
            groupRepo,
            ledgerRepo,
            uow,
            companyContext,
            NullLogger<CompanyService>.Instance);

        var invService = new InventoryService(
            context,
            uow,
            NullLogger<InventoryService>.Instance);

        var ioService = new ImportExportService(
            context,
            uow,
            NullLogger<ImportExportService>.Instance);

        return (context, compService, invService, ioService);
    }

    [Fact]
    public void CsvUtility_Should_Correctly_Parse_And_Write_Quotes_And_Commas()
    {
        var original = new List<List<string>>
        {
            new() { "Name", "Note", "Amount" },
            new() { "Alpha, Inc.", "Note with \"quotes\" and, comma", "1500.50" },
            new() { "Simple", "Normal text", "200" }
        };

        string csvText = CsvUtility.WriteCsv(original);
        csvText.Should().Contain("\"Alpha, Inc.\"");
        csvText.Should().Contain("\"Note with \"\"quotes\"\" and, comma\"");

        var parsed = CsvUtility.ParseCsv(csvText);
        parsed.Should().HaveCount(3);
        parsed[1][0].Should().Be("Alpha, Inc.");
        parsed[1][1].Should().Be("Note with \"quotes\" and, comma");
        parsed[1][2].Should().Be("1500.50");
        parsed[2][0].Should().Be("Simple");
    }

    [Fact]
    public async Task GenerateTemplateCsv_Should_Produce_Valid_Csv_For_All_Entities()
    {
        var (_, _, _, ioService) = CreateTestSetup();

        var ledgerTemplate = await ioService.GenerateTemplateCsvAsync(ImportEntityType.Ledgers);
        ledgerTemplate.Should().Contain("Ledger Name");
        ledgerTemplate.Should().Contain("Group Name");

        var stockTemplate = await ioService.GenerateTemplateCsvAsync(ImportEntityType.StockItems);
        stockTemplate.Should().Contain("Item Name");
        stockTemplate.Should().Contain("Unit Symbol");

        var voucherTemplate = await ioService.GenerateTemplateCsvAsync(ImportEntityType.Vouchers);
        voucherTemplate.Should().Contain("Voucher Type");
        voucherTemplate.Should().Contain("Debit Ledger");
        voucherTemplate.Should().Contain("Credit Ledger");
    }

    [Fact]
    public async Task PreviewImportCsv_Ledgers_Should_Validate_And_Detect_Duplicates()
    {
        var (context, compService, _, ioService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Import Preview Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true
        });

        // "Cash" is already created by default ledgers.
        string csv = @"Ledger Name,Group Name,Opening Balance,Dr/Cr,Description
New Customer Ltd,Sundry Debtors,45000,Dr,Valid client
Cash,Cash-in-Hand,1000,Dr,Duplicate ledger
Invalid Group Ledger,NonExistentGroup,500,Dr,Invalid group error";

        var preview = await ioService.PreviewImportCsvAsync(
            company.CompanyId,
            ImportEntityType.Ledgers,
            csv,
            DuplicateAction.Skip);

        preview.TotalRows.Should().Be(3);
        preview.ValidCount.Should().Be(1);
        preview.DuplicateCount.Should().Be(1);
        preview.ErrorCount.Should().Be(1);

        var validRow = preview.Rows.First(r => r.PrimaryIdentifier == "New Customer Ltd");
        validRow.Status.Should().Be(ImportRowStatus.Valid);

        var dupRow = preview.Rows.First(r => r.PrimaryIdentifier == "Cash");
        dupRow.Status.Should().Be(ImportRowStatus.Duplicate);

        var errRow = preview.Rows.First(r => r.PrimaryIdentifier == "Invalid Group Ledger");
        errRow.Status.Should().Be(ImportRowStatus.Error);
        errRow.ErrorMessage.Should().Contain("does not exist");
    }

    [Fact]
    public async Task ExecuteImport_Ledgers_Should_Persist_Records_And_Respect_Duplicate_Actions()
    {
        var (context, compService, _, ioService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Import Exec Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true
        });

        string csv = @"Ledger Name,Group Name,Opening Balance,Dr/Cr,Description
Beta Customer,Sundry Debtors,25000,Dr,New party
Cash,Cash-in-Hand,5000,Dr,Updated cash balance";

        // 1. Preview with UpdateDuplicate action
        var preview = await ioService.PreviewImportCsvAsync(
            company.CompanyId,
            ImportEntityType.Ledgers,
            csv,
            DuplicateAction.Update);

        preview.ErrorCount.Should().Be(0);
        preview.ValidCount.Should().Be(1);
        preview.DuplicateCount.Should().Be(1);

        // 2. Execute Import
        var result = await ioService.ExecuteImportAsync(
            company.CompanyId,
            ImportEntityType.Ledgers,
            preview,
            DuplicateAction.Update);

        result.Success.Should().BeTrue();
        result.InsertedCount.Should().Be(1);
        result.UpdatedCount.Should().Be(1);

        // Verify in database
        var beta = await context.Ledgers.FirstOrDefaultAsync(l => l.CompanyId == company.CompanyId && l.LedgerName == "Beta Customer");
        beta.Should().NotBeNull();
        beta!.OpeningBalance.Should().Be(25000m);

        var cash = await context.Ledgers.FirstAsync(l => l.CompanyId == company.CompanyId && l.LedgerName == "Cash");
        cash.OpeningBalance.Should().Be(5000m);
    }

    [Fact]
    public async Task Preview_And_ExecuteImport_StockItems_Should_Calculate_Valuation_And_Persist()
    {
        var (context, compService, invService, ioService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Stock Import Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var unit = await invService.CreateUnitAsync(company.CompanyId, new UnitCreateDto
        {
            UnitName = "Box",
            FormalName = "Boxes"
        });

        string csv = @"Item Name,Unit Symbol,Opening Quantity,Opening Rate,Description
Super Basmati Rice,Box,20,400,Aromatic rice
Organic Green Tea,Box,50,150,Tea box";

        var preview = await ioService.PreviewImportCsvAsync(
            company.CompanyId,
            ImportEntityType.StockItems,
            csv,
            DuplicateAction.Skip);

        preview.ErrorCount.Should().Be(0);
        preview.ValidCount.Should().Be(2);

        var execResult = await ioService.ExecuteImportAsync(
            company.CompanyId,
            ImportEntityType.StockItems,
            preview,
            DuplicateAction.Skip);

        execResult.Success.Should().BeTrue();
        execResult.InsertedCount.Should().Be(2);

        var rice = await context.StockItems.FirstAsync(s => s.CompanyId == company.CompanyId && s.ItemName == "Super Basmati Rice");
        rice.OpeningQuantity.Should().Be(20m);
        rice.OpeningRate.Should().Be(400m);
        rice.OpeningValue.Should().Be(8000m); // 20 * 400
    }

    [Fact]
    public async Task ExportData_Should_Generate_Valid_Csv_And_Json()
    {
        var (context, compService, _, ioService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Export Test Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true
        });

        // 1. Export Ledgers to CSV
        var csvData = await ioService.ExportDataAsync(company.CompanyId, new ExportOptionsDto
        {
            EntityType = ImportEntityType.Ledgers,
            Format = ExportFormat.Csv
        });

        csvData.Should().NotBeNullOrWhiteSpace();
        csvData.Should().Contain("Ledger Name");
        csvData.Should().Contain("Cash");
        csvData.Should().Contain("Profit & Loss A/c");

        // 2. Export Ledgers to JSON
        var jsonData = await ioService.ExportDataAsync(company.CompanyId, new ExportOptionsDto
        {
            EntityType = ImportEntityType.Ledgers,
            Format = ExportFormat.Json
        });

        jsonData.Should().NotBeNullOrWhiteSpace();
        jsonData.Should().Contain("\"LedgerName\": \"Cash\"");

        using var doc = JsonDocument.Parse(jsonData);
        doc.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetArrayLength().Should().BeGreaterThan(0);
    }
}
