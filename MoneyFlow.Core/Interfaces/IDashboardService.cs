using System;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;

namespace MoneyFlow.Core.Interfaces;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardDataAsync(int companyId, DateTime asOfDate, CancellationToken ct = default);
}
