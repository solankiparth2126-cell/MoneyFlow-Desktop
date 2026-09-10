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

public class Phase14DebitNoteTests
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
    public async Task SaveDebitNote_PurchaseReturn_To_SundryCreditor_Should_Reduce_Creditor_And_Purchase()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Debit Note Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var creditorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Creditors");
        var purchaseGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Purchase Accounts");

        // Supplier with initial payable liability of ₹20,000 Cr
        var supplier = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = creditorGroup.GroupId,
            LedgerName = "Prime Vendor",
            OpeningBalance = 20000m,
            OpeningBalanceType = BalanceType.Credit
        });

        var purchaseReturn = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = purchaseGroup.GroupId,
            LedgerName = "Purchase Returns"
        });

        var debitNoteType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.DebitNote);

        // Debit Note: Prime Vendor Dr ₹3,500 To Purchase Returns ₹3,500
        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = debitNoteType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 20),
            ReferenceNumber = "PUR-INV-100",
            Narration = "Goods returned due to defect (Qty: 7 @ ₹500)",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = supplier.LedgerId, Debit = 3500m, Credit = 0m, Narration = "Debit Note to Prime Vendor" },
                new() { LedgerId = purchaseReturn.LedgerId, Debit = 0m, Credit = 3500m, Narration = "Goods returned due to defect" }
            }
        });

        voucher.Should().NotBeNull();
        voucher.VoucherNumber.Should().Be("DBN-00001");
        voucher.VoucherEntries.Should().HaveCount(2);

        // Supplier balance: 20,000 Cr - 3,500 Dr = 16,500 Cr
        var suppBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, supplier.LedgerId);
        suppBal.ClosingBalance.Should().Be(16500m);
        suppBal.ClosingType.Should().Be(BalanceType.Credit);

        // Purchase Returns balance: 3,500 Cr
        var retBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, purchaseReturn.LedgerId);
        retBal.ClosingBalance.Should().Be(3500m);
        retBal.ClosingType.Should().Be(BalanceType.Credit);
    }

    [Fact]
    public async Task SaveDebitNote_CashRefund_Should_Increase_Cash_And_Reduce_Purchase()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Cash Refund Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var purchaseGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Purchase Accounts");

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Store Cash",
            OpeningBalance = 5000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var purchaseAccount = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = purchaseGroup.GroupId,
            LedgerName = "Stationery Purchases"
        });

        var debitNoteType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.DebitNote);

        // Cash refund of ₹800 on returned stationery: Cash Dr ₹800 To Stationery Purchases ₹800
        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = debitNoteType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 10),
            Narration = "Cash refund received for damaged files",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = cash.LedgerId, Debit = 800m, Credit = 0m },
                new() { LedgerId = purchaseAccount.LedgerId, Debit = 0m, Credit = 800m }
            }
        });

        voucher.Should().NotBeNull();

        // Cash balance: 5,000 + 800 = 5,800 Dr
        var cashBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, cash.LedgerId);
        cashBal.ClosingBalance.Should().Be(5800m);
    }

    [Fact]
    public async Task SaveDebitNote_Sequential_Numbering_Should_Increment()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Seq DebitNote Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var creditorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Creditors");
        var purchaseGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Purchase Accounts");

        var vendor = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = creditorGroup.GroupId,
            LedgerName = "Vendor Alpha"
        });

        var purchase = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = purchaseGroup.GroupId,
            LedgerName = "General Purchases"
        });

        var debitNoteType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.DebitNote);

        var v1 = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = debitNoteType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 1),
            Narration = "Debit Note 1",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = vendor.LedgerId, Debit = 1000m, Credit = 0m },
                new() { LedgerId = purchase.LedgerId, Debit = 0m, Credit = 1000m }
            }
        });

        var v2 = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = debitNoteType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 2),
            Narration = "Debit Note 2",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = vendor.LedgerId, Debit = 1500m, Credit = 0m },
                new() { LedgerId = purchase.LedgerId, Debit = 0m, Credit = 1500m }
            }
        });

        v1.VoucherNumber.Should().Be("DBN-00001");
        v2.VoucherNumber.Should().Be("DBN-00002");
    }

    [Fact]
    public async Task DeleteDebitNote_Should_Restore_Creditor_And_Purchase_Balances()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Restore DebitNote Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var creditorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Creditors");
        var purchaseGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Purchase Accounts");

        var supplier = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = creditorGroup.GroupId,
            LedgerName = "Hardware Solutions",
            OpeningBalance = 30000m,
            OpeningBalanceType = BalanceType.Credit
        });

        var purchase = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = purchaseGroup.GroupId,
            LedgerName = "Hardware Purchases"
        });

        var debitNoteType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.DebitNote);

        var voucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = debitNoteType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 7, 10),
            ReferenceNumber = "BILL-4001",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = supplier.LedgerId, Debit = 5000m, Credit = 0m },
                new() { LedgerId = purchase.LedgerId, Debit = 0m, Credit = 5000m }
            }
        });

        (await acctService.GetLedgerBalanceAsync(company.CompanyId, supplier.LedgerId)).ClosingBalance.Should().Be(25000m);
        (await acctService.GetLedgerBalanceAsync(company.CompanyId, purchase.LedgerId)).ClosingBalance.Should().Be(5000m);

        var deleted = await acctService.DeleteVoucherAsync(voucher.VoucherId);
        deleted.Should().BeTrue();

        (await acctService.GetLedgerBalanceAsync(company.CompanyId, supplier.LedgerId)).ClosingBalance.Should().Be(30000m);
        (await acctService.GetLedgerBalanceAsync(company.CompanyId, purchase.LedgerId)).ClosingBalance.Should().Be(0m);
    }
}
