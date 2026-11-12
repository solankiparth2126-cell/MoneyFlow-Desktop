using System;
using MoneyFlow.Desktop.Controls.Dropdown.Models;

namespace MoneyFlow.Desktop.Controls.Dropdown.Events;

public class DropdownItemSelectedEventArgs : EventArgs
{
    public DropdownItemModel? Item { get; }
    public object? Value => Item?.Id;
    public string Text => Item?.Name ?? string.Empty;

    public DropdownItemSelectedEventArgs(DropdownItemModel? item)
    {
        Item = item;
    }
}

public class DropdownCreateEventArgs : EventArgs
{
    public MasterType MasterType { get; }
    public bool Handled { get; set; }
    public DropdownItemModel? CreatedItem { get; set; }

    public DropdownCreateEventArgs(MasterType masterType)
    {
        MasterType = masterType;
    }
}

public class DropdownMoreEventArgs : EventArgs
{
    public MasterType MasterType { get; }
    public bool Handled { get; set; }

    public DropdownMoreEventArgs(MasterType masterType)
    {
        MasterType = masterType;
    }
}
