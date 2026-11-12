using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;

namespace MoneyFlow.Services.Security;

public class SecurityService : ISecurityService
{
    private readonly AppDbContext _context;
    private readonly IUserContext _userContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<SecurityService> _logger;

    public SecurityService(
        AppDbContext context,
        IUserContext userContext,
        IAuditService auditService,
        ILogger<SecurityService> logger)
    {
        _context = context;
        _userContext = userContext;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<AuthenticationResultDto> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        var cleanUsername = username?.Trim() ?? string.Empty;
        var user = await _context.Users
            .Include(u => u.Role)
                .ThenInclude(r => r!.Permissions)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == cleanUsername.ToLower(), ct);

        if (user == null)
        {
            await _auditService.LogAsync(null, "LoginFailed", "Security", cleanUsername, "Login attempt for non-existent username.", ct);
            return new AuthenticationResultDto
            {
                IsSuccess = false,
                Message = "Invalid username or password."
            };
        }

        if (!user.IsActive)
        {
            await _auditService.LogAsync(null, "LoginBlocked", "Security", user.UserId.ToString(), $"Login attempt for deactivated user '{user.Username}'.", ct);
            return new AuthenticationResultDto
            {
                IsSuccess = false,
                Message = "This user account is inactive. Please contact your system administrator."
            };
        }

        bool passwordValid = false;

        // Check if legacy plaintext password exists
        if (string.IsNullOrWhiteSpace(user.Salt))
        {
            if (user.PasswordHash == password)
            {
                passwordValid = true;
                // Auto-upgrade to PBKDF2 with salt
                var (newHash, newSalt) = PasswordHasher.HashPassword(password);
                user.PasswordHash = newHash;
                user.Salt = newSalt;
                await _context.SaveChangesAsync(ct);
            }
        }
        else
        {
            passwordValid = PasswordHasher.VerifyPassword(password, user.PasswordHash, user.Salt);
        }

        if (!passwordValid)
        {
            await _auditService.LogAsync(null, "LoginFailed", "Security", user.UserId.ToString(), $"Invalid password attempt for '{user.Username}'.", ct);
            return new AuthenticationResultDto
            {
                IsSuccess = false,
                Message = "Invalid username or password."
            };
        }

        user.LastLoginAt = DateTime.Now;
        await _context.SaveChangesAsync(ct);

        var permissions = user.Role?.Permissions.Select(p => p.PermissionKey).ToList() ?? new List<string>();

        // Update active UserContext
        _userContext.SetCurrentUser(
            user.UserId,
            user.Username,
            user.FullName,
            user.Role?.RoleName ?? "User",
            permissions);

        await _auditService.LogAsync(null, "Login", "Security", user.UserId.ToString(), $"User '{user.Username}' authenticated successfully.", ct);

        return new AuthenticationResultDto
        {
            IsSuccess = true,
            Message = "Authentication successful.",
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            RoleName = user.Role?.RoleName ?? "User",
            Permissions = permissions
        };
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetAllUsersAsync(CancellationToken ct = default)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .OrderBy(u => u.Username)
            .Select(u => new UserSummaryDto
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                RoleId = u.RoleId,
                RoleName = u.Role != null ? u.Role.RoleName : "None",
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .ToListAsync(ct);
    }

    public async Task<UserSummaryDto?> GetUserByIdAsync(int userId, CancellationToken ct = default)
    {
        var u = await _context.Users
            .AsNoTracking()
            .Include(x => x.Role)
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);

        if (u == null) return null;

