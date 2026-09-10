using System;
using System.Collections.Generic;

namespace MoneyFlow.Core.Entities;

public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public virtual ICollection<User> Users { get; set; } = new List<User>();
    public virtual ICollection<Permission> Permissions { get; set; } = new List<Permission>();
}

public class Permission
{
    public int PermissionId { get; set; }
    public string PermissionKey { get; set; } = string.Empty; // e.g., "Voucher.Create"
    public string Module { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();
}

public class User
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? LastLoginAt { get; set; }

    public virtual Role? Role { get; set; }
}

public class AuditLog
{
    public long AuditLogId { get; set; }
    public int? CompanyId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // Create, Edit, Delete, Backup, etc.
    public string Module { get; set; } = string.Empty; // Voucher, Ledger, Company, etc.
    public string RecordId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string Description { get; set; } = string.Empty;
}

public class AppSetting
{
    public int SettingId { get; set; }
    public string SettingKey { get; set; } = string.Empty;
    public string SettingValue { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class BackupHistory
{
    public int BackupHistoryId { get; set; }
    public int? CompanyId { get; set; }
    public string BackupFileName { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DateTime BackupDate { get; set; } = DateTime.Now;
    public bool IsAutomated { get; set; }
    public bool IsSuccessful { get; set; } = true;
    public string Notes { get; set; } = string.Empty;
}
