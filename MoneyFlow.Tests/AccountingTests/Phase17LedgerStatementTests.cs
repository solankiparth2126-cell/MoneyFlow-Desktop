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

public class Phase17LedgerStatementTests
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
    public async Task GetLedgerStatement_With_Prior_Transactions_Should_Calculate_Correct_Opening_Balance()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Statement Test Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var debtorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");

        // Customer opening balance: ₹10,000 Dr
        var customer = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = debtorGroup.GroupId,
            LedgerName = "Reliable Traders",
            OpeningBalance = 10000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var sales = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Hardware Sales"
        });

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Till Cash"
        });

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);
        var receiptType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Receipt);

        // April 10: Sale ₹5,000 (Dr Customer, Cr Sales)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 10),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 5000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 5000m }
            }
        });

        // April 20: Receipt ₹3,000 (Dr Cash, Cr Customer)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = receiptType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 20),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = cash.LedgerId, Debit = 3000m, Credit = 0m },
                new() { LedgerId = customer.LedgerId, Debit = 0m, Credit = 3000m }
            }
        });

        // Query statement for May: From 2026-05-01 to 2026-05-31
        var statement = await acctService.GetLedgerStatementAsync(
            company.CompanyId,
            customer.LedgerId,
            new DateTime(2026, 5, 1),
            new DateTime(2026, 5, 31));

        statement.Should().NotBeNull();
        statement.LedgerName.Should().Be("Reliable Traders");
        statement.GroupName.Should().Be("Sundry Debtors");

        // Dynamic opening balance: 10,000 + 5,000 - 3,000 = 12,000 Dr
        statement.OpeningBalance.Should().Be(12000m);
        statement.OpeningType.Should().Be(BalanceType.Debit);
        statement.Lines.Should().BeEmpty(); // No transactions in May yet
        statement.ClosingBalance.Should().Be(12000m);
        statement.ClosingType.Should().Be(BalanceType.Debit);
    }

    [Fact]
    public async Task GetLedgerStatement_Should_Track_Running_Balance_And_Opposing_Particulars()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Running Balance Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var debtorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");

        var customer = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = debtorGroup.GroupId,
            LedgerName = "City Electronics",
            OpeningBalance = 0m
        });

        var sales = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Component Sales"
        });

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Counter Cash"
        });

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);
        var receiptType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Receipt);

        // Transaction 1: 2026-05-05: Sales ₹8,000 (Dr Customer, Cr Sales)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 5),
            ReferenceNumber = "INV-501",
            Narration = "Sale of microcontrollers",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 8000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 8000m }
            }
        });

        // Transaction 2: 2026-05-15: Receipt ₹5,000 (Dr Cash, Cr Customer)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = receiptType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 15),
            ReferenceNumber = "RCT-201",
            Narration = "Partial payment received",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = cash.LedgerId, Debit = 5000m, Credit = 0m },
                new() { LedgerId = customer.LedgerId, Debit = 0m, Credit = 5000m }
            }
        });

        var statement = await acctService.GetLedgerStatementAsync(
            company.CompanyId,
            customer.LedgerId,
            new DateTime(2026, 5, 1),
            new DateTime(2026, 5, 31));

        statement.Lines.Should().HaveCount(2);

        // Line 1: Sales
        var line1 = statement.Lines[0];
        line1.Particulars.Should().Be("Component Sales");
        line1.Debit.Should().Be(8000m);
        line1.Credit.Should().Be(0m);
        line1.RunningBalance.Should().Be(8000m);
        line1.RunningType.Should().Be(BalanceType.Debit);

        // Line 2: Receipt
        var line2 = statement.Lines[1];
        line2.Particulars.Should().Be("Counter Cash");
        line2.Debit.Should().Be(0m);
        line2.Credit.Should().Be(5000m);
        line2.RunningBalance.Should().Be(3000m);
        line2.RunningType.Should().Be(BalanceType.Debit);

        // Totals
        statement.TotalDebit.Should().Be(8000m);
        statement.TotalCredit.Should().Be(5000m);
        statement.ClosingBalance.Should().Be(3000m);
        statement.ClosingType.Should().Be(BalanceType.Debit);
    }

    [Fact]
    public async Task GetLedgerStatement_SoftDeleted_Vouchers_Must_Not_Affect_Statement()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Delete Statement Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "SBI Current",
            OpeningBalance = 50000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var electricity = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Electricity Expense"
        });

        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        // Valid voucher
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 5),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = electricity.LedgerId, Debit = 4000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 4000m }
            }
        });

        // Voucher to be deleted
        var voucherToDelete = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 10),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = electricity.LedgerId, Debit = 90000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 90000m }
            }
        });

        // Delete voucher
        var deleted = await acctService.DeleteVoucherAsync(voucherToDelete.VoucherId);
        deleted.Should().BeTrue();

        var statement = await acctService.GetLedgerStatementAsync(
            company.CompanyId,
            bank.LedgerId,
            new DateTime(2026, 6, 1),
            new DateTime(2026, 6, 30));

        statement.Lines.Should().HaveCount(1);
        statement.Lines[0].Debit.Should().Be(0m);
        statement.Lines[0].Credit.Should().Be(4000m);
        statement.ClosingBalance.Should().Be(46000m);
        statement.ClosingType.Should().Be(BalanceType.Debit);
    }
}
