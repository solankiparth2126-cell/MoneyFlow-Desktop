using System;

namespace MoneyFlow.Desktop.Controls.Lookup;

/// <summary>
/// Universal data model representing an item in the MoneyFlow lookup system.
/// Supports flat lists as well as multi-level hierarchical trees.
/// </summary>
public class LookupItem
{
    public object? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Subtitle { get; set; }
    public object? ParentId { get; set; }
    public int Level { get; set; }
    public bool HasChildren { get; set; }
    public bool IsExpanded { get; set; } = true;
    public bool IsSentinel { get; set; }
    public object? RawData { get; set; }

    public bool Matches(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        var q = query.Trim();
        if (Name.StartsWith(q, StringComparison.OrdinalIgnoreCase))
            return true;
        if (Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (!string.IsNullOrEmpty(Code) && Code.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        if (!string.IsNullOrEmpty(Subtitle) && Subtitle.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        return false;
    }

    public override string ToString() => Name;
}
