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

public class SoftDeleteAndConcurrencySafetyTests
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
    public async Task MasterPrompt_Section46_SoftDelete_PreservesRecordInDatabase_WhileExcludingFromReports()
    {
        // =========================================================================
        // Master Prompt Section 46 & 69:
        // "Use soft delete for accounting transactions."
        // "Never delete accounting records permanently by default."
        // =========================================================================

        // Arrange
        var (context, accountingSvc, companySvc, ledgerSvc) = CreateTestSetup();
        var company = await companySvc.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "SoftDelete Corp", CreateDefaultLedgers = false });
        int compId = company.CompanyId;
        var fy = await context.FinancialYears.FirstAsync(f => f.CompanyId == compId);

        var grp = new Group { CompanyId = compId, GroupName = "Cash-in-Hand", Nature = GroupNature.Assets };
        var grpExp = new Group { CompanyId = compId, GroupName = "General Expenses", Nature = GroupNature.Expenses };
        context.Groups.AddRange(grp, grpExp);
        await context.SaveChangesAsync();

        var cash = await ledgerSvc.CreateLedgerAsync(compId, new LedgerCreateDto { LedgerName = "Cash", GroupId = grp.GroupId, OpeningBalance = 20000m, OpeningBalanceType = BalanceType.Debit });
        var exp = await ledgerSvc.CreateLedgerAsync(compId, new LedgerCreateDto { LedgerName = "Tea & Coffee", GroupId = grpExp.GroupId });

        var payType = new VoucherType { Name = "Payment", Code = "PMT", Type = VoucherTypeEnum.Payment, Prefix = "PAY-" };
        context.VoucherTypes.Add(payType);
        await context.SaveChangesAsync();

        var voucher = await accountingSvc.SaveVoucherAsync(compId, new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = payType.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(5),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = exp.LedgerId, Debit = 500m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 500m }
            }
        });

        // Verify balance before soft-delete: Cash closing = 19,500 Dr
        var balanceBefore = await accountingSvc.GetLedgerBalanceAsync(compId, cash.LedgerId);
        balanceBefore.ClosingBalance.Should().Be(19500m);

        // Act: Soft delete the voucher via service
        var deleteResult = await accountingSvc.DeleteVoucherAsync(voucher.VoucherId);
        deleteResult.Should().BeTrue();

        // Assert 1: The row still exists in database (NOT permanently deleted)
        var voucherInDb = await context.Vouchers.IgnoreQueryFilters().FirstOrDefaultAsync(v => v.VoucherId == voucher.VoucherId);
        voucherInDb.Should().NotBeNull();
        voucherInDb!.IsDeleted.Should().BeTrue();

        // Assert 2: Ledger balance recalculates automatically ignoring soft-deleted voucher
        var balanceAfter = await accountingSvc.GetLedgerBalanceAsync(compId, cash.LedgerId);
        balanceAfter.ClosingBalance.Should().Be(20000m); // Back to opening balance

        // Assert 3: DayBook excludes soft-deleted voucher
        var dayBook = await accountingSvc.GetDayBookAsync(compId, fy.StartDate, fy.EndDate);
        dayBook.Items.Should().NotContain(i => i.VoucherId == voucher.VoucherId);
    }

    [Fact]
    public async Task InactiveLedger_CannotBeUsedInNewVouchers()
    {
        // Arrange
        var (context, accountingSvc, companySvc, ledgerSvc) = CreateTestSetup();
        var company = await companySvc.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Inactive Ledger Co", CreateDefaultLedgers = false });
        int compId = company.CompanyId;
        var fy = await context.FinancialYears.FirstAsync(f => f.CompanyId == compId);

        var grp = new Group { CompanyId = compId, GroupName = "General", Nature = GroupNature.Expenses };
        context.Groups.Add(grp);
        await context.SaveChangesAsync();

        var l1 = await ledgerSvc.CreateLedgerAsync(compId, new LedgerCreateDto { LedgerName = "Active Ledger", GroupId = grp.GroupId });
        var l2 = await ledgerSvc.CreateLedgerAsync(compId, new LedgerCreateDto { LedgerName = "Discontinued Ledger", GroupId = grp.GroupId });

        // Deactivate l2 directly in database
        var ledgerToDeactivate = await context.Ledgers.FindAsync(l2.LedgerId);
        ledgerToDeactivate!.IsActive = false;
        await context.SaveChangesAsync();

        var jType = new VoucherType { Name = "Journal", Code = "JRN", Type = VoucherTypeEnum.Journal, Prefix = "JRN-" };
        context.VoucherTypes.Add(jType);
        await context.SaveChangesAsync();

        var voucher = new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = jType.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(5),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = l1.LedgerId, Debit = 200m, Credit = 0m },
                new() { LedgerId = l2.LedgerId, Debit = 0m, Credit = 200m } // INACTIVE!
            }
        };

        // Act & Assert: Must throw InvalidOperationException
        var act = async () => await accountingSvc.SaveVoucherAsync(compId, voucher);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inactive*");
    }

    [Fact]
    public async Task MasterPrompt_Rule70_DecimalPrecision_MoneyCalculationsNeverUseFloats()
    {
        // =========================================================================
        // Master Prompt Section 70:
        // "Use decimal for all monetary calculations. Never use float or double."
        // =========================================================================

        // Arrange
        var (context, accountingSvc, companySvc, ledgerSvc) = CreateTestSetup();
        var company = await companySvc.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Precision Co", CreateDefaultLedgers = false });
        int compId = company.CompanyId;
        var fy = await context.FinancialYears.FirstAsync(f => f.CompanyId == compId);

        var grp = new Group { CompanyId = compId, GroupName = "Expenses", Nature = GroupNature.Expenses };
        context.Groups.Add(grp);
        await context.SaveChangesAsync();

        var l1 = await ledgerSvc.CreateLedgerAsync(compId, new LedgerCreateDto { LedgerName = "Exact Decimal 1", GroupId = grp.GroupId });
        var l2 = await ledgerSvc.CreateLedgerAsync(compId, new LedgerCreateDto { LedgerName = "Exact Decimal 2", GroupId = grp.GroupId });

        var jType = new VoucherType { Name = "Journal", Code = "JRN", Type = VoucherTypeEnum.Journal, Prefix = "JRN-" };
        context.VoucherTypes.Add(jType);
        await context.SaveChangesAsync();

        // Precise decimal fractions that cause floating-point errors
        decimal amount = 1234567.89m;

        var voucher = new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = jType.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(10),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = l1.LedgerId, Debit = amount, Credit = 0m },
                new() { LedgerId = l2.LedgerId, Debit = 0m, Credit = amount }
            }
        };

        // Act
        var saved = await accountingSvc.SaveVoucherAsync(compId, voucher);

        // Assert
        saved.VoucherEntries.First().Debit.Should().Be(1234567.89m);
        saved.VoucherEntries.Last().Credit.Should().Be(1234567.89m);

        var bal1 = await accountingSvc.GetLedgerBalanceAsync(compId, l1.LedgerId);
        bal1.ClosingBalance.Should().Be(1234567.89m);
    }
}
