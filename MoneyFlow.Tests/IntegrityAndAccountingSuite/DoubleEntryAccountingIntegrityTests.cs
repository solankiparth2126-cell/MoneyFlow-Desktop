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

namespace MoneyFlow.Tests.IntegrityAndAccountingSuite;

public class DoubleEntryAccountingIntegrityTests
{
    private (
        AppDbContext Context,
        AccountingService AccountingSvc,
        CompanyService CompanySvc,
        LedgerService LedgerSvc
    ) CreateTestSetup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var unitOfWork = new UnitOfWork(context);
        var voucherRepo = new VoucherRepository(context);
        var ledgerRepo = new LedgerRepository(context);
        var fyRepo = new FinancialYearRepository(context);
        var companyRepo = new CompanyRepository(context);
        var groupRepo = new GroupRepository(context);

        var companyContext = new CompanyContext();

        var accountingService = new AccountingService(
            context,
            voucherRepo,
            ledgerRepo,
            fyRepo,
            companyRepo,
            unitOfWork,
            NullLogger<AccountingService>.Instance);

        var companyService = new CompanyService(
            companyRepo,
            fyRepo,
            groupRepo,
            ledgerRepo,
            unitOfWork,
            companyContext,
            NullLogger<CompanyService>.Instance);

        var ledgerService = new LedgerService(
            context,
            ledgerRepo,
            groupRepo,
            companyRepo,
            unitOfWork,
            NullLogger<LedgerService>.Instance);

