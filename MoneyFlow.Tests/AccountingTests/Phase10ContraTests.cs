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

public class Phase10ContraTests
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
    public async Task SaveContraVoucher_Cash_Deposit_To_Bank_Should_Increase_Bank_And_Decrease_Cash()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Contra Deposit Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");

        // Cash Opening Balance: ₹50,000 Dr
        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Office Cash",
            OpeningBalance = 50000m,
            OpeningBalanceType = BalanceType.Debit
        });

        // Bank Opening Balance: ₹10,000 Dr
        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "HDFC Current A/c",
            OpeningBalance = 10000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var contraType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Contra);

        // Deposit ₹15,000 cash into Bank: HDFC Bank Dr 15,000 To Office Cash 15,000
        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = contraType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 10),
            Narration = "Cash deposited into HDFC Bank",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 15000m, Credit = 0m, Narration = "Deposit" },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 15000m, Narration = "Cash deposited" }
            }
        });

        voucher.Should().NotBeNull();
        voucher.VoucherNumber.Should().Be("CTR-00001");
        voucher.VoucherEntries.Should().HaveCount(2);

        // Verify Balances:
        // Cash: 50,000 - 15,000 = 35,000 Dr
        var cashBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, cash.LedgerId);
        cashBal.ClosingBalance.Should().Be(35000m);
        cashBal.ClosingBalanceType.Should().Be(BalanceType.Debit);

        // Bank: 10,000 + 15,000 = 25,000 Dr
        var bankBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, bank.LedgerId);
        bankBal.ClosingBalance.Should().Be(25000m);
        bankBal.ClosingBalanceType.Should().Be(BalanceType.Debit);
    }

    [Fact]
    public async Task SaveContraVoucher_Cash_Withdrawal_From_Bank_Should_Increase_Cash_And_Decrease_Bank()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Contra Withdrawal Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Main Cash",
            OpeningBalance = 5000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "SBI Current A/c",
            OpeningBalance = 60000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var contraType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Contra);

        // Withdraw ₹20,000 from SBI: Main Cash Dr 20,000 To SBI Current A/c 20,000
        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = contraType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 2),
            Narration = "Cash withdrawn for office petty expenses via Cheque #102938",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = cash.LedgerId, Debit = 20000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 20000m, Narration = "[Cheque #102938] Self withdrawal" }
            }
        });

        voucher.Should().NotBeNull();
        voucher.VoucherNumber.Should().Be("CTR-00001");

        // Cash: 5,000 + 20,000 = 25,000 Dr
        var cashBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, cash.LedgerId);
        cashBal.ClosingBalance.Should().Be(25000m);

        // Bank: 60,000 - 20,000 = 40,000 Dr
        var bankBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, bank.LedgerId);
        bankBal.ClosingBalance.Should().Be(40000m);
    }

    [Fact]
    public async Task SaveContraVoucher_Bank_To_Bank_Transfer_Should_Work_Correctly()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Bank Transfer Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");

        var hdfc = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "HDFC Bank",
            OpeningBalance = 100000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var icici = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "ICICI Bank",
            OpeningBalance = 25000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var contraType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Contra);

        // Transfer ₹30,000 from HDFC to ICICI: ICICI Dr 30,000 To HDFC 30,000
        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = contraType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 7, 1),
            Narration = "Fund transfer via NEFT",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = icici.LedgerId, Debit = 30000m, Credit = 0m },
                new() { LedgerId = hdfc.LedgerId, Debit = 0m, Credit = 30000m, Narration = "[NEFT/RTGS] Txn ref #892837482" }
            }
        });

        voucher.Should().NotBeNull();

        // ICICI: 25,000 + 30,000 = 55,000 Dr
        var iciciBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, icici.LedgerId);
        iciciBal.ClosingBalance.Should().Be(55000m);

        // HDFC: 100,000 - 30,000 = 70,000 Dr
        var hdfcBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, hdfc.LedgerId);
        hdfcBal.ClosingBalance.Should().Be(70000m);
    }

    [Fact]
    public async Task SaveContraVoucher_Non_CashBank_Ledger_Should_Throw_InvalidOperationException()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Invalid Contra Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Cash A/c"
        });

        var rent = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Office Rent"
        });

        var contraType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Contra);

        // Attempting to record Contra with an expense ledger must be strictly rejected
        var act = () => acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = contraType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 20),
            Narration = "Invalid contra with expense account",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = rent.LedgerId, Debit = 5000m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 5000m }
            }
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Contra vouchers can only be recorded between Cash and Bank accounts*");
    }

    [Fact]
    public async Task DeleteContraVoucher_Should_Restore_Both_Account_Balances()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Restore Contra Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Petty Cash",
            OpeningBalance = 20000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "Axis Bank",
            OpeningBalance = 50000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var contraType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Contra);

        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = contraType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 8, 10),
            Narration = "Deposit to Axis Bank",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 8000m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 8000m }
            }
        });

        // Balances after deposit
        (await acctService.GetLedgerBalanceAsync(company.CompanyId, bank.LedgerId)).ClosingBalance.Should().Be(58000m);
        (await acctService.GetLedgerBalanceAsync(company.CompanyId, cash.LedgerId)).ClosingBalance.Should().Be(12000m);

        // Delete the voucher
        var deleted = await acctService.DeleteVoucherAsync(voucher.VoucherId);
        deleted.Should().BeTrue();

        // Balances restored to initial opening balances
        (await acctService.GetLedgerBalanceAsync(company.CompanyId, bank.LedgerId)).ClosingBalance.Should().Be(50000m);
        (await acctService.GetLedgerBalanceAsync(company.CompanyId, cash.LedgerId)).ClosingBalance.Should().Be(20000m);
    }
}
