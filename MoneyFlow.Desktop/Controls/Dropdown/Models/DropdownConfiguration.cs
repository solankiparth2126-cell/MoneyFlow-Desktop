using System;
using System.Collections.Generic;

namespace MoneyFlow.Desktop.Controls.Dropdown.Models;

/// <summary>
/// Configuration specification governing title, behavior, shortcuts, and columns of a MoneyFlowTallyDropdown instance.
/// </summary>
public class DropdownConfiguration
{
    public MasterType MasterType { get; set; } = MasterType.General;

    /// <summary>
    /// Popup top title, e.g. "LIST OF COUNTRIES", "LIST OF STATES", "LIST OF LEDGERS".
    /// </summary>
    public string HeaderText { get; set; } = "LIST OF RECORDS";

    public string DisplayMember { get; set; } = "Name";
    public string ValueMember { get; set; } = "Id";
    public string CodeMember { get; set; } = "Code";
    public List<string> SearchMembers { get; set; } = new() { "Name", "Code" };

    /// <summary>
    /// Whether direct in-line master creation is enabled (+ New Master [Alt+C]).
    /// </summary>
    public bool AllowCreate { get; set; } = true;
    public string CreateText { get; set; } = "+ New Record";
    public string CreateShortcut { get; set; } = "Alt+C";

    /// <summary>
    /// Whether "Show More [Alt+M]" action is enabled.
    /// </summary>
    public bool AllowShowMore { get; set; } = true;
    public string MoreText { get; set; } = "Show More";
    public string MoreShortcut { get; set; } = "Alt+M";

    public bool ShowStatus { get; set; } = true;
    public bool ShowCode { get; set; } = true;
    public bool ShowCount { get; set; } = true;
    public bool SearchEnabled { get; set; } = true;

    public int PopupWidth { get; set; } = MoneyFlowERPTheme.DefaultPopupWidth;
    public int PopupHeight { get; set; } = MoneyFlowERPTheme.DefaultPopupHeight;
    public int RowHeight { get; set; } = MoneyFlowERPTheme.DefaultRowHeight;

    public string EmptyText { get; set; } = "No records found";
    public string StatusSubtitle { get; set; } = "Configured";

    /// <summary>
    /// If true, newly created item via Alt+C is automatically selected when returned.
    /// </summary>
    public bool AutoSelectCreatedItem { get; set; } = true;

    // ─────────────────────────────────────────────────────────────
    // Factory presets matching reference screenshots
    // ─────────────────────────────────────────────────────────────
    public static DropdownConfiguration ForCountry() => new()
    {
        MasterType = MasterType.Country,
        HeaderText = "LIST OF COUNTRIES",
        CreateText = "+ New Country",
        CreateShortcut = "Alt+C",
        AllowCreate = true,
        AllowShowMore = true,
        MoreText = "Show More",
        MoreShortcut = "Alt+M",
        ShowCode = true,
        ShowStatus = true,
        StatusSubtitle = "Configured",
        PopupWidth = 430,
        PopupHeight = 420
    };

    public static DropdownConfiguration ForState() => new()
    {
        MasterType = MasterType.State,
        HeaderText = "LIST OF STATES",
        CreateText = "+ New State",
        CreateShortcut = "Alt+C",
        AllowCreate = true,
        AllowShowMore = true,
        MoreText = "Show More",
        MoreShortcut = "Alt+M",
        ShowCode = true,
        ShowStatus = false,
        StatusSubtitle = "States / UTs",
        PopupWidth = 420,
        PopupHeight = 410
    };

    public static DropdownConfiguration ForLedger() => new()
    {
        MasterType = MasterType.Ledger,
        HeaderText = "LIST OF LEDGERS",
        CreateText = "+ New Ledger",
        CreateShortcut = "Alt+C",
        AllowCreate = true,
        AllowShowMore = true,
        MoreText = "Show More",
        MoreShortcut = "Alt+M",
        ShowCode = true,
        ShowStatus = true,
        StatusSubtitle = "Active Ledgers",
        SearchMembers = new() { "Name", "Code", "SecondaryText" },
        PopupWidth = 480,
        PopupHeight = 440
    };

    public static DropdownConfiguration ForGroup() => new()
    {
        MasterType = MasterType.Group,
        HeaderText = "LIST OF GROUPS",
        CreateText = "+ New Group",
        CreateShortcut = "Alt+C",
        AllowCreate = true,
        AllowShowMore = false,
        ShowCode = false,
        ShowStatus = true,
        StatusSubtitle = "Primary / Sub Groups",
        PopupWidth = 430,
        PopupHeight = 400
    };

    public static DropdownConfiguration ForCustomer() => new()
    {
        MasterType = MasterType.Customer,
        HeaderText = "LIST OF CUSTOMERS",
        CreateText = "+ New Customer",
        CreateShortcut = "Alt+C",
        AllowCreate = true,
        AllowShowMore = true,
        ShowCode = true,
        ShowStatus = true,
        StatusSubtitle = "Debtors",
        PopupWidth = 490,
        PopupHeight = 440
    };

    public static DropdownConfiguration ForSupplier() => new()
    {
        MasterType = MasterType.Supplier,
        HeaderText = "LIST OF SUPPLIERS",
        CreateText = "+ New Supplier",
        CreateShortcut = "Alt+C",
        AllowCreate = true,
        AllowShowMore = true,
        ShowCode = true,
        ShowStatus = true,
        StatusSubtitle = "Creditors",
        PopupWidth = 490,
        PopupHeight = 440
    };

    public static DropdownConfiguration ForStockItem() => new()
    {
        MasterType = MasterType.StockItem,
        HeaderText = "LIST OF STOCK ITEMS",
        CreateText = "+ New Stock Item",
        CreateShortcut = "Alt+C",
        AllowCreate = true,
        AllowShowMore = true,
        ShowCode = true,
        ShowStatus = true,
        StatusSubtitle = "Inventory Masters",
        PopupWidth = 510,
        PopupHeight = 450
    };
}
