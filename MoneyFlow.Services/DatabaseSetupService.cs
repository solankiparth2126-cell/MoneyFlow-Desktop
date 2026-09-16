using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.Constants;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Data;

namespace MoneyFlow.Services;

public class DatabaseSetupService : IDatabaseSetupService
{
    private readonly AppDbContext _context;
    private readonly ILogger<DatabaseSetupService> _logger;
    private string _activeConnectionString;

    public DatabaseSetupService(AppDbContext context, ILogger<DatabaseSetupService> logger)
    {
        _context = context;
        _logger = logger;
        _activeConnectionString = _context.Database.IsRelational() 
            ? (_context.Database.GetConnectionString() ?? string.Empty) 
            : "Server=.\\SQLEXPRESS;Database=MoneyFlowDB;Trusted_Connection=True;TrustServerCertificate=True;";
    }

    public string GetActiveConnectionString() => _activeConnectionString;

    public void SetActiveConnectionString(string connectionString)
    {
        _activeConnectionString = connectionString;
        if (_context.Database.IsRelational())
        {
            _context.Database.SetConnectionString(connectionString);
        }
    }

    public async Task<DatabaseConnectionResult> TestConnectionAsync(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        try
        {
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            
            return new DatabaseConnectionResult
            {
                IsSuccess = true,
                Message = $"Successfully connected to SQL Server: {builder.DataSource}, Database: {builder.InitialCatalog}",
                ServerInstance = builder.DataSource,
                DatabaseName = builder.InitialCatalog
            };
        }
        catch (SqlException ex) when (ex.Number == 4060) // Database does not exist yet
        {
            // Verify if SQL Server instance is reachable via master database
            try
            {
                var masterBuilder = new SqlConnectionStringBuilder(connectionString)
                {
                    InitialCatalog = "master"
                };
                using var masterConn = new SqlConnection(masterBuilder.ConnectionString);
                await masterConn.OpenAsync();

                return new DatabaseConnectionResult
                {
                    IsSuccess = true,
                    Message = $"Successfully reached SQL Server '{builder.DataSource}'. Database '{builder.InitialCatalog}' will be created upon initialization.",
                    ServerInstance = builder.DataSource,
                    DatabaseName = builder.InitialCatalog
                };
            }
            catch (Exception masterEx)
            {
                _logger.LogError(masterEx, "Failed to connect to master database on {DataSource}", builder.DataSource);
                return new DatabaseConnectionResult
                {
                    IsSuccess = false,
                    Message = $"Unable to connect to SQL Server at '{builder.DataSource}'. Please ensure the SQL Server instance is running and accessible.",
                    ServerInstance = builder.DataSource,
                    DatabaseName = builder.InitialCatalog,
                    Exception = masterEx
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to database using connection string.");
            return new DatabaseConnectionResult
            {
                IsSuccess = false,
                Message = $"Unable to connect to SQL Server at '{builder.DataSource}'. {ex.Message}",
                ServerInstance = builder.DataSource,
                DatabaseName = builder.InitialCatalog,
                Exception = ex
            };
        }
    }

    public async Task<DatabaseConnectionResult> InitializeDatabaseAsync()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(_activeConnectionString) && _context.Database.IsRelational())
            {
                _context.Database.SetConnectionString(_activeConnectionString);
            }

            // Create database and apply schema if not exists
            await _context.Database.EnsureCreatedAsync();

            // Seed System Data: Voucher Types
            await SeedVoucherTypesAsync();

            // Seed System Data: Default Admin Role
            await SeedDefaultRolesAndUserAsync();

            // Migrate Accounting Masters (28 predefined groups, purge dummy ledgers)
            await MigrateAccountingMastersAsync();

            return new DatabaseConnectionResult
            {
                IsSuccess = true,
                Message = "Database verified and system data initialized successfully."
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing database.");
            return new DatabaseConnectionResult
            {
                IsSuccess = false,
                Message = $"Database initialization failed: {ex.Message}",
                Exception = ex
            };
        }
    }

