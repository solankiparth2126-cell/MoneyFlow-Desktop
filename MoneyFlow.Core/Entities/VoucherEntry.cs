namespace MoneyFlow.Core.Entities;

public class VoucherEntry
{
    public int VoucherEntryId { get; set; }
    public int VoucherId { get; set; }
    public int LedgerId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string Narration { get; set; } = string.Empty;

    // Navigation properties
    public virtual Voucher? Voucher { get; set; }
    public virtual Ledger? Ledger { get; set; }
}
