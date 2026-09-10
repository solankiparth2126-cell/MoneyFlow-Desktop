using System;
using Microsoft.EntityFrameworkCore;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<FinancialYear> FinancialYears => Set<FinancialYear>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Ledger> Ledgers => Set<Ledger>();
    public DbSet<VoucherType> VoucherTypes => Set<VoucherType>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<VoucherEntry> VoucherEntries => Set<VoucherEntry>();
    public DbSet<StockItem> StockItems => Set<StockItem>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<BackupHistory> BackupHistories => Set<BackupHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Decimal precision for all monetary columns (18, 2)
        foreach (var property in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            property.SetColumnType("decimal(18,2)");
        }

        // Company
        modelBuilder.Entity<Company>(entity =>
        {
            entity.HasKey(e => e.CompanyId);
            entity.Property(e => e.CompanyName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Address).HasMaxLength(250);
            entity.Property(e => e.State).HasMaxLength(100);
            entity.Property(e => e.Country).HasMaxLength(100);
            entity.Property(e => e.PAN).HasMaxLength(20);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.Currency).HasMaxLength(10);
            entity.HasIndex(e => e.CompanyName);
        });

        // FinancialYear
        modelBuilder.Entity<FinancialYear>(entity =>
        {
            entity.HasKey(e => e.FinancialYearId);
            entity.Property(e => e.YearName).HasMaxLength(50).IsRequired();
            entity.HasOne(e => e.Company)
                  .WithMany(c => c.FinancialYears)
                  .HasForeignKey(e => e.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.CompanyId, e.StartDate, e.EndDate });
        });

        // Group
        modelBuilder.Entity<Group>(entity =>
        {
            entity.HasKey(e => e.GroupId);
            entity.Property(e => e.GroupName).HasMaxLength(100).IsRequired();
            entity.HasOne(e => e.Company)
                  .WithMany(c => c.Groups)
                  .HasForeignKey(e => e.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ParentGroup)
                  .WithMany(g => g.SubGroups)
                  .HasForeignKey(e => e.ParentGroupId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.CompanyId, e.GroupName });
        });

        // Ledger
        modelBuilder.Entity<Ledger>(entity =>
        {
            entity.HasKey(e => e.LedgerId);
            entity.Property(e => e.LedgerName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Address).HasMaxLength(250);
            entity.Property(e => e.Phone).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.PAN).HasMaxLength(20);
            entity.Property(e => e.State).HasMaxLength(100);
            entity.Property(e => e.BankName).HasMaxLength(100);
            entity.Property(e => e.BankAccountNumber).HasMaxLength(50);
            entity.Property(e => e.IFSC).HasMaxLength(20);

            entity.HasOne(e => e.Company)
                  .WithMany(c => c.Ledgers)
                  .HasForeignKey(e => e.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Group)
                  .WithMany(g => g.Ledgers)
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.CompanyId, e.LedgerName });
            entity.HasIndex(e => e.GroupId);
        });

        // VoucherType
        modelBuilder.Entity<VoucherType>(entity =>
        {
            entity.HasKey(e => e.VoucherTypeId);
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Code).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Prefix).HasMaxLength(10);
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // Voucher
        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.VoucherId);
            entity.Property(e => e.VoucherNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ReferenceNumber).HasMaxLength(50);
            entity.Property(e => e.Narration).HasMaxLength(1000);
            entity.Property(e => e.CreatedBy).HasMaxLength(50);
            entity.Property(e => e.ModifiedBy).HasMaxLength(50);

            entity.HasOne(e => e.Company)
                  .WithMany(c => c.Vouchers)
                  .HasForeignKey(e => e.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.FinancialYear)
                  .WithMany(f => f.Vouchers)
                  .HasForeignKey(e => e.FinancialYearId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.VoucherType)
                  .WithMany(v => v.Vouchers)
                  .HasForeignKey(e => e.VoucherTypeId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.CompanyId);
            entity.HasIndex(e => e.FinancialYearId);
            entity.HasIndex(e => e.VoucherDate);
            entity.HasIndex(e => new { e.CompanyId, e.VoucherNumber });
        });

        // VoucherEntry
        modelBuilder.Entity<VoucherEntry>(entity =>
        {
            entity.HasKey(e => e.VoucherEntryId);
            entity.Property(e => e.Narration).HasMaxLength(500);

            entity.HasOne(e => e.Voucher)
                  .WithMany(v => v.VoucherEntries)
                  .HasForeignKey(e => e.VoucherId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Ledger)
                  .WithMany(l => l.VoucherEntries)
                  .HasForeignKey(e => e.LedgerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.VoucherId);
            entity.HasIndex(e => e.LedgerId);
        });

        // Inventory: Unit
        modelBuilder.Entity<Unit>(entity =>
        {
            entity.HasKey(e => e.UnitId);
            entity.Property(e => e.UnitName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.FormalName).HasMaxLength(100);
            entity.HasOne(e => e.Company)
                  .WithMany(c => c.Units)
                  .HasForeignKey(e => e.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Inventory: StockItem
        modelBuilder.Entity<StockItem>(entity =>
        {
            entity.HasKey(e => e.StockItemId);
            entity.Property(e => e.ItemName).HasMaxLength(150).IsRequired();
            entity.HasOne(e => e.Company)
                  .WithMany(c => c.StockItems)
                  .HasForeignKey(e => e.CompanyId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Unit)
                  .WithMany()
                  .HasForeignKey(e => e.UnitId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => new { e.CompanyId, e.ItemName });
        });

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Salt).HasMaxLength(128);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.HasOne(e => e.Role)
                  .WithMany(r => r.Users)
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.Username).IsUnique();
        });

        // Role & Permission
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId);
            entity.Property(e => e.RoleName).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.HasIndex(e => e.RoleName).IsUnique();
        });

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.HasKey(e => e.PermissionId);
            entity.Property(e => e.PermissionKey).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Module).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.HasIndex(e => e.PermissionKey).IsUnique();
        });

        // AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.AuditLogId);
            entity.Property(e => e.Username).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Module).HasMaxLength(50).IsRequired();
            entity.Property(e => e.RecordId).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.CompanyId);
        });

        // Settings
        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(e => e.SettingId);
            entity.Property(e => e.SettingKey).HasMaxLength(100).IsRequired();
            entity.Property(e => e.SettingValue).HasMaxLength(1000).IsRequired();
            entity.HasIndex(e => e.SettingKey).IsUnique();
        });

        // BackupHistory
        modelBuilder.Entity<BackupHistory>(entity =>
        {
            entity.HasKey(e => e.BackupHistoryId);
            entity.Property(e => e.BackupFileName).HasMaxLength(250).IsRequired();
            entity.Property(e => e.BackupPath).HasMaxLength(500).IsRequired();
            entity.HasIndex(e => e.BackupDate);
        });
    }
}
