using System;

namespace MoneyFlow.Core.DTOs;

public enum GlobalSearchCategory
{
    All,
    Ledger,
    Voucher,
    StockItem,
    Navigation
}

public class GlobalSearchResultDto
{
    public GlobalSearchCategory Category { get; set; }
    public string CategoryName => Category switch
    {
        GlobalSearchCategory.Ledger => "Ledger / Party",
        GlobalSearchCategory.Voucher => "Voucher",
        GlobalSearchCategory.StockItem => "Stock Item",
        GlobalSearchCategory.Navigation => "Go To Screen",
        _ => "General"
    };

    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public int? EntityId { get; set; }
    public string NavigationTarget { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public DateTime? Date { get; set; }

    public string FormattedAmount => Amount.HasValue ? $"₹{Amount.Value:N2}" : string.Empty;
    public string FormattedDate => Date.HasValue ? Date.Value.ToString("dd-MMM-yyyy") : string.Empty;
}
