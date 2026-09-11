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

public class Phase22CashBankBookTests
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
    public async Task GetCashBook_Should_Calculate_Opening_Receipts_Payments_And_RunningBalance()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Cash Trading Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var salesGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sales Accounts");
        var debtorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Debtors");
        var expenseGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Indirect Expenses");

        var salesType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Sales);
        var receiptType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Receipt);
        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        // Cash account with Opening Balance ₹10,000 Dr
        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Main Cash",
            OpeningBalance = 10000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var sales = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = salesGroup.GroupId,
            LedgerName = "Direct Cash Sales"
        });

        var debtor = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = debtorGroup.GroupId,
            LedgerName = "John Customer"
        });

        var rent = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = expenseGroup.GroupId,
            LedgerName = "Office Rent"
        });

        // 1. Transaction in April (prior to May report): Sales for ₹5,000 paid in cash on 2026-04-15
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = salesType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 15),
            Narration = "April cash sale",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = cash.LedgerId, Debit = 5000m, Credit = 0m },
                new() { LedgerId = sales.LedgerId, Debit = 0m, Credit = 5000m }
            }
        });

        // 2. Transactions in May:
        // May 05: Receipt from John Customer ₹2,500
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = receiptType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 5),
            ReferenceNumber = "REC-001",
            Narration = "Received from customer",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = cash.LedgerId, Debit = 2500m, Credit = 0m },
                new() { LedgerId = debtor.LedgerId, Debit = 0m, Credit = 2500m }
            }
        });

        // May 10: Payment for Office Rent ₹4,000
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 10),
            ReferenceNumber = "PMT-001",
            Narration = "Paid May office rent",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = rent.LedgerId, Debit = 4000m, Credit = 0m },
                new() { LedgerId = cash.LedgerId, Debit = 0m, Credit = 4000m }
            }
        });

        // Execute: Query Cash Book for May (2026-05-01 to 2026-05-31)
        var report = await acctService.GetCashBankBookAsync(
            company.CompanyId,
            cash.LedgerId,
            CashBankBookType.CashBook,
            new DateTime(2026, 5, 1),
            new DateTime(2026, 5, 31));

        // Assert
        report.Should().NotBeNull();
        report.BookType.Should().Be(CashBankBookType.CashBook);
        report.SelectedLedgerName.Should().Be("Main Cash");

        // Dynamic opening balance: Master 10,000 + April 5,000 = 15,000 Dr
        report.OpeningBalance.Should().Be(15000m);
        report.OpeningType.Should().Be(BalanceType.Debit);

        // Lines
        report.Lines.Should().HaveCount(2);

        var line1 = report.Lines[0];
        line1.Date.Should().Be(new DateTime(2026, 5, 5));
        line1.Particulars.Should().Be("John Customer");
        line1.Debit.Should().Be(2500m);
        line1.Credit.Should().Be(0m);
        line1.RunningBalance.Should().Be(17500m);
        line1.RunningType.Should().Be(BalanceType.Debit);

        var line2 = report.Lines[1];
        line2.Date.Should().Be(new DateTime(2026, 5, 10));
        line2.Particulars.Should().Be("Office Rent");
        line2.Debit.Should().Be(0m);
        line2.Credit.Should().Be(4000m);
        line2.RunningBalance.Should().Be(13500m);
        line2.RunningType.Should().Be(BalanceType.Debit);

        // Totals
        report.TotalDebit.Should().Be(2500m);
        report.TotalCredit.Should().Be(4000m);
        report.ClosingBalance.Should().Be(13500m);
        report.ClosingType.Should().Be(BalanceType.Debit);
    }

    [Fact]
    public async Task GetBankBook_Should_Calculate_Deposits_Withdrawals_And_Isolate_Multiple_Banks()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Multi Bank Enterprise",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var creditorGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Sundry Creditors");

        var contraType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Contra);
        var paymentType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Payment);

        var hdfc = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "HDFC Bank A/c",
            OpeningBalance = 50000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var sbi = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "SBI Bank A/c",
            OpeningBalance = 20000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var supplier = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = creditorGroup.GroupId,
            LedgerName = "Mega Supplier Ltd"
        });

        // 1. Contra: Transfer ₹10,000 from SBI to HDFC
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = contraType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 10),
            ReferenceNumber = "NEFT-10023",
            Narration = "Fund transfer SBI to HDFC",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = hdfc.LedgerId, Debit = 10000m, Credit = 0m },
                new() { LedgerId = sbi.LedgerId, Debit = 0m, Credit = 10000m }
            }
        });

        // 2. Payment: ₹15,000 paid to supplier from HDFC
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = paymentType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 15),
            ReferenceNumber = "CHQ-882201",
            Narration = "Supplier invoice clearance",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = supplier.LedgerId, Debit = 15000m, Credit = 0m },
                new() { LedgerId = hdfc.LedgerId, Debit = 0m, Credit = 15000m }
            }
        });

        // Query HDFC Bank Book
        var hdfcReport = await acctService.GetCashBankBookAsync(
            company.CompanyId,
            hdfc.LedgerId,
            CashBankBookType.BankBook,
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 30));

        hdfcReport.OpeningBalance.Should().Be(50000m);
        hdfcReport.TotalDebit.Should().Be(10000m);
        hdfcReport.TotalCredit.Should().Be(15000m);
        hdfcReport.ClosingBalance.Should().Be(45000m); // 50000 + 10000 - 15000
        hdfcReport.ClosingType.Should().Be(BalanceType.Debit);
        hdfcReport.Lines.Should().HaveCount(2);
        hdfcReport.Lines[0].Particulars.Should().Be("SBI Bank A/c");
        hdfcReport.Lines[1].Particulars.Should().Be("Mega Supplier Ltd");

        // Query SBI Bank Book
        var sbiReport = await acctService.GetCashBankBookAsync(
            company.CompanyId,
            sbi.LedgerId,
            CashBankBookType.BankBook,
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 30));

        sbiReport.OpeningBalance.Should().Be(20000m);
        sbiReport.TotalDebit.Should().Be(0m);
        sbiReport.TotalCredit.Should().Be(10000m);
        sbiReport.ClosingBalance.Should().Be(10000m); // 20000 - 10000
        sbiReport.ClosingType.Should().Be(BalanceType.Debit);
        sbiReport.Lines.Should().HaveCount(1);
        sbiReport.Lines[0].Particulars.Should().Be("HDFC Bank A/c");
    }

    [Fact]
    public async Task GetCashBankBook_Consolidated_Should_Aggregate_All_Accounts_And_Provide_Summaries()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Consolidated Banks Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var bankGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Bank Accounts");
        var contraType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Contra);

        var hdfc = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "HDFC Bank",
            OpeningBalance = 30000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var sbi = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = bankGroup.GroupId,
            LedgerName = "SBI Bank",
            OpeningBalance = 40000m,
            OpeningBalanceType = BalanceType.Debit
        });

        // Inter-bank transfer: ₹5,000 from SBI to HDFC
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = contraType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 12),
            ReferenceNumber = "TXN-999",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = hdfc.LedgerId, Debit = 5000m, Credit = 0m },
                new() { LedgerId = sbi.LedgerId, Debit = 0m, Credit = 5000m }
            }
        });

        // Query consolidated (ledgerId: null)
        var report = await acctService.GetCashBankBookAsync(
            company.CompanyId,
            null,
            CashBankBookType.BankBook,
            new DateTime(2026, 4, 1),
            new DateTime(2026, 4, 30));

        report.Should().NotBeNull();
        report.SelectedLedgerName.Should().Be("All Bank Accounts");
        report.OpeningBalance.Should().Be(70000m); // 30000 + 40000

        // Per account summaries
        report.AccountSummaries.Should().HaveCount(2);

        var hdfcSummary = report.AccountSummaries.First(s => s.LedgerId == hdfc.LedgerId);
        hdfcSummary.OpeningBalance.Should().Be(30000m);
        hdfcSummary.TotalDebit.Should().Be(5000m);
        hdfcSummary.TotalCredit.Should().Be(0m);
        hdfcSummary.ClosingBalance.Should().Be(35000m);

        var sbiSummary = report.AccountSummaries.First(s => s.LedgerId == sbi.LedgerId);
        sbiSummary.OpeningBalance.Should().Be(40000m);
        sbiSummary.TotalDebit.Should().Be(0m);
        sbiSummary.TotalCredit.Should().Be(5000m);
        sbiSummary.ClosingBalance.Should().Be(35000m);

        // Overall closing balance
        report.ClosingBalance.Should().Be(70000m);
        report.ClosingType.Should().Be(BalanceType.Debit);
    }

    [Fact]
    public async Task GetCashBankBook_Should_Exclude_Soft_Deleted_Vouchers_And_Respect_Date_Range()
    {
        var (context, compService, ledgService, acctService) = CreateTestSetup();

        var company = await compService.CreateCompanyAsync(new CompanyCreateDto
        {
            CompanyName = "Soft Delete Cash Co",
            FinancialYearFrom = new DateTime(2026, 4, 1),
            CreateDefaultLedgers = false
        });

        var currentFY = await context.FinancialYears.FirstAsync(fy => fy.CompanyId == company.CompanyId);
        var cashGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Cash-in-Hand");
        var incomeGroup = await context.Groups.FirstAsync(g => g.CompanyId == company.CompanyId && g.GroupName == "Direct Income");

        var receiptType = await acctService.GetVoucherTypeByEnumAsync(VoucherTypeEnum.Receipt);

        var cash = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = cashGroup.GroupId,
            LedgerName = "Counter Cash",
            OpeningBalance = 5000m,
            OpeningBalanceType = BalanceType.Debit
        });

        var income = await ledgService.CreateLedgerAsync(company.CompanyId, new LedgerCreateDto
        {
            GroupId = incomeGroup.GroupId,
            LedgerName = "Service Fees"
        });

        // 1. Valid voucher inside date range (May 10)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = receiptType!.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 10),
            Narration = "Valid May receipt",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = cash.LedgerId, Debit = 2000m, Credit = 0m },
                new() { LedgerId = income.LedgerId, Debit = 0m, Credit = 2000m }
            }
        });

        // 2. Voucher inside date range that will be soft-deleted
        var deleteVch = await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = receiptType.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 12),
            Narration = "Deleted receipt",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = cash.LedgerId, Debit = 8000m, Credit = 0m },
                new() { LedgerId = income.LedgerId, Debit = 0m, Credit = 8000m }
            }
        });

        await acctService.DeleteVoucherAsync(deleteVch.VoucherId);

        // 3. Voucher outside date range (June 15)
        await acctService.SaveVoucherAsync(company.CompanyId, new VoucherCreateDto
        {
            FinancialYearId = currentFY.FinancialYearId,
            VoucherTypeId = receiptType.VoucherTypeId,
            VoucherDate = new DateTime(2026, 6, 15),
            Narration = "Future June receipt",
            Entries = new List<VoucherEntryDto>
            {
                new() { LedgerId = cash.LedgerId, Debit = 3000m, Credit = 0m },
                new() { LedgerId = income.LedgerId, Debit = 0m, Credit = 3000m }
            }
        });

        // Query Cash Book strictly for May (2026-05-01 to 2026-05-31)
        var report = await acctService.GetCashBankBookAsync(
            company.CompanyId,
            cash.LedgerId,
            CashBankBookType.CashBook,
            new DateTime(2026, 5, 1),
            new DateTime(2026, 5, 31));

        // Soft-deleted and June vouchers must be completely excluded
        report.Lines.Should().HaveCount(1);
        report.Lines[0].Narration.Should().Be("Valid May receipt");
        report.TotalDebit.Should().Be(2000m);
        report.TotalCredit.Should().Be(0m);
        report.ClosingBalance.Should().Be(7000m); // 5000 opening + 2000
    }
}
