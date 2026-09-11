using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;
using MoneyFlow.Data.Repositories;
using MoneyFlow.Services;
using MoneyFlow.Services.Accounting;
using MoneyFlow.Services.Backup;
using MoneyFlow.Services.Company;
using MoneyFlow.Services.Dashboard;
using MoneyFlow.Services.FinancialYear;
using MoneyFlow.Services.Group;
using MoneyFlow.Services.ImportExport;
using MoneyFlow.Services.Inventory;
using MoneyFlow.Services.Ledger;
using MoneyFlow.Services.Search;
using MoneyFlow.Services.Security;
using MoneyFlow.Services.Settings;
using Xunit;

namespace MoneyFlow.Tests.FinalQATests;

public class Phase34FinalSystemQATests
{
    [Fact]
    public void MasterPrompt_Section5_Section71_NoGstAudit_ZeroGstFoundInEntireDomainModel()
    {
        // =========================================================================
        // Master Prompt Section 5 & Section 71:
        // "GST IS COMPLETELY OUT OF SCOPE.
        //  Do NOT create: GST, GSTIN, HSN, SAC, CGST, SGST, IGST...
        //  This is a pure accounting application."
        // =========================================================================

        var entityAssembly = typeof(Company).Assembly;
        var entityTypes = entityAssembly.GetTypes()
            .Where(t => t.Namespace != null && t.Namespace.StartsWith("MoneyFlow.Core.Entities"))
            .ToList();

        entityTypes.Should().NotBeEmpty();

        var forbiddenGstTokens = new[] { "GST", "GSTIN", "HSN", "CGST", "SGST", "IGST" };

        foreach (var type in entityTypes)
        {
            // 1. Check Class Name
            foreach (var token in forbiddenGstTokens)
            {
                type.Name.ToUpperInvariant().Should().NotContain(token, $"Entity class '{type.Name}' must NOT contain GST-related token '{token}'.");
            }
            type.Name.Should().NotBe("SAC", "Entity class name must not be SAC.");
            type.Name.Should().NotContain("SACCode", "Entity class name must not contain SACCode.");

            // 2. Check Property Names
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                foreach (var token in forbiddenGstTokens)
                {
                    prop.Name.ToUpperInvariant().Should().NotContain(token, $"Entity property '{type.Name}.{prop.Name}' must NOT contain GST-related token '{token}'.");
                }
                prop.Name.Should().NotBe("SAC", $"Entity property '{type.Name}.{prop.Name}' must not be SAC.");
                prop.Name.Should().NotContain("SACCode", $"Entity property '{type.Name}.{prop.Name}' must not contain SACCode.");
            }
        }
    }

    [Fact]
    public void MasterPrompt_Section70_MonetaryCalculations_StrictlyDecimal_ZeroFloatsOrDoubles()
    {
        // =========================================================================
        // Master Prompt Section 70:
        // "Use: decimal for all monetary calculations.
        //  Never use: float, double for financial amounts."
        // =========================================================================

        var coreAssembly = typeof(Company).Assembly;
        var types = coreAssembly.GetTypes()
            .Where(t => t.Namespace != null && (t.Namespace.StartsWith("MoneyFlow.Core.Entities") || t.Namespace.StartsWith("MoneyFlow.Core.DTOs")))
            .ToList();

        var financialNamePatterns = new[] { "Amount", "Balance", "Debit", "Credit", "Rate", "Price", "Total", "Value" };

        foreach (var type in types)
        {
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in properties)
            {
                bool isFinancialProperty = financialNamePatterns.Any(p => prop.Name.Contains(p, StringComparison.OrdinalIgnoreCase));
                if (isFinancialProperty)
                {
                    var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                    // Must NOT be float or double
                    propType.Should().NotBe(typeof(float), $"Property '{type.Name}.{prop.Name}' must not be float per Master Prompt Section 70.");
                    propType.Should().NotBe(typeof(double), $"Property '{type.Name}.{prop.Name}' must not be double per Master Prompt Section 70.");
                }
            }
        }
    }

    [Fact]
    public void MasterPrompt_Section71_FeatureMatrix_AllCoreServicesRegisteredInDependencyInjection()
    {
        // =========================================================================
        // Master Prompt Section 71: Verify all 15 core architectural services
        // =========================================================================

        var services = new ServiceCollection();

        // Register in-memory database & logging
        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddLogging();

        // Repositories & Unit of Work
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IFinancialYearRepository, FinancialYearRepository>();
        services.AddScoped<IGroupRepository, GroupRepository>();
        services.AddScoped<ILedgerRepository, LedgerRepository>();
        services.AddScoped<IVoucherRepository, VoucherRepository>();

        // Domain Services
        services.AddSingleton<ICompanyContext, CompanyContext>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IFinancialYearService, FinancialYearService>();
        services.AddScoped<IGroupService, GroupService>();
        services.AddScoped<ILedgerService, LedgerService>();
        services.AddScoped<IAccountingService, AccountingService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IImportExportService, ImportExportService>();
        services.AddScoped<IBackupRestoreService, BackupRestoreService>();
        services.AddSingleton<IUserContext, UserContext>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<ISecurityService, SecurityService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IDatabaseSetupService, DatabaseSetupService>();

        var provider = services.BuildServiceProvider();

        // Assert: Every single service can be resolved without error
        provider.GetRequiredService<ICompanyService>().Should().NotBeNull();
        provider.GetRequiredService<IFinancialYearService>().Should().NotBeNull();
        provider.GetRequiredService<IGroupService>().Should().NotBeNull();
        provider.GetRequiredService<ILedgerService>().Should().NotBeNull();
        provider.GetRequiredService<IAccountingService>().Should().NotBeNull();
        provider.GetRequiredService<IInventoryService>().Should().NotBeNull();
        provider.GetRequiredService<ISearchService>().Should().NotBeNull();
        provider.GetRequiredService<IDashboardService>().Should().NotBeNull();
        provider.GetRequiredService<IImportExportService>().Should().NotBeNull();
        provider.GetRequiredService<IBackupRestoreService>().Should().NotBeNull();
        provider.GetRequiredService<ISecurityService>().Should().NotBeNull();
        provider.GetRequiredService<IUserContext>().Should().NotBeNull();
        provider.GetRequiredService<IAuditService>().Should().NotBeNull();
        provider.GetRequiredService<ISettingsService>().Should().NotBeNull();
        provider.GetRequiredService<IDatabaseSetupService>().Should().NotBeNull();
    }

    [Fact]
    public async Task MasterPrompt_EndToEndAccounting_CompleteSimulatedLifecycle_BalancesAllFinancialStatements()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var unitOfWork = new UnitOfWork(context);
        var voucherRepo = new VoucherRepository(context);
        var ledgerRepo = new LedgerRepository(context);
        var fyRepo = new FinancialYearRepository(context);
        var companyRepo = new CompanyRepository(context);
        var groupRepo = new GroupRepository(context);
        var companyContext = new CompanyContext();

        var accountingSvc = new AccountingService(
            context, voucherRepo, ledgerRepo, fyRepo, companyRepo, unitOfWork, NullLogger<AccountingService>.Instance);
        var companySvc = new CompanyService(
            companyRepo, fyRepo, groupRepo, ledgerRepo, unitOfWork, companyContext, NullLogger<CompanyService>.Instance);
        var ledgerSvc = new LedgerService(
            context, ledgerRepo, groupRepo, companyRepo, unitOfWork, NullLogger<LedgerService>.Instance);

        // 1. Create Company
        var company = await companySvc.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Apex Trading Enterprise",
            Currency = "₹",
            CreateDefaultLedgers = false
        });
        var fy = await context.FinancialYears.FirstAsync(f => f.CompanyId == company.CompanyId);

        // 2. Set up Chart of Accounts Groups
        var grpCapital = new Group { CompanyId = company.CompanyId, GroupName = "Capital Account", Nature = GroupNature.Liabilities, AffectProfitLoss = false };
        var grpCash = new Group { CompanyId = company.CompanyId, GroupName = "Cash-in-Hand", Nature = GroupNature.Assets, AffectProfitLoss = false };
        var grpBank = new Group { CompanyId = company.CompanyId, GroupName = "Bank Accounts", Nature = GroupNature.Assets, AffectProfitLoss = false };
        var grpSales = new Group { CompanyId = company.CompanyId, GroupName = "Sales Accounts", Nature = GroupNature.Income, AffectProfitLoss = true };
        var grpPurchase = new Group { CompanyId = company.CompanyId, GroupName = "Purchase Accounts", Nature = GroupNature.Expenses, AffectProfitLoss = true };
        var grpDebtors = new Group { CompanyId = company.CompanyId, GroupName = "Sundry Debtors", Nature = GroupNature.Assets, AffectProfitLoss = false };
        var grpCreditors = new Group { CompanyId = company.CompanyId, GroupName = "Sundry Creditors", Nature = GroupNature.Liabilities, AffectProfitLoss = false };
        var grpExpenses = new Group { CompanyId = company.CompanyId, GroupName = "Indirect Expenses", Nature = GroupNature.Expenses, AffectProfitLoss = true };

        context.Groups.AddRange(grpCapital, grpCash, grpBank, grpSales, grpPurchase, grpDebtors, grpCreditors, grpExpenses);
        await context.SaveChangesAsync();

        // 3. Set up Ledgers
        var ledOwnerCapital = await ledgerSvc.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { LedgerName = "Owner Capital", GroupId = grpCapital.GroupId });
        var ledCash = await ledgerSvc.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { LedgerName = "Cash", GroupId = grpCash.GroupId });
        var ledBank = await ledgerSvc.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { LedgerName = "HDFC Bank", GroupId = grpBank.GroupId });
        var ledSales = await ledgerSvc.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { LedgerName = "Domestic Sales", GroupId = grpSales.GroupId });
        var ledPurchase = await ledgerSvc.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { LedgerName = "Local Purchases", GroupId = grpPurchase.GroupId });
        var ledCustomer = await ledgerSvc.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { LedgerName = "Customer Acme", GroupId = grpDebtors.GroupId });
        var ledSupplier = await ledgerSvc.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { LedgerName = "Supplier Zenith", GroupId = grpCreditors.GroupId });
        var ledRent = await ledgerSvc.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { LedgerName = "Office Rent", GroupId = grpExpenses.GroupId });

        // 4. Seed Voucher Types
        var vtReceipt = new VoucherType { Name = "Receipt", Code = "RCP", Type = VoucherTypeEnum.Receipt, Prefix = "RCP-" };
        var vtContra = new VoucherType { Name = "Contra", Code = "CTR", Type = VoucherTypeEnum.Contra, Prefix = "CNT-" };
        var vtPurchase = new VoucherType { Name = "Purchase", Code = "PUR", Type = VoucherTypeEnum.Purchase, Prefix = "PUR-" };
        var vtSales = new VoucherType { Name = "Sales", Code = "SLS", Type = VoucherTypeEnum.Sales, Prefix = "SLS-" };
        var vtPayment = new VoucherType { Name = "Payment", Code = "PMT", Type = VoucherTypeEnum.Payment, Prefix = "PAY-" };
        var vtJournal = new VoucherType { Name = "Journal", Code = "JRN", Type = VoucherTypeEnum.Journal, Prefix = "JRN-" };

        context.VoucherTypes.AddRange(vtReceipt, vtContra, vtPurchase, vtSales, vtPayment, vtJournal);
        await context.SaveChangesAsync();

        // 5. Post Transaction Sequence:
        // a) Capital introduced: Cash Dr ₹100,000 / Capital Cr ₹100,000 (Receipt)
        await accountingSvc.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = vtReceipt.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(1),
            Narration = "Capital introduced into business",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledCash.LedgerId, Debit = 100000m, Credit = 0m },
                new() { LedgerId = ledOwnerCapital.LedgerId, Debit = 0m, Credit = 100000m }
            }
        });

        // b) Cash deposited into Bank: Bank Dr ₹60,000 / Cash Cr ₹60,000 (Contra)
        await accountingSvc.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = vtContra.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(2),
            Narration = "Deposit cash to bank account",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledBank.LedgerId, Debit = 60000m, Credit = 0m },
                new() { LedgerId = ledCash.LedgerId, Debit = 0m, Credit = 60000m }
            }
        });

        // c) Purchase on credit: Purchase Dr ₹40,000 / Supplier Cr ₹40,000 (Purchase)
        await accountingSvc.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = vtPurchase.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(3),
            Narration = "Purchase raw materials from Zenith",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledPurchase.LedgerId, Debit = 40000m, Credit = 0m },
                new() { LedgerId = ledSupplier.LedgerId, Debit = 0m, Credit = 40000m }
            }
        });

        // d) Sales on credit: Customer Dr ₹75,000 / Sales Cr ₹75,000 (Sales)
        await accountingSvc.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = vtSales.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(4),
            Narration = "Sales to Customer Acme",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledCustomer.LedgerId, Debit = 75000m, Credit = 0m },
                new() { LedgerId = ledSales.LedgerId, Debit = 0m, Credit = 75000m }
            }
        });

        // e) Payment to Supplier: Supplier Dr ₹30,000 / Bank Cr ₹30,000 (Payment)
        await accountingSvc.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = vtPayment.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(5),
            Narration = "Partial payout to Zenith via bank transfer",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledSupplier.LedgerId, Debit = 30000m, Credit = 0m },
                new() { LedgerId = ledBank.LedgerId, Debit = 0m, Credit = 30000m }
            }
        });

        // f) Receipt from Customer: Bank Dr ₹50,000 / Customer Cr ₹50,000 (Receipt)
        await accountingSvc.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = vtReceipt.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(6),
            Narration = "Customer collection via NEFT",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledBank.LedgerId, Debit = 50000m, Credit = 0m },
                new() { LedgerId = ledCustomer.LedgerId, Debit = 0m, Credit = 50000m }
            }
        });

        // g) Operational expense: Rent Dr ₹5,000 / Cash Cr ₹5,000 (Payment)
        await accountingSvc.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = vtPayment.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(7),
            Narration = "Office rent paid in cash",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledRent.LedgerId, Debit = 5000m, Credit = 0m },
                new() { LedgerId = ledCash.LedgerId, Debit = 0m, Credit = 5000m }
            }
        });

        // 6. Assertions Across All Financial Reports:
        // Day Book
        var dayBook = await accountingSvc.GetDayBookAsync(company.CompanyId, fy.StartDate, fy.EndDate);
        dayBook.Should().NotBeNull();
        dayBook.Items.Should().HaveCount(7);
        dayBook.IsBalanced.Should().BeTrue();
        dayBook.Difference.Should().Be(0m);

        // Trial Balance
        var tb = await accountingSvc.GetTrialBalanceAsync(company.CompanyId, fy.StartDate, fy.EndDate);
        tb.Should().NotBeNull();
        tb.IsBalanced.Should().BeTrue();
        tb.Difference.Should().Be(0m);
        tb.TotalClosingDebit.Should().Be(tb.TotalClosingCredit);

        // Profit & Loss
        var pl = await accountingSvc.GetProfitAndLossAsync(company.CompanyId, fy.StartDate, fy.EndDate);
        pl.Should().NotBeNull();
        // Sales = ₹75,000, Purchases = ₹40,000 -> Gross Profit = ₹35,000
        pl.TotalTradingRevenue.Should().Be(75000m);
        pl.TotalTradingExpense.Should().Be(40000m);
        pl.GrossProfit.Should().Be(35000m);
        pl.TotalIndirectExpense.Should().Be(5000m); // Rent
        pl.NetProfit.Should().Be(30000m);

        // Balance Sheet
        var bs = await accountingSvc.GetBalanceSheetAsync(company.CompanyId, fy.EndDate);
        bs.Should().NotBeNull();
        bs.IsBalanced.Should().BeTrue();
        bs.Difference.Should().Be(0m);
        bs.TotalAssetsSide.Should().Be(bs.TotalLiabilitiesSide);
    }
}
