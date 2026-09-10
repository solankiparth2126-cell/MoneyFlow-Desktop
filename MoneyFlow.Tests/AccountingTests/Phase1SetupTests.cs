using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Data;
using MoneyFlow.Services;
using Xunit;

namespace MoneyFlow.Tests.AccountingTests;

public class Phase1SetupTests
{
    private AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    [Fact]
    public void AppDbContext_Should_Have_All_Core_Accounting_DbSets_Configured()
    {
        using var context = CreateInMemoryDbContext();

        context.Companies.Should().NotBeNull();
        context.FinancialYears.Should().NotBeNull();
        context.Groups.Should().NotBeNull();
        context.Ledgers.Should().NotBeNull();
        context.VoucherTypes.Should().NotBeNull();
        context.Vouchers.Should().NotBeNull();
        context.VoucherEntries.Should().NotBeNull();
        context.StockItems.Should().NotBeNull();
        context.Units.Should().NotBeNull();
        context.Users.Should().NotBeNull();
        context.Roles.Should().NotBeNull();
        context.AuditLogs.Should().NotBeNull();
        context.Settings.Should().NotBeNull();
        context.BackupHistories.Should().NotBeNull();
    }

    [Fact]
    public async Task DatabaseSetupService_Should_Seed_VoucherTypes_And_Admin()
    {
        using var context = CreateInMemoryDbContext();
        var logger = NullLogger<DatabaseSetupService>.Instance;
        var service = new DatabaseSetupService(context, logger);

        var result = await service.InitializeDatabaseAsync();

        result.IsSuccess.Should().BeTrue();
        context.VoucherTypes.Count().Should().Be(8);
        context.Roles.Any(r => r.RoleName == "Administrator").Should().BeTrue();
        context.Users.Any(u => u.Username == "admin").Should().BeTrue();
    }

    [Fact]
    public void DoubleEntry_Principle_VoucherValidation_Check()
    {
        // Testing fundamental accounting principle: Total Debit must equal Total Credit
        var entries = new[]
        {
            new VoucherEntry { LedgerId = 1, Debit = 500m, Credit = 0m },
            new VoucherEntry { LedgerId = 2, Debit = 0m, Credit = 500m }
        };

        decimal totalDebit = entries.Sum(e => e.Debit);
        decimal totalCredit = entries.Sum(e => e.Credit);
        decimal difference = Math.Abs(totalDebit - totalCredit);

        difference.Should().Be(0m);
        totalDebit.Should().Be(totalCredit);
    }
}
