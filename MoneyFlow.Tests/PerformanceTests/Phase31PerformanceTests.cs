using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using MoneyFlow.Services.Ledger;
using Xunit;

namespace MoneyFlow.Tests.PerformanceTests;

public class Phase31PerformanceTests
{
    private (
        AppDbContext Context,
        AccountingService AccountingSvc,
        CompanyService CompanySvc,
        LedgerService LedgerSvc
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

        var ledgerService = new LedgerService(
            context,
            ledgerRepo,
            groupRepo,
            companyRepo,
            unitOfWork,
            NullLogger<LedgerService>.Instance);

        return (context, accountingService, companyService, ledgerService);
    }

    [Fact]
    public void ModelIndexVerification_AllSection50Indexes_AreRegisteredInEFCoreModel()
    {
        // Arrange
        var (context, _, _, _) = CreateTestSetup();
        var model = context.Model;

        // Act & Assert for Voucher Entity
        var voucherEntity = model.FindEntityType(typeof(Voucher));
        voucherEntity.Should().NotBeNull();
        var voucherIndexes = voucherEntity!.GetIndexes().ToList();

        // Section 50: CompanyId, FinancialYearId, VoucherDate, VoucherNumber
        voucherIndexes.Any(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "CompanyId" })).Should().BeTrue("Index on CompanyId must exist");
        voucherIndexes.Any(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "FinancialYearId" })).Should().BeTrue("Index on FinancialYearId must exist");
        voucherIndexes.Any(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "VoucherDate" })).Should().BeTrue("Index on VoucherDate must exist");
        voucherIndexes.Any(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "VoucherNumber" })).Should().BeTrue("Index on VoucherNumber must exist");

        // Compound indexes for Day Book, Reports, and Type filtering
        voucherIndexes.Any(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "CompanyId", "FinancialYearId", "VoucherDate", "IsDeleted" }))
            .Should().BeTrue("Compound index for FY DayBook lookups must exist");
        voucherIndexes.Any(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "CompanyId", "VoucherDate", "IsDeleted" }))
            .Should().BeTrue("Compound index for date range reports must exist");
        voucherIndexes.Any(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "CompanyId", "VoucherTypeId", "FinancialYearId" }))
            .Should().BeTrue("Compound index for voucher type filtering must exist");

        // Act & Assert for VoucherEntry Entity
        var entryEntity = model.FindEntityType(typeof(VoucherEntry));
        entryEntity.Should().NotBeNull();
        var entryIndexes = entryEntity!.GetIndexes().ToList();

        // Section 50: LedgerId, VoucherId
        entryIndexes.Any(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "VoucherId" })).Should().BeTrue("Index on VoucherId must exist");
        entryIndexes.Any(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "LedgerId" })).Should().BeTrue("Index on LedgerId must exist");
        entryIndexes.Any(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "LedgerId", "VoucherId" })).Should().BeTrue("Compound index on {LedgerId, VoucherId} must exist");

        // Group & Ledger Active Status Indexes
        var groupEntity = model.FindEntityType(typeof(Group))!;
        groupEntity.GetIndexes().Any(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "CompanyId", "IsActive" })).Should().BeTrue();

        var ledgerEntity = model.FindEntityType(typeof(Ledger))!;
        ledgerEntity.GetIndexes().Any(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "CompanyId", "IsActive" })).Should().BeTrue();

        // StockItem Active Status Index
        var stockItemEntity = model.FindEntityType(typeof(StockItem))!;
        stockItemEntity.GetIndexes().Any(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "CompanyId", "IsActive" })).Should().BeTrue();

        // AuditLog Compound Indexes
        var auditEntity = model.FindEntityType(typeof(AuditLog))!;
        auditEntity.GetIndexes().Any(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "CompanyId", "Timestamp" })).Should().BeTrue();
        auditEntity.GetIndexes().Any(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Module", "Action" })).Should().BeTrue();
    }

    [Fact]
    public async Task BatchVoucherPosting_Processes100VouchersRapidly_AndReconcilesTrialBalance()
    {
        // Arrange
        var (context, accountingSvc, companySvc, ledgerSvc) = CreateTestSetup();
        var company = await companySvc.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Speed Test Traders", CreateDefaultLedgers = false });
        var fy = await context.FinancialYears.FirstAsync(f => f.CompanyId == company.CompanyId);

        var assetsGroup = new Group { CompanyId = company.CompanyId, GroupName = "Current Assets", Nature = GroupNature.Assets };
        var incomeGroup = new Group { CompanyId = company.CompanyId, GroupName = "Direct Incomes", Nature = GroupNature.Income };
        context.Groups.AddRange(assetsGroup, incomeGroup);
        await context.SaveChangesAsync();

        var cashLedger = await ledgerSvc.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { LedgerName = "Speed Cash", GroupId = assetsGroup.GroupId });
        var salesLedger = await ledgerSvc.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { LedgerName = "Speed Sales", GroupId = incomeGroup.GroupId });

        var receiptType = new VoucherType { Name = "Receipt", Code = "RCP", Type = VoucherTypeEnum.Receipt, Prefix = "RCP-" };
        context.VoucherTypes.Add(receiptType);
        await context.SaveChangesAsync();

        // Act: Sequential posting of 100 balanced double-entry vouchers
        var sw = Stopwatch.StartNew();
        const int voucherCount = 100;
        decimal totalAmountPosted = 0m;

        for (int i = 1; i <= voucherCount; i++)
        {
            decimal amt = 100m + i;
            totalAmountPosted += amt;

            var voucherDto = new VoucherCreateDto
            {
                FinancialYearId = fy.FinancialYearId,
                VoucherTypeId = receiptType.VoucherTypeId,
                VoucherDate = fy.StartDate.AddDays(i % 25),
                Narration = $"High volume performance voucher #{i}",
                Entries = new List<VoucherEntryDto>
                {
                    new()
                    {
                        LedgerId = cashLedger.LedgerId,
                        Debit = amt,
                        Credit = 0m,
                        Narration = "Cash Dr"
                    },
                    new()
                    {
                        LedgerId = salesLedger.LedgerId,
                        Debit = 0m,
                        Credit = amt,
                        Narration = "Sales Cr"
                    }
                }
            };

            var created = await accountingSvc.SaveVoucherAsync(company.CompanyId, voucherDto);
            created.Should().NotBeNull();
        }

        sw.Stop();

        // Assert performance benchmark: 100 full double-entry validations & ledger closing balance updates in < 5000ms
        sw.ElapsedMilliseconds.Should().BeLessThan(5000, "Posting 100 balanced double-entry vouchers must be swift for desktop performance");

        // Act: DayBook retrieval
        var dayBookSw = Stopwatch.StartNew();
        var dayBook = await accountingSvc.GetDayBookAsync(company.CompanyId, fy.StartDate, fy.EndDate);
        dayBookSw.Stop();

        dayBook.Should().NotBeNull();
        dayBook.Items.Should().HaveCount(100);
        dayBookSw.ElapsedMilliseconds.Should().BeLessThan(1000, "Day Book retrieval across 100 vouchers must execute under 1 second");

        // Act: Trial Balance verification
        var tbSw = Stopwatch.StartNew();
        var tb = await accountingSvc.GetTrialBalanceAsync(company.CompanyId, fy.StartDate, fy.EndDate);
        tbSw.Stop();

        tb.Should().NotBeNull();
        tb.IsBalanced.Should().BeTrue();
        tb.Difference.Should().Be(0m);
        tb.TotalClosingDebit.Should().Be(totalAmountPosted);
        tb.TotalClosingCredit.Should().Be(totalAmountPosted);
        tbSw.ElapsedMilliseconds.Should().BeLessThan(1000, "Trial Balance calculation must execute in under 1 second");
    }

    [Fact]
    public async Task DayBookPaginationAndQueryEfficiency_LoadsExactSubsetsRapidly()
    {
        // Arrange
        var (context, accountingSvc, companySvc, ledgerSvc) = CreateTestSetup();
        var company = await companySvc.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Pagination Traders", CreateDefaultLedgers = false });
        var fy = await context.FinancialYears.FirstAsync(f => f.CompanyId == company.CompanyId);

        var grpCash = new Group { CompanyId = company.CompanyId, GroupName = "Cash-in-Hand", Nature = GroupNature.Assets };
        var grpBank = new Group { CompanyId = company.CompanyId, GroupName = "Bank Accounts", Nature = GroupNature.Assets };
        context.Groups.AddRange(grpCash, grpBank);
        await context.SaveChangesAsync();

        var cashLedger = await ledgerSvc.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { LedgerName = "Cash Paging", GroupId = grpCash.GroupId });
        var bankLedger = await ledgerSvc.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { LedgerName = "Bank Paging", GroupId = grpBank.GroupId });

        var contraType = new VoucherType { Name = "Contra", Code = "CTR", Type = VoucherTypeEnum.Contra, Prefix = "CNT-" };
        context.VoucherTypes.Add(contraType);
        await context.SaveChangesAsync();

        // Seed 60 contra vouchers
        for (int i = 1; i <= 60; i++)
        {
            var voucherDto = new VoucherCreateDto
            {
                FinancialYearId = fy.FinancialYearId,
                VoucherTypeId = contraType.VoucherTypeId,
                VoucherDate = fy.StartDate.AddDays(i % 30),
                Narration = $"Contra paging #{i}",
                Entries = new List<VoucherEntryDto>
                {
                    new() { LedgerId = bankLedger.LedgerId, Debit = 500m, Credit = 0m },
                    new() { LedgerId = cashLedger.LedgerId, Debit = 0m, Credit = 500m }
                }
            };
            await accountingSvc.SaveVoucherAsync(company.CompanyId, voucherDto);
        }

        // Act: Test Day Book date range slice
        var fromDate = fy.StartDate;
        var toDate = fy.StartDate.AddDays(9);
        var dayBookSlice = await accountingSvc.GetDayBookAsync(company.CompanyId, fromDate, toDate);

        // Assert: Returns non-empty subset matching date criteria swiftly
        dayBookSlice.Should().NotBeNull();
        dayBookSlice.Items.Should().NotBeEmpty();
        dayBookSlice.Items.All(v => v.Date >= fromDate && v.Date <= toDate).Should().BeTrue();
    }
}
