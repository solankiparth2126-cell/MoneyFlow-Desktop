using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.Constants;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Data.Storage;
namespace MoneyFlow.Services.Startup;

using Company = MoneyFlow.Core.Entities.Company;
using Group = MoneyFlow.Core.Entities.Group;
using Ledger = MoneyFlow.Core.Entities.Ledger;
using FinancialYear = MoneyFlow.Core.Entities.FinancialYear;

/// <summary>
/// Replaces DatabaseSetupService. Initializes the file-based storage system.
/// No SQL Server dependency.
/// </summary>
public class ApplicationInitializationService
{
    private readonly CompanyManager _companyManager;
    private readonly ILogger<ApplicationInitializationService> _logger;

    public ApplicationInitializationService(CompanyManager companyManager, ILogger<ApplicationInitializationService> logger)
    {
        _companyManager = companyManager;
        _logger = logger;
    }

    /// <summary>
    /// Initializes the MyERP application: creates directories and prepares the system.
    /// Called once at startup. No database, no SQL.
    /// </summary>
    public void Initialize()
    {
        _companyManager.InitializeDirectories();
        _logger.LogInformation("MyERP application initialized.");
    }

    /// <summary>
    /// Creates a new company with system seed data (voucher types, roles, permissions, admin user, predefined groups).
    /// This preserves the exact same seed data that was in DatabaseSetupService.
    /// </summary>
    public string CreateCompanyWithSeedData(
        string companyName,
        string? address,
        DateTime financialYearStart,
        DateTime financialYearEnd,
        string? password)
    {
        var dataStore = new CompanyDataStore();

        // Company info
        dataStore.CompanyInfo = new Company
        {
            CompanyId = 1,
            CompanyName = companyName,
            Address = address ?? string.Empty,
            CreatedAt = DateTime.Now,
            IsActive = true
        };
        dataStore.NextIds.CompanyId = 1;

        // Financial Year
        var fy = new FinancialYear
        {
            FinancialYearId = 1,
            CompanyId = 1,
            YearName = $"{financialYearStart:yyyy}-{financialYearEnd:yyyy}",
            StartDate = financialYearStart,
            EndDate = financialYearEnd,
            IsClosed = false,
            CreatedAt = DateTime.Now
        };
        dataStore.FinancialYears.Add(fy);
        dataStore.NextIds.FinancialYearId = 1;

        // Seed Voucher Types (same as DatabaseSetupService)
        SeedVoucherTypes(dataStore);

        // Seed Permissions
        SeedPermissions(dataStore);

        // Seed Roles
        SeedRoles(dataStore);

        // Seed Default Admin User
        SeedAdminUser(dataStore);

        // Seed 28 Predefined Accounting Groups (Tally-style)
        SeedPredefinedGroups(dataStore);

        // Seed Reserved Ledgers (Cash, Profit & Loss A/c)
        SeedReservedLedgers(dataStore);

        // Create the company
        var companyId = _companyManager.CreateCompany(companyName, dataStore, password);

        _logger.LogInformation("Company created with seed data: {CompanyId} ({Name}).", companyId, companyName);
        return companyId;
    }

