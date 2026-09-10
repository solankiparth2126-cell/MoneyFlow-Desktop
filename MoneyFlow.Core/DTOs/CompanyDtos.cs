using System;

namespace MoneyFlow.Core.DTOs;

public class CompanyCreateDto
{
    public string CompanyName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = "India";
    public string PAN { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public DateTime FinancialYearFrom { get; set; } = new DateTime(2026, 4, 1);
    public DateTime BooksBeginningFrom { get; set; } = new DateTime(2026, 4, 1);
    public string Currency { get; set; } = "₹";
    public bool CreateDefaultLedgers { get; set; } = true;
}

public class CompanyUpdateDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string Country { get; set; } = "India";
    public string PAN { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Currency { get; set; } = "₹";
    public bool IsActive { get; set; } = true;
}

public class CompanySummaryDto
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime FinancialYearFrom { get; set; }
    public DateTime BooksBeginningFrom { get; set; }
    public string Currency { get; set; } = "₹";
    public bool IsActive { get; set; }
}
