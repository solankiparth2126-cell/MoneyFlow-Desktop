using System;
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
using MoneyFlow.Services;
using MoneyFlow.Services.Company;
using MoneyFlow.Services.FinancialYear;
using Xunit;

namespace MoneyFlow.Tests.AccountingTests;

public class Phase4FinancialYearTests
{
    private (AppDbContext Context, CompanyService CompService, FinancialYearService FYService, CompanyContext CompContext) CreateTestSetup()
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

        var fyService = new FinancialYearService(
            fyRepo,
            companyRepo,
            uow,
            companyContext,
            NullLogger<FinancialYearService>.Instance);

        return (context, compService, fyService, companyContext);
    }

    [Fact]
    public async Task CreateFinancialYearAsync_Should_Create_Valid_FY_With_Computed_Name()
    {
        var (_, compService, fyService, companyContext) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Apex Industries",
            FinancialYearFrom = new DateTime(2026, 4, 1)
        });

        var nextFYDto = new FinancialYearCreateDto
        {
            StartDate = new DateTime(2027, 4, 1),
            EndDate = new DateTime(2028, 3, 31)
        };

        var createdFY = await fyService.CreateFinancialYearAsync(company.CompanyId, nextFYDto);

        createdFY.Should().NotBeNull();
        createdFY.FinancialYearId.Should().BeGreaterThan(0);
        createdFY.YearName.Should().Be("2027-28");
        createdFY.StartDate.Should().Be(new DateTime(2027, 4, 1));
        createdFY.EndDate.Should().Be(new DateTime(2028, 3, 31));
        createdFY.IsClosed.Should().BeFalse();

        var allYears = await fyService.GetFinancialYearsByCompanyAsync(company.CompanyId);
        allYears.Should().HaveCount(2); // Initial 2026-27 + new 2027-28
    }

    [Fact]
    public async Task CreateFinancialYearAsync_Overlapping_Dates_Should_Throw_InvalidOperationException()
    {
        var (_, compService, fyService, _) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Nexus Tech",
            FinancialYearFrom = new DateTime(2026, 4, 1)
        });

        // 2026-27 is 01-Apr-2026 to 31-Mar-2027. Overlapping range: 01-Jan-2027 to 31-Dec-2027
        var overlappingDto = new FinancialYearCreateDto
        {
            StartDate = new DateTime(2027, 1, 1),
            EndDate = new DateTime(2027, 12, 31)
        };

        var act = async () => await fyService.CreateFinancialYearAsync(company.CompanyId, overlappingDto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*overlaps with an existing Financial Year*");
    }

    [Fact]
    public async Task ValidateDateInCurrentFY_Should_Accept_Valid_Dates_And_Reject_Dates_Outside_Active_FY()
    {
        var (_, compService, fyService, companyContext) = CreateTestSetup();

        await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Validations Corp",
            FinancialYearFrom = new DateTime(2026, 4, 1)
        });

        // Active FY is 2026-27: 01-Apr-2026 to 31-Mar-2027
        bool validDate = fyService.ValidateDateInCurrentFY(new DateTime(2026, 9, 10), out string error1);
        validDate.Should().BeTrue();
        error1.Should().BeEmpty();

        // Past date before FY
        bool pastDate = fyService.ValidateDateInCurrentFY(new DateTime(2025, 12, 31), out string error2);
        pastDate.Should().BeFalse();
        error2.Should().Contain("outside the active Financial Year");

        // Future date after FY
        bool futureDate = fyService.ValidateDateInCurrentFY(new DateTime(2027, 4, 1), out string error3);
        futureDate.Should().BeFalse();
        error3.Should().Contain("outside the active Financial Year");
    }

    [Fact]
    public async Task SetActiveFinancialYearAsync_Should_Switch_Active_FY_In_CompanyContext()
    {
        var (_, compService, fyService, companyContext) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Multi FY Co",
            FinancialYearFrom = new DateTime(2026, 4, 1)
        });

        var newFY = await fyService.CreateFinancialYearAsync(company.CompanyId, new FinancialYearCreateDto
        {
            StartDate = new DateTime(2027, 4, 1),
            EndDate = new DateTime(2028, 3, 31)
        });

        // Initially active FY is 2026-27
        companyContext.CurrentFinancialYear?.YearName.Should().Be("2026-27");

        // Switch active FY to 2027-28
        bool switched = await fyService.SetActiveFinancialYearAsync(newFY.FinancialYearId);
        switched.Should().BeTrue();
        companyContext.CurrentFinancialYear?.YearName.Should().Be("2027-28");
    }

    [Fact]
    public async Task CloseFinancialYearAsync_Should_Prevent_Setting_As_Active()
    {
        var (_, compService, fyService, _) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Closing FY Co",
            FinancialYearFrom = new DateTime(2026, 4, 1)
        });

        var oldFY = await fyService.CreateFinancialYearAsync(company.CompanyId, new FinancialYearCreateDto
        {
            StartDate = new DateTime(2025, 4, 1),
            EndDate = new DateTime(2026, 3, 31)
        });

        await fyService.CloseFinancialYearAsync(oldFY.FinancialYearId);

        var act = async () => await fyService.SetActiveFinancialYearAsync(oldFY.FinancialYearId);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*closed and cannot be set as active*");
    }

    [Fact]
    public async Task Section54_Multi_FinancialYear_Vouchers_Should_Be_Scoped_To_Respective_FY()
    {
        // Master Prompt Section 54:
        // Create 2025-26 and 2026-27. Create a voucher in 2025-26.
        // Open 2026-27. Verify that the voucher does not appear in normal 2026-27 reports.
        var (context, compService, fyService, companyContext) = CreateTestSetup();
        var voucherRepo = new VoucherRepository(context);
        var uow = new UnitOfWork(context);

        var dbSetup = new DatabaseSetupService(context, NullLogger<DatabaseSetupService>.Instance);
        await dbSetup.InitializeDatabaseAsync();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Section54 Traders",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true
        });

        var fy2026_27 = companyContext.CurrentFinancialYear!;

        var fy2025_26 = await fyService.CreateFinancialYearAsync(company.CompanyId, new FinancialYearCreateDto
        {
            StartDate = new DateTime(2025, 4, 1),
            EndDate = new DateTime(2026, 3, 31)
        });

        var voucherType = await context.VoucherTypes.FirstAsync(v => v.Code == "PMT");
        var cashLedger = await context.Ledgers.FirstAsync(l => l.CompanyId == company.CompanyId && l.LedgerName == "Cash");

        // Insert Voucher in 2025-26
        var voucher2025 = new Voucher
        {
            CompanyId = company.CompanyId,
            FinancialYearId = fy2025_26.FinancialYearId,
            VoucherTypeId = voucherType.VoucherTypeId,
            VoucherNumber = "PAY-00001",
            VoucherDate = new DateTime(2025, 10, 15),
            Narration = "Voucher in 2025-26"
        };
        await voucherRepo.AddAsync(voucher2025);
        await uow.SaveChangesAsync();

        // Switch context to 2026-27
        await fyService.SetActiveFinancialYearAsync(fy2026_27.FinancialYearId);

        // Query Vouchers for active FY 2026-27
        var activeFYVouchers = await voucherRepo.GetVouchersByDateRangeAsync(
            company.CompanyId,
            companyContext.CurrentFinancialYear!.FinancialYearId,
            companyContext.CurrentFinancialYear!.StartDate,
            companyContext.CurrentFinancialYear!.EndDate);

        // Assert: 2025-26 voucher must NOT appear in normal 2026-27 voucher queries
        activeFYVouchers.Should().BeEmpty();
    }
}
