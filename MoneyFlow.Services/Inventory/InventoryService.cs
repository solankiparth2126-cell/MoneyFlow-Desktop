using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MoneyFlow.Core.DTOs;
using MoneyFlow.Core.Entities;
using MoneyFlow.Core.Interfaces;

namespace MoneyFlow.Services.Inventory;

public class InventoryService : IInventoryService
{
    private readonly IRepository<Unit> _unitRepo;
    private readonly IRepository<StockItem> _stockItemRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<InventoryService> _logger;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public InventoryService(
        IRepository<Unit> unitRepo,
        IRepository<StockItem> stockItemRepo,
        IUnitOfWork unitOfWork,
        ILogger<InventoryService> logger)
    {
        _unitRepo = unitRepo;
        _stockItemRepo = stockItemRepo;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public InventoryService(
        IRepository<Unit> unitRepo,
        IRepository<StockItem> stockItemRepo,
        IUnitOfWork unitOfWork)
        : this(unitRepo, stockItemRepo, unitOfWork, Microsoft.Extensions.Logging.Abstractions.NullLogger<InventoryService>.Instance)
    {
    }

    internal InventoryService(
        MoneyFlow.Data.AppDbContext context,
        IUnitOfWork unitOfWork,
        ILogger<InventoryService> logger)
        : this(
            new MoneyFlow.Data.Repositories.Repository<Unit>(context),
            new MoneyFlow.Data.Repositories.Repository<StockItem>(context),
            unitOfWork,
            logger)
    {
    }

    #region Units of Measure

    public async Task<IReadOnlyList<UnitDto>> GetUnitsByCompanyAsync(int companyId, CancellationToken ct = default)
    {
        var units = await _unitRepo.FindAsync(u => u.CompanyId == companyId, ct);
        return units
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
            .ToList();
    }

    public async Task<UnitDto?> GetUnitByIdAsync(int unitId, CancellationToken ct = default)
    {
        var unit = await _unitRepo.GetByIdAsync(unitId, ct);
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

        bool exists = await _unitRepo.AnyAsync(u => u.CompanyId == companyId && u.UnitName.ToLower() == trimmedName.ToLower(), ct);
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

        await _unitRepo.AddAsync(unit, ct);
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
        var unit = await _unitRepo.GetByIdAsync(unitId, ct);
        if (unit == null)
            throw new KeyNotFoundException($"Unit with ID {unitId} was not found.");

        if (string.IsNullOrWhiteSpace(dto.UnitName))
            throw new ArgumentException("Unit Symbol / Name is required.", nameof(dto.UnitName));

        var trimmedName = dto.UnitName.Trim();

        bool exists = await _unitRepo.AnyAsync(u => u.CompanyId == unit.CompanyId && u.UnitId != unitId && u.UnitName.ToLower() == trimmedName.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"Another Unit with the symbol '{trimmedName}' already exists.");

        unit.UnitName = trimmedName;
        unit.FormalName = dto.FormalName?.Trim() ?? string.Empty;
        unit.DecimalPlaces = Math.Clamp(dto.DecimalPlaces, 0, 4);
        unit.IsActive = dto.IsActive;

        _unitRepo.Update(unit);
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
        var unit = await _unitRepo.GetByIdAsync(unitId, ct);
        if (unit == null) return false;

        bool inUse = await _stockItemRepo.AnyAsync(s => s.UnitId == unitId, ct);
        if (inUse)
            throw new InvalidOperationException($"Cannot delete Unit '{unit.UnitName}' because it is in use by one or more Stock Items.");

        _unitRepo.Delete(unit);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted Unit '{UnitName}' (ID {UnitId})", unit.UnitName, unitId);
        return true;
    }

    #endregion

    #region Stock Items

    public async Task<IReadOnlyList<StockItemDto>> GetStockItemsByCompanyAsync(int companyId, CancellationToken ct = default)
    {
        var items = await _stockItemRepo.FindAsync(s => s.CompanyId == companyId, ct);
        var units = (await _unitRepo.FindAsync(u => u.CompanyId == companyId, ct)).ToDictionary(u => u.UnitId);

        return items
            .OrderBy(s => s.ItemName)
            .Select(s => new StockItemDto
            {
                StockItemId = s.StockItemId,
                CompanyId = s.CompanyId,
                ItemName = s.ItemName,
                UnitId = s.UnitId,
                UnitName = s.UnitId.HasValue && units.TryGetValue(s.UnitId.Value, out var u) ? u.UnitName : string.Empty,
                OpeningQuantity = s.OpeningQuantity,
                OpeningRate = s.OpeningRate,
                OpeningValue = s.OpeningValue,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt
            })
            .ToList();
    }

    public async Task<StockItemDto?> GetStockItemByIdAsync(int stockItemId, CancellationToken ct = default)
    {
        var item = await _stockItemRepo.GetByIdAsync(stockItemId, ct);
        if (item == null) return null;

        Unit? unit = item.UnitId.HasValue ? await _unitRepo.GetByIdAsync(item.UnitId.Value, ct) : null;

        return new StockItemDto
        {
            StockItemId = item.StockItemId,
            CompanyId = item.CompanyId,
            ItemName = item.ItemName,
            UnitId = item.UnitId,
            UnitName = unit?.UnitName ?? string.Empty,
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

        bool exists = await _stockItemRepo.AnyAsync(s => s.CompanyId == companyId && s.ItemName.ToLower() == trimmedName.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"A Stock Item named '{trimmedName}' already exists in this company.");

        if (dto.UnitId.HasValue)
        {
            bool unitValid = await _unitRepo.AnyAsync(u => u.CompanyId == companyId && u.UnitId == dto.UnitId.Value, ct);
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
            CreatedAt = DateTime.UtcNow
        };

        await _stockItemRepo.AddAsync(item, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Created Stock Item '{ItemName}' for Company {CompanyId}", item.ItemName, companyId);

        Unit? unit = item.UnitId.HasValue ? await _unitRepo.GetByIdAsync(item.UnitId.Value, ct) : null;

        return new StockItemDto
        {
            StockItemId = item.StockItemId,
            CompanyId = item.CompanyId,
            ItemName = item.ItemName,
            UnitId = item.UnitId,
            UnitName = unit?.UnitName ?? string.Empty,
            OpeningQuantity = item.OpeningQuantity,
            OpeningRate = item.OpeningRate,
            OpeningValue = item.OpeningValue,
            IsActive = item.IsActive,
            CreatedAt = item.CreatedAt
        };
    }

    public async Task<StockItemDto> UpdateStockItemAsync(int stockItemId, StockItemUpdateDto dto, CancellationToken ct = default)
    {
        var item = await _stockItemRepo.GetByIdAsync(stockItemId, ct);
        if (item == null)
            throw new KeyNotFoundException($"Stock Item with ID {stockItemId} was not found.");

        if (string.IsNullOrWhiteSpace(dto.ItemName))
            throw new ArgumentException("Item Name is required.", nameof(dto.ItemName));

        var trimmedName = dto.ItemName.Trim();

        bool exists = await _stockItemRepo.AnyAsync(s => s.CompanyId == item.CompanyId && s.StockItemId != stockItemId && s.ItemName.ToLower() == trimmedName.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"Another Stock Item named '{trimmedName}' already exists.");

        if (dto.UnitId.HasValue)
        {
            bool unitValid = await _unitRepo.AnyAsync(u => u.CompanyId == item.CompanyId && u.UnitId == dto.UnitId.Value, ct);
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
        item.UpdatedAt = DateTime.UtcNow;

        _stockItemRepo.Update(item);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Updated Stock Item '{ItemName}' (ID {StockItemId})", item.ItemName, stockItemId);

        Unit? unit = item.UnitId.HasValue ? await _unitRepo.GetByIdAsync(item.UnitId.Value, ct) : null;

        return new StockItemDto
        {
            StockItemId = item.StockItemId,
            CompanyId = item.CompanyId,
            ItemName = item.ItemName,
            UnitId = item.UnitId,
            UnitName = unit?.UnitName ?? string.Empty,
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
        var item = await _stockItemRepo.GetByIdAsync(stockItemId, ct);
        if (item == null) return false;

        _stockItemRepo.Delete(item);
        await _unitOfWork.SaveChangesAsync(ct);

        _logger.LogInformation("Deleted Stock Item '{ItemName}' (ID {StockItemId})", item.ItemName, stockItemId);
        return true;
    }

    #endregion

    #region Stock Summary

    public async Task<StockSummaryReportDto> GetStockSummaryAsync(int companyId, DateTime? asOfDate = null, CancellationToken ct = default)
    {
        var date = asOfDate ?? DateTime.Today;

        var items = (await _stockItemRepo.FindAsync(s => s.CompanyId == companyId && s.IsActive, ct)).OrderBy(s => s.ItemName).ToList();
        var units = (await _unitRepo.FindAsync(u => u.CompanyId == companyId, ct)).ToDictionary(u => u.UnitId);

        var summaryItems = new List<StockSummaryItemDto>();

        foreach (var item in items)
        {
            decimal closingQty = item.OpeningQuantity;
            decimal closingRate = item.OpeningRate;
            decimal closingVal = item.OpeningValue;

            string unitName = item.UnitId.HasValue && units.TryGetValue(item.UnitId.Value, out var u) ? u.UnitName : string.Empty;

            summaryItems.Add(new StockSummaryItemDto
            {
                StockItemId = item.StockItemId,
                ItemName = item.ItemName,
                UnitName = unitName,
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
