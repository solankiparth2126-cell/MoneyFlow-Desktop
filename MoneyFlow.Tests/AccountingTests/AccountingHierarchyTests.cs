using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MoneyFlow.Core.Constants;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Data;
using MoneyFlow.Data.Repositories;
using MoneyFlow.Services;
using MoneyFlow.Services.Accounting;
using MoneyFlow.Services.Company;
using MoneyFlow.Services.FinancialYear;
using MoneyFlow.Services.Group;
using MoneyFlow.Services.Ledger;
using Xunit;
using GroupEntity = MoneyFlow.Core.Entities.Group;
using LedgerEntity = MoneyFlow.Core.Entities.Ledger;

namespace MoneyFlow.Tests.AccountingTests;

public class AccountingHierarchyTests
{
    private (AppDbContext Context, CompanyService CompService, GroupService GrpService, LedgerService LedgService, AccountingService AcctService, AccountingHierarchyService HierarchyService) CreateTestSetup()
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

        var grpService = new GroupService(
            context,
            groupRepo,
            companyRepo,
            uow,
            NullLogger<GroupService>.Instance);

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

        var hierarchyService = new AccountingHierarchyService(
            context,
            NullLogger<AccountingHierarchyService>.Instance);

        return (context, compService, grpService, ledgService, acctService, hierarchyService);
    }

    private async Task<(Company Company, FinancialYear FY)> CreateTestCompanyAsync(CompanyService compService, AppDbContext context)
    {
        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Hierarchy Corp",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var fy = await context.FinancialYears.FirstAsync(f => f.CompanyId == company.CompanyId);
        return (company, fy);
    }

    [Fact]
    public async Task Test1_CreateGroup_SubGroup_Ledger_ResolvesCorrectHierarchy()
    {
        var (context, compService, grpService, ledgService, _, hierarchyService) = CreateTestSetup();
        var (company, _) = await CreateTestCompanyAsync(compService, context);

        // Find or create Primary Group "Current Assets"
        var currentAssets = await context.Groups.FirstOrDefaultAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Current Assets");
        currentAssets.Should().NotBeNull();
        currentAssets!.ParentGroupId.Should().BeNull();
        currentAssets.PrimaryGroup.Should().BeTrue();

        // Find or create Sub Group "Bank Accounts" under "Current Assets"
        var bankAccounts = await context.Groups.FirstOrDefaultAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        bankAccounts.Should().NotBeNull();
        bankAccounts!.ParentGroupId.Should().Be(currentAssets.GroupId);

        // Create Ledger: HDFC Bank under Bank Accounts
        var hdfcLedger = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankAccounts.GroupId,
            LedgerName = "HDFC Bank",
            OpeningBalance = 50000m,
            OpeningBalanceType = BalanceType.Debit
        });

        // Verify database foreign keys
        var dbLedger = await context.Ledgers.Include(l => l.Group).ThenInclude(g => g!.ParentGroup)
            .FirstOrDefaultAsync(l => l.LedgerId == hdfcLedger.LedgerId);

        dbLedger.Should().NotBeNull();
        dbLedger!.GroupId.Should().Be(bankAccounts.GroupId);
        dbLedger.Group!.ParentGroupId.Should().Be(currentAssets.GroupId);

        // Verify path resolution through AccountingHierarchyService
        var ledgerPath = await hierarchyService.GetLedgerPathAsync(hdfcLedger.LedgerId);
        ledgerPath.Should().Be("Current Assets > Bank Accounts > HDFC Bank");
    }

    [Fact]
    public async Task Test2_CreateSupplier_UnderSundryCreditors_UnderCurrentLiabilities()
    {
        var (context, compService, grpService, ledgService, _, hierarchyService) = CreateTestSetup();
        var (company, _) = await CreateTestCompanyAsync(compService, context);

        var currentLiabilities = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Current Liabilities");
        var sundryCreditors = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Creditors");
        sundryCreditors.ParentGroupId.Should().Be(currentLiabilities.GroupId);

        var abcSupplier = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = sundryCreditors.GroupId,
            LedgerName = "ABC Supplier",
            OpeningBalance = 25000m,
            OpeningBalanceType = BalanceType.Credit
        });

        var path = await hierarchyService.GetLedgerPathAsync(abcSupplier.LedgerId);
        path.Should().Be("Current Liabilities > Sundry Creditors > ABC Supplier");
    }

    [Fact]
    public async Task Test3_CreatePaymentVoucher_StoresStableLedgerIds()
    {
        var (context, compService, grpService, ledgService, acctService, _) = CreateTestSetup();
        var (company, fy) = await CreateTestCompanyAsync(compService, context);

        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        var hdfc = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "HDFC Bank",
            OpeningBalance = 100000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var electricity = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Electricity Expense",
            OpeningBalance = 0m,
            OpeningBalanceType = BalanceType.Debit
        });

        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        var voucherDto = new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 10),
            Narration = "Payment for electricity bill",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = electricity.LedgerId, Debit = 5000m, Credit = 0m, Narration = "Electricity Exp" },
                new() { LedgerId = hdfc.LedgerId, Debit = 0m, Credit = 5000m, Narration = "Bank payment" }
            }
        };

        var posted = await acctService.SaveVoucherAsync(company.CompanyId, voucherDto);
        posted.Should().NotBeNull();

        // Verify entries in database reference exact LedgerId
        var dbVoucher = await acctService.GetVoucherByIdAsync(posted.VoucherId);
        dbVoucher!.VoucherEntries.Should().HaveCount(2);
        dbVoucher.VoucherEntries.First(e => e.Debit == 5000m).LedgerId.Should().Be(electricity.LedgerId);
        dbVoucher.VoucherEntries.First(e => e.Credit == 5000m).LedgerId.Should().Be(hdfc.LedgerId);
    }

    [Fact]
    public async Task Test4_OpenLedgerReport_HDFC_ShowsTransactionsAndClosingBalance()
    {
        var (context, compService, grpService, ledgService, acctService, _) = CreateTestSetup();
        var (company, fy) = await CreateTestCompanyAsync(compService, context);

        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        var hdfc = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "HDFC Bank",
            OpeningBalance = 50000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var rent = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Rent Expense",
            OpeningBalance = 0m
        });

        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 15),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = rent.LedgerId, Debit = 15000m, Credit = 0m },
                new() { LedgerId = hdfc.LedgerId, Debit = 0m, Credit = 15000m }
            }
        });

        var statement = await acctService.GetLedgerStatementAsync(company.CompanyId, hdfc.LedgerId, fy.StartDate, fy.EndDate);
        statement.Should().NotBeNull();
        statement!.OpeningBalance.Should().Be(50000m);
        statement.TotalCredit.Should().Be(15000m);
        statement.ClosingBalance.Should().Be(35000m);
        statement.ClosingType.Should().Be(BalanceType.Debit);
        statement.Lines.Should().HaveCount(1);
    }

    [Fact]
    public async Task Test5_TrialBalance_CalculatesLedgerBalances_And_Reconciles()
    {
        var (context, compService, grpService, ledgService, acctService, _) = CreateTestSetup();
        var (company, fy) = await CreateTestCompanyAsync(compService, context);

        var capGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Capital Account");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");

        var capital = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = capGroup.GroupId,
            LedgerName = "Parth Capital",
            OpeningBalance = 50000m,
            OpeningBalanceType = BalanceType.Credit
        });

        var hdfc = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "HDFC Bank",
            OpeningBalance = 50000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var tb = await acctService.GetTrialBalanceAsync(company.CompanyId, fy.StartDate, fy.EndDate);
        tb.Should().NotBeNull();
        tb.TotalClosingDebit.Should().Be(50000m);
        tb.TotalClosingCredit.Should().Be(50000m);
        tb.IsBalanced.Should().BeTrue();

        var hdfcItem = tb.Items.FirstOrDefault(i => i.LedgerId == hdfc.LedgerId);
        hdfcItem.Should().NotBeNull();
        hdfcItem!.GroupName.Should().Be("Bank Accounts");
        hdfcItem.ClosingDebit.Should().Be(50000m);
    }

    [Fact]
    public async Task Test6_BalanceSheet_HDFC_ContributesToCurrentAssets()
    {
        var (context, compService, grpService, ledgService, acctService, _) = CreateTestSetup();
        var (company, fy) = await CreateTestCompanyAsync(compService, context);

        var capGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Capital Account");
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");

        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = capGroup.GroupId,
            LedgerName = "Parth Capital",
            OpeningBalance = 100000m,
            OpeningBalanceType = BalanceType.Credit
        });

        var hdfc = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "HDFC Bank",
            OpeningBalance = 100000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var bs = await acctService.GetBalanceSheetAsync(company.CompanyId, fy.EndDate);
        bs.Should().NotBeNull();
        bs.TotalAssetsSide.Should().Be(100000m);
        bs.TotalLiabilitiesSide.Should().Be(100000m);
        bs.IsBalanced.Should().BeTrue();

        var assetGroup = bs.Assets.FirstOrDefault(a => a.GroupName == "Bank Accounts");
        assetGroup.Should().NotBeNull();
        assetGroup!.Lines.Should().Contain(l => l.LedgerId == hdfc.LedgerId && l.Amount == 100000m);
    }

    [Fact]
    public async Task Test7_ProfitLoss_ExpenseLedgerContributesToExpenses()
    {
        var (context, compService, grpService, ledgService, acctService, _) = CreateTestSetup();
        var (company, fy) = await CreateTestCompanyAsync(compService, context);

        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");
        var expGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Direct Expenses");

        var bank = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "HDFC Bank",
            OpeningBalance = 200000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var sales = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Sales",
            OpeningBalance = 0m
        });

        var salary = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expGroup.GroupId,
            LedgerName = "Salary Expense",
            OpeningBalance = 0m
        });

        var receiptType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Receipt);
        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        // Sales income ₹1,00,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = receiptType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 10),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bank.LedgerId, Debit = 100000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 100000m }
            }
        });

        // Salary expense ₹40,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = paymentType.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 25),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = salary.LedgerId, Debit = 40000m, Credit = 0m },
                new() { LedgerId = bank.LedgerId, Debit = 0m, Credit = 40000m }
            }
        });

        var pl = await acctService.GetProfitAndLossAsync(company.CompanyId, fy.StartDate, fy.EndDate);
        pl.Should().NotBeNull();
        pl.TotalTradingRevenue.Should().Be(100000m);
        pl.TotalTradingExpense.Should().Be(40000m);
        pl.NetProfit.Should().Be(60000m);
    }

    [Fact]
    public async Task Test8_MoveLedgerToAnotherGroup_AutomaticallyReflectsInHierarchyAndPath()
    {
        var (context, compService, grpService, ledgService, _, hierarchyService) = CreateTestSetup();
        var (company, _) = await CreateTestCompanyAsync(compService, context);

        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && (g.GroupName == "Cash-in-hand" || g.GroupName == "Cash-in-Hand"));

        var hdfc = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "HDFC Bank",
            OpeningBalance = 50000m
        });

        var initialPath = await hierarchyService.GetLedgerPathAsync(hdfc.LedgerId);
        initialPath.Should().Be("Current Assets > Bank Accounts > HDFC Bank");

        // Move to Cash-in-hand group
        await ledgService.UpdateLedgerAsync(new LedgerUpdateDto
        {
            LedgerId = hdfc.LedgerId,
            GroupId = cashGroup.GroupId,
            LedgerName = "HDFC Bank",
            OpeningBalance = 50000m,
            OpeningBalanceType = BalanceType.Debit,
            IsActive = true
        });

        var updatedPath = await hierarchyService.GetLedgerPathAsync(hdfc.LedgerId);
        updatedPath.Should().Be($"Current Assets > {cashGroup.GroupName} > HDFC Bank");
    }

    [Fact]
    public async Task Test9_CircularGroupHierarchy_IsSafelyRejected()
    {
        var (context, compService, grpService, _, _, _) = CreateTestSetup();
        var (company, _) = await CreateTestCompanyAsync(compService, context);

        var parent = await grpService.CreateGroupAsync(company.CompanyId, new GroupCreateDto
        {
            GroupName = "Parent Group A",
            Nature = GroupNature.Assets
        });

        var child = await grpService.CreateGroupAsync(company.CompanyId, new GroupCreateDto
        {
            GroupName = "Child Group B",
            ParentGroupId = parent.GroupId,
            Nature = GroupNature.Assets
        });

        // Attempt to make parent a child of its own descendant (Parent Group A under Child Group B)
        var act = async () =>
        {
            await grpService.UpdateGroupAsync(new GroupUpdateDto
            {
                GroupId = parent.GroupId,
                GroupName = parent.GroupName,
                ParentGroupId = child.GroupId,
                Nature = parent.Nature
            });
        };

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*circular reference*");
    }

    [Fact]
    public async Task Test10_DeleteGroupContainingLedgers_IsSafelyRejected()
    {
        var (context, compService, grpService, ledgService, _, _) = CreateTestSetup();
        var (company, _) = await CreateTestCompanyAsync(compService, context);

        var group = await grpService.CreateGroupAsync(company.CompanyId, new GroupCreateDto
        {
            GroupName = "Operations Department",
            Nature = GroupNature.Expenses
        });

        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = group.GroupId,
            LedgerName = "Cleaning Supplies",
            OpeningBalance = 0m
        });

        // Attempt to delete group that contains a ledger
        var act = async () => await grpService.DeleteGroupAsync(group.GroupId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*contains ledgers*");
    }

    [Fact]
    public async Task Test11_RecursiveGroupBalance_RollsUpSubGroupsAndDirectLedgers()
    {
        var (context, compService, grpService, ledgService, _, hierarchyService) = CreateTestSetup();
        var (company, _) = await CreateTestCompanyAsync(compService, context);

        var currentAssets = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Current Assets");
        var bankAccounts = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var cashInHand = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && (g.GroupName == "Cash-in-hand" || g.GroupName == "Cash-in-Hand"));

        // Ledger 1: HDFC Bank ₹50,000 Dr under Bank Accounts
        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankAccounts.GroupId,
            LedgerName = "HDFC Bank",
            OpeningBalance = 50000m,
            OpeningBalanceType = BalanceType.Debit
        });

        // Ledger 2: SBI Bank ₹20,000 Dr under Bank Accounts
        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankAccounts.GroupId,
            LedgerName = "SBI Bank",
            OpeningBalance = 20000m,
            OpeningBalanceType = BalanceType.Debit
        });

        // Ledger 3: Cash ₹10,000 Dr under Cash-in-Hand
        await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashInHand.GroupId,
            LedgerName = "Cash",
            OpeningBalance = 10000m,
            OpeningBalanceType = BalanceType.Debit
        });

        // Recursive balance for Bank Accounts should be ₹70,000
        var bankBalance = await hierarchyService.GetGroupBalanceAsync(company.CompanyId, bankAccounts.GroupId);
        bankBalance.TotalDebit.Should().Be(70000m);

        // Recursive balance for Current Assets (parent of Bank Accounts & Cash-in-Hand) should be ₹80,000!
        var currentAssetsBalance = await hierarchyService.GetGroupBalanceAsync(company.CompanyId, currentAssets.GroupId);
        currentAssetsBalance.TotalDebit.Should().Be(80000m);
        currentAssetsBalance.TotalLedgerCount.Should().Be(3);
    }

    [Fact]
    public async Task Test12_Predefined28Groups_ExistWithCorrectHierarchy()
    {
        var (context, compService, _, _, _, _) = CreateTestSetup();
        var (company, _) = await CreateTestCompanyAsync(compService, context);

        var groups = await context.Groups
            .Where(g => g.CompanyId == company.CompanyId)
            .ToListAsync();

        // Exactly 28 predefined groups
        groups.Should().HaveCount(28);

        // 15 Primary Groups
        var primaryGroups = groups.Where(g => g.PrimaryGroup).ToList();
        primaryGroups.Should().HaveCount(15);
        foreach (var pgDef in PredefinedAccountingGroups.PrimaryGroups)
        {
            var matched = primaryGroups.FirstOrDefault(g => g.GroupName.Equals(pgDef.Name, StringComparison.OrdinalIgnoreCase));
            matched.Should().NotBeNull($"Primary group '{pgDef.Name}' must exist");
            matched!.ParentGroupId.Should().BeNull();
            matched.Nature.Should().Be(pgDef.Nature);
            matched.IsPredefined.Should().BeTrue();
        }

        // 13 Sub-Groups
        var subGroups = groups.Where(g => !g.PrimaryGroup).ToList();
        subGroups.Should().HaveCount(13);
        var groupById = groups.ToDictionary(g => g.GroupId);

        foreach (var sgDef in PredefinedAccountingGroups.SubGroups)
        {
            var matched = subGroups.FirstOrDefault(g => g.GroupName.Equals(sgDef.Name, StringComparison.OrdinalIgnoreCase));
            matched.Should().NotBeNull($"Sub-group '{sgDef.Name}' must exist");
            matched!.ParentGroupId.Should().NotBeNull();
            var parent = groupById[matched.ParentGroupId!.Value];
            parent.GroupName.Should().Be(sgDef.ParentGroupName);
            matched.Nature.Should().Be(sgDef.Nature);
            matched.IsPredefined.Should().BeTrue();
        }
    }

    [Fact]
    public async Task Test13_SeedDefaultLedgers_CreatesOnlyRealLedgers_AndNoGroups()
    {
        var (context, compService, _, _, _, _) = CreateTestSetup();
        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Ledger Audit Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true // Seed default ledgers
        });

        var ledgers = await context.Ledgers
            .Include(l => l.Group)
            .Where(l => l.CompanyId == company.CompanyId)
            .ToListAsync();

        // Only 4 canonical default ledgers: Cash, Profit & Loss A/c, Sales, Purchase
        ledgers.Should().HaveCount(4);

        var cash = ledgers.FirstOrDefault(l => l.LedgerName == "Cash");
        cash.Should().NotBeNull();
        cash!.Group!.GroupName.Should().Be("Cash-in-hand");

        var pnl = ledgers.FirstOrDefault(l => l.LedgerName == "Profit & Loss A/c");
        pnl.Should().NotBeNull();
        pnl!.Group!.GroupName.Should().Be("Capital Account");

        var sales = ledgers.FirstOrDefault(l => l.LedgerName == "Sales");
        sales.Should().NotBeNull();
        sales!.Group!.GroupName.Should().Be("Sales Accounts");

        var purchase = ledgers.FirstOrDefault(l => l.LedgerName == "Purchase");
        purchase.Should().NotBeNull();
        purchase!.Group!.GroupName.Should().Be("Purchase Accounts");

        // Assert that NO groups exist as ledgers
        var groupNames = PredefinedAccountingGroups.PrimaryGroups.Select(g => g.Name)
            .Concat(PredefinedAccountingGroups.SubGroups.Select(g => g.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var l in ledgers)
        {
            groupNames.Should().NotContain(l.LedgerName, $"Ledger '{l.LedgerName}' must not have the name of an accounting group");
        }
    }

    [Fact]
    public async Task Test14_PredefinedGroup_CannotBeDeleted()
    {
        var (context, compService, grpService, _, _, _) = CreateTestSetup();
        var (company, _) = await CreateTestCompanyAsync(compService, context);

        var suspenseAc = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Suspense A/c");

        var act = async () => await grpService.DeleteGroupAsync(suspenseAc.GroupId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*predefined*");
    }

    [Fact]
    public async Task Test15_ReservedLedgers_CannotBeDeleted()
    {
        var (context, compService, _, ledgService, _, _) = CreateTestSetup();
        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Reserved Ledger Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = true
        });

        var cash = await context.Ledgers.FirstAsync(l => l.CompanyId == company.CompanyId && l.LedgerName == "Cash");
        var actCash = async () => await ledgService.DeleteLedgerAsync(cash.LedgerId);
        await actCash.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reserved*");

        var pnl = await context.Ledgers.FirstAsync(l => l.CompanyId == company.CompanyId && l.LedgerName == "Profit & Loss A/c");
        var actPnl = async () => await ledgService.DeleteLedgerAsync(pnl.LedgerId);
        await actPnl.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*reserved*");
    }

    [Fact]
    public async Task Test16_DatabaseMigration_PurgesDummyGroupAsLedger_AndFixesHierarchy()
    {
        var (context, _, _, _, _, _) = CreateTestSetup();

        // 1. Manually simulate old/legacy database state for a company
        var company = new Company
        {
            CompanyName = "Legacy Migration Co",
            Currency = "₹",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            BooksBeginningFrom = new DateTime(2026, 4, 1),
            IsActive = true,
            CreatedAt = DateTime.Now
        };
        await context.Companies.AddAsync(company);
        await context.SaveChangesAsync();

        // Seed old groups with legacy names and Duties & Taxes as primary
        var curLiab = new GroupEntity { CompanyId = company.CompanyId, GroupName = "Current Liabilities", Nature = GroupNature.Liabilities, PrimaryGroup = true, IsActive = true };
        var curAssets = new GroupEntity { CompanyId = company.CompanyId, GroupName = "Current Assets", Nature = GroupNature.Assets, PrimaryGroup = true, IsActive = true };
        var dutiesTaxes = new GroupEntity { CompanyId = company.CompanyId, GroupName = "Duties & Taxes", Nature = GroupNature.Liabilities, PrimaryGroup = true, ParentGroupId = null, IsActive = true };
        var legacyLoans = new GroupEntity { CompanyId = company.CompanyId, GroupName = "Loans", Nature = GroupNature.Liabilities, PrimaryGroup = true, IsActive = true };
        var cashInHand = new GroupEntity { CompanyId = company.CompanyId, GroupName = "Cash-in-hand", Nature = GroupNature.Assets, ParentGroupId = curAssets.GroupId, PrimaryGroup = false, IsActive = true };

        await context.Groups.AddRangeAsync(curLiab, curAssets, dutiesTaxes, legacyLoans, cashInHand);
        await context.SaveChangesAsync();

        // Seed dummy group-as-ledgers
        var dummy1 = new LedgerEntity { CompanyId = company.CompanyId, GroupId = curLiab.GroupId, LedgerName = "Capital Account", IsActive = true };
        var dummy2 = new LedgerEntity { CompanyId = company.CompanyId, GroupId = curAssets.GroupId, LedgerName = "Direct Expenses", IsActive = true };
        var dummy3 = new LedgerEntity { CompanyId = company.CompanyId, GroupId = curLiab.GroupId, LedgerName = "Sundry Creditors", IsActive = true };
        var realCash = new LedgerEntity { CompanyId = company.CompanyId, GroupId = curAssets.GroupId, LedgerName = "Cash", IsActive = true }; // Wrong parent

        await context.Ledgers.AddRangeAsync(dummy1, dummy2, dummy3, realCash);
        await context.SaveChangesAsync();

        // 2. Run Startup Migration
        var dbSetup = new DatabaseSetupService(context, NullLogger<DatabaseSetupService>.Instance);
        await dbSetup.InitializeDatabaseAsync();

        // 3. Verify Migration Results
        // 3a. Duties & Taxes is now a Sub-Group under Current Liabilities
        var migratedDT = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Duties & Taxes");
        migratedDT.PrimaryGroup.Should().BeFalse();
        migratedDT.ParentGroupId.Should().Be(curLiab.GroupId);

        // 3b. "Loans" renamed to "Loans (Liability)"
        var migratedLoans = await context.Groups.FirstOrDefaultAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Loans (Liability)");
        migratedLoans.Should().NotBeNull();

        // 3c. All 28 groups exist
        var totalGroups = await context.Groups.Where(g => g.CompanyId == company.CompanyId).CountAsync();
        totalGroups.Should().Be(28);

        // 3d. Dummy ledgers with 0 transactions were safely purged
        var remainingLedgers = await context.Ledgers.Where(l => l.CompanyId == company.CompanyId).ToListAsync();
        remainingLedgers.Should().NotContain(l => l.LedgerName == "Capital Account");
        remainingLedgers.Should().NotContain(l => l.LedgerName == "Direct Expenses");
        remainingLedgers.Should().NotContain(l => l.LedgerName == "Sundry Creditors");

        // 3e. Cash ledger re-mapped to Cash-in-hand group
        var migratedCash = remainingLedgers.First(l => l.LedgerName == "Cash");
        migratedCash.GroupId.Should().Be(cashInHand.GroupId);
    }
}
