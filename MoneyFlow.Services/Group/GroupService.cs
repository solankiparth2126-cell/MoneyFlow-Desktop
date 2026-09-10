using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;

namespace MoneyFlow.Services.Group;

public class GroupService : IGroupService
{
    private readonly AppDbContext _context;
    private readonly IGroupRepository _groupRepo;
    private readonly ICompanyRepository _companyRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GroupService> _logger;

    public GroupService(
        AppDbContext context,
        IGroupRepository groupRepo,
        ICompanyRepository companyRepo,
        IUnitOfWork unitOfWork,
        ILogger<GroupService> logger)
    {
        _context = context;
        _groupRepo = groupRepo;
        _companyRepo = companyRepo;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Core.Entities.Group> CreateGroupAsync(int companyId, GroupCreateDto dto, CancellationToken ct = default)
    {
        var company = await _companyRepo.GetByIdAsync(companyId, ct)
            ?? throw new KeyNotFoundException($"Company with ID {companyId} not found.");

        if (string.IsNullOrWhiteSpace(dto.GroupName))
        {
            throw new ArgumentException("Group name is required.", nameof(dto.GroupName));
        }

        string trimmedName = dto.GroupName.Trim();
        bool exists = await _groupRepo.GroupNameExistsAsync(companyId, trimmedName, null, ct);
        if (exists)
        {
            throw new InvalidOperationException($"A group named '{trimmedName}' already exists in this company.");
        }

        var group = new Core.Entities.Group
        {
            CompanyId = companyId,
            GroupName = trimmedName,
            CreatedAt = DateTime.Now,
            IsActive = true
        };

        if (dto.ParentGroupId.HasValue)
        {
            var parent = await _groupRepo.GetByIdAsync(dto.ParentGroupId.Value, ct)
                ?? throw new KeyNotFoundException($"Parent group with ID {dto.ParentGroupId.Value} not found.");

            if (parent.CompanyId != companyId)
            {
                throw new InvalidOperationException("Parent group must belong to the same company.");
            }

            group.ParentGroupId = parent.GroupId;
            group.Nature = parent.Nature;
            group.PrimaryGroup = false;
            group.AffectProfitLoss = parent.AffectProfitLoss;
        }
        else
        {
            group.ParentGroupId = null;
            group.Nature = dto.Nature;
            group.PrimaryGroup = true;
            group.AffectProfitLoss = (dto.Nature == GroupNature.Income || dto.Nature == GroupNature.Expenses);
        }

        await _groupRepo.AddAsync(group, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Group '{GroupName}' created for Company ID {CompanyId}.", group.GroupName, companyId);
        return group;
    }

    public async Task<Core.Entities.Group> UpdateGroupAsync(GroupUpdateDto dto, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(dto.GroupId, ct)
            ?? throw new KeyNotFoundException($"Group with ID {dto.GroupId} not found.");

        string trimmedName = dto.GroupName.Trim();
        bool exists = await _groupRepo.GroupNameExistsAsync(group.CompanyId, trimmedName, group.GroupId, ct);
        if (exists)
        {
            throw new InvalidOperationException($"Another group named '{trimmedName}' already exists in this company.");
        }

        if (dto.ParentGroupId.HasValue)
        {
            if (dto.ParentGroupId.Value == group.GroupId)
            {
                throw new InvalidOperationException("A group cannot be its own parent.");
            }

            // Cycle detection: ensure target parent is not a descendant of this group
            var descendantIds = await GetDescendantGroupIdsAsync(group.CompanyId, group.GroupId, ct);
            if (descendantIds.Contains(dto.ParentGroupId.Value))
            {
                throw new InvalidOperationException("Cannot set a descendant group as the parent (circular reference).");
            }

            var parent = await _groupRepo.GetByIdAsync(dto.ParentGroupId.Value, ct)
                ?? throw new KeyNotFoundException($"Parent group with ID {dto.ParentGroupId.Value} not found.");

            group.ParentGroupId = parent.GroupId;
            group.Nature = parent.Nature;
            group.PrimaryGroup = false;
            group.AffectProfitLoss = parent.AffectProfitLoss;
        }
        else
        {
            group.ParentGroupId = null;
            group.Nature = dto.Nature;
            group.PrimaryGroup = true;
            group.AffectProfitLoss = (dto.Nature == GroupNature.Income || dto.Nature == GroupNature.Expenses);
        }

        group.GroupName = trimmedName;
        group.UpdatedAt = DateTime.Now;

        _groupRepo.Update(group);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Group '{GroupName}' updated.", group.GroupName);
        return group;
    }

    public async Task<bool> DeleteGroupAsync(int groupId, CancellationToken ct = default)
    {
        var group = await _groupRepo.GetByIdAsync(groupId, ct);
        if (group == null) return false;

        // Check if group has sub-groups
        bool hasSubGroups = await _context.Groups.AnyAsync(g => g.ParentGroupId == groupId && g.IsActive, ct);
        if (hasSubGroups)
        {
            throw new InvalidOperationException($"Cannot delete group '{group.GroupName}' because it contains sub-groups. Move or delete the sub-groups first.");
        }

        // Check if group has ledgers
        bool hasLedgers = await _context.Ledgers.AnyAsync(l => l.GroupId == groupId && l.IsActive, ct);
        if (hasLedgers)
        {
            throw new InvalidOperationException($"Cannot delete group '{group.GroupName}' because it contains ledgers. Reassign or delete the ledgers first.");
        }

        _groupRepo.Delete(group);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Group '{GroupName}' deleted.", group.GroupName);
        return true;
    }

    public async Task<Core.Entities.Group?> GetGroupByIdAsync(int groupId, CancellationToken ct = default)
    {
        return await _context.Groups
            .Include(g => g.ParentGroup)
            .Include(g => g.SubGroups)
            .Include(g => g.Ledgers)
            .FirstOrDefaultAsync(g => g.GroupId == groupId, ct);
    }

    public async Task<IReadOnlyList<GroupSummaryDto>> GetGroupsByCompanyAsync(int companyId, string? searchTerm = null, CancellationToken ct = default)
    {
        var query = _context.Groups
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId && g.IsActive);

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            string term = searchTerm.Trim().ToLower();
            query = query.Where(g => g.GroupName.ToLower().Contains(term));
        }

        var groups = await query
            .Include(g => g.ParentGroup)
            .Include(g => g.SubGroups)
            .Include(g => g.Ledgers)
            .OrderBy(g => g.GroupName)
            .ToListAsync(ct);

        return groups.Select(g => new GroupSummaryDto
        {
            GroupId = g.GroupId,
            CompanyId = g.CompanyId,
            GroupName = g.GroupName,
            ParentGroupId = g.ParentGroupId,
            ParentGroupName = g.ParentGroup?.GroupName,
            Nature = g.Nature,
            PrimaryGroup = g.PrimaryGroup,
            AffectProfitLoss = g.AffectProfitLoss,
            SubGroupsCount = g.SubGroups.Count(s => s.IsActive),
            LedgersCount = g.Ledgers.Count(l => l.IsActive)
        }).ToList();
    }

