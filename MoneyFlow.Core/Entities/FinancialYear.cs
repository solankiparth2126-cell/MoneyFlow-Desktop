using System;
using System.Collections.Generic;

namespace MoneyFlow.Core.Entities;

public class FinancialYear
{
    public int FinancialYearId { get; set; }
    public int CompanyId { get; set; }
    public string YearName { get; set; } = string.Empty; // e.g. "2026-27"
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation properties
    public virtual Company? Company { get; set; }
    public virtual ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
}
