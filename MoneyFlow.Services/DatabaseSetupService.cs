using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
            : "Server=.\\SQLEXPRESS02;Database=MoneyFlowDB;Trusted_Connection=True;TrustServerCertificate=True;";
    }

    public string GetActiveConnectionString() => _activeConnectionString;

    public void SetActiveConnectionString(string connectionString)
    {
        _activeConnectionString = connectionString;
    }

    public async Task<DatabaseConnectionResult> TestConnectionAsync(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to database using connection string.");
            var builder = new SqlConnectionStringBuilder(connectionString);
            return new DatabaseConnectionResult
            {
                IsSuccess = false,
                Message = $"Unable to connect to SQL Server at '{builder.DataSource}'. Please ensure the SQL Server instance is running and accessible.",
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
            var canConnect = await _context.Database.CanConnectAsync();
            if (!canConnect)
            {
                return new DatabaseConnectionResult
                {
                    IsSuccess = false,
                    Message = "Database connection failed. Please check that SQL Server is running."
                };
            }

            // Create database if not exists or apply migrations
            await _context.Database.EnsureCreatedAsync();

            // Seed System Data: Voucher Types
            await SeedVoucherTypesAsync();

            // Seed System Data: Default Admin Role
            await SeedDefaultRolesAndUserAsync();

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
        if (!await _context.Roles.AnyAsync())
        {
            var adminRole = new Role
            {
                RoleName = "Administrator",
                Description = "System Administrator with full access"
            };

            await _context.Roles.AddAsync(adminRole);
            await _context.SaveChangesAsync();

            if (!await _context.Users.AnyAsync())
            {
                var adminUser = new User
                {
                    Username = "admin",
                    FullName = "Administrator",
                    RoleId = adminRole.RoleId,
                    // Simple hash placeholder for Phase 1 - Phase 29 implements full hashing/security
                    PasswordHash = "admin123",
                    CreatedAt = DateTime.Now,
                    IsActive = true
                };

                await _context.Users.AddAsync(adminUser);
                await _context.SaveChangesAsync();
            }
        }
    }
}
