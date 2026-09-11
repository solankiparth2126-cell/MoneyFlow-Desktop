using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Data;
using MoneyFlow.Data.Repositories;
using MoneyFlow.Services.Company;
using MoneyFlow.Services.FinancialYear;
using MoneyFlow.Services.Group;
using MoneyFlow.Services.Inventory;
using MoneyFlow.Services.Ledger;
using Xunit;

namespace MoneyFlow.Tests.InventoryTests;

public class Phase23InventoryTests
{
    private (AppDbContext Context, CompanyService CompService, InventoryService InvService) CreateTestSetup()
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

        return (context, compService, invService);
    }

    [Fact]
    public async Task CreateUnit_Should_Persist_And_Enforce_Unique_Symbol_Per_Company()
    {
        var (context, compService, invService) = CreateTestSetup();

        var companyA = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Inventory Enterprise A",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var companyB = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Inventory Enterprise B",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        // 1. Create Unit in Company A
        var unitA = await invService.CreateUnitAsync(companyA.CompanyId, new UnitCreateDto
        {
            UnitName = "Nos",
            FormalName = "Numbers",
            DecimalPlaces = 0
        });

        unitA.Should().NotBeNull();
        unitA.UnitId.Should().BeGreaterThan(0);
        unitA.UnitName.Should().Be("Nos");
        unitA.FormalName.Should().Be("Numbers");
        unitA.DecimalPlaces.Should().Be(0);

        // 2. Duplicate in Company A should throw
        var actDuplicate = async () => await invService.CreateUnitAsync(companyA.CompanyId, new UnitCreateDto
        {
            UnitName = "nos", // case-insensitive duplicate
            FormalName = "Duplicate Numbers"
        });

        await actDuplicate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");

        // 3. Same symbol in Company B should succeed (company isolation)
        var unitB = await invService.CreateUnitAsync(companyB.CompanyId, new UnitCreateDto
        {
            UnitName = "Nos",
            FormalName = "Numbers for B"
        });

        unitB.Should().NotBeNull();
        unitB.CompanyId.Should().Be(companyB.CompanyId);

        // Verify retrieval
        var listA = await invService.GetUnitsByCompanyAsync(companyA.CompanyId);
        listA.Should().HaveCount(1);
        listA[0].UnitName.Should().Be("Nos");
    }

    [Fact]
    public async Task DeleteUnit_Should_Prevent_Deletion_When_In_Use_By_Stock_Item()
    {
        var (context, compService, invService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Unit Deletion Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var unit = await invService.CreateUnitAsync(company.CompanyId, new UnitCreateDto
        {
            UnitName = "Box",
            FormalName = "Boxes"
        });

        var item = await invService.CreateStockItemAsync(company.CompanyId, new StockItemCreateDto
        {
            ItemName = "Packaging Cartons",
            UnitId = unit.UnitId,
            OpeningQuantity = 10,
            OpeningRate = 50
        });

        // Attempting to delete the unit while attached to an item should fail
        var actDelete = async () => await invService.DeleteUnitAsync(unit.UnitId);

        await actDelete.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*in use by one or more Stock Items*");

        // After deleting the stock item, deleting the unit must succeed
        await invService.DeleteStockItemAsync(item.StockItemId);
        bool deleted = await invService.DeleteUnitAsync(unit.UnitId);
        deleted.Should().BeTrue();

        var remainingUnits = await invService.GetUnitsByCompanyAsync(company.CompanyId);
        remainingUnits.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateStockItem_Should_AutoCalculate_OpeningValue_And_Enforce_Unique_Name()
    {
        var (context, compService, invService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Stock Valuation Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var unitKg = await invService.CreateUnitAsync(company.CompanyId, new UnitCreateDto
        {
            UnitName = "Kg",
            FormalName = "Kilograms",
            DecimalPlaces = 2
        });

        // 1. Create stock item: 25.50 Kg @ ₹80.00/Kg
        var item = await invService.CreateStockItemAsync(company.CompanyId, new StockItemCreateDto
        {
            ItemName = "Organic Green Tea",
            UnitId = unitKg.UnitId,
            OpeningQuantity = 25.50m,
            OpeningRate = 80.00m
        });

        item.Should().NotBeNull();
        item.StockItemId.Should().BeGreaterThan(0);
        item.ItemName.Should().Be("Organic Green Tea");
        item.UnitName.Should().Be("Kg");
        item.OpeningQuantity.Should().Be(25.50m);
        item.OpeningRate.Should().Be(80.00m);
        item.OpeningValue.Should().Be(2040.00m); // 25.50 * 80.00 = 2040.00

        // 2. Duplicate Item Name in same company should fail
        var actDuplicate = async () => await invService.CreateStockItemAsync(company.CompanyId, new StockItemCreateDto
        {
            ItemName = "organic green tea",
            UnitId = unitKg.UnitId,
            OpeningQuantity = 10,
            OpeningRate = 50
        });

        await actDuplicate.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateStockItem_Should_Modify_Fields_And_Recalculate_OpeningValue()
    {
        var (context, compService, invService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Stock Update Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var unit = await invService.CreateUnitAsync(company.CompanyId, new UnitCreateDto
        {
            UnitName = "Pcs",
            FormalName = "Pieces"
        });

        var item = await invService.CreateStockItemAsync(company.CompanyId, new StockItemCreateDto
        {
            ItemName = "Original Item",
            UnitId = unit.UnitId,
            OpeningQuantity = 100m,
            OpeningRate = 20m
        });

        item.OpeningValue.Should().Be(2000m);

        // Update item name, quantity, and rate
        var updated = await invService.UpdateStockItemAsync(item.StockItemId, new StockItemUpdateDto
        {
            ItemName = "Renamed Item Premium",
            UnitId = unit.UnitId,
            OpeningQuantity = 150m,
            OpeningRate = 30m,
            IsActive = true
        });

        updated.ItemName.Should().Be("Renamed Item Premium");
        updated.OpeningQuantity.Should().Be(150m);
        updated.OpeningRate.Should().Be(30m);
        updated.OpeningValue.Should().Be(4500m); // 150 * 30 = 4500
    }

    [Fact]
    public async Task GetStockSummary_Should_Aggregate_Stock_Items_And_Valuations()
    {
        var (context, compService, invService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Stock Summary Trading Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var unit = await invService.CreateUnitAsync(company.CompanyId, new UnitCreateDto
        {
            UnitName = "Nos",
            FormalName = "Numbers"
        });

        // Item 1: 100 Nos @ ₹50 = ₹5,000
        await invService.CreateStockItemAsync(company.CompanyId, new StockItemCreateDto
        {
            ItemName = "Item Alpha",
            UnitId = unit.UnitId,
            OpeningQuantity = 100m,
            OpeningRate = 50m
        });

        // Item 2: 200 Nos @ ₹25 = ₹5,000
        await invService.CreateStockItemAsync(company.CompanyId, new StockItemCreateDto
        {
            ItemName = "Item Beta",
            UnitId = unit.UnitId,
            OpeningQuantity = 200m,
            OpeningRate = 25m
        });

        // Item 3: 50 Nos @ ₹100 = ₹5,000
        await invService.CreateStockItemAsync(company.CompanyId, new StockItemCreateDto
        {
            ItemName = "Item Gamma",
            UnitId = unit.UnitId,
            OpeningQuantity = 50m,
            OpeningRate = 100m
        });

        // Query Stock Summary
        var summary = await invService.GetStockSummaryAsync(company.CompanyId, new DateTime(2026, 9, 30));

        summary.Should().NotBeNull();
        summary.Items.Should().HaveCount(3);
        summary.TotalOpeningValue.Should().Be(15000m);
        summary.TotalClosingValue.Should().Be(15000m);

        var alpha = summary.Items.First(i => i.ItemName == "Item Alpha");
        alpha.ClosingQuantity.Should().Be(100m);
        alpha.ClosingRate.Should().Be(50m);
        alpha.ClosingValue.Should().Be(5000m);
    }
}
