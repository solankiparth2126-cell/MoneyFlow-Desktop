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
using MoneyFlow.Services.Ledger;
using Xunit;
using GroupEntity = MoneyFlow.Core.Entities.Group;
using LedgerEntity = MoneyFlow.Core.Entities.Ledger;

namespace MoneyFlow.Tests.AccountingTests;

public class Phase6LedgerTests
{
    private (AppDbContext Context, CompanyService CompService, GroupService GrpService, LedgerService LedgService) CreateTestSetup()
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

        var ledgService = new LedgerService(
            context,
            ledgerRepo,
            groupRepo,
            companyRepo,
            uow,
            NullLogger<LedgerService>.Instance);

        return (context, compService, grpService, ledgService);
    }

    [Fact]
    public async Task CreateLedgerAsync_Should_Create_Ledger_With_Debit_OpeningBalance()
    {
        var (context, compService, _, ledgService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Test Trading Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");

        var dto = new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "HDFC Bank Current A/c",
            OpeningBalance = 50000m,
            OpeningBalanceType = BalanceType.Debit,
            BankName = "HDFC Bank",
            BankAccountNumber = "50200012345678",
            IFSC = "HDFC0001234"
        };

        var created = await ledgService.CreateLedgerAsync(company.CompanyId, dto);

        created.Should().NotBeNull();
        created.LedgerId.Should().BeGreaterThan(0);
        created.LedgerName.Should().Be("HDFC Bank Current A/c");
        created.OpeningBalance.Should().Be(50000m);
        created.OpeningBalanceType.Should().Be(BalanceType.Debit);
        created.BankName.Should().Be("HDFC Bank");
        created.BankAccountNumber.Should().Be("50200012345678");
        created.IFSC.Should().Be("HDFC0001234");
        created.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateLedgerAsync_Should_Create_Ledger_With_Credit_OpeningBalance()
    {
        var (context, compService, _, ledgService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Vendor Supplies Ltd",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var creditorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Creditors");

        var dto = new LedgerCreateDto
        {
            GroupId = creditorGroup.GroupId,
            LedgerName = "Acme Raw Materials",
            OpeningBalance = 75000m,
            OpeningBalanceType = BalanceType.Credit,
            PAN = "abcde1234f",
            CreditLimit = 100000m,
            CreditDays = 30
        };

        var created = await ledgService.CreateLedgerAsync(company.CompanyId, dto);

        created.Should().NotBeNull();
        created.OpeningBalance.Should().Be(75000m);
        created.OpeningBalanceType.Should().Be(BalanceType.Credit);
        created.PAN.Should().Be("ABCDE1234F"); // Upper-cased
        created.CreditLimit.Should().Be(100000m);
        created.CreditDays.Should().Be(30);
    }

    [Fact]
    public async Task CreateLedgerAsync_Negative_OpeningBalance_Should_Throw_ArgumentException()
    {
        var (context, compService, _, ledgService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Negative Bal Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");

        var dto = new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "Invalid Account",
            OpeningBalance = -500m
        };

        var act = async () => await ledgService.CreateLedgerAsync(company.CompanyId, dto);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Opening balance cannot be negative*");
    }

    [Fact]
    public async Task CreateLedgerAsync_Duplicate_Name_Should_Throw_InvalidOperationException()
    {
        var (context, compService, _, ledgService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Dup Ledger Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");

        var dto1 = new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Petty Cash",
            OpeningBalance = 1000m
        };

        await ledgService.CreateLedgerAsync(company.CompanyId, dto1);

        var dto2 = new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "petty cash", // Case-insensitive duplicate
            OpeningBalance = 2000m
        };

        var act = async () => await ledgService.CreateLedgerAsync(company.CompanyId, dto2);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateLedgerAsync_Should_Update_Details_And_Change_Group()
    {
        var (context, compService, _, ledgService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Update Ledger Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");

        var created = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "Primary Account",
            OpeningBalance = 10000m
        });

        var updateDto = new LedgerUpdateDto
        {
            LedgerId = created.LedgerId,
            GroupId = cashGroup.GroupId, // Changed group
            LedgerName = "Updated Primary Cash",
            OpeningBalance = 15000m,
            OpeningBalanceType = BalanceType.Debit,
            Address = "123 Commercial Street",
            State = "Karnataka",
            Phone = "9876543210"
        };

        var updated = await ledgService.UpdateLedgerAsync(updateDto);

        updated.LedgerName.Should().Be("Updated Primary Cash");
        updated.GroupId.Should().Be(cashGroup.GroupId);
        updated.OpeningBalance.Should().Be(15000m);
        updated.State.Should().Be("Karnataka");
        updated.Phone.Should().Be("9876543210");
        updated.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteLedgerAsync_Without_Vouchers_Should_Succeed()
    {
        var (context, compService, _, ledgService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Delete Safe Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var group = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        var created = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = group.GroupId,
            LedgerName = "Temporary Expense"
        });

        var deleted = await ledgService.DeleteLedgerAsync(created.LedgerId);
        deleted.Should().BeTrue();

        var lookup = await ledgService.GetLedgerByIdAsync(created.LedgerId);
        lookup.Should().BeNull();
    }

    [Fact]
    public async Task DeleteLedgerAsync_With_Voucher_Entries_Should_Throw_InvalidOperationException()
    {
        var (context, compService, _, ledgService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Voucher Delete Check Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var group = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");

        var ledger = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = group.GroupId,
            LedgerName = "Active SBI Bank"
        });

        // Add a voucher and entry referencing this ledger
        var voucherType = new VoucherType { Name = "Payment", Code = "PMT", Type = VoucherTypeEnum.Payment };
        context.VoucherTypes.Add(voucherType);
        await context.SaveChangesAsync();

        var voucher = new Voucher
        {
            CompanyId = company.CompanyId,
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = voucherType.VoucherTypeId,
            VoucherNumber = "PMT-001",
            VoucherDate = new DateTime(2026, 5, 1),
            Narration = "Payment voucher"
        };
        context.Vouchers.Add(voucher);
        await context.SaveChangesAsync();

        var entry = new VoucherEntry
        {
            VoucherId = voucher.VoucherId,
            LedgerId = ledger.LedgerId,
            Debit = 500m,
            Credit = 0m,
            Narration = "Entry line"
        };
        context.VoucherEntries.Add(entry);
        await context.SaveChangesAsync();

        var act = async () => await ledgService.DeleteLedgerAsync(ledger.LedgerId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*transactions have already been recorded*");
    }

    [Fact]
    public async Task GetLedgersByCompanyAsync_Search_And_Filter_Should_Return_Matching_Ledgers()
    {
        var (context, compService, _, ledgService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Search Filter Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true // Seeds 11 default ledgers
        });

        // Test search
        var cashResults = await ledgService.GetLedgersByCompanyAsync(company.CompanyId, searchTerm: "Cash");
        cashResults.Should().Contain(l => l.LedgerName == "Cash");

        // Test group filter
        var debtorsGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var debtorResults = await ledgService.GetLedgersByCompanyAsync(company.CompanyId, groupId: debtorsGroup.GroupId);
        debtorResults.Should().OnlyContain(l => l.GroupId == debtorsGroup.GroupId);
    }

    [Fact]
    public async Task Multi_Company_Ledger_Isolation_Should_Not_Leak_Across_Companies()
    {
        var (context, compService, _, ledgService) = CreateTestSetup();

        var companyA = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Company A",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var companyB = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Company B",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var groupA = await context.Groups.FirstAsync(g => g.CompanyId == companyA.CompanyId && g.GroupName == "Bank Accounts");

        await ledgService.CreateLedgerAsync(companyA.CompanyId, new LedgerCreateDto
        {
            GroupId = groupA.GroupId,
            LedgerName = "Unique Company A Bank"
        });

        var companyBLedgers = await ledgService.GetLedgersByCompanyAsync(companyB.CompanyId);
        companyBLedgers.Should().NotContain(l => l.LedgerName == "Unique Company A Bank");
    }
}