        return new UserSummaryDto
        {
            UserId = u.UserId,
            Username = u.Username,
            FullName = u.FullName,
            Email = u.Email,
            RoleId = u.RoleId,
            RoleName = u.Role?.RoleName ?? "None",
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt
        };
    }

    public async Task<UserSummaryDto> CreateUserAsync(UserCreateDto dto, CancellationToken ct = default)
    {
        string username = dto.Username?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 4)
        {
            throw new ArgumentException("Password must be at least 4 characters.");
        }

        bool exists = await _context.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower(), ct);
        if (exists)
        {
            throw new InvalidOperationException($"Username '{username}' is already taken.");
        }

        var role = await _context.Roles.FindAsync(new object[] { dto.RoleId }, ct)
            ?? throw new KeyNotFoundException($"Role with ID {dto.RoleId} not found.");

        var (hash, salt) = PasswordHasher.HashPassword(dto.Password);

        var user = new User
        {
            Username = username,
            FullName = dto.FullName?.Trim() ?? username,
            Email = dto.Email?.Trim() ?? string.Empty,
            RoleId = role.RoleId,
            PasswordHash = hash,
            Salt = salt,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.Now
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync(null, "Create", "Security", user.UserId.ToString(), $"Created user '{user.Username}' with role '{role.RoleName}'.", ct);

        return new UserSummaryDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            RoleId = role.RoleId,
            RoleName = role.RoleName,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<UserSummaryDto> UpdateUserAsync(UserUpdateDto dto, CancellationToken ct = default)
    {
        var user = await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == dto.UserId, ct)
            ?? throw new KeyNotFoundException($"User with ID {dto.UserId} not found.");

        var role = await _context.Roles.FindAsync(new object[] { dto.RoleId }, ct)
            ?? throw new KeyNotFoundException($"Role with ID {dto.RoleId} not found.");

        user.FullName = dto.FullName?.Trim() ?? user.Username;
        user.Email = dto.Email?.Trim() ?? string.Empty;
        user.RoleId = role.RoleId;
        user.IsActive = dto.IsActive;

        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync(null, "Edit", "Security", user.UserId.ToString(), $"Updated user details for '{user.Username}'.", ct);

        return new UserSummaryDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            RoleId = role.RoleId,
            RoleName = role.RoleName,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    public async Task<bool> ToggleUserStatusAsync(int userId, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync(new object[] { userId }, ct);
        if (user == null) return false;

        // Prevent deactivating admin account
        if (string.Equals(user.Username, "admin", StringComparison.OrdinalIgnoreCase) && user.IsActive)
        {
            throw new InvalidOperationException("The master 'admin' account cannot be deactivated.");
        }

        user.IsActive = !user.IsActive;
        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync(null, "Edit", "Security", user.UserId.ToString(), $"Toggled active status for '{user.Username}' to {user.IsActive}.", ct);

        return true;
    }

    public async Task<bool> ChangePasswordAsync(UserChangePasswordDto dto, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync(new object[] { dto.UserId }, ct);
        if (user == null) return false;

        bool currentValid = false;
        if (string.IsNullOrWhiteSpace(user.Salt))
        {
            currentValid = (user.PasswordHash == dto.CurrentPassword);
        }
        else
        {
            currentValid = PasswordHasher.VerifyPassword(dto.CurrentPassword, user.PasswordHash, user.Salt);
        }

        if (!currentValid)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 4)
        {
            throw new ArgumentException("New password must be at least 4 characters.");
        }

        var (hash, salt) = PasswordHasher.HashPassword(dto.NewPassword);
        user.PasswordHash = hash;
        user.Salt = salt;
        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync(null, "PasswordChange", "Security", user.UserId.ToString(), $"Password changed by user '{user.Username}'.", ct);
        return true;
    }

    public async Task<bool> ResetPasswordAsync(int userId, string newPassword, CancellationToken ct = default)
    {
        var user = await _context.Users.FindAsync(new object[] { userId }, ct);
        if (user == null) return false;

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
        {
            throw new ArgumentException("Password must be at least 4 characters.");
        }

        var (hash, salt) = PasswordHasher.HashPassword(newPassword);
        user.PasswordHash = hash;
        user.Salt = salt;
        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync(null, "PasswordReset", "Security", user.UserId.ToString(), $"Administrator reset password for '{user.Username}'.", ct);
        return true;
    }

    public async Task<IReadOnlyList<RoleDto>> GetAllRolesAsync(CancellationToken ct = default)
    {
        return await _context.Roles
            .AsNoTracking()
            .Include(r => r.Users)
            .Include(r => r.Permissions)
            .OrderBy(r => r.RoleId)
            .Select(r => new RoleDto
            {
                RoleId = r.RoleId,
                RoleName = r.RoleName,
                Description = r.Description,
                UsersCount = r.Users.Count,
                PermissionKeys = r.Permissions.Select(p => p.PermissionKey).ToList()
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync(int? forRoleId = null, CancellationToken ct = default)
    {
        HashSet<int> grantedIds = new();
        if (forRoleId.HasValue)
        {
            var role = await _context.Roles
                .AsNoTracking()
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r => r.RoleId == forRoleId.Value, ct);

            if (role != null)
            {
                grantedIds = role.Permissions.Select(p => p.PermissionId).ToHashSet();
            }
        }

        var all = await _context.Permissions
            .AsNoTracking()
            .OrderBy(p => p.Module)
            .ThenBy(p => p.PermissionKey)
            .ToListAsync(ct);

        return all.Select(p => new PermissionDto
        {
            PermissionId = p.PermissionId,
            PermissionKey = p.PermissionKey,
            Module = p.Module,
            Description = p.Description,
            IsGranted = forRoleId.HasValue && grantedIds.Contains(p.PermissionId)
        }).ToList();
    }

    public async Task<bool> UpdateRolePermissionsAsync(RolePermissionsUpdateDto dto, CancellationToken ct = default)
    {
        var role = await _context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.RoleId == dto.RoleId, ct);

        if (role == null) return false;

        // Administrator role always retains all permissions
        if (string.Equals(role.RoleName, "Administrator", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var targetPermissions = await _context.Permissions
            .Where(p => dto.PermissionKeys.Contains(p.PermissionKey))
            .ToListAsync(ct);

        role.Permissions.Clear();
        foreach (var p in targetPermissions)
        {
            role.Permissions.Add(p);
        }

        await _context.SaveChangesAsync(ct);

        await _auditService.LogAsync(null, "Edit", "Security", role.RoleId.ToString(), $"Updated permissions for role '{role.RoleName}'.", ct);
        return true;
    }
}
