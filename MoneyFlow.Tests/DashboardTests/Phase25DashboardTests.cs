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
using MoneyFlow.Services.Company;
using MoneyFlow.Services.Dashboard;
using MoneyFlow.Services.FinancialYear;
using MoneyFlow.Services.Group;
using MoneyFlow.Services.Inventory;
using MoneyFlow.Services.Ledger;
using Xunit;

namespace MoneyFlow.Tests.DashboardTests;

public class Phase25DashboardTests
{
    private (AppDbContext Context, CompanyService CompService, InventoryService InvService, DashboardService DashService) CreateTestSetup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var companyRepo = new CompanyRepository(context);
        var fyRepo = new FinancialYearRepository(context);
        var groupRepo = new GroupRepository(context);
        var ledgerRepo = new LedgerRepository(context);
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

        var invService = new InventoryService(
            context,
            uow,
            NullLogger<InventoryService>.Instance);

        var dashService = new DashboardService(
            context,
            NullLogger<DashboardService>.Instance);

        return (context, compService, invService, dashService);
    }

    [Fact]
    public async Task GetDashboardData_Should_Calculate_Liquid_Funds_Cash_And_Bank_Accurately()
    {
        var (context, compService, _, dashService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Liquidity Test Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true
        });

        var cashLedger = await context.Ledgers.FirstAsync(l => l.CompanyId == company.CompanyId && l.LedgerName == "Cash");

        // Add a bank account ledger under "Bank Accounts"
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var bankLedger = new Core.Entities.Ledger
        {
            CompanyId = company.CompanyId,
            GroupId = bankGroup.GroupId,
            LedgerName = "HDFC Current Account",
            OpeningBalance = 20000m,
            OpeningBalanceType = BalanceType.Debit,
            IsActive = true
        };
        context.Ledgers.Add(bankLedger);

        // Add Voucher Types if not present
        var rctType = await context.VoucherTypes.FirstOrDefaultAsync(t => t.Type == VoucherTypeEnum.Receipt);
        if (rctType == null)
        {
            rctType = new VoucherType { Name = "Receipt", Code = "RCT", Type = VoucherTypeEnum.Receipt, Prefix = "REC-", NextNumber = 1, IsActive = true };
            context.VoucherTypes.Add(rctType);
        }

        var salesLedger = await context.Ledgers.FirstAsync(l => l.CompanyId == company.CompanyId && l.LedgerName == "Sales");

        // Receipt of ₹15,000 into Cash from Sales
        var vchReceipt = new Voucher
        {
            CompanyId = company.CompanyId,
            VoucherTypeId = rctType.VoucherTypeId,
            VoucherNumber = "REC-001",
            VoucherDate = new DateTime(2026, 5, 10),
            Narration = "Cash collection",
            IsDeleted = false
        };
        context.Vouchers.Add(vchReceipt);
        await context.SaveChangesAsync();

        context.VoucherEntries.AddRange(
            new VoucherEntry { VoucherId = vchReceipt.VoucherId, LedgerId = cashLedger.LedgerId, Debit = 15000m, Credit = 0m },
            new VoucherEntry { VoucherId = vchReceipt.VoucherId, LedgerId = salesLedger.LedgerId, Debit = 0m, Credit = 15000m }
        );
        await context.SaveChangesAsync();

        // Query Dashboard as of 31-May-2026
        var dashboard = await dashService.GetDashboardDataAsync(company.CompanyId, new DateTime(2026, 5, 31));

        dashboard.Should().NotBeNull();
        dashboard.CashBalance.Should().Be(15000m);
        dashboard.BankBalance.Should().Be(20000m);
        dashboard.TotalLiquidFunds.Should().Be(35000m);
    }

    [Fact]
    public async Task GetDashboardData_Should_Calculate_Receivables_And_Payables_For_Debtors_And_Creditors()
    {
        var (context, compService, _, dashService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Working Capital Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true
        });

        var debtorsGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var creditorsGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Creditors");

        // Customer with ₹45,000 Dr opening balance
        context.Ledgers.Add(new Core.Entities.Ledger
        {
            CompanyId = company.CompanyId,
            GroupId = debtorsGroup.GroupId,
            LedgerName = "Apex Enterprise",
            OpeningBalance = 45000m,
            OpeningBalanceType = BalanceType.Debit,
            IsActive = true
        });

        // Supplier with ₹18,000 Cr opening balance
        context.Ledgers.Add(new Core.Entities.Ledger
        {
            CompanyId = company.CompanyId,
            GroupId = creditorsGroup.GroupId,
            LedgerName = "Zenith Wholesalers",
            OpeningBalance = 18000m,
            OpeningBalanceType = BalanceType.Credit,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var dashboard = await dashService.GetDashboardDataAsync(company.CompanyId, new DateTime(2026, 9, 30));

        dashboard.TotalReceivables.Should().Be(45000m);
        dashboard.TotalPayables.Should().Be(18000m);
        dashboard.NetWorkingCapital.Should().Be(45000m - 18000m); // 27,000

        dashboard.TopDebtors.Should().HaveCount(1);
        dashboard.TopDebtors[0].PartyName.Should().Be("Apex Enterprise");
        dashboard.TopDebtors[0].Balance.Should().Be(45000m);

        dashboard.TopCreditors.Should().HaveCount(1);
        dashboard.TopCreditors[0].PartyName.Should().Be("Zenith Wholesalers");
        dashboard.TopCreditors[0].Balance.Should().Be(18000m);
    }

    [Fact]
    public async Task GetDashboardData_Should_Calculate_Profitability_Sales_Purchases_And_Net_Profit()
    {
        var (context, compService, _, dashService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Profitability Test Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true
        });

        var salesLedger = await context.Ledgers.FirstAsync(l => l.CompanyId == company.CompanyId && l.LedgerName == "Sales");
        var purchaseLedger = await context.Ledgers.FirstAsync(l => l.CompanyId == company.CompanyId && l.LedgerName == "Purchase");
        var cashLedger = await context.Ledgers.FirstAsync(l => l.CompanyId == company.CompanyId && l.LedgerName == "Cash");

        var salesType = await context.VoucherTypes.FirstOrDefaultAsync(t => t.Type == VoucherTypeEnum.Sales);
        if (salesType == null)
        {
            salesType = new VoucherType { Name = "Sales", Code = "SLS", Type = VoucherTypeEnum.Sales, Prefix = "SAL-", NextNumber = 1, IsActive = true };
            context.VoucherTypes.Add(salesType);
        }

        var purType = await context.VoucherTypes.FirstOrDefaultAsync(t => t.Type == VoucherTypeEnum.Purchase);
        if (purType == null)
        {
            purType = new VoucherType { Name = "Purchase", Code = "PUR", Type = VoucherTypeEnum.Purchase, Prefix = "PUR-", NextNumber = 1, IsActive = true };
            context.VoucherTypes.Add(purType);
        }
        await context.SaveChangesAsync();

        // 1. Sales Voucher: ₹60,000
        var vchSale = new Voucher
        {
            CompanyId = company.CompanyId,
            VoucherTypeId = salesType.VoucherTypeId,
            VoucherNumber = "SAL-001",
            VoucherDate = new DateTime(2026, 6, 1),
            Narration = "Cash Sales",
            IsDeleted = false
        };
        context.Vouchers.Add(vchSale);
        await context.SaveChangesAsync();
        context.VoucherEntries.AddRange(
            new VoucherEntry { VoucherId = vchSale.VoucherId, LedgerId = cashLedger.LedgerId, Debit = 60000m, Credit = 0m },
            new VoucherEntry { VoucherId = vchSale.VoucherId, LedgerId = salesLedger.LedgerId, Debit = 0m, Credit = 60000m }
        );

        // 2. Purchase Voucher: ₹35,000
        var vchPur = new Voucher
        {
            CompanyId = company.CompanyId,
            VoucherTypeId = purType.VoucherTypeId,
            VoucherNumber = "PUR-001",
            VoucherDate = new DateTime(2026, 6, 15),
            Narration = "Goods Purchase",
            IsDeleted = false
        };
        context.Vouchers.Add(vchPur);
        await context.SaveChangesAsync();
        context.VoucherEntries.AddRange(
            new VoucherEntry { VoucherId = vchPur.VoucherId, LedgerId = purchaseLedger.LedgerId, Debit = 35000m, Credit = 0m },
            new VoucherEntry { VoucherId = vchPur.VoucherId, LedgerId = cashLedger.LedgerId, Debit = 0m, Credit = 35000m }
        );
        await context.SaveChangesAsync();

        var dashboard = await dashService.GetDashboardDataAsync(company.CompanyId, new DateTime(2026, 6, 30));

        dashboard.TotalSales.Should().Be(60000m);
        dashboard.TotalPurchases.Should().Be(35000m);
        dashboard.GrossProfitOrLoss.Should().Be(25000m); // 60,000 - 35,000
        dashboard.NetProfitOrLoss.Should().Be(25000m);

        // Verify monthly trends contains June 2026 with ₹60k sales and ₹35k purchases
        var juneTrend = dashboard.MonthlyTrends.FirstOrDefault(m => m.Year == 2026 && m.Month == 6);
        juneTrend.Should().NotBeNull();
        juneTrend!.SalesAmount.Should().Be(60000m);
        juneTrend.PurchaseAmount.Should().Be(35000m);
    }

    [Fact]
    public async Task GetDashboardData_Should_Aggregate_Stock_Valuation()
    {
        var (context, compService, invService, dashService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Stock Valuation Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var unit = await invService.CreateUnitAsync(company.CompanyId, new UnitCreateDto
        {
            UnitName = "Pcs",
            FormalName = "Pieces"
        });

        // Item 1: 50 @ ₹120 = ₹6,000
        await invService.CreateStockItemAsync(company.CompanyId, new StockItemCreateDto
        {
            ItemName = "Item Alpha",
            UnitId = unit.UnitId,
            OpeningQuantity = 50m,
            OpeningRate = 120m
        });

        // Item 2: 100 @ ₹40 = ₹4,000
        await invService.CreateStockItemAsync(company.CompanyId, new StockItemCreateDto
        {
            ItemName = "Item Beta",
            UnitId = unit.UnitId,
            OpeningQuantity = 100m,
            OpeningRate = 40m
        });

        var dashboard = await dashService.GetDashboardDataAsync(company.CompanyId, new DateTime(2026, 8, 31));

        dashboard.TotalStockItemsCount.Should().Be(2);
        dashboard.TotalStockValuation.Should().Be(10000m); // 6,000 + 4,000
    }

    [Fact]
    public async Task GetDashboardData_Should_Enforce_Company_Isolation()
    {
        var (context, compService, _, dashService) = CreateTestSetup();

        var companyA = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Company Isolation A",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true
        });

        var companyB = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Company Isolation B",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true
        });

        // Add debtor in Company A only
        var debtorsA = await context.Groups.FirstAsync(g => g.CompanyId == companyA.CompanyId && g.GroupName == "Sundry Debtors");
        context.Ledgers.Add(new Core.Entities.Ledger
        {
            CompanyId = companyA.CompanyId,
            GroupId = debtorsA.GroupId,
            LedgerName = "Only In Company A",
            OpeningBalance = 99000m,
            OpeningBalanceType = BalanceType.Debit,
            IsActive = true
        });
        await context.SaveChangesAsync();

        var dashA = await dashService.GetDashboardDataAsync(companyA.CompanyId, new DateTime(2026, 5, 1));
        var dashB = await dashService.GetDashboardDataAsync(companyB.CompanyId, new DateTime(2026, 5, 1));

        dashA.TotalReceivables.Should().Be(99000m);
        dashA.TopDebtors.Should().Contain(d => d.PartyName == "Only In Company A");

        dashB.TotalReceivables.Should().Be(0m);
        dashB.TopDebtors.Should().BeEmpty();
    }
}
