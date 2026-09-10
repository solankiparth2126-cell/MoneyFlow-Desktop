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
using FinancialYearEntity = MoneyFlow.Core.Entities.FinancialYear;

namespace MoneyFlow.Tests.AccountingTests;

public class Phase7AccountingEngineTests
{
    private (AppDbContext Context, CompanyService CompService, FinancialYearService FyService, LedgerService LedgService, AccountingService AcctService) CreateTestSetup()
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

        var fyService = new FinancialYearService(
            fyRepo,
            companyRepo,
            uow,
            companyContext,
            NullLogger<FinancialYearService>.Instance);

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

        return (context, compService, fyService, ledgService, acctService);
    }

    [Fact]
    public void ValidateVoucher_Balanced_Should_Succeed()
    {
        var (_, _, _, _, acctService) = CreateTestSetup();

        var dto = new VoucherCreateDto
        {
            FinancialYearId = 1,
            VoucherTypeId = 1,
            VoucherDate = new DateTime(2026, 5, 1),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = 1, Debit = 500m, Credit = 0m },
                new() { LedgerId = 2, Debit = 0m, Credit = 500m }
            }
        };

        var result = acctService.ValidateVoucher(dto);

        result.IsValid.Should().BeTrue();
        result.TotalDebit.Should().Be(500m);
        result.TotalCredit.Should().Be(500m);
        result.Difference.Should().Be(0m);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateVoucher_Unbalanced_Should_Fail_With_Calculated_Difference()
    {
        var (_, _, _, _, acctService) = CreateTestSetup();

        var dto = new VoucherCreateDto
        {
            FinancialYearId = 1,
            VoucherTypeId = 1,
            VoucherDate = new DateTime(2026, 5, 1),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = 1, Debit = 500m, Credit = 0m },
                new() { LedgerId = 2, Debit = 0m, Credit = 400m }
            }
        };

        var result = acctService.ValidateVoucher(dto);

        result.IsValid.Should().BeFalse();
        result.TotalDebit.Should().Be(500m);
        result.TotalCredit.Should().Be(400m);
        result.Difference.Should().Be(100m);
        result.Errors.Should().Contain(e => e.Contains("Voucher is not balanced") && e.Contains("100.00"));
    }

    [Fact]
    public void ValidateVoucher_Single_Entry_Should_Fail()
    {
        var (_, _, _, _, acctService) = CreateTestSetup();

        var dto = new VoucherCreateDto
        {
            FinancialYearId = 1,
            VoucherTypeId = 1,
            VoucherDate = new DateTime(2026, 5, 1),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = 1, Debit = 500m, Credit = 0m }
            }
        };

        var result = acctService.ValidateVoucher(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("at least two accounting entries"));
    }

    [Fact]
    public void ValidateVoucher_Both_Debit_And_Credit_On_Same_Line_Should_Fail()
    {
        var (_, _, _, _, acctService) = CreateTestSetup();

        var dto = new VoucherCreateDto
        {
            FinancialYearId = 1,
            VoucherTypeId = 1,
            VoucherDate = new DateTime(2026, 5, 1),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = 1, Debit = 500m, Credit = 500m },
                new() { LedgerId = 2, Debit = 500m, Credit = 500m }
            }
        };

        var result = acctService.ValidateVoucher(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("cannot have both Debit and Credit amounts"));
    }

    [Fact]
    public void ValidateVoucher_Negative_Amount_Should_Fail()
    {
        var (_, _, _, _, acctService) = CreateTestSetup();

        var dto = new VoucherCreateDto
        {
            FinancialYearId = 1,
            VoucherTypeId = 1,
            VoucherDate = new DateTime(2026, 5, 1),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = 1, Debit = -500m, Credit = 0m },
                new() { LedgerId = 2, Debit = 0m, Credit = -500m }
            }
        };

        var result = acctService.ValidateVoucher(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("cannot be negative"));
    }

    [Fact]
    public async Task SaveVoucherAsync_Balanced_Transaction_Should_Persist_And_Generate_VoucherNumber()
    {
        var (context, compService, _, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Balanced Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Direct Expenses");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Main Cash",
            OpeningBalance = 1000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var foodExp = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Food Expense"
        });

        var voucherType = new VoucherType { Name = "Payment", Code = "PMT", Type = VoucherTypeEnum.Payment, Prefix = "PAY-" };
        context.VoucherTypes.Add(voucherType);
        await context.SaveChangesAsync();

        var dto = new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = voucherType.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 10),
            Narration = "Food bill for office lunch",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = foodExp.LedgerId, Debit = 500m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 500m }
            }
        };

        var saved = await acctService.SaveVoucherAsync(company.CompanyId, dto);

        saved.Should().NotBeNull();
        saved.VoucherId.Should().BeGreaterThan(0);
        saved.VoucherNumber.Should().Be("PAY-00001");
        saved.VoucherEntries.Should().HaveCount(2);

        // Verify DB persistence
        var dbVoucher = await context.Vouchers
            .Include(v => v.VoucherEntries)
            .FirstOrDefaultAsync(v => v.VoucherId == saved.VoucherId);

        dbVoucher.Should().NotBeNull();
        dbVoucher!.VoucherEntries.Sum(e => e.Debit).Should().Be(500m);
        dbVoucher.VoucherEntries.Sum(e => e.Credit).Should().Be(500m);
    }

    [Fact]
    public async Task SaveVoucherAsync_Date_Outside_FY_Should_Throw_InvalidOperationException()
    {
        var (context, compService, _, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Boundary Check Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Office Cash"
        });

        var voucherType = new VoucherType { Name = "Payment", Code = "PMT", Type = VoucherTypeEnum.Payment };
        context.VoucherTypes.Add(voucherType);
        await context.SaveChangesAsync();

        var dto = new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = voucherType.VoucherTypeId,
            VoucherDate = new DateTime(2025, 12, 31), // Prior to 01-Apr-2026
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = cash.LedgerId, Debit = 500m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 500m }
            }
        };

        var act = async () => await acctService.SaveVoucherAsync(company.CompanyId, dto);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*outside the active Financial Year*");
    }

    [Fact]
    public async Task GetLedgerBalanceAsync_Should_Calculate_Accurately_From_Transactions()
    {
        var (context, compService, _, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Ledger Balance Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        // Bank has Opening Balance of 10,000 Dr
        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "SBI Bank A/c",
            OpeningBalance = 10000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var sales = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Sales Revenue"
        });

        var rent = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Office Rent"
        });

        var receiptType = new VoucherType { Name = "Receipt", Code = "RCT", Type = VoucherTypeEnum.Receipt };
        var paymentType = new VoucherType { Name = "Payment", Code = "PMT", Type = VoucherTypeEnum.Payment };
        context.VoucherTypes.AddRange(receiptType, paymentType);
        await context.SaveChangesAsync();

        // Transaction 1: Receipt: Bank Dr 5,000, Sales Cr 5,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = receiptType.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 15),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 5000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 5000m }
            }
        });

        // Transaction 2: Payment: Rent Dr 2,000, Bank Cr 2,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 20),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = rent.LedgerId, Debit = 2000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 2000m }
            }
        });

        // Test Dynamic Calculations (Section 8: Never authoritative pre-calculated in DB)
        var bankBalance = await acctService.GetLedgerBalanceAsync(company.CompanyId, bank.LedgerId);
        bankBalance.OpeningBalance.Should().Be(10000m);
        bankBalance.OpeningType.Should().Be(BalanceType.Debit);
        bankBalance.TotalDebit.Should().Be(5000m);
        bankBalance.TotalCredit.Should().Be(2000m);
        bankBalance.ClosingBalance.Should().Be(13000m); // 10,000 + 5,000 - 2,000 = 13,000
        bankBalance.ClosingType.Should().Be(BalanceType.Debit);

        var salesBalance = await acctService.GetLedgerBalanceAsync(company.CompanyId, sales.LedgerId);
        salesBalance.OpeningBalance.Should().Be(0m);
        salesBalance.TotalDebit.Should().Be(0m);
        salesBalance.TotalCredit.Should().Be(5000m);
        salesBalance.ClosingBalance.Should().Be(5000m);
        salesBalance.ClosingType.Should().Be(BalanceType.Credit);

        var rentBalance = await acctService.GetLedgerBalanceAsync(company.CompanyId, rent.LedgerId);
        rentBalance.OpeningBalance.Should().Be(0m);
        rentBalance.TotalDebit.Should().Be(2000m);
        rentBalance.TotalCredit.Should().Be(0m);
        rentBalance.ClosingBalance.Should().Be(2000m);
        rentBalance.ClosingType.Should().Be(BalanceType.Debit);
    }

    [Fact]
    public async Task GetLedgerStatementAsync_Should_Calculate_Running_Balances_Chronologically()
    {
        var (context, compService, _, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Statement Test Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var incomeGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Direct Income");

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "Current Account",
            OpeningBalance = 5000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var serviceIncome = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = incomeGroup.GroupId,
            LedgerName = "Consulting Fees"
        });

        var receiptType = new VoucherType { Name = "Receipt", Code = "RCT", Type = VoucherTypeEnum.Receipt };
        context.VoucherTypes.Add(receiptType);
        await context.SaveChangesAsync();

        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = receiptType.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 10),
            Narration = "Invoice 01",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 2500m, Credit = 0m },
                new() { LedgerId = serviceIncome.LedgerId, Debit = 0m, Credit = 2500m }
            }
        });

        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = receiptType.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 15),
            Narration = "Invoice 02",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 3500m, Credit = 0m },
                new() { LedgerId = serviceIncome.LedgerId, Debit = 0m, Credit = 3500m }
            }
        });

        var statement = await acctService.GetLedgerStatementAsync(
            company.CompanyId,
            bank.LedgerId,
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 30));

        statement.OpeningBalance.Should().Be(5000m);
        statement.OpeningType.Should().Be(BalanceType.Debit);
        statement.Lines.Should().HaveCount(2);

        // Line 1: 5,000 + 2,500 = 7,500 Dr
        statement.Lines[0].Debit.Should().Be(2500m);
        statement.Lines[0].RunningBalance.Should().Be(7500m);
        statement.Lines[0].RunningType.Should().Be(BalanceType.Debit);
        statement.Lines[0].Particulars.Should().Be("Consulting Fees");

        // Line 2: 7,500 + 3,500 = 11,000 Dr
        statement.Lines[1].Debit.Should().Be(3500m);
        statement.Lines[1].RunningBalance.Should().Be(11000m);
        statement.Lines[1].RunningType.Should().Be(BalanceType.Debit);

        statement.TotalDebit.Should().Be(6000m);
        statement.TotalCredit.Should().Be(0m);
        statement.ClosingBalance.Should().Be(11000m);
        statement.ClosingType.Should().Be(BalanceType.Debit);
    }

    [Fact]
    public async Task GetTrialBalanceAsync_Should_Balance_Perfect_Dr_Cr()
    {
        var (context, compService, _, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Trial Balance Perfect Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var capGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Capital Account");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        // Owner Capital: 100,000 Cr Opening
        var capital = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = capGroup.GroupId,
            LedgerName = "Owner Capital",
            OpeningBalance = 100000m,
            OpeningBalanceType = BalanceType.Credit
        });

        // Bank: 100,000 Dr Opening
        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "HDFC Bank",
            OpeningBalance = 100000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var rent = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Rent Expense"
        });

        var paymentType = new VoucherType { Name = "Payment", Code = "PMT", Type = VoucherTypeEnum.Payment };
        context.VoucherTypes.Add(paymentType);
        await context.SaveChangesAsync();

        // Transaction: Pay Rent ₹10,000 via Bank
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 1),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = rent.LedgerId, Debit = 10000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 10000m }
            }
        });

        var tb = await acctService.GetTrialBalanceAsync(
            company.CompanyId,
            new DateTime(2026, 4, 1),
            new DateTime(2027, 3, 31));

        tb.Should().NotBeNull();
        tb.IsBalanced.Should().BeTrue();
        tb.Difference.Should().Be(0m);

        // Opening totals must balance: 100,000 Dr == 100,000 Cr
        tb.TotalOpeningDebit.Should().Be(100000m);
        tb.TotalOpeningCredit.Should().Be(100000m);

        // Period totals must balance: 10,000 Dr == 10,000 Cr
        tb.TotalPeriodDebit.Should().Be(10000m);
        tb.TotalPeriodCredit.Should().Be(10000m);

        // Closing totals must balance:
        // Bank: 90,000 Dr
        // Rent: 10,000 Dr
        // Total Closing Debit = 100,000
        // Capital: 100,000 Cr
        // Total Closing Credit = 100,000
        tb.TotalClosingDebit.Should().Be(100000m);
        tb.TotalClosingCredit.Should().Be(100000m);
    }
}
