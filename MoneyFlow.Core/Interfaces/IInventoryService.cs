using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MoneyFlow.Core.DTOs;

namespace MoneyFlow.Core.Interfaces;

public interface IInventoryService
{
    // Units of Measure
    Task<IReadOnlyList<UnitDto>> GetUnitsByCompanyAsync(int companyId, CancellationToken ct = default);
    Task<UnitDto?> GetUnitByIdAsync(int unitId, CancellationToken ct = default);
    Task<UnitDto> CreateUnitAsync(int companyId, UnitCreateDto dto, CancellationToken ct = default);
    Task<UnitDto> UpdateUnitAsync(int unitId, UnitUpdateDto dto, CancellationToken ct = default);
    Task<bool> DeleteUnitAsync(int unitId, CancellationToken ct = default);

    // Stock Items
    Task<IReadOnlyList<StockItemDto>> GetStockItemsByCompanyAsync(int companyId, CancellationToken ct = default);
    Task<StockItemDto?> GetStockItemByIdAsync(int stockItemId, CancellationToken ct = default);
    Task<StockItemDto> CreateStockItemAsync(int companyId, StockItemCreateDto dto, CancellationToken ct = default);
    Task<StockItemDto> UpdateStockItemAsync(int stockItemId, StockItemUpdateDto dto, CancellationToken ct = default);
    Task<bool> DeleteStockItemAsync(int stockItemId, CancellationToken ct = default);

    // Stock Summary
    Task<StockSummaryReportDto> GetStockSummaryAsync(int companyId, DateTime? asOfDate = null, CancellationToken ct = default);
}
