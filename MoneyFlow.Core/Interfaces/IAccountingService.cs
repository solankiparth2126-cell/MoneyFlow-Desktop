using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Enums;

namespace MoneyFlow.Core.Interfaces;

public interface IAccountingService
{
    VoucherValidationResult ValidateVoucher(VoucherCreateDto dto, DateTime? fyStartDate = null, DateTime? fyEndDate = null);
    Task<Voucher> SaveVoucherAsync(int companyId, VoucherCreateDto dto, CancellationToken ct = default);
    Task<LedgerBalanceDto> GetLedgerBalanceAsync(int companyId, int ledgerId, DateTime? asOfDate = null, CancellationToken ct = default);
    Task<LedgerStatementDto> GetLedgerStatementAsync(int companyId, int ledgerId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<TrialBalanceDto> GetTrialBalanceAsync(int companyId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<VoucherType?> GetVoucherTypeByEnumAsync(VoucherTypeEnum type, CancellationToken ct = default);
    Task<string> GetNextVoucherNumberPreviewAsync(int companyId, int voucherTypeId, int financialYearId, CancellationToken ct = default);
    Task<IReadOnlyList<LedgerSummaryDto>> GetCashAndBankLedgersAsync(int companyId, CancellationToken ct = default);
    Task<IReadOnlyList<LedgerSummaryDto>> GetCustomerPartyLedgersAsync(int companyId, CancellationToken ct = default);
    Task<IReadOnlyList<LedgerSummaryDto>> GetSalesLedgersAsync(int companyId, CancellationToken ct = default);
    Task<IReadOnlyList<LedgerSummaryDto>> GetSupplierPartyLedgersAsync(int companyId, CancellationToken ct = default);
    Task<IReadOnlyList<LedgerSummaryDto>> GetPurchaseLedgersAsync(int companyId, CancellationToken ct = default);
    Task<Voucher?> GetVoucherByIdAsync(int voucherId, CancellationToken ct = default);
    Task<bool> DeleteVoucherAsync(int voucherId, CancellationToken ct = default);
    Task<IReadOnlyList<Voucher>> GetVouchersByTypeAsync(int companyId, int financialYearId, VoucherTypeEnum type, DateTime? fromDate = null, DateTime? toDate = null, CancellationToken ct = default);
    Task<DayBookReportDto> GetDayBookAsync(int companyId, DateTime fromDate, DateTime toDate, VoucherTypeEnum? voucherType = null, CancellationToken ct = default);
    Task<ProfitLossStatementDto> GetProfitAndLossAsync(int companyId, DateTime fromDate, DateTime toDate, CancellationToken ct = default);
    Task<BalanceSheetDto> GetBalanceSheetAsync(int companyId, DateTime asOfDate, CancellationToken ct = default);
}
