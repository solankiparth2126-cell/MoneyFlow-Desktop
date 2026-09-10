using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Core.Interfaces;

public interface ILedgerService
{
    Task<Ledger> CreateLedgerAsync(int companyId, LedgerCreateDto dto, CancellationToken ct = default);
    Task<Ledger> UpdateLedgerAsync(LedgerUpdateDto dto, CancellationToken ct = default);
    Task<bool> DeleteLedgerAsync(int ledgerId, CancellationToken ct = default);
    Task<Ledger?> GetLedgerByIdAsync(int ledgerId, CancellationToken ct = default);
    Task<LedgerDetailDto?> GetLedgerDetailsAsync(int ledgerId, CancellationToken ct = default);
    Task<IReadOnlyList<LedgerSummaryDto>> GetLedgersByCompanyAsync(int companyId, string? searchTerm = null, int? groupId = null, CancellationToken ct = default);
    Task<bool> ValidateLedgerNameAsync(int companyId, string ledgerName, int? excludeLedgerId = null, CancellationToken ct = default);
}
