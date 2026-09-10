using System;
using System.Collections.Generic;

namespace MoneyFlow.Core.Entities;

public class Voucher
{
    public int VoucherId { get; set; }
    public int CompanyId { get; set; }
    public int FinancialYearId { get; set; }
    public int VoucherTypeId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateTime VoucherDate { get; set; }
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Narration { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ModifiedAt { get; set; }
    public string CreatedBy { get; set; } = "Admin";
    public string ModifiedBy { get; set; } = string.Empty;
    public bool IsDeleted { get; set; } = false;

    // Navigation properties
    public virtual Company? Company { get; set; }
    public virtual FinancialYear? FinancialYear { get; set; }
    public virtual VoucherType? VoucherType { get; set; }
    public virtual ICollection<VoucherEntry> VoucherEntries { get; set; } = new List<VoucherEntry>();
}
