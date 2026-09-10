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

public class Phase13PurchaseTests
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
    public async Task SavePurchaseVoucher_CreditPurchase_From_SundryCreditor_Should_Increase_Creditor_And_Purchase()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Purchase Test Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var creditorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Creditors");
        var purchaseGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Purchase Accounts");

        var supplier = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = creditorGroup.GroupId,
            LedgerName = "XYZ Supplies"
        });

        var purchaseAccount = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = purchaseGroup.GroupId,
            LedgerName = "Raw Material Purchases"
        });

        var purchaseType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Purchase);

        // Raw Material Alpha: Qty 20 @ ₹50, Disc ₹20 => Net = ₹980
        // Purchase A/c Dr ₹980 To XYZ Supplies ₹980
        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = purchaseType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 12),
            ReferenceNumber = "SUP-9921",
            Narration = "Raw Material Alpha (Qty: 20 @ ₹50, Disc: ₹20)",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = purchaseAccount.LedgerId, Debit = 980m, Credit = 0m, Narration = "Raw Material Alpha purchase" },
                new() { LedgerId = supplier.LedgerId, Debit = 0m, Credit = 980m, Narration = "Purchase from XYZ Supplies" }
            }
        });

        voucher.Should().NotBeNull();
        voucher.VoucherNumber.Should().Be("PUR-00001");
        voucher.VoucherEntries.Should().HaveCount(2);

        // Supplier Creditor balance: ₹980 Cr
        var suppBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, supplier.LedgerId);
        suppBal.ClosingBalance.Should().Be(980m);
        suppBal.ClosingType.Should().Be(BalanceType.Credit);

        // Purchase expense balance: ₹980 Dr
        var purBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, purchaseAccount.LedgerId);
        purBal.ClosingBalance.Should().Be(980m);
        purBal.ClosingType.Should().Be(BalanceType.Debit);
    }

    [Fact]
    public async Task SavePurchaseVoucher_CashPurchase_Should_Decrease_Cash_And_Increase_Purchase()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Cash Purchase Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var purchaseGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Purchase Accounts");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Main Cash",
            OpeningBalance = 10000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var purchaseAccount = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = purchaseGroup.GroupId,
            LedgerName = "Office Supplies Purchase"
        });

        var purchaseType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Purchase);

        // Cash purchase of ₹1,500
        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = purchaseType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 5),
            Narration = "Cash purchase of office supplies",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = purchaseAccount.LedgerId, Debit = 1500m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 1500m }
            }
        });

        voucher.Should().NotBeNull();

        // Cash balance: 10,000 - 1,500 = 8,500 Dr
        var cashBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, cash.LedgerId);
        cashBal.ClosingBalance.Should().Be(8500m);

        // Purchase balance: 1,500 Dr
        var purBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, purchaseAccount.LedgerId);
        purBal.ClosingBalance.Should().Be(1500m);
    }

    [Fact]
    public async Task GetSupplierPartyLedgersAsync_Should_Return_Creditors_Cash_And_Bank()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Supplier Lookup Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var creditorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Creditors");
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var incomeGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Direct Income");

        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { GroupId = creditorGroup.GroupId, LedgerName = "Vendor Beta" });
        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { GroupId = cashGroup.GroupId, LedgerName = "Cash" });
        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { GroupId = incomeGroup.GroupId, LedgerName = "Service Revenue" });

        var supplierLedgers = await acctService.GetSupplierPartyLedgersAsync(company.CompanyId);

        supplierLedgers.Should().Contain(l => l.LedgerName == "Vendor Beta");
        supplierLedgers.Should().Contain(l => l.LedgerName == "Cash");
        supplierLedgers.Should().NotContain(l => l.LedgerName == "Service Revenue");
    }

    [Fact]
    public async Task GetPurchaseLedgersAsync_Should_Return_Purchase_And_Direct_Expenses()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Purchase Lookup Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var purchaseGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Purchase Accounts");
        var directExpGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Direct Expenses");
        var fixedAssetGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Fixed Assets");

        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { GroupId = purchaseGroup.GroupId, LedgerName = "Goods Purchase" });
        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { GroupId = directExpGroup.GroupId, LedgerName = "Freight Inward" });
        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { GroupId = fixedAssetGroup.GroupId, LedgerName = "Office Computers" });

        var purchaseLedgers = await acctService.GetPurchaseLedgersAsync(company.CompanyId);

        purchaseLedgers.Should().Contain(l => l.LedgerName == "Goods Purchase");
        purchaseLedgers.Should().Contain(l => l.LedgerName == "Freight Inward");
        purchaseLedgers.Should().NotContain(l => l.LedgerName == "Office Computers");
    }

    [Fact]
    public async Task DeletePurchaseVoucher_Should_Restore_Creditor_And_Purchase_Balances()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Restore Purchase Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var creditorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Creditors");
        var purchaseGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Purchase Accounts");

        var supplier = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = creditorGroup.GroupId,
            LedgerName = "Mega Supplies",
            OpeningBalance = 25000m,
            OpeningBalanceType = BalanceType.Credit
        });

        var purchaseAccount = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = purchaseGroup.GroupId,
            LedgerName = "Imported Materials"
        });

        var purchaseType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Purchase);

        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = purchaseType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 7, 1),
            ReferenceNumber = "BILL-882",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = purchaseAccount.LedgerId, Debit = 6000m, Credit = 0m },
                new() { LedgerId = supplier.LedgerId, Debit = 0m, Credit = 6000m }
            }
        });

        (await acctService.GetLedgerBalanceAsync(company.CompanyId, supplier.LedgerId)).ClosingBalance.Should().Be(31000m);
        (await acctService.GetLedgerBalanceAsync(company.CompanyId, purchaseAccount.LedgerId)).ClosingBalance.Should().Be(6000m);

        var deleted = await acctService.DeleteVoucherAsync(voucher.VoucherId);
        deleted.Should().BeTrue();

        (await acctService.GetLedgerBalanceAsync(company.CompanyId, supplier.LedgerId)).ClosingBalance.Should().Be(25000m);
        (await acctService.GetLedgerBalanceAsync(company.CompanyId, purchaseAccount.LedgerId)).ClosingBalance.Should().Be(0m);
    }
}
