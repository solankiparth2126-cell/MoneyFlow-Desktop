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

public class Phase16DayBookTests
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
    public async Task GetDayBook_Should_Return_All_Vouchers_In_Date_Range_Chronologically()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "DayBook Test Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Petty Cash",
            OpeningBalance = 50000m,
            OpeningBalanceType = BalanceType.Debit
        });

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
            LedgerName = "Office Rent"
        });

        var contraType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Contra);
        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        // Voucher 1: 2026-05-02 - Contra (Cash to Bank) ₹10,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = contraType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 2),
            Narration = "Cash deposit into HDFC Bank",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 10000m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 10000m }
            }
        });

        // Voucher 2: 2026-05-10 - Payment (Rent from Bank) ₹15,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 10),
            Narration = "Rent payment for May 2026",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = rent.LedgerId, Debit = 15000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 15000m }
            }
        });

        // Voucher 3: 2026-06-01 (Outside May range) - Payment ₹5,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 1),
            Narration = "June expense",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = rent.LedgerId, Debit = 5000m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 5000m }
            }
        });

        // Query Day Book for May 2026 only
        var report = await acctService.GetDayBookAsync(company.CompanyId, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

        report.Should().NotBeNull();
        report.TotalTransactions.Should().Be(2);
        report.Items.Should().HaveCount(2);

        // Chronological order verification
        report.Items[0].Date.Should().Be(new DateTime(2026, 5, 2));
        report.Items[0].VoucherType.Should().Be(VoucherTypeEnum.Contra);
        report.Items[0].DebitAmount.Should().Be(10000m);
        report.Items[0].CreditAmount.Should().Be(10000m);

        report.Items[1].Date.Should().Be(new DateTime(2026, 5, 10));
        report.Items[1].VoucherType.Should().Be(VoucherTypeEnum.Payment);
        report.Items[1].DebitAmount.Should().Be(15000m);
        report.Items[1].CreditAmount.Should().Be(15000m);

        // Overall Totals
        report.TotalDebit.Should().Be(25000m);
        report.TotalCredit.Should().Be(25000m);
        report.IsBalanced.Should().BeTrue();
        report.Difference.Should().Be(0m);
    }

    [Fact]
    public async Task GetDayBook_With_VoucherType_Filter_Should_Return_Only_Matching_Vouchers()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Filter DayBook Co",
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
            LedgerName = "Target Customer"
        });

        var sales = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Goods Sales"
        });

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Drawer Cash"
        });

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);
        var receiptType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Receipt);

        // 1. Sales Voucher
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 7, 5),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 20000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 20000m }
            }
        });

        // 2. Receipt Voucher
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = receiptType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 7, 6),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = cash.LedgerId, Debit = 10000m, Credit = 0m },
                new() { LedgerId = customer.LedgerId, Debit = 0m, Credit = 10000m }
            }
        });

        // Filter by Sales only
        var salesReport = await acctService.GetDayBookAsync(
            company.CompanyId,
            new DateTime(2026, 7, 1),
            new DateTime(2026, 7, 31),
            VoucherTypeEnum.Sales);

        salesReport.TotalTransactions.Should().Be(1);
        salesReport.Items.Should().ContainSingle(i => i.VoucherType == VoucherTypeEnum.Sales);
        salesReport.TotalDebit.Should().Be(20000m);

        // Filter by Receipt only
        var receiptReport = await acctService.GetDayBookAsync(
            company.CompanyId,
            new DateTime(2026, 7, 1),
            new DateTime(2026, 7, 31),
            VoucherTypeEnum.Receipt);

        receiptReport.TotalTransactions.Should().Be(1);
        receiptReport.Items.Should().ContainSingle(i => i.VoucherType == VoucherTypeEnum.Receipt);
        receiptReport.TotalDebit.Should().Be(10000m);
    }

    [Fact]
    public async Task GetDayBook_SoftDeleted_Vouchers_Must_Not_Appear()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Delete Audit Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Office Cash"
        });

        var tea = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Tea & Refreshments"
        });

        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        var voucher1 = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 8, 1),
            Narration = "Refreshments voucher 1",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = tea.LedgerId, Debit = 200m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 200m }
            }
        });

        var voucher2 = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 8, 2),
            Narration = "Refreshments voucher 2",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = tea.LedgerId, Debit = 300m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 300m }
            }
        });

        // Delete voucher 1
        var deleted = await acctService.DeleteVoucherAsync(voucher1.VoucherId);
        deleted.Should().BeTrue();

        // Day Book must only show voucher 2
        var report = await acctService.GetDayBookAsync(company.CompanyId, new DateTime(2026, 8, 1), new DateTime(2026, 8, 31));

        report.TotalTransactions.Should().Be(1);
        report.Items.Should().NotContain(i => i.VoucherId == voucher1.VoucherId);
        report.Items.Should().ContainSingle(i => i.VoucherId == voucher2.VoucherId);
        report.TotalDebit.Should().Be(300m);
    }
}
