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

public class Phase9ReceiptTests
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
    public async Task SaveReceiptVoucher_Single_Credit_Should_Create_Balanced_Voucher()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Receipt Test Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var incomeGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Direct Income");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Counter Cash",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var servicesIncome = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = incomeGroup.GroupId,
            LedgerName = "Software Services Income"
        });

        var rctType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Receipt);

        // Receipt: Cash Dr 10,000 To Software Services Income 10,000
        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = rctType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 15),
            Narration = "Software services fee received in cash",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = servicesIncome.LedgerId, Debit = 0m, Credit = 10000m },
                new() { LedgerId = cash.LedgerId, Debit = 10000m, Credit = 0m }
            }
        });

        voucher.Should().NotBeNull();
        voucher.VoucherNumber.Should().Be("RCT-00001");
        voucher.VoucherEntries.Should().HaveCount(2);

        // Cash increases by ₹10,000
        var cashBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, cash.LedgerId);
        cashBal.ClosingBalance.Should().Be(10000m);
        cashBal.ClosingType.Should().Be(BalanceType.Debit);

        // Income increases by ₹10,000 Cr
        var incBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, servicesIncome.LedgerId);
        incBal.ClosingBalance.Should().Be(10000m);
        incBal.ClosingType.Should().Be(BalanceType.Credit);
    }

    [Fact]
    public async Task SaveReceiptVoucher_Compound_Credit_Should_Create_Balanced_Voucher()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Compound Receipt Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var debtorsGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var incomeGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Income");

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "ICICI Bank",
            OpeningBalance = 20000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var customer = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = debtorsGroup.GroupId,
            LedgerName = "Alpha Client Ltd",
            OpeningBalance = 15000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var interest = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = incomeGroup.GroupId,
            LedgerName = "Bank Interest Received"
        });

        var rctType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Receipt);

        // Receive ₹15,000 from Alpha Client and ₹2,000 Interest into Bank (Total: ₹17,000 Dr)
        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = rctType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 20),
            Narration = "Received client dues and bank interest",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 0m, Credit = 15000m, Narration = "Full settlement" },
                new() { LedgerId = interest.LedgerId, Debit = 0m, Credit = 2000m, Narration = "Q1 Interest" },
                new() { LedgerId = bank.LedgerId, Debit = 17000m, Credit = 0m, Narration = "Deposit into ICICI" }
            }
        });

        voucher.VoucherEntries.Should().HaveCount(3);

        // Bank balance: 20,000 + 17,000 = 37,000 Dr
        var bankBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, bank.LedgerId);
        bankBal.ClosingBalance.Should().Be(37000m);
        bankBal.ClosingType.Should().Be(BalanceType.Debit);

        // Customer balance: 15,000 Dr - 15,000 Cr = 0
        var custBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, customer.LedgerId);
        custBal.ClosingBalance.Should().Be(0m);
    }

    [Fact]
    public async Task SaveReceiptVoucher_Sequential_Numbering_Should_Increment_Properly()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Receipt Numbering Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var incomeGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Direct Income");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Cash"
        });

        var income = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = incomeGroup.GroupId,
            LedgerName = "Commission Income"
        });

        var rctType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Receipt);

        var v1 = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = rctType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 10),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = income.LedgerId, Debit = 0m, Credit = 1000m },
                new() { LedgerId = cash.LedgerId, Debit = 1000m, Credit = 0m }
            }
        });

        var v2 = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = rctType.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 11),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = income.LedgerId, Debit = 0m, Credit = 2000m },
                new() { LedgerId = cash.LedgerId, Debit = 2000m, Credit = 0m }
            }
        });

        v1.VoucherNumber.Should().Be("RCT-00001");
        v2.VoucherNumber.Should().Be("RCT-00002");
    }

    [Fact]
    public async Task DeleteReceiptVoucher_SoftDelete_Should_Reverse_Cash_Increase()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Receipt Reversal Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var incomeGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Direct Income");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Vault Cash",
            OpeningBalance = 5000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var fees = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = incomeGroup.GroupId,
            LedgerName = "Service Fees"
        });

        var rctType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Receipt);

        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = rctType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 1),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = fees.LedgerId, Debit = 0m, Credit = 8000m },
                new() { LedgerId = cash.LedgerId, Debit = 8000m, Credit = 0m }
            }
        });

        // Cash was 5,000 + 8,000 = 13,000 Dr
        var balBefore = await acctService.GetLedgerBalanceAsync(company.CompanyId, cash.LedgerId);
        balBefore.ClosingBalance.Should().Be(13000m);

        // Soft delete the receipt
        var deleted = await acctService.DeleteVoucherAsync(voucher.VoucherId);
        deleted.Should().BeTrue();

        // Cash restored to initial 5,000 Dr
        var balAfter = await acctService.GetLedgerBalanceAsync(company.CompanyId, cash.LedgerId);
        balAfter.ClosingBalance.Should().Be(5000m);
        balAfter.TotalDebit.Should().Be(0m);
    }
}
