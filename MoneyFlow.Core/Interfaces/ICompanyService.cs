using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Core.Interfaces;

public interface ICompanyService
{
    Task<Company> CreateCompanyAsync(CompanyCreateDto dto, CancellationToken ct = default);
    Task<Company> UpdateCompanyAsync(CompanyUpdateDto dto, CancellationToken ct = default);
    Task<Company?> GetCompanyByIdAsync(int companyId, CancellationToken ct = default);
    Task<IReadOnlyList<CompanySummaryDto>> GetAllCompaniesAsync(CancellationToken ct = default);
    Task<bool> DeleteCompanyAsync(int companyId, CancellationToken ct = default);
    Task<bool> OpenCompanyAsync(int companyId, CancellationToken ct = default);
    void CloseCompany();
}
