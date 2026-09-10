using System;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Services.Company;

public class CompanyContext : ICompanyContext
{
    public Core.Entities.Company? CurrentCompany { get; private set; }
    public Core.Entities.FinancialYear? CurrentFinancialYear { get; private set; }
    public bool IsCompanyOpen => CurrentCompany != null;

    public event Action? OnCompanyChanged;

    public void SetActiveCompany(Core.Entities.Company company, Core.Entities.FinancialYear? financialYear)
    {
        CurrentCompany = company ?? throw new ArgumentNullException(nameof(company));
        CurrentFinancialYear = financialYear;
        OnCompanyChanged?.Invoke();
    }

    public void SetActiveFinancialYear(Core.Entities.FinancialYear financialYear)
    {
        CurrentFinancialYear = financialYear ?? throw new ArgumentNullException(nameof(financialYear));
        OnCompanyChanged?.Invoke();
    }

    public void CloseCompany()
    {
        CurrentCompany = null;
        CurrentFinancialYear = null;
        OnCompanyChanged?.Invoke();
    }
}
