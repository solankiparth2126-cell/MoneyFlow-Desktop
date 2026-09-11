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
using MoneyFlow.Services.FinancialYear;
using MoneyFlow.Services.Group;
using MoneyFlow.Services.Ledger;
using Xunit;

namespace MoneyFlow.Tests.IntegrityAndAccountingSuite;

public class FinancialYearIsolationTests
{
    private (
        AppDbContext Context,
        AccountingService AccountingSvc,
        CompanyService CompanySvc,
        FinancialYearService FySvc,
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

        var fyService = new FinancialYearService(
            fyRepo,
            companyRepo,
            unitOfWork,
            companyContext,
            NullLogger<FinancialYearService>.Instance);

        var ledgerService = new LedgerService(
            context,
            ledgerRepo,
            groupRepo,
            companyRepo,
            unitOfWork,
            NullLogger<LedgerService>.Instance);

        return (context, accountingService, companyService, fyService, ledgerService);
    }

    [Fact]
    public async Task MasterPrompt_Section54_FinancialYearTest_VoucherInPastYearDoesNotAppearInCurrentYearReports()
    {
        // =========================================================================
        // Master Prompt Section 54:
        // Create 2025-26 and 2026-27. Create a voucher in 2025-26.
        // Open 2026-27. Verify that the voucher does not appear in normal 2026-27 reports.
        // =========================================================================

        // Arrange
        var (context, accountingSvc, companySvc, fySvc, ledgerSvc) = CreateTestSetup();

        // 1. Create Company
        var company = await companySvc.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Multi-Period Trading Ltd",
            FinancialYearFrom = new DateTime(2025, 4, 1),
            CreateDefaultLedgers = false
        });
        int companyId = company.CompanyId;

        // Company creation automatically seeds 2025-26 based on FinancialYearFrom
        var fy2526 = await context.FinancialYears.FirstAsync(f => f.CompanyId == companyId);

        // Create the subsequent FY 2026-27 (01-Apr-2026 to 31-Mar-2027)
        var fy2627 = await fySvc.CreateFinancialYearAsync(companyId, new FinancialYearCreateDto
        {
            YearName = "2026-27",
            StartDate = new DateTime(2026, 4, 1),
            EndDate = new DateTime(2027, 3, 31)
        });

        // 3. Create Ledgers
        var grpCash = new Group { CompanyId = companyId, GroupName = "Cash-in-Hand", Nature = GroupNature.Assets };
        var grpExp = new Group { CompanyId = companyId, GroupName = "Indirect Expenses", Nature = GroupNature.Expenses };
        context.Groups.AddRange(grpCash, grpExp);
        await context.SaveChangesAsync();

        var ledgerCash = await ledgerSvc.CreateLedgerAsync(companyId, new LedgerCreateDto { LedgerName = "Cash", GroupId = grpCash.GroupId, OpeningBalance = 50000m, OpeningBalanceType = BalanceType.Debit });
        var ledgerRent = await ledgerSvc.CreateLedgerAsync(companyId, new LedgerCreateDto { LedgerName = "Office Rent", GroupId = grpExp.GroupId });

        var paymentType = new VoucherType { Name = "Payment", Code = "PMT", Type = VoucherTypeEnum.Payment, Prefix = "PAY-" };
        context.VoucherTypes.Add(paymentType);
        await context.SaveChangesAsync();

        // 4. Create Voucher in 2025-26 (dated 15-Jun-2025)
        var voucher2526 = await accountingSvc.SaveVoucherAsync(companyId, new VoucherCreateDto
        {
            FinancialYearId = fy2526.FinancialYearId,
            VoucherTypeId = paymentType.VoucherTypeId,
            VoucherDate = new DateTime(2025, 6, 15),
            ReferenceNumber = "REF-25-01",
            Narration = "Rent payment for June 2025",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledgerRent.LedgerId, Debit = 15000m, Credit = 0m },
                new() { LedgerId = ledgerCash.LedgerId, Debit = 0m, Credit = 15000m }
            }
        });

        voucher2526.Should().NotBeNull();

        // 5. Open/Query Day Book for 2026-27 period (01-Apr-2026 to 31-Mar-2027)
        var dayBook2627 = await accountingSvc.GetDayBookAsync(
            companyId,
            new DateTime(2026, 4, 1),
            new DateTime(2027, 3, 31));

        // Assert: 2025-26 voucher MUST NOT appear in 2026-27 Day Book!
        dayBook2627.Items.Should().NotContain(i => i.VoucherId == voucher2526.VoucherId);
        dayBook2627.TotalTransactions.Should().Be(0);

        // 6. Query Day Book for 2025-26 period: It SHOULD appear here!
        var dayBook2526 = await accountingSvc.GetDayBookAsync(
            companyId,
            new DateTime(2025, 4, 1),
            new DateTime(2026, 3, 31));

        dayBook2526.Items.Should().Contain(i => i.VoucherId == voucher2526.VoucherId);
        dayBook2526.TotalTransactions.Should().Be(1);
    }

    [Fact]
    public async Task VoucherCreation_RejectsDate_OutsideFinancialYearPeriod()
    {
        // Arrange
        var (context, accountingSvc, companySvc, fySvc, ledgerSvc) = CreateTestSetup();

        var company = await companySvc.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Date Check Co", CreateDefaultLedgers = false });
        int companyId = company.CompanyId;

        var fy2627 = await context.FinancialYears.FirstAsync(f => f.CompanyId == companyId);

        var grp = new Group { CompanyId = companyId, GroupName = "General", Nature = GroupNature.Expenses };
        context.Groups.Add(grp);
        await context.SaveChangesAsync();

        var l1 = await ledgerSvc.CreateLedgerAsync(companyId, new LedgerCreateDto { LedgerName = "Expense A", GroupId = grp.GroupId });
        var l2 = await ledgerSvc.CreateLedgerAsync(companyId, new LedgerCreateDto { LedgerName = "Expense B", GroupId = grp.GroupId });

        var jType = new VoucherType { Name = "Journal", Code = "JRN", Type = VoucherTypeEnum.Journal, Prefix = "JRN-" };
        context.VoucherTypes.Add(jType);
        await context.SaveChangesAsync();

        // Act: Try saving voucher with date in 2028 (outside 2026-27)
        var badVoucher = new VoucherCreateDto
        {
            FinancialYearId = fy2627.FinancialYearId,
            VoucherTypeId = jType.VoucherTypeId,
            VoucherDate = new DateTime(2028, 1, 1), // OUT OF RANGE!
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = l1.LedgerId, Debit = 100m, Credit = 0m },
                new() { LedgerId = l2.LedgerId, Debit = 0m, Credit = 100m }
            }
        };

        // Assert: Must reject
        var act = async () => await accountingSvc.SaveVoucherAsync(companyId, badVoucher);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*outside the active Financial Year period*");
    }

    [Fact]
    public async Task ClosedFinancialYear_PreventsVoucherPosting()
    {
        // Arrange
        var (context, accountingSvc, companySvc, fySvc, ledgerSvc) = CreateTestSetup();

        var company = await companySvc.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Locked FY Co", CreateDefaultLedgers = false });
        int companyId = company.CompanyId;

        var fy = await fySvc.CreateFinancialYearAsync(companyId, new FinancialYearCreateDto
        {
            YearName = "2024-25",
            StartDate = new DateTime(2024, 4, 1),
            EndDate = new DateTime(2025, 3, 31)
        });

        // Close the FY
        await fySvc.CloseFinancialYearAsync(fy.FinancialYearId);

        var grp = new Group { CompanyId = companyId, GroupName = "General", Nature = GroupNature.Expenses };
        context.Groups.Add(grp);
        await context.SaveChangesAsync();

        var l1 = await ledgerSvc.CreateLedgerAsync(companyId, new LedgerCreateDto { LedgerName = "Exp 1", GroupId = grp.GroupId });
        var l2 = await ledgerSvc.CreateLedgerAsync(companyId, new LedgerCreateDto { LedgerName = "Exp 2", GroupId = grp.GroupId });
        var jType = new VoucherType { Name = "Journal", Code = "JRN", Type = VoucherTypeEnum.Journal, Prefix = "JRN-" };
        context.VoucherTypes.Add(jType);
        await context.SaveChangesAsync();

        var voucher = new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = jType.VoucherTypeId,
            VoucherDate = new DateTime(2024, 8, 1),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = l1.LedgerId, Debit = 500m, Credit = 0m },
                new() { LedgerId = l2.LedgerId, Debit = 0m, Credit = 500m }
            }
        };

        // Act & Assert
        var act = async () => await accountingSvc.SaveVoucherAsync(companyId, voucher);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*is closed/locked for transactions*");
    }
}
