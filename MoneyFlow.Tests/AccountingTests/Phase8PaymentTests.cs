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

public class Phase8PaymentTests
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
    public async Task SavePaymentVoucher_Single_Debit_Should_Create_Balanced_Voucher()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Payment Test Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Direct Expenses");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Cash Account",
            OpeningBalance = 10000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var foodExp = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Food & Refreshment"
        });

        var pmtType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = pmtType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 10),
            Narration = "Food expense payment",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = foodExp.LedgerId, Debit = 500m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 500m }
            }
        });

        voucher.Should().NotBeNull();
        voucher.VoucherNumber.Should().Be("PAY-00001");
        voucher.VoucherEntries.Should().HaveCount(2);

        var cashBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, cash.LedgerId);
        cashBal.ClosingBalance.Should().Be(9500m); // 10,000 - 500
        cashBal.ClosingType.Should().Be(BalanceType.Debit);

        var expBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, foodExp.LedgerId);
        expBal.ClosingBalance.Should().Be(500m);
        expBal.ClosingType.Should().Be(BalanceType.Debit);
    }

    [Fact]
    public async Task SavePaymentVoucher_Compound_Debit_Should_Create_Balanced_Voucher()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Compound Payment Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "HDFC Bank A/c",
            OpeningBalance = 50000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var rent = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Office Rent"
        });

        var electricity = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Electricity Bill"
        });

        var pmtType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        // Pay Rent (10,000) and Electricity (5,000) through Bank (15,000)
        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = pmtType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 1),
            Narration = "Utilities and rent payment",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = rent.LedgerId, Debit = 10000m, Credit = 0m, Narration = "Office Rent May" },
                new() { LedgerId = electricity.LedgerId, Debit = 5000m, Credit = 0m, Narration = "Electricity May" },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 15000m, Narration = "Paid via NEFT" }
            }
        });

        voucher.VoucherEntries.Should().HaveCount(3);

        var bankBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, bank.LedgerId);
        bankBal.ClosingBalance.Should().Be(35000m); // 50,000 - 15,000
        bankBal.ClosingType.Should().Be(BalanceType.Debit);
    }

    [Fact]
    public async Task GetCashAndBankLedgersAsync_Should_Return_Only_Cash_And_Bank_Ledgers()
    {
        var (_, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Ledger Filter Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true // Seeds 11 default ledgers
        });

        var cashAndBank = await acctService.GetCashAndBankLedgersAsync(company.CompanyId);

        cashAndBank.Should().NotBeEmpty();
        cashAndBank.Should().Contain(l => l.LedgerName == "Cash");
        cashAndBank.Should().NotContain(l => l.LedgerName == "Sales");
        cashAndBank.Should().NotContain(l => l.LedgerName == "Purchase");
        cashAndBank.Should().NotContain(l => l.LedgerName == "Capital Account");
    }

    [Fact]
    public async Task SavePaymentVoucher_Sequential_Numbering_Should_Increment_Properly()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Voucher Seq Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Direct Expenses");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Main Cash"
        });

        var tea = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Tea Expenses"
        });

        var pmtType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        var v1 = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = pmtType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 10),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = tea.LedgerId, Debit = 100m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 100m }
            }
        });

        var v2 = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = pmtType.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 11),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = tea.LedgerId, Debit = 200m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 200m }
            }
        });

        v1.VoucherNumber.Should().Be("PAY-00001");
        v2.VoucherNumber.Should().Be("PAY-00002");
    }

    [Fact]
    public async Task DeleteVoucherAsync_SoftDelete_Should_Restore_Ledger_Balance()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Restore Balance Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Direct Expenses");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Safe Cash",
            OpeningBalance = 10000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var travel = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Travel Expense"
        });

        var pmtType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = pmtType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 15),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = travel.LedgerId, Debit = 3000m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 3000m }
            }
        });

        // Balance before delete: 10,000 - 3,000 = 7,000
        var balBefore = await acctService.GetLedgerBalanceAsync(company.CompanyId, cash.LedgerId);
        balBefore.ClosingBalance.Should().Be(7000m);

        // Soft delete the voucher
        var deleted = await acctService.DeleteVoucherAsync(voucher.VoucherId);
        deleted.Should().BeTrue();

        // Balance after soft delete: restored to 10,000 Dr
        var balAfter = await acctService.GetLedgerBalanceAsync(company.CompanyId, cash.LedgerId);
        balAfter.ClosingBalance.Should().Be(10000m);
        balAfter.TotalCredit.Should().Be(0m);
    }
}
