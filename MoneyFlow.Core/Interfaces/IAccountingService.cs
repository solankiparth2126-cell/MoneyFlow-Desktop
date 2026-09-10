using System;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;

namespace MoneyFlow.Core.Interfaces;

public interface IAccountingService
{
    VoucherValidationResult ValidateVoucher(VoucherCreateDto dto, DateTime? fyStartDate = null, DateTime? fyEndDate = null);
    Task<Voucher> SaveVoucherAsync(int companyId, VoucherCreateDto dto, CancellationToken ct = default);
    Task<LedgerBalanceDto> GetLedgerBalanceAsync(int companyId, int ledgerId, DateTime? asOfDate = null, CancellationToken ct = default);
    Task<LedgerStatementDto> GetLedgerStatementAsync(int companyId, int ledgerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<TrialBalanceDto> GetTrialBalanceAsync(int companyId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
}
