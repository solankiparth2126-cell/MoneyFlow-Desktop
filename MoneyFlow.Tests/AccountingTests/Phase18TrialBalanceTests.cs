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

public class Phase18TrialBalanceTests
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
    public async Task GetTrialBalance_Should_Reconcile_All_Vouchers_And_Balance_Dr_Cr()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "TB Reconcile Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var capGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Capital Account");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var debtorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var creditorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Creditors");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");
        var purchaseGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Purchase Accounts");

        // Master Ledgers with balanced opening setup (Capital ₹5,00,000 Cr, Bank ₹5,00,000 Dr)
        var capital = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = capGroup.GroupId,
            LedgerName = "Owner Capital",
            OpeningBalance = 500000m,
            OpeningBalanceType = BalanceType.Credit
        });

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "Primary Bank",
            OpeningBalance = 500000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var customer = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = debtorGroup.GroupId,
            LedgerName = "Zenith Ltd"
        });

        var supplier = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = creditorGroup.GroupId,
            LedgerName = "Vortex Corp"
        });

        var sales = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Standard Sales"
        });

        var purchase = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = purchaseGroup.GroupId,
            LedgerName = "Direct Purchases"
        });

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);
        var purchaseType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Purchase);
        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);
        var receiptType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Receipt);

        // 1. Purchase on credit: ₹1,00,000 (Purchase Dr, Supplier Cr)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = purchaseType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 15),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = purchase.LedgerId, Debit = 100000m, Credit = 0m },
                new() { LedgerId = supplier.LedgerId, Debit = 0m, Credit = 100000m }
            }
        });

        // 2. Sales on credit: ₹1,50,000 (Customer Dr, Sales Cr)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 20),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 150000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 150000m }
            }
        });

        // 3. Customer partial payment received: ₹80,000 (Bank Dr, Customer Cr)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = receiptType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 25),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 80000m, Credit = 0m },
                new() { LedgerId = customer.LedgerId, Debit = 0m, Credit = 80000m }
            }
        });

        // 4. Supplier partial payment paid: ₹60,000 (Supplier Dr, Bank Cr)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 28),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = supplier.LedgerId, Debit = 60000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 60000m }
            }
        });

        // Query Trial Balance for full FY
        var tb = await acctService.GetTrialBalanceAsync(
            company.CompanyId,
            new DateTime(2026, 4, 1),
            new DateTime(2027, 3, 31));

        tb.Should().NotBeNull();
        tb.IsBalanced.Should().BeTrue();
        tb.Difference.Should().Be(0m);

        // Opening totals must balance
        tb.TotalOpeningDebit.Should().Be(500000m);
        tb.TotalOpeningCredit.Should().Be(500000m);

        // Period totals must balance (100k + 150k + 80k + 60k = 390k)
        tb.TotalPeriodDebit.Should().Be(390000m);
        tb.TotalPeriodCredit.Should().Be(390000m);

        // Closing totals must balance
        tb.TotalClosingDebit.Should().Be(tb.TotalClosingCredit);
    }

    [Fact]
    public async Task GetTrialBalance_Opening_Period_And_Closing_Formula_Verification()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Formula TB Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Shop Cash",
            OpeningBalance = 30000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var printing = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Printing & Stationery"
        });

        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 10),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = printing.LedgerId, Debit = 2500m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 2500m }
            }
        });

        var tb = await acctService.GetTrialBalanceAsync(
            company.CompanyId,
            new DateTime(2026, 4, 1),
            new DateTime(2026, 5, 31));

        foreach (var item in tb.Items)
        {
            decimal netOp = item.OpeningDebit - item.OpeningCredit;
            decimal netTxn = item.PeriodDebit - item.PeriodCredit;
            decimal netCl = item.ClosingDebit - item.ClosingCredit;

            netCl.Should().Be(netOp + netTxn, $"Formula netCl == netOp + netTxn must hold for {item.LedgerName}");
        }
    }

    [Fact]
    public async Task GetTrialBalance_SoftDeleted_Vouchers_Excluded()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Delete TB Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Office Cash",
            OpeningBalance = 10000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var repair = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Equipment Repairs"
        });

        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 1),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = repair.LedgerId, Debit = 9000m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 9000m }
            }
        });

        // Delete voucher
        await acctService.DeleteVoucherAsync(voucher.VoucherId);

        var tb = await acctService.GetTrialBalanceAsync(
            company.CompanyId,
            new DateTime(2026, 4, 1),
            new DateTime(2026, 6, 30));

        // Deleted repair expense must not appear in period transactions
        var cashItem = tb.Items.First(i => i.LedgerId == cash.LedgerId);
        cashItem.PeriodCredit.Should().Be(0m);
        cashItem.ClosingDebit.Should().Be(10000m);
    }
}