    public async Task<IReadOnlyList<GroupTreeNodeDto>> GetGroupTreeAsync(int companyId, CancellationToken ct = default)
    {
        var allGroups = await _context.Groups
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId && g.IsActive)
            .OrderBy(g => g.GroupName)
            .ToListAsync(ct);

        var roots = allGroups.Where(g => g.ParentGroupId == null).ToList();
        var tree = new List<GroupTreeNodeDto>();

        foreach (var root in roots)
        {
            tree.Add(BuildTreeNode(root, allGroups));
        }

        return tree;
    }

    public async Task<IReadOnlyList<Core.Entities.Group>> GetPotentialParentGroupsAsync(int companyId, int? currentGroupId = null, CancellationToken ct = default)
    {
        var allGroups = await _context.Groups
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId && g.IsActive)
            .OrderBy(g => g.GroupName)
            .ToListAsync(ct);

        if (!currentGroupId.HasValue)
        {
            return allGroups;
        }

        var descendantIds = await GetDescendantGroupIdsAsync(companyId, currentGroupId.Value, ct);
        descendantIds.Add(currentGroupId.Value);

        return allGroups.Where(g => !descendantIds.Contains(g.GroupId)).ToList();
    }

    private GroupTreeNodeDto BuildTreeNode(Core.Entities.Group current, List<Core.Entities.Group> allGroups)
    {
        var node = new GroupTreeNodeDto
        {
            GroupId = current.GroupId,
            GroupName = current.GroupName,
            Nature = current.Nature,
            ParentGroupId = current.ParentGroupId
        };

        var children = allGroups.Where(g => g.ParentGroupId == current.GroupId).ToList();
        foreach (var child in children)
        {
            node.Children.Add(BuildTreeNode(child, allGroups));
        }

        return node;
    }

    private async Task<HashSet<int>> GetDescendantGroupIdsAsync(int companyId, int rootGroupId, CancellationToken ct)
    {
        var allGroups = await _context.Groups
            .AsNoTracking()
            .Where(g => g.CompanyId == companyId && g.IsActive)
            .Select(g => new { g.GroupId, g.ParentGroupId })
            .ToListAsync(ct);

        var descendants = new HashSet<int>();
        CollectDescendants(rootGroupId, allGroups, descendants);
        return descendants;
    }

    private void CollectDescendants(int currentId, IEnumerable<dynamic> allGroups, HashSet<int> accumulator)
    {
        var directChildren = allGroups.Where(g => g.ParentGroupId == currentId).Select(g => (int)g.GroupId);
        foreach (var childId in directChildren)
        {
            if (accumulator.Add(childId))
            {
                CollectDescendants(childId, allGroups, accumulator);
            }
        }
    }
}