    private void SeedVoucherTypes(CompanyDataStore store)
    {
        var types = new List<VoucherType>
        {
            new() { VoucherTypeId = 1, Name = "Payment", Code = "PMT", Type = VoucherTypeEnum.Payment, Prefix = "PAY-", NextNumber = 1, IsActive = true },
            new() { VoucherTypeId = 2, Name = "Receipt", Code = "RCT", Type = VoucherTypeEnum.Receipt, Prefix = "REC-", NextNumber = 1, IsActive = true },
            new() { VoucherTypeId = 3, Name = "Contra", Code = "CNT", Type = VoucherTypeEnum.Contra, Prefix = "CTR-", NextNumber = 1, IsActive = true },
            new() { VoucherTypeId = 4, Name = "Journal", Code = "JRN", Type = VoucherTypeEnum.Journal, Prefix = "JRN-", NextNumber = 1, IsActive = true },
            new() { VoucherTypeId = 5, Name = "Sales", Code = "SLS", Type = VoucherTypeEnum.Sales, Prefix = "SAL-", NextNumber = 1, IsActive = true },
            new() { VoucherTypeId = 6, Name = "Purchase", Code = "PUR", Type = VoucherTypeEnum.Purchase, Prefix = "PUR-", NextNumber = 1, IsActive = true },
            new() { VoucherTypeId = 7, Name = "Debit Note", Code = "DRN", Type = VoucherTypeEnum.DebitNote, Prefix = "DRN-", NextNumber = 1, IsActive = true },
            new() { VoucherTypeId = 8, Name = "Credit Note", Code = "CRN", Type = VoucherTypeEnum.CreditNote, Prefix = "CRN-", NextNumber = 1, IsActive = true }
        };

        store.VoucherTypes.AddRange(types);
        store.NextIds.VoucherTypeId = 8;
    }

    private void SeedPermissions(CompanyDataStore store)
    {
        var permissions = new List<Permission>
        {
            new() { PermissionId = 1, PermissionKey = "Company.Manage", Module = "Company", Description = "Create and alter companies" },
            new() { PermissionId = 2, PermissionKey = "Company.Select", Module = "Company", Description = "Open and select companies" },
            new() { PermissionId = 3, PermissionKey = "Group.Manage", Module = "Masters", Description = "Create and modify account groups" },
            new() { PermissionId = 4, PermissionKey = "Ledger.Create", Module = "Masters", Description = "Create new ledger accounts" },
            new() { PermissionId = 5, PermissionKey = "Ledger.Edit", Module = "Masters", Description = "Modify existing ledger accounts" },
            new() { PermissionId = 6, PermissionKey = "Ledger.Delete", Module = "Masters", Description = "Delete or deactivate ledgers" },
            new() { PermissionId = 7, PermissionKey = "Ledger.View", Module = "Masters", Description = "View ledger list and statements" },
            new() { PermissionId = 8, PermissionKey = "Voucher.Create", Module = "Transactions", Description = "Create new vouchers across all 8 types" },
            new() { PermissionId = 9, PermissionKey = "Voucher.Edit", Module = "Transactions", Description = "Modify existing accounting vouchers" },
            new() { PermissionId = 10, PermissionKey = "Voucher.Delete", Module = "Transactions", Description = "Cancel or delete accounting vouchers" },
            new() { PermissionId = 11, PermissionKey = "Voucher.View", Module = "Transactions", Description = "View Day Book and voucher registers" },
            new() { PermissionId = 12, PermissionKey = "Inventory.Manage", Module = "Inventory", Description = "Create and edit stock items and units" },
            new() { PermissionId = 13, PermissionKey = "Inventory.View", Module = "Inventory", Description = "View stock summary and item details" },
            new() { PermissionId = 14, PermissionKey = "Report.Financial", Module = "Reports", Description = "View Balance Sheet, P&L, Trial Balance" },
            new() { PermissionId = 15, PermissionKey = "Report.Registers", Module = "Reports", Description = "View Cash/Bank Book and Outstanding" },
            new() { PermissionId = 16, PermissionKey = "Data.ImportExport", Module = "Utilities", Description = "Import and export accounting data" },
            new() { PermissionId = 17, PermissionKey = "System.Backup", Module = "Utilities", Description = "Create company and database backups" },
            new() { PermissionId = 18, PermissionKey = "System.Restore", Module = "Utilities", Description = "Restore backups into database" },
            new() { PermissionId = 19, PermissionKey = "Security.ManageUsers", Module = "Security", Description = "Manage user accounts and credentials" },
            new() { PermissionId = 20, PermissionKey = "Security.ManageRoles", Module = "Security", Description = "Manage roles and permission matrix" }
        };

        store.Permissions.AddRange(permissions);
        store.NextIds.PermissionId = 20;
    }

