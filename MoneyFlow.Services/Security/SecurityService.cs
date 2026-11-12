using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Services.Security;

public class SecurityService : ISecurityService
{
    private readonly IRepository<User> _userRepo;
    private readonly IRepository<Role> _roleRepo;
    private readonly IRepository<Permission> _permissionRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserContext _userContext;
    private readonly IAuditService _auditService;
    private readonly ILogger<SecurityService> _logger;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public SecurityService(
        IRepository<User> userRepo,
        IRepository<Role> roleRepo,
        IRepository<Permission> permissionRepo,
        IUnitOfWork unitOfWork,
        IUserContext userContext,
        IAuditService auditService,
        ILogger<SecurityService> logger)
    {
        _userRepo = userRepo;
        _roleRepo = roleRepo;
        _permissionRepo = permissionRepo;
        _unitOfWork = unitOfWork;
        _userContext = userContext;
        _auditService = auditService;
        _logger = logger;
    }

    public SecurityService(
        IRepository<User> userRepo,
        IRepository<Role> roleRepo,
        IRepository<Permission> permissionRepo,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        ILogger<SecurityService> logger)
        : this(userRepo, roleRepo, permissionRepo, unitOfWork, new UserContext(), auditService, logger)
    {
    }

    internal SecurityService(
        MoneyFlow.Data.AppDbContext context,
        IUserContext userContext,
        IAuditService auditService,
        ILogger<SecurityService> logger)
        : this(
            new MoneyFlow.Data.Repositories.Repository<User>(context),
            new MoneyFlow.Data.Repositories.Repository<Role>(context),
            new MoneyFlow.Data.Repositories.Repository<Permission>(context),
            new MoneyFlow.Data.Repositories.UnitOfWork(context),
            userContext,
            auditService,
            logger)
    {
    }

