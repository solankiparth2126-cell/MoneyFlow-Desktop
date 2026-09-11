using System;
using System.Collections.Generic;
using System.Linq;
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
using MoneyFlow.Services.Inventory;
using MoneyFlow.Services.Ledger;
using MoneyFlow.Services.Search;
using Xunit;

namespace MoneyFlow.Tests.SearchTests;

public class Phase24GlobalSearchTests
{
    private (AppDbContext Context, CompanyService CompService, InventoryService InvService, SearchService SrchService) CreateTestSetup()
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

        var srchService = new SearchService(
            context,
            NullLogger<SearchService>.Instance);

        return (context, compService, invService, srchService);
    }

    [Fact]
    public async Task Search_Should_Return_Navigation_Screens_On_Empty_Query_Or_Matching_Keywords()
    {
        var (context, compService, _, srchService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Search Nav Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        // 1. Empty query should return default navigation catalog
        var defaultResults = await srchService.SearchAsync(company.CompanyId, "");
        defaultResults.Should().NotBeEmpty();
        defaultResults.All(r => r.Category == GlobalSearchCategory.Navigation).Should().BeTrue();
        defaultResults.Should().Contain(r => r.Title == "Trial Balance");
        defaultResults.Should().Contain(r => r.Title == "Day Book");
        defaultResults.Should().Contain(r => r.Title == "Cash / Bank Book");

        // 2. Keyword query "pnl" should match Profit & Loss
        var pnlResults = await srchService.SearchAsync(company.CompanyId, "pnl");
        pnlResults.Should().Contain(r => r.Title == "Profit & Loss Account" && r.NavigationTarget == "ProfitLoss");

        // 3. Keyword query "contra" should match Contra Voucher
        var contraResults = await srchService.SearchAsync(company.CompanyId, "contra");
        contraResults.Should().Contain(r => r.Title.Contains("Contra Voucher") && r.NavigationTarget == "Contra");
    }

    [Fact]
    public async Task Search_Should_Find_Ledgers_And_Respect_Company_Isolation()
    {
        var (context, compService, _, srchService) = CreateTestSetup();

        var companyA = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Search Co A",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true
        });

        var companyB = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Search Co B",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true
        });

        // Add custom ledger in Company A
        var grpA = await context.Groups.FirstAsync(g => g.CompanyId == companyA.CompanyId && g.GroupName == "Sundry Debtors");
        context.Ledgers.Add(new Core.Entities.Ledger
        {
            CompanyId = companyA.CompanyId,
            GroupId = grpA.GroupId,
            LedgerName = "Acme Global Industries",
            OpeningBalance = 45000m,
            OpeningBalanceType = BalanceType.Debit,
            IsActive = true
        });

        // Add different ledger in Company B
        var grpB = await context.Groups.FirstAsync(g => g.CompanyId == companyB.CompanyId && g.GroupName == "Sundry Debtors");
        context.Ledgers.Add(new Core.Entities.Ledger
        {
            CompanyId = companyB.CompanyId,
            GroupId = grpB.GroupId,
            LedgerName = "Beta Global Industries",
            OpeningBalance = 12000m,
            OpeningBalanceType = BalanceType.Debit,
            IsActive = true
        });
        await context.SaveChangesAsync();

        // Search in Company A for "Global"
        var resultsA = await srchService.SearchAsync(companyA.CompanyId, "Global", GlobalSearchCategory.Ledger);
        resultsA.Should().HaveCount(1);
        resultsA[0].Title.Should().Be("Acme Global Industries");
        resultsA[0].Subtitle.Should().Contain("Sundry Debtors");
        resultsA[0].Amount.Should().Be(45000m);

        // Search in Company B for "Global"
        var resultsB = await srchService.SearchAsync(companyB.CompanyId, "Global", GlobalSearchCategory.Ledger);
        resultsB.Should().HaveCount(1);
        resultsB[0].Title.Should().Be("Beta Global Industries");
    }

    [Fact]
    public async Task Search_Should_Find_Stock_Items_By_Name_And_Unit()
    {
        var (context, compService, invService, srchService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Search Inventory Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var unitBox = await invService.CreateUnitAsync(company.CompanyId, new UnitCreateDto
        {
            UnitName = "Box",
            FormalName = "Boxes"
        });

        await invService.CreateStockItemAsync(company.CompanyId, new StockItemCreateDto
        {
            ItemName = "Premium Basmati Rice 5kg",
            UnitId = unitBox.UnitId,
            OpeningQuantity = 50m,
            OpeningRate = 450m
        });

        // Search for "Basmati"
        var resultsItem = await srchService.SearchAsync(company.CompanyId, "Basmati", GlobalSearchCategory.StockItem);
        resultsItem.Should().HaveCount(1);
        resultsItem[0].Title.Should().Be("Premium Basmati Rice 5kg");
        resultsItem[0].Subtitle.Should().Contain("Unit: Box");
        resultsItem[0].Amount.Should().Be(22500m); // 50 * 450
    }

    [Fact]
    public async Task Search_Should_Find_Vouchers_By_Number_Narration_Or_Parsed_Amount()
    {
        var (context, compService, _, srchService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Search Voucher Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true
        });

        var cashLedger = await context.Ledgers.FirstAsync(l => l.CompanyId == company.CompanyId && l.LedgerName == "Cash");
        var salesLedger = await context.Ledgers.FirstAsync(l => l.CompanyId == company.CompanyId && l.LedgerName == "Sales");

        var vchType = await context.VoucherTypes.FirstOrDefaultAsync(t => t.Type == VoucherTypeEnum.Receipt);
        if (vchType == null)
        {
            vchType = new VoucherType { Name = "Receipt", Code = "RCT", Type = VoucherTypeEnum.Receipt, Prefix = "REC-", NextNumber = 1, IsActive = true };
            context.VoucherTypes.Add(vchType);
            await context.SaveChangesAsync();
        }

        // Create Voucher: Cash Receipt ₹7,500
        var vch = new Voucher
        {
            CompanyId = company.CompanyId,
            VoucherTypeId = vchType.VoucherTypeId,
            VoucherNumber = "RCPT-2026-0042",
            VoucherDate = new DateTime(2026, 6, 15),
            ReferenceNumber = "CHQ-98765",
            Narration = "Annual subscription fee received in cash",
            IsDeleted = false
        };
        context.Vouchers.Add(vch);
        await context.SaveChangesAsync();

        context.VoucherEntries.AddRange(
            new VoucherEntry { VoucherId = vch.VoucherId, LedgerId = cashLedger.LedgerId, Debit = 7500m, Credit = 0m },
            new VoucherEntry { VoucherId = vch.VoucherId, LedgerId = salesLedger.LedgerId, Debit = 0m, Credit = 7500m }
        );
        await context.SaveChangesAsync();

        // 1. Search by voucher number
        var byNumber = await srchService.SearchAsync(company.CompanyId, "RCPT-2026", GlobalSearchCategory.Voucher);
        byNumber.Should().HaveCount(1);
        byNumber[0].EntityId.Should().Be(vch.VoucherId);
        byNumber[0].Title.Should().Contain("RCPT-2026-0042");

        // 2. Search by narration keyword
        var byNarration = await srchService.SearchAsync(company.CompanyId, "subscription", GlobalSearchCategory.Voucher);
        byNarration.Should().HaveCount(1);
        byNarration[0].Subtitle.Should().Contain("subscription fee received");

        // 3. Search by parsed numeric amount "7500"
        var byAmount = await srchService.SearchAsync(company.CompanyId, "7500", GlobalSearchCategory.Voucher);
        byAmount.Should().HaveCount(1);
        byAmount[0].Amount.Should().Be(7500m);
    }

    [Fact]
    public async Task Search_Should_Filter_By_Category_Correctly()
    {
        var (context, compService, invService, srchService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Search Category Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true
        });

        var unit = await invService.CreateUnitAsync(company.CompanyId, new UnitCreateDto
        {
            UnitName = "Nos",
            FormalName = "Numbers"
        });

        // Add ledger and stock item sharing a keyword "Gold"
        var grp = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Creditors");
        context.Ledgers.Add(new Core.Entities.Ledger
        {
            CompanyId = company.CompanyId,
            GroupId = grp.GroupId,
            LedgerName = "Gold Suppliers Ltd",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Credit,
            IsActive = true
        });
        await context.SaveChangesAsync();

        await invService.CreateStockItemAsync(company.CompanyId, new StockItemCreateDto
        {
            ItemName = "Gold Medal Flour",
            UnitId = unit.UnitId,
            OpeningQuantity = 10,
            OpeningRate = 100
        });

        // Query All
        var allResults = await srchService.SearchAsync(company.CompanyId, "Gold", GlobalSearchCategory.All);
        allResults.Should().Contain(r => r.Category == GlobalSearchCategory.Ledger && r.Title == "Gold Suppliers Ltd");
        allResults.Should().Contain(r => r.Category == GlobalSearchCategory.StockItem && r.Title == "Gold Medal Flour");

        // Query Category.Ledger only
        var ledgerOnly = await srchService.SearchAsync(company.CompanyId, "Gold", GlobalSearchCategory.Ledger);
        ledgerOnly.Should().HaveCount(1);
        ledgerOnly[0].Category.Should().Be(GlobalSearchCategory.Ledger);

        // Query Category.StockItem only
        var stockOnly = await srchService.SearchAsync(company.CompanyId, "Gold", GlobalSearchCategory.StockItem);
        stockOnly.Should().HaveCount(1);
        stockOnly[0].Category.Should().Be(GlobalSearchCategory.StockItem);
    }
}
