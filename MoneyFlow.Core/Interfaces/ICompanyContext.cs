using System;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Core.Interfaces;

public interface ICompanyContext
{
    Company? CurrentCompany { get; }
    FinancialYear? CurrentFinancialYear { get; }
    bool IsCompanyOpen { get; }
    
    event Action? OnCompanyChanged;

    void SetActiveCompany(Company company, FinancialYear? financialYear);
    void SetActiveFinancialYear(FinancialYear financialYear);
    void CloseCompany();
}
