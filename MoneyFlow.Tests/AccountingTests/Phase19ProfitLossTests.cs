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

public class Phase19ProfitLossTests
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
    public async Task GetProfitAndLoss_Should_Calculate_Trading_Gross_Profit_Correctly()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Trading Profit Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");
        var purchaseGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Purchase Accounts");
        var directExpGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Direct Expenses");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);
        var purchaseType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Purchase);
        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "HDFC Bank",
            OpeningBalance = 1000000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var salesLedger = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Domestic Sales",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Credit
        });

        var purchaseLedger = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = purchaseGroup.GroupId,
            LedgerName = "Raw Material Purchase",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var freightLedger = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = directExpGroup.GroupId,
            LedgerName = "Freight Inward",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        // 1. Sales Voucher: HDFC Bank Dr 5,00,000 / Sales Cr 5,00,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 15),
            ReferenceNumber = "SLS-001",
            Narration = "Sales to Cash Customer",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 500000m, Credit = 0m },
                new() { LedgerId = salesLedger.LedgerId, Debit = 0m, Credit = 500000m }
            }
        });

        // 2. Purchase Voucher: Purchase Dr 3,00,000 / HDFC Bank Cr 3,00,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = purchaseType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 18),
            ReferenceNumber = "PUR-001",
            Narration = "Raw materials purchase",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = purchaseLedger.LedgerId, Debit = 300000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 300000m }
            }
        });

        // 3. Payment Voucher for Freight: Freight Inward Dr 20,000 / HDFC Bank Cr 20,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 20),
            ReferenceNumber = "PMT-001",
            Narration = "Freight paid",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = freightLedger.LedgerId, Debit = 20000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 20000m }
            }
        });

        // Act: Generate Profit & Loss for April 2026
        var pl = await acctService.GetProfitAndLossAsync(company.CompanyId, new DateTime(2026, 4, 1), new DateTime(2026, 4, 30));

        // Assert:
        // Total Trading Revenue = 5,00,000 (Sales)
        // Total Trading Expense = 3,00,000 (Purchases) + 20,000 (Freight) = 3,20,000
        // Gross Profit = 5,00,000 - 3,20,000 = 1,80,000
        pl.TotalTradingRevenue.Should().Be(500000m);
        pl.TotalTradingExpense.Should().Be(320000m);
        pl.GrossProfit.Should().Be(180000m);
        pl.HasGrossProfit.Should().BeTrue();
        pl.GrossLoss.Should().Be(0m);

        // No indirect income or expenses yet -> Net Profit equals Gross Profit
        pl.NetProfit.Should().Be(180000m);
        pl.HasNetProfit.Should().BeTrue();
        pl.NetLoss.Should().Be(0m);
    }

    [Fact]
    public async Task GetProfitAndLoss_Should_Calculate_Net_Profit_With_Indirect_Expenses_And_Incomes()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Full PnL Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");
        var purchaseGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Purchase Accounts");
        var indExpGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");
        var indIncGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Income");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);
        var purchaseType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Purchase);
        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);
        var receiptType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Receipt);

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "SBI Bank",
            OpeningBalance = 1000000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var salesLedger = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Export Sales",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Credit
        });

        var purchaseLedger = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = purchaseGroup.GroupId,
            LedgerName = "Purchase Goods",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var rentLedger = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = indExpGroup.GroupId,
            LedgerName = "Office Rent",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var salaryLedger = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = indExpGroup.GroupId,
            LedgerName = "Staff Salary",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var interestLedger = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = indIncGroup.GroupId,
            LedgerName = "Interest Received",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Credit
        });

        // 1. Sales: ₹5,00,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 2),
            ReferenceNumber = "SLS-002",
            Narration = "Sales",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 500000m, Credit = 0m },
                new() { LedgerId = salesLedger.LedgerId, Debit = 0m, Credit = 500000m }
            }
        });

        // 2. Purchase: ₹2,00,000 -> Gross Profit = 3,00,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = purchaseType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 5),
            ReferenceNumber = "PUR-002",
            Narration = "Purchase",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = purchaseLedger.LedgerId, Debit = 200000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 200000m }
            }
        });

        // 3. Rent Payment: ₹30,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 10),
            ReferenceNumber = "PMT-002",
            Narration = "Rent payment",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = rentLedger.LedgerId, Debit = 30000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 30000m }
            }
        });

        // 4. Salary Payment: ₹50,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 15),
            ReferenceNumber = "PMT-003",
            Narration = "Salary payment",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = salaryLedger.LedgerId, Debit = 50000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 50000m }
            }
        });

        // 5. Interest Receipt: ₹10,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = receiptType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 20),
            ReferenceNumber = "RCT-001",
            Narration = "Bank interest credited",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 10000m, Credit = 0m },
                new() { LedgerId = interestLedger.LedgerId, Debit = 0m, Credit = 10000m }
            }
        });

        // Act
        var pl = await acctService.GetProfitAndLossAsync(company.CompanyId, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));

        // Assert
        // Gross Profit = 5,00,000 - 2,00,000 = 3,00,000
        // Indirect Expenses = 30,000 + 50,000 = 80,000
        // Indirect Incomes = 10,000
        // Net Profit = Gross Profit (3,00,000) + Indirect Income (10,000) - Indirect Expenses (80,000) = 2,30,000
        pl.GrossProfit.Should().Be(300000m);
        pl.HasGrossProfit.Should().BeTrue();
        pl.TotalIndirectExpense.Should().Be(80000m);
        pl.TotalIndirectIncome.Should().Be(10000m);
        pl.NetProfit.Should().Be(230000m);
        pl.HasNetProfit.Should().BeTrue();
        pl.NetLoss.Should().Be(0m);
    }

    [Fact]
    public async Task GetProfitAndLoss_Should_Handle_Gross_Loss_And_Net_Loss()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Loss Scenario Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");
        var purchaseGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Purchase Accounts");
        var indExpGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);
        var purchaseType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Purchase);
        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "Axis Bank",
            OpeningBalance = 1000000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var salesLedger = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Local Sales",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Credit
        });

        var purchaseLedger = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = purchaseGroup.GroupId,
            LedgerName = "Inventory Purchases",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var rentLedger = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = indExpGroup.GroupId,
            LedgerName = "Warehouse Rent",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        // 1. Sales = ₹2,00,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 5),
            ReferenceNumber = "SLS-LOSS",
            Narration = "Sales",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 200000m, Credit = 0m },
                new() { LedgerId = salesLedger.LedgerId, Debit = 0m, Credit = 200000m }
            }
        });

        // 2. Purchase = ₹3,50,000 (Purchase > Sales -> Gross Loss of ₹1,50,000)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = purchaseType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 8),
            ReferenceNumber = "PUR-LOSS",
            Narration = "Purchase",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = purchaseLedger.LedgerId, Debit = 350000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 350000m }
            }
        });

        // 3. Rent = ₹50,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 15),
            ReferenceNumber = "PMT-RENT",
            Narration = "Rent",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = rentLedger.LedgerId, Debit = 50000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 50000m }
            }
        });

        // Act
        var pl = await acctService.GetProfitAndLossAsync(company.CompanyId, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30));

        // Assert
        // Gross Loss = 3,50,000 - 2,00,000 = 1,50,000
        // Net Loss = Gross Loss (1,50,000) + Indirect Expenses (50,000) = 2,00,000
        pl.HasGrossProfit.Should().BeFalse();
        pl.GrossProfit.Should().Be(0m);
        pl.GrossLoss.Should().Be(150000m);

        pl.HasNetProfit.Should().BeFalse();
        pl.NetProfit.Should().Be(0m);
        pl.NetLoss.Should().Be(200000m);
    }

    [Fact]
    public async Task GetProfitAndLoss_Should_Exclude_Soft_Deleted_Vouchers_And_Respect_Date_Range()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Soft Delete Filter Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "Kotak Bank",
            OpeningBalance = 1000000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var salesLedger = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Direct Sales",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Credit
        });

        // 1. Voucher in April: ₹1,00,000 (Active)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 10),
            ReferenceNumber = "SLS-APR",
            Narration = "Valid April Sale",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 100000m, Credit = 0m },
                new() { LedgerId = salesLedger.LedgerId, Debit = 0m, Credit = 100000m }
            }
        });

        // 2. Voucher in April: ₹50,000 (Soft-deleted)
        var toDeleteVoucher = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 15),
            ReferenceNumber = "SLS-DEL",
            Narration = "Deleted sale",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 50000m, Credit = 0m },
                new() { LedgerId = salesLedger.LedgerId, Debit = 0m, Credit = 50000m }
            }
        });
        await acctService.DeleteVoucherAsync(toDeleteVoucher.VoucherId);

        // 3. Voucher in May: ₹75,000 (Outside April range)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 5),
            ReferenceNumber = "SLS-MAY",
            Narration = "May sale",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 75000m, Credit = 0m },
                new() { LedgerId = salesLedger.LedgerId, Debit = 0m, Credit = 75000m }
            }
        });

        // Act: Request P&L for April only
        var pl = await acctService.GetProfitAndLossAsync(company.CompanyId, new DateTime(2026, 4, 1), new DateTime(2026, 4, 30));

        // Assert:
        // Only April active sale (₹1,00,000) should be included.
        pl.TotalTradingRevenue.Should().Be(100000m);
        pl.GrossProfit.Should().Be(100000m);
        pl.NetProfit.Should().Be(100000m);
    }
}
