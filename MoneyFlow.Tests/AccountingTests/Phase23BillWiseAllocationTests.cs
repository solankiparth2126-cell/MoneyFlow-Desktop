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

public class Phase23BillWiseAllocationTests
{
    private (AppDbContext Context, AccountingService AccService, BillAllocationService BillService, int CompanyId, int FyId, int DebtorId, int SalesId, int CashId) CreateTestSetup()
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

        var billService = new BillAllocationService(context, NullLogger<BillAllocationService>.Instance);

        // Seed Company & FY
        var company = new Company { CompanyName = "Sunrise Enterprises", Currency = "₹" };
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

        // Voucher Types
        var vtSales = new VoucherType { Name = "Sales", Code = "SLS", Type = VoucherTypeEnum.Sales, Prefix = "INV/", PaddingWidth = 4 };
        var vtReceipt = new VoucherType { Name = "Receipt", Code = "RCP", Type = VoucherTypeEnum.Receipt, Prefix = "RCP/", PaddingWidth = 4 };
        context.VoucherTypes.AddRange(vtSales, vtReceipt);

        // Groups
        var grpDebtors = new Group { CompanyId = company.CompanyId, GroupName = "Sundry Debtors", Nature = GroupNature.Assets, PrimaryGroup = true };
        var grpSales = new Group { CompanyId = company.CompanyId, GroupName = "Sales Accounts", Nature = GroupNature.Income, PrimaryGroup = true };
        var grpCash = new Group { CompanyId = company.CompanyId, GroupName = "Cash-in-Hand", Nature = GroupNature.Assets, PrimaryGroup = true };
        context.Groups.AddRange(grpDebtors, grpSales, grpCash);
        context.SaveChanges();

        // Ledgers
        var lDebtor = new Ledger { CompanyId = company.CompanyId, GroupId = grpDebtors.GroupId, LedgerName = "Apex Retailers" };
        var lSales = new Ledger { CompanyId = company.CompanyId, GroupId = grpSales.GroupId, LedgerName = "Domestic Sales" };
        var lCash = new Ledger { CompanyId = company.CompanyId, GroupId = grpCash.GroupId, LedgerName = "Cash" };
        context.Ledgers.AddRange(lDebtor, lSales, lCash);
        context.SaveChanges();

        compContext.SetActiveCompany(company, fy);

