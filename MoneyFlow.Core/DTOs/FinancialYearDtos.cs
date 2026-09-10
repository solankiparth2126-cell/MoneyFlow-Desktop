using System;

namespace MoneyFlow.Core.DTOs;

public class FinancialYearCreateDto
{
    public string YearName { get; set; } = string.Empty; // e.g. "2027-28"
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class FinancialYearSummaryDto
{
    public int FinancialYearId { get; set; }
    public int CompanyId { get; set; }
    public string YearName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
    public bool IsActive { get; set; }
}
