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
using Xunit;

namespace MoneyFlow.Tests.AccountingTests;

public class Phase24BankReconciliationTests
{
    private (AppDbContext Context, AccountingService AccService, BankReconciliationService BrsService, int CompanyId, int FyId, int BankLedgerId, int VendorId, int CustomerId) CreateTestSetup()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options);
        var uow = new UnitOfWork(context);
        var voucherRepo = new VoucherRepository(context);
        var ledgerRepo = new LedgerRepository(context);
        var fyRepo = new FinancialYearRepository(context);
        var compRepo = new CompanyRepository(context);
        var compContext = new CompanyContext();

        var accService = new AccountingService(
            context,
            voucherRepo,
            ledgerRepo,
            fyRepo,
            compRepo,
            uow,
            NullLogger<AccountingService>.Instance);

        var brsService = new BankReconciliationService(context, NullLogger<BankReconciliationService>.Instance);

        var company = new Company { CompanyName = "Metro Traders", Currency = "₹" };
        context.Companies.Add(company);
        context.SaveChanges();

        var fy = new FinancialYear
        {
            CompanyId = company.CompanyId,
            YearName = "2026-27",
            StartDate = new DateTime(2026, 4, 1),
            EndDate = new DateTime(2027, 3, 31)
        };
        context.FinancialYears.Add(fy);

        var vtPayment = new VoucherType { Name = "Payment", Code = "PMT", Type = VoucherTypeEnum.Payment };
        var vtReceipt = new VoucherType { Name = "Receipt", Code = "RCP", Type = VoucherTypeEnum.Receipt };
        context.VoucherTypes.AddRange(vtPayment, vtReceipt);

        var grpBank = new Group { CompanyId = company.CompanyId, GroupName = "Bank Accounts", Nature = GroupNature.Assets, PrimaryGroup = true };
        var grpCreditors = new Group { CompanyId = company.CompanyId, GroupName = "Sundry Creditors", Nature = GroupNature.Liabilities, PrimaryGroup = true };
        var grpDebtors = new Group { CompanyId = company.CompanyId, GroupName = "Sundry Debtors", Nature = GroupNature.Assets, PrimaryGroup = true };
        context.Groups.AddRange(grpBank, grpCreditors, grpDebtors);
        context.SaveChanges();

        // Bank with ₹1,00,000 opening debit balance
        var lBank = new Ledger
        {
            CompanyId = company.CompanyId,
            GroupId = grpBank.GroupId,
            LedgerName = "HDFC Bank A/c",
            OpeningBalance = 100000m,
            OpeningBalanceType = BalanceType.Debit
        };
        var lVendor = new Ledger { CompanyId = company.CompanyId, GroupId = grpCreditors.GroupId, LedgerName = "Modern Suppliers" };
        var lCustomer = new Ledger { CompanyId = company.CompanyId, GroupId = grpDebtors.GroupId, LedgerName = "Royal Stores" };
        context.Ledgers.AddRange(lBank, lVendor, lCustomer);
        context.SaveChanges();

        compContext.SetActiveCompany(company, fy);

        return (context, accService, brsService, company.CompanyId, fy.FinancialYearId, lBank.LedgerId, lVendor.LedgerId, lCustomer.LedgerId);
    }

    [Fact]
    public async Task BankReconciliation_Should_Compute_Balances_And_Handle_Clearance()
    {
        var (context, accService, brsService, companyId, fyId, bankId, vendorId, customerId) = CreateTestSetup();
        var vtPayment = await context.VoucherTypes.FirstAsync(v => v.Type == VoucherTypeEnum.Payment);
        var vtReceipt = await context.VoucherTypes.FirstAsync(v => v.Type == VoucherTypeEnum.Receipt);

        // 1. Issue Cheque to Vendor for ₹20,000 (Payment) on 10-Apr-2026 (No BankDate)
        var paymentDto = new VoucherCreateDto
        {
            FinancialYearId = fyId,
            VoucherTypeId = vtPayment.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 10),
            Narration = "Chq #1001 issued to Modern Suppliers",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = vendorId, Debit = 20000m, Credit = 0m },
                new() { LedgerId = bankId, Debit = 0m, Credit = 20000m, InstrumentNumber = "1001" }
            }
        };
        await accService.SaveVoucherAsync(companyId, paymentDto);

        // 2. Deposit Cheque from Customer for ₹35,000 (Receipt) on 12-Apr-2026 (No BankDate)
        var receiptDto = new VoucherCreateDto
        {
            FinancialYearId = fyId,
            VoucherTypeId = vtReceipt.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 12),
            Narration = "Chq #5502 received from Royal Stores",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = bankId, Debit = 35000m, Credit = 0m, InstrumentNumber = "5502" },
                new() { LedgerId = customerId, Debit = 0m, Credit = 35000m }
            }
        };
        await accService.SaveVoucherAsync(companyId, receiptDto);

        // 3. Query BRS as on 30-Apr-2026
        // Book Balance = 100,000 + 35,000 - 20,000 = ₹1,15,000
        // Cheques issued not presented = ₹20,000
        // Cheques deposited not cleared = ₹35,000
        // Balance as per Bank = 1,15,000 + 20,000 - 35,000 = ₹1,00,000
        var brs1 = await brsService.GetBankReconciliationDataAsync(
            companyId,
            bankId,
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 30));

        brs1.BalanceAsPerCompanyBooks.Should().Be(115000m);
        brs1.ChequesIssuedNotPresented.Should().Be(20000m);
        brs1.ChequesDepositedNotCleared.Should().Be(35000m);
        brs1.BalanceAsPerBank.Should().Be(100000m);
        brs1.Transactions.Should().HaveCount(2);

        // 4. Clear the Receipt Cheque in Bank on 14-Apr-2026
        var receiptBankEntry = brs1.Transactions.First(t => t.Debit == 35000m);
        var update = new List<BankClearanceUpdateDto>
        {
            new() { VoucherEntryId = receiptBankEntry.VoucherEntryId, BankDate = new DateTime(2026, 4, 14), InstrumentNumber = "5502" }
        };
        await brsService.UpdateBankClearanceAsync(companyId, update);

        // 5. Query BRS again
        // Cheques deposited not cleared should now be ₹0 (cleared!)
        // Balance as per Bank = 1,15,000 + 20,000 - 0 = ₹1,35,000!
        var brs2 = await brsService.GetBankReconciliationDataAsync(
            companyId,
            bankId,
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 30));

        brs2.ChequesDepositedNotCleared.Should().Be(0m);
        brs2.ChequesIssuedNotPresented.Should().Be(20000m);
        brs2.BalanceAsPerBank.Should().Be(135000m);
    }
}