    public async Task<AuthenticationResultDto> AuthenticateAsync(string username, string password, CancellationToken ct = default)
    {
        var cleanUsername = username?.Trim() ?? string.Empty;
        var users = await _userRepo.GetAllAsync(ct);
        var user = users.FirstOrDefault(u => u.Username.Equals(cleanUsername, StringComparison.OrdinalIgnoreCase));

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

        if (string.IsNullOrWhiteSpace(user.Salt))
        {
            if (user.PasswordHash == password)
            {
                passwordValid = true;
                var (newHash, newSalt) = PasswordHasher.HashPassword(password);
                user.PasswordHash = newHash;
                user.Salt = newSalt;
                _userRepo.Update(user);
                await _unitOfWork.SaveChangesAsync(ct);
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

        user.LastLoginAt = DateTime.UtcNow;
        _userRepo.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        var roles = await _roleRepo.GetAllAsync(ct);
        var role = roles.FirstOrDefault(r => r.RoleId == user.RoleId);

        var permissions = role?.Permissions.Select(p => p.PermissionKey).ToList() ?? new List<string>();

        _userContext.SetCurrentUser(
            user.UserId,
            user.Username,
            user.FullName,
            role?.RoleName ?? "User",
            permissions);

        await _auditService.LogAsync(null, "LoginSuccess", "Security", user.UserId.ToString(), $"User '{user.Username}' logged in successfully.", ct);

        return new AuthenticationResultDto
        {
            IsSuccess = true,
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            RoleName = role?.RoleName ?? "User",
            Permissions = permissions,
            Message = "Authentication successful."
        };
    }

    public async Task<IReadOnlyList<UserSummaryDto>> GetAllUsersAsync(CancellationToken ct = default)
    {
        var users = await _userRepo.GetAllAsync(ct);
        var roles = (await _roleRepo.GetAllAsync(ct)).ToDictionary(r => r.RoleId);

        return users
            .OrderBy(u => u.Username)
            .Select(u => new UserSummaryDto
            {
                UserId = u.UserId,
                Username = u.Username,
                FullName = u.FullName,
                Email = u.Email,
                RoleId = u.RoleId,
                RoleName = roles.TryGetValue(u.RoleId, out var r) ? r.RoleName : "User",
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .ToList();
    }

    public async Task<UserSummaryDto?> GetUserByIdAsync(int userId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user == null) return null;

        var role = await _roleRepo.GetByIdAsync(user.RoleId, ct);

        return new UserSummaryDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            RoleId = user.RoleId,
            RoleName = role?.RoleName ?? "User",
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    public async Task<UserSummaryDto> CreateUserAsync(UserCreateDto dto, CancellationToken ct = default)
    {
        var cleanUsername = dto.Username.Trim();
        bool exists = await _userRepo.AnyAsync(u => u.Username.ToLower() == cleanUsername.ToLower(), ct);
        if (exists)
        {
            throw new InvalidOperationException($"Username '{cleanUsername}' already exists.");
        }

        var (hash, salt) = PasswordHasher.HashPassword(dto.Password);

        var user = new User
        {
            Username = cleanUsername,
            FullName = dto.FullName?.Trim() ?? string.Empty,
            Email = dto.Email?.Trim() ?? string.Empty,
            RoleId = dto.RoleId,
            PasswordHash = hash,
            Salt = salt,
            IsActive = dto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepo.AddAsync(user, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        var role = await _roleRepo.GetByIdAsync(user.RoleId, ct);

        await _auditService.LogAsync(null, "CreateUser", "Security", user.UserId.ToString(), $"Created user '{user.Username}' with role '{role?.RoleName}'.", ct);

        return new UserSummaryDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            RoleId = user.RoleId,
            RoleName = role?.RoleName ?? "User",
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt
        };
    }

    public async Task<UserSummaryDto> UpdateUserAsync(UserUpdateDto dto, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(dto.UserId, ct)
            ?? throw new KeyNotFoundException($"User with ID {dto.UserId} not found.");

        user.FullName = dto.FullName?.Trim() ?? string.Empty;
        user.Email = dto.Email?.Trim() ?? string.Empty;
        user.RoleId = dto.RoleId;
        user.IsActive = dto.IsActive;

        _userRepo.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        var role = await _roleRepo.GetByIdAsync(user.RoleId, ct);

        await _auditService.LogAsync(null, "UpdateUser", "Security", user.UserId.ToString(), $"Updated user profile for '{user.Username}'.", ct);

        return new UserSummaryDto
        {
            UserId = user.UserId,
            Username = user.Username,
            FullName = user.FullName,
            Email = user.Email,
            RoleId = user.RoleId,
            RoleName = role?.RoleName ?? "User",
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt
        };
    }

    public async Task<bool> ToggleUserStatusAsync(int userId, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user == null) return false;

        if (string.Equals(user.Username, "admin", StringComparison.OrdinalIgnoreCase) && user.IsActive)
        {
            throw new InvalidOperationException("The master 'admin' account cannot be deactivated.");
        }

        user.IsActive = !user.IsActive;
        _userRepo.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        await _auditService.LogAsync(null, "Edit", "Security", user.UserId.ToString(), $"Toggled active status for '{user.Username}' to {user.IsActive}.", ct);
        return true;
    }

    public async Task<bool> ChangePasswordAsync(UserChangePasswordDto dto, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(dto.UserId, ct);
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
        _userRepo.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        await _auditService.LogAsync(null, "PasswordChange", "Security", user.UserId.ToString(), $"Password changed by user '{user.Username}'.", ct);
        return true;
    }

    public async Task<bool> ResetPasswordAsync(int userId, string newPassword, CancellationToken ct = default)
    {
        var user = await _userRepo.GetByIdAsync(userId, ct);
        if (user == null) return false;

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
        {
            throw new ArgumentException("Password must be at least 4 characters.");
        }

        var (hash, salt) = PasswordHasher.HashPassword(newPassword);
        user.PasswordHash = hash;
        user.Salt = salt;
        _userRepo.Update(user);
        await _unitOfWork.SaveChangesAsync(ct);

        await _auditService.LogAsync(null, "PasswordReset", "Security", user.UserId.ToString(), $"Administrator reset password for '{user.Username}'.", ct);
        return true;
    }

    public async Task<IReadOnlyList<RoleDto>> GetAllRolesAsync(CancellationToken ct = default)
    {
        var roles = await _roleRepo.GetAllAsync(ct);
        var users = await _userRepo.GetAllAsync(ct);

        return roles.Select(r => new RoleDto
        {
            RoleId = r.RoleId,
            RoleName = r.RoleName,
            Description = r.Description,
            UsersCount = users.Count(u => u.RoleId == r.RoleId),
            PermissionKeys = r.Permissions.Select(p => p.PermissionKey).ToList()
        }).ToList();
    }

    public async Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync(int? forRoleId = null, CancellationToken ct = default)
    {
        HashSet<int> grantedIds = new();
        if (forRoleId.HasValue)
        {
            var role = await _roleRepo.GetByIdAsync(forRoleId.Value, ct);
            if (role != null)
            {
                grantedIds = role.Permissions.Select(p => p.PermissionId).ToHashSet();
            }
        }

        var all = await _permissionRepo.GetAllAsync(ct);

        return all
            .OrderBy(p => p.Module)
            .ThenBy(p => p.PermissionKey)
            .Select(p => new PermissionDto
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
        var role = await _roleRepo.GetByIdAsync(dto.RoleId, ct);
        if (role == null) return false;

        if (string.Equals(role.RoleName, "Administrator", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var allPermissions = await _permissionRepo.GetAllAsync(ct);
        var targetPermissions = allPermissions.Where(p => dto.PermissionKeys.Contains(p.PermissionKey)).ToList();

        role.Permissions.Clear();
        foreach (var p in targetPermissions)
        {
            role.Permissions.Add(p);
        }

        _roleRepo.Update(role);
        await _unitOfWork.SaveChangesAsync(ct);

        await _auditService.LogAsync(null, "Edit", "Security", role.RoleId.ToString(), $"Updated permissions for role '{role.RoleName}'.", ct);
        return true;
    }
}
