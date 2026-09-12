using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Data.Configurations;

public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("Units");
        builder.HasKey(u => u.UnitId);
        builder.Property(u => u.UnitName).IsRequired().HasMaxLength(50);
        builder.Property(u => u.FormalName).HasMaxLength(100);
        builder.HasOne(u => u.Company).WithMany(c => c.Units).HasForeignKey(u => u.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(u => new { u.CompanyId, u.UnitName }).IsUnique();
    }
}

public class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.ToTable("StockItems");
        builder.HasKey(s => s.StockItemId);
        builder.Property(s => s.ItemName).IsRequired().HasMaxLength(150);
        builder.Property(s => s.OpeningQuantity).HasColumnType("decimal(18,2)");
        builder.Property(s => s.OpeningRate).HasColumnType("decimal(18,2)");
        builder.Property(s => s.OpeningValue).HasColumnType("decimal(18,2)");

        builder.HasOne(s => s.Company).WithMany(c => c.StockItems).HasForeignKey(s => s.CompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Unit).WithMany().HasForeignKey(s => s.UnitId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(s => new { s.CompanyId, s.ItemName }).IsUnique();
        builder.HasIndex(s => new { s.CompanyId, s.IsActive });
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.UserId);
        builder.Property(u => u.Username).IsRequired().HasMaxLength(50);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);
        builder.Property(u => u.Salt).HasMaxLength(128);
        builder.Property(u => u.FullName).HasMaxLength(100);
        builder.Property(u => u.Email).HasMaxLength(100);

        builder.HasOne(u => u.Role).WithMany(r => r.Users).HasForeignKey(u => u.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(u => u.Username).IsUnique();
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.RoleId);
        builder.Property(r => r.RoleName).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Description).HasMaxLength(200);
        builder.HasIndex(r => r.RoleName).IsUnique();
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions");
        builder.HasKey(p => p.PermissionId);
        builder.Property(p => p.PermissionKey).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Module).HasMaxLength(50);
        builder.Property(p => p.Description).HasMaxLength(200);
        builder.HasIndex(p => p.PermissionKey).IsUnique();
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(a => a.AuditLogId);
        builder.Property(a => a.Username).IsRequired().HasMaxLength(50);
        builder.Property(a => a.Action).IsRequired().HasMaxLength(50);
        builder.Property(a => a.Module).IsRequired().HasMaxLength(50);
        builder.Property(a => a.RecordId).HasMaxLength(50);
        builder.Property(a => a.Description).HasMaxLength(1000);

        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => a.CompanyId);
        builder.HasIndex(a => new { a.CompanyId, a.Timestamp });
        builder.HasIndex(a => new { a.Module, a.Action });
    }
}

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("Settings");
        builder.HasKey(s => s.SettingId);
        builder.Property(s => s.SettingKey).IsRequired().HasMaxLength(100);
        builder.Property(s => s.SettingValue).IsRequired().HasMaxLength(1000);
        builder.HasIndex(s => s.SettingKey).IsUnique();
    }
}

public class BackupHistoryConfiguration : IEntityTypeConfiguration<BackupHistory>
{
    public void Configure(EntityTypeBuilder<BackupHistory> builder)
    {
        builder.ToTable("BackupHistory");
        builder.HasKey(b => b.BackupHistoryId);
        builder.Property(b => b.BackupFileName).IsRequired().HasMaxLength(250);
        builder.Property(b => b.BackupPath).IsRequired().HasMaxLength(500);
        builder.HasIndex(b => b.BackupDate);
    }
}