        return (context, accService, billService, company.CompanyId, fy.FinancialYearId, lDebtor.LedgerId, lSales.LedgerId, lCash.LedgerId);
    }

    [Fact]
    public async Task BillWiseAllocation_NewRef_And_AgstRef_Should_Track_And_Settle_Pending_Bills()
    {
        var (context, accService, billService, companyId, fyId, debtorId, salesId, cashId) = CreateTestSetup();
        var vtSales = await context.VoucherTypes.FirstAsync(v => v.Type == VoucherTypeEnum.Sales);
        var vtReceipt = await context.VoucherTypes.FirstAsync(v => v.Type == VoucherTypeEnum.Receipt);

        // 1. Create Sales Invoice for ₹40,000 with New Ref "INV-2026-001"
        var salesDto = new VoucherCreateDto
        {
            FinancialYearId = fyId,
            VoucherTypeId = vtSales.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 15),
            ReferenceNumber = "INV-2026-001",
            Narration = "Goods sold to Apex Retailers",
            Entries = new List<VoucherEntryDto>
            {
                new()
                {
                    LedgerId = debtorId,
                    Debit = 40000m,
                    Credit = 0m,
                    BillAllocations = new List<BillAllocationCreateDto>
                    {
                        new()
                        {
                            LedgerId = debtorId,
                            BillType = BillType.NewRef,
                            BillName = "INV-2026-001",
                            DueDate = new DateTime(2026, 5, 15),
                            CreditDays = 30,
                            Amount = 40000m
                        }
                    }
                },
                new()
                {
                    LedgerId = salesId,
                    Debit = 0m,
                    Credit = 40000m
                }
            }
        };

        var savedSales = await accService.SaveVoucherAsync(companyId, salesDto);
        savedSales.VoucherNumber.Should().StartWith("INV/");

        // 2. Query Pending Bills - Should return 1 bill with ₹40,000 pending
        var pending1 = await billService.GetPendingBillsAsync(companyId, debtorId);
        pending1.Should().HaveCount(1);
        pending1[0].BillName.Should().Be("INV-2026-001");
        pending1[0].OriginalAmount.Should().Be(40000m);
        pending1[0].SettledAmount.Should().Be(0m);
        pending1[0].PendingAmount.Should().Be(40000m);

        // 3. Post Partial Receipt of ₹25,000 with Agst Ref "INV-2026-001"
        var receiptDto = new VoucherCreateDto
        {
            FinancialYearId = fyId,
            VoucherTypeId = vtReceipt.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 20),
            Narration = "Part payment received from Apex Retailers",
            Entries = new List<VoucherEntryDto>
            {
                new()
                {
                    LedgerId = cashId,
                    Debit = 25000m,
                    Credit = 0m
                },
                new()
                {
                    LedgerId = debtorId,
                    Debit = 0m,
                    Credit = 25000m,
                    BillAllocations = new List<BillAllocationCreateDto>
                    {
                        new()
                        {
                            LedgerId = debtorId,
                            BillType = BillType.AgstRef,
                            BillName = "INV-2026-001",
                            Amount = 25000m
                        }
                    }
                }
            }
        };

        await accService.SaveVoucherAsync(companyId, receiptDto);

        // 4. Query Pending Bills - Should now return ₹15,000 pending
        var pending2 = await billService.GetPendingBillsAsync(companyId, debtorId);
        pending2.Should().HaveCount(1);
        pending2[0].OriginalAmount.Should().Be(40000m);
        pending2[0].SettledAmount.Should().Be(25000m);
        pending2[0].PendingAmount.Should().Be(15000m);

        // 5. Post Final Receipt of remaining ₹15,000
        var receiptFinalDto = new VoucherCreateDto
        {
            FinancialYearId = fyId,
            VoucherTypeId = vtReceipt.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 25),
            Narration = "Final balance received from Apex Retailers",
            Entries = new List<VoucherEntryDto>
            {
                new()
                {
                    LedgerId = cashId,
                    Debit = 15000m,
                    Credit = 0m
                },
                new()
                {
                    LedgerId = debtorId,
                    Debit = 0m,
                    Credit = 15000m,
                    BillAllocations = new List<BillAllocationCreateDto>
                    {
                        new()
                        {
                            LedgerId = debtorId,
                            BillType = BillType.AgstRef,
                            BillName = "INV-2026-001",
                            Amount = 15000m
                        }
                    }
                }
            }
        };

        await accService.SaveVoucherAsync(companyId, receiptFinalDto);

        // 6. Query Pending Bills - Bill is completely settled!
        var pending3 = await billService.GetPendingBillsAsync(companyId, debtorId);
        pending3.Should().BeEmpty();
    }

    [Fact]
    public async Task BillAgingSummary_Should_Categorize_Buckets_Correctly()
    {
        var (context, accService, billService, companyId, fyId, debtorId, salesId, _) = CreateTestSetup();
        var vtSales = await context.VoucherTypes.FirstAsync(v => v.Type == VoucherTypeEnum.Sales);

        // Bill 1: Due 10 days ago (0-30 days bucket)
        // Bill 2: Due 45 days ago (31-60 days bucket)
        DateTime asOfDate = new DateTime(2026, 6, 1);

        var sales1 = new VoucherCreateDto
        {
            FinancialYearId = fyId,
            VoucherTypeId = vtSales.VoucherTypeId,
            VoucherDate = new DateTime(2026, 5, 1),
            ReferenceNumber = "BILL-A",
            Entries = new List<VoucherEntryDto>
            {
                new()
                {
                    LedgerId = debtorId,
                    Debit = 10000m,
                    Credit = 0m,
                    BillAllocations = new List<BillAllocationCreateDto>
                    {
                        new() { LedgerId = debtorId, BillType = BillType.NewRef, BillName = "BILL-A", DueDate = new DateTime(2026, 5, 20), Amount = 10000m }
                    }
                },
                new() { LedgerId = salesId, Debit = 0m, Credit = 10000m }
            }
        };

        var sales2 = new VoucherCreateDto
        {
            FinancialYearId = fyId,
            VoucherTypeId = vtSales.VoucherTypeId,
            VoucherDate = new DateTime(2026, 4, 1),
            ReferenceNumber = "BILL-B",
            Entries = new List<VoucherEntryDto>
            {
                new()
                {
                    LedgerId = debtorId,
                    Debit = 20000m,
                    Credit = 0m,
                    BillAllocations = new List<BillAllocationCreateDto>
                    {
                        new() { LedgerId = debtorId, BillType = BillType.NewRef, BillName = "BILL-B", DueDate = new DateTime(2026, 4, 15), Amount = 20000m }
                    }
                },
                new() { LedgerId = salesId, Debit = 0m, Credit = 20000m }
            }
        };

        await accService.SaveVoucherAsync(companyId, sales1);
        await accService.SaveVoucherAsync(companyId, sales2);

        var aging = await billService.GetBillAgingSummaryAsync(companyId, debtorId, asOfDate);

        aging.CurrentAmount.Should().Be(10000m); // 12 days overdue
        aging.Days31To60.Should().Be(20000m);    // 47 days overdue
        aging.TotalPending.Should().Be(30000m);
    }
}
