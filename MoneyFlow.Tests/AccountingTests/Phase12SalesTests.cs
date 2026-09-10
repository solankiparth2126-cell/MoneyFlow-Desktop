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

public class Phase12SalesTests
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
    public async Task SaveSalesVoucher_CreditSale_To_SundryDebtor_Should_Increase_Debtor_And_Sales()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Sales Test Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var debtorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");

        var customer = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = debtorGroup.GroupId,
            LedgerName = "ABC Traders"
        });

        var salesAcount = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Domestic Sales"
        });

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);

        // Section 24 Example: Product A, Qty 10 @ ₹100, Discount ₹50 => Net Total = ₹950
        // Customer ABC Traders Dr ₹950 To Domestic Sales ₹950
        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 2),
            ReferenceNumber = "INV-001",
            Narration = "Product A (Qty: 10 @ ₹100, Disc: ₹50)",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 950m, Credit = 0m, Narration = "Sales to ABC Traders" },
                new() { LedgerId = salesAcount.LedgerId, Debit = 0m, Credit = 950m, Narration = "Product A (Qty: 10 @ ₹100, Disc: ₹50)" }
            }
        });

        voucher.Should().NotBeNull();
        voucher.VoucherNumber.Should().Be("SLS-00001");
        voucher.VoucherEntries.Should().HaveCount(2);

        // ABC Traders debtor balance: ₹950 Dr
        var custBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, customer.LedgerId);
        custBal.ClosingBalance.Should().Be(950m);
        custBal.ClosingType.Should().Be(BalanceType.Debit);

        // Domestic Sales revenue balance: ₹950 Cr
        var salesBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, salesAcount.LedgerId);
        salesBal.ClosingBalance.Should().Be(950m);
        salesBal.ClosingType.Should().Be(BalanceType.Credit);
    }

    [Fact]
    public async Task SaveSalesVoucher_CashSale_Should_Increase_Cash_And_Sales()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Cash Sales Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Counter Cash",
            OpeningBalance = 1000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var salesAcount = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Cash Sales A/c"
        });

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);

        // Cash Sale of ₹4,200
        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 10),
            Narration = "Over-the-counter cash sale",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = cash.LedgerId, Debit = 4200m, Credit = 0m },
                new() { LedgerId = salesAcount.LedgerId, Debit = 0m, Credit = 4200m }
            }
        });

        voucher.Should().NotBeNull();

        // Cash balance: 1,000 + 4,200 = 5,200 Dr
        var cashBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, cash.LedgerId);
        cashBal.ClosingBalance.Should().Be(5200m);

        // Sales balance: 4,200 Cr
        var salesBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, salesAcount.LedgerId);
        salesBal.ClosingBalance.Should().Be(4200m);
    }

    [Fact]
    public async Task GetCustomerPartyLedgersAsync_Should_Return_Debtors_Cash_And_Bank()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Party Lookup Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var debtorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { GroupId = debtorGroup.GroupId, LedgerName = "Customer X" });
        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { GroupId = cashGroup.GroupId, LedgerName = "Cash" });
        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { GroupId = expGroup.GroupId, LedgerName = "Office Stationery" });

        var partyLedgers = await acctService.GetCustomerPartyLedgersAsync(company.CompanyId);

        partyLedgers.Should().Contain(l => l.LedgerName == "Customer X");
        partyLedgers.Should().Contain(l => l.LedgerName == "Cash");
        partyLedgers.Should().NotContain(l => l.LedgerName == "Office Stationery");
    }

    [Fact]
    public async Task GetSalesLedgersAsync_Should_Return_Sales_And_Income()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Sales Lookup Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");
        var incomeGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Direct Income");
        var faGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Fixed Assets");

        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { GroupId = salesGroup.GroupId, LedgerName = "Product Sales" });
        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { GroupId = incomeGroup.GroupId, LedgerName = "Consulting Fees" });
        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { GroupId = faGroup.GroupId, LedgerName = "Machinery" });

        var salesLedgers = await acctService.GetSalesLedgersAsync(company.CompanyId);

        salesLedgers.Should().Contain(l => l.LedgerName == "Product Sales");
        salesLedgers.Should().Contain(l => l.LedgerName == "Consulting Fees");
        salesLedgers.Should().NotContain(l => l.LedgerName == "Machinery");
    }

    [Fact]
    public async Task DeleteSalesVoucher_Should_Restore_Debtor_And_Sales_Balances()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Restore Sales Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var debtorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");

        var customer = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = debtorGroup.GroupId,
            LedgerName = "Client Delta",
            OpeningBalance = 10000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var salesAcount = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Software Sales"
        });

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);

        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 1),
            ReferenceNumber = "INV-099",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 5000m, Credit = 0m },
                new() { LedgerId = salesAcount.LedgerId, Debit = 0m, Credit = 5000m }
            }
        });

        (await acctService.GetLedgerBalanceAsync(company.CompanyId, customer.LedgerId)).ClosingBalance.Should().Be(15000m);
        (await acctService.GetLedgerBalanceAsync(company.CompanyId, salesAcount.LedgerId)).ClosingBalance.Should().Be(5000m);

        var deleted = await acctService.DeleteVoucherAsync(voucher.VoucherId);
        deleted.Should().BeTrue();

        (await acctService.GetLedgerBalanceAsync(company.CompanyId, customer.LedgerId)).ClosingBalance.Should().Be(10000m);
        (await acctService.GetLedgerBalanceAsync(company.CompanyId, salesAcount.LedgerId)).ClosingBalance.Should().Be(0m);
    }
}
