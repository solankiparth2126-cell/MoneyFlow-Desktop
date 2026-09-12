using System;
using MoneyFlow.Core.Enums;

namespace MoneyFlow.Core.Entities;

public class BillAllocation
{
    public int BillAllocationId { get; set; }
    public int CompanyId { get; set; }
    public int VoucherEntryId { get; set; }
    public int LedgerId { get; set; }
    public BillType BillType { get; set; } = BillType.NewRef;
    public string BillName { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int? CreditDays { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation properties
    public virtual Company? Company { get; set; }
    public virtual VoucherEntry? VoucherEntry { get; set; }
    public virtual Ledger? Ledger { get; set; }
}