    private async Task SeedVoucherTypesAsync()
    {
        if (!await _context.VoucherTypes.AnyAsync())
        {
            var defaultTypes = new List<VoucherType>
            {
                new() { Name = "Payment", Code = "PMT", Type = VoucherTypeEnum.Payment, Prefix = "PAY-", NextNumber = 1, IsActive = true },
                new() { Name = "Receipt", Code = "RCT", Type = VoucherTypeEnum.Receipt, Prefix = "REC-", NextNumber = 1, IsActive = true },
                new() { Name = "Contra", Code = "CNT", Type = VoucherTypeEnum.Contra, Prefix = "CTR-", NextNumber = 1, IsActive = true },
                new() { Name = "Journal", Code = "JRN", Type = VoucherTypeEnum.Journal, Prefix = "JRN-", NextNumber = 1, IsActive = true },
                new() { Name = "Sales", Code = "SLS", Type = VoucherTypeEnum.Sales, Prefix = "SAL-", NextNumber = 1, IsActive = true },
                new() { Name = "Purchase", Code = "PUR", Type = VoucherTypeEnum.Purchase, Prefix = "PUR-", NextNumber = 1, IsActive = true },
                new() { Name = "Debit Note", Code = "DRN", Type = VoucherTypeEnum.DebitNote, Prefix = "DRN-", NextNumber = 1, IsActive = true },
                new() { Name = "Credit Note", Code = "CRN", Type = VoucherTypeEnum.CreditNote, Prefix = "CRN-", NextNumber = 1, IsActive = true }
            };

            await _context.VoucherTypes.AddRangeAsync(defaultTypes);
            await _context.SaveChangesAsync();
        }
    }

