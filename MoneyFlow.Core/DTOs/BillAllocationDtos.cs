using System;
using System.Collections.Generic;
using MoneyFlow.Core.Enums;

namespace MoneyFlow.Core.DTOs;

public class BillAllocationCreateDto
{
    public int LedgerId { get; set; }
    public BillType BillType { get; set; } = BillType.NewRef;
    public string BillName { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public int? CreditDays { get; set; }
    public decimal Amount { get; set; }
}

public class PendingBillDto
{
    public string BillName { get; set; } = string.Empty;
    public DateTime BillDate { get; set; }
    public DateTime? DueDate { get; set; }
    public int? CreditDays { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal SettledAmount { get; set; }
    public decimal PendingAmount => Math.Max(0, OriginalAmount - SettledAmount);
    public int OverdueDays => DueDate.HasValue && DateTime.Today > DueDate.Value ? (DateTime.Today - DueDate.Value).Days : 0;
    public bool IsOverdue => OverdueDays > 0;
}

public class BillAgingSummaryDto
{
    public int LedgerId { get; set; }
    public string LedgerName { get; set; } = string.Empty;
    public decimal CurrentAmount { get; set; }      // 0-30 days
    public decimal Days31To60 { get; set; }         // 31-60 days
    public decimal Days61To90 { get; set; }         // 61-90 days
    public decimal Over90Days { get; set; }         // >90 days
    public decimal TotalPending => CurrentAmount + Days31To60 + Days61To90 + Over90Days;
    public List<PendingBillDto> Bills { get; set; } = new();
}