        return (context, accountingService, companyService, ledgerService);
    }

    [Fact]
    public async Task MasterPrompt_Section52_UnbalancedVoucher_IsRejected_AndNotSaved()
    {
        // =========================================================================
        // Master Prompt Section 52:
        // Unbalanced:
        // Cash Dr ₹1,000
        // Expected: Voucher rejected
        // =========================================================================

        // Arrange
        var (context, accountingSvc, companySvc, ledgerSvc) = CreateTestSetup();
        var company = await companySvc.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Unbalanced Test Co", CreateDefaultLedgers = false });
        var fy = await context.FinancialYears.FirstAsync(f => f.CompanyId == company.CompanyId);

        var grp = new Group { CompanyId = company.CompanyId, GroupName = "Cash-in-Hand", Nature = GroupNature.Assets };
        context.Groups.Add(grp);
        await context.SaveChangesAsync();

        var cashLedger = await ledgerSvc.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { LedgerName = "Cash Account", GroupId = grp.GroupId });
        var paymentType = new VoucherType { Name = "Payment", Code = "PMT", Type = VoucherTypeEnum.Payment, Prefix = "PAY-" };
        context.VoucherTypes.Add(paymentType);
        await context.SaveChangesAsync();

        // Act: Create an unbalanced voucher (Debit = 1,000, Credit = 0)
        var unbalancedVoucher = new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = paymentType.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(10),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = cashLedger.LedgerId, Debit = 1000m, Credit = 0m }
            }
        };

        // Assert: Must be rejected
        var act = async () => await accountingSvc.SaveVoucherAsync(company.CompanyId, unbalancedVoucher);
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert: Database must have zero vouchers
        var voucherCount = await context.Vouchers.CountAsync(v => v.CompanyId == company.CompanyId);
        voucherCount.Should().Be(0);
    }

    [Fact]
    public async Task MasterPrompt_Section52_BalancedVoucher_Succeeds_AndReconciles()
    {
        // =========================================================================
        // Master Prompt Section 52:
        // Cash Dr ₹1,000
        //     To Income ₹1,000
        // Expected: Debit = ₹1,000, Credit = ₹1,000, Difference = ₹0
        // =========================================================================

        // Arrange
        var (context, accountingSvc, companySvc, ledgerSvc) = CreateTestSetup();
        var company = await companySvc.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Balanced Test Co", CreateDefaultLedgers = false });
        var fy = await context.FinancialYears.FirstAsync(f => f.CompanyId == company.CompanyId);

        var grpCash = new Group { CompanyId = company.CompanyId, GroupName = "Cash-in-Hand", Nature = GroupNature.Assets };
        var grpIncome = new Group { CompanyId = company.CompanyId, GroupName = "Direct Income", Nature = GroupNature.Income };
        context.Groups.AddRange(grpCash, grpIncome);
        await context.SaveChangesAsync();

        var cash = await ledgerSvc.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { LedgerName = "Cash", GroupId = grpCash.GroupId });
        var income = await ledgerSvc.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto { LedgerName = "Consulting Income", GroupId = grpIncome.GroupId });

        var receiptType = new VoucherType { Name = "Receipt", Code = "RCT", Type = VoucherTypeEnum.Receipt, Prefix = "RCT-" };
        context.VoucherTypes.Add(receiptType);
        await context.SaveChangesAsync();

        var voucherDto = new VoucherCreateDto
        {
            FinancialYearId = fy.FinancialYearId,
            VoucherTypeId = receiptType.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(5),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = cash.LedgerId, Debit = 1000m, Credit = 0m },
                new() { LedgerId = income.LedgerId, Debit = 0m, Credit = 1000m }
            }
        };

        // Act
        var validation = accountingSvc.ValidateVoucher(voucherDto, fy.StartDate, fy.EndDate);

        // Assert validation
        validation.IsValid.Should().BeTrue();
        validation.TotalDebit.Should().Be(1000m);
        validation.TotalCredit.Should().Be(1000m);
        validation.Difference.Should().Be(0m);

        // Act: Save
        var savedVoucher = await accountingSvc.SaveVoucherAsync(company.CompanyId, voucherDto);
        savedVoucher.Should().NotBeNull();

        // Assert ledger balances
        var cashBalance = await accountingSvc.GetLedgerBalanceAsync(company.CompanyId, cash.LedgerId);
        cashBalance.ClosingBalance.Should().Be(1000m);
        cashBalance.ClosingType.Should().Be(BalanceType.Debit);

        var incomeBalance = await accountingSvc.GetLedgerBalanceAsync(company.CompanyId, income.LedgerId);
        incomeBalance.ClosingBalance.Should().Be(1000m);
        incomeBalance.ClosingType.Should().Be(BalanceType.Credit);
    }

    [Fact]
    public async Task MasterPrompt_FullAccountingLifecycle_ReconcilesTrialBalanceToZeroDifference()
    {
        // Arrange
        var (context, accountingSvc, companySvc, ledgerSvc) = CreateTestSetup();
        var company = await companySvc.CreateCompanyAsync(new CompanyCreateDto { CompanyName = "Lifecycle ERP Corp", CreateDefaultLedgers = false });
        int compId = company.CompanyId;
        var fy = await context.FinancialYears.FirstAsync(f => f.CompanyId == compId);
        int fyId = fy.FinancialYearId;

        // Setup Groups
        var grpCap = new Group { CompanyId = compId, GroupName = "Capital Account", Nature = GroupNature.Liabilities };
        var grpCash = new Group { CompanyId = compId, GroupName = "Cash-in-Hand", Nature = GroupNature.Assets };
        var grpBank = new Group { CompanyId = compId, GroupName = "Bank Accounts", Nature = GroupNature.Assets };
        var grpCreditors = new Group { CompanyId = compId, GroupName = "Sundry Creditors", Nature = GroupNature.Liabilities };
        var grpDebtors = new Group { CompanyId = compId, GroupName = "Sundry Debtors", Nature = GroupNature.Assets };
        var grpPur = new Group { CompanyId = compId, GroupName = "Purchase Accounts", Nature = GroupNature.Expenses };
        var grpSal = new Group { CompanyId = compId, GroupName = "Sales Accounts", Nature = GroupNature.Income };
        var grpFixed = new Group { CompanyId = compId, GroupName = "Fixed Assets", Nature = GroupNature.Assets };
        var grpExp = new Group { CompanyId = compId, GroupName = "Indirect Expenses", Nature = GroupNature.Expenses };

        context.Groups.AddRange(grpCap, grpCash, grpBank, grpCreditors, grpDebtors, grpPur, grpSal, grpFixed, grpExp);
        await context.SaveChangesAsync();

        // Setup Ledgers with balanced opening setup
        var ledCapital = await ledgerSvc.CreateLedgerAsync(compId, new LedgerCreateDto { LedgerName = "Owner Capital", GroupId = grpCap.GroupId, OpeningBalance = 500000m, OpeningBalanceType = BalanceType.Credit });
        var ledCash = await ledgerSvc.CreateLedgerAsync(compId, new LedgerCreateDto { LedgerName = "Cash", GroupId = grpCash.GroupId, OpeningBalance = 300000m, OpeningBalanceType = BalanceType.Debit });
        var ledEquipment = await ledgerSvc.CreateLedgerAsync(compId, new LedgerCreateDto { LedgerName = "Office Equipment", GroupId = grpFixed.GroupId, OpeningBalance = 200000m, OpeningBalanceType = BalanceType.Debit });

        var ledBank = await ledgerSvc.CreateLedgerAsync(compId, new LedgerCreateDto { LedgerName = "State Bank of India", GroupId = grpBank.GroupId });
        var ledSupplier = await ledgerSvc.CreateLedgerAsync(compId, new LedgerCreateDto { LedgerName = "Apex Supplies Ltd", GroupId = grpCreditors.GroupId });
        var ledCustomer = await ledgerSvc.CreateLedgerAsync(compId, new LedgerCreateDto { LedgerName = "Global Traders", GroupId = grpDebtors.GroupId });
        var ledPurchase = await ledgerSvc.CreateLedgerAsync(compId, new LedgerCreateDto { LedgerName = "General Purchases", GroupId = grpPur.GroupId });
        var ledSales = await ledgerSvc.CreateLedgerAsync(compId, new LedgerCreateDto { LedgerName = "General Sales", GroupId = grpSal.GroupId });
        var ledDeprec = await ledgerSvc.CreateLedgerAsync(compId, new LedgerCreateDto { LedgerName = "Depreciation Expense", GroupId = grpExp.GroupId });

        // Types
        var vtContra = new VoucherType { Name = "Contra", Code = "CNT", Type = VoucherTypeEnum.Contra, Prefix = "CNT-" };
        var vtPurchase = new VoucherType { Name = "Purchase", Code = "PUR", Type = VoucherTypeEnum.Purchase, Prefix = "PUR-" };
        var vtPayment = new VoucherType { Name = "Payment", Code = "PMT", Type = VoucherTypeEnum.Payment, Prefix = "PAY-" };
        var vtSales = new VoucherType { Name = "Sales", Code = "SAL", Type = VoucherTypeEnum.Sales, Prefix = "SAL-" };
        var vtReceipt = new VoucherType { Name = "Receipt", Code = "RCT", Type = VoucherTypeEnum.Receipt, Prefix = "RCT-" };
        var vtJournal = new VoucherType { Name = "Journal", Code = "JRN", Type = VoucherTypeEnum.Journal, Prefix = "JRN-" };
        var vtDebitNote = new VoucherType { Name = "DebitNote", Code = "DBN", Type = VoucherTypeEnum.DebitNote, Prefix = "DBN-" };
        var vtCreditNote = new VoucherType { Name = "CreditNote", Code = "CRN", Type = VoucherTypeEnum.CreditNote, Prefix = "CRN-" };

        context.VoucherTypes.AddRange(vtContra, vtPurchase, vtPayment, vtSales, vtReceipt, vtJournal, vtDebitNote, vtCreditNote);
        await context.SaveChangesAsync();

        // 1. Contra: Deposit 100,000 cash into Bank
        await accountingSvc.SaveVoucherAsync(compId, new VoucherCreateDto
        {
            FinancialYearId = fyId,
            VoucherTypeId = vtContra.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(2),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledBank.LedgerId, Debit = 100000m, Credit = 0m },
                new() { LedgerId = ledCash.LedgerId, Debit = 0m, Credit = 100000m }
            }
        });

        // 2. Purchase: Buy 80,000 from Supplier
        await accountingSvc.SaveVoucherAsync(compId, new VoucherCreateDto
        {
            FinancialYearId = fyId,
            VoucherTypeId = vtPurchase.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(5),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledPurchase.LedgerId, Debit = 80000m, Credit = 0m },
                new() { LedgerId = ledSupplier.LedgerId, Debit = 0m, Credit = 80000m }
            }
        });

        // 3. Payment: Pay supplier 50,000 via Bank
        await accountingSvc.SaveVoucherAsync(compId, new VoucherCreateDto
        {
            FinancialYearId = fyId,
            VoucherTypeId = vtPayment.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(8),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledSupplier.LedgerId, Debit = 50000m, Credit = 0m },
                new() { LedgerId = ledBank.LedgerId, Debit = 0m, Credit = 50000m }
            }
        });

        // 4. Sales: Sell 120,000 to Customer
        await accountingSvc.SaveVoucherAsync(compId, new VoucherCreateDto
        {
            FinancialYearId = fyId,
            VoucherTypeId = vtSales.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(12),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledCustomer.LedgerId, Debit = 120000m, Credit = 0m },
                new() { LedgerId = ledSales.LedgerId, Debit = 0m, Credit = 120000m }
            }
        });

        // 5. Receipt: Receive 90,000 from Customer into Bank
        await accountingSvc.SaveVoucherAsync(compId, new VoucherCreateDto
        {
            FinancialYearId = fyId,
            VoucherTypeId = vtReceipt.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(15),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledBank.LedgerId, Debit = 90000m, Credit = 0m },
                new() { LedgerId = ledCustomer.LedgerId, Debit = 0m, Credit = 90000m }
            }
        });

        // 6. Debit Note: Return 5,000 defective goods to Supplier
        await accountingSvc.SaveVoucherAsync(compId, new VoucherCreateDto
        {
            FinancialYearId = fyId,
            VoucherTypeId = vtDebitNote.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(18),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledSupplier.LedgerId, Debit = 5000m, Credit = 0m },
                new() { LedgerId = ledPurchase.LedgerId, Debit = 0m, Credit = 5000m }
            }
        });

        // 7. Credit Note: Customer returns 3,000 goods
        await accountingSvc.SaveVoucherAsync(compId, new VoucherCreateDto
        {
            FinancialYearId = fyId,
            VoucherTypeId = vtCreditNote.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(20),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledSales.LedgerId, Debit = 3000m, Credit = 0m },
                new() { LedgerId = ledCustomer.LedgerId, Debit = 0m, Credit = 3000m }
            }
        });

        // 8. Journal: Record 10,000 depreciation on Equipment
        await accountingSvc.SaveVoucherAsync(compId, new VoucherCreateDto
        {
            FinancialYearId = fyId,
            VoucherTypeId = vtJournal.VoucherTypeId,
            VoucherDate = fy.StartDate.AddDays(25),
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = ledDeprec.LedgerId, Debit = 10000m, Credit = 0m },
                new() { LedgerId = ledEquipment.LedgerId, Debit = 0m, Credit = 10000m }
            }
        });

        // Act: Generate Trial Balance
        var trialBalance = await accountingSvc.GetTrialBalanceAsync(compId, fy.StartDate, fy.EndDate);

        // Assert: Trial Balance mathematical reconciliation
        trialBalance.Should().NotBeNull();
        trialBalance.IsBalanced.Should().BeTrue();
        trialBalance.Difference.Should().Be(0m);
        trialBalance.TotalPeriodDebit.Should().Be(trialBalance.TotalPeriodCredit);
        trialBalance.TotalClosingDebit.Should().Be(trialBalance.TotalClosingCredit);
        trialBalance.TotalClosingDebit.Should().BeGreaterThan(0m);
    }
}
