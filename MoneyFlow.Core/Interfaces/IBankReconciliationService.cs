using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;

namespace MoneyFlow.Core.Interfaces;

public interface IBankReconciliationService
{
    Task<BankReconciliationReportDto> GetBankReconciliationDataAsync(int companyId, int bankLedgerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<bool> UpdateBankClearanceAsync(int companyId, List<BankClearanceUpdateDto> updates, CancellationToken ct = default);
}
