using System.Collections.Generic;
using MoneyFlow.Core.Enums;

namespace MoneyFlow.Core.DTOs;

public class GroupCreateDto
{
    public string GroupName { get; set; } = string.Empty;
    public int? ParentGroupId { get; set; }
    public GroupNature Nature { get; set; }
    public bool AffectProfitLoss { get; set; }
}

public class GroupUpdateDto
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int? ParentGroupId { get; set; }
    public GroupNature Nature { get; set; }
    public bool AffectProfitLoss { get; set; }
}

public class GroupSummaryDto
{
    public int GroupId { get; set; }
    public int CompanyId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int? ParentGroupId { get; set; }
    public string? ParentGroupName { get; set; }
    public GroupNature Nature { get; set; }
    public string NatureDisplay => Nature.ToString();
    public bool PrimaryGroup { get; set; }
    public bool AffectProfitLoss { get; set; }
    public bool IsPredefined { get; set; }
    public int SubGroupsCount { get; set; }
    public int LedgersCount { get; set; }
}

public class GroupTreeNodeDto
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public GroupNature Nature { get; set; }
    public int? ParentGroupId { get; set; }
    public bool IsPredefined { get; set; }
    public List<GroupTreeNodeDto> Children { get; set; } = new List<GroupTreeNodeDto>();
}
