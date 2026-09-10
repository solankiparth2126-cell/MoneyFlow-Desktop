using System;
using System.Collections.Generic;
using MoneyFlow.Core.Enums;

namespace MoneyFlow.Core.Entities;

public class Group
{
    public int GroupId { get; set; }
    public int CompanyId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int? ParentGroupId { get; set; }
    public GroupNature Nature { get; set; }
    public bool PrimaryGroup { get; set; }
    public bool AffectProfitLoss { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual Company? Company { get; set; }
    public virtual Group? ParentGroup { get; set; }
    public virtual ICollection<Group> SubGroups { get; set; } = new List<Group>();
    public virtual ICollection<Ledger> Ledgers { get; set; } = new List<Ledger>();
}
