using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Core.Interfaces;

public interface IGroupService
{
    Task<Group> CreateGroupAsync(int companyId, GroupCreateDto dto, CancellationToken ct = default);
    Task<Group> UpdateGroupAsync(GroupUpdateDto dto, CancellationToken ct = default);
    Task<bool> DeleteGroupAsync(int groupId, CancellationToken ct = default);
    Task<Group?> GetGroupByIdAsync(int groupId, CancellationToken ct = default);
    Task<IReadOnlyList<GroupSummaryDto>> GetGroupsByCompanyAsync(int companyId, string? searchTerm = null, CancellationToken ct = default);
    Task<IReadOnlyList<GroupTreeNodeDto>> GetGroupTreeAsync(int companyId, CancellationToken ct = default);
    Task<IReadOnlyList<Group>> GetPotentialParentGroupsAsync(int companyId, int? currentGroupId = null, CancellationToken ct = default);
}
