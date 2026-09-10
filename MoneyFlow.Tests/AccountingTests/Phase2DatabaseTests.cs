using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Data;
using MoneyFlow.Data.Repositories;
using Xunit;

namespace MoneyFlow.Tests.AccountingTests;

public class Phase2DatabaseTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public async Task MultiCompany_Isolation_Should_Never_Leak_Data_Between_Companies()
    {
        using var context = CreateInMemoryDbContext();
        var companyRepo = new CompanyRepository(context);
        var ledgerRepo = new LedgerRepository(context);
        var uow = new UnitOfWork(context);

        // 1. Setup Company A
        var companyA = new Company
        {
            CompanyName = "Alpha Corp",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            BooksBeginningFrom = new DateTime(2026, 4, 1)
        };
        await companyRepo.AddAsync(companyA);
        await uow.SaveChangesAsync();

        // 2. Setup Company B
        var companyB = new Company
        {
            CompanyName = "Beta Industries",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            BooksBeginningFrom = new DateTime(2026, 4, 1)
        };
        await companyRepo.AddAsync(companyB);
        await uow.SaveChangesAsync();

        // 3. Create Group & Ledger inside Company A
        var groupA = new Group
        {
            CompanyId = companyA.CompanyId,
            GroupName = "Current Assets",
            Nature = GroupNature.Assets
        };
        await context.Groups.AddAsync(groupA);
        await uow.SaveChangesAsync();

        var ledgerA = new Ledger
        {
            CompanyId = companyA.CompanyId,
            GroupId = groupA.GroupId,
            LedgerName = "Cash Alpha",
            OpeningBalance = 10000m,
            OpeningBalanceType = BalanceType.Debit
        };
        await ledgerRepo.AddAsync(ledgerA);
        await uow.SaveChangesAsync();

        // 4. Create Group & Ledger inside Company B
        var groupB = new Group
        {
            CompanyId = companyB.CompanyId,
            GroupName = "Current Assets",
            Nature = GroupNature.Assets
        };
        await context.Groups.AddAsync(groupB);
        await uow.SaveChangesAsync();

        var ledgerB = new Ledger
        {
            CompanyId = companyB.CompanyId,
            GroupId = groupB.GroupId,
            LedgerName = "Cash Beta",
            OpeningBalance = 5000m,
            OpeningBalanceType = BalanceType.Debit
        };
        await ledgerRepo.AddAsync(ledgerB);
        await uow.SaveChangesAsync();

        // 5. Query Ledgers for Company B
        var companyBLedgers = await ledgerRepo.GetByCompanyIdAsync(companyB.CompanyId);

        // Assert: Company B must see only Cash Beta and NOT Cash Alpha!
        companyBLedgers.Should().HaveCount(1);
        companyBLedgers.First().LedgerName.Should().Be("Cash Beta");
        companyBLedgers.Any(l => l.LedgerName == "Cash Alpha").Should().BeFalse();
    }

    [Fact]
    public async Task Hierarchical_Group_Tree_Should_Support_Nested_SubGroups()
    {
        using var context = CreateInMemoryDbContext();
        var groupRepo = new GroupRepository(context);
        var uow = new UnitOfWork(context);

        int companyId = 1;

        // Primary Group: Current Assets
        var parentGroup = new Group
        {
            CompanyId = companyId,
            GroupName = "Current Assets",
            Nature = GroupNature.Assets,
            PrimaryGroup = true
        };
        await groupRepo.AddAsync(parentGroup);
        await uow.SaveChangesAsync();

        // Child Group: Bank Accounts
        var childGroup = new Group
        {
            CompanyId = companyId,
            GroupName = "Bank Accounts",
            ParentGroupId = parentGroup.GroupId,
            Nature = GroupNature.Assets,
            PrimaryGroup = false
        };
        await groupRepo.AddAsync(childGroup);
        await uow.SaveChangesAsync();

        // Query hierarchical groups
        var groups = await groupRepo.GetByCompanyIdAsync(companyId);
        groups.Should().HaveCount(2);

        var retrievedChild = groups.First(g => g.GroupName == "Bank Accounts");
        retrievedChild.ParentGroupId.Should().Be(parentGroup.GroupId);
    }

    [Fact]
    public async Task Voucher_Creation_With_Entries_And_Cascade_Delete()
    {
        using var context = CreateInMemoryDbContext();
        var voucherRepo = new VoucherRepository(context);
        var uow = new UnitOfWork(context);

        // Setup base data
        var company = new Company { CompanyName = "Test Co", FinancialYearFrom = new DateTime(2026, 4, 1), BooksBeginningFrom = new DateTime(2026, 4, 1) };
        await context.Companies.AddAsync(company);

        var fy = new FinancialYear { Company = company, YearName = "2026-27", StartDate = new DateTime(2026, 4, 1), EndDate = new DateTime(2027, 3, 31) };
        await context.FinancialYears.AddAsync(fy);

        var group = new Group { Company = company, GroupName = "Cash-in-Hand", Nature = GroupNature.Assets };
        await context.Groups.AddAsync(group);

        var ledger1 = new Ledger { Company = company, Group = group, LedgerName = "Cash A/c" };
        var ledger2 = new Ledger { Company = company, Group = group, LedgerName = "Food Expense" };
        await context.Ledgers.AddRangeAsync(ledger1, ledger2);

        var voucherType = new VoucherType { Name = "Payment", Code = "PMT", Type = VoucherTypeEnum.Payment, Prefix = "PAY-" };
        await context.VoucherTypes.AddAsync(voucherType);
        await uow.SaveChangesAsync();

        // Create Voucher with 2 entries
        var voucher = new Voucher
        {
            CompanyId = company.CompanyId,
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = voucherType.VoucherTypeId,
            VoucherNumber = "PAY-00001",
            VoucherDate = new DateTime(2026, 9, 10),
            Narration = "Office refreshments payment"
        };

        voucher.VoucherEntries.Add(new VoucherEntry { LedgerId = ledger2.LedgerId, Debit = 500m, Credit = 0m, Narration = "Food" });
        voucher.VoucherEntries.Add(new VoucherEntry { LedgerId = ledger1.LedgerId, Debit = 0m, Credit = 500m, Narration = "Cash paid" });

        await voucherRepo.AddAsync(voucher);
        await uow.SaveChangesAsync();

        // Verify voucher and entries exist
        var savedVoucher = await voucherRepo.GetVoucherWithEntriesAsync(voucher.VoucherId);
        savedVoucher.Should().NotBeNull();
        savedVoucher!.VoucherEntries.Should().HaveCount(2);
        savedVoucher.VoucherEntries.Sum(e => e.Debit).Should().Be(500m);
        savedVoucher.VoucherEntries.Sum(e => e.Credit).Should().Be(500m);

        // Test cascade deletion: deleting voucher should delete all entries
        voucherRepo.Delete(savedVoucher);
        await uow.SaveChangesAsync();

        context.Vouchers.Any(v => v.VoucherId == voucher.VoucherId).Should().BeFalse();
        context.VoucherEntries.Any(e => e.VoucherId == voucher.VoucherId).Should().BeFalse();
        // Ledgers must remain intact
        context.Ledgers.Any(l => l.LedgerId == ledger1.LedgerId).Should().BeTrue();
    }

    [Fact]
    public async Task NextVoucherNumber_Should_Generate_Sequential_Numbers()
    {
        using var context = CreateInMemoryDbContext();
        var voucherRepo = new VoucherRepository(context);
        var uow = new UnitOfWork(context);

        var voucherType = new VoucherType { Name = "Receipt", Code = "RCT", Type = VoucherTypeEnum.Receipt, Prefix = "REC-" };
        await context.VoucherTypes.AddAsync(voucherType);
        await uow.SaveChangesAsync();

        string nextNo1 = await voucherRepo.GetNextVoucherNumberAsync(companyId: 1, voucherType.VoucherTypeId, financialYearId: 1);
        nextNo1.Should().Be("REC-00001");
    }
}
