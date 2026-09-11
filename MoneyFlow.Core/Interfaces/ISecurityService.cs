using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;

namespace MoneyFlow.Core.Interfaces;

public interface ISecurityService
{
    Task<AuthenticationResultDto> AuthenticateAsync(string username, string password, CancellationToken ct = default);
    Task<IReadOnlyList<UserSummaryDto>> GetAllUsersAsync(CancellationToken ct = default);
    Task<UserSummaryDto?> GetUserByIdAsync(int userId, CancellationToken ct = default);
    Task<UserSummaryDto> CreateUserAsync(UserCreateDto dto, CancellationToken ct = default);
    Task<UserSummaryDto> UpdateUserAsync(UserUpdateDto dto, CancellationToken ct = default);
    Task<bool> ToggleUserStatusAsync(int userId, CancellationToken ct = default);
    Task<bool> ChangePasswordAsync(UserChangePasswordDto dto, CancellationToken ct = default);
    Task<bool> ResetPasswordAsync(int userId, string newPassword, CancellationToken ct = default);

    Task<IReadOnlyList<RoleDto>> GetAllRolesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<PermissionDto>> GetAllPermissionsAsync(int? forRoleId = null, CancellationToken ct = default);
    Task<bool> UpdateRolePermissionsAsync(RolePermissionsUpdateDto dto, CancellationToken ct = default);
}
