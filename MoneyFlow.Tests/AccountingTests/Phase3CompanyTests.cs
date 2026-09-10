using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Data;
using MoneyFlow.Data.Repositories;
using MoneyFlow.Services.Company;
using Xunit;

namespace MoneyFlow.Tests.AccountingTests;

public class Phase3CompanyTests
{
    private (AppDbContext Context, CompanyService Service, CompanyContext CompanyContext) CreateTestSetup()
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
        var logger = NullLogger<CompanyService>.Instance;

        var service = new CompanyService(
            companyRepo,
            fyRepo,
            groupRepo,
            ledgerRepo,
            uow,
            companyContext,
            logger);

        return (context, service, companyContext);
    }

    [Fact]
    public async Task CreateCompanyAsync_Should_Create_Company_With_Default_Groups_And_FY()
    {
        var (context, service, companyContext) = CreateTestSetup();

        var dto = new CompanyCreateDto
        {
            CompanyName = "ABC Traders",
            Address = "123 Business Street",
            State = "Maharashtra",
            Country = "India",
            PAN = "ABCDE1234F",
            Email = "info@abctraders.com",
            Phone = "9876543210",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            BooksBeginningFrom = new DateTime(2026, 4, 1),
            Currency = "₹",
            CreateDefaultLedgers = false
        };

        var created = await service.CreateCompanyAsync(dto);

        created.Should().NotBeNull();
        created.CompanyId.Should().BeGreaterThan(0);
        created.CompanyName.Should().Be("ABC Traders");

        // Verify FY
        var fys = await context.FinancialYears.Where(f => f.CompanyId == created.CompanyId).ToListAsync();
        fys.Should().HaveCount(1);
        fys.First().YearName.Should().Be("2026-27");

        // Verify 17 Default Groups (13 primary + 4 subgroups)
        var groups = await context.Groups.Where(g => g.CompanyId == created.CompanyId).ToListAsync();
        groups.Should().HaveCount(17);
        groups.Any(g => g.GroupName == "Cash-in-Hand").Should().BeTrue();
        groups.Any(g => g.GroupName == "Bank Accounts").Should().BeTrue();
        groups.Any(g => g.GroupName == "Sundry Debtors").Should().BeTrue();
        groups.Any(g => g.GroupName == "Sundry Creditors").Should().BeTrue();

        // Verify Active Context
        companyContext.IsCompanyOpen.Should().BeTrue();
        companyContext.CurrentCompany?.CompanyName.Should().Be("ABC Traders");
        companyContext.CurrentFinancialYear?.YearName.Should().Be("2026-27");
    }

    [Fact]
    public async Task CreateCompanyAsync_With_Default_Ledgers_Should_Seed_Ledgers()
    {
        var (context, service, _) = CreateTestSetup();

        var dto = new CompanyCreateDto
        {
            CompanyName = "Zenith Retail",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            BooksBeginningFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true
        };

        var created = await service.CreateCompanyAsync(dto);

        var ledgers = await context.Ledgers.Where(l => l.CompanyId == created.CompanyId).ToListAsync();
        ledgers.Should().HaveCount(11);
        ledgers.Any(l => l.LedgerName == "Cash").Should().BeTrue();
        ledgers.Any(l => l.LedgerName == "Profit & Loss A/c").Should().BeTrue();
        ledgers.Any(l => l.LedgerName == "Sales").Should().BeTrue();
        ledgers.Any(l => l.LedgerName == "Purchase").Should().BeTrue();
    }

    [Fact]
    public async Task CreateCompanyAsync_Duplicate_Name_Should_Throw_InvalidOperationException()
    {
        var (_, service, _) = CreateTestSetup();

        var dto1 = new CompanyCreateDto { CompanyName = "Global Logistics" };
        await service.CreateCompanyAsync(dto1);

        var dto2 = new CompanyCreateDto { CompanyName = "GLOBAL LOGISTICS" };
        var act = async () => await service.CreateCompanyAsync(dto2);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateCompanyAsync_Should_Modify_Details()
    {
        var (context, service, _) = CreateTestSetup();

        var created = await service.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Original Name",
            State = "Delhi"
        });

        var updateDto = new CompanyUpdateDto
        {
            CompanyId = created.CompanyId,
            CompanyName = "Updated Name",
            State = "Karnataka",
            Address = "MG Road, Bengaluru",
            IsActive = true
        };

        var updated = await service.UpdateCompanyAsync(updateDto);

        updated.CompanyName.Should().Be("Updated Name");
        updated.State.Should().Be("Karnataka");
        updated.Address.Should().Be("MG Road, Bengaluru");

        var dbCompany = await context.Companies.FindAsync(created.CompanyId);
        dbCompany!.CompanyName.Should().Be("Updated Name");
    }

    [Fact]
    public async Task Switch_Company_Should_Update_CompanyContext()
    {
        var (_, service, companyContext) = CreateTestSetup();

        var comp1 = await service.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Company One" });
        companyContext.CurrentCompany?.CompanyName.Should().Be("Company One");

        var comp2 = await service.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Company Two" });
        companyContext.CurrentCompany?.CompanyName.Should().Be("Company Two");

        // Switch back to Company 1
        bool opened = await service.OpenCompanyAsync(comp1.CompanyId);
        opened.Should().BeTrue();
        companyContext.CurrentCompany?.CompanyName.Should().Be("Company One");

        // Close Company
        service.CloseCompany();
        companyContext.IsCompanyOpen.Should().BeFalse();
        companyContext.CurrentCompany.Should().BeNull();
    }
}
