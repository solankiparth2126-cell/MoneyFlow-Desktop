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

public class Phase15CreditNoteTests
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
    public async Task SaveCreditNote_CustomerSalesReturn_Should_Decrease_Debtor_Receivable()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Credit Note Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var debtorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");

        // Customer owes ₹50,000 Dr
        var customer = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = debtorGroup.GroupId,
            LedgerName = "Apex Retailers",
            OpeningBalance = 50000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var salesReturn = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Sales Returns"
        });

        var creditNoteType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.CreditNote);
        creditNoteType.Should().NotBeNull();

        // Customer returns goods worth ₹15,000
        // Double entry: Dr Sales Returns ₹15,000, Cr Customer ₹15,000
        var voucherDto = new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = creditNoteType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 10),
            ReferenceNumber = "SLS-2026-001",
            Narration = "Sales return of damaged electronics against invoice SLS-2026-001",
            Entries = new List<VoucherEntryDto>
            {
                new()
                {
                    LedgerId = salesReturn.LedgerId,
                    Debit = 15000m,
                    Credit = 0m,
                    Narration = "Defective monitors returned"
                },
                new()
                {
                    LedgerId = customer.LedgerId,
                    Debit = 0m,
                    Credit = 15000m,
                    Narration = "Credit note issued to Apex Retailers"
                }
            }
        };

        var savedVoucher = await acctService.SaveVoucherAsync(company.CompanyId, voucherDto);
        savedVoucher.Should().NotBeNull();
        savedVoucher.VoucherNumber.Should().StartWith("CRN-");

        // Verify balances dynamically:
        // Customer was ₹50,000 Dr, credited ₹15,000 -> now ₹35,000 Dr
        var customerBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, customer.LedgerId);
        customerBal.ClosingBalance.Should().Be(35000m);
        customerBal.ClosingType.Should().Be(BalanceType.Debit);

        // Sales Returns was ₹0, debited ₹15,000 -> now ₹15,000 Dr
        var returnBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, salesReturn.LedgerId);
        returnBal.ClosingBalance.Should().Be(15000m);
        returnBal.ClosingType.Should().Be(BalanceType.Debit);
    }

    [Fact]
    public async Task SaveCreditNote_CashRefund_Should_Decrease_Cash_And_Debit_SalesReturns()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Cash Return Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");

        var cashLedger = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Main Cash",
            OpeningBalance = 20000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var salesReturn = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Sales Returns"
        });

        var creditNoteType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.CreditNote);

        // Immediate cash refund of ₹3,000 for retail return
        // Dr Sales Returns ₹3,000, Cr Main Cash ₹3,000
        var voucherDto = new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = creditNoteType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 1),
            ReferenceNumber = "CSH-RET-12",
            Narration = "Counter return cash refund",
            Entries = new List<VoucherEntryDto>
            {
                new()
                {
                    LedgerId = salesReturn.LedgerId,
                    Debit = 3000m,
                    Credit = 0m,
                    Narration = "Returned goods"
                },
                new()
                {
                    LedgerId = cashLedger.LedgerId,
                    Debit = 0m,
                    Credit = 3000m,
                    Narration = "Cash refunded over counter"
                }
            }
        };

        var saved = await acctService.SaveVoucherAsync(company.CompanyId, voucherDto);
        saved.Should().NotBeNull();

        // Cash was ₹20,000 Dr, credited ₹3,000 -> now ₹17,000 Dr
        var cashBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, cashLedger.LedgerId);
        cashBal.ClosingBalance.Should().Be(17000m);
        cashBal.ClosingType.Should().Be(BalanceType.Debit);
    }

    [Fact]
    public async Task DeleteCreditNote_Should_Restore_Debtor_Balance()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Delete Credit Note Co",
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
            OpeningBalance = 40000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var salesReturn = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Sales Returns"
        });

        var creditNoteType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.CreditNote);

        var voucherDto = new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = creditNoteType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 7, 1),
            Narration = "Credit note to be deleted",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = salesReturn.LedgerId, Debit = 8000m, Credit = 0m },
                new() { LedgerId = customer.LedgerId, Debit = 0m, Credit = 8000m }
            }
        };

        var saved = await acctService.SaveVoucherAsync(company.CompanyId, voucherDto);

        // Balance decreased to ₹32,000 Dr
        var midBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, customer.LedgerId);
        midBal.ClosingBalance.Should().Be(32000m);

        // Delete voucher
        var deleted = await acctService.DeleteVoucherAsync(saved.VoucherId);
        deleted.Should().BeTrue();

        // Customer balance dynamically restored to ₹40,000 Dr
        var restoredBal = await acctService.GetLedgerBalanceAsync(company.CompanyId, customer.LedgerId);
        restoredBal.ClosingBalance.Should().Be(40000m);
        restoredBal.ClosingType.Should().Be(BalanceType.Debit);
    }

    [Fact]
    public async Task Sales_And_CreditNote_RoundTrip_Should_Zero_Out_Debtor_Balance()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "RoundTrip Credit Note Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var debtorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");

        // Customer starts with ₹0 balance
        var customer = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = debtorGroup.GroupId,
            LedgerName = "Zero Sum Customer",
            OpeningBalance = 0m
        });

        var salesAccount = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "General Sales"
        });

        var salesReturn = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Sales Returns"
        });

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);
        var creditNoteType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.CreditNote);

        // 1. Sales Invoice: ₹25,000 (Dr Customer ₹25,000, Cr Sales ₹25,000)
        var salesInvoice = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 8, 1),
            ReferenceNumber = "INV-001",
            Narration = "Sale of goods",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 25000m, Credit = 0m },
                new() { LedgerId = salesAccount.LedgerId, Debit = 0m, Credit = 25000m }
            }
        });

        var balAfterSale = await acctService.GetLedgerBalanceAsync(company.CompanyId, customer.LedgerId);
        balAfterSale.ClosingBalance.Should().Be(25000m);
        balAfterSale.ClosingType.Should().Be(BalanceType.Debit);

        // 2. Full Return via Credit Note: ₹25,000 (Dr Sales Returns ₹25,000, Cr Customer ₹25,000)
        var creditNote = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = creditNoteType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 8, 5),
            ReferenceNumber = "INV-001",
            Narration = "Complete return of order INV-001",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = salesReturn.LedgerId, Debit = 25000m, Credit = 0m },
                new() { LedgerId = customer.LedgerId, Debit = 0m, Credit = 25000m }
            }
        });

        // 3. Customer balance must now be perfectly ZERO
        var balAfterReturn = await acctService.GetLedgerBalanceAsync(company.CompanyId, customer.LedgerId);
        balAfterReturn.ClosingBalance.Should().Be(0m);
    }
}
