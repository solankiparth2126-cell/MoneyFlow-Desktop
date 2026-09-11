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
using MoneyFlow.Services.Accounting;
using MoneyFlow.Services.Company;
using MoneyFlow.Services.Group;
using MoneyFlow.Services.Inventory;
using MoneyFlow.Services.Ledger;
using Xunit;

namespace MoneyFlow.Tests.IntegrityAndAccountingSuite;

public class MultiCompanyIsolationTests
{
    private (
        AppDbContext Context,
        AccountingService AccountingSvc,
        CompanyService CompanySvc,
        LedgerService LedgerSvc,
        GroupService GroupSvc,
        InventoryService InventorySvc
    ) CreateTestSetup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var unitOfWork = new UnitOfWork(context);
        var voucherRepo = new VoucherRepository(context);
        var ledgerRepo = new LedgerRepository(context);
        var fyRepo = new FinancialYearRepository(context);
        var companyRepo = new CompanyRepository(context);
        var groupRepo = new GroupRepository(context);

        var companyContext = new CompanyContext();

        var accountingService = new AccountingService(
            context,
            voucherRepo,
            ledgerRepo,
            fyRepo,
            companyRepo,
            unitOfWork,
            NullLogger<AccountingService>.Instance);

        var companyService = new CompanyService(
            companyRepo,
            fyRepo,
            groupRepo,
            ledgerRepo,
            unitOfWork,
            companyContext,
            NullLogger<CompanyService>.Instance);

        var groupService = new GroupService(
            context,
            groupRepo,
            companyRepo,
            unitOfWork,
            NullLogger<GroupService>.Instance);

        var ledgerService = new LedgerService(
            context,
            ledgerRepo,
            groupRepo,
            companyRepo,
            unitOfWork,
            NullLogger<LedgerService>.Instance);

        var inventoryService = new InventoryService(
            context,
            unitOfWork,
            NullLogger<InventoryService>.Instance);

