using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;
using MoneyFlow.Data.Repositories;
using MoneyFlow.Services.Accounting;
using MoneyFlow.Services.Company;
using Xunit;
using GroupEntity = MoneyFlow.Core.Entities.Group;
using LedgerEntity = MoneyFlow.Core.Entities.Ledger;
using FinancialYearEntity = MoneyFlow.Core.Entities.FinancialYear;

namespace MoneyFlow.Tests.SecurityTests;

public class Phase25CompanySplitAndSecurityTests
{
    private (AppDbContext Context, CompanyContext CompContext, CompanyService CompService, CompanySplitService SplitService) CreateSetup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var unitOfWork = new UnitOfWork(context);
        var compContext = new CompanyContext();
        var companyRepo = new CompanyRepository(context);
        var fyRepo = new FinancialYearRepository(context);
        var groupRepo = new GroupRepository(context);
        var ledgerRepo = new LedgerRepository(context);

        var compService = new CompanyService(companyRepo, fyRepo, groupRepo, ledgerRepo, unitOfWork, compContext, NullLogger<CompanyService>.Instance);
        var billService = new BillAllocationService(context, NullLogger<BillAllocationService>.Instance);
        var splitService = new CompanySplitService(context, billService, compContext, unitOfWork, NullLogger<CompanySplitService>.Instance);

