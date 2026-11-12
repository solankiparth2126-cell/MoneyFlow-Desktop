using System;

namespace MoneyFlow.Desktop.Controls.Dropdown.Models;

/// <summary>
/// Universal data item model for MoneyFlow Tally-style dropdown entries.
/// </summary>
public class DropdownItemModel
{
    /// <summary>
    /// Unique identifier or primary key value.
    /// </summary>
    public object? Id { get; set; }

    /// <summary>
    /// Short code or indicator (e.g. "IND (+91)", "GJ").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Primary display name (e.g. "India", "Gujarat").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Secondary subtitle or category text (e.g. "Global", "Current Assets").
    /// </summary>
    public string? SecondaryText { get; set; }

    /// <summary>
    /// Status text (e.g. "Global", "Active", "Configured").
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// Flag indicating whether this is a special system entry (e.g. "◆ Not Applicable").
    /// </summary>
    public bool IsSpecial { get; set; }

    /// <summary>
    /// Flag indicating whether this item can be selected.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Reference to the underlying master entity.
    /// </summary>
    public object? Tag { get; set; }

    public DropdownItemModel()
    {
    }

    public DropdownItemModel(object? id, string name, string code = "", string? secondaryText = null, bool isSpecial = false, object? tag = null)
    {
        Id = id;
        Name = name ?? string.Empty;
        Code = code ?? string.Empty;
        SecondaryText = secondaryText;
        IsSpecial = isSpecial;
        Tag = tag;
    }

    public static DropdownItemModel CreateSpecial(string name, object? id = null, string code = "Global")
    {
        return new DropdownItemModel
        {
            Id = id,
            Name = name,
            Code = code,
            IsSpecial = true,
            Status = "Global"
        };
    }

    public override string ToString() => string.IsNullOrWhiteSpace(Code) ? Name : $"{Name} ({Code})";
}