        return (context, accountingService, companyService, ledgerService, groupService, inventoryService);
    }

    [Fact]
    public async Task MasterPrompt_Section53_MultiCompanyTest_CashACannotAppearInCompanyB()
    {
        // =========================================================================
        // Master Prompt Section 53:
        // Create Company A, Company B. Create Cash A inside Company A.
        // Switch to Company B. Verify Cash A cannot appear. This is mandatory.
        // =========================================================================

        // Arrange
        var (context, _, companySvc, ledgerSvc, _, _) = CreateTestSetup();

        // 1. Create Company A and Company B
        var companyA = await companySvc.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Company A Pvt Ltd",
            Currency = "₹",
            CreateDefaultLedgers = false
        });

        var companyB = await companySvc.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Company B Pvt Ltd",
            Currency = "₹",
            CreateDefaultLedgers = false
        });

        // 2. Create Group in Company A and Group in Company B
        var groupA = new Group { CompanyId = companyA.CompanyId, GroupName = "Cash-in-Hand", Nature = GroupNature.Assets };
        var groupB = new Group { CompanyId = companyB.CompanyId, GroupName = "Cash-in-Hand", Nature = GroupNature.Assets };
        context.Groups.AddRange(groupA, groupB);
        await context.SaveChangesAsync();

        // 3. Create "Cash A" inside Company A
        var cashA = await ledgerSvc.CreateLedgerAsync(companyA.CompanyId, new LedgerCreateDto
        {
            LedgerName = "Cash A",
            GroupId = groupA.GroupId,
            OpeningBalance = 10000m,
            OpeningBalanceType = BalanceType.Debit
        });

        // Act: Fetch ledgers for Company B
        var ledgersInCompanyB = await ledgerSvc.GetLedgersByCompanyAsync(companyB.CompanyId);

        // Assert: "Cash A" MUST NOT appear in Company B
        ledgersInCompanyB.Should().NotContain(l => l.LedgerName == "Cash A");
        ledgersInCompanyB.Should().NotContain(l => l.LedgerId == cashA.LedgerId);

        // Act: Verify Company A does contain "Cash A"
        var ledgersInCompanyA = await ledgerSvc.GetLedgersByCompanyAsync(companyA.CompanyId);
        ledgersInCompanyA.Should().Contain(l => l.LedgerName == "Cash A");
    }

    [Fact]
    public async Task CrossCompany_VoucherCreation_ThrowsInvalidOperationException_WhenLedgerBelongsToOtherCompany()
    {
        // Arrange
        var (context, accountingSvc, companySvc, ledgerSvc, _, _) = CreateTestSetup();

        var compA = await companySvc.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Alpha Corp", CreateDefaultLedgers = false });
        var compB = await companySvc.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Beta Corp", CreateDefaultLedgers = false });

        var fyB = await context.FinancialYears.FirstAsync(f => f.CompanyId == compB.CompanyId);

        var grpA = new Group { CompanyId = compA.CompanyId, GroupName = "Direct Expenses", Nature = GroupNature.Expenses };
        var grpB = new Group { CompanyId = compB.CompanyId, GroupName = "Bank Accounts", Nature = GroupNature.Assets };
        context.Groups.AddRange(grpA, grpB);
        await context.SaveChangesAsync();

        var ledgerA = await ledgerSvc.CreateLedgerAsync(compA.CompanyId, new LedgerCreateDto { LedgerName = "Office Rent A", GroupId = grpA.GroupId });
        var ledgerB = await ledgerSvc.CreateLedgerAsync(compB.CompanyId, new LedgerCreateDto { LedgerName = "HDFC Bank B", GroupId = grpB.GroupId });

        var voucherType = new VoucherType { Name = "Payment", Code = "PMT", Type = VoucherTypeEnum.Payment, Prefix = "PAY-" };
        context.VoucherTypes.Add(voucherType);
        await context.SaveChangesAsync();

        // Act: Try to create voucher in Company B using Ledger from Company A
        var badVoucher = new VoucherCreateDto
        {
            FinancialYearId = fyB.FinancialYearId,
            VoucherTypeId = voucherType.VoucherTypeId,
            VoucherDate = fyB.StartDate.AddMonths(1),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledgerA.LedgerId, Debit = 5000m, Credit = 0m }, // Belonging to Company A!
                new() { LedgerId = ledgerB.LedgerId, Debit = 0m, Credit = 5000m }
            }
        };

        // Assert: Must throw InvalidOperationException
        var act = async () => await accountingSvc.SaveVoucherAsync(compB.CompanyId, badVoucher);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*One or more selected ledgers do not exist, are inactive, or do not belong to the active company.*");
    }

    [Fact]
    public async Task CrossCompany_VoucherCreation_ThrowsInvalidOperationException_WhenFinancialYearBelongsToOtherCompany()
    {
        // Arrange
        var (context, accountingSvc, companySvc, ledgerSvc, _, _) = CreateTestSetup();

        var compA = await companySvc.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Company 1", CreateDefaultLedgers = false });
        var compB = await companySvc.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Company 2", CreateDefaultLedgers = false });

        var fyA = await context.FinancialYears.FirstAsync(f => f.CompanyId == compA.CompanyId);

        var grpB = new Group { CompanyId = compB.CompanyId, GroupName = "Bank Accounts", Nature = GroupNature.Assets };
        context.Groups.Add(grpB);
        await context.SaveChangesAsync();

        var ledgerB1 = await ledgerSvc.CreateLedgerAsync(compB.CompanyId, new LedgerCreateDto { LedgerName = "Bank B1", GroupId = grpB.GroupId });
        var ledgerB2 = await ledgerSvc.CreateLedgerAsync(compB.CompanyId, new LedgerCreateDto { LedgerName = "Bank B2", GroupId = grpB.GroupId });

        var voucherType = new VoucherType { Name = "Contra", Code = "CNT", Type = VoucherTypeEnum.Contra, Prefix = "CNT-" };
        context.VoucherTypes.Add(voucherType);
        await context.SaveChangesAsync();

        // Act: Save voucher for Company B using FinancialYear of Company A
        var crossFyVoucher = new VoucherCreateDto
        {
            FinancialYearId = fyA.FinancialYearId, // BELONGS TO COMPANY A!
            VoucherTypeId = voucherType.VoucherTypeId,
            VoucherDate = fyA.StartDate.AddMonths(1),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledgerB1.LedgerId, Debit = 1000m, Credit = 0m },
                new() { LedgerId = ledgerB2.LedgerId, Debit = 0m, Credit = 1000m }
            }
        };

        // Assert: Must reject
        var act = async () => await accountingSvc.SaveVoucherAsync(compB.CompanyId, crossFyVoucher);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*The financial year does not belong to the active company.*");
    }

    [Fact]
    public async Task MultiCompany_InventoryIsolation_StockItemsDoNotCrossBetweenCompanies()
    {
        // Arrange
        var (_, _, companySvc, _, _, inventorySvc) = CreateTestSetup();

        var compA = await companySvc.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Mfg Corp A", CreateDefaultLedgers = false });
        var compB = await companySvc.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Trading Corp B", CreateDefaultLedgers = false });

        var unitA = await inventorySvc.CreateUnitAsync(compA.CompanyId, new UnitCreateDto { UnitName = "PCS", FormalName = "Pieces" });
        await inventorySvc.CreateUnitAsync(compB.CompanyId, new UnitCreateDto { UnitName = "KG", FormalName = "Kilograms" });

        var itemA = await inventorySvc.CreateStockItemAsync(compA.CompanyId, new StockItemCreateDto
        {
            ItemName = "Item Special Alpha",
            UnitId = unitA.UnitId,
            OpeningQuantity = 50,
            OpeningRate = 100m
        });

        // Act
        var itemsInB = await inventorySvc.GetStockItemsByCompanyAsync(compB.CompanyId);

        // Assert
        itemsInB.Should().NotContain(i => i.ItemName == "Item Special Alpha");
        itemsInB.Should().NotContain(i => i.StockItemId == itemA.StockItemId);
    }
}
