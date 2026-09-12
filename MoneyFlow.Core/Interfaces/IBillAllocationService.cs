using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;

namespace MoneyFlow.Core.Interfaces;

public interface IBillAllocationService
{
    Task<IReadOnlyList<PendingBillDto>> GetPendingBillsAsync(int companyId, int ledgerId, CancellationToken ct = default);
    Task<BillAgingSummaryDto> GetBillAgingSummaryAsync(int companyId, int ledgerId, DateTime asOfDate, CancellationToken ct = default);
    Task<IReadOnlyList<BillAgingSummaryDto>> GetAllOutstandingAgingAsync(int companyId, bool isReceivables, DateTime asOfDate, CancellationToken ct = default);
}
