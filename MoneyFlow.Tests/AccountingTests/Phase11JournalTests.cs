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
using GroupEntity = MoneyFlow.Core.Entities.Group;
using LedgerEntity = MoneyFlow.Core.Entities.Ledger;

namespace MoneyFlow.Tests.AccountingTests;

public class Phase11JournalTests
{
    private (AppDbContext Context, CompanyService CompService, LedgerService LedgService, AccountingService AcctService) CreateTestSetup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var companyRepo = new CompanyRepository(context);
        var fyRepo = new FinancialYearRepository(context);
        var groupRepo = new GroupRepository(context);
        var ledgerRepo = new LedgerRepository(context);
        var voucherRepo = new VoucherRepository(context);
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

        var ledgService = new LedgerService(
            context,
            ledgerRepo,
            groupRepo,
            companyRepo,
            uow,
            NullLogger<LedgerService>.Instance);

        var acctService = new AccountingService(
            context,
            voucherRepo,
            ledgerRepo,
            fyRepo,
            companyRepo,
            uow,
            NullLogger<AccountingService>.Instance);

        return (context, compService, ledgService, acctService);
    }

    [Fact]
    public async Task SaveJournalVoucher_Depreciation_Adjustment_Should_Work()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Journal Adj Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var faGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Fixed Assets");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        // Furniture: Opening Balance ₹50,000 Dr
        var furniture = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = faGroup.GroupId,
            LedgerName = "Office Furniture",
            OpeningBalance = 50000m,
            OpeningBalanceType = BalanceType.Debit
        });

        // Depreciation Expense
        var depExp = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Depreciation Expense"
        });

        var jrnType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Journal);

        // Journal Entry: Depreciation Dr ₹5,000 To Office Furniture ₹5,000
        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = jrnType!.VoucherTypeId,
            VoucherDate = new DateTime(2027, 3, 31),
            Narration = "Year-end depreciation on office furniture at 10%",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = depExp.LedgerId, Debit = 5000m, Credit = 0m, Narration = "Depreciation provision" },
                new() { LedgerId = furniture.LedgerId, Debit = 0m, Credit = 5000m, Narration = "Written down value adjustment" }
            }
        });

        voucher.Should().NotBeNull();
        voucher.VoucherNumber.Should().Be("JRN-00001");
        voucher.VoucherEntries.Should().HaveCount(2);

        // Depreciation Expense balance: ₹5,000 Dr
        var depBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, depExp.LedgerId);
        depBal.ClosingBalance.Should().Be(5000m);
        depBal.ClosingType.Should().Be(BalanceType.Debit);

        // Furniture balance: 50,000 - 5,000 = ₹45,000 Dr
        var furnBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, furniture.LedgerId);
        furnBal.ClosingBalance.Should().Be(45000m);
        furnBal.ClosingType.Should().Be(BalanceType.Debit);
    }

    [Fact]
    public async Task SaveJournalVoucher_Compound_MultiDebit_MultiCredit_Should_Work()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Compound Journal Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");
        var clGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Current Liabilities");

        var salaries = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Salaries"
        });

        var rent = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Office Rent"
        });

        var salPayable = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = clGroup.GroupId,
            LedgerName = "Salary Payable"
        });

        var rentPayable = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = clGroup.GroupId,
            LedgerName = "Rent Payable"
        });

        var jrnType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Journal);

        // Compound Journal:
        // Salaries Dr ₹60,000
        // Office Rent Dr ₹25,000
        //   To Salary Payable ₹60,000
        //   To Rent Payable ₹25,000
        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = jrnType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 30),
            Narration = "Month-end provisions for April 2026",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = salaries.LedgerId, Debit = 60000m, Credit = 0m },
                new() { LedgerId = rent.LedgerId, Debit = 25000m, Credit = 0m },
                new() { LedgerId = salPayable.LedgerId, Debit = 0m, Credit = 60000m },
                new() { LedgerId = rentPayable.LedgerId, Debit = 0m, Credit = 25000m }
            }
        });

        voucher.Should().NotBeNull();
        voucher.VoucherEntries.Should().HaveCount(4);

        // Verification of dynamic balances
        var salBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, salaries.LedgerId);
        salBal.ClosingBalance.Should().Be(60000m);
        salBal.ClosingType.Should().Be(BalanceType.Debit);

        var rentBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, rent.LedgerId);
        rentBal.ClosingBalance.Should().Be(25000m);
        rentBal.ClosingType.Should().Be(BalanceType.Debit);

        var salPayBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, salPayable.LedgerId);
        salPayBal.ClosingBalance.Should().Be(60000m);
        salPayBal.ClosingType.Should().Be(BalanceType.Credit);

        var rentPayBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, rentPayable.LedgerId);
        rentPayBal.ClosingBalance.Should().Be(25000m);
        rentPayBal.ClosingType.Should().Be(BalanceType.Credit);
    }

    [Fact]
    public async Task SaveJournalVoucher_Unbalanced_Should_Throw_InvalidOperationException()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Unbalanced Journal Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        var ledgA = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Expense A"
        });

        var ledgB = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Expense B"
        });

        var jrnType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Journal);

        // Debit ₹5,000 vs Credit ₹4,000
        var act = () => acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = jrnType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 15),
            Narration = "Unbalanced journal test",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledgA.LedgerId, Debit = 5000m, Credit = 0m },
                new() { LedgerId = ledgB.LedgerId, Debit = 0m, Credit = 4000m }
            }
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Voucher is not balanced*");
    }

    [Fact]
    public async Task SaveJournalVoucher_Sequential_Numbering_Should_Increment()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Seq Journal Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        var ledgA = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Ledger A"
        });

        var ledgB = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Ledger B"
        });

        var jrnType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Journal);

        var v1 = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = jrnType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 1),
            Narration = "Journal 1",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledgA.LedgerId, Debit = 1000m, Credit = 0m },
                new() { LedgerId = ledgB.LedgerId, Debit = 0m, Credit = 1000m }
            }
        });

        var v2 = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = jrnType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 2),
            Narration = "Journal 2",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledgA.LedgerId, Debit = 2000m, Credit = 0m },
                new() { LedgerId = ledgB.LedgerId, Debit = 0m, Credit = 2000m }
            }
        });

        v1.VoucherNumber.Should().Be("JRN-00001");
        v2.VoucherNumber.Should().Be("JRN-00002");
    }

    [Fact]
    public async Task DeleteJournalVoucher_Should_Restore_Ledger_Balances()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Restore Journal Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        var ledgA = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Adjusted Ledger A",
            OpeningBalance = 15000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var ledgB = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Adjusted Ledger B",
            OpeningBalance = 30000m,
            OpeningBalanceType = BalanceType.Credit
        });

        var jrnType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Journal);

        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = jrnType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 20),
            Narration = "Adjustment to be reversed",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledgA.LedgerId, Debit = 4000m, Credit = 0m },
                new() { LedgerId = ledgB.LedgerId, Debit = 0m, Credit = 4000m }
            }
        });

        (await acctService.GetLedgerBalanceAsync(company.CompanyId, ledgA.LedgerId)).ClosingBalance.Should().Be(19000m);
        (await acctService.GetLedgerBalanceAsync(company.CompanyId, ledgB.LedgerId)).ClosingBalance.Should().Be(34000m);

        var deleted = await acctService.DeleteVoucherAsync(voucher.VoucherId);
        deleted.Should().BeTrue();

        (await acctService.GetLedgerBalanceAsync(company.CompanyId, ledgA.LedgerId)).ClosingBalance.Should().Be(15000m);
        (await acctService.GetLedgerBalanceAsync(company.CompanyId, ledgB.LedgerId)).ClosingBalance.Should().Be(30000m);
    }
}
