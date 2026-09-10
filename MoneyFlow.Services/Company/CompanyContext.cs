using System;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Services.Company;

public class CompanyContext : ICompanyContext
{
    public Core.Entities.Company? CurrentCompany { get; private set; }
    public FinancialYear? CurrentFinancialYear { get; private set; }
    public bool IsCompanyOpen => CurrentCompany != null;

    public event Action? OnCompanyChanged;

    public void SetActiveCompany(Core.Entities.Company company, FinancialYear? financialYear)
    {
        CurrentCompany = company ?? throw new ArgumentNullException(nameof(company));
        CurrentFinancialYear = financialYear;
        OnCompanyChanged?.Invoke();
    }

    public void CloseCompany()
    {
        CurrentCompany = null;
        CurrentFinancialYear = null;
        OnCompanyChanged?.Invoke();
    }
}