    private async Task SeedDefaultRolesAndUserAsync()
    {
        // 1. Seed Permissions
        if (!await _context.Permissions.AnyAsync())
        {
            var defaultPermissions = new List<Permission>
            {
                new() { PermissionKey = "Company.Manage", Module = "Company", Description = "Create and alter companies" },
                new() { PermissionKey = "Company.Select", Module = "Company", Description = "Open and select companies" },
                new() { PermissionKey = "Group.Manage", Module = "Masters", Description = "Create and modify account groups" },
                new() { PermissionKey = "Ledger.Create", Module = "Masters", Description = "Create new ledger accounts" },
                new() { PermissionKey = "Ledger.Edit", Module = "Masters", Description = "Modify existing ledger accounts" },
                new() { PermissionKey = "Ledger.Delete", Module = "Masters", Description = "Delete or deactivate ledgers" },
                new() { PermissionKey = "Ledger.View", Module = "Masters", Description = "View ledger list and statements" },
                new() { PermissionKey = "Voucher.Create", Module = "Transactions", Description = "Create new vouchers across all 8 types" },
                new() { PermissionKey = "Voucher.Edit", Module = "Transactions", Description = "Modify existing accounting vouchers" },
                new() { PermissionKey = "Voucher.Delete", Module = "Transactions", Description = "Cancel or delete accounting vouchers" },
                new() { PermissionKey = "Voucher.View", Module = "Transactions", Description = "View Day Book and voucher registers" },
                new() { PermissionKey = "Inventory.Manage", Module = "Inventory", Description = "Create and edit stock items and units" },
                new() { PermissionKey = "Inventory.View", Module = "Inventory", Description = "View stock summary and item details" },
                new() { PermissionKey = "Report.Financial", Module = "Reports", Description = "View Balance Sheet, P&L, Trial Balance" },
                new() { PermissionKey = "Report.Registers", Module = "Reports", Description = "View Cash/Bank Book and Outstanding" },
                new() { PermissionKey = "Data.ImportExport", Module = "Utilities", Description = "Import and export accounting data" },
                new() { PermissionKey = "System.Backup", Module = "Utilities", Description = "Create company and database backups" },
                new() { PermissionKey = "System.Restore", Module = "Utilities", Description = "Restore backups into database" },
                new() { PermissionKey = "Security.ManageUsers", Module = "Security", Description = "Manage user accounts and credentials" },
                new() { PermissionKey = "Security.ManageRoles", Module = "Security", Description = "Manage roles and permission matrix" }
            };

            await _context.Permissions.AddRangeAsync(defaultPermissions);
            await _context.SaveChangesAsync();
        }

        var allPermissions = await _context.Permissions.ToListAsync();

        // 2. Seed Roles per Section 47
        if (!await _context.Roles.AnyAsync())
        {
            var adminRole = new Role
            {
                RoleName = "Administrator",
                Description = "Full administrative system access",
                Permissions = allPermissions.ToList()
            };

            var accountantRole = new Role
            {
                RoleName = "Accountant",
                Description = "Comprehensive accounting, transactions, and reports access",
                Permissions = allPermissions.Where(p => !p.PermissionKey.StartsWith("Security.") && p.PermissionKey != "System.Restore").ToList()
            };

            var operatorRole = new Role
            {
                RoleName = "Operator",
                Description = "Data entry operator for vouchers and stock creation",
                Permissions = allPermissions.Where(p => 
                    p.PermissionKey == "Company.Select" ||
                    p.PermissionKey == "Ledger.Create" || p.PermissionKey == "Ledger.View" ||
                    p.PermissionKey == "Voucher.Create" || p.PermissionKey == "Voucher.View" ||
                    p.PermissionKey == "Inventory.View" ||
                    p.PermissionKey == "Report.Registers").ToList()
            };

            var viewerRole = new Role
            {
                RoleName = "Viewer",
                Description = "Read-only access to financial reports and accounting registers",
                Permissions = allPermissions.Where(p => 
                    p.PermissionKey == "Company.Select" ||
                    p.PermissionKey == "Ledger.View" ||
                    p.PermissionKey == "Voucher.View" ||
                    p.PermissionKey == "Inventory.View" ||
                    p.PermissionKey == "Report.Financial" ||
                    p.PermissionKey == "Report.Registers").ToList()
            };

            await _context.Roles.AddRangeAsync(adminRole, accountantRole, operatorRole, viewerRole);
            await _context.SaveChangesAsync();
        }

        // 3. Seed Default Admin User with PBKDF2 Salt & Hash
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == "admin");
        if (admin == null)
        {
            var adminRole = await _context.Roles.FirstAsync(r => r.RoleName == "Administrator");
            var (hash, salt) = MoneyFlow.Services.Security.PasswordHasher.HashPassword("admin123");

            var adminUser = new User
            {
                Username = "admin",
                FullName = "System Administrator",
                RoleId = adminRole.RoleId,
                PasswordHash = hash,
                Salt = salt,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            await _context.Users.AddAsync(adminUser);
            await _context.SaveChangesAsync();
        }
        else if (string.IsNullOrWhiteSpace(admin.Salt))
        {
            // Upgrade legacy password to PBKDF2 hash + salt
            var (hash, salt) = MoneyFlow.Services.Security.PasswordHasher.HashPassword("admin123");
            admin.PasswordHash = hash;
            admin.Salt = salt;
            await _context.SaveChangesAsync();
        }
    }

