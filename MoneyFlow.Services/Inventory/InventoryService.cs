using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;
using MoneyFlow.Data;

namespace MoneyFlow.Services.Inventory;

public class InventoryService : IInventoryService
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InventoryService> _logger;

    public InventoryService(
        AppDbContext context,
        IUnitOfWork unitOfWork,
        ILogger<InventoryService> logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    #region Units of Measure

    public async Task<IReadOnlyList<UnitDto>> GetUnitsByCompanyAsync(int companyId, CancellationToken ct = default)
    {
        return await _context.Units
            .AsNoTracking()
            .Where(u => u.CompanyId == companyId)
            .OrderBy(u => u.UnitName)
            .Select(u => new UnitDto
            {
                UnitId = u.UnitId,
                CompanyId = u.CompanyId,
                UnitName = u.UnitName,
                FormalName = u.FormalName,
                DecimalPlaces = u.DecimalPlaces,
                IsActive = u.IsActive
            })
            .ToListAsync(ct);
    }

    public async Task<UnitDto?> GetUnitByIdAsync(int unitId, CancellationToken ct = default)
    {
        var unit = await _context.Units
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UnitId == unitId, ct);

        if (unit == null) return null;

        return new UnitDto
        {
            UnitId = unit.UnitId,
            CompanyId = unit.CompanyId,
            UnitName = unit.UnitName,
            FormalName = unit.FormalName,
            DecimalPlaces = unit.DecimalPlaces,
            IsActive = unit.IsActive
        };
    }

    public async Task<UnitDto> CreateUnitAsync(int companyId, UnitCreateDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.UnitName))
            throw new ArgumentException("Unit Symbol / Name is required.", nameof(dto.UnitName));

        var trimmedName = dto.UnitName.Trim();

        // Unique validation per company
        bool exists = await _context.Units
            .AnyAsync(u => u.CompanyId == companyId && u.UnitName.ToLower() == trimmedName.ToLower(), ct);

        if (exists)
            throw new InvalidOperationException($"A Unit with the symbol '{trimmedName}' already exists.");

        var unit = new Unit
        {
            CompanyId = companyId,
            UnitName = trimmedName,
            FormalName = dto.FormalName?.Trim() ?? string.Empty,
            DecimalPlaces = Math.Clamp(dto.DecimalPlaces, 0, 4),
            IsActive = true
        };

        await _context.Units.AddAsync(unit, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Created Unit '{UnitName}' for Company {CompanyId}", unit.UnitName, companyId);

        return new UnitDto
        {
            UnitId = unit.UnitId,
            CompanyId = unit.CompanyId,
            UnitName = unit.UnitName,
            FormalName = unit.FormalName,
            DecimalPlaces = unit.DecimalPlaces,
            IsActive = unit.IsActive
        };
    }

    public async Task<UnitDto> UpdateUnitAsync(int unitId, UnitUpdateDto dto, CancellationToken ct = default)
    {
        var unit = await _context.Units.FirstOrDefaultAsync(u => u.UnitId == unitId, ct);
        if (unit == null)
            throw new KeyNotFoundException($"Unit with ID {unitId} was not found.");

        if (string.IsNullOrWhiteSpace(dto.UnitName))
            throw new ArgumentException("Unit Symbol / Name is required.", nameof(dto.UnitName));

        var trimmedName = dto.UnitName.Trim();

        // Unique check excluding self
        bool exists = await _context.Units
            .AnyAsync(u => u.CompanyId == unit.CompanyId && u.UnitId != unitId && u.UnitName.ToLower() == trimmedName.ToLower(), ct);

        if (exists)
            throw new InvalidOperationException($"Another Unit with the symbol '{trimmedName}' already exists.");

        unit.UnitName = trimmedName;
        unit.FormalName = dto.FormalName?.Trim() ?? string.Empty;
        unit.DecimalPlaces = Math.Clamp(dto.DecimalPlaces, 0, 4);
        unit.IsActive = dto.IsActive;

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Updated Unit '{UnitName}' (ID {UnitId})", unit.UnitName, unitId);

        return new UnitDto
        {
            UnitId = unit.UnitId,
            CompanyId = unit.CompanyId,
            UnitName = unit.UnitName,
            FormalName = unit.FormalName,
            DecimalPlaces = unit.DecimalPlaces,
            IsActive = unit.IsActive
        };
    }

    public async Task<bool> DeleteUnitAsync(int unitId, CancellationToken ct = default)
    {
        var unit = await _context.Units.FirstOrDefaultAsync(u => u.UnitId == unitId, ct);
        if (unit == null) return false;

        // Check if any stock items reference this unit
        bool inUse = await _context.StockItems.AnyAsync(s => s.UnitId == unitId, ct);
        if (inUse)
            throw new InvalidOperationException($"Cannot delete Unit '{unit.UnitName}' because it is in use by one or more Stock Items.");

        _context.Units.Remove(unit);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted Unit '{UnitName}' (ID {UnitId})", unit.UnitName, unitId);
        return true;
    }

    #endregion

    #region Stock Items

    public async Task<IReadOnlyList<StockItemDto>> GetStockItemsByCompanyAsync(int companyId, CancellationToken ct = default)
    {
        return await _context.StockItems
            .AsNoTracking()
            .Include(s => s.Unit)
            .Where(s => s.CompanyId == companyId)
            .OrderBy(s => s.ItemName)
            .Select(s => new StockItemDto
            {
                StockItemId = s.StockItemId,
                CompanyId = s.CompanyId,
                ItemName = s.ItemName,
                UnitId = s.UnitId,
                UnitName = s.Unit != null ? s.Unit.UnitName : string.Empty,
                OpeningQuantity = s.OpeningQuantity,
                OpeningRate = s.OpeningRate,
                OpeningValue = s.OpeningValue,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<StockItemDto?> GetStockItemByIdAsync(int stockItemId, CancellationToken ct = default)
    {
        var item = await _context.StockItems
            .AsNoTracking()
            .Include(s => s.Unit)
            .FirstOrDefaultAsync(s => s.StockItemId == stockItemId, ct);

        if (item == null) return null;

        return new StockItemDto
        {
            StockItemId = item.StockItemId,
            CompanyId = item.CompanyId,
            ItemName = item.ItemName,
            UnitId = item.UnitId,
            UnitName = item.Unit != null ? item.Unit.UnitName : string.Empty,
            OpeningQuantity = item.OpeningQuantity,
            OpeningRate = item.OpeningRate,
            OpeningValue = item.OpeningValue,
            IsActive = item.IsActive,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    public async Task<StockItemDto> CreateStockItemAsync(int companyId, StockItemCreateDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.ItemName))
            throw new ArgumentException("Item Name is required.", nameof(dto.ItemName));

        var trimmedName = dto.ItemName.Trim();

        // Unique validation per company
        bool exists = await _context.StockItems
            .AnyAsync(s => s.CompanyId == companyId && s.ItemName.ToLower() == trimmedName.ToLower(), ct);

        if (exists)
            throw new InvalidOperationException($"A Stock Item named '{trimmedName}' already exists in this company.");

        if (dto.UnitId.HasValue)
        {
            bool unitValid = await _context.Units.AnyAsync(u => u.CompanyId == companyId && u.UnitId == dto.UnitId.Value, ct);
            if (!unitValid)
                throw new InvalidOperationException("Selected Unit of Measure is invalid.");
        }

        decimal openingVal = dto.OpeningValue > 0
            ? Math.Round(dto.OpeningValue, 2)
            : Math.Round(dto.OpeningQuantity * dto.OpeningRate, 2);

        var item = new StockItem
        {
            CompanyId = companyId,
            ItemName = trimmedName,
            UnitId = dto.UnitId,
            OpeningQuantity = dto.OpeningQuantity,
            OpeningRate = dto.OpeningRate,
            OpeningValue = openingVal,
            IsActive = true,
            CreatedAt = DateTime.Now
        };

        await _context.StockItems.AddAsync(item, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Created Stock Item '{ItemName}' for Company {CompanyId}", item.ItemName, companyId);

        string unitName = string.Empty;
        if (item.UnitId.HasValue)
        {
            unitName = await _context.Units
                .Where(u => u.UnitId == item.UnitId.Value)
                .Select(u => u.UnitName)
                .FirstOrDefaultAsync(ct) ?? string.Empty;
        }

        return new StockItemDto
        {
            StockItemId = item.StockItemId,
            CompanyId = item.CompanyId,
            ItemName = item.ItemName,
            UnitId = item.UnitId,
            UnitName = unitName,
            OpeningQuantity = item.OpeningQuantity,
            OpeningRate = item.OpeningRate,
            OpeningValue = item.OpeningValue,
            IsActive = item.IsActive,
            CreatedAt = item.CreatedAt
        };
    }

    public async Task<StockItemDto> UpdateStockItemAsync(int stockItemId, StockItemUpdateDto dto, CancellationToken ct = default)
    {
        var item = await _context.StockItems
            .Include(s => s.Unit)
            .FirstOrDefaultAsync(s => s.StockItemId == stockItemId, ct);

        if (item == null)
            throw new KeyNotFoundException($"Stock Item with ID {stockItemId} was not found.");

        if (string.IsNullOrWhiteSpace(dto.ItemName))
            throw new ArgumentException("Item Name is required.", nameof(dto.ItemName));

        var trimmedName = dto.ItemName.Trim();

        // Unique check excluding self
        bool exists = await _context.StockItems
            .AnyAsync(s => s.CompanyId == item.CompanyId && s.StockItemId != stockItemId && s.ItemName.ToLower() == trimmedName.ToLower(), ct);

        if (exists)
            throw new InvalidOperationException($"Another Stock Item named '{trimmedName}' already exists.");

        if (dto.UnitId.HasValue)
        {
            bool unitValid = await _context.Units.AnyAsync(u => u.CompanyId == item.CompanyId && u.UnitId == dto.UnitId.Value, ct);
            if (!unitValid)
                throw new InvalidOperationException("Selected Unit of Measure is invalid.");
        }

        decimal openingVal = dto.OpeningValue > 0
            ? Math.Round(dto.OpeningValue, 2)
            : Math.Round(dto.OpeningQuantity * dto.OpeningRate, 2);

        item.ItemName = trimmedName;
        item.UnitId = dto.UnitId;
        item.OpeningQuantity = dto.OpeningQuantity;
        item.OpeningRate = dto.OpeningRate;
        item.OpeningValue = openingVal;
        item.IsActive = dto.IsActive;
        item.UpdatedAt = DateTime.Now;

        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Updated Stock Item '{ItemName}' (ID {StockItemId})", item.ItemName, stockItemId);

        string unitName = string.Empty;
        if (item.UnitId.HasValue)
        {
            unitName = await _context.Units
                .Where(u => u.UnitId == item.UnitId.Value)
                .Select(u => u.UnitName)
                .FirstOrDefaultAsync(ct) ?? string.Empty;
        }

        return new StockItemDto
        {
            StockItemId = item.StockItemId,
            CompanyId = item.CompanyId,
            ItemName = item.ItemName,
            UnitId = item.UnitId,
            UnitName = unitName,
            OpeningQuantity = item.OpeningQuantity,
            OpeningRate = item.OpeningRate,
            OpeningValue = item.OpeningValue,
            IsActive = item.IsActive,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    public async Task<bool> DeleteStockItemAsync(int stockItemId, CancellationToken ct = default)
    {
        var item = await _context.StockItems.FirstOrDefaultAsync(s => s.StockItemId == stockItemId, ct);
        if (item == null) return false;

        _context.StockItems.Remove(item);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted Stock Item '{ItemName}' (ID {StockItemId})", item.ItemName, stockItemId);
        return true;
    }

    #endregion

    #region Stock Summary

    public async Task<StockSummaryReportDto> GetStockSummaryAsync(int companyId, DateTime? asOfDate = null, CancellationToken ct = default)
    {
        var date = asOfDate ?? DateTime.Today;

        var items = await _context.StockItems
            .AsNoTracking()
            .Include(s => s.Unit)
            .Where(s => s.CompanyId == companyId && s.IsActive)
            .OrderBy(s => s.ItemName)
            .ToListAsync(ct);

        var summaryItems = new List<StockSummaryItemDto>();

        foreach (var item in items)
        {
            // For basic inventory, Closing Stock = Opening Stock (unless transaction movements occur)
            decimal closingQty = item.OpeningQuantity;
            decimal closingRate = item.OpeningRate;
            decimal closingVal = item.OpeningValue;

            summaryItems.Add(new StockSummaryItemDto
            {
                StockItemId = item.StockItemId,
                ItemName = item.ItemName,
                UnitName = item.Unit != null ? item.Unit.UnitName : string.Empty,
                OpeningQuantity = item.OpeningQuantity,
                OpeningRate = item.OpeningRate,
                OpeningValue = item.OpeningValue,
                InwardsQuantity = 0m,
                OutwardsQuantity = 0m,
                ClosingQuantity = closingQty,
                ClosingRate = closingRate,
                ClosingValue = closingVal
            });
        }

        return new StockSummaryReportDto
        {
            CompanyId = companyId,
            AsOfDate = date,
            Items = summaryItems
        };
    }

    #endregion
}
