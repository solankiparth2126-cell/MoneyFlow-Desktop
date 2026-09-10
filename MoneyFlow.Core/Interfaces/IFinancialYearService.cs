using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Core.Interfaces;

public interface IFinancialYearService
{
    Task<FinancialYear> CreateFinancialYearAsync(int companyId, FinancialYearCreateDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<FinancialYearSummaryDto>> GetFinancialYearsByCompanyAsync(int companyId, CancellationToken ct = default);
    Task<bool> SetActiveFinancialYearAsync(int financialYearId, CancellationToken ct = default);
    bool ValidateDateInCurrentFY(DateTime transactionDate, out string errorMessage);
    Task<bool> CloseFinancialYearAsync(int financialYearId, CancellationToken ct = default);
    FinancialYear? GetCurrentFinancialYear();
}