    private async Task MigrateAccountingMastersAsync()
    {
        try
        {
            // 1. Ensure Groups table has IsPredefined column in relational DB
            if (_context.Database.IsRelational())
            {
                try
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        @"IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[Groups]') AND name = 'IsPredefined')
                          BEGIN
                              ALTER TABLE [Groups] ADD [IsPredefined] BIT NOT NULL DEFAULT 0;
                          END");
                }
                catch (Exception sqlEx)
                {
                    _logger.LogWarning(sqlEx, "Schema migration warning on Groups.IsPredefined");
                }
            }

            // 2. Fetch all companies
            var companies = await _context.Companies.ToListAsync();
            if (companies.Count == 0) return;

            foreach (var comp in companies)
            {
                int companyId = comp.CompanyId;

                // Load all existing groups for this company
                var existingGroups = await _context.Groups
                    .Where(g => g.CompanyId == companyId)
                    .ToListAsync();

                // Map of normalized group names
                var groupByName = new Dictionary<string, MoneyFlow.Core.Entities.Group>(StringComparer.OrdinalIgnoreCase);
                foreach (var g in existingGroups)
                {
                    groupByName[g.GroupName] = g;
                }

                // 2a. Rename legacy groups if they exist
                var renames = new (string OldName, string NewName)[]
                {
                    ("Loans", "Loans (Liability)"),
                    ("Direct Income", "Direct Incomes"),
                    ("Indirect Income", "Indirect Incomes"),
                    ("Cash-in-Hand", "Cash-in-hand")
                };

                foreach (var (oldName, newName) in renames)
                {
                    if (groupByName.TryGetValue(oldName, out var grp))
                    {
                        if (!grp.GroupName.Equals(newName, StringComparison.Ordinal))
                        {
                            grp.GroupName = newName;
                            grp.IsPredefined = true;
                            grp.UpdatedAt = DateTime.Now;
                            groupByName.Remove(oldName);
                            groupByName[newName] = grp;
                            _logger.LogInformation("Migrated group name '{OldName}' -> '{NewName}' for Company {CompanyId}", oldName, newName, companyId);
                        }
                    }
                }

                // 2b. Re-parent 'Duties & Taxes' under 'Current Liabilities'
                if (groupByName.TryGetValue("Duties & Taxes", out var dtGroup))
                {
                    if (groupByName.TryGetValue("Current Liabilities", out var curLiabGroup))
                    {
                        if (dtGroup.PrimaryGroup || dtGroup.ParentGroupId != curLiabGroup.GroupId)
                        {
                            dtGroup.PrimaryGroup = false;
                            dtGroup.ParentGroupId = curLiabGroup.GroupId;
                            dtGroup.Nature = GroupNature.Liabilities;
                            dtGroup.IsPredefined = true;
                            dtGroup.UpdatedAt = DateTime.Now;
                            _logger.LogInformation("Re-parented Duties & Taxes under Current Liabilities for Company {CompanyId}", companyId);
                        }
                    }
                }

                // 2c. Seed missing 15 Primary Groups
                foreach (var pgDef in PredefinedAccountingGroups.PrimaryGroups)
                {
                    if (!groupByName.TryGetValue(pgDef.Name, out var grp))
                    {
                        grp = new MoneyFlow.Core.Entities.Group
                        {
                            CompanyId = companyId,
                            GroupName = pgDef.Name,
                            Nature = pgDef.Nature,
                            PrimaryGroup = true,
                            ParentGroupId = null,
                            AffectProfitLoss = pgDef.AffectProfitLoss,
                            IsPredefined = true,
                            IsActive = true,
                            CreatedAt = DateTime.Now
                        };
                        await _context.Groups.AddAsync(grp);
                        groupByName[grp.GroupName] = grp;
                        _logger.LogInformation("Added missing primary group '{GroupName}' for Company {CompanyId}", grp.GroupName, companyId);
                    }
                    else
                    {
                        grp.IsPredefined = true;
                        grp.Nature = pgDef.Nature;
                        grp.PrimaryGroup = true;
                        grp.AffectProfitLoss = pgDef.AffectProfitLoss;
                    }
                }

                await _context.SaveChangesAsync();

                // 2d. Seed missing 13 Sub-Groups
                foreach (var sgDef in PredefinedAccountingGroups.SubGroups)
                {
                    if (!groupByName.TryGetValue(sgDef.Name, out var grp))
                    {
                        if (!string.IsNullOrEmpty(sgDef.ParentGroupName) && groupByName.TryGetValue(sgDef.ParentGroupName, out var parentGrp))
                        {
                            grp = new MoneyFlow.Core.Entities.Group
                            {
                                CompanyId = companyId,
                                GroupName = sgDef.Name,
                                Nature = sgDef.Nature,
                                PrimaryGroup = false,
                                ParentGroupId = parentGrp.GroupId,
                                AffectProfitLoss = sgDef.AffectProfitLoss,
                                IsPredefined = true,
                                IsActive = true,
                                CreatedAt = DateTime.Now
                            };
                            await _context.Groups.AddAsync(grp);
                            groupByName[grp.GroupName] = grp;
                            _logger.LogInformation("Added missing sub-group '{GroupName}' under '{Parent}' for Company {CompanyId}", grp.GroupName, parentGrp.GroupName, companyId);
                        }
                    }
                    else
                    {
                        grp.IsPredefined = true;
                        grp.Nature = sgDef.Nature;
                        grp.PrimaryGroup = false;
                        grp.AffectProfitLoss = sgDef.AffectProfitLoss;
                        if (!string.IsNullOrEmpty(sgDef.ParentGroupName) && groupByName.TryGetValue(sgDef.ParentGroupName, out var parentGrp))
                        {
                            grp.ParentGroupId = parentGrp.GroupId;
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // 2e. Audit Ledgers: purge dummy Group-as-Ledger records that have 0 voucher entries
                var dummyLedgerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "Capital Account",
                    "Current Assets",
                    "Current Liabilities",
                    "Direct Expenses",
                    "Direct Income",
                    "Direct Incomes",
                    "Indirect Expenses",
                    "Indirect Income",
                    "Indirect Incomes",
                    "Sundry Debtors",
                    "Sundry Creditors",
                    "Bank Accounts",
                    "Cash-in-hand",
                    "Cash-in-Hand",
                    "Fixed Assets",
                    "Investments",
                    "Loans",
                    "Loans (Liability)",
                    "Duties & Taxes"
                };

                var existingLedgers = await _context.Ledgers
                    .Where(l => l.CompanyId == companyId)
                    .ToListAsync();

                foreach (var ledger in existingLedgers)
                {
                    if (dummyLedgerNames.Contains(ledger.LedgerName.Trim()))
                    {
                        bool hasTransactions = await _context.VoucherEntries.AnyAsync(ve => ve.LedgerId == ledger.LedgerId);
                        if (!hasTransactions)
                        {
                            _context.Ledgers.Remove(ledger);
                            _logger.LogInformation("Purged dummy Group-as-Ledger '{LedgerName}' (ID: {LedgerId}) for Company {CompanyId}", ledger.LedgerName, ledger.LedgerId, companyId);
                        }
                        else
                        {
                            _logger.LogWarning("Dummy Group-as-Ledger '{LedgerName}' (ID: {LedgerId}) has transactions; preserved.", ledger.LedgerName, ledger.LedgerId);
                        }
                    }
                }

                await _context.SaveChangesAsync();

                // 2f. Ensure Cash ledger is under Cash-in-hand group
                var cashLedger = existingLedgers.FirstOrDefault(l => l.LedgerName.Equals("Cash", StringComparison.OrdinalIgnoreCase));
                if (cashLedger != null && groupByName.TryGetValue("Cash-in-hand", out var cashGroup))
                {
                    if (cashLedger.GroupId != cashGroup.GroupId)
                    {
                        cashLedger.GroupId = cashGroup.GroupId;
                        _logger.LogInformation("Re-assigned Cash ledger to Cash-in-hand group for Company {CompanyId}", companyId);
                    }
                }

                // 2g. Ensure Profit & Loss A/c ledger is under Capital Account group
                var pnlLedger = existingLedgers.FirstOrDefault(l => l.LedgerName.Equals("Profit & Loss A/c", StringComparison.OrdinalIgnoreCase));
                if (pnlLedger != null && groupByName.TryGetValue("Capital Account", out var capGroup))
                {
                    if (pnlLedger.GroupId != capGroup.GroupId)
                    {
                        pnlLedger.GroupId = capGroup.GroupId;
                        _logger.LogInformation("Re-assigned Profit & Loss A/c ledger to Capital Account group for Company {CompanyId}", companyId);
                    }
                }

                await _context.SaveChangesAsync();
            }

            _logger.LogInformation("Completed accounting masters migration for all companies.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MigrateAccountingMastersAsync");
        }
    }
}
