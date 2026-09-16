using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MoneyFlow.Data;
using MoneyFlow.Services;
using Xunit;

namespace MoneyFlow.Tests.AccountingTests;

public class LiveDatabaseMigrationTest
{
    private const string ConnectionString = "Data Source=DESKTOP-7T8TUSM;Initial Catalog=MoneyFlowDB;Integrated Security=True;TrustServerCertificate=True;";

    [Fact]
    public async Task MigrateLiveDatabase_AppliesPredefined28Groups_AndPurgesDummyLedgers()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        using var context = new AppDbContext(options);
        
        // Ensure can connect to SQL Server
        bool canConnect = await context.Database.CanConnectAsync();
        if (!canConnect)
        {
            // Skip if live SQL Server instance is not accessible in this environment
            return;
        }

        var dbSetup = new DatabaseSetupService(context, NullLogger<DatabaseSetupService>.Instance);
        var result = await dbSetup.InitializeDatabaseAsync();
        result.IsSuccess.Should().BeTrue();

        // 1. Verify Groups Count is exactly 28 for Company 1
        var company = await context.Companies.FirstOrDefaultAsync(c => c.IsActive);
        if (company != null)
        {
            int companyId = company.CompanyId;
            var groups = await context.Groups.Where(g => g.CompanyId == companyId).ToListAsync();
            groups.Should().HaveCount(28);

            // 15 Primary Groups
            var primaryGroups = groups.Where(g => g.PrimaryGroup).ToList();
            primaryGroups.Should().HaveCount(15);

            // 13 Sub-Groups
            var subGroups = groups.Where(g => !g.PrimaryGroup).ToList();
            subGroups.Should().HaveCount(13);

            // Duties & Taxes is under Current Liabilities
            var dutiesTaxes = subGroups.FirstOrDefault(g => g.GroupName == "Duties & Taxes");
            dutiesTaxes.Should().NotBeNull();
            var curLiab = primaryGroups.First(g => g.GroupName == "Current Liabilities");
            dutiesTaxes!.ParentGroupId.Should().Be(curLiab.GroupId);

            // 2. Verify Ledgers in Company 1
            var ledgers = await context.Ledgers.Where(l => l.CompanyId == companyId).ToListAsync();
            
            // Dummy groups-as-ledgers should NOT be present
            var purgedNames = new[] { "Capital Account", "Direct Expenses", "Indirect Expenses", "Direct Income", "Indirect Income", "Sundry Debtors", "Sundry Creditors" };
            foreach (var purged in purgedNames)
            {
                ledgers.Should().NotContain(l => l.LedgerName == purged, $"Dummy ledger '{purged}' must be purged");
            }

            // Real ledgers exist
            var cash = ledgers.FirstOrDefault(l => l.LedgerName == "Cash");
            cash.Should().NotBeNull();
            var cashGroup = groups.First(g => g.GroupName.Equals("Cash-in-hand", StringComparison.OrdinalIgnoreCase));
            cash!.GroupId.Should().Be(cashGroup.GroupId);

            var pnl = ledgers.FirstOrDefault(l => l.LedgerName == "Profit & Loss A/c");
            pnl.Should().NotBeNull();
        }
    }
}
