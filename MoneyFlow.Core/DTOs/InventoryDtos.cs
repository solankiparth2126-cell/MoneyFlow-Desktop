using System;
using System.Collections.Generic;

namespace MoneyFlow.Core.DTOs;

public class UnitDto
{
    public int UnitId { get; set; }
    public int CompanyId { get; set; }
    public string UnitName { get; set; } = string.Empty; // e.g. "Nos", "Kg", "Box"
    public string FormalName { get; set; } = string.Empty; // e.g. "Numbers", "Kilograms"
    public int DecimalPlaces { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UnitCreateDto
{
    public string UnitName { get; set; } = string.Empty;
    public string FormalName { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; } = 0;
}

public class UnitUpdateDto
{
    public string UnitName { get; set; } = string.Empty;
    public string FormalName { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; }
    public bool IsActive { get; set; } = true;
}

public class StockItemDto
{
    public int StockItemId { get; set; }
    public int CompanyId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int? UnitId { get; set; }
    public string UnitName { get; set; } = string.Empty;
    public decimal OpeningQuantity { get; set; }
    public decimal OpeningRate { get; set; }
    public decimal OpeningValue { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public string FormattedOpeningStock =>
        $"{OpeningQuantity:N2} {UnitName} @ ₹{OpeningRate:N2} = ₹{OpeningValue:N2}";
}

public class StockItemCreateDto
{
    public string ItemName { get; set; } = string.Empty;
    public int? UnitId { get; set; }
    public decimal OpeningQuantity { get; set; }
    public decimal OpeningRate { get; set; }
    public decimal OpeningValue { get; set; }
}

public class StockItemUpdateDto
{
    public string ItemName { get; set; } = string.Empty;
    public int? UnitId { get; set; }
    public decimal OpeningQuantity { get; set; }
    public decimal OpeningRate { get; set; }
    public decimal OpeningValue { get; set; }
    public bool IsActive { get; set; } = true;
}

public class StockSummaryItemDto
{
    public int StockItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string UnitName { get; set; } = string.Empty;
    public decimal OpeningQuantity { get; set; }
    public decimal OpeningRate { get; set; }
    public decimal OpeningValue { get; set; }
    public decimal InwardsQuantity { get; set; }
    public decimal OutwardsQuantity { get; set; }
    public decimal ClosingQuantity { get; set; }
    public decimal ClosingRate { get; set; }
    public decimal ClosingValue { get; set; }
}

public class StockSummaryReportDto
{
    public int CompanyId { get; set; }
    public DateTime AsOfDate { get; set; }
    public List<StockSummaryItemDto> Items { get; set; } = new();
    public decimal TotalOpeningValue => Items.Sum(i => i.OpeningValue);
    public decimal TotalClosingValue => Items.Sum(i => i.ClosingValue);
}