        return (context, compContext, compService, splitService);
    }

    [Fact]
    public async Task CompanyService_WithVaultPassword_HashesPasswordAndVerifiesSuccessfully()
    {
        // Arrange
        var (context, _, compService, _) = CreateSetup();
        var createDto = new CompanyCreateDto
        {
            CompanyName = "Secure Traders Ltd",
            CompanyNumber = "010050",
            DataDirectory = Path.Combine(Path.GetTempPath(), "MoneyFlowTests", "Data"),
            FinancialYearFrom = new DateTime(2025, 4, 1),
            BooksBeginningFrom = new DateTime(2025, 4, 1),
            VaultPassword = "VaultPass#2026",
            AutoBackupOnExit = true,
            CreateDefaultLedgers = false
        };

        // Act
        var company = await compService.CreateCompanyAsync(createDto);

        // Assert
        company.Should().NotBeNull();
        company.IsPasswordProtected.Should().BeTrue();
        company.AutoBackupOnExit.Should().BeTrue();

        var dbCompany = await context.Companies.FindAsync(company.CompanyId);
        dbCompany.Should().NotBeNull();
        dbCompany!.PasswordHash.Should().NotBeNullOrEmpty();
        dbCompany.PasswordSalt.Should().NotBeNullOrEmpty();
        dbCompany.PasswordHash.Should().NotBe("VaultPass#2026"); // Must be PBKDF2 hashed

        // Verify password
        bool correctValid = await compService.VerifyCompanyPasswordAsync(company.CompanyId, "VaultPass#2026");
        correctValid.Should().BeTrue();

        bool wrongValid = await compService.VerifyCompanyPasswordAsync(company.CompanyId, "WrongPass123");
        wrongValid.Should().BeFalse();
    }

    [Fact]
    public async Task CompanyService_WithoutVaultPassword_NotProtected()
    {
        // Arrange
        var (context, _, compService, _) = CreateSetup();
        var createDto = new CompanyCreateDto
        {
            CompanyName = "Open Bookkeeping Corp",
            CompanyNumber = "010051",
            DataDirectory = Path.Combine(Path.GetTempPath(), "MoneyFlowTests", "Data"),
            FinancialYearFrom = new DateTime(2025, 4, 1),
            BooksBeginningFrom = new DateTime(2025, 4, 1),
            VaultPassword = null,
            AutoBackupOnExit = false,
            CreateDefaultLedgers = false
        };

        // Act
        var company = await compService.CreateCompanyAsync(createDto);

        // Assert
        company.IsPasswordProtected.Should().BeFalse();
        company.AutoBackupOnExit.Should().BeFalse();

        bool verified = await compService.VerifyCompanyPasswordAsync(company.CompanyId, "AnyPassword");
        verified.Should().BeTrue();
    }

    [Fact]
    public async Task CompanyService_UpdateCompany_UpdatesPasswordAndBackupSettings()
    {
        // Arrange
        var (context, _, compService, _) = CreateSetup();
        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Alpha Logistics",
            CompanyNumber = "010052",
            DataDirectory = Path.Combine(Path.GetTempPath(), "MoneyFlowTests", "Data"),
            FinancialYearFrom = new DateTime(2025, 4, 1),
            BooksBeginningFrom = new DateTime(2025, 4, 1),
            VaultPassword = "OldPassword1",
            AutoBackupOnExit = false
        });

        // Act - Update password and enable auto backup
        await compService.UpdateCompanyAsync(new CompanyUpdateDto
        {
            CompanyId = company.CompanyId,
            CompanyName = "Alpha Logistics Updated",
            CompanyNumber = company.CompanyNumber,
            DataDirectory = company.DataDirectory,
            Currency = "₹",
            NewVaultPassword = "NewSecretPassword99",
            AutoBackupOnExit = true,
            IsActive = true
        });

        // Assert
        bool oldValid = await compService.VerifyCompanyPasswordAsync(company.CompanyId, "OldPassword1");
        oldValid.Should().BeFalse();

        bool newValid = await compService.VerifyCompanyPasswordAsync(company.CompanyId, "NewSecretPassword99");
        newValid.Should().BeTrue();

        var updated = await compService.GetCompanyByIdAsync(company.CompanyId);
        updated!.AutoBackupOnExit.Should().BeTrue();
    }

    [Fact]
    public async Task CompanySplitService_SplitCompany_CarriesForwardBalancesAndPendingBills()
    {
        // Arrange
        var (context, _, compService, splitService) = CreateSetup();
        string testDataRoot = Path.Combine(Path.GetTempPath(), "MoneyFlowTests", "SplitTest_" + Guid.NewGuid().ToString("N"));

        var srcCompany = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Mahavir Enterprises",
            CompanyNumber = "010001",
            DataDirectory = testDataRoot,
            FinancialYearFrom = new DateTime(2025, 4, 1),
            BooksBeginningFrom = new DateTime(2025, 4, 1),
            CreateDefaultLedgers = false,
            AutoBackupOnExit = true
        });

        var fy = new FinancialYearEntity
        {
            CompanyId = srcCompany.CompanyId,
            YearName = "2025-26",
            StartDate = new DateTime(2025, 4, 1),
            EndDate = new DateTime(2026, 3, 31),
            IsClosed = false
        };
        context.FinancialYears.Add(fy);
        await context.SaveChangesAsync();

        // Create Groups: Capital, Current Assets, Current Liabilities, Revenue
        var grpCapital = new GroupEntity { CompanyId = srcCompany.CompanyId, GroupName = "Capital Account", Nature = GroupNature.Liabilities, PrimaryGroup = true };
        var grpSundryDebtors = new GroupEntity { CompanyId = srcCompany.CompanyId, GroupName = "Sundry Debtors", Nature = GroupNature.Assets, PrimaryGroup = true };
        var grpSundryCreditors = new GroupEntity { CompanyId = srcCompany.CompanyId, GroupName = "Sundry Creditors", Nature = GroupNature.Liabilities, PrimaryGroup = true };
        var grpBank = new GroupEntity { CompanyId = srcCompany.CompanyId, GroupName = "Bank Accounts", Nature = GroupNature.Assets, PrimaryGroup = true };
        var grpSales = new GroupEntity { CompanyId = srcCompany.CompanyId, GroupName = "Sales Accounts", Nature = GroupNature.Income, AffectProfitLoss = true, PrimaryGroup = true };

        context.Groups.AddRange(grpCapital, grpSundryDebtors, grpSundryCreditors, grpBank, grpSales);
        await context.SaveChangesAsync();

        // Create Ledgers
        var ledCapital = new LedgerEntity { CompanyId = srcCompany.CompanyId, GroupId = grpCapital.GroupId, LedgerName = "Owner Capital", OpeningBalance = 50000m, OpeningBalanceType = BalanceType.Credit };
        var ledBank = new LedgerEntity { CompanyId = srcCompany.CompanyId, GroupId = grpBank.GroupId, LedgerName = "HDFC Bank", OpeningBalance = 40000m, OpeningBalanceType = BalanceType.Debit };
        var ledDebtor = new LedgerEntity { CompanyId = srcCompany.CompanyId, GroupId = grpSundryDebtors.GroupId, LedgerName = "Customer Ramesh", OpeningBalance = 0m, OpeningBalanceType = BalanceType.Debit };
        var ledCreditor = new LedgerEntity { CompanyId = srcCompany.CompanyId, GroupId = grpSundryCreditors.GroupId, LedgerName = "Supplier Suresh", OpeningBalance = 0m, OpeningBalanceType = BalanceType.Credit };
        var ledSales = new LedgerEntity { CompanyId = srcCompany.CompanyId, GroupId = grpSales.GroupId, LedgerName = "Pure Bookkeeping Sales", OpeningBalance = 0m, OpeningBalanceType = BalanceType.Credit };

        context.Ledgers.AddRange(ledCapital, ledBank, ledDebtor, ledCreditor, ledSales);
        await context.SaveChangesAsync();

        // Add a sales voucher with bill allocation: Ramesh Debited 25000, Sales Credited 25000
        var vSales = new Voucher
        {
            CompanyId = srcCompany.CompanyId,
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = 5,
            VoucherNumber = "SAL-001",
            VoucherDate = new DateTime(2025, 6, 15)
        };
        context.Vouchers.Add(vSales);
        await context.SaveChangesAsync();

        var entry1 = new VoucherEntry { VoucherId = vSales.VoucherId, LedgerId = ledDebtor.LedgerId, Debit = 25000m, Credit = 0m };
        var entry2 = new VoucherEntry { VoucherId = vSales.VoucherId, LedgerId = ledSales.LedgerId, Debit = 0m, Credit = 25000m };
        context.VoucherEntries.AddRange(entry1, entry2);
        await context.SaveChangesAsync();

        var bill1 = new BillAllocation
        {
            CompanyId = srcCompany.CompanyId,
            VoucherEntryId = entry1.VoucherEntryId,
            BillType = BillType.NewRef,
            BillName = "INV-2025-001",
            DueDate = new DateTime(2025, 6, 15),
            Amount = 25000m,
            LedgerId = ledDebtor.LedgerId
        };
        context.BillAllocations.Add(bill1);

        // Ramesh pays 10,000 against INV-2025-001 (Leaving 15,000 unpaid)
        var vRcpt = new Voucher
        {
            CompanyId = srcCompany.CompanyId,
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = 3,
            VoucherNumber = "RCP-001",
            VoucherDate = new DateTime(2025, 8, 10)
        };
        context.Vouchers.Add(vRcpt);
        await context.SaveChangesAsync();

        var entryRcptBank = new VoucherEntry { VoucherId = vRcpt.VoucherId, LedgerId = ledBank.LedgerId, Debit = 10000m, Credit = 0m };
        var entryRcptDebtor = new VoucherEntry { VoucherId = vRcpt.VoucherId, LedgerId = ledDebtor.LedgerId, Debit = 0m, Credit = 10000m };
        context.VoucherEntries.AddRange(entryRcptBank, entryRcptDebtor);
        await context.SaveChangesAsync();

        var billAdj = new BillAllocation
        {
            CompanyId = srcCompany.CompanyId,
            VoucherEntryId = entryRcptDebtor.VoucherEntryId,
            BillType = BillType.AgstRef,
            BillName = "INV-2025-001",
            DueDate = new DateTime(2025, 8, 10),
            Amount = 10000m,
            LedgerId = ledDebtor.LedgerId
        };
        context.BillAllocations.Add(billAdj);
        await context.SaveChangesAsync();

        // Act - Split company on 2026-04-01
        var splitDto = new CompanySplitDto
        {
            SourceCompanyId = srcCompany.CompanyId,
            SplitFromDate = new DateTime(2026, 4, 1),
            NewCompanyName = "Mahavir Enterprises (2026-2027)",
            CarryForwardOpeningBalances = true,
            CarryForwardPendingBills = true
        };

        var splitResult = await splitService.SplitCompanyAsync(splitDto);

        // Assert
        splitResult.Should().NotBeNull();
        splitResult.CompanyName.Should().Be("Mahavir Enterprises (2026-2027)");
        splitResult.FinancialYearFrom.Should().Be(new DateTime(2026, 4, 1));
        splitResult.FinancialYearFrom.AddYears(1).AddDays(-1).Should().Be(new DateTime(2027, 3, 31));

        // Verify folder isolation
        splitResult.DataDirectory.Should().NotBeNullOrEmpty();
        Directory.Exists(splitResult.DataDirectory).Should().BeTrue();

        // Verify carried-forward ledgers in the new company
        var newLedgers = await context.Ledgers.Where(l => l.CompanyId == splitResult.CompanyId).ToListAsync();
        newLedgers.Should().NotBeEmpty();

        // Ramesh debtor ledger in new company: opening balance should be 15,000 Debit (25000 - 10000)
        var newRamesh = newLedgers.FirstOrDefault(l => l.LedgerName == "Customer Ramesh");
        newRamesh.Should().NotBeNull();
        newRamesh!.OpeningBalance.Should().Be(15000m);
        newRamesh.OpeningBalanceType.Should().Be(BalanceType.Debit);

        // HDFC bank: 40000 opening + 10000 receipt = 50,000 Debit
        var newBank = newLedgers.FirstOrDefault(l => l.LedgerName == "HDFC Bank");
        newBank.Should().NotBeNull();
        newBank!.OpeningBalance.Should().Be(50000m);
        newBank.OpeningBalanceType.Should().Be(BalanceType.Debit);

        // Pure Bookkeeping Sales ledger in new company: revenue should be reset to 0 opening balance
        var newSales = newLedgers.FirstOrDefault(l => l.LedgerName == "Pure Bookkeeping Sales");
        newSales.Should().NotBeNull();
        newSales!.OpeningBalance.Should().Be(0m);

        // Capital Account: 50,000 initial + 25,000 net sales profit = 75,000 Credit
        var newCapital = newLedgers.FirstOrDefault(l => l.LedgerName == "Owner Capital");
        newCapital.Should().NotBeNull();
        newCapital!.OpeningBalance.Should().Be(75000m);
        newCapital.OpeningBalanceType.Should().Be(BalanceType.Credit);

        // Carried-forward pending bills check:
        // Customer Ramesh should have pending bill INV-2025-001 with remaining unpaid amount = 15,000
        var newBillAllocations = await context.BillAllocations
            .Where(b => b.CompanyId == splitResult.CompanyId)
            .ToListAsync();

        newBillAllocations.Should().NotBeEmpty();
        var pendingBill = newBillAllocations.FirstOrDefault(b => b.BillName == "INV-2025-001");
        pendingBill.Should().NotBeNull();
        pendingBill!.Amount.Should().Be(15000m);
        pendingBill.BillType.Should().Be(BillType.NewRef);

        // Clean up temp test directory
        try
        {
            if (Directory.Exists(testDataRoot))
            {
                Directory.Delete(testDataRoot, true);
            }
        }
        catch { }
    }
}
