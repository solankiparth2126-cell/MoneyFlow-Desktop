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

public class Phase21OutstandingTests
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
    public async Task GetOutstandingReport_Should_Calculate_Receivables_And_Aging_Buckets()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Aging Debtors Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var debtorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);

        var customer = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = debtorGroup.GroupId,
            LedgerName = "Customer Alpha",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var sales = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "General Sales",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Credit
        });

        var asOfDate = new DateTime(2026, 8, 1);

        // Sales 1: 12 days old -> ₹20,000 (0-30 bucket)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 7, 20),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 20000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 20000m }
            }
        });

        // Sales 2: 42 days old -> ₹30,000 (31-60 bucket)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 20),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 30000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 30000m }
            }
        });

        // Sales 3: 78 days old -> ₹40,000 (61-90 bucket)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 15),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 40000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 40000m }
            }
        });

        // Sales 4: 113 days old -> ₹50,000 (>90 bucket)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 10),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 50000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 50000m }
            }
        });

        // Act: Generate Receivables report as of 2026-08-01
        var report = await acctService.GetOutstandingReportAsync(company.CompanyId, asOfDate, isReceivables: true);

        // Assert
        report.Parties.Should().HaveCount(1);
        var party = report.Parties[0];
        party.PartyName.Should().Be("Customer Alpha");
        party.TotalOutstanding.Should().Be(140000m);
        party.Aging.Days0To30.Should().Be(20000m);
        party.Aging.Days31To60.Should().Be(30000m);
        party.Aging.Days61To90.Should().Be(40000m);
        party.Aging.DaysOver90.Should().Be(50000m);
        party.Aging.Total.Should().Be(140000m);
    }

    [Fact]
    public async Task GetOutstandingReport_Should_Calculate_Payables_With_Partial_Payments()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Aging Creditors Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var creditorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Creditors");
        var purchaseGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Purchase Accounts");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");

        var purchaseType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Purchase);
        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        var supplier = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = creditorGroup.GroupId,
            LedgerName = "Vendor Beta",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Credit
        });

        var purchase = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = purchaseGroup.GroupId,
            LedgerName = "Inventory Purchases",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "Operating Bank",
            OpeningBalance = 500000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var asOfDate = new DateTime(2026, 6, 30);

        // Purchase 1: 76 days old (2026-04-15) -> ₹1,00,000
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

        // Purchase 2: 15 days old (2026-06-15) -> ₹50,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = purchaseType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 15),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = purchase.LedgerId, Debit = 50000m, Credit = 0m },
                new() { LedgerId = supplier.LedgerId, Debit = 0m, Credit = 50000m }
            }
        });

        // Payment: 2026-06-20 -> ₹60,000 paid to supplier
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 20),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = supplier.LedgerId, Debit = 60000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 60000m }
            }
        });

        // Act: Generate Payables report as of 2026-06-30
        var report = await acctService.GetOutstandingReportAsync(company.CompanyId, asOfDate, isReceivables: false);

        // Assert:
        // Total Outstanding = 1,00,000 + 50,000 - 60,000 = 90,000.
        // FIFO newest first:
        // Purchase 2 (15 days old) = 50,000 into 0-30 days
        // Remaining 40,000 comes from Purchase 1 (76 days old) = 40,000 into 61-90 days
        report.Parties.Should().HaveCount(1);
        var party = report.Parties[0];
        party.TotalOutstanding.Should().Be(90000m);
        party.Aging.Days0To30.Should().Be(50000m);
        party.Aging.Days31To60.Should().Be(0m);
        party.Aging.Days61To90.Should().Be(40000m);
        party.Aging.DaysOver90.Should().Be(0m);
        party.Aging.Total.Should().Be(90000m);
    }

    [Fact]
    public async Task GetOutstandingReport_Should_Exclude_Settled_Zero_Balance_Parties()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Zero Balance Exclude Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var debtorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);
        var receiptType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Receipt);

        var customerGamma = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = debtorGroup.GroupId,
            LedgerName = "Customer Gamma (Paid)",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var customerDelta = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = debtorGroup.GroupId,
            LedgerName = "Customer Delta (Unpaid)",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var sales = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Sales",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Credit
        });

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "Bank",
            OpeningBalance = 100000m,
            OpeningBalanceType = BalanceType.Debit
        });

        // Customer Gamma: Sale 25,000, Receipt 25,000 (Balance = 0)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 1),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customerGamma.LedgerId, Debit = 25000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 25000m }
            }
        });

        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = receiptType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 10),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 25000m, Credit = 0m },
                new() { LedgerId = customerGamma.LedgerId, Debit = 0m, Credit = 25000m }
            }
        });

        // Customer Delta: Sale 15,000, Unpaid (Balance = 15,000)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 15),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customerDelta.LedgerId, Debit = 15000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 15000m }
            }
        });

        // Act
        var report = await acctService.GetOutstandingReportAsync(company.CompanyId, new DateTime(2026, 5, 31), isReceivables: true);

        // Assert: Only Customer Delta should be present
        report.Parties.Should().HaveCount(1);
        report.Parties[0].PartyName.Should().Be("Customer Delta (Unpaid)");
        report.Parties[0].TotalOutstanding.Should().Be(15000m);
    }

    [Fact]
    public async Task GetOutstandingReport_Should_Exclude_Soft_Deleted_Vouchers_And_Respect_AsOfDate()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Date Filter Outstanding Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var debtorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);

        var customer = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = debtorGroup.GroupId,
            LedgerName = "Customer Epsilon",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var sales = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Sales",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Credit
        });

        // 1. Valid sale in April: ₹30,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 10),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 30000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 30000m }
            }
        });

        // 2. Soft-deleted sale in April: ₹20,000
        var delVoucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 15),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 20000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 20000m }
            }
        });
        await acctService.DeleteVoucherAsync(delVoucher.VoucherId);

        // 3. Future sale in May: ₹40,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 10),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 40000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 40000m }
            }
        });

        // Act: Outstanding as of April 30, 2026
        var report = await acctService.GetOutstandingReportAsync(company.CompanyId, new DateTime(2026, 4, 30), isReceivables: true);

        // Assert: Only the April 10 active sale (₹30,000) should be included
        report.Parties.Should().HaveCount(1);
        report.Parties[0].TotalOutstanding.Should().Be(30000m);
    }
}
