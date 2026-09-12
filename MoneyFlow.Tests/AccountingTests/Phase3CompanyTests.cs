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

    [Fact]
    public async Task CreateMultipleCompanies_Should_Create_Isolated_Directories_And_Not_Mix_Data()
    {
        var (_, service, companyContext) = CreateTestSetup();

        string tempBase = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MoneyFlowTest_" + Guid.NewGuid().ToString("N"));

        try
        {
            var comp1 = await service.CreateCompanyAsync(new CompanyCreateDto
            {
                CompanyName = "Alpha Steel Mills",
                DataDirectory = tempBase,
                CreateDefaultLedgers = true
            });

            var comp2 = await service.CreateCompanyAsync(new CompanyCreateDto
            {
                CompanyName = "Beta Textiles",
                DataDirectory = tempBase,
                CreateDefaultLedgers = true
            });

            // Verify company numbers are distinct
            comp1.CompanyNumber.Should().NotBeNullOrWhiteSpace();
            comp2.CompanyNumber.Should().NotBeNullOrWhiteSpace();
            comp1.CompanyNumber.Should().NotBe(comp2.CompanyNumber);

            // Verify directories are isolated
            comp1.DataDirectory.Should().NotBe(comp2.DataDirectory);
            System.IO.Directory.Exists(comp1.DataDirectory).Should().BeTrue();
            System.IO.Directory.Exists(comp2.DataDirectory).Should().BeTrue();

            // Verify isolated subfolders and metadata
            System.IO.Directory.Exists(System.IO.Path.Combine(comp1.DataDirectory, "Backups")).Should().BeTrue();
            System.IO.Directory.Exists(System.IO.Path.Combine(comp2.DataDirectory, "Backups")).Should().BeTrue();
            System.IO.File.Exists(System.IO.Path.Combine(comp1.DataDirectory, "company.json")).Should().BeTrue();
            System.IO.File.Exists(System.IO.Path.Combine(comp2.DataDirectory, "company.json")).Should().BeTrue();

            // Verify metadata content does not mix
            string meta1 = await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(comp1.DataDirectory, "company.json"));
            string meta2 = await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(comp2.DataDirectory, "company.json"));
            meta1.Should().Contain("Alpha Steel Mills");
            meta1.Should().NotContain("Beta Textiles");
            meta2.Should().Contain("Beta Textiles");
            meta2.Should().NotContain("Alpha Steel Mills");

            // Verify switching companies
            await service.OpenCompanyAsync(comp1.CompanyId);
            companyContext.CurrentCompany?.CompanyName.Should().Be("Alpha Steel Mills");
            companyContext.CurrentCompany?.CompanyNumber.Should().Be(comp1.CompanyNumber);

            await service.OpenCompanyAsync(comp2.CompanyId);
            companyContext.CurrentCompany?.CompanyName.Should().Be("Beta Textiles");
            companyContext.CurrentCompany?.CompanyNumber.Should().Be(comp2.CompanyNumber);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempBase))
            {
                try { System.IO.Directory.Delete(tempBase, true); } catch { }
            }
        }
    }
}
