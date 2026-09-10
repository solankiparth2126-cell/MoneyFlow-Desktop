using System;
using System.Collections.Generic;

namespace MoneyFlow.Core.Entities;

public class Company
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = "India";
    public string PAN { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime FinancialYearFrom { get; set; }
    public DateTime BooksBeginningFrom { get; set; }
    public string Currency { get; set; } = "₹";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual ICollection<FinancialYear> FinancialYears { get; set; } = new List<FinancialYear>();
    public virtual ICollection<Group> Groups { get; set; } = new List<Group>();
    public virtual ICollection<Ledger> Ledgers { get; set; } = new List<Ledger>();
    public virtual ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
    public virtual ICollection<StockItem> StockItems { get; set; } = new List<StockItem>();
    public virtual ICollection<Unit> Units { get; set; } = new List<Unit>();
}
