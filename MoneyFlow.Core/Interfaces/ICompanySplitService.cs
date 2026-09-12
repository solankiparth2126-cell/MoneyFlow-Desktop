using System;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Core.Interfaces;

public interface ICompanySplitService
{
    Task<bool> ValidateSplitEligibilityAsync(int companyId, DateTime splitFromDate, CancellationToken ct = default);
    Task<Company> SplitCompanyDataAsync(CompanySplitDto dto, CancellationToken ct = default);
    Task<Company> SplitCompanyAsync(CompanySplitDto dto, CancellationToken ct = default);
}
