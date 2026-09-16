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
using MoneyFlow.Services.Company;
using MoneyFlow.Services.Group;
using Xunit;

namespace MoneyFlow.Tests.AccountingTests;

public class Phase5GroupTests
{
    private (AppDbContext Context, CompanyService CompService, GroupService GrpService, CompanyContext CompContext) CreateTestSetup()
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

        var grpService = new GroupService(
            context,
            groupRepo,
            companyRepo,
            uow,
            NullLogger<GroupService>.Instance);

        return (context, compService, grpService, companyContext);
    }

    [Fact]
    public async Task CreateGroupAsync_Should_Create_SubGroup_Inheriting_Nature()
    {
        var (context, compService, grpService, _) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Omega Trading",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentAssets = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Current Assets");

        var dto = new GroupCreateDto
        {
            GroupName = "Short Term Deposits",
            ParentGroupId = currentAssets.GroupId
        };

        var created = await grpService.CreateGroupAsync(company.CompanyId, dto);

        created.Should().NotBeNull();
        created.GroupName.Should().Be("Short Term Deposits");
        created.ParentGroupId.Should().Be(currentAssets.GroupId);
        created.Nature.Should().Be(GroupNature.Assets); // Inherited from parent
        created.PrimaryGroup.Should().BeFalse();
    }

    [Fact]
    public async Task CreateGroupAsync_Primary_Should_Allow_Explicit_Nature()
    {
        var (_, compService, grpService, _) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Primary Group Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var dto = new GroupCreateDto
        {
            GroupName = "Special Reserves",
            ParentGroupId = null,
            Nature = GroupNature.Liabilities
        };

        var created = await grpService.CreateGroupAsync(company.CompanyId, dto);

        created.Should().NotBeNull();
        created.GroupName.Should().Be("Special Reserves");
        created.ParentGroupId.Should().BeNull();
        created.Nature.Should().Be(GroupNature.Liabilities);
        created.PrimaryGroup.Should().BeTrue();
    }

    [Fact]
    public async Task CreateGroupAsync_Duplicate_Name_Should_Throw_InvalidOperationException()
    {
        var (_, compService, grpService, _) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Duplicate Check Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        // "Bank Accounts" is already seeded by default
        var dto = new GroupCreateDto
        {
            GroupName = "Bank Accounts",
            Nature = GroupNature.Assets
        };

        var act = async () => await grpService.CreateGroupAsync(company.CompanyId, dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateGroupAsync_Circular_Reference_Should_Throw_InvalidOperationException()
    {
        var (_, compService, grpService, _) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Circular Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        // Hierarchy: Level1 -> Level2 -> Level3
        var level1 = await grpService.CreateGroupAsync(company.CompanyId, new GroupCreateDto { GroupName = "Level 1", Nature = GroupNature.Assets });
        var level2 = await grpService.CreateGroupAsync(company.CompanyId, new GroupCreateDto { GroupName = "Level 2", ParentGroupId = level1.GroupId });
        var level3 = await grpService.CreateGroupAsync(company.CompanyId, new GroupCreateDto { GroupName = "Level 3", ParentGroupId = level2.GroupId });

        // Attempting to set Level1's parent to Level3 (circular dependency)
        var act1 = async () => await grpService.UpdateGroupAsync(new GroupUpdateDto
        {
            GroupId = level1.GroupId,
            GroupName = "Level 1",
            ParentGroupId = level3.GroupId
        });

        await act1.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*circular reference*");

        // Attempting to set Level1's parent to itself
        var act2 = async () => await grpService.UpdateGroupAsync(new GroupUpdateDto
        {
            GroupId = level1.GroupId,
            GroupName = "Level 1",
            ParentGroupId = level1.GroupId
        });

        await act2.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*own parent*");
    }

    [Fact]
    public async Task DeleteGroupAsync_With_SubGroups_Should_Throw_InvalidOperationException()
    {
        var (context, compService, grpService, _) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Delete Protect Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentAssets = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Current Assets");

        var act = async () => await grpService.DeleteGroupAsync(currentAssets.GroupId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*contains sub-groups*");
    }

    [Fact]
    public async Task DeleteGroupAsync_With_Ledgers_Should_Throw_InvalidOperationException()
    {
        var (context, compService, grpService, _) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Ledger Protect Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true // Creates Cash under Cash-in-Hand
        });

        var cashInHand = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && (g.GroupName == "Cash-in-hand" || g.GroupName == "Cash-in-Hand"));

        var act = async () => await grpService.DeleteGroupAsync(cashInHand.GroupId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*contains ledgers*");
    }

    [Fact]
    public async Task GetGroupTreeAsync_Should_Return_Hierarchical_Structure()
    {
        var (_, compService, grpService, _) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Tree Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var tree = await grpService.GetGroupTreeAsync(company.CompanyId);

        tree.Should().NotBeEmpty();
        var currentAssets = tree.FirstOrDefault(t => t.GroupName == "Current Assets");
        currentAssets.Should().NotBeNull();
        currentAssets!.Children.Should().NotBeEmpty();
        currentAssets.Children.Any(c => c.GroupName == "Bank Accounts").Should().BeTrue();
        currentAssets.Children.Any(c => c.GroupName.Equals("Cash-in-hand", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }
}