    private void SeedRoles(CompanyDataStore store)
    {
        var allPermKeys = store.Permissions.Select(p => p.PermissionKey).ToHashSet();

        store.Roles.AddRange(new[]
        {
            new Role { RoleId = 1, RoleName = "Administrator", Description = "Full administrative system access" },
            new Role { RoleId = 2, RoleName = "Accountant", Description = "Comprehensive accounting, transactions, and reports access" },
            new Role { RoleId = 3, RoleName = "Operator", Description = "Data entry operator for vouchers and stock creation" },
            new Role { RoleId = 4, RoleName = "Viewer", Description = "Read-only access to financial reports and accounting registers" }
        });

        store.NextIds.RoleId = 4;
    }

    private void SeedAdminUser(CompanyDataStore store)
    {
        var (hash, salt) = Security.PasswordHasher.HashPassword("admin123");

        store.Users.Add(new User
        {
            UserId = 1,
            Username = "admin",
            FullName = "System Administrator",
            RoleId = 1, // Administrator
            PasswordHash = hash,
            Salt = salt,
            CreatedAt = DateTime.Now,
            IsActive = true
        });

        store.NextIds.UserId = 1;
    }

    private void SeedPredefinedGroups(CompanyDataStore store)
    {
        int groupId = 1;
        var groupMap = new Dictionary<string, int>();

        // Primary Groups
        foreach (var def in PredefinedAccountingGroups.PrimaryGroups)
        {
            var group = new Group
            {
                GroupId = groupId,
                CompanyId = 1,
                GroupName = def.Name,
                Nature = def.Nature,
                PrimaryGroup = def.PrimaryGroup,
                AffectProfitLoss = def.AffectProfitLoss,
                ParentGroupId = null,
                IsPredefined = true,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            store.Groups.Add(group);
            groupMap[def.Name] = groupId;
            groupId++;
        }

        // Sub-Groups (with parent references)
        foreach (var def in PredefinedAccountingGroups.SubGroups)
        {
            int? parentId = null;
            if (def.ParentGroupName != null && groupMap.TryGetValue(def.ParentGroupName, out var pid))
                parentId = pid;

            var group = new Group
            {
                GroupId = groupId,
                CompanyId = 1,
                GroupName = def.Name,
                Nature = def.Nature,
                PrimaryGroup = def.PrimaryGroup,
                AffectProfitLoss = def.AffectProfitLoss,
                ParentGroupId = parentId,
                IsPredefined = true,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            store.Groups.Add(group);
            groupMap[def.Name] = groupId;
            groupId++;
        }

        store.NextIds.GroupId = groupId - 1;
    }

    private void SeedReservedLedgers(CompanyDataStore store)
    {
        int ledgerId = 1;

        // Cash ledger under "Cash-in-hand" group
        var cashGroupId = store.Groups.FirstOrDefault(g => g.GroupName == "Cash-in-hand")?.GroupId ?? 1;
        store.Ledgers.Add(new Ledger
        {
            LedgerId = ledgerId++,
            CompanyId = 1,
            GroupId = cashGroupId,
            LedgerName = "Cash",
            OpeningBalance = 0,
            OpeningBalanceType = BalanceType.Debit,
            IsActive = true,
            CreatedAt = DateTime.Now
        });

        // Profit & Loss A/c under "Primary Group" (special system ledger)
        var directIncomesGroupId = store.Groups.FirstOrDefault(g => g.GroupName == "Direct Incomes")?.GroupId ?? 1;
        store.Ledgers.Add(new Ledger
        {
            LedgerId = ledgerId++,
            CompanyId = 1,
            GroupId = directIncomesGroupId,
            LedgerName = "Profit & Loss A/c",
            OpeningBalance = 0,
            OpeningBalanceType = BalanceType.Credit,
            IsActive = true,
            CreatedAt = DateTime.Now
        });

        store.NextIds.LedgerId = ledgerId - 1;
    }
}
