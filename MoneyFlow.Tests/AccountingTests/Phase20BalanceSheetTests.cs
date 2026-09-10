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

public class Phase20BalanceSheetTests
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
    public async Task GetBalanceSheet_Should_Reconcile_And_Balance_DoubleEntry()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Balanced BS Corp",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var capGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Capital Account");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var fixAssetGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Fixed Assets");
        var debtorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var creditorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Creditors");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");
        var purchaseGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Purchase Accounts");

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);
        var purchaseType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Purchase);
        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        // 1. Setup Opening Master Balances: Capital ₹10,00,000 Cr, Bank ₹10,00,000 Dr
        var capital = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = capGroup.GroupId,
            LedgerName = "Owner Share Capital",
            OpeningBalance = 1000000m,
            OpeningBalanceType = BalanceType.Credit
        });

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "Standard Chartered Bank",
            OpeningBalance = 1000000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var machinery = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = fixAssetGroup.GroupId,
            LedgerName = "Plant & Machinery",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var customer = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = debtorGroup.GroupId,
            LedgerName = "Apex Industries",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var supplier = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = creditorGroup.GroupId,
            LedgerName = "Global Steel Suppliers",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Credit
        });

        var sales = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Standard Sales",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Credit
        });

        var purchase = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = purchaseGroup.GroupId,
            LedgerName = "Raw Material Purchase",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        // 2. Buy Machinery: Machinery Dr 3,00,000 / Bank Cr 3,00,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 10),
            ReferenceNumber = "PMT-MCH",
            Narration = "Machinery purchase",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = machinery.LedgerId, Debit = 300000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 300000m }
            }
        });

        // 3. Purchase Goods on Credit: Purchase Dr 2,00,000 / Supplier Cr 2,00,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = purchaseType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 15),
            ReferenceNumber = "PUR-01",
            Narration = "Credit purchase",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = purchase.LedgerId, Debit = 200000m, Credit = 0m },
                new() { LedgerId = supplier.LedgerId, Debit = 0m, Credit = 200000m }
            }
        });

        // 4. Sales Goods on Credit: Customer Dr 3,50,000 / Sales Cr 3,50,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 20),
            ReferenceNumber = "SLS-01",
            Narration = "Credit sales",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 350000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 350000m }
            }
        });

        // Act: Generate Balance Sheet as of April 30, 2026
        var bs = await acctService.GetBalanceSheetAsync(company.CompanyId, new DateTime(2026, 4, 30));

        // Assert:
        // Net Profit = Sales (3,50,000) - Purchase (2,00,000) = 1,50,000
        bs.NetProfit.Should().Be(150000m);
        bs.HasNetProfit.Should().BeTrue();
        bs.NetLoss.Should().Be(0m);

        // Liabilities = Capital (10,00,000) + Supplier (2,00,000) + Net Profit (1,50,000) = 13,50,000
        // Assets = Bank (7,00,000) + Machinery (3,00,000) + Customer (3,50,000) = 13,50,000
        bs.TotalLiabilitiesSide.Should().Be(1350000m);
        bs.TotalAssetsSide.Should().Be(1350000m);
        bs.Difference.Should().Be(0m);
        bs.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task GetBalanceSheet_Should_Reflect_Net_Profit_From_Operating_Revenues_And_Expenses()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Operating BS Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var capGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Capital Account");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");
        var indExpGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");
        var indIncGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Income");

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);
        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);
        var receiptType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Receipt);

        var capital = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = capGroup.GroupId,
            LedgerName = "Equity Capital",
            OpeningBalance = 500000m,
            OpeningBalanceType = BalanceType.Credit
        });

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "ICICI Bank",
            OpeningBalance = 500000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var sales = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Consulting Revenue",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Credit
        });

        var rent = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = indExpGroup.GroupId,
            LedgerName = "Premises Rent",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var interest = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = indIncGroup.GroupId,
            LedgerName = "FD Interest",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Credit
        });

        // 1. Sales: Bank Dr 2,00,000 / Sales Cr 2,00,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 5),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 200000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 200000m }
            }
        });

        // 2. Rent: Rent Dr 40,000 / Bank Cr 40,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 10),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = rent.LedgerId, Debit = 40000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 40000m }
            }
        });

        // 3. Interest: Bank Dr 5,000 / Interest Cr 5,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = receiptType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 15),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 5000m, Credit = 0m },
                new() { LedgerId = interest.LedgerId, Debit = 0m, Credit = 5000m }
            }
        });

        // Act
        var bs = await acctService.GetBalanceSheetAsync(company.CompanyId, new DateTime(2026, 5, 31));

        // Assert:
        // Net Profit = Revenue (2,00,000) + Interest (5,000) - Rent (40,000) = 1,65,000
        // Liabilities = Capital (5,00,000) + Net Profit (1,65,000) = 6,65,000
        // Bank = 5,00,000 + 2,00,000 - 40,000 + 5,000 = 6,65,000
        bs.NetProfit.Should().Be(165000m);
        bs.TotalLiabilitiesSide.Should().Be(665000m);
        bs.TotalAssetsSide.Should().Be(665000m);
        bs.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task GetBalanceSheet_Should_Handle_Net_Loss_And_Balance_AssetsSide()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Loss Scenario BS Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var capGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Capital Account");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");
        var purchaseGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Purchase Accounts");
        var indExpGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);
        var purchaseType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Purchase);
        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        var capital = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = capGroup.GroupId,
            LedgerName = "Partner Capital",
            OpeningBalance = 800000m,
            OpeningBalanceType = BalanceType.Credit
        });

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "Yes Bank",
            OpeningBalance = 800000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var sales = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Product Sales",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Credit
        });

        var purchase = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = purchaseGroup.GroupId,
            LedgerName = "Cost of Goods",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var rent = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = indExpGroup.GroupId,
            LedgerName = "Factory Rent",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        // 1. Purchase: Purchase Dr 5,00,000 / Bank Cr 5,00,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = purchaseType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 5),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = purchase.LedgerId, Debit = 500000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 500000m }
            }
        });

        // 2. Sales: Bank Dr 2,00,000 / Sales Cr 2,00,000 -> Gross Loss = 3,00,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 10),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 200000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 200000m }
            }
        });

        // 3. Rent: Rent Dr 50,000 / Bank Cr 50,000 -> Net Loss = 3,50,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 15),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = rent.LedgerId, Debit = 50000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 50000m }
            }
        });

        // Act
        var bs = await acctService.GetBalanceSheetAsync(company.CompanyId, new DateTime(2026, 6, 30));

        // Assert:
        // Net Loss = 3,50,000
        // Liabilities Side: Capital (8,00,000)
        // Assets Side: Bank (8,00,000 - 5,00,000 + 2,00,000 - 50,000 = 4,50,000) + Net Loss (3,50,000) = 8,00,000
        bs.HasNetProfit.Should().BeFalse();
        bs.NetLoss.Should().Be(350000m);
        bs.TotalLiabilitiesSide.Should().Be(800000m);
        bs.TotalAssetsSide.Should().Be(800000m);
        bs.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public async Task GetBalanceSheet_Should_Exclude_Soft_Deleted_Vouchers_And_Respect_AsOfDate()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Filter Check BS Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var capGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Capital Account");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var debtorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);

        var capital = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = capGroup.GroupId,
            LedgerName = "Promoter Capital",
            OpeningBalance = 300000m,
            OpeningBalanceType = BalanceType.Credit
        });

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "Canara Bank",
            OpeningBalance = 300000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var customer = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = debtorGroup.GroupId,
            LedgerName = "Retail Customer",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var sales = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Product Sales",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Credit
        });

        // 1. April 10 sale (valid): ₹1,00,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 10),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 100000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 100000m }
            }
        });

        // 2. April 15 sale (soft-deleted): ₹50,000
        var delVoucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 15),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 50000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 50000m }
            }
        });
        await acctService.DeleteVoucherAsync(delVoucher.VoucherId);

        // 3. May 10 sale (future/outside April): ₹80,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 10),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = customer.LedgerId, Debit = 80000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 80000m }
            }
        });

        // Act: Balance Sheet as of April 30, 2026
        var bs = await acctService.GetBalanceSheetAsync(company.CompanyId, new DateTime(2026, 4, 30));

        // Assert:
        // Customer balance should be exactly ₹1,00,000 (excluding deleted ₹50,000 and future ₹80,000)
        // Net Profit = ₹1,00,000
        // Liabilities = Capital (3,00,000) + Net Profit (1,00,000) = 4,00,000
        // Assets = Bank (3,00,000) + Customer (1,00,000) = 4,00,000
        bs.NetProfit.Should().Be(100000m);
        bs.TotalLiabilitiesSide.Should().Be(400000m);
        bs.TotalAssetsSide.Should().Be(400000m);
        bs.IsBalanced.Should().BeTrue();
    }
}
